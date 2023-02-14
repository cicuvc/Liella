using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using LLVMInterop = LLVMSharp.Interop.LLVM;

namespace Liella.Compiler.LLVM {
    public static class LLVMHelpers {
        private static string[] m_AttributeNames = new string[] { "alwaysinline", "noduplicate", "inlinehint", "noinline", "nounwind" };
        private static string[] m_MetadataNames = new string[] { "invariant.load", "invariant.group", "absolute_symbol" };
        private static Dictionary<string, uint> m_AttributeKindMap = new Dictionary<string, uint>();
        private static Dictionary<string, uint> m_MetadataKindMap = new Dictionary<string, uint>();
        unsafe static LLVMHelpers() {
            foreach (var i in m_AttributeNames) {
                var namePtr = Marshal.StringToHGlobalAnsi(i);
                var kind = LLVMInterop.GetEnumAttributeKindForName((sbyte*)namePtr, (nuint)i.Length);
                m_AttributeKindMap.Add(i, kind);
                Marshal.FreeHGlobal(namePtr);
            }
            foreach (var i in m_MetadataNames) {
                var namePtr = Marshal.StringToHGlobalAnsi(i);
                var kind = LLVMInterop.GetMDKindID((sbyte*)namePtr, (uint)i.Length);
                m_MetadataKindMap.Add(i, kind);
                Marshal.FreeHGlobal(namePtr);
            }
        }
        public unsafe static void AddAttributeForFunction(LLVMModuleRef module, LLVMValueRef function, string attributeNames) {
            var atttribute = LLVMInterop.CreateEnumAttribute(module.Context, m_AttributeKindMap[attributeNames], 1);
            LLVMInterop.AddAttributeAtIndex(function, LLVMAttributeIndex.LLVMAttributeFunctionIndex, atttribute);
        }
        public unsafe static void AddMetadataForInst(LLVMValueRef inst, string metadataName, LLVMValueRef[] values) {
            var mdNode = LLVMValueRef.CreateMDNode(values);
            inst.SetMetadata(m_MetadataKindMap[metadataName], mdNode);
        }

        public unsafe static LLVMValueRef GetIntrinsicFunction(LLVMModuleRef module, string name, LLVMTypeRef[] types) {
            var namePtr = Marshal.StringToHGlobalAnsi(name);
            var kind = LLVMInterop.LookupIntrinsicID((sbyte*)namePtr, (nuint)name.Length);

            fixed (LLVMTypeRef* typesPtr = types) {
                return (LLVMValueRef)LLVMInterop.GetIntrinsicDeclaration(module, kind, (LLVMOpaqueType**)typesPtr, (nuint)types.Length);
            }
        }
        public static LLVMValueRef CreateConstU32(uint value) => LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, value);
        public static LLVMValueRef CreateConstU64(ulong value) => LLVMValueRef.CreateConstInt(LLVMTypeRef.Int64, value);
        public static LLVMValueRef CreateConstU32(int value) => LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (uint)value);
        public static LLVMValueRef CreateConstU64(long value) => LLVMValueRef.CreateConstInt(LLVMTypeRef.Int64, (ulong)value);

        public static unsafe ulong EvaluateConstIntValue(LLVMValueRef value, LLVMCompiler compiler) {
            var tempFunction = compiler.Module.AddFunction($"eval_func_{System.Environment.TickCount}", LLVMTypeRef.CreateFunction(value.TypeOf, Array.Empty<LLVMTypeRef>()));
            var defaultBlock = tempFunction.AppendBasicBlock("block0");
            var builder = compiler.EvalBuilder;
            builder.PositionAtEnd(defaultBlock);
            builder.BuildRet(value);

            compiler.Evaluator.RecompileAndRelinkFunction(tempFunction);
            var result = compiler.Evaluator.RunFunction(tempFunction, Array.Empty<LLVMGenericValueRef>());
            var valueResult = LLVMInterop.GenericValueToInt(result, 0);

            defaultBlock.Delete();
            tempFunction.DeleteFunction();

            return valueResult;
        }
    }

}
