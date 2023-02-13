using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Liella.Compiler.LLVM;
using Liella.Metadata;
using Liella.MSIL;
using LLVMSharp;
using LLVMSharp.Interop;


namespace Liella.Compiler {
    
    
    
    
    
   
    
    
    public abstract class LLVMCompileTimeFunctionDef {
        public abstract bool MatchFunction(LLVMMethodInfoWrapper method);
        public abstract void EmitInst(LLVMMethodInfoWrapper context,Stack<LLVMCompValue> evalStack, LLVMBuilderRef builder);
    }

    public abstract class LLVMIntrinsicFunctionDef {
        public abstract bool MatchFunction(LLVMMethodInfoWrapper method);
        public abstract void FillFunctionBody(LLVMMethodInfoWrapper method,LLVMBuilderRef builder);
    }
    public class LLVMUnsafeDerefInvariant : LLVMIntrinsicFunctionDef {
        public override void FillFunctionBody(LLVMMethodInfoWrapper method, LLVMBuilderRef builder) {
            var mainBlock = method.Function.AppendBasicBlock("Block0");
            builder.PositionAtEnd(mainBlock);
            var val = builder.BuildLoad(method.Function.GetParam(0));
            LLVMHelpers.AddMetadataForInst(val, "invariant.load", new LLVMValueRef[] { });
            builder.BuildRet(val);
        }

        public override bool MatchFunction(LLVMMethodInfoWrapper method) {
            return method.Method.Entry.ToString().StartsWith("System.Runtime.CompilerServices::Unsafe.DereferenceInvariant<");
        }
    }
    public class LLVMUnsafeDerefInvariantIndex : LLVMIntrinsicFunctionDef {
        public override void FillFunctionBody(LLVMMethodInfoWrapper method, LLVMBuilderRef builder) {
            var mainBlock = method.Function.AppendBasicBlock("Block0");
            builder.PositionAtEnd(mainBlock);
            var parma0 = method.Function.GetParam(0);
            var ptrAddr = builder.BuildGEP(parma0, new LLVMValueRef[] { method.Function.GetParam(1) });
            var val = builder.BuildLoad(ptrAddr);
            LLVMHelpers.AddMetadataForInst(val, "invariant.load",new LLVMValueRef[] { });
            builder.BuildRet(val);
        }

        public override bool MatchFunction(LLVMMethodInfoWrapper method) {
            return method.Method.Entry.ToString().StartsWith("System.Runtime.CompilerServices::Unsafe.DereferenceInvariantIndex<");
        }
    }
    public class LLVMUnsafeAsPtr : LLVMIntrinsicFunctionDef {
        public override void FillFunctionBody(LLVMMethodInfoWrapper method, LLVMBuilderRef builder) {
            var mainBlock = method.Function.AppendBasicBlock("Block0");
            builder.PositionAtEnd(mainBlock);
            
            var val = builder.BuildPtrToInt(method.Function.GetParam(0), LLVMTypeRef.Int64);
            builder.BuildRet(val);
        }

        public override bool MatchFunction(LLVMMethodInfoWrapper method) {
            return method.Method.Entry.ToString().StartsWith("System.Runtime.CompilerServices::Unsafe.AsPtr<");
        }
    }
    public class LLVMPInvoke : LLVMIntrinsicFunctionDef {
        public override void FillFunctionBody(LLVMMethodInfoWrapper method, LLVMBuilderRef builder) {
            var llvmFunction = method.Function;
            llvmFunction.Name = method.Method.Entry.Name;
            llvmFunction.Linkage = LLVMLinkage.LLVMExternalLinkage;
            llvmFunction.FunctionCallConv = (uint)LLVMCallConv.LLVMWin64CallConv;
        }

