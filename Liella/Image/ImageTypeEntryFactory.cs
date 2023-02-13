using Liella.Metadata;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Image {
    public class ImageTypeEntryFactory {
        protected Hashtable m_Cache = new Hashtable();
        protected ImageGenericInstanceTypeEntryKey m_GenericInstnaceKey = new ImageGenericInstanceTypeEntryKey();
        protected ImageRealTypeEntryKey m_RealTypeKey = new ImageRealTypeEntryKey();
        protected ImageGenericParamTypeEntry m_GPKey = new ImageGenericParamTypeEntry();
        protected ImageMethodEntryKey m_MethodEntryKey = new ImageMethodEntryKey();
        protected PointerTypeEntry m_PointerEntryKey = new PointerTypeEntry(null);
        public TypeEntry CreateTypeEntry(TypeDefinition def, ImmutableArray<TypeEntry> genericParams) {
            var key = m_GenericInstnaceKey.WithTypeDef(def).WithGeneric(genericParams);
            if (m_Cache.ContainsKey(key)) return (TypeEntry)m_Cache[key];
            var newTypeEntry = new ImageInstanceTypeEntry(def, genericParams);
            m_Cache.Add(newTypeEntry, newTypeEntry);
            return newTypeEntry;
        }
        public TypeEntry CreateTypeEntry(TypeDefinition def) {
            var key = m_RealTypeKey.WithTypeDef(def);
            if (m_Cache.ContainsKey(key)) return (TypeEntry)m_Cache[key];
            var newTypeEntry = new ImageRealTypeEntry(def);
            m_Cache.Add(newTypeEntry, newTypeEntry);
            return newTypeEntry;
        }
        public TypeEntry CreateTypeEntry(TypeEntry elementEntry) {
            m_PointerEntryKey.ElementEntry = elementEntry;
            if (m_Cache.ContainsKey(m_PointerEntryKey)) return (TypeEntry)m_Cache[m_PointerEntryKey];
            var newTypeEntry = new PointerTypeEntry(elementEntry);
            m_Cache.Add(newTypeEntry, newTypeEntry);
            return newTypeEntry;
        }
        public TypeEntry CreateTypeEntry(uint index) {
            m_GPKey.GenericIndex = index;
            if (m_Cache.ContainsKey(m_GPKey)) return (TypeEntry)m_Cache[m_GPKey];
            var newTypeEntry = new ImageGenericParamTypeEntry(index);
            m_Cache.Add(newTypeEntry, newTypeEntry);
            return newTypeEntry;
        }
        public ImageMethodEntry CreateMethodEntry(TypeEntry type, MethodDefinition def, ImmutableArray<TypeEntry> genericParams) {
            var key = m_MethodEntryKey.WithMethodDef(def).WithGenericTypes(genericParams).WithDeclType(type);
            if (m_Cache.ContainsKey(key)) return (ImageMethodEntry)m_Cache[key];
            var newMethodEntry = new ImageMethodEntry(type, def, genericParams);
            m_Cache.Add(newMethodEntry, newMethodEntry);
            return newMethodEntry;
        }
    }

}
