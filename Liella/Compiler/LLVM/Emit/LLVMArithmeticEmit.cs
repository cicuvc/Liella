using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM.Emit {
    public partial class LLVMILEmit {
        [ILCodeHandler(ILOpCode.Add)]
        public void ArithmeticAdd(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value2 = evalStack.Pop();
            var value1 = evalStack.Pop();

            if (value1.Type.TypeTag.HasFlag(LLVMTypeTag.Pointer) || value2.Type.TypeTag.HasFlag(LLVMTypeTag.Pointer)) {
                var ptrValue = value1.Type.TypeTag.HasFlag(LLVMTypeTag.Pointer) ? value1 : value2;
                var intValue = value1.Type.TypeTag.HasFlag(LLVMTypeTag.Pointer) ? value2 : value1;
                var resultInt = m_Builder.BuildAdd(intValue.TryCast(LLVMCompType.Int64, m_Builder).Value, m_Builder.BuildPtrToInt(ptrValue.Value, LLVMTypeRef.Int64));
                evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildIntToPtr(resultInt, ptrValue.Type.LLVMType), ptrValue.Type.TypeTag));
            } else {
                value2 = value2.TryCast(value1.Type, m_Builder);
                if (value1.Type.TypeTag.HasFlag(LLVMTypeTag.Real)) {
                    evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildFAdd(value1.Value, value2.Value), value1.Type));
                } else {
                    evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildAdd(value1.Value, value2.Value), value1.Type));
                }
            }
        }
        [ILCodeHandler(ILOpCode.Sub)]
        public void ArithmeticSub(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value2 = evalStack.Pop();
            var value1 = evalStack.Pop();

            if (value1.Type.TypeTag.HasFlag(LLVMTypeTag.Pointer) && !value2.Type.TypeTag.HasFlag(LLVMTypeTag.Pointer)) {
                var resultInt = m_Builder.BuildSub(m_Builder.BuildPtrToInt(value1.Value, LLVMTypeRef.Int64), value2.TryCast(LLVMCompType.Int64, m_Builder).Value);
                evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildIntToPtr(resultInt, value1.Type.LLVMType), value1.Type.TypeTag));
            } else {
                value2 = value2.TryCast(value1.Type, m_Builder);
                if (value1.Type.TypeTag.HasFlag(LLVMTypeTag.Real)) {
                    evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildFSub(value1.Value, value2.Value), value1.Type));
                } else {
                    evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildSub(value1.Value, value2.Value), value1.Type));
                }
                //evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildSub(value1.Value, value2.Value), value1.Type));
            }
        }
        [ILCodeHandler(ILOpCode.Div_un)]
        public void ArithmeticDivUnsigned(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value2 = evalStack.Pop();
            var value1 = evalStack.Pop();
            value2 = value2.TryCast(value1.Type, m_Builder);
            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildUDiv(value1.Value, value2.Value), value1.Type));

        }
        [ILCodeHandler(ILOpCode.Div)]
        public void ArithmeticDiv(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value2 = evalStack.Pop();
            var value1 = evalStack.Pop();
            value2 = value2.TryCast(value1.Type, m_Builder);
            if (value1.Type.TypeTag.HasFlag(LLVMTypeTag.Real)) {
                evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildFDiv(value1.Value, value2.Value), value1.Type));
            } else {
                evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildSDiv(value1.Value, value2.Value), value1.Type));
            }

        }
        [ILCodeHandler(ILOpCode.Rem_un)]
        public void ArithmeticRemainUnsigned(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value2 = evalStack.Pop();
            var value1 = evalStack.Pop();
            value2 = value2.TryCast(value1.Type, m_Builder);
            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildURem(value1.Value, value2.Value), value1.Type));

        }
        [ILCodeHandler(ILOpCode.Rem)]
        public void ArithmeticRemain(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value2 = evalStack.Pop();
            var value1 = evalStack.Pop();
            value2 = value2.TryCast(value1.Type, m_Builder);
            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildSRem(value1.Value, value2.Value), value1.Type));

        }
        [ILCodeHandler(ILOpCode.Mul, ILOpCode.Mul_ovf, ILOpCode.Mul_ovf_un)]
        public void ArithmeticMul(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value2 = evalStack.Pop();
            var value1 = evalStack.Pop();

            value2 = value2.TryCast(value1.Type, m_Builder);
            if (value1.Type.TypeTag.HasFlag(LLVMTypeTag.Real)) {
                evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildFMul(value1.Value, value2.Value), value1.Type));
            } else {
                evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildMul(value1.Value, value2.Value), value1.Type));
            }

        }
        [ILCodeHandler(ILOpCode.Shr)]
        public void ArithmeticShiftRight(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value2 = evalStack.Pop();
            var value1 = evalStack.Pop();

            value2 = value2.TryCast(value1.Type, m_Builder);

            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildAShr(value1.Value, value2.Value), value1.Type));

        }
        [ILCodeHandler(ILOpCode.Shr_un)]
        public void ArithmeticShiftRightUnsigned(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value2 = evalStack.Pop();
            var value1 = evalStack.Pop();

            value2 = value2.TryCast(value1.Type, m_Builder);


            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildLShr(value1.Value, value2.Value), value1.Type));

        }
        [ILCodeHandler(ILOpCode.Shl)]
        public void ArithmeticShiftLeft(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value2 = evalStack.Pop();
            var value1 = evalStack.Pop();

            value2 = value2.TryCast(value1.Type, m_Builder);

            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildShl(value1.Value, value2.Value), value1.Type));

        }
        [ILCodeHandler(ILOpCode.And)]
        public void ArithmeticBitwiseAnd(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value1 = evalStack.Pop();
            var value2 = evalStack.Pop();
            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildAnd(value1.Value, value2.Value), value1.Type));

        }
        [ILCodeHandler(ILOpCode.Or)]
        public void ArithmeticBitwiseOr(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value1 = evalStack.Pop();
            var value2 = evalStack.Pop();
            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildOr(value1.Value, value2.Value), value1.Type));

        }
        [ILCodeHandler(ILOpCode.Xor)]
        public void ArithmeticBitwiseXor(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value1 = evalStack.Pop();
            var value2 = evalStack.Pop();
            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildXor(value1.Value, value2.Value), value1.Type));

        }
        [ILCodeHandler(ILOpCode.Neg)]
        public void ArithmeticNegate(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value = evalStack.Pop();
            if (value.Type.TypeTag.HasFlag(LLVMTypeTag.Real)) {
                evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildFNeg(value.Value), value.Type));
            } else {
                evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildNeg(value.Value), value.Type));
            }
        }
    }
}
