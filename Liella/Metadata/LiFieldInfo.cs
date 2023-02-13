using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Metadata {
    public abstract class LiFieldInfo {
        public abstract TypeEntry Type { get; }
        public abstract TypeEntry DeclType { get; }
        public abstract uint FieldIndex { get; }
    }
}
