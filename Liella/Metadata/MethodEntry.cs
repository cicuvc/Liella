using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Metadata {
    public abstract class MethodEntry {
        protected ImmutableArray<TypeEntry> m_GenericType;
        protected TypeEntry m_TypeEntry;

        public ImmutableArray<TypeEntry> GenericType => m_GenericType;
        public TypeEntry TypeEntry => m_TypeEntry;
        public abstract string Name { get; }
    }
}
