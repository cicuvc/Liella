using Liella.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Image {
    public class ImageTypeInfo : LiTypeInfo, IGenericParameterContext {
        protected ImageCompilationUnitSet m_TypeEnv;
        public override TypeEntry Entry { get => m_Entry; }
        protected ImageRealTypeEntry m_Entry;
        protected IGenericParameterContext m_ParentGenericContext;
        protected SortedList<string, LiFieldInfo> m_Fields = new SortedList<string, LiFieldInfo>();
        protected SortedList<string, LiFieldInfo> m_StaticFields = new SortedList<string, LiFieldInfo>();
        protected Dictionary<MethodEntry, MethodInstance> m_Methods = new Dictionary<MethodEntry, MethodInstance>();
        protected List<MethodImplInfo> m_ImplInfo = new List<MethodImplInfo>();
        protected TypeLayout m_Layout;

        protected MetadataReader m_Reader;
        protected ImageTypeInfo[] m_Interfaces = null;
        protected ImageTypeInfo m_DeclType = null;

        protected ImageTypeInfo m_BaseType;
        protected bool m_IsValueType;
        protected string m_FullName;

        public override LiTypeInfo BaseType => m_BaseType;
        public override string FullName => m_FullName;
        public override bool IsValueType => m_IsValueType;
        public override SortedList<string, LiFieldInfo> Fields => m_Fields;
        public override SortedList<string, LiFieldInfo> StaticFields => m_StaticFields;
        public override TypeAttributes Attributes => ((ImageRealTypeEntry)Entry).TypeDef.Attributes;
        public override ImageCompilationUnitSet TypeEnv => m_TypeEnv;
        public override Dictionary<MethodEntry, MethodInstance> Methods => m_Methods;
        public override ImageTypeInfo[] Interfaces => m_Interfaces;
        public override List<MethodImplInfo> ImplInfo => m_ImplInfo;
        public override TypeLayout Layout => m_Layout;

        public override string ToString() {
            return Entry.ToString();
        }
        public override void RegisterFields() {
            var typeDef = ((ImageRealTypeEntry)Entry).TypeDef;
            var reader = MetadataHelper.GetMetadataReader(ref typeDef);
            var index = 0u;
            var staticIndex = 0u;
            foreach (var i in typeDef.GetFields()) {
                var fldDef = reader.GetFieldDefinition(i);
                var fldName = reader.GetString(fldDef.Name);
                var type = fldDef.DecodeSignature(m_TypeEnv.SignatureDecoder, this);
                if (fldDef.Attributes.HasFlag(FieldAttributes.Static)) {
                    m_StaticFields.Add(fldName, new FieldInfo(Entry, type, fldDef, staticIndex++));
                } else {
                    m_Fields.Add(fldName, new FieldInfo(Entry, type, fldDef, index++));
                }
            }
        }
        public override void RegisterMethodInstance(MethodInstance methodInstance) => m_Methods.Add(methodInstance.Entry, methodInstance);

        public ImageTypeInfo(ImageCompilationUnitSet env, ImageRealTypeEntry entry, ImageTypeInfo baseType, IGenericParameterContext genericContext, ImageTypeInfo declType)
            : this(env, entry, baseType, genericContext) {
            m_DeclType = declType;
        }
        public ImageTypeInfo(ImageCompilationUnitSet env, ImageRealTypeEntry entry, ImageTypeInfo baseType, IGenericParameterContext genericContext) {
            var typeDef = ((ImageRealTypeEntry)entry).TypeDef;

            m_TypeEnv = env;
            m_Entry = entry;
            m_ParentGenericContext = genericContext;
            m_BaseType = baseType;
            m_FullName = entry.ToString();
            m_Reader = MetadataHelper.GetMetadataReader(ref typeDef);
            m_Layout = typeDef.GetLayout();


            switch (m_FullName) {
                case "System::Object": {
                    m_IsValueType = false;
                    break;
                }
                case "System::ValueType": {
                    m_IsValueType = true;
                    break;
                }
                case "::<Module>": {
                    m_IsValueType = false;
                    break;
                }
                default: {
                    if (baseType == null) {
                        m_IsValueType = false;
                    } else {
                        m_IsValueType = m_BaseType.IsValueType;
                    }

                    break;
                }
            }

            var interfaceImpl = entry.TypeDef.GetInterfaceImplementations();
            m_Interfaces = new ImageTypeInfo[interfaceImpl.Count];
            var index = 0;
            foreach (var i in interfaceImpl) {
                var impl = m_Reader.GetInterfaceImplementation(i);
                var typeEntry = m_TypeEnv.ResolveTypeByHandle(impl.Interface, m_Reader, this);
                m_Interfaces[index++] = m_TypeEnv.ActivateType(typeEntry);
            }
            var methodImpl = entry.TypeDef.GetMethodImplementations();
            foreach (var i in methodImpl) {
                var implInfo = m_Reader.GetMethodImplementation(i);
                var interfaceDecl = m_TypeEnv.ResolveMethodByHandle(implInfo.MethodDeclaration, m_Reader, this, out var interfaceEntry, out _);
                var implBody = m_TypeEnv.ResolveMethodByHandle(implInfo.MethodBody, m_Reader, this, out var _, out _);

                m_ImplInfo.Add(MethodImplInfo.CreateRecord(interfaceDecl, implBody));
            }
        }
        public ImageMethodInstance MatchSignature(ImageMethodInstance pattern) {
            foreach (var i in m_Methods.Values) {
                if (i.EqualsSignature(pattern)) return i as ImageMethodInstance;
            }
            return m_BaseType?.MatchSignature(pattern);
        }

        public TypeEntry GetMethodGenericByIndex(CompilationUnitSet env, uint index) {
            throw new NotImplementedException();
        }

        public TypeEntry GetTypeGenericByIndex(CompilationUnitSet env, uint index) {
            if (Entry is ImageInstanceTypeEntry) {
                var entry_ = (ImageInstanceTypeEntry)Entry;
                if (entry_.GenericType.Length <= index) throw new Exception("");
                return entry_.GenericType[(int)(index)];
            } else {
                return (env as ImageCompilationUnitSet).TypeEntryFactory.CreateTypeEntry(index);
            }

        }

        public override MethodInstance FindMethodByName(string name) {
            foreach (var i in m_Methods) {
                if (i.Value.Entry.Name == name) {
                    return i.Value;
                }
            }
            return null;
        }
    }

}
