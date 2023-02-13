using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM.Emit {
    public partial class LLVMILEmit {
        [ILCodeHandler(ILOpCode.Ldind_i)]
        public void LoadMemoryNativeInt8(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var pointerValue = evalStack.Pop();
            var castPtr = pointerValue.TryCast(LLVMCompType.Int8.ToPointerType().ToPointerType(), m_Builder);
            var value = m_Builder.BuildLoad(castPtr.Value);

            evalStack.Push(LLVMCompValue.CreateValue(value, LLVMCompType.Int8.ToPointerType()));
        }
        [ILCodeHandler(ILOpCode.Stind_i)]
        public void StoreMemoryNativeInt8(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop();
            var pointerValue = evalStack.Pop();
            var castPtr = pointerValue.TryCast(LLVMCompType.Int8.ToPointerType().ToPointerType(), m_Builder);
            m_Builder.BuildStore(value.Value, castPtr.Value);
        }
        [ILCodeHandler(ILOpCode.Ldind_u1,ILOpCode.Ldind_i1)]
        public void LoadMemoryInt8(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var pointerValue = evalStack.Pop();
            var type = (opcode == ILOpCode.Ldind_u1) ? LLVMCompType.UInt8 : LLVMCompType.Int8;
            var castPtr = pointerValue.TryCast(type.ToPointerType(), m_Builder);
            var value = m_Builder.BuildLoad(castPtr.Value);

            evalStack.Push(LLVMCompValue.CreateValue(value, type));
        }
        [ILCodeHandler(ILOpCode.Ldind_u2,ILOpCode.Ldind_i2)]
        public void LoadMemoryInt16(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var pointerValue = evalStack.Pop();
            var type = (opcode == ILOpCode.Ldind_u2) ? LLVMCompType.UInt16 : LLVMCompType.Int16;
            var castPtr = pointerValue.TryCast(type.ToPointerType(), m_Builder);
            var value = m_Builder.BuildLoad(castPtr.Value);
            evalStack.Push(LLVMCompValue.CreateValue(value, type));
        }
        [ILCodeHandler(ILOpCode.Ldind_u4,ILOpCode.Ldind_i4)]
        public void LoadMemoryInt32(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var pointerValue = evalStack.Pop();
            var type = (opcode == ILOpCode.Ldind_u4) ? LLVMCompType.UInt32 : LLVMCompType.Int32;
            var castPtr = pointerValue.TryCast(type.ToPointerType(), m_Builder);
            var value = m_Builder.BuildLoad(castPtr.Value);
            evalStack.Push(LLVMCompValue.CreateValue(value, type));
        }
        [ILCodeHandler(ILOpCode.Ldind_i8)]
        public void LoadMemoryInt64(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var pointerValue = evalStack.Pop();
            var castPtr = pointerValue.TryCast(LLVMCompType.Int64.ToPointerType(), m_Builder);
            var value = m_Builder.BuildLoad(castPtr.Value);
            evalStack.Push(LLVMCompValue.CreateValue(value, LLVMCompType.Int64));
        }
        [ILCodeHandler(ILOpCode.Stind_i1)]
        public void StoreMemoryInt8(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop().TryCast(LLVMCompType.Int8, m_Builder);
            var pointerValue = evalStack.Pop();
            var castPtr = pointerValue.TryCast(LLVMCompType.Int8.ToPointerType(), m_Builder);
            m_Builder.BuildStore(value.Value, castPtr.Value);
        }
        [ILCodeHandler(ILOpCode.Stind_i2)]
        public void StoreMemoryInt16(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop().TryCast(LLVMCompType.Int16, m_Builder);
            var pointerValue = evalStack.Pop();
            var castPtr = pointerValue.TryCast(LLVMCompType.Int16.ToPointerType(), m_Builder);
            m_Builder.BuildStore(value.Value, castPtr.Value);
        }
        [ILCodeHandler(ILOpCode.Stind_i4)]
        public void StoreMemoryInt32(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop().TryCast(LLVMCompType.Int32, m_Builder);
            var pointerValue = evalStack.Pop();
            var castPtr = pointerValue.TryCast(LLVMCompType.Int32.ToPointerType(), m_Builder);
            m_Builder.BuildStore(value.Value, castPtr.Value);
        }
        [ILCodeHandler(ILOpCode.Stind_i8)]
        public void StoreMemoryInt64(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop().TryCast(LLVMCompType.Int64, m_Builder);
            var pointerValue = evalStack.Pop();
            var castPtr = pointerValue.TryCast(LLVMCompType.Int64.ToPointerType(), m_Builder);
            m_Builder.BuildStore(value.Value, castPtr.Value);
        }
    }
}
