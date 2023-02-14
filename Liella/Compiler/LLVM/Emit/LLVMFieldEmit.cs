using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM.Emit {
    public partial class LLVMILEmit {
        [ILCodeHandler(ILOpCode.Ldsfld)]
        public void LoadStaticField(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var fieldAddr = IRHelper.GetStaticFieldAddress((uint)operand, context, m_Builder, out var fieldType);
            var fieldValue = m_Builder.BuildLoad(fieldAddr);
            evalStack.Push(LLVMCompValue.CreateValue(fieldValue, fieldType.TypeTag));
        }
        [ILCodeHandler(ILOpCode.Stsfld)]
        public void StoreStaticField(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var fieldAddr = IRHelper.GetStaticFieldAddress((uint)operand, context, m_Builder, out var fieldType);

            var value = evalStack.Pop().TryCast(fieldType, m_Builder);
            m_Builder.BuildStore(value.Value, fieldAddr);
        }
        [ILCodeHandler(ILOpCode.Ldsflda)]
        public void LoadStaticFieldAddress(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var fieldAddress = IRHelper.GetStaticFieldAddress((uint)operand, context, m_Builder, out var fieldType);
            evalStack.Push(LLVMCompValue.CreateValue(fieldAddress, fieldType.TypeTag));
        }
        [ILCodeHandler(ILOpCode.Ldflda)]
        public void LoadFieldAddress(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;
            var instancePtr = (evalStack.Pop());
            var fieldPtr = IRHelper.GetInstanceFieldAddress(instancePtr, (uint)operand, context, m_Builder, out var fieldType);
            evalStack.Push(LLVMCompValue.CreateValue(fieldPtr, LLVMTypeTag.TypePointer));
        }
        [ILCodeHandler(ILOpCode.Ldfld)]
        public void LoadField(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var instancePtr = (evalStack.Pop());
            //if (instancePtr.Type.TypeTag.HasFlag(LLVMTypeTag.Class)|| instancePtr.Type.TypeTag.HasFlag(LLVMTypeTag.Pointer)) {
            var fieldPtr = IRHelper.GetInstanceFieldAddress(instancePtr, (uint)operand, context, m_Builder, out var fieldType);
            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildLoad(fieldPtr), fieldType.TypeTag));
        }
        [ILCodeHandler(ILOpCode.Stfld)]
        public void StoreField(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop();
            var instancePtr = (evalStack.Pop());
            var fieldPtr = IRHelper.GetInstanceFieldAddress(instancePtr, (uint)operand, context, m_Builder, out var fieldType);
            value = value.TryCast(fieldType, m_Builder);
            m_Builder.BuildStore(value.Value, fieldPtr);
        }
    }
}
