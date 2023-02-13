using Liella.Metadata;
using Liella.MSIL;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM.Emit {
    public partial class LLVMILEmit:ILCodeHandler {
        protected MethodBasicBlock m_BasicBlock;
        protected LLVMMethodInfoWrapper m_Context;
        protected Stack<LLVMCompValue> m_EvalStack;

        protected MethodInstance m_Method;
        protected LLVMBasicBlockRef m_LLVMBasicBlock;

        protected LLVMBuilderRef m_Builder;

        protected LLVMTypeInfo m_CallvirtTypeHint;
        public LLVMILEmit(LLVMBuilderRef builder) {
            m_Builder = builder;
        }

        public void StartEmit(MethodBasicBlock methodBasicBlock, LLVMMethodInfoWrapper context, Stack<LLVMCompValue> evalStack) {
            m_BasicBlock = methodBasicBlock;
            m_Context = context;
            m_EvalStack = evalStack;

            m_Method = context.Method;
            m_LLVMBasicBlock = context.GetLLVMBasicBlock(methodBasicBlock);
            m_CallvirtTypeHint = null;

            m_Builder.PositionAtEnd(m_LLVMBasicBlock);
        }
    }
}
