using Liella.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Image {
    public class ImageGenericParamTypeEntry : TypeEntry {
        private uint m_GenericIndex;
        public uint GenericIndex { get => m_GenericIndex; set { m_GenericIndex = value; } }
        private static Dictionary<uint, ImageGenericParamTypeEntry> m_Cache = new Dictionary<uint, ImageGenericParamTypeEntry>();
        public ImageGenericParamTypeEntry() { }
        public ImageGenericParamTypeEntry(uint index) {
            m_GenericIndex = index;
        }

        public override bool Equals(TypeEntry other) {
            return (other is ImageGenericParamTypeEntry) && (((ImageGenericParamTypeEntry)other).m_GenericIndex == m_GenericIndex);
        }
        public override int GetHashCode() {
            var hash = (int)0x01000000;
            return hash ^ (int)m_GenericIndex;
        }
        public override string ToString() {
            return $"({m_GenericIndex})";
        }
    }
}
