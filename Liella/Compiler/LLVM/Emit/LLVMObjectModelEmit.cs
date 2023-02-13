using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM.Emit {
    public partial class LLVMILEmit {
        [ILCodeHandler(ILOpCode.Castclass)]
        public void CastClass(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var unkToken = MetadataHelper.CreateHandle((uint)operand);
            var targetType = context.Context.ResolveLLVMType(context.Method.ResolveTypeToken(unkToken));
            var srcObject = evalStack.Pop();

            var runtimeHelpers = context.Context.ResolveLLVMType(context.Context.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::RuntimeHelpers"]);
            var castBack = context.Context.ResolveLLVMMethod(runtimeHelpers.MetadataType.FindMethodByName("CastBack").Entry);
            var castToInterface = context.Context.ResolveLLVMMethod(runtimeHelpers.MetadataType.FindMethodByName("CastToInterface").Entry);

            if (targetType is LLVMClassTypeInfo) {
                if (srcObject.Type.TypeTag.HasFlag(LLVMTypeTag.Interface)) {
                    // interface to class
                    var dstObject = m_Builder.BuildCall2(castBack.FunctionType, castBack.Function, new LLVMValueRef[] {
                                    srcObject.TryCast(castBack.ParamTypes[0],m_Builder).Value
                                });
                    evalStack.Push(LLVMCompValue.CreateValue(dstObject, targetType.InstanceType));
                } else {
                    // class to class
                    evalStack.Push(LLVMCompValue.CreateValue(srcObject.Value, targetType.InstanceType));
                }
            } else {
                if (srcObject.Type.TypeTag.HasFlag(LLVMTypeTag.Interface)) {
                    // interface to interface. Should be avoid if possible
                    var rawObject = m_Builder.BuildCall2(castBack.FunctionType, castBack.Function, new LLVMValueRef[] {
                                    srcObject.TryCast(castBack.ParamTypes[0],m_Builder).Value
                                });
                    var dstObject = m_Builder.BuildCall2(castToInterface.FunctionType, castToInterface.Function, new LLVMValueRef[] {
                                    m_Builder.BuildBitCast(rawObject,castToInterface.ParamTypes[0].LLVMType),
                                    LLVMHelpers.CreateConstU32(targetType.TypeHash)
                                });
                    evalStack.Push(LLVMCompValue.CreateValue(dstObject, targetType.InstanceType));
                } else {
                    // class to interface
                    var dstObject = m_Builder.BuildCall2(castToInterface.FunctionType, castToInterface.Function, new LLVMValueRef[] {
                                    srcObject.TryCast(castToInterface.ParamTypes[0],m_Builder).Value,
                                    LLVMHelpers.CreateConstU32(targetType.TypeHash)
                                });
                    evalStack.Push(LLVMCompValue.CreateValue(dstObject, targetType.InstanceType));
                }
            }
        }
        [ILCodeHandler(ILOpCode.Newobj)]
        public void NewObject(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var ctor = IRHelper.GetMethodInfo((uint)operand, context, m_Builder, out var declType, out _);

            var pthis = declType.MetadataType.IsValueType ?
                m_Builder.BuildAlloca(((LLVMClassTypeInfo)declType).DataStorageType)
                : IRHelper.AllocObjectDefault(context.Context, (LLVMClassTypeInfo)declType, m_Builder);
            var argumentList = new LLVMValueRef[ctor.ParamCount];
            for (var i = ctor.ParamCount - 1; i >= 1; i--) {
                var argValue = evalStack.Pop();
                argumentList[i] = argValue.TryCast(ctor.ParamTypes[i], m_Builder).Value;
            }
            argumentList[0] = pthis;
            m_Builder.BuildCall2(ctor.FunctionType, ctor.Function, argumentList);

            if (declType.MetadataType.IsValueType) {
                evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildLoad(pthis), declType.InstanceType));
            } else {
                evalStack.Push(LLVMCompValue.CreateValue(pthis, declType.InstanceType));
            }
        }
        [ILCodeHandler(ILOpCode.Initobj)]
        public void InitObject(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            evalStack.Pop();
        }
        [ILCodeHandler(ILOpCode.Sizeof)]
        public void SizeOf(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var unkToken = MetadataHelper.CreateHandle((uint)operand);
            var targetTypeEntry = context.Method.ResolveTypeToken(unkToken);
            var targetType = (LLVMClassTypeInfo)context.Context.ResolveLLVMType(targetTypeEntry);
            evalStack.Push(LLVMCompValue.CreateValue(targetType.DataStorageType.SizeOf, LLVMCompType.Int64));
        }
        [ILCodeHandler(ILOpCode.Ldnull)]
        public void LoadNullObject(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            evalStack.Push(LLVMCompValue.CreateValue(LLVMValueRef.CreateConstNull(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)), LLVMTypeTag.Pointer));
        }
        [ILCodeHandler(ILOpCode.Constrained)]
        public void CallVirtualTypeConstrain(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var unkToken = MetadataHelper.CreateHandle((uint)operand);
            var targetTypeEntry = context.Method.ResolveTypeToken(unkToken);
            m_CallvirtTypeHint = context.Context.ResolveLLVMType(targetTypeEntry);
        }
        [ILCodeHandler(ILOpCode.Unbox_any)]
        public void Unbox(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var unkToken = MetadataHelper.CreateHandle((uint)operand);
            var targetTypeEntry = context.Method.ResolveTypeToken(unkToken);
            var targetType = context.Context.ResolveLLVMType(targetTypeEntry);
            var value = evalStack.Pop();

            var ptrDataStor = m_Builder.BuildBitCast(value.Value, LLVMTypeRef.CreatePointer(targetType.InstanceType.LLVMType, 0));
            var dataStorVal = m_Builder.BuildLoad(ptrDataStor);
            evalStack.Push(LLVMCompValue.CreateValue(dataStorVal, targetType.InstanceType.TypeTag));
        }
        [ILCodeHandler(ILOpCode.Box)]
        public void Box(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var unkToken = MetadataHelper.CreateHandle((uint)operand);
            var targetTypeEntry = context.Method.ResolveTypeToken(unkToken);
            var boxType = (LLVMClassTypeInfo)context.Context.ResolveLLVMType(targetTypeEntry);
            var boxObject = IRHelper.AllocObjectDefault(context.Context, boxType, m_Builder);
            var dataStorValue = evalStack.Pop();
            var boxDataStorPtr = m_Builder.BuildBitCast(boxObject, dataStorValue.Type.ToPointerType().LLVMType);
            m_Builder.BuildStore(dataStorValue.Value, boxDataStorPtr);
            evalStack.Push(LLVMCompValue.CreateValue(boxObject, boxType.HeapPtrType));

        }
        [ILCodeHandler(ILOpCode.Isinst)]
        public void IsInstance(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var runtimeHelpers = context.Context.ResolveLLVMType(context.Context.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::RuntimeHelpers"]);
            var isInstClass = context.Context.ResolveLLVMMethod(runtimeHelpers.MetadataType.FindMethodByName("IsInstClass").Entry);
            var isInstInterface = context.Context.ResolveLLVMMethod(runtimeHelpers.MetadataType.FindMethodByName("IsInstInterface").Entry);
            var value = evalStack.Pop();
            var unkToken = MetadataHelper.CreateHandle((uint)operand);
            var targetTypeEntry = context.Method.ResolveTypeToken(unkToken);
            var targetType = context.Context.ResolveLLVMType(targetTypeEntry);

            var objectType = (LLVMClassTypeInfo)context.Context.ResolveLLVMType(context.Context.TypeEnvironment.IntrinicsTypes["System::Object"]);

            if (targetType is LLVMClassTypeInfo) {
                var result = m_Builder.BuildCall2(isInstClass.FunctionType, isInstClass.Function, new LLVMValueRef[] {
                                m_Builder.BuildBitCast(value.Value,isInstClass.ParamTypes[0].LLVMType),
                                targetType.VtableType.StructElementTypes[0].SizeOf,
                                m_Builder.BuildBitCast(targetType.VtableBody,isInstClass.ParamTypes[2].LLVMType),
                            });
                var retObject = m_Builder.BuildBitCast(result, value.Type.LLVMType);
                evalStack.Push(LLVMCompValue.CreateValue(retObject, value.Type));
            } else {
                var result = m_Builder.BuildCall2(isInstInterface.FunctionType, isInstInterface.Function, new LLVMValueRef[] {
                                m_Builder.BuildBitCast(value.Value,isInstInterface.ParamTypes[0].LLVMType),
                                LLVMHelpers.CreateConstU32(targetType.TypeHash),
                            });
                var retObject = m_Builder.BuildBitCast(result, value.Type.LLVMType);
                evalStack.Push(LLVMCompValue.CreateValue(retObject, value.Type));
            }
        }
    }
}
