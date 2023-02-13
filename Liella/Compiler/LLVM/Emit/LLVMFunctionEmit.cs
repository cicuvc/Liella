using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

using LLVMInterop = LLVMSharp.Interop.LLVM;

namespace Liella.Compiler.LLVM.Emit {
    public partial class LLVMILEmit {
        [ILCodeHandler(ILOpCode.Ldftn)]
        public void LoadFunctionAddress(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var llvmFunction = IRHelper.GetMethodInfo((uint)operand, context, m_Builder, out var llvmType, out _);
            var ptrType = LLVMCompType.Int8.ToPointerType();
            var ptrValue = m_Builder.BuildBitCast(llvmFunction.Function, ptrType.LLVMType);
            evalStack.Push(LLVMCompValue.CreateValue(ptrValue, ptrType));
        }
        [ILCodeHandler(ILOpCode.Calli)]
        public void CallIndirect(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var signatureToken = (StandaloneSignatureHandle)MetadataHelper.CreateHandle((uint)operand);
            var signature = context.Method.ResolveSignatureToken(signatureToken);
            var callSite = signature.DecodeMethodSignature(context.Method.TypeEnv.SignatureDecoder, context.Method);
            var argType = new LLVMTypeRef[callSite.ParameterTypes.Length];
            var argValue = new LLVMValueRef[callSite.ParameterTypes.Length];
            var functionPtr = evalStack.Pop();
            for (var i = callSite.ParameterTypes.Length - 1; i >= 0; i--) {
                var paramType = context.Context.ResolveLLVMInstanceType(callSite.ParameterTypes[i]);
                argType[i] = paramType.LLVMType;
                argValue[i] = evalStack.Pop().TryCast(paramType, m_Builder).Value;
            }
            var retType = context.Context.ResolveLLVMInstanceType(callSite.ReturnType);
            var funcType = LLVMTypeRef.CreatePointer(LLVMTypeRef.CreateFunction(retType.LLVMType, argType), 0);
            functionPtr = functionPtr.TryCast(LLVMCompType.CreateType(LLVMTypeTag.Pointer, funcType), m_Builder);

            var result = m_Builder.BuildCall2(funcType.ElementType, functionPtr.Value, argValue);
            if (retType.LLVMType != LLVMTypeRef.Void) evalStack.Push(LLVMCompValue.CreateValue(result, retType));
        }
        [ILCodeHandler(ILOpCode.Callvirt)]
        public void CallVirtual(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;
            var callvirtTypeHint = m_CallvirtTypeHint;

            var targetMethod = IRHelper.GetMethodInfo((uint)operand, context, m_Builder, out var declType, out _);

            var argumentList = new LLVMValueRef[targetMethod.ParamCount];
            for (var i = targetMethod.ParamCount - 1; i >= 1; i--) {
                var argValue = evalStack.Pop();
                argumentList[i] = argValue.TryCast(targetMethod.ParamTypes[i], m_Builder, context.Context).Value;
            }
            var pthis = evalStack.Pop();
            argumentList[0] = pthis.TryCast(targetMethod.ParamTypes[0], m_Builder).Value;

            // target is not virtual
            if (!targetMethod.Method.Attributes.HasFlag(MethodAttributes.Virtual)) {
                IRHelper.MakeCall(targetMethod, targetMethod.Function, argumentList, m_Builder, evalStack);
                return;
            }

            // delegate invoke
            var delegateBaseType = (LLVMClassTypeInfo)context.Context.ResolveLLVMType(context.Context.TypeEnvironment.IntrinicsTypes["System::MulticastDelegate"]);
            if (delegateBaseType == declType.BaseType) {
                IRHelper.MakeCall(targetMethod, targetMethod.Function, argumentList, m_Builder, evalStack);
                return;
            }

            // constrained
            var skipLookup = false;
            if (callvirtTypeHint != null) {
                if (callvirtTypeHint.MetadataType.IsValueType) {
                    foreach (var i in callvirtTypeHint.MetadataType.Methods) {
                        if (i.Value.Entry.Name == targetMethod.Method.Entry.Name && i.Value.EqualsSignature(targetMethod.Method)) {
                            skipLookup = true;
                            var llvmMethod = context.Context.ResolveLLVMMethod(i.Value.Entry);
                            argumentList[0] = pthis.TryCast(llvmMethod.ParamTypes[0], m_Builder).Value;

                            IRHelper.MakeCall(targetMethod, llvmMethod.Function, argumentList, m_Builder, evalStack);
                            break;
                        }
                    }
                    if (!skipLookup) {
                        // should be boxed
                        throw new NotImplementedException();
                    }
                } else {
                    // should dereference pthis
                    throw new NotImplementedException();
                }
                callvirtTypeHint = null;
                if (skipLookup) return;
            }

            var vtableIndex = 0;
            var realPtrThis = pthis.Value;
            LLVMValueRef targetFunction = default;

            var runtimeHelpers = context.Context.ResolveLLVMType(context.Context.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::RuntimeHelpers"]);
            var lookupInterfaceVtable = context.Context.ResolveLLVMMethod(runtimeHelpers.MetadataType.FindMethodByName("LookupInterfaceVtable").Entry);
            var castToInterface = context.Context.ResolveLLVMMethod(runtimeHelpers.MetadataType.FindMethodByName("CastToInterface").Entry);

            vtableIndex = declType.LocateMethodInMainTable(targetMethod);

            if (declType is LLVMClassTypeInfo) {
                if (pthis.Type.TypeTag.HasFlag(LLVMTypeTag.Interface)) {
                    // call heleprs
                    throw new InvalidProgramException();
                } else {
                    // class to class
                    targetFunction = IRHelper.LookupVtable(realPtrThis, vtableIndex, declType.VtableType.StructElementTypes[0], m_Builder);
                    argumentList[0] = pthis.TryCast(targetMethod.ParamTypes[0], m_Builder).Value;
                }
            } else {
                if (pthis.Type.TypeTag.HasFlag(LLVMTypeTag.Interface)) {
                    // interface target => interface method
                    var llvmType = (LLVMInterfaceTypeInfo)context.Context.ResolveLLVMTypeFromTypeRef(pthis.Type);
                    if (llvmType.Interfaces.Contains(declType) || llvmType == declType) {
                        vtableIndex = llvmType.LocateMethodInMainTable(targetMethod);

                        var vtableOffset = LLVMValueRef.CreateConstPtrToInt(
                            LLVMValueRef.CreateConstGEP(LLVMValueRef.CreateConstNull(LLVMTypeRef.CreatePointer(llvmType.VtableType, 0)),
                            new LLVMValueRef[] {
                                            LLVMHelpers.CreateConstU32(0),
                                            LLVMHelpers.CreateConstU32(vtableIndex + 1) // skip interface header
                            })
                            , LLVMTypeRef.Int32);

                        var ptrMaskFunc = LLVMHelpers.GetIntrinsicFunction(context.Context.Module, "llvm.ptrmask", new LLVMTypeRef[] {
                                        declType.InstanceType.LLVMType,
                                        LLVMTypeRef.Int64
                                    });
                        var realPthis = m_Builder.BuildCall2(ptrMaskFunc.TypeOf.ElementType, ptrMaskFunc, new LLVMValueRef[] {
                                        pthis.TryCast(declType.InstanceType,m_Builder).Value,
                                        LLVMHelpers.CreateConstU64(0xFFFFFFFFFFFF)
                                    });

                        targetFunction = m_Builder.BuildBitCast(
                            m_Builder.BuildCall2(lookupInterfaceVtable.FunctionType, lookupInterfaceVtable.Function, new LLVMValueRef[] {
                                            pthis.TryCast(lookupInterfaceVtable.ParamTypes[0],m_Builder).Value,
                                            m_Builder.BuildBitCast(realPthis,lookupInterfaceVtable.ParamTypes[1].LLVMType),
                                            vtableOffset
                            }),
                            targetMethod.FunctionPtrType);

                        argumentList[0] = m_Builder.BuildBitCast(realPthis, targetMethod.ParamTypes[0].LLVMType);
                    } else {
                        throw new NotImplementedException();
                    }
                } else {
                    // class instance => interface method
                    throw new NotImplementedException();
                }
            }


            IRHelper.MakeCall(targetMethod, targetFunction, argumentList, m_Builder, evalStack);
        }
        [ILCodeHandler(ILOpCode.Call)]
        public unsafe void Call(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var targetMethod = IRHelper.GetMethodInfo((uint)operand, context, m_Builder, out var declType, out var callSiteSig);
            var callSiteParamCount = callSiteSig.ParameterTypes.Length + (targetMethod.Method.Attributes.HasFlag(MethodAttributes.Static) ? 0 : 1);
            var defParams = targetMethod.ParamCount;
            var argumentList = new LLVMValueRef[callSiteParamCount];

            var runtimeHelpers = context.Context.ResolveLLVMType(context.Context.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::RuntimeHelpers"]);
            var toUTF16String = context.Context.ResolveLLVMMethod(runtimeHelpers.MetadataType.FindMethodByName("ToUTF16String").Entry);

            if (targetMethod == toUTF16String) {
                var stringValue = context.Context.GetInternedString(evalStack.Pop().Value);
                var buffer = new char[stringValue.Length + 1];
                stringValue.CopyTo(0, buffer, 0, stringValue.Length);
                fixed (char* pString = buffer) {

                    var utf16String = (LLVMValueRef)LLVMInterop.ConstStringInContext(context.Context.Context, (sbyte*)pString, (uint)buffer.Length * 2, 1);
                    var utf16Global = context.Context.Module.AddGlobal(utf16String.TypeOf, $"U16_{buffer.GetHashCode()}");
                    utf16Global.IsGlobalConstant = true;
                    utf16Global.Initializer = utf16String;

                    evalStack.Push(LLVMCompValue.CreateValue(utf16Global, targetMethod.ReturnType));
                    return;
                }
            }

            if (targetMethod.Method.Signature.Header.CallingConvention.HasFlag(SignatureCallingConvention.VarArgs)) {
                // va args
                for (var i = callSiteParamCount - 1; i >= defParams; i--) {
                    var paramValue = evalStack.Pop();
                    var paramType = context.Context.ResolveLLVMInstanceType(callSiteSig.ParameterTypes[i]);
                    argumentList[i] = paramValue.TryCast(paramType, m_Builder).Value;
                }

                var asmHelpers = context.Context.ResolveLLVMType(context.Context.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::UnsafeAsm"]);


                if (targetMethod.DeclType == asmHelpers) {
                    var constrainString = context.Context.GetInternedString(evalStack.Pop().Value);
                    var asmCode = context.Context.GetInternedString(evalStack.Pop().Value);

                    var newArgList = new LLVMValueRef[callSiteParamCount - defParams];
                    for (var i = defParams; i < callSiteParamCount; i++) newArgList[i - defParams] = argumentList[i];
                    var asmTypes = newArgList.Select(e => e.TypeOf).ToArray();
                    var asmFuncType = LLVMTypeRef.CreateFunction(targetMethod.ReturnType.LLVMType, asmTypes);
                    var asmStmt = LLVMValueRef.CreateConstInlineAsm(asmFuncType, asmCode, constrainString, true, false);
                    var asmResult = m_Builder.BuildCall(asmStmt, newArgList);
                    if (targetMethod.ReturnType.LLVMType != LLVMTypeRef.Void) {
                        evalStack.Push(LLVMCompValue.CreateValue(asmResult, targetMethod.ReturnType));
                    }

                    return;
                }
            }

            for (var i = defParams - 1; i >= 0; i--) {
                argumentList[i] = evalStack.Pop().TryCast(targetMethod.ParamTypes[i], m_Builder, context.Context).Value;
            }
            var callReturn = m_Builder.BuildCall2(targetMethod.FunctionType, targetMethod.Function, argumentList);
            if (targetMethod.ReturnType.LLVMType != LLVMTypeRef.Void)
                evalStack.Push(LLVMCompValue.CreateValue(callReturn, targetMethod.ReturnType));
        }
    }
}
