using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM.Emit {
    public partial class LLVMILEmit {
        [ILCodeHandler(
            ILOpCode.Ldc_i4_0, ILOpCode.Ldc_i4_1, 
            ILOpCode.Ldc_i4_2, ILOpCode.Ldc_i4_3, 
            ILOpCode.Ldc_i4_4, ILOpCode.Ldc_i4_5,
            ILOpCode.Ldc_i4_6, ILOpCode.Ldc_i4_7,
            ILOpCode.Ldc_i4_8)]
        public void LoadConstantInt32(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            evalStack.Push(LLVMCompValue.CreateConstI32(opcode - ILOpCode.Ldc_i4_0));
        }
        [ILCodeHandler(ILOpCode.Ldc_i4_s)]
        public void LoadConstantInt32Signed(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            evalStack.Push(LLVMCompValue.CreateConstI32((int)(sbyte)operand));
        }
        [ILCodeHandler(ILOpCode.Ldc_i4)]
        public void LoadConstantInt32Value(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            evalStack.Push(LLVMCompValue.CreateConstI32((uint)operand));
        }
        [ILCodeHandler(ILOpCode.Ldc_i8)]
        public void LoadConstantInt64Value(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            evalStack.Push(LLVMCompValue.CreateConstI64(operand));
        }
        [ILCodeHandler(ILOpCode.Ldc_i4_m1)]
        public void LoadConstantMinus1(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            evalStack.Push(LLVMCompValue.CreateConstI32(-1));
        }
        [ILCodeHandler(ILOpCode.Ldc_r8)]
        public unsafe void LoadConstantFloat64Value(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = *(double*)(&operand);
            var doubleValue = LLVMValueRef.CreateConstReal(LLVMTypeRef.Double, value);
            evalStack.Push(LLVMCompValue.CreateValue(doubleValue, LLVMTypeTag.NumberReal | LLVMTypeTag.FP64));
        }
        [ILCodeHandler(ILOpCode.Ldc_r4)]
        public unsafe void LoadConstantFloat32Value(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = *(float*)(&operand);
            var doubleValue = LLVMValueRef.CreateConstReal(LLVMTypeRef.Float, value);
            evalStack.Push(LLVMCompValue.CreateValue(doubleValue, LLVMTypeTag.NumberReal));
        }
    }
}
