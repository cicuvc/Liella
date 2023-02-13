using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Metadata {
    public abstract class TypeEntry : IEquatable<TypeEntry> {

        public abstract bool Equals(TypeEntry other);
        public abstract override int GetHashCode();
        public override bool Equals(object obj) {
            if (obj is TypeEntry) return Equals((TypeEntry)obj);
            return base.Equals(obj);
        }
    }

}
