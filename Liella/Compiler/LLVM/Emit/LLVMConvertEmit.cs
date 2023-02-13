using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM.Emit {
    public partial class LLVMILEmit {
        [ILCodeHandler(ILOpCode.Conv_r8)]
        public void ConvertReal64(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop();

            evalStack.Push(value.TryCast(LLVMCompType.Float64, m_Builder));

        }
        [ILCodeHandler(ILOpCode.Conv_r4)]
        public void ConvertReal32(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop();

            evalStack.Push(value.TryCast(LLVMCompType.Float32, m_Builder));

        }
        [ILCodeHandler(ILOpCode.Conv_u)]
        public void ConvertNativeUInt(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop();

            evalStack.Push(value.TryCast(LLVMCompType.Int64, m_Builder));

        }
        [ILCodeHandler(ILOpCode.Conv_i)]
        public void ConvertNativeInt(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop();
            evalStack.Push(value.TryCast(LLVMCompType.Int64, m_Builder));

        }
        [ILCodeHandler(ILOpCode.Conv_i8)]
        public void ConvertInt64(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop();
            evalStack.Push(value.TryCast(LLVMCompType.Int64, m_Builder));

        }
        [ILCodeHandler(ILOpCode.Conv_i4)]
        public void ConvertInt32(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop();
            evalStack.Push(value.TryCast(LLVMCompType.Int32, m_Builder));

        }
        [ILCodeHandler(ILOpCode.Conv_i2)]
        public void ConvertInt16(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop();
            evalStack.Push(value.TryCast(LLVMCompType.Int16, m_Builder));

        }
        [ILCodeHandler(ILOpCode.Conv_i1)]
        public void ConvertInt8(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop();
            evalStack.Push(value.TryCast(LLVMCompType.Int8, m_Builder));

        }
        [ILCodeHandler(ILOpCode.Conv_u8)]
        public void ConvertUInt64(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop();
            evalStack.Push(value.TryCast(LLVMCompType.UInt64, m_Builder));

        }
        [ILCodeHandler(ILOpCode.Conv_u4)]
        public void ConvertUInt32(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop();
            evalStack.Push(value.TryCast(LLVMCompType.UInt32, m_Builder));

        }
        [ILCodeHandler(ILOpCode.Conv_u2)]
        public void ConvertUInt16(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop();
            evalStack.Push(value.TryCast(LLVMCompType.UInt16, m_Builder));

        }
        [ILCodeHandler(ILOpCode.Conv_u1)]
        public void ConvertUInt8(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop();
            evalStack.Push(value.TryCast(LLVMCompType.UInt8, m_Builder));

        }
    }
}
