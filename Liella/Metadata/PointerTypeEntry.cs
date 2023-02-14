using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Metadata {
    public sealed class PointerTypeEntry : TypeEntry {
        private TypeEntry m_ElementEntry;
        public TypeEntry ElementEntry { get => m_ElementEntry; set { m_ElementEntry = value; } }
        public PointerTypeEntry(TypeEntry elementEntry) {
            m_ElementEntry = elementEntry;
        }

        public override bool Equals(TypeEntry other) {
            return (other is PointerTypeEntry) && m_ElementEntry.Equals(((PointerTypeEntry)other).m_ElementEntry);
        }

        public override int GetHashCode() {
            return m_ElementEntry.GetHashCode() ^ 0x50000000;
        }
        public override string ToString() {
            return $"{m_ElementEntry}*";
        }
    }
}
