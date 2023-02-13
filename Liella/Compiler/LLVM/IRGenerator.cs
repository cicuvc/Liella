using Liella.Compiler.LLVM.Emit;
using Liella.MSIL;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

using LLVMInterop = LLVMSharp.Interop.LLVM;

namespace Liella.Compiler.LLVM {
    public class IRGenerator {
        protected LLVMBuilderRef m_Builder;
        public LLVMBuilderRef Builder => m_Builder;
        protected LLVMILEmit m_IREmitter;
        public IRGenerator(LLVMBuilderRef builder) {
            m_Builder = builder;
            m_IREmitter = new LLVMILEmit(builder);
        }
        public unsafe Stack<LLVMCompValue> GenerateForBasicBlock(MethodBasicBlock basicBlock, LLVMMethodInfoWrapper context, Stack<LLVMCompValue> evalStack) {
            m_IREmitter.StartEmit(basicBlock, context, evalStack);

            context.Method.ForEachIL(basicBlock.Interval, m_IREmitter.Emit, true);

            return evalStack;
        }
    }

}
