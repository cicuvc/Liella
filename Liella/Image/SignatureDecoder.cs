using Liella.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Image {
    public class ImageSignatureDecoder : LiSignatureDecoder {
        protected ImageCompilationUnitSet m_TypeEnv;
        public ImageSignatureDecoder(ImageCompilationUnitSet env) {
            m_TypeEnv = env;
        }
        public override TypeEntry GetArrayType(TypeEntry elementType, ArrayShape shape) {
            throw new NotImplementedException();
        }

        public override TypeEntry GetByReferenceType(TypeEntry elementType) {
            return m_TypeEnv.TypeEntryFactory.CreateTypeEntry(elementType);
        }

        public override TypeEntry GetFunctionPointerType(MethodSignature<TypeEntry> signature) {
            var primitiveTypeDef = m_TypeEnv.ResolveTypeByPrototypeName($"System::IntPtr");
            return m_TypeEnv.TypeEntryFactory.CreateTypeEntry(primitiveTypeDef);
        }

        public override TypeEntry GetGenericInstantiation(TypeEntry genericType, ImmutableArray<TypeEntry> typeArguments) {
            return m_TypeEnv.TypeEntryFactory.CreateTypeEntry(((ImageRealTypeEntry)genericType).TypeDef, typeArguments);
        }

        public override TypeEntry GetGenericMethodParameter(IGenericParameterContext genericContext, int index) {
            if (genericContext == null) return m_TypeEnv.TypeEntryFactory.CreateTypeEntry((uint)index);
            return genericContext.GetMethodGenericByIndex(m_TypeEnv, (uint)index);
        }

        public override TypeEntry GetGenericTypeParameter(IGenericParameterContext genericContext, int index) {
            if (genericContext == null) return m_TypeEnv.TypeEntryFactory.CreateTypeEntry((uint)index);
            return genericContext.GetTypeGenericByIndex(m_TypeEnv, (uint)index);
        }

        public override TypeEntry GetModifiedType(TypeEntry modifier, TypeEntry unmodifiedType, bool isRequired) {
            return unmodifiedType;
            //throw new NotImplementedException();
        }

        public override TypeEntry GetPinnedType(TypeEntry elementType) {
            throw new NotImplementedException();
        }

        public override TypeEntry GetPointerType(TypeEntry elementType) {
            return m_TypeEnv.TypeEntryFactory.CreateTypeEntry(elementType);
        }

        public override TypeEntry GetPrimitiveType(PrimitiveTypeCode typeCode) {
            var primitiveTypeDef = m_TypeEnv.ResolveTypeByPrototypeName($"System::{typeCode}");
            return m_TypeEnv.TypeEntryFactory.CreateTypeEntry(primitiveTypeDef);
        }

        public override TypeEntry GetSZArrayType(TypeEntry elementType) {
            throw new NotImplementedException();
        }

        public override TypeEntry GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) {
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

        public override TypeEntry GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) {
            var typeRef = reader.GetTypeReference(handle);
            var fullName = GetTypeReferenceFullName(reader, typeRef);
            var typeDef = m_TypeEnv.ResolveTypeByPrototypeName(fullName);
            return m_TypeEnv.TypeEntryFactory.CreateTypeEntry(typeDef);
        }

        public override TypeEntry GetTypeFromSpecification(MetadataReader reader, IGenericParameterContext genericContext, TypeSpecificationHandle handle, byte rawTypeKind) {
            throw new NotImplementedException();
        }

        public override TypeEntry GetSystemType() {
            throw new NotImplementedException();
        }

        public override bool IsSystemType(TypeEntry type) {
            throw new NotImplementedException();
        }

        public override TypeEntry GetTypeFromSerializedName(string name) {
            throw new NotImplementedException();
        }

        public override PrimitiveTypeCode GetUnderlyingEnumType(TypeEntry type) {
            throw new NotImplementedException();
        }
    }
}
