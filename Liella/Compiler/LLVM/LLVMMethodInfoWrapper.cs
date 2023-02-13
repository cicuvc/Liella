using Liella.Metadata;
using Liella.MSIL;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM {
    public class LLVMMethodInfoWrapper {
        protected static Queue<MethodBasicBlock> s_Queue = new Queue<MethodBasicBlock>();
        protected static Stack<LLVMCompValue> s_EvalStack = new Stack<LLVMCompValue>();

        protected MethodInstance m_Method;
        protected LLVMTypeInfo m_DeclType;
        protected LLVMCompiler m_Compiler;
        protected LLVMTypeRef m_FunctionType;
        protected LLVMValueRef m_Function;
        protected LLVMTypeRef m_FunctionPtrType;
        protected LLVMCompValue[] m_LocalVarValues = null;
        protected SortedList<Interval, LLVMBasicBlockRef> m_LLVMBasicBlocks;
        protected LLVMCompValue[] m_ParamValues = null;
        protected LLVMCompType m_FunctionRetType;
        protected LLVMCompType[] m_ParamType = null;

        public bool IsAbstract => m_Method.Attributes.HasFlag(MethodAttributes.Abstract);
        public LLVMCompValue[] ParamValueRef => m_ParamValues;
        public LLVMCompType[] ParamTypes => m_ParamType;
        public int ParamCount => m_ParamType.Length;
        public LLVMCompValue[] LocalValueRef => m_LocalVarValues;
        public LLVMCompiler Context => m_Compiler;
        public LLVMValueRef Function => m_Function;
        public LLVMTypeRef FunctionType => m_FunctionType;
        public LLVMTypeRef FunctionPtrType => m_FunctionPtrType;
        public LLVMCompType ReturnType => m_FunctionRetType;
        public MethodInstance Method => m_Method;
        public LLVMTypeInfo DeclType => m_DeclType;


        public LLVMMethodInfoWrapper(LLVMCompiler compiler, MethodInstance method) {
            m_Compiler = compiler;
            m_Method = method;
            m_DeclType = compiler.ResolveLLVMType(method.DeclType.Entry);
            var signature = method.Signature;
            var returnType = compiler.ResolveLLVMInstanceType(signature.ReturnType);

            var idx = 0u;
            var isStatic = m_Method.Attributes.HasFlag(MethodAttributes.Static);

            m_ParamType = new LLVMCompType[signature.ParameterTypes.Length + (isStatic ? 0 : 1)];
            idx = 0;
            if (!isStatic) {
                var declType = m_Method.DeclType;
                var llvmType = m_Compiler.ResolveLLVMType(declType.Entry);
                m_ParamType[idx++] = llvmType is LLVMClassTypeInfo classType ? classType.HeapPtrType : (llvmType.InstanceType.WithTag(LLVMTypeTag.StackAlloc));
            }
            foreach (var i in signature.ParameterTypes) m_ParamType[idx++] = m_Compiler.ResolveLLVMInstanceType(i);

            var isVaArg = method.Signature.Header.CallingConvention.HasFlag(SignatureCallingConvention.VarArgs);
            m_FunctionRetType = returnType;
            m_FunctionType = LLVMTypeRef.CreateFunction(returnType.LLVMType, m_ParamType.Select(e => e.LLVMType).ToArray(), isVaArg);

            m_FunctionPtrType = LLVMTypeRef.CreatePointer(m_FunctionType, 0);

            if (!m_Method.Attributes.HasFlag(MethodAttributes.Abstract)) {
                m_Function = compiler.Module.AddFunction(method.Entry.ToString(), m_FunctionType);

                LLVMHelpers.AddAttributeForFunction(compiler.Module, m_Function, "nounwind");

                if (m_Method.ImplAttributes.HasFlag(MethodImplAttributes.NoInlining)) {
                    LLVMHelpers.AddAttributeForFunction(compiler.Module, m_Function, "noinline");
                }
                if (m_Method.ImplAttributes.HasFlag(MethodImplAttributes.AggressiveInlining)) {
                    LLVMHelpers.AddAttributeForFunction(compiler.Module, m_Function, "inlinehint");
                }
            }

            // static ctor
            if (method.Attributes.HasFlag(MethodAttributes.Static | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName)) {
                if (method.Entry.Name.Contains("ctor")) {
                    m_Compiler.StaticConstructorList.Add(this);
                }
            }

        }
        public LLVMBasicBlockRef GetLLVMBasicBlock(MethodBasicBlock basicBlock) => m_LLVMBasicBlocks[basicBlock.Interval];
        public void GeneratePrologue(IRGenerator irGenerator) {
            if (m_Method.IsDummy) return;
            m_Method.MakeBasicBlocks();
            var builder = irGenerator.Builder;
            var basicBlocks = m_Method.BasicBlocks;
            var llvmBasicBlocks = m_LLVMBasicBlocks = new SortedList<Interval, LLVMBasicBlockRef>(new Interval.IntervalTreeComparer());
            var signature = m_Method.Signature;
            var isStatic = m_Method.Attributes.HasFlag(MethodAttributes.Static);

            var prologue = m_Function.AppendBasicBlock("Prologue");

            var idx = 0;
            foreach (var i in basicBlocks) {
                llvmBasicBlocks.Add(i.Key, m_Function.AppendBasicBlock($"Block{idx++}"));
            }
            builder.PositionAtEnd(prologue);


            m_LocalVarValues = new LLVMCompValue[m_Method.LocalVaribleTypes.Length];
            idx = 0;
            foreach (var i in m_Method.LocalVaribleTypes) {
                var llvmType = m_Compiler.ResolveLLVMInstanceType(i);
                if (i is PointerTypeEntry || m_Compiler.TypeEnvironment.ActiveTypes[i].IsValueType) llvmType.TypeTag |= LLVMTypeTag.StackAlloc;

                m_LocalVarValues[idx++] = LLVMCompValue.CreateValue(builder.BuildAlloca(llvmType.LLVMType), llvmType.TypeTag);
            }

            m_ParamValues = new LLVMCompValue[signature.ParameterTypes.Length + (isStatic ? 0 : 1)];
            idx = 0;
            foreach (var i in m_ParamType) {
                var paramValues = m_ParamValues[idx] = LLVMCompValue.CreateValue(builder.BuildAlloca(i.LLVMType), i.TypeTag);
                builder.BuildStore(m_Function.GetParam((uint)(idx++)), paramValues.Value);
            }


            builder.BuildBr(llvmBasicBlocks.First().Value);

        }
        public void AddMergePhiNode(IRGenerator irGenerator, MethodBasicBlock basicBlock, MethodBasicBlock predBasicBlock, LLVMCompValue[] predStack) {
            var builder = irGenerator.Builder;
            var predLLVMBlock = new LLVMBasicBlockRef[] { m_LLVMBasicBlocks[predBasicBlock.Interval] };
            var currentLLVMBlock = m_LLVMBasicBlocks[basicBlock.Interval];
            if (basicBlock.PreStack != null) {
                builder.PositionBefore(predLLVMBlock[0].LastInstruction);
                //builder.PositionAtEnd(predLLVMBlock[0]);
                for (var i = 0; i < basicBlock.PreStack.Length; i++) {
                    basicBlock.PreStack[i].Value.AddIncoming(new LLVMValueRef[] { predStack[i].TryCast(basicBlock.PreStack[i].Type, builder, m_Compiler).Value }, predLLVMBlock, 1);
                }
            } else {
                builder.PositionAtEnd(currentLLVMBlock);
                basicBlock.PreStack = predStack.Select(e => {
                    var node = builder.BuildPhi(e.Type.LLVMType);
                    node.AddIncoming(new LLVMValueRef[] { e.Value }, predLLVMBlock, 1);
                    return LLVMCompValue.CreateValue(node, e.Type);
                }).ToArray();
            }
        }
        public void GenerateCode(IRGenerator irGenerator) {
            var intrinsicFuncDef = m_Compiler.TryFindFunctionImpl(this);
            if (intrinsicFuncDef != null) {
                intrinsicFuncDef.FillFunctionBody(this, irGenerator.Builder);

            }
            if (m_Method.IsDummy) return;

            RunStackConsistencyCheck();
            foreach (var i in m_Method.BasicBlocks.Values) {
                if (i.FalseExit != null) {
                    i.FalseExit.Predecessor.Add(i);
                }
                if (i.TrueExit != null) {
                    foreach (var j in i.TrueExit) {
                        if (j != null && i.FalseExit != j) {
                            j.Predecessor.Add(i);
                        }
                    }
                }


            }
            foreach (var i in m_Method.BasicBlocks.Values) {
                if (i.Predecessor.Count == 0 || i.ExitStackDepth == i.StackDepthDelta) s_Queue.Enqueue(i);
            }
            while (s_Queue.Count != 0) {
                var current = s_Queue.Dequeue();
                s_EvalStack.Clear();
                var finalStack = irGenerator.GenerateForBasicBlock(current, this, current.PreStack != null ? new Stack<LLVMCompValue>(current.PreStack.Reverse()) : s_EvalStack);

                if (current.FalseExit != null) {
                    if (finalStack.Count != 0) AddMergePhiNode(irGenerator, current.FalseExit, current, finalStack.ToArray());
                    current.FalseExit.Predecessor.Remove(current);
                    if (current.FalseExit.Predecessor.Count == 0 && current.FalseExit.StackDepthDelta != current.FalseExit.ExitStackDepth) {
                        s_Queue.Enqueue(current.FalseExit);
                    }
                }
                if (current.TrueExit != null) {
                    foreach (var k in current.TrueExit) {
                        if (current.FalseExit != k) {
                            if (finalStack.Count != 0) AddMergePhiNode(irGenerator, k, current, finalStack.ToArray());
                            k.Predecessor.Remove(current);
                            if (k.Predecessor.Count == 0 && k.StackDepthDelta != k.ExitStackDepth) {
                                s_Queue.Enqueue(k);
                            }
                        }
                    }
                }


            }
        }

        public void RunStackConsistencyCheck() {
            if (m_Method.IsDummy) return;
            var entryBlock = m_Method.BasicBlocks[new Interval(0, 0)];
            entryBlock.ExitStackDepth = entryBlock.StackDepthDelta;
            s_Queue.Enqueue(entryBlock);
            while (s_Queue.Count != 0) {
                var current = s_Queue.Dequeue();
                if (current.FalseExit != null) {
                    var branch = current.FalseExit;
                    if (branch.ExitStackDepth != int.MinValue) {
                        if (branch.ExitStackDepth != branch.StackDepthDelta + current.ExitStackDepth) {
                            throw new InvalidProgramException("Stack analysis fault");
                        }
                    } else {
                        branch.ExitStackDepth = branch.StackDepthDelta + current.ExitStackDepth;
                        if (branch.ExitStackDepth < 0) throw new InvalidProgramException("Stack underflow");
                        s_Queue.Enqueue(branch);
                    }
                }
                if (current.TrueExit != null) {
                    foreach (var j in current.TrueExit) {
                        if (j != current.FalseExit) {
                            var branch = j;
                            if (branch.ExitStackDepth != int.MinValue) {
                                if (branch.ExitStackDepth != branch.StackDepthDelta + current.ExitStackDepth) {
                                    throw new InvalidProgramException("Stack analysis fault");
                                }
                            } else {
                                branch.ExitStackDepth = branch.StackDepthDelta + current.ExitStackDepth;
                                if (branch.ExitStackDepth < 0) throw new InvalidProgramException("Stack underflow");
                                s_Queue.Enqueue(branch);
                            }
                        }
                    }
                }


            }
        }
        public override string ToString() {
            return m_Method.Entry.ToString();
        }
    }

}
