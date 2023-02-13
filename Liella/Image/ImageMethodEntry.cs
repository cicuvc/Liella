using Liella.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Image {
    public class ImageMethodEntryKey : ImageMethodEntry {
        public ImageMethodEntryKey WithMethodDef(MethodDefinition def) {
            m_MethodDef = def;
            return this;
        }
        public ImageMethodEntryKey WithDeclType(TypeEntry declType) {
            m_TypeEntry = declType;
            return this;
        }
        public ImageMethodEntryKey WithGenericTypes(ImmutableArray<TypeEntry> gTypes) {
            m_GenericType = gTypes;
            return this;
        }
    }
    public class ImageMethodEntry : MethodEntry, IEquatable<ImageMethodEntry> {

        protected MethodDefinition m_MethodDef;
        public MethodDefinition MethodDef => m_MethodDef;

        public override string Name {
            get {
                var reader = MetadataHelper.GetMetadataReader(ref m_MethodDef);
                return reader.GetString(m_MethodDef.Name);
            }
        }
        public ImageMethodEntry() { }
        public ImageMethodEntry(TypeEntry type, MethodDefinition def, ImmutableArray<TypeEntry> genericParams) {
            m_TypeEntry = type;
            m_MethodDef = def;
            m_GenericType = genericParams;
        }
        public ImageMethodEntry(TypeEntry type, MethodDefinition def)
            : this(type, def, ImmutableArray<TypeEntry>.Empty) { }

        public override int GetHashCode() {
            var hash = (int)TypeEntry.GetHashCode() ^ 0x10000000;
            foreach (var i in GenericType) hash ^= i.GetHashCode();
            return hash ^ (int)MetadataHelper.ExtractToken(ref m_MethodDef);
        }

        public bool Equals(ImageMethodEntry other) {
            if (TypeEntry.Equals((other).TypeEntry) && m_MethodDef.Equals(other.m_MethodDef)) {
                if (GenericType.Length != other.GenericType.Length) return false;
                for (int i = 0, length = GenericType.Length; i < length; i++)
                    if (!GenericType[i].Equals(other.GenericType[i]))
                        return false;
                return true;
            }
            return false;
        }
        public override bool Equals(object obj) {
            if (obj is ImageMethodEntry) return Equals((ImageMethodEntry)obj);
            return base.Equals(obj);
        }
        public override string ToString() {
            var reader = MetadataHelper.GetMetadataReader(ref m_MethodDef);
            if (GenericType.Length != 0) {
                var builder = new StringBuilder();
                builder.AppendJoin(",", GenericType);
                return $"{TypeEntry}.{reader.GetString(m_MethodDef.Name)}<{builder}>";
            }
            return $"{TypeEntry}.{reader.GetString(m_MethodDef.Name)}";
        }
    }
}
