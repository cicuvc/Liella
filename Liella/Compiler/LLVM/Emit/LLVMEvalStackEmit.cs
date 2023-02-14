using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM.Emit {
    public partial class LLVMILEmit {
        [ILCodeHandler(ILOpCode.Dup)]
        public void Duplicate(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            evalStack.Push(evalStack.Peek());
        }
        [ILCodeHandler(ILOpCode.Pop)]
        public void Pop(ILOpCode opcode, ulong operand) {
            var evalStack = m_EvalStack;

            evalStack.Pop();
        }
        [ILCodeHandler(ILOpCode.Ldstr)]
        public void LoadString(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var token = MetadataHelper.CreateStringHandle(0xFFFFFF & ((uint)operand));
            var constString = m_Method.ResolveStringToken(token);
            var stringValue = context.Context.InternString(constString);

            evalStack.Push(LLVMCompValue.CreateValue(stringValue, LLVMTypeTag.Class));
        }
        [ILCodeHandler(ILOpCode.Nop)]
        public void NoOperation(ILOpCode opcode, ulong operand) {
        }
        [ILCodeHandler(ILOpCode.Arglist)]
        public void LoadArgumentList(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var valistWrapperType = context.Context.TypeEnvironment.IntrinicsTypes["System::RuntimeArgumentHandle"];
            var valistLLVMType = (LLVMClassTypeInfo)context.Context.ResolveLLVMType(valistWrapperType);

            var valistStorage = m_Builder.BuildAlloca(valistLLVMType.DataStorageType);
            var valistPtr = m_Builder.BuildGEP(valistStorage, new LLVMValueRef[] {
                            LLVMHelpers.CreateConstU32(0),
                            LLVMHelpers.CreateConstU32(1)
                        });

            var vaStartFunc = LLVMHelpers.GetIntrinsicFunction(context.Context.Module, "llvm.va_start", Array.Empty<LLVMTypeRef>());
            m_Builder.BuildCall(vaStartFunc, new LLVMValueRef[] {
                            m_Builder.BuildBitCast(valistPtr,LLVMCompType.Integer8.ToPointerType().LLVMType)
                        });
            var valistValue = m_Builder.BuildLoad(valistStorage); ;
            evalStack.Push(LLVMCompValue.CreateValue(valistValue, valistLLVMType.InstanceType));
        }
    }
}
