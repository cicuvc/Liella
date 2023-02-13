using Liella.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Image {

    public class ImageCompilationUnitSet : CompilationUnitSet {
        protected ImageTypeEntryFactory m_TypeEntryFactory = new ImageTypeEntryFactory();
        protected ImageSignatureDecoder m_SignatureDecoder;
        protected Dictionary<MetadataReader, ImageAssembly> m_AssemblyList = new Dictionary<MetadataReader, ImageAssembly>();
        protected Dictionary<string, TypeDefinition> m_LoadedTypeList = new Dictionary<string, TypeDefinition>();
        protected Dictionary<TypeEntry, LiTypeInfo> m_ActiveTypeList = new Dictionary<TypeEntry, LiTypeInfo>();
        protected Dictionary<MethodEntry, MethodInstance> m_ActiveMethod = new Dictionary<MethodEntry, MethodInstance>();
        protected ImageAssembly m_MainAssembly;
        protected Queue<TypeEntry> m_TypeScanQueue = new Queue<TypeEntry>();
        protected Queue<ImageMethodEntry> m_MethodScanQueue = new Queue<ImageMethodEntry>();

        protected string[] m_IntrinicsTypeNames;

        public ImageTypeEntryFactory TypeEntryFactory { get => m_TypeEntryFactory; }
        public override LiSignatureDecoder SignatureDecoder => m_SignatureDecoder;
        public override Dictionary<TypeEntry, LiTypeInfo> ActiveTypes => m_ActiveTypeList;
        public override Dictionary<MethodEntry, MethodInstance> ActiveMethods => m_ActiveMethod;


        public ImageCompilationUnitSet(Stream mainAssembly, string[] intrinicsTypeNames) {
            m_MainAssembly = new ImageAssembly(mainAssembly);
            m_SignatureDecoder = new ImageSignatureDecoder(this);
            m_AssemblyList.Add(m_MainAssembly.Reader, m_MainAssembly);
            m_IntrinicsTypeNames = intrinicsTypeNames;
        }
        public void AddReference(Stream stream) {
            var referenceAssembly = new ImageAssembly(stream);
            m_AssemblyList.Add(referenceAssembly.Reader, referenceAssembly);
        }
        public TypeDefinition ResolveTypeByPrototypeName(string name) {
            return m_LoadedTypeList[name];
        }
        public TypeEntry ResolveTypeByHandle(EntityHandle typeHandle, MetadataReader reader, IGenericParameterContext context) {
            switch (typeHandle.Kind) {
                case HandleKind.TypeDefinition: {
                    return m_TypeEntryFactory.CreateTypeEntry(reader.GetTypeDefinition((TypeDefinitionHandle)typeHandle));
                }
                case HandleKind.TypeReference: {
                    return ResolveTypeByTypeReference((TypeReferenceHandle)typeHandle, reader);
                }
                case HandleKind.TypeSpecification: {
                    var spec = reader.GetTypeSpecification((TypeSpecificationHandle)typeHandle);
                    return spec.DecodeSignature(m_SignatureDecoder, context);
                }
                default: {
                    throw new NotImplementedException();
                }
            }
        }
        public FieldInfo ResolveStaticFieldByHandle(EntityHandle unkToken, MetadataReader reader, IGenericParameterContext context) {
            ImageTypeInfo typeInfo = null;
            FieldInfo fieldInfo = null;

            if (unkToken.Kind == HandleKind.FieldDefinition) {
                var fieldDef = reader.GetFieldDefinition((FieldDefinitionHandle)unkToken);
                var declType = reader.GetTypeDefinition(fieldDef.GetDeclaringType());
                var typeEntry = m_TypeEntryFactory.CreateTypeEntry(declType);
                typeInfo = m_ActiveTypeList[typeEntry] as ImageTypeInfo;
                fieldInfo = typeInfo.StaticFields[reader.GetString(fieldDef.Name)] as FieldInfo;
            } else {
                var memberRef = reader.GetMemberReference((MemberReferenceHandle)unkToken); ;
                var name = reader.GetString(memberRef.Name);
                if (memberRef.Parent.Kind == HandleKind.TypeReference) {
                    var parent = reader.GetTypeReference((TypeReferenceHandle)memberRef.Parent);
                    var fullName = m_SignatureDecoder.GetTypeReferenceFullName(reader, parent);
                    var declTypeDef = ResolveTypeByPrototypeName(fullName);
                    var declTypeEntry = m_TypeEntryFactory.CreateTypeEntry(declTypeDef);
                    typeInfo = m_ActiveTypeList[declTypeEntry] as ImageTypeInfo;
                    fieldInfo = typeInfo.StaticFields[(name)] as FieldInfo;
                } else {
                    var typeSpec = reader.GetTypeSpecification((TypeSpecificationHandle)memberRef.Parent);
                    var genericTypes = typeSpec.DecodeSignature(m_SignatureDecoder, context);
                    typeInfo = m_ActiveTypeList[genericTypes] as ImageTypeInfo;
                    fieldInfo = typeInfo.StaticFields[(name)] as FieldInfo;
                }
            }
            return fieldInfo;
        }
        public FieldInfo ResolveFieldByHandle(EntityHandle unkToken, MetadataReader reader, IGenericParameterContext context) {
            ImageTypeInfo typeInfo = null;
            FieldInfo fieldInfo = null;

            if (unkToken.Kind == HandleKind.FieldDefinition) {
                var fieldDef = reader.GetFieldDefinition((FieldDefinitionHandle)unkToken);
                var declType = reader.GetTypeDefinition(fieldDef.GetDeclaringType());
                var typeEntry = m_TypeEntryFactory.CreateTypeEntry(declType);
                typeInfo = m_ActiveTypeList[typeEntry] as ImageTypeInfo;
                fieldInfo = typeInfo.Fields[reader.GetString(fieldDef.Name)] as FieldInfo;
            } else {
                var memberRef = reader.GetMemberReference((MemberReferenceHandle)unkToken); ;
                var name = reader.GetString(memberRef.Name);
                if (memberRef.Parent.Kind == HandleKind.TypeReference) {
                    var parent = reader.GetTypeReference((TypeReferenceHandle)memberRef.Parent);
                    var fullName = m_SignatureDecoder.GetTypeReferenceFullName(reader, parent);
                    var declTypeDef = ResolveTypeByPrototypeName(fullName);
                    var declTypeEntry = m_TypeEntryFactory.CreateTypeEntry(declTypeDef);
                    typeInfo = m_ActiveTypeList[declTypeEntry] as ImageTypeInfo;
                    fieldInfo = typeInfo.Fields[(name)] as FieldInfo;
                } else {
                    var typeSpec = reader.GetTypeSpecification((TypeSpecificationHandle)memberRef.Parent);
                    var genericTypes = typeSpec.DecodeSignature(m_SignatureDecoder, context);
                    typeInfo = m_ActiveTypeList[genericTypes] as ImageTypeInfo;
                    fieldInfo = typeInfo.Fields[(name)] as FieldInfo;
                }
            }
            return fieldInfo;
        }
        public ImageMethodEntry ResolveMethodByHandle(EntityHandle methodHandle, MetadataReader reader, IGenericParameterContext context, out TypeEntry declType, out MethodSignature<TypeEntry> signature) {
            switch (methodHandle.Kind) {
                case HandleKind.MethodDefinition: {

                    var methodDef = reader.GetMethodDefinition((MethodDefinitionHandle)methodHandle);
                    declType = ResolveTypeByHandle(methodDef.GetDeclaringType(), reader, context);
                    var methodEntry = m_TypeEntryFactory.CreateMethodEntry(declType, methodDef, ImmutableArray<TypeEntry>.Empty);
                    signature = methodDef.DecodeSignature(SignatureDecoder, ActivateMethod(methodEntry));
                    return methodEntry;
                }
                case HandleKind.MemberReference: {
                    var methodDef = ResolveMethodDefFromMemberRef(methodHandle, reader, context, out declType, out signature);
                    return m_TypeEntryFactory.CreateMethodEntry(declType, methodDef, ImmutableArray<TypeEntry>.Empty);
                }
                case HandleKind.MethodSpecification: {
                    var methodDef = ResolveMethodDefFromMethodSpec((MethodSpecificationHandle)methodHandle, reader, context, out declType, out var specTypes);
                    var methodEntry = m_TypeEntryFactory.CreateMethodEntry(declType, methodDef, specTypes);
                    signature = methodDef.DecodeSignature(SignatureDecoder, ActivateMethod(methodEntry));
                    return methodEntry;
                }
                default: {
                    throw new NotImplementedException();
                }
            }
        }
        public TypeEntry ResolveTypeByTypeReference(TypeReferenceHandle typeRef, MetadataReader reader) {
            return m_SignatureDecoder.GetTypeFromReference(reader, typeRef, 0);
        }
        protected static void CollectNestedTypes(TypeDefinition typeDef, Action<TypeDefinition> callback) {
            var reader = MetadataHelper.GetMetadataReader(ref typeDef);
            foreach (var i in typeDef.GetNestedTypes()) {
                var nestedType = reader.GetTypeDefinition(i);
                callback(nestedType);
            }
        }
        public static string GetTypeDefinitionFullName(TypeDefinition typeDef) {
            var reader = MetadataHelper.GetMetadataReader(ref typeDef);
            if (typeDef.IsNested) // nested type
            {
                var scope = reader.GetTypeDefinition(typeDef.GetDeclaringType());
                return $"{GetTypeDefinitionFullName(scope)}::{reader.GetString(typeDef.Name)}";
            } else {
                return $"{reader.GetString(typeDef.Namespace)}::{reader.GetString(typeDef.Name)}";
            }
        }
        public void LoadTypes() {
            foreach (var i in m_AssemblyList.Values) {
                var types = i.Reader.TypeDefinitions;
                foreach (var j in types) {
                    var typeDef = i.Reader.GetTypeDefinition(j);
                    Action<TypeDefinition> callback = (type) => {
                        if (type.GetGenericParameters().Count != 0) return;
                        var fullName = GetTypeDefinitionFullName(type);
                        if (m_LoadedTypeList.ContainsKey(fullName)) {
                            return;
                            //throw new InvalidProgramException($"Conflict type {fullName}");
                        }
                        m_LoadedTypeList.Add(fullName, type);
                    };
                    callback(typeDef);
                    CollectNestedTypes(typeDef, callback);
                }
            }
        }
        public void CollectTypes() {
            var types = m_MainAssembly.Reader.TypeDefinitions.Select((e) => {
                return m_MainAssembly.Reader.GetTypeDefinition(e);
            }).Concat(m_IntrinicsTypeNames.Select((e) => {
                return m_LoadedTypeList[e];
            }));
            foreach (var j in types) {
                Action<TypeDefinition> callback = (type) => {
                    if (type.GetGenericParameters().Count != 0) return;
                    var typeEntry = m_TypeEntryFactory.CreateTypeEntry(type);
                    ActivateType(typeEntry);
                };
                callback(j);
                CollectNestedTypes(j, callback);
            }
            foreach (var i in m_ActiveTypeList) m_TypeScanQueue.Enqueue(i.Key);
            var primitiveType = typeof(PrimitiveTypeCode);
            foreach (var i in primitiveType.GetEnumNames()) {
                var typeEntry = m_TypeEntryFactory.CreateTypeEntry(m_LoadedTypeList[$"System::{i}"]);
                ActivateType(typeEntry);
            }
            while (m_TypeScanQueue.Count != 0) {
                var type = m_TypeScanQueue.Dequeue();
                ScanType(m_ActiveTypeList[type] as ImageTypeInfo);
            }
            foreach (var i in m_ActiveTypeList.Values) i.RegisterFields();
            foreach (var i in m_ActiveMethod.Values) i.DeclType.RegisterMethodInstance(i);

            foreach (var i in m_ActiveTypeList) {
                if (m_IntrinicsTypeNames.Contains(i.Value.FullName)) {
                    m_IntrinicsTypes.Add(i.Value.FullName, i.Value.Entry);
                }
            }
        }
        public ImageTypeInfo ActivateType(MetadataReader reader, EntityHandle handle) {
            if (handle.IsNil) return null;
            switch (handle.Kind) {
                case HandleKind.TypeDefinition: {
                    var baseTypeDefinition = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
                    return ActivateType(TypeEntryFactory.CreateTypeEntry(baseTypeDefinition));

                }
                case HandleKind.TypeReference: {
                    var baseTypeReference = reader.GetTypeReference((TypeReferenceHandle)handle);
                    var fullName = m_SignatureDecoder.GetTypeReferenceFullName(reader, baseTypeReference);
                    return ActivateType(TypeEntryFactory.CreateTypeEntry(ResolveTypeByPrototypeName(fullName)));
                }
                default: {
                    throw new NotImplementedException();
                }
            }
        }
        public ImageTypeInfo ActivateType(TypeEntry entry) {

            if (entry is ImageGenericParamTypeEntry) return null;
            if (entry is PointerTypeEntry) return ActivateType(((PointerTypeEntry)entry).ElementEntry);
            if (!m_ActiveTypeList.ContainsKey(entry)) {
                var entry_ = (ImageRealTypeEntry)entry;
                var typeDef = entry_.TypeDef;
                var reader = MetadataHelper.GetMetadataReader(ref typeDef);
                var genericParamCount = typeDef.GetGenericParameters().Count;
                if (typeDef.IsNested) {
                    var declType = reader.GetTypeDefinition(typeDef.GetDeclaringType());
                    if (entry_ is ImageInstanceTypeEntry) {
                        var genericParams = ((ImageInstanceTypeEntry)entry_).GenericType.RemoveRange(declType.GetGenericParameters().Count, genericParamCount - declType.GetGenericParameters().Count);
                        var declTypeEntry = m_TypeEntryFactory.CreateTypeEntry(declType, genericParams);
                        var declTypeInfo = ActivateType(declTypeEntry);
                        var typeInfo = new ImageTypeInfo(this, (ImageRealTypeEntry)entry, ActivateType(reader, typeDef.BaseType), declTypeInfo, declTypeInfo);
                        m_ActiveTypeList.Add(entry, typeInfo);
                    } else {
                        var declTypeEntry = m_TypeEntryFactory.CreateTypeEntry(declType);
                        var declTypeInfo = ActivateType(declTypeEntry);
                        var typeInfo = new ImageTypeInfo(this, (ImageRealTypeEntry)entry, ActivateType(reader, typeDef.BaseType), declTypeInfo, declTypeInfo);
                        m_ActiveTypeList.Add(entry, typeInfo);
                    }

                } else {
                    var typeInfo = new ImageTypeInfo(this, (ImageRealTypeEntry)entry, ActivateType(reader, typeDef.BaseType), DummyGenericParameterContext.Dummy);
                    m_ActiveTypeList.Add(entry, typeInfo);
                }
                m_TypeScanQueue.Enqueue(entry);
            }
            return m_ActiveTypeList[entry] as ImageTypeInfo;
        }

        public ImageMethodInstance ActivateMethod(ImageMethodEntry entry) {
            if (!m_ActiveMethod.ContainsKey(entry)) {
                var methodDef = entry.MethodDef;
                var reader = MetadataHelper.GetMetadataReader(ref methodDef);
                var peReader = m_AssemblyList[reader].PEReader;

                var methodInstance = new ImageMethodInstance(this, entry, entry.MethodDef.RelativeVirtualAddress != 0 ? peReader.GetMethodBody(entry.MethodDef.RelativeVirtualAddress) : null, ActivateType(entry.TypeEntry));
                m_ActiveMethod.Add(entry, methodInstance);

                ScanMethod(methodInstance);
            }
            return (ImageMethodInstance)m_ActiveMethod[entry];
        }

        public void ScanType(ImageTypeInfo type) {
            if (type.Entry.ToString().Contains("FP3")) Debugger.Break();
            var typeDef = ((ImageRealTypeEntry)type.Entry).TypeDef;
            var reader = MetadataHelper.GetMetadataReader(ref typeDef);
            var typeMethods = typeDef.GetMethods();
            var typeFields = typeDef.GetFields();
            foreach (var i in typeMethods) {
                var methodDef = reader.GetMethodDefinition(i);
                if (methodDef.GetGenericParameters().Count == 0) {
                    var methodEntry = m_TypeEntryFactory.CreateMethodEntry(type.Entry, methodDef, ImmutableArray<TypeEntry>.Empty);
                    ActivateMethod(methodEntry);
                }
            }
            //if (type.FullName.Contains("Delegate")) Debugger.Break();

            foreach (var i in typeFields) {
                var fieldDef = reader.GetFieldDefinition(i);
                var fieldType = fieldDef.DecodeSignature(m_SignatureDecoder, type);
                ActivateType(fieldType);
            }
        }

        public MethodDefinition ResolveMethodDefFromMethodSpec(MethodSpecificationHandle handle, MetadataReader reader, IGenericParameterContext context, out TypeEntry declTypeEntry, out ImmutableArray<TypeEntry> specTypes) {
            var methodSpec = reader.GetMethodSpecification(handle);
            specTypes = methodSpec.DecodeSignature(m_SignatureDecoder, context);

            if (methodSpec.Method.Kind == HandleKind.MethodDefinition) {
                var methodDef = reader.GetMethodDefinition((MethodDefinitionHandle)methodSpec.Method);
                declTypeEntry = m_TypeEntryFactory.CreateTypeEntry(reader.GetTypeDefinition(methodDef.GetDeclaringType()));

                return methodDef;
            } else {
                return ResolveMethodDefFromMemberRef(methodSpec.Method, reader, context, out declTypeEntry, out _);
            }

        }
        public MethodDefinition ResolveMethodDefFromMemberRef(EntityHandle handle, MetadataReader reader, IGenericParameterContext context, out TypeEntry declTypeEntry, out MethodSignature<TypeEntry> callSiteSignature) {
            var memberRef = reader.GetMemberReference((MemberReferenceHandle)handle);

            declTypeEntry = (memberRef.Parent.Kind == HandleKind.MethodDefinition) ? ResolveMethodByHandle(memberRef.Parent, reader, context, out _, out _).TypeEntry : ResolveTypeByHandle(memberRef.Parent, reader, context);
            ActivateType(declTypeEntry);

            var declTypeDef = ((ImageRealTypeEntry)declTypeEntry).TypeDef;
            var declTypeReader = MetadataHelper.GetMetadataReader(ref declTypeDef);
            var memberRefSig = callSiteSignature = memberRef.DecodeMethodSignature(m_SignatureDecoder, null);
            var memberRefName = reader.GetString(memberRef.Name);

            var methodDefHandle = ((ImageRealTypeEntry)declTypeEntry).TypeDef.GetMethods().Where((e) => {
                var eMethodDef = declTypeReader.GetMethodDefinition(e);
                if (memberRefName != declTypeReader.GetString(eMethodDef.Name)) return false;
                var sign = eMethodDef.DecodeSignature(m_SignatureDecoder, null);
                if (memberRefSig.GenericParameterCount != sign.GenericParameterCount || memberRefSig.ReturnType != sign.ReturnType) return false;
                if (sign.Header.CallingConvention.HasFlag(SignatureCallingConvention.VarArgs)) {
                    if (memberRefSig.ParameterTypes.Length < sign.ParameterTypes.Length) return false;
                } else {
                    if (memberRefSig.ParameterTypes.Length != sign.ParameterTypes.Length) return false;
                }
                for (int i = 0, length = sign.ParameterTypes.Length; i < length; i++)
                    if (memberRefSig.ParameterTypes[i] != sign.ParameterTypes[i])
                        return false;

                return true;
            });
            var methodDef = declTypeReader.GetMethodDefinition(methodDefHandle.First());
            return methodDef;
        }
        public void ScanMethod(ImageMethodInstance method) {
            //if (method.Entry.ToString().Contains("Payload::Main.MainX")) Debugger.Break();
            if (method.Body == null) return;
            if (method.LocalVaribleTypes.Length != 0) {
                var localSignature = method.Reader.GetStandaloneSignature(method.Body.LocalSignature);
                var localTypes = localSignature.DecodeLocalSignature(m_SignatureDecoder, method);
                foreach (var i in localTypes) ActivateType(i);
            }
            //Console.WriteLine(method.Entry.ToString());
            //Console.WriteLine(method.Reader.GetString(method.Definition.Name));

            method.PreprocessILCode();
            method.CollectToken(OperandType.InlineField, (uint token) => {
                if ((token >> 24) == 0x0A) {
                    var handle = (MemberReferenceHandle)MetadataHelper.CreateHandle(token); ;
                    var memberRef = method.Reader.GetMemberReference(handle);
                    if (memberRef.Parent.Kind == HandleKind.TypeSpecification) {
                        var typeSpec = method.Reader.GetTypeSpecification((TypeSpecificationHandle)memberRef.Parent);
                        var declType = typeSpec.DecodeSignature(m_SignatureDecoder, method);
                        var typeInfo = ActivateType(declType);
                        var fieldType = memberRef.DecodeFieldSignature(m_SignatureDecoder, typeInfo);
                        ActivateType(fieldType);
                    }
                }
            });
            method.CollectToken(OperandType.InlineMethod, (uint token) => {
                var unkHandle = MetadataHelper.CreateHandle(token);
                if (unkHandle.Kind == HandleKind.MemberReference) {
                    var methodDef = ResolveMethodDefFromMemberRef(unkHandle, method.Reader, method, out var declTypeEntry, out _);
                    var methodEntry = m_TypeEntryFactory.CreateMethodEntry(declTypeEntry, methodDef, ImmutableArray<TypeEntry>.Empty);
                    ActivateMethod(methodEntry);
                }
                if (unkHandle.Kind == HandleKind.MethodSpecification) {
                    var handle = (MethodSpecificationHandle)MetadataHelper.CreateHandle(token);
                    var methodSpec = method.Reader.GetMethodSpecification(handle);

                    if (methodSpec.Method.Kind == HandleKind.MethodDefinition) {
                        var methodDef = method.Reader.GetMethodDefinition((MethodDefinitionHandle)methodSpec.Method);
                        var typeEntry = m_TypeEntryFactory.CreateTypeEntry(method.Reader.GetTypeDefinition(methodDef.GetDeclaringType()));
                        var methodEntry = m_TypeEntryFactory.CreateMethodEntry(typeEntry, methodDef, methodSpec.DecodeSignature(m_SignatureDecoder, method));
                        ActivateMethod(methodEntry);
                    } else // member ref
                      {
                        var methodDef = ResolveMethodDefFromMemberRef(methodSpec.Method, method.Reader, method, out var declTypeEntry, out _);
                        var methodEntry = m_TypeEntryFactory.CreateMethodEntry(declTypeEntry, methodDef, methodSpec.DecodeSignature(m_SignatureDecoder, method));
                        ActivateMethod(methodEntry);
                    }
                    return;
                }
                if (unkHandle.Kind == HandleKind.MethodDefinition) {
                    var methodDef = method.Reader.GetMethodDefinition((MethodDefinitionHandle)unkHandle);
                    var typeEntry = m_TypeEntryFactory.CreateTypeEntry(method.Reader.GetTypeDefinition(methodDef.GetDeclaringType()));
                    var methodEntry = m_TypeEntryFactory.CreateMethodEntry(typeEntry, methodDef, ImmutableArray<TypeEntry>.Empty);
                    ActivateMethod(methodEntry);
                    return;
                }

            });
        }
    }

}
