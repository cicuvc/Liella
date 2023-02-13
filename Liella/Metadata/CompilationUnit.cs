using System.Collections.Generic;

namespace Liella.Metadata {
    public abstract class CompilationUnitSet
    {
        protected Dictionary<string, TypeEntry> m_IntrinicsTypes = new Dictionary<string, TypeEntry>();
        public Dictionary<string, TypeEntry> IntrinicsTypes => m_IntrinicsTypes;
        public abstract Dictionary<TypeEntry, LiTypeInfo> ActiveTypes { get; }
        public abstract Dictionary<MethodEntry, MethodInstance> ActiveMethods { get; }
        public abstract LiSignatureDecoder SignatureDecoder { get; }
        
    }
}
