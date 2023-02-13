using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM.Emit {
    public partial class LLVMILEmit : ILCodeHandler {
        [ILCodeHandler(ILOpCode.Ldarg_0, ILOpCode.Ldarg_1, ILOpCode.Ldarg_2, ILOpCode.Ldarg_3, ILOpCode.Ldarg_s)]
        public void LoadArgument(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var index = opcode == ILOpCode.Ldarg_s ? ((int)operand) : (opcode - ILOpCode.Ldarg_0);
            var paramValue = context.ParamValueRef[index];
            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildLoad(paramValue.Value), paramValue.Type.TypeTag));
        }
        [ILCodeHandler(ILOpCode.Ldloc_0, ILOpCode.Ldloc_1, ILOpCode.Ldloc_2, ILOpCode.Ldloc_3, ILOpCode.Ldloc_s)]
        public void LoadLocals(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var index = opcode == ILOpCode.Ldloc_s ? ((int)operand) : (opcode - ILOpCode.Ldloc_0);
            var localVarValue = context.LocalValueRef[index];
            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildLoad(localVarValue.Value), localVarValue.Type.ToDerefType()));
        }
        [ILCodeHandler(ILOpCode.Starg, ILOpCode.Starg_s)]
        public void StoreArgument(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var argValue = context.ParamValueRef[(int)operand];
            var value = evalStack.Pop().TryCast(argValue.Type.ToDerefType(), m_Builder);
            m_Builder.BuildStore(value.Value, argValue.Value);
        }
        [ILCodeHandler(ILOpCode.Stloc_0, ILOpCode.Stloc_1, ILOpCode.Stloc_2, ILOpCode.Stloc_3, ILOpCode.Stloc_s)]
        public void StoreLocals(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var index = opcode == ILOpCode.Stloc_s ? ((int)operand) : (opcode - ILOpCode.Stloc_0);

            var localVarValue = context.LocalValueRef[index];
            var value = evalStack.Pop().TryCast(localVarValue.Type.ToDerefType(), m_Builder);

            m_Builder.BuildStore(value.Value, localVarValue.Value);
        }
        [ILCodeHandler(ILOpCode.Ldloca_s)]
        public void LoadLocalsAddress(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var index = opcode == ILOpCode.Ldloca_s ? ((int)operand) : (opcode - ILOpCode.Ldloc_0);
            evalStack.Push((context.LocalValueRef[index]));
        }
        [ILCodeHandler(ILOpCode.Localloc)]
        public void LocalAlloc(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            var length = evalStack.Pop().TryCast(LLVMCompType.Int64, m_Builder);
            var ptr = m_Builder.BuildArrayAlloca(LLVMTypeRef.Int8, length.Value);
            evalStack.Push(LLVMCompValue.CreateValue(ptr, LLVMCompType.Int8.ToPointerType()));
        }
        [ILCodeHandler(ILOpCode.Ldarga_s, ILOpCode.Ldarga)]
        public void LoadArugmentAddress(ILOpCode opcode, ulong operand) {
            var context = m_Context;
            var evalStack = m_EvalStack;

            evalStack.Push(context.ParamValueRef[operand]);
        }
    }
}
