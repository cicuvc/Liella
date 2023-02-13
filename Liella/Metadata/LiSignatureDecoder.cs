using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Metadata {
    public abstract class LiSignatureDecoder : ISignatureTypeProvider<TypeEntry, IGenericParameterContext>, ICustomAttributeTypeProvider<TypeEntry> {
        public abstract TypeEntry GetArrayType(TypeEntry elementType, ArrayShape shape);
        public abstract TypeEntry GetByReferenceType(TypeEntry elementType);
        public abstract TypeEntry GetFunctionPointerType(MethodSignature<TypeEntry> signature);
        public abstract TypeEntry GetGenericInstantiation(TypeEntry genericType, ImmutableArray<TypeEntry> typeArguments);
        public abstract TypeEntry GetGenericMethodParameter(IGenericParameterContext genericContext, int index);
        public abstract TypeEntry GetGenericTypeParameter(IGenericParameterContext genericContext, int index);
        public abstract TypeEntry GetModifiedType(TypeEntry modifier, TypeEntry unmodifiedType, bool isRequired);
        public abstract TypeEntry GetPinnedType(TypeEntry elementType);
        public abstract TypeEntry GetPointerType(TypeEntry elementType);
        public abstract TypeEntry GetPrimitiveType(PrimitiveTypeCode typeCode);
        public abstract TypeEntry GetSystemType();
        public abstract TypeEntry GetSZArrayType(TypeEntry elementType);
        public abstract TypeEntry GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind);
        public abstract TypeEntry GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind);
        public abstract TypeEntry GetTypeFromSerializedName(string name);
        public abstract TypeEntry GetTypeFromSpecification(MetadataReader reader, IGenericParameterContext genericContext, TypeSpecificationHandle handle, byte rawTypeKind);
        public abstract PrimitiveTypeCode GetUnderlyingEnumType(TypeEntry type);
        public abstract bool IsSystemType(TypeEntry type);
    }
}
