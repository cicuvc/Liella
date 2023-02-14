using Liella.Metadata;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM {
    public class LLVMInterfaceTypeInfo : LLVMTypeInfo {

        protected LLVMTypeRef m_InterfaceType = LLVMTypeRef.Void;
        protected HashSet<LLVMInterfaceTerm> m_InterfaceTerms = new HashSet<LLVMInterfaceTerm>();
        public HashSet<LLVMInterfaceTerm> InterfaceTerms => m_InterfaceTerms;
        public LLVMTypeRef InterfaceType => m_InterfaceType;
        public override ulong DataStorageSize => 8; // fix: other target

        public LLVMInterfaceTypeInfo(LLVMCompiler compiler, LiTypeInfo typeInfo)
            : base(compiler, typeInfo) {

        }

        protected override void ProcessDependenceCore() {
            base.ProcessDependenceCore();
            var index = 0u;
            foreach (var i in m_MetadataType.Methods) {
                m_InterfaceTerms.Add(new LLVMInterfaceTerm(m_Compiler, i.Value, index++));
            }
            foreach (var i in m_Interfaces) {
                foreach (var j in i.m_InterfaceTerms) {
                    if (!m_InterfaceTerms.Contains(j)) m_InterfaceTerms.Add(j.Clone(index++));
                }
            }
        }

        protected override void GenerateVTableCore() {
            var interfaceTerms = m_InterfaceTerms.ToList();
            var vtableTypes = new List<LLVMTypeRef>();
            interfaceTerms.Sort((a, b) => a.InterfaceIndex.CompareTo(b.InterfaceIndex));

            vtableTypes.Add(m_Compiler.InterfaceHeaderType); // interface VTable header
            vtableTypes.AddRange(interfaceTerms.Select((e) => LLVMTypeRef.CreatePointer(m_Compiler.ResolveLLVMMethod(e.TemplateEntry).FunctionType, 0)));
            m_VtableType.StructSetBody(vtableTypes.ToArray(), false);
        }

        protected override LLVMCompType SetupLLVMTypesCore() {
            base.SetupLLVMTypesCore();

            m_InterfaceType = m_Compiler.Context.CreateNamedStruct($"ref.{m_MetadataType.FullName}");
            m_InterfaceType.StructSetBody(Array.Empty<LLVMTypeRef>(), false);

            m_InstanceType = LLVMCompType.CreateType(LLVMTypeTag.TypePointer | LLVMTypeTag.Interface, LLVMTypeRef.CreatePointer(m_InterfaceType, 0));
            return m_InstanceType;
        }
        public override int LocateMethodInMainTable(LLVMMethodInfoWrapper method) {
            var key = new LLVMInterfaceTerm(m_Compiler, method.Method, 0);
            if (!m_InterfaceTerms.TryGetValue(key, out var term)) return -1;
            return (int)term.InterfaceIndex;
        }
        public int LocateMethodInMainTableStrict(LLVMMethodInfoWrapper method, LLVMInterfaceTypeInfo fromInterface) {
            var key = new LLVMInterfaceTerm(m_Compiler, method.Method, 0);
            if (!m_InterfaceTerms.TryGetValue(key, out var term)) return -1;
            if (m_Compiler.ResolveLLVMMethod(term.TemplateEntry).DeclType != fromInterface) return -1;
            return (int)term.InterfaceIndex;
        }

    }

}
