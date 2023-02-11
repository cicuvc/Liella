using Liella.Project;
using LLVMSharp.Interop;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Liella {
    public class LiteAssembly {
        protected Stream m_AssemblyStream;
        protected MetadataReader m_Reader;
        protected PEReader m_PEReader;
        public PEReader PEReader { get => m_PEReader; }
        public MetadataReader Reader { get => m_Reader; }
        public unsafe LiteAssembly(Stream stream) {
            m_AssemblyStream = stream;
            m_PEReader = new PEReader(stream);
            var metadata = m_PEReader.GetMetadata();
            m_Reader = MetadataHelper.CreateMetadataReader(metadata.Pointer, metadata.Length);
        }
    }

    public class SignatureDecoder : ISignatureTypeProvider<TypeEntry, IGenericParameterContext>, ICustomAttributeTypeProvider<TypeEntry> {
        protected TypeEnvironment m_TypeEnv;
        public SignatureDecoder(TypeEnvironment env) {
            m_TypeEnv = env;
        }
        public TypeEntry GetArrayType(TypeEntry elementType, ArrayShape shape) {
            throw new NotImplementedException();
        }

        public TypeEntry GetByReferenceType(TypeEntry elementType) {
            return m_TypeEnv.TypeEntryFactory.CreateTypeEntry(elementType);
        }

        public TypeEntry GetFunctionPointerType(MethodSignature<TypeEntry> signature) {
            var primitiveTypeDef = m_TypeEnv.ResolveTypeByPrototypeName($"System::IntPtr");
            return m_TypeEnv.TypeEntryFactory.CreateTypeEntry(primitiveTypeDef);
        }

        public TypeEntry GetGenericInstantiation(TypeEntry genericType, ImmutableArray<TypeEntry> typeArguments) {
            return m_TypeEnv.TypeEntryFactory.CreateTypeEntry(((RealTypeEntry)genericType).TypeDef, typeArguments);
        }

        public TypeEntry GetGenericMethodParameter(IGenericParameterContext genericContext, int index) {
            if (genericContext == null) return m_TypeEnv.TypeEntryFactory.CreateTypeEntry((uint)index);
            return genericContext.GetMethodGenericByIndex(m_TypeEnv, (uint)index);
        }

        public TypeEntry GetGenericTypeParameter(IGenericParameterContext genericContext, int index) {
            if (genericContext == null) return m_TypeEnv.TypeEntryFactory.CreateTypeEntry((uint)index);
            return genericContext.GetTypeGenericByIndex(m_TypeEnv, (uint)index);
        }

        public TypeEntry GetModifiedType(TypeEntry modifier, TypeEntry unmodifiedType, bool isRequired) {
            return unmodifiedType;
            //throw new NotImplementedException();
        }

        public TypeEntry GetPinnedType(TypeEntry elementType) {
            throw new NotImplementedException();
        }

        public TypeEntry GetPointerType(TypeEntry elementType) {
            return m_TypeEnv.TypeEntryFactory.CreateTypeEntry(elementType);
        }

        public TypeEntry GetPrimitiveType(PrimitiveTypeCode typeCode) {
            var primitiveTypeDef = m_TypeEnv.ResolveTypeByPrototypeName($"System::{typeCode}");
            return m_TypeEnv.TypeEntryFactory.CreateTypeEntry(primitiveTypeDef);
        }

        public TypeEntry GetSZArrayType(TypeEntry elementType) {
            throw new NotImplementedException();
        }

        public TypeEntry GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) {
            var typeDef = reader.GetTypeDefinition(handle);
            return m_TypeEnv.TypeEntryFactory.CreateTypeEntry(typeDef);
        }

        public string GetTypeReferenceFullName(MetadataReader reader, TypeReference typeReference) {
            if (typeReference.ResolutionScope.Kind == HandleKind.TypeReference) // nested type
            {
                var scope = reader.GetTypeReference((TypeReferenceHandle)typeReference.ResolutionScope);
                return $"{GetTypeReferenceFullName(reader, scope)}::{reader.GetString(typeReference.Name)}";
            } else {
                return $"{reader.GetString(typeReference.Namespace)}::{reader.GetString(typeReference.Name)}";
            }
        }

        public TypeEntry GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) {
            var typeRef = reader.GetTypeReference(handle);
            var fullName = GetTypeReferenceFullName(reader, typeRef);
            var typeDef = m_TypeEnv.ResolveTypeByPrototypeName(fullName);
            return m_TypeEnv.TypeEntryFactory.CreateTypeEntry(typeDef);
        }

        public TypeEntry GetTypeFromSpecification(MetadataReader reader, IGenericParameterContext genericContext, TypeSpecificationHandle handle, byte rawTypeKind) {
            throw new NotImplementedException();
        }

        public TypeEntry GetSystemType() {
            throw new NotImplementedException();
        }

        public bool IsSystemType(TypeEntry type) {
            throw new NotImplementedException();
        }

        public TypeEntry GetTypeFromSerializedName(string name) {
            throw new NotImplementedException();
        }

        public PrimitiveTypeCode GetUnderlyingEnumType(TypeEntry type) {
            throw new NotImplementedException();
        }
    }
    public class TypeEnvironment {
        protected TypeEntry.TypeEntryFactory m_TypeEntryFactory = new TypeEntry.TypeEntryFactory();
        protected SignatureDecoder m_SignatureDecoder;
        protected Dictionary<MetadataReader, LiteAssembly> m_AssemblyList = new Dictionary<MetadataReader, LiteAssembly>();
        protected Dictionary<string, TypeDefinition> m_LoadedTypeList = new Dictionary<string, TypeDefinition>();
        protected Dictionary<TypeEntry, TypeInfo> m_ActiveTypeList = new Dictionary<TypeEntry, TypeInfo>();
        protected Dictionary<MethodEntry, MethodInstanceInfo> m_ActiveMethod = new Dictionary<MethodEntry, MethodInstanceInfo>();
        protected LiteAssembly m_MainAssembly;
        protected Queue<TypeEntry> m_TypeScanQueue = new Queue<TypeEntry>();
        protected Queue<MethodEntry> m_MethodScanQueue = new Queue<MethodEntry>();
        protected ILReader m_ILReader;
        protected Dictionary<string, TypeEntry> m_IntrinicsTypes = new Dictionary<string, TypeEntry>();
        protected string[] m_IntrinicsTypeNames;

        public TypeEntry.TypeEntryFactory TypeEntryFactory { get => m_TypeEntryFactory; }
        public SignatureDecoder SignatureDecoder => m_SignatureDecoder;
        public Dictionary<TypeEntry, TypeInfo> ActiveTypes => m_ActiveTypeList;
        public Dictionary<MethodEntry, MethodInstanceInfo> ActiveMethods => m_ActiveMethod;
        public Dictionary<string, TypeEntry> IntrinicsTypes => m_IntrinicsTypes;

        public ILReader TokenReader => m_ILReader;

        public TypeEnvironment(Stream mainAssembly, string[] intrinicsTypeNames) {
            m_MainAssembly = new LiteAssembly(mainAssembly);
            m_SignatureDecoder = new SignatureDecoder(this);
            m_AssemblyList.Add(m_MainAssembly.Reader, m_MainAssembly);
            m_ILReader = new ILReader(this);
            m_IntrinicsTypeNames = intrinicsTypeNames;
        }
        public void AddReference(Stream stream) {
            var referenceAssembly = new LiteAssembly(stream);
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
        public MethodEntry ResolveMethodByHandle(EntityHandle methodHandle, MetadataReader reader, IGenericParameterContext context, out TypeEntry declType, out MethodSignature<TypeEntry> signature) {
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
        public bool VerifyIntrinicsMethods(string[] methodNames) {
            var namesSet = new SortedSet<string>(methodNames);
            foreach (var i in m_ActiveMethod) {
                var fullName = i.Value.Entry.ToString();
                if (namesSet.Contains(fullName)) namesSet.Remove(fullName);
            }
            return namesSet.Count == 0;
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
                ScanType(m_ActiveTypeList[type]);
            }
            foreach (var i in m_ActiveTypeList.Values) i.RegisterFields();
            foreach (var i in m_ActiveMethod.Values) i.DeclType.RegisterMethodInstance(i);

            foreach (var i in m_ActiveTypeList) {
                if (m_IntrinicsTypeNames.Contains(i.Value.FullName)) {
                    m_IntrinicsTypes.Add(i.Value.FullName, i.Value.Entry);
                }
            }
        }
        public TypeInfo ActivateType(MetadataReader reader, EntityHandle handle) {
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
        public TypeInfo ActivateType(TypeEntry entry) {

            if (entry is GenericParamTypeEntry) return null;
            if (entry is PointerTypeEntry) return ActivateType(((PointerTypeEntry)entry).ElementEntry);
            if (!m_ActiveTypeList.ContainsKey(entry)) {
                var entry_ = (RealTypeEntry)entry;
                var typeDef = entry_.TypeDef;
                var reader = MetadataHelper.GetMetadataReader(ref typeDef);
                var genericParamCount = typeDef.GetGenericParameters().Count;
                if (typeDef.IsNested) {
                    var declType = reader.GetTypeDefinition(typeDef.GetDeclaringType());
                    if (entry_ is GenericInstanceTypeEntry) {
                        var genericParams = ((GenericInstanceTypeEntry)entry_).GenericType.RemoveRange(declType.GetGenericParameters().Count, genericParamCount - declType.GetGenericParameters().Count);
                        var declTypeEntry = m_TypeEntryFactory.CreateTypeEntry(declType, genericParams);
                        var declTypeInfo = ActivateType(declTypeEntry);
                        var typeInfo = new TypeInfo(this, (RealTypeEntry)entry, ActivateType(reader, typeDef.BaseType), declTypeInfo, declTypeInfo);
                        m_ActiveTypeList.Add(entry, typeInfo);
                    } else {
                        var declTypeEntry = m_TypeEntryFactory.CreateTypeEntry(declType);
                        var declTypeInfo = ActivateType(declTypeEntry);
                        var typeInfo = new TypeInfo(this, (RealTypeEntry)entry, ActivateType(reader, typeDef.BaseType), declTypeInfo, declTypeInfo);
                        m_ActiveTypeList.Add(entry, typeInfo);
                    }

                } else {
                    var typeInfo = new TypeInfo(this, (RealTypeEntry)entry, ActivateType(reader, typeDef.BaseType), DummyGenericParameterContext.Dummy);
                    m_ActiveTypeList.Add(entry, typeInfo);
                }
                m_TypeScanQueue.Enqueue(entry);
            }
            return m_ActiveTypeList[entry];
        }

        public MethodInstanceInfo ActivateMethod(MethodEntry entry) {
            if (!m_ActiveMethod.ContainsKey(entry)) {
                var reader = MetadataHelper.GetMetadataReader(ref entry.MethodDef);
                var peReader = m_AssemblyList[reader].PEReader;
                //if (entry.GenericType.Length != 0) Debugger.Break();
                var methodInstance = new MethodInstanceInfo(this, entry, entry.MethodDef.RelativeVirtualAddress != 0 ? peReader.GetMethodBody(entry.MethodDef.RelativeVirtualAddress) : null, ActivateType(entry.TypeEntry));
                m_ActiveMethod.Add(entry, methodInstance);

                /*if(entry.GenericType.Length!=0 && entry.MethodDef.Attributes.HasFlag(MethodAttributes.Virtual) && (!entry.MethodDef.Attributes.HasFlag(MethodAttributes.NewSlot)) && entry.TypeEntry is RealTypeEntry)
                {
                    if(methodInstance.BaseMethod != null) ActivateMethod(methodInstance.BaseMethod);
                }*/
                ScanMethod(methodInstance);
            }
            return m_ActiveMethod[entry];
        }

        public void ScanType(TypeInfo type) {
            if (type.Entry.ToString().Contains("FP3")) Debugger.Break();
            var typeDef = ((RealTypeEntry)type.Entry).TypeDef;
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

            var declTypeReader = MetadataHelper.GetMetadataReader(ref ((RealTypeEntry)declTypeEntry).TypeDef);
            var memberRefSig = callSiteSignature = memberRef.DecodeMethodSignature(m_SignatureDecoder, null);
            var memberRefName = reader.GetString(memberRef.Name);

            var methodDefHandle = ((RealTypeEntry)declTypeEntry).TypeDef.GetMethods().Where((e) => {
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
        public void ScanMethod(MethodInstanceInfo method) {
            //if (method.Entry.ToString().Contains("Payload::Main.MainX")) Debugger.Break();
            if (method.Body == null) return;
            if (!method.Body.LocalSignature.IsNil) {
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
    public class ILReader {
        protected TypeEnvironment m_TypeEnv;
        public ILReader(TypeEnvironment typeEnv) {
            m_TypeEnv = typeEnv;
        }
        public MethodEntry ResolveMethodToken(MethodInstanceInfo method, EntityHandle token) {
            var factory = m_TypeEnv.TypeEntryFactory;
            var reader = method.Reader;
            switch (token.Kind) {
                case HandleKind.MethodDefinition: {
                    var methodDef = reader.GetMethodDefinition((MethodDefinitionHandle)token);
                    var declType = reader.GetTypeDefinition(methodDef.GetDeclaringType());
                    var typeEntry = factory.CreateTypeEntry(declType);
                    var methodEntry = factory.CreateMethodEntry(typeEntry, methodDef, ImmutableArray<TypeEntry>.Empty);
                    return methodEntry;
                }
                case HandleKind.MemberReference: {
                    var methodDef = m_TypeEnv.ResolveMethodDefFromMemberRef(token, method.Reader, method, out var declType, out _);
                    var methodEntry = factory.CreateMethodEntry(declType, methodDef, System.Collections.Immutable.ImmutableArray<TypeEntry>.Empty);
                    return methodEntry;
                }
                case HandleKind.MethodSpecification: {
                    var spec = method.Reader.GetMethodSpecification((MethodSpecificationHandle)token);
                    var genericTypes = spec.DecodeSignature(m_TypeEnv.SignatureDecoder, method);
                    if (spec.Method.Kind == HandleKind.MethodDefinition) {
                        var methodDef = method.Reader.GetMethodDefinition((MethodDefinitionHandle)spec.Method);
                        var declType = m_TypeEnv.TypeEntryFactory.CreateTypeEntry(method.Reader.GetTypeDefinition(methodDef.GetDeclaringType()));
                        var methodEntry = factory.CreateMethodEntry(declType, methodDef, genericTypes);
                        return methodEntry;
                    } else {
                        var methodDef = m_TypeEnv.ResolveMethodDefFromMemberRef(spec.Method, method.Reader, method, out var declType, out _);
                        var methodEntry = factory.CreateMethodEntry(declType, methodDef, genericTypes);
                        return methodEntry;
                    }
                }
                default: {
                    throw new InvalidOperationException("Invalid method token");
                }
            }
        }
    }
    public class MethodSignature {
        protected string m_MethodName;

    }

    public class MethodInstanceInfo : IGenericParameterContext {
        protected TypeEnvironment m_TypeEnv;
        protected MethodEntry m_Entry;
        protected MetadataReader m_Reader;
        protected MethodDefinition m_Definition;
        protected MethodBodyBlock m_Body;
        protected StandaloneSignature m_LocalSignature;
        protected TypeInfo m_DeclaringType;
        protected ImmutableArray<byte> m_ILSequence;
        protected ImmutableArray<ushort> m_ILCodeStart = ImmutableArray<ushort>.Empty;
        protected ImmutableArray<ushort> m_ILBranch = ImmutableArray<ushort>.Empty;
        protected IGenericParameterContext m_ParentGenericContext;
        protected uint m_GenericParamStart;
        protected SortedList<Interval, MethodBasicBlock> m_BasicBlocks = new SortedList<Interval, MethodBasicBlock>(new Interval.IntervalTreeComparer());
        protected MethodSignature<TypeEntry> m_Signature;
        protected ImmutableArray<TypeEntry> m_LocalVaribleTypes = ImmutableArray<TypeEntry>.Empty;
        protected ImmutableHashSet<MethodInstanceInfo> m_BaseMethods = default;
        protected ImmutableDictionary<TypeEntry, CustomAttribute> m_CustomAttributes = ImmutableDictionary<TypeEntry, CustomAttribute>.Empty;
        protected ImmutableArray<ExceptionRegion> m_ExceptionTable;
        public MethodAttributes Attributes => m_Entry.MethodDef.Attributes;
        public MethodEntry Entry => m_Entry;
        public TypeEnvironment TypeEnv => m_TypeEnv;
        public MethodDefinition Definition { get => m_Definition; }
        public MethodBodyBlock Body { get => m_Body; }
        public StandaloneSignature LocalSignature { get => m_LocalSignature; }
        public MetadataReader Reader { get => m_Reader; }
        public TypeInfo DeclType { get => m_DeclaringType; }
        public ImmutableArray<byte> ILSequence => m_ILSequence;
        public ImmutableArray<ushort> ILStart => m_ILCodeStart;
        public MethodSignature<TypeEntry> Signature => m_Signature;
        public SortedList<Interval, MethodBasicBlock> BasicBlocks => m_BasicBlocks;
        public ImmutableArray<TypeEntry> LocalVaribleTypes => m_LocalVaribleTypes;

        public ImmutableHashSet<MethodInstanceInfo> BaseMethods => m_BaseMethods;
        public ImmutableDictionary<TypeEntry, CustomAttribute> CustomAttributes => m_CustomAttributes;

        public bool EqualsSignature(MethodInstanceInfo oth) {
            var othSig = oth.m_Signature;
            if (m_Signature.ReturnType != othSig.ReturnType) return false;
            if (m_Signature.ParameterTypes.Length != othSig.ParameterTypes.Length) return false;
            for (var i = 0; i < m_Signature.ParameterTypes.Length; i++) {
                if (!m_Signature.ParameterTypes[i].Equals(othSig.ParameterTypes[i])) return false;
            }
            return true;
        }
        public bool EqualsSignature(MethodSignature<TypeEntry> othSig) {
            if (m_Signature.ReturnType != othSig.ReturnType) return false;
            if (m_Signature.ParameterTypes.Length != othSig.ParameterTypes.Length) return false;
            for (var i = 0; i < m_Signature.ParameterTypes.Length; i++) {
                if (!m_Signature.ParameterTypes[i].Equals(othSig.ParameterTypes[i])) return false;
            }
            return true;
        }

        public MethodEntry MatchBaseMethod(TypeInfo baseClass) {
            if (baseClass == null) return null;
            var baseClassDef = baseClass.Definition;
            var signatureDecoder = m_TypeEnv.SignatureDecoder;
            var baseClassReader = MetadataHelper.GetMetadataReader(ref baseClassDef);
            foreach (var i in baseClassDef.GetMethods()) {
                var method = baseClassReader.GetMethodDefinition(i);
                var methodName = baseClassReader.GetString(method.Name);
                if (methodName == (m_Entry.Name)) {
                    var signature = method.DecodeSignature(signatureDecoder, this);
                    if (EqualsSignature(signature)) {
                        return m_TypeEnv.TypeEntryFactory.CreateMethodEntry(baseClass.Entry, method, m_Entry.GenericType);
                    }
                }
            }
            return null;
        }

        public MethodInstanceInfo(TypeEnvironment typeEnv, MethodEntry entry, MethodBodyBlock impl, TypeInfo declType) {
            m_TypeEnv = typeEnv;
            m_Reader = MetadataHelper.GetMetadataReader(ref ((RealTypeEntry)declType.Entry).TypeDef);
            m_Body = impl;
            m_Entry = entry;
            m_Definition = entry.MethodDef;
            


            m_DeclaringType = declType;
            m_LocalSignature = (impl == null || impl.LocalSignature.IsNil) ? default : m_Reader.GetStandaloneSignature(impl.LocalSignature);
            m_ILSequence = (impl != null) ? impl.GetILContent() : ImmutableArray<byte>.Empty;
            m_ParentGenericContext = declType;
            m_Signature = entry.MethodDef.DecodeSignature(declType.TypeEnv.SignatureDecoder, this);
            m_LocalVaribleTypes = (impl == null || impl.LocalSignature.IsNil) ? ImmutableArray<TypeEntry>.Empty : m_LocalSignature.DecodeLocalSignature(declType.TypeEnv.SignatureDecoder, this);
            m_ExceptionTable = (impl != null) ? impl.ExceptionRegions : ImmutableArray<ExceptionRegion>.Empty;

            if (entry.MethodDef.Attributes.HasFlag(MethodAttributes.Virtual) && !entry.MethodDef.Attributes.HasFlag(MethodAttributes.NewSlot)) {
                var baseMethods = new HashSet<MethodInstanceInfo>();
                var baseMethod = MatchBaseMethod(m_DeclaringType.BaseType);
                if (baseMethod != null) baseMethods.Add(m_TypeEnv.ActivateMethod(baseMethod));

                foreach (var i in m_DeclaringType.Interfaces) {
                    var implMethod = MatchBaseMethod(i);
                    if (implMethod != null) baseMethods.Add(m_TypeEnv.ActivateMethod(implMethod));
                }
                m_BaseMethods = baseMethods.ToImmutableHashSet();
            }

            var customAttrBuilder = m_CustomAttributes.ToBuilder();
            var customAttrs = entry.MethodDef.GetCustomAttributes().Select(e => m_Reader.GetCustomAttribute(e)).ToArray();
            foreach (var i in customAttrs) {
                var ctorMethod = typeEnv.ResolveMethodByHandle(i.Constructor, m_Reader, m_DeclaringType, out var attrType,out _);
                customAttrBuilder.Add(attrType, i);
            }
            m_CustomAttributes = customAttrBuilder.ToImmutable();

        }
        // Determine the start points of instructions
        public unsafe void PreprocessILCode() {
            if (m_ILCodeStart != ImmutableArray<ushort>.Empty) return;
            var builder = m_ILCodeStart.ToBuilder();
            var length = m_ILSequence.Length;
            using (var memBuffer = m_ILSequence.AsMemory().Pin()) {
                var pBuffer = (byte*)memBuffer.Pointer;
                for (int i = 0; i < length;) {
                    builder.Add((ushort)i);
                    var ilOpcode = (ILOpCode)pBuffer[i++];
                    if (((uint)ilOpcode) >= 249) {
                        ilOpcode = (ILOpCode)((((uint)ilOpcode) << 8) + pBuffer[i++]);
                    }
                    //if (ilOpcode == ILOpCode.Ldc_r8) Debugger.Break();
                    var operandLength = ilOpcode.GetOperandSize();
                    if (ilOpcode == ILOpCode.Switch) {
                        operandLength +=4*( *(int*)&pBuffer[i]);
                    }
                    i += operandLength;
                }
            }
            m_ILCodeStart = builder.ToImmutable();
        }
        public unsafe void CollectToken(OperandType type, Action<uint> callback) {
            using (var memBuffer = m_ILSequence.AsMemory().Pin()) {
                var pBuffer = (byte*)memBuffer.Pointer;
                foreach (var i in m_ILCodeStart) {
                    var start = i;
                    uint ilCode = pBuffer[start++];
                    ilCode = ilCode < 249 ? ilCode : (ilCode << 8) | pBuffer[start++];
                    var opcode = ((ILOpCode)ilCode).ToOpCode();

                    if (opcode.OperandType == type) callback(*(uint*)&pBuffer[start]);
                }
            }
        }

        public TypeEntry GetMethodGenericByIndex(TypeEnvironment env, uint index) {
            if (m_Entry.GenericType.Length <= index) throw new Exception();
            return m_Entry.GenericType[(int)(index)];
        }

        public TypeEntry GetTypeGenericByIndex(TypeEnvironment env, uint index) {
            return m_ParentGenericContext.GetTypeGenericByIndex(env, index);
        }
        public unsafe void MakeBasicBlocks() {
            
            if (!m_ILBranch.IsEmpty) return;
            using (var memBuffer = m_ILSequence.AsMemory().Pin()) {
                var builder = m_ILBranch.ToBuilder();
                var pBuffer = (byte*)memBuffer.Pointer;
                var codeStart = m_ILCodeStart;
                foreach (var i in codeStart) {
                    var codePos = i; ;
                    uint ilCode = pBuffer[codePos++];
                    ilCode = ilCode < 249 ? ilCode : (ilCode << 8) | pBuffer[codePos++];
                    var opcode = ((ILOpCode)ilCode).ToOpCode();
                    if (opcode == OpCodes.Leave_S || opcode == OpCodes.Leave
                        || opcode == OpCodes.Endfinally || opcode == OpCodes.Endfilter) continue;
                    if (opcode.FlowControl == (FlowControl.Branch) || opcode.FlowControl == (FlowControl.Cond_Branch) || opcode.FlowControl == (FlowControl.Return)) {
                        var branchOffset = m_ILCodeStart.BinarySearch(i);
                        builder.Add((ushort)branchOffset);
                    }
                }
                m_ILBranch = builder.ToImmutable();
                var mainBlock = new MethodBasicBlock(this, 0, (uint)m_ILCodeStart.Length);
                m_BasicBlocks.Add(mainBlock.Interval, mainBlock);
                foreach (var i in m_ILBranch) {
                    // locate jump target
                    var codePos = m_ILCodeStart[(int)i]; ;
                    uint ilCode = pBuffer[codePos++];
                    ilCode = ilCode < 249 ? ilCode : (ilCode << 8) | pBuffer[codePos++];
                    var opcode = ((ILOpCode)ilCode).ToOpCode();

                    
                    // cut false branch
                    var falseBranch = MethodBasicBlock.CutBasicBlock((uint)(i + 1), m_BasicBlocks);
                    if (falseBranch != null) {
                        var branchInstBlock0 = m_BasicBlocks[new Interval(i, i)];
                        branchInstBlock0.FalseExit = falseBranch;
                    }

                    switch ((ILOpCode)ilCode) {
                        case ILOpCode.Ret: {
                            continue;
                        }
                        case ILOpCode.Switch: {
                            var branchListLength = *(int*)&pBuffer[codePos];
                            codePos += 4; // skip length operand
                            var trueBranches = new MethodBasicBlock[branchListLength];
                            var branchOffsetStart = codePos + (branchListLength) * 4;
                            for (var j = 0u; j < branchListLength; j++) {
                                var switchTarget = (uint)((*(int*)&pBuffer[codePos + j * 4]) + branchOffsetStart);
                                var switchBranchIndex = (uint)m_ILCodeStart.BinarySearch((ushort)switchTarget);

                                trueBranches[j] = MethodBasicBlock.CutBasicBlock(switchBranchIndex, m_BasicBlocks);
                            }
                            var switchBlock = m_BasicBlocks[new Interval(i, i)];
                            switchBlock.TrueExit = trueBranches;
                            continue;
                        }
                        case ILOpCode.Br:
                        case ILOpCode.Br_s: {
                            m_BasicBlocks[new Interval(i, i)].FalseExit = null;
                            break;
                        }
                    }

                    uint branchTarget = 0;
                    if (opcode.OperandType == OperandType.ShortInlineBrTarget) {
                        branchTarget = (uint)((sbyte)pBuffer[codePos] + (codePos + 1));
                    } else {
                        branchTarget = (uint)((*(int*)&pBuffer[codePos]) + (codePos + 4));
                    }
                    var targetBranchIndex = (uint)m_ILCodeStart.BinarySearch((ushort)branchTarget);

                    // cut true branch

                    var trueBranch = MethodBasicBlock.CutBasicBlock(targetBranchIndex, m_BasicBlocks);
                    var branchInstBlock = m_BasicBlocks[new Interval(i, i)];
                    branchInstBlock.TrueExit = new MethodBasicBlock[] { trueBranch };
                    continue;
                }

            }
            /*foreach(var i in m_ExceptionTable) {
                var tryBlockIndex = (uint)m_ILCodeStart.BinarySearch((ushort)i.TryOffset);
                var tryBlockEndIndex = (uint)m_ILCodeStart.BinarySearch((ushort)(i.TryOffset + i.TryLength));
                MethodBasicBlock.CutBasicBlock(tryBlockEndIndex, m_BasicBlocks);
                MethodBasicBlock.CutBasicBlock(tryBlockIndex, m_BasicBlocks);

                //var handlerIndex = (uint)m_ILCodeStart.
            }*/
            foreach (var i in m_BasicBlocks) {
                var stackDelta = 0;
                //if (i.Key.Left == 42 && i.Key.Right == 48) Debugger.Break();
                ForEachIL(i.Key, (opcode, operand) => {
                    var delta = ILCodeExtension.s_StackDeltaTable[opcode];
                    if (delta != int.MaxValue) {
                        stackDelta += delta;
                    } else {
                        switch (opcode) {
                            case ILOpCode.Calli: {
                                var signature = (StandaloneSignatureHandle)MetadataHelper.CreateHandle((uint)operand);
                                var sigObj = Reader.GetStandaloneSignature(signature);
                                var targetSignature = sigObj.DecodeMethodSignature(TypeEnv.SignatureDecoder, this);
                                stackDelta -= targetSignature.ParameterTypes.Length;
                                stackDelta--; // ftn
                                if (!targetSignature.ReturnType.ToString().Equals("System::Void")) {
                                    stackDelta++;
                                }
                                break;
                            }
                            case ILOpCode.Newobj: {
                                var method = m_TypeEnv.ResolveMethodByHandle(MetadataHelper.CreateHandle((uint)operand), Reader, this, out var declType, out var signature);
                                stackDelta -= signature.ParameterTypes.Length;
                                stackDelta++;
                                break;
                            }
                            case ILOpCode.Callvirt:
                            case ILOpCode.Call: {
                                var method = m_TypeEnv.ResolveMethodByHandle(MetadataHelper.CreateHandle((uint)operand), Reader, this, out var declType, out var signature);
                                stackDelta -= signature.ParameterTypes.Length;
                                if (!method.MethodDef.Attributes.HasFlag(MethodAttributes.Static)) stackDelta--;
                                if (!signature.ReturnType.ToString().Equals("System::Void")) {
                                    stackDelta++;
                                }
                                break;
                            }
                            case ILOpCode.Ret: {
                                if (!m_Signature.ReturnType.ToString().Equals("System::Void")) {
                                    stackDelta--;
                                }
                                break;
                            }
                            default: {
                                throw new NotImplementedException();
                            }
                        }

                    }
                }, false);
                i.Value.StackDepthDelta = stackDelta;
            }
        }
        public unsafe void ForEachIL(Interval iv, Action<ILOpCode, ulong> callback, bool makeVirtualExit) {
            using (var memBuffer = m_ILSequence.AsMemory().Pin()) {
                var pBuffer = (byte*)memBuffer.Pointer;
                var lastOpCode = ILOpCode.Nop;
                for (int i = (int)iv.Left; i < iv.Right; i++) {
                    var instStart = m_ILCodeStart[i];
                    uint ilCode = pBuffer[instStart++];
                    ilCode = ilCode < 249 ? ilCode : (ilCode << 8) | pBuffer[instStart++];
                    //if (ilCode == (uint)ILOpCode.Switch) Debugger.Break();
                    var opcode = (lastOpCode = (ILOpCode)ilCode).ToOpCode();
                    ulong operand = 0;
                    switch (opcode.OperandType) {
                        case OperandType.InlineBrTarget:
                        case OperandType.InlineField:
                        case OperandType.InlineI:
                        case OperandType.InlineMethod:
                        case OperandType.InlineSig:
                        case OperandType.InlineString:
                        case OperandType.InlineTok:
                        case OperandType.InlineType:
                        case OperandType.InlineVar:
                        case OperandType.InlineSwitch:
                        case OperandType.ShortInlineR: {
                            operand = *(uint*)&pBuffer[instStart];
                            //instStart += 4;
                            break;
                        }
                        case OperandType.ShortInlineBrTarget:
                        case OperandType.ShortInlineI:
                        case OperandType.ShortInlineVar: {
                            operand = pBuffer[instStart];
                            break;
                        }
                        case OperandType.InlineR:
                        case OperandType.InlineI8: {
                            operand = *(ulong*)&pBuffer[instStart];
                            //instStart += 8;
                            break;
                        }
                    }
                    callback((ILOpCode)ilCode, operand);
                }
                if (!makeVirtualExit) return;
                if (!lastOpCode.IsBranch() & lastOpCode.ToOpCode().FlowControl != FlowControl.Return & lastOpCode!=ILOpCode.Switch) {
                    var currentBlock = m_BasicBlocks[iv];
                    var nextIV = new Interval(currentBlock.Interval.Right, currentBlock.Interval.Right);
                    currentBlock.TrueExit =  new MethodBasicBlock[] { m_BasicBlocks[nextIV] };
                    callback(ILOpCode.Br, 0); // virtual branch
                }
            }
        }
    }
    public struct Interval {
        public uint Left { get; set; }
        public uint Right { get; set; }
        public class IntervalTreeComparer : IComparer<Interval> {
            public int Compare(Interval x, Interval y) {
                if (x.Left == x.Right) { var t = x; x = y; y = t; }
                var xl = x.Left;
                var xr = x.Right;
                var yv = y.Left;
                return (xl > yv ? 1 : (xr > yv ? 0 : -1));
            }
        }
        public Interval(uint left, uint right) {
            Left = left;
            Right = right;
        }
        public override string ToString() {
            return $"[{Left},{Right})";
        }
    }
    public class MethodBasicBlock {
        public MethodBasicBlock[] TrueExit { get; set; }
        public MethodBasicBlock FalseExit { get; set; }
        protected MethodInstanceInfo m_MethodInfo;
        protected Interval m_Interval;
        protected List<MethodBasicBlock> m_Predecessor = new List<MethodBasicBlock>();
        public int StackDepthDelta { get; set; }
        public uint StartIndex => m_Interval.Left;
        public uint EndIndex => m_Interval.Right;
        public Interval Interval => m_Interval;
        public MethodInstanceInfo Method => m_MethodInfo;

        public int ExitStackDepth { get; set; } = int.MinValue;

        public LLVMCompValue[] PreStack { get => m_PreStack; set => m_PreStack = value; }
        public List<MethodBasicBlock> Predecessor => m_Predecessor;

        protected LLVMCompValue[] m_PreStack;
        public MethodBasicBlock(MethodInstanceInfo method, uint startIndex, uint endIndex) {
            m_MethodInfo = method;
            m_Interval = new Interval(startIndex, endIndex);
        }
        public MethodBasicBlock Split(uint pos) {
            if (m_Interval.Left == pos) return null;
            var newBlock = new MethodBasicBlock(m_MethodInfo, pos, m_Interval.Right);
            m_Interval.Right = pos;
            newBlock.TrueExit = TrueExit;
            newBlock.FalseExit = FalseExit;
            FalseExit = newBlock;
            return newBlock;
        }
        public static MethodBasicBlock CutBasicBlock(uint cutIndex, SortedList<Interval, MethodBasicBlock> basicBlocks) {
            var falseExitCutPoint = new Interval(cutIndex, cutIndex);
            if (basicBlocks.TryGetValue(falseExitCutPoint, out var block0)) {
                var block1 = block0.Split(cutIndex);
                if (block1 != null) {
                    basicBlocks.Remove(falseExitCutPoint);
                    basicBlocks.Add(block0.Interval, block0);
                    basicBlocks.Add(block1.Interval, block1);
                }
                return basicBlocks[falseExitCutPoint];
            } else {
                return null;
            }
        }
        /*public static void CutTrueBlock(uint targetBranchIndex, SortedList<Interval, MethodBasicBlock> basicBlocks) {
            var targetBranchTargetPoint = new Interval(targetBranchIndex, targetBranchIndex);
            var block2 = basicBlocks[targetBranchTargetPoint];
            var block3 = block2.Split(targetBranchIndex);

            if (block3 != null) {
                basicBlocks.Remove(targetBranchTargetPoint);
                basicBlocks.Add(block2.Interval, block2);
                basicBlocks.Add(block3.Interval, block3);
            }

            var branchInstBlock = m_BasicBlocks[new Interval(i, i)];
            branchInstBlock.TrueExit = new MethodBasicBlock[] { m_BasicBlocks[targetBranchTargetPoint] };
        }*/
        public override string ToString() {
            return $"{m_Interval}->({((TrueExit!=null)? (string.Join(',', TrueExit.Select(e => e.StartIndex).ToArray())) : "")},{FalseExit?.StartIndex})";
        }

    }
    public interface IGenericParameterContext {
        TypeEntry GetMethodGenericByIndex(TypeEnvironment env, uint index);
        TypeEntry GetTypeGenericByIndex(TypeEnvironment env, uint index);
    }
    public class DummyGenericParameterContext : IGenericParameterContext {
        public static DummyGenericParameterContext Dummy = new DummyGenericParameterContext();
        public uint GenericParameterCount => 0;

        public TypeEntry GetMethodGenericByIndex(TypeEnvironment env, uint index) {
            throw new ArgumentOutOfRangeException();
        }

        public TypeEntry GetTypeGenericByIndex(TypeEnvironment env, uint index) {
            throw new ArgumentOutOfRangeException();
        }
    }
    public struct ImmutableTypeEntries {

    }

    public class GenericInstanceTypeEntry : RealTypeEntry {
        public ImmutableArray<TypeEntry> GenericType;

        public GenericInstanceTypeEntry() { }
        public GenericInstanceTypeEntry(TypeDefinition def, ImmutableArray<TypeEntry> genericParams)
            : base(def) {
            GenericType = genericParams;
        }
        public override string ToString() {
            var fullName = TypeEnvironment.GetTypeDefinitionFullName(TypeDef);
            if (GenericType.Length != 0) {
                var genericList = new StringBuilder();
                genericList.AppendJoin(',', GenericType);
                return $"{fullName}<{genericList}>";
            }
            return fullName;
        }
        public override bool Equals(TypeEntry other) {
            if ((other is GenericInstanceTypeEntry) && TypeDef.Equals(((RealTypeEntry)other).TypeDef)) {
                var other_ = (GenericInstanceTypeEntry)other;
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
            return hash ^ (int)MetadataHelper.MakeHash(ref TypeDef);
        }
    }
    public class PointerTypeEntry : TypeEntry {
        public TypeEntry ElementEntry;
        public PointerTypeEntry(TypeEntry elementEntry) {
            ElementEntry = elementEntry;
        }

        public override bool Equals(TypeEntry other) {
            return (other is PointerTypeEntry) && ElementEntry.Equals(((PointerTypeEntry)other).ElementEntry);
        }

        public override int GetHashCode() {
            return ElementEntry.GetHashCode() ^ 0x50000000;
        }
        public override string ToString() {
            return $"{ElementEntry}*";
        }
    }
    public class RealTypeEntry : TypeEntry {
        public TypeDefinition TypeDef;
        public RealTypeEntry() { }
        public RealTypeEntry(TypeDefinition def) {
            TypeDef = def;
        }

        public override bool Equals(TypeEntry other) {
            return (other is RealTypeEntry) && TypeDef.Equals(((RealTypeEntry)other).TypeDef);
        }

        public override string ToString() {
            var fullName = TypeEnvironment.GetTypeDefinitionFullName(TypeDef);
            return fullName;
        }
        public override int GetHashCode() {
            var hash = (int)0x02000000;
            return hash ^ (int)MetadataHelper.MakeHash(ref TypeDef);
        }
    }
    public class GenericParamTypeEntry : TypeEntry {
        public uint GenericIndex;
        protected static Dictionary<uint, GenericParamTypeEntry> m_Cache = new Dictionary<uint, GenericParamTypeEntry>();
        public GenericParamTypeEntry() { }
        public GenericParamTypeEntry(uint index) {
            GenericIndex = index;
        }

        public override bool Equals(TypeEntry other) {
            return (other is GenericParamTypeEntry) && (((GenericParamTypeEntry)other).GenericIndex == GenericIndex);
        }
        public override int GetHashCode() {
            var hash = (int)0x01000000;
            return hash ^ (int)GenericIndex;
        }
        public override string ToString() {
            return $"({GenericIndex})";
        }
    }
    public abstract class TypeEntry : IEquatable<TypeEntry> {
        public class TypeEntryFactory {
            protected Hashtable m_Cache = new Hashtable();
            protected GenericInstanceTypeEntry m_GenericInstnaceKey = new GenericInstanceTypeEntry();
            protected RealTypeEntry m_RealTypeKey = new RealTypeEntry();
            protected GenericParamTypeEntry m_GPKey = new GenericParamTypeEntry();
            protected MethodEntry m_MethodEntryKey = new MethodEntry();
            protected PointerTypeEntry m_PointerEntryKey = new PointerTypeEntry(null);
            public TypeEntry CreateTypeEntry(TypeDefinition def, ImmutableArray<TypeEntry> genericParams) {
                m_GenericInstnaceKey.TypeDef = def;
                m_GenericInstnaceKey.GenericType = genericParams;
                if (m_Cache.ContainsKey(m_GenericInstnaceKey)) return (TypeEntry)m_Cache[m_GenericInstnaceKey];
                var newTypeEntry = new GenericInstanceTypeEntry(def, genericParams);
                m_Cache.Add(newTypeEntry, newTypeEntry);
                return newTypeEntry;
            }
            public TypeEntry CreateTypeEntry(TypeDefinition def) {
                m_RealTypeKey.TypeDef = def;
                if (m_Cache.ContainsKey(m_RealTypeKey)) return (TypeEntry)m_Cache[m_RealTypeKey];
                var newTypeEntry = new RealTypeEntry(def);
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
                var newTypeEntry = new GenericParamTypeEntry(index);
                m_Cache.Add(newTypeEntry, newTypeEntry);
                return newTypeEntry;
            }
            public MethodEntry CreateMethodEntry(TypeEntry type, MethodDefinition def, ImmutableArray<TypeEntry> genericParams) {
                m_MethodEntryKey.GenericType = genericParams;
                m_MethodEntryKey.MethodDef = def;
                m_MethodEntryKey.TypeEntry = type;
                if (m_Cache.ContainsKey(m_MethodEntryKey)) return (MethodEntry)m_Cache[m_MethodEntryKey];
                var newMethodEntry = new MethodEntry(type, def, genericParams);
                m_Cache.Add(newMethodEntry, newMethodEntry);
                return newMethodEntry;
            }
        }
        public abstract bool Equals(TypeEntry other);
        public abstract override int GetHashCode();
        public override bool Equals(object obj) {
            if (obj is TypeEntry) return Equals((TypeEntry)obj);
            return base.Equals(obj);
        }
    }
    public class MethodEntry : IEquatable<MethodEntry> {
        public TypeEntry TypeEntry;
        public MethodDefinition MethodDef;
        public ImmutableArray<TypeEntry> GenericType;

        public string Name {
            get {
                var reader = MetadataHelper.GetMetadataReader(ref MethodDef);
                return reader.GetString(MethodDef.Name);
            }
        }
        public MethodEntry() { }
        public MethodEntry(TypeEntry type, MethodDefinition def, ImmutableArray<TypeEntry> genericParams) {
            TypeEntry = type;
            MethodDef = def;
            GenericType = genericParams;
        }
        public MethodEntry(TypeEntry type, MethodDefinition def)
            : this(type, def, ImmutableArray<TypeEntry>.Empty) { }

        public override int GetHashCode() {
            var hash = (int)TypeEntry.GetHashCode() ^ 0x10000000;
            foreach (var i in GenericType) hash ^= i.GetHashCode();
            return hash ^ (int)MetadataHelper.ExtractToken(ref MethodDef);
        }

        public bool Equals(MethodEntry other) {
            if (TypeEntry.Equals((other).TypeEntry) && MethodDef.Equals(other.MethodDef)) {
                if (GenericType.Length != other.GenericType.Length) return false;
                for (int i = 0, length = GenericType.Length; i < length; i++)
                    if (!GenericType[i].Equals(other.GenericType[i]))
                        return false;
                return true;
            }
            return false;
        }
        public override bool Equals(object obj) {
            if (obj is MethodEntry) return Equals((MethodEntry)obj);
            return base.Equals(obj);
        }
        public override string ToString() {
            var reader = MetadataHelper.GetMetadataReader(ref MethodDef);
            if (GenericType.Length != 0) {
                var builder = new StringBuilder();
                builder.AppendJoin(",", GenericType);
                return $"{TypeEntry}.{reader.GetString(MethodDef.Name)}<{builder}>";
            }
            return $"{TypeEntry}.{reader.GetString(MethodDef.Name)}";
        }
    }
    public class FieldInfo {
        public FieldAttributes Flags;
        public string Name;
        public TypeEntry Type;
        public TypeEntry DeclType;
        public FieldDefinition Definition;
        public uint LayoutOffset;
        public uint FieldIndex;

        public FieldInfo(TypeEntry declType, TypeEntry fieldType, FieldDefinition fieldDef, uint index) {
            //if (declType.ToString().Contains("Delegate")) Debugger.Break();
            var reader = MetadataHelper.GetMetadataReader(ref fieldDef);
            Name = reader.GetString(fieldDef.Name);
            Definition = fieldDef;
            DeclType = declType;
            Type = fieldType;
            Flags = fieldDef.Attributes;
            FieldIndex = index;
        }
        public override string ToString() {
            return $"{Type} {DeclType}.{Name}";
        }
    }
    public class TypeInfo : IGenericParameterContext {
        protected TypeEnvironment m_TypeEnv;
        public RealTypeEntry Entry;
        protected IGenericParameterContext m_ParentGenericContext;
        protected SortedList<string, FieldInfo> m_Fields = new SortedList<string, FieldInfo>();
        protected SortedList<string, FieldInfo> m_StaticFields = new SortedList<string, FieldInfo>();
        protected Dictionary<MethodEntry, MethodInstanceInfo> m_Methods = new Dictionary<MethodEntry, MethodInstanceInfo>();
        protected MetadataReader m_Reader;
        protected TypeInfo[] m_Interfaces = null;
        protected TypeInfo m_DeclType = null;

        protected TypeInfo m_BaseType;
        protected bool m_IsValueType;
        protected string m_FullName;

        public TypeInfo BaseType => m_BaseType;
        public string FullName => m_FullName;
        public bool IsValueType => m_IsValueType;
        public SortedList<string, FieldInfo> Fields => m_Fields;
        public SortedList<string, FieldInfo> StaticFields => m_StaticFields;
        public TypeAttributes Attribute => ((RealTypeEntry)Entry).TypeDef.Attributes;
        public TypeDefinition Definition => ((RealTypeEntry)Entry).TypeDef;
        public TypeEnvironment TypeEnv => m_TypeEnv;
        public Dictionary<MethodEntry, MethodInstanceInfo> Methods => m_Methods;
        public MetadataReader Reader => m_Reader;
        public TypeInfo[] Interfaces => m_Interfaces;

        public void RegisterFields() {
            var typeDef = ((RealTypeEntry)Entry).TypeDef;
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
        public void RegisterMethodInstance(MethodInstanceInfo methodInstance) => m_Methods.Add(methodInstance.Entry, methodInstance);

        public TypeInfo(TypeEnvironment env, RealTypeEntry entry, TypeInfo baseType, IGenericParameterContext genericContext, TypeInfo declType)
            : this(env, entry, baseType, genericContext) {
            m_DeclType = declType;
        }
        public TypeInfo(TypeEnvironment env, RealTypeEntry entry, TypeInfo baseType, IGenericParameterContext genericContext) {
            m_TypeEnv = env;
            Entry = entry;
            m_ParentGenericContext = genericContext;
            m_BaseType = baseType;
            m_FullName = entry.ToString();
            m_Reader = MetadataHelper.GetMetadataReader(ref ((RealTypeEntry)Entry).TypeDef);



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
            m_Interfaces = new TypeInfo[interfaceImpl.Count];
            var index = 0;
            foreach (var i in interfaceImpl) {
                var impl = m_Reader.GetInterfaceImplementation(i);
                var typeEntry = m_TypeEnv.ResolveTypeByHandle(impl.Interface, m_Reader, this);
                m_Interfaces[index++] = m_TypeEnv.ActivateType(typeEntry);
            }
        }
        public MethodInstanceInfo MatchSignature(MethodInstanceInfo pattern) {
            foreach (var i in m_Methods.Values) {
                if (i.EqualsSignature(pattern)) return i;
            }
            return m_BaseType?.MatchSignature(pattern);
        }

        //public uint GenericParameterCount => m_ParentGenericContext.GenericParameterCount+ ((Entry is GenericInstanceTypeEntry)?((uint)((GenericInstanceTypeEntry)Entry).GenericType.Length):0);

        public TypeEntry GetMethodGenericByIndex(TypeEnvironment env, uint index) {
            throw new NotImplementedException();
        }

        public TypeEntry GetTypeGenericByIndex(TypeEnvironment env, uint index) {
            if (Entry is GenericInstanceTypeEntry) {
                var entry_ = (GenericInstanceTypeEntry)Entry;
                if (entry_.GenericType.Length <= index) throw new Exception("");
                return entry_.GenericType[(int)(index)];
            } else {
                return env.TypeEntryFactory.CreateTypeEntry(index);
            }

        }


        public MethodInstanceInfo FindMethodByName(string name) {
            foreach (var i in m_Methods) {
                if (i.Value.Entry.Name == name) {
                    return i.Value;
                }
            }
            return null;
        }
    }
    public class App {
        public unsafe static void LoadObj() {
            var obj = MemoryMappedFile.CreateFromFile("./loader_elf.obj");
            var view = obj.CreateViewAccessor();
            byte* ptr = null;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            var elfModule = new ELFReader(ptr, (int)view.SafeMemoryMappedViewHandle.ByteLength);
            elfModule.ReadFile();

            view.Dispose();
            obj.Dispose();
        }
        class Vis0: CSharpSyntaxRewriter {
            public override Microsoft.CodeAnalysis.SyntaxNode VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node) {
                return base.VisitLocalDeclarationStatement(node);
            }
            public override Microsoft.CodeAnalysis.SyntaxNode VisitLineSpanDirectiveTrivia(LineSpanDirectiveTriviaSyntax node) {
                return base.VisitLineSpanDirectiveTrivia(node);
            }
            public override Microsoft.CodeAnalysis.SyntaxNode VisitLineDirectiveTrivia(LineDirectiveTriviaSyntax node) {
                return base.VisitLineDirectiveTrivia(node);
            }
            public override Microsoft.CodeAnalysis.SyntaxNode VisitXmlElement(XmlElementSyntax node) {
                return base.VisitXmlElement(node);
            }
            public override Microsoft.CodeAnalysis.SyntaxNode VisitBlock(BlockSyntax node) {
                return base.VisitBlock(node);
            }
        }
        static SyntaxTrivia EmptyTrivia(SyntaxTrivia t1, SyntaxTrivia t2) {
            if(t1.IsKind(SyntaxKind.MultiLineCommentTrivia) || t1.IsKind(SyntaxKind.SingleLineCommentTrivia)) {
                return default;
            } else {
                return t1;
            }
        }
        public unsafe static void Main() {
            

            CSharpProject.CollectSources();

            //LoadObj();
            Environment.Exit(0);

            var envPath = Environment.GetEnvironmentVariable("Path");
            envPath += ";./llvm/";
            Environment.SetEnvironmentVariable("Path", envPath);

            var stream = new FileStream("./EFILoader.dll", FileMode.Open,FileAccess.Read);
            var auxLib = new FileStream("./FrameworkLib.dll", FileMode.Open, FileAccess.Read);
            var typeEnv = new TypeEnvironment(stream, new string[] {
                "System.Runtime.CompilerServices::RuntimeHelpers",
                "System.Runtime.CompilerServices::UnsafeAsm",
                "System::String",
                "System::IntPtr",
                "System::Delegate",
                "System.Runtime.InteropServices::DllImportAttribute",
                "System::Object",
                "System::MulticastDelegate",
                "System.Runtime.CompilerServices::RuntimeExport",
                "System::Enum",
                "System::RuntimeArgumentHandle",
                "System::RuntimeVaList"
            });
            typeEnv.AddReference(auxLib);
            typeEnv.LoadTypes();
            typeEnv.CollectTypes();

            Console.WriteLine("###### Collected Types ######");
            foreach (var i in typeEnv.ActiveTypes.Keys) Console.WriteLine(i.ToString());

            Console.WriteLine("###### Collected Methods ######");
            foreach (var i in typeEnv.ActiveMethods.Keys) Console.WriteLine(i.ToString());

            var compiler = new LLVMCompiler(typeEnv, "payload");

            compiler.BuildAssembly();

            var irCode = compiler.PrintIRCode();
            Console.WriteLine("=========Start of IR Section==========");
            Console.WriteLine(irCode);
            Console.WriteLine("=========End of IR Section==========");

            compiler.PostProcess();

            File.WriteAllText("payload.ll", compiler.PrintIRCode());
            compiler.Module.WriteBitcodeToFile("./payload.bc");
            compiler.GenerateBinary("./payload.obj");

            nuint len = 0;
            var linker = lto_codegen_create();
            lto_codegen_set_cpu(linker, (sbyte*)Marshal.StringToHGlobalAnsi("znver3"));
            
            var lnkModule = lto_module_create((sbyte*)Marshal.StringToHGlobalAnsi("./payload.bc"));
            
            
            lto_codegen_set_debug_model(linker, lto_debug_model.LTO_DEBUG_MODEL_DWARF);
            lto_codegen_set_pic_model(linker, lto_codegen_model.LTO_CODEGEN_PIC_MODEL_DYNAMIC);
            lto_module_set_target_triple(lnkModule, (sbyte*)Marshal.StringToHGlobalAnsi("x86_64-pc-none-eabi"));
            lto_codegen_add_module(linker, lnkModule);
            lto_codegen_add_must_preserve_symbol(linker, (sbyte*)Marshal.StringToHGlobalAnsi("EFILoader::App.EfiMain"));
            lto_codegen_add_must_preserve_symbol(linker, (sbyte*)Marshal.StringToHGlobalAnsi("__chkstk"));
            //lto_codegen_add_must_preserve_symbol(linker, (sbyte*)Marshal.StringToHGlobalAnsi("WriteFile"));
            //lto_codegen_optimize(linker);
            var compileResult = lto_codegen_compile(linker, &len);

            
            
            
            //
            var data = new byte[len];
            Marshal.Copy((IntPtr)compileResult, data, 0, (int)len);

            var memory = new ReadOnlyMemory<byte>(data);
            //reader.ParseFile();
            

            File.WriteAllBytes("./loader_elf.obj", data);



            Console.WriteLine("Complete");
            Console.ReadLine();
        }
        [DllImport("LTO", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public unsafe static extern void lto_codegen_set_debug_model(LLVMOpaqueLTOCodeGenerator* cg, lto_debug_model model);
        [DllImport("LTO", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public unsafe static extern void lto_codegen_set_pic_model(LLVMOpaqueLTOCodeGenerator* cg, lto_codegen_model model);
        [DllImport("LTO", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public unsafe static extern void lto_module_set_target_triple(LLVMOpaqueLTOModule* cg, sbyte* symbol);
        [DllImport("LTO", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public unsafe static extern void lto_codegen_set_cpu(LLVMOpaqueLTOCodeGenerator* cg, sbyte* symbol);
        [DllImport("LTO", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public unsafe static extern void lto_codegen_add_must_preserve_symbol(LLVMOpaqueLTOCodeGenerator* cg, sbyte* symbol);
        [DllImport("LTO", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public unsafe static extern sbyte* lto_module_get_symbol_name(LLVMOpaqueLTOModule* mod, uint index);
        [DllImport("LTO", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public unsafe static extern byte lto_codegen_optimize(LLVMOpaqueLTOCodeGenerator* cg);
        [DllImport("LTO", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public unsafe static extern byte lto_codegen_write_merged_modules(LLVMOpaqueLTOCodeGenerator* cg, sbyte* path);
        [DllImport("LTO", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public unsafe static extern byte lto_codegen_compile_to_file(LLVMOpaqueLTOCodeGenerator* cg, sbyte** name);

        [DllImport("LTO", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public unsafe static extern void* lto_codegen_compile(LLVMOpaqueLTOCodeGenerator* cg, nuint* length);

        [DllImport("LTO", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public unsafe static extern LLVMOpaqueLTOCodeGenerator* lto_codegen_create();
        [DllImport("LTO", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public unsafe static extern LLVMOpaqueLTOModule* lto_module_create(sbyte* path);
        [DllImport("LTO", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public unsafe static extern byte lto_codegen_add_module(LLVMOpaqueLTOCodeGenerator* cg, LLVMOpaqueLTOModule* mod);

    }



}
