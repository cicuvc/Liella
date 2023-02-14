using Liella.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM {
    public sealed class LLVMInterfaceTerm {
        private LLVMCompiler m_Compiler;
        private uint m_InterfaceIndex;
        private string m_MethodName;
        private TypeEntry m_ReturnType;
        private ImmutableArray<TypeEntry> m_ParamTypes = ImmutableArray<TypeEntry>.Empty;
        private int m_HashCode;
        private MethodEntry m_MethodEntry;

        public uint InterfaceIndex => m_InterfaceIndex;
        public MethodEntry TemplateEntry => m_MethodEntry;
        public LLVMInterfaceTerm() { }
        public LLVMInterfaceTerm(LLVMCompiler compiler, MethodInstance template, uint index) {
            m_Compiler = compiler;
            m_InterfaceIndex = index;
            UpdateTermInfo(template, index);

        }
        public LLVMInterfaceTerm Clone(uint index) {
            return new LLVMInterfaceTerm() {
                m_Compiler = m_Compiler,
                m_MethodName = m_MethodName,
                m_ReturnType = m_ReturnType,
                m_ParamTypes = m_ParamTypes,
                m_HashCode = m_HashCode,
                m_InterfaceIndex = index,
                m_MethodEntry = m_MethodEntry
            };
        }
        public void UpdateTermInfo(MethodInstance template, uint index) {
            m_MethodName = template.Entry.Name;
            m_ReturnType = template.Signature.ReturnType;
            m_ParamTypes = template.Signature.ParameterTypes;
            var hashCode = m_MethodName.GetHashCode() ^ m_ReturnType.GetHashCode();
            foreach (var i in m_ParamTypes) hashCode ^= i.GetHashCode();
            m_HashCode = hashCode;
            m_InterfaceIndex = index;
            m_MethodEntry = template.Entry;
        }

        public override bool Equals(object obj) {
            if (obj is LLVMInterfaceTerm method) {
                if (method.m_MethodName != m_MethodName) return false;
                if (method.m_ParamTypes.SequenceEqual(m_ParamTypes) && method.m_ReturnType == m_ReturnType) {
                    return true;
                }
            }
            return base.Equals(obj);
        }
        public override int GetHashCode() {
            return m_HashCode;
        }

    }

}
