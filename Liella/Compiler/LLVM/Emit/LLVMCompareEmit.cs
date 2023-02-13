using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM.Emit {
    public partial class LLVMILEmit {
        [ILCodeHandler(ILOpCode.Cgt)]
        public void CompareGreater(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value2 = evalStack.Pop();
            var value1 = evalStack.Pop();

            var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntSGT, value1.Value, value2.Value);
            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildZExt(result, LLVMTypeRef.Int32), LLVMTypeTag.SignedInt));

        }
        [ILCodeHandler(ILOpCode.Cgt_un)]
        public void CompareGreaterUnsigned(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value2 = evalStack.Pop().TryCastComparable(m_Builder);
            var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
            var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntUGT, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);
            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildZExt(result, LLVMTypeRef.Int32), LLVMTypeTag.SignedInt));

        }
        [ILCodeHandler(ILOpCode.Clt)]
        public void CompareLess(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value2 = evalStack.Pop().TryCastComparable(m_Builder);
            var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
            var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, value1.Value, value2.Value);
            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildZExt(result, LLVMTypeRef.Int32), LLVMTypeTag.SignedInt));

        }
        [ILCodeHandler(ILOpCode.Clt_un)]
        public void CompareLessUnsigned(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value2 = evalStack.Pop().TryCastComparable(m_Builder); ;
            var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
            var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntULT, value1.Value, value2.Value);
            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildZExt(result, LLVMTypeRef.Int32), LLVMTypeTag.SignedInt));

        }
        [ILCodeHandler(ILOpCode.Ceq)]
        public void CompareEqual(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var value2 = evalStack.Pop().TryCastComparable(m_Builder); ;
            var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
            var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, value1.Value, value2.Value);
            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildZExt(result, LLVMTypeRef.Int32), LLVMTypeTag.SignedInt));

        }
    }
}
