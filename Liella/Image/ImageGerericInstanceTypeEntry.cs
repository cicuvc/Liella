using Liella.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Image {
    public class ImageGenericInstanceTypeEntryKey : ImageInstanceTypeEntry {
        public ImageGenericInstanceTypeEntryKey WithGeneric(ImmutableArray<TypeEntry> genericTypes) {
            GenericType = genericTypes;
            return this;
        }
        public ImageGenericInstanceTypeEntryKey WithTypeDef(TypeDefinition definition) {
            m_TypeDef = definition;
            return this;
        }
    }
    public class ImageInstanceTypeEntry : ImageRealTypeEntry {
        public ImmutableArray<TypeEntry> GenericType;

        public ImageInstanceTypeEntry() { }
        public ImageInstanceTypeEntry(TypeDefinition def, ImmutableArray<TypeEntry> genericParams)
            : base(def) {
            GenericType = genericParams;
        }
        public override string ToString() {
            var fullName = ImageCompilationUnitSet.GetTypeDefinitionFullName(m_TypeDef);
            if (GenericType.Length != 0) {
                var genericList = new StringBuilder();
                genericList.AppendJoin(',', GenericType);
                return $"{fullName}<{genericList}>";
            }
            return fullName;
        }
        public override bool Equals(TypeEntry other) {
            if ((other is ImageInstanceTypeEntry other_) && m_TypeDef.Equals((other_).TypeDef)) {
                if (GenericType.Length != other_.GenericType.Length) return false;
                for (int i = 0, length = GenericType.Length; i < length; i++)
                    if (!GenericType[i].Equals(other_.GenericType[i]))
                        return false;
                return true;
            }
            return false;
        }
        public override int GetHashCode() {
            var hash = (int)0x03000000;
            foreach (var i in GenericType) hash ^= i.GetHashCode();
            return hash ^ (int)MetadataHelper.MakeHash(ref m_TypeDef);
        }
    }
}