        public override bool MatchFunction(LLVMMethodInfoWrapper method) {
            return method.Method.Attributes.HasFlag(MethodAttributes.PinvokeImpl);
        }
    }
    public class LLVMRuntimeExport : LLVMIntrinsicFunctionDef {
        public override void FillFunctionBody(LLVMMethodInfoWrapper method, LLVMBuilderRef builder) {
            var value = method.Method.CustomAttributes[(method.Context.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::RuntimeExport"])].DecodeValue(method.Context.TypeEnvironment.SignatureDecoder);
            var llvmFunction = method.Function;
            llvmFunction.Name = (string)value.FixedArguments[0].Value;
        }

        public override bool MatchFunction(LLVMMethodInfoWrapper method) {
            return method.Method.CustomAttributes.ContainsKey(method.Context.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::RuntimeExport"]);
        }
    }
    public class LLVMDelegate : LLVMIntrinsicFunctionDef {
        public override void FillFunctionBody(LLVMMethodInfoWrapper method, LLVMBuilderRef builder) {
            var delegateType = (LLVMClassTypeInfo)method.Context.ResolveLLVMType(method.Context.TypeEnvironment.IntrinicsTypes["System::Delegate"]);
            switch (method.Method.Entry.Name) {
                case "Invoke": {
                    var basicBlock = method.Function.AppendBasicBlock("Block0");
                    builder.PositionAtEnd(basicBlock);

                    var delegateDataPtr = builder.BuildBitCast(method.Function.GetParam(0), delegateType.HeapPtrType.LLVMType);
                    var instancePtr = builder.BuildGEP(delegateDataPtr, new LLVMValueRef[] {
                        LLVMHelpers.CreateConstU32(0),
                        LLVMHelpers.CreateConstU32(1)
                    });
                    var instance = builder.BuildLoad(instancePtr);
                    var funcPtr = builder.BuildGEP(delegateDataPtr, new LLVMValueRef[] {
                        LLVMHelpers.CreateConstU32(0),
                        LLVMHelpers.CreateConstU32(2)
                    });
                    var funcValue = builder.BuildLoad(funcPtr);
                    var targetDelegateFunction = builder.BuildBitCast(funcValue, method.FunctionPtrType);
                    var argumentList = new LLVMValueRef[method.ParamCount];
                    for (var i = 1u; i < method.ParamCount; i++) argumentList[i] = method.Function.GetParam(i);
                    argumentList[0] = builder.BuildBitCast(instance, method.ParamTypes[0].LLVMType);

                    var result = builder.BuildCall2(targetDelegateFunction.TypeOf.ElementType,targetDelegateFunction, argumentList);
                    if (method.ReturnType.LLVMType != LLVMTypeRef.Void) {
                        builder.BuildRet(result);
                    } else {
                        builder.BuildRetVoid();
                    }
                    break;
                }
                case ".ctor": {

                    var basicBlock = method.Function.AppendBasicBlock("Block0");
                    builder.PositionAtEnd(basicBlock);

                    var delegateDataPtr = builder.BuildBitCast(method.Function.GetParam(0), delegateType.HeapPtrType.LLVMType);
                    var instancePtr = builder.BuildGEP(delegateDataPtr, new LLVMValueRef[] {
                        LLVMHelpers.CreateConstU32(0),
                        LLVMHelpers.CreateConstU32(1)
                    });
                    builder.BuildStore(method.Function.GetParam(1), instancePtr);
                    var funcPtr = builder.BuildGEP(delegateDataPtr, new LLVMValueRef[] {
                        LLVMHelpers.CreateConstU32(0),
                        LLVMHelpers.CreateConstU32(2)
                    });
                    builder.BuildStore(method.Function.GetParam(2), funcPtr);
                    builder.BuildRetVoid();
                    break;
                }
                default: {
                    throw new NotImplementedException();
                }
            }
            
        }

        public override bool MatchFunction(LLVMMethodInfoWrapper method) {
            return method.Method.DeclType.BaseType?.Entry == method.Method.TypeEnv.IntrinicsTypes["System::MulticastDelegate"];
        }
    }

    public class LLVMVaList : LLVMIntrinsicFunctionDef {
        public override void FillFunctionBody(LLVMMethodInfoWrapper method, LLVMBuilderRef builder) {
            var delegateType = (LLVMClassTypeInfo)method.Context.ResolveLLVMType(method.Context.TypeEnvironment.IntrinicsTypes["System::Delegate"]);
            switch (method.Method.Entry.Name) {
                case "GetList": {
                    var basicBlock = method.Function.AppendBasicBlock("Block0");
                    builder.PositionAtEnd(basicBlock);

                    var dstPtr = method.Function.GetParam(1);
                    var srcValue = builder.BuildBitCast(method.Function.GetParam(0),dstPtr.TypeOf);
                    
                    var copyFunc = LLVMHelpers.GetIntrinsicFunction(method.Context.Module, "llvm.va_copy", new LLVMTypeRef[] {
                    });
                    var i8p = LLVMCompType.Int8.ToPointerType().LLVMType;
                    builder.BuildCall2(copyFunc.TypeOf.ElementType, copyFunc, new LLVMValueRef[] { 
                        builder.BuildBitCast(dstPtr,i8p),
                        builder.BuildBitCast(srcValue,i8p)
                    });
                    builder.BuildRetVoid();
                    break;
                }
                case "GetNextValue": {

                    var basicBlock = method.Function.AppendBasicBlock("Block0");
                    builder.PositionAtEnd(basicBlock);


                    var ptrList = method.Function.GetParam(0);
                    var valType = method.ReturnType.LLVMType;
                    var value = builder.BuildVAArg(ptrList, valType);
                    builder.BuildRet(value);
                    break;
                }
                case "End": {
                    var basicBlock = method.Function.AppendBasicBlock("Block0");
                    builder.PositionAtEnd(basicBlock);

                    LLVMHelpers.AddAttributeForFunction(method.Context.Module, method.Function, "alwaysinline");

                    var ptrList = method.Function.GetParam(0);
                    var endVaFunc = LLVMHelpers.GetIntrinsicFunction(method.Context.Module, "llvm.va_end", new LLVMTypeRef[] { 
                    });
                    var i8p = LLVMCompType.Int8.ToPointerType().LLVMType;
                    builder.BuildCall(endVaFunc, new LLVMValueRef[] { 
                        builder.BuildBitCast(ptrList,i8p)
                    });
                    builder.BuildRetVoid();
                    break;
                }
                default: {
                    throw new NotImplementedException();
                }
            }

        }

        public override bool MatchFunction(LLVMMethodInfoWrapper method) {
            return method.Method.DeclType.Entry == method.Method.TypeEnv.IntrinicsTypes["System::RuntimeArgumentHandle"]
                || method.Method.DeclType.Entry == method.Method.TypeEnv.IntrinicsTypes["System::RuntimeVaList"];
        }
    }
    public class LLVMRuntimeHelpersIntrinsic : LLVMIntrinsicFunctionDef {
        public override void FillFunctionBody(LLVMMethodInfoWrapper method, LLVMBuilderRef builder) {
            var basicBlock = method.Function.AppendBasicBlock("Block0");
            builder.PositionAtEnd(basicBlock);

            var staticCtorList = method.Context.StaticConstructorList;
            foreach(var i in staticCtorList) {
                builder.BuildCall(i.Function, new LLVMValueRef[] { });
            }
            builder.BuildRetVoid();

        }

        public override bool MatchFunction(LLVMMethodInfoWrapper method) {
            return method.Method.Entry.ToString().StartsWith("System.Runtime.CompilerServices::RuntimeHelpers.RunStaticConstructors");
        }
    }



}
