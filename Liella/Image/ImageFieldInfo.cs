using Liella.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Image {
    public class FieldInfo : LiFieldInfo{
        protected FieldAttributes m_Flags;
        protected string m_Name;
        protected TypeEntry m_Type;
        protected TypeEntry m_DeclType;
        protected FieldDefinition m_Definition;
        protected uint m_LayoutOffset;
        protected uint m_FieldIndex;
        public override TypeEntry Type => m_Type;
        public override TypeEntry DeclType => m_DeclType;
        public override uint FieldIndex => m_FieldIndex;

        public FieldInfo(TypeEntry declType, TypeEntry fieldType, FieldDefinition fieldDef, uint index) {
            //if (declType.ToString().Contains("Delegate")) Debugger.Break();
            var reader = MetadataHelper.GetMetadataReader(ref fieldDef);
            m_Name = reader.GetString(fieldDef.Name);
            m_Definition = fieldDef;
            m_DeclType = declType;
            m_Type = fieldType;
            m_Flags = fieldDef.Attributes;
            m_FieldIndex = index;
        }
        public override string ToString() {
            return $"{m_Type} {m_DeclType}.{m_Name}";
        }
    }
}
