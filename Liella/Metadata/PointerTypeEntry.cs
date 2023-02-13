using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Metadata {
    public class PointerTypeEntry : TypeEntry {
        public TypeEntry ElementEntry;
        public PointerTypeEntry(TypeEntry elementEntry) {
            ElementEntry = elementEntry;
        }

        public override bool Equals(TypeEntry other) {
            return (other is PointerTypeEntry) && ElementEntry.Equals(((PointerTypeEntry)other).ElementEntry);
        }

        public override int GetHashCode() {
            return ElementEntry.GetHashCode() ^ 0x50000000;
        }
        public override string ToString() {
            return $"{ElementEntry}*";
        }
    }
}
