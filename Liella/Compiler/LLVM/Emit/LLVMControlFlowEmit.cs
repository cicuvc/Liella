using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM.Emit {
    public partial class LLVMILEmit {
        [ILCodeHandler(ILOpCode.Bgt, ILOpCode.Bgt_s)]
        public void BranchGreater(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;
            var basicBlock = m_BasicBlock;

            var value2 = evalStack.Pop().TryCastComparable(m_Builder);
            var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
            var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntSGT, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);

            var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
            var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
            m_Builder.BuildCondBr(result, target, falseTarget);

        }
        [ILCodeHandler(ILOpCode.Bgt_un, ILOpCode.Bgt_un_s)]
        public void BranchGreaterUnsigned(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;
            var basicBlock = m_BasicBlock;

            var value2 = evalStack.Pop().TryCastComparable(m_Builder);
            var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
            var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntUGT, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);

            var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
            var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
            m_Builder.BuildCondBr(result, target, falseTarget);

        }
        [ILCodeHandler(ILOpCode.Blt, ILOpCode.Blt_s)]
        public void BranchLess(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;
            var basicBlock = m_BasicBlock;

            var value2 = evalStack.Pop().TryCastComparable(m_Builder);
            var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
            var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);

            var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
            var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
            m_Builder.BuildCondBr(result, target, falseTarget);

        }
        [ILCodeHandler(ILOpCode.Blt_un, ILOpCode.Blt_un_s)]
        public void BranchLessUnsigned(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;
            var basicBlock = m_BasicBlock;

            var value2 = evalStack.Pop().TryCastComparable(m_Builder);
            var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
            var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntULT, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);

            var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
            var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
            m_Builder.BuildCondBr(result, target, falseTarget);


        }
        [ILCodeHandler(ILOpCode.Ble, ILOpCode.Ble_s)]
        public void BranchLessEqual(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;
            var basicBlock = m_BasicBlock;

            var value2 = evalStack.Pop().TryCastComparable(m_Builder);
            var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
            var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLE, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);

            var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
            var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
            m_Builder.BuildCondBr(result, target, falseTarget);

        }
        [ILCodeHandler(ILOpCode.Ble_un, ILOpCode.Ble_un_s)]
        public void BranchLessEqualUnsigned(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;
            var basicBlock = m_BasicBlock;

            var value2 = evalStack.Pop().TryCastComparable(m_Builder);
            var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
            var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntULE, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);

            var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
            var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
            m_Builder.BuildCondBr(result, target, falseTarget);
        }
        [ILCodeHandler(ILOpCode.Bge, ILOpCode.Bge_s)]
        public void BranchGreaterEqual(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;
            var basicBlock = m_BasicBlock;

            var value2 = evalStack.Pop().TryCastComparable(m_Builder);
            var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
            var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntSGE, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);

            var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
            var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
            m_Builder.BuildCondBr(result, target, falseTarget);

        }
        [ILCodeHandler(ILOpCode.Bge_un, ILOpCode.Bge_un_s)]
        public void BranchGreaterEqualUnsigned(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;
            var basicBlock = m_BasicBlock;

            var value2 = evalStack.Pop().TryCastComparable(m_Builder);
            var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
            var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntUGE, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);

            var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
            var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
            m_Builder.BuildCondBr(result, target, falseTarget);
        }
        [ILCodeHandler(ILOpCode.Beq, ILOpCode.Beq_s)]
        public void BranchEqual(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;
            var basicBlock = m_BasicBlock;

            var value2 = evalStack.Pop().TryCastComparable(m_Builder);
            var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
            var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);

            var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
            var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
            m_Builder.BuildCondBr(result, target, falseTarget);
        }
        [ILCodeHandler(ILOpCode.Brtrue, ILOpCode.Brtrue_s)]
        public void BranchTrue(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;
            var basicBlock = m_BasicBlock;

            var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
            var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
            var cond = evalStack.Pop().TryCastCond(m_Builder);
            m_Builder.BuildCondBr(cond.Value, target, falseTarget);
        }
        [ILCodeHandler(ILOpCode.Brfalse, ILOpCode.Brfalse_s)]
        public void BranchFalse(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;
            var basicBlock = m_BasicBlock;

            var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
            var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
            var cond = evalStack.Pop().TryCastCond(m_Builder);
            m_Builder.BuildCondBr(cond.Value, falseTarget, target);
        }
        [ILCodeHandler(ILOpCode.Br, ILOpCode.Br_s)]
        public void BranchImmediate(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;
            var basicBlock = m_BasicBlock;

            var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
            m_Builder.BuildBr(target);
        }
        [ILCodeHandler(ILOpCode.Switch)]
        public void BranchSwitch(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;
            var basicBlock = m_BasicBlock;

            var branches = (uint)operand;
            var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
            var value = evalStack.Pop().TryCast(LLVMCompType.UInt32, m_Builder);
            var switchInst = m_Builder.BuildSwitch(value.Value, falseTarget, branches);
            for (var i = 0u; i < branches; i++) {
                var trueTarget = context.GetLLVMBasicBlock(basicBlock.TrueExit[i]);
                switchInst.AddCase(LLVMHelpers.CreateConstU32(i), trueTarget);
            }
        }
        [ILCodeHandler(ILOpCode.Ret)]
        public void BranchReturn(ILOpCode code, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;
            var basicBlock = m_BasicBlock;

            if (m_Method.Signature.ReturnType.ToString() != "System::Void") {
                var retValue = evalStack.Pop().TryCast(context.ReturnType, m_Builder);

                m_Builder.BuildRet(retValue.Value);
            } else {
                m_Builder.BuildRetVoid();
            }
            if (evalStack.Count != 0) throw new Exception("Stack analysis fault");
        }
    }
}
