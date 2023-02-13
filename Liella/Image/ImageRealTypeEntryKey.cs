using Liella.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Image {
    public class ImageRealTypeEntryKey : ImageRealTypeEntry {
        public ImageRealTypeEntryKey WithTypeDef(TypeDefinition definition) {
            m_TypeDef = definition;
            return this;
        }
    }
    public class ImageRealTypeEntry : TypeEntry {
        protected TypeDefinition m_TypeDef;
        public TypeDefinition TypeDef => m_TypeDef;
        public ImageRealTypeEntry() { }
        public ImageRealTypeEntry(TypeDefinition def) {
            m_TypeDef = def;
        }

        public override bool Equals(TypeEntry other) {
            return (other is ImageRealTypeEntry) && m_TypeDef.Equals(((ImageRealTypeEntry)other).m_TypeDef);
        }

        public override string ToString() {
            var fullName = ImageCompilationUnitSet.GetTypeDefinitionFullName(m_TypeDef);
            return fullName;
        }
        public override int GetHashCode() {
            var hash = (int)0x02000000;
            return hash ^ (int)MetadataHelper.MakeHash(ref m_TypeDef);
        }
    }
}
