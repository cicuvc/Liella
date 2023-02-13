using Liella.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Image {
    public class ImageGenericParamTypeEntry : TypeEntry {
        public uint GenericIndex;
        protected static Dictionary<uint, ImageGenericParamTypeEntry> m_Cache = new Dictionary<uint, ImageGenericParamTypeEntry>();
        public ImageGenericParamTypeEntry() { }
        public ImageGenericParamTypeEntry(uint index) {
            GenericIndex = index;
        }

        public override bool Equals(TypeEntry other) {
            return (other is ImageGenericParamTypeEntry) && (((ImageGenericParamTypeEntry)other).GenericIndex == GenericIndex);
        }
        public override int GetHashCode() {
            var hash = (int)0x01000000;
            return hash ^ (int)GenericIndex;
        }
        public override string ToString() {
            return $"({GenericIndex})";
        }
    }
}
