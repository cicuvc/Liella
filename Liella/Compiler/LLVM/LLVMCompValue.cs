using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM {
    public struct LLVMCompValue {
        public LLVMValueRef Value { get; set; }
        public LLVMCompType Type { get; set; }
        public static LLVMCompValue CreateValue(LLVMValueRef value, LLVMCompType type) {
            LLVMCompValue result = default;
            result.Value = value;
            result.Type = type;
            return result;
        }
        public static LLVMCompValue CreateValue(LLVMValueRef value, LLVMTypeTag tag) {
            LLVMCompValue result = default;
            result.Value = value;
            result.Type = LLVMCompType.CreateType(tag, value.TypeOf);
            return result;
        }
        public static LLVMCompValue CreateConstI32(uint value) {
            return CreateValue(LLVMHelpers.CreateConstU32(value), LLVMCompType.Int32);
        }
        public static LLVMCompValue CreateConstI32(int value) {
            return CreateValue(LLVMHelpers.CreateConstU32(value), LLVMCompType.Int32);
        }
        public static LLVMCompValue CreateConstI64(ulong value) {
            return CreateValue(LLVMHelpers.CreateConstU64(value), LLVMCompType.Int64);
        }
        public static LLVMCompValue CreateConstI64(long value) {
            return CreateValue(LLVMHelpers.CreateConstU64(value), LLVMCompType.Int64);
        }
        public unsafe LLVMCompValue TryCastComparable(LLVMBuilderRef builder) {
            switch (Type.LLVMType.Kind) {
                case LLVMTypeKind.LLVMIntegerTypeKind:
                case LLVMTypeKind.LLVMFloatTypeKind:
                case LLVMTypeKind.LLVMDoubleTypeKind: {
                    return this;
                }
                case LLVMTypeKind.LLVMPointerTypeKind: {
                    var ptrType = LLVMCompType.Int8.ToPointerType();
                    return CreateValue(builder.BuildBitCast(Value, ptrType.LLVMType), ptrType);
                }
                case LLVMTypeKind.LLVMStructTypeKind: {
                    throw new NotImplementedException();
                }
            }
            throw new NotImplementedException();
        }
        public unsafe LLVMCompValue TryCastCond(LLVMBuilderRef builder) {
            switch (Type.LLVMType.Kind) {
                case LLVMTypeKind.LLVMPointerTypeKind:
                case LLVMTypeKind.LLVMIntegerTypeKind: {
                    var result = builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, Value, LLVMValueRef.CreateConstNull(Type.LLVMType));
                    return CreateValue(result, LLVMCompType.Int1);
                }
                case LLVMTypeKind.LLVMFloatTypeKind:
                case LLVMTypeKind.LLVMDoubleTypeKind: {
                    var result = builder.BuildFCmp(LLVMRealPredicate.LLVMRealONE, Value, LLVMValueRef.CreateConstNull(Type.LLVMType));
                    return CreateValue(result, LLVMCompType.Int1);
                }
                case LLVMTypeKind.LLVMStructTypeKind: {
                    throw new NotImplementedException();
                }
            }
            throw new NotImplementedException();
        }
        public unsafe LLVMCompValue TryCast(LLVMCompType dstType, LLVMBuilderRef builder, LLVMCompiler compiler = null) {
            var value = Value;
            var tag = Type.TypeTag;
            switch (dstType.LLVMType.Kind) {
                case LLVMTypeKind.LLVMIntegerTypeKind: {
                    if (value.TypeOf.Kind.HasFlag(LLVMTypeKind.LLVMPointerTypeKind)) {
                        value = builder.BuildPtrToInt(value, LLVMCompType.Int64.LLVMType);
                        tag = LLVMTypeTag.SignedInt;
                    }
                    if (value.TypeOf.Kind.HasFlag(LLVMTypeKind.LLVMFloatTypeKind)) {
                        value = dstType.TypeTag.HasFlag(LLVMTypeTag.Signed) ? builder.BuildFPToSI(value, dstType.LLVMType) : builder.BuildFPToUI(value, dstType.LLVMType);
                        tag = dstType.TypeTag;
                    }
                    if (value.TypeOf.Kind.HasFlag(LLVMTypeKind.LLVMIntegerTypeKind)) {
                        if (value.TypeOf.IntWidth == dstType.LLVMType.IntWidth) return CreateValue(value, dstType);
                        if (value.TypeOf.IntWidth < dstType.LLVMType.IntWidth) {
                            return CreateValue(tag.HasFlag(LLVMTypeTag.Signed) ? builder.BuildSExt(value, dstType.LLVMType) : builder.BuildZExt(value, dstType.LLVMType), dstType);
                        } else {
                            return CreateValue(builder.BuildTrunc(value, dstType.LLVMType), dstType);
                        }
                    }
                    throw new InvalidCastException();
                }
                case LLVMTypeKind.LLVMFloatTypeKind: {
                    throw new NotImplementedException();
                }
                case LLVMTypeKind.LLVMDoubleTypeKind: {
                    if (value.TypeOf.Kind.HasFlag(LLVMTypeKind.LLVMDoubleTypeKind)) return this;
                    if (value.TypeOf.Kind.HasFlag(LLVMTypeKind.LLVMFloatTypeKind)) {
                        return CreateValue(builder.BuildFPExt(value, LLVMTypeRef.Double), dstType);
                    }
                    if (value.TypeOf.Kind.HasFlag(LLVMTypeKind.LLVMIntegerTypeKind)) {
                        if (tag.HasFlag(LLVMTypeTag.Signed))
                            return CreateValue(builder.BuildSIToFP(value, LLVMTypeRef.Double), dstType);
                        return CreateValue(builder.BuildUIToFP(value, LLVMTypeRef.Double), dstType);
                    }
                    throw new NotImplementedException();

                }
                case LLVMTypeKind.LLVMPointerTypeKind: {
                    if (value.TypeOf.Kind.HasFlag(LLVMTypeKind.LLVMPointerTypeKind)) {
                        if (Type.TypeTag.HasFlag(LLVMTypeTag.Interface) && dstType.TypeTag.HasFlag(LLVMTypeTag.Class)) {
                            // interface to class
                            var ptrmaskFunc = LLVMHelpers.GetIntrinsicFunction(compiler.Module, "llvm.ptrmask", new LLVMTypeRef[] {
                                Type.LLVMType, LLVMTypeRef.Int64
                            });
                            value = builder.BuildCall2(ptrmaskFunc.TypeOf.ElementType, ptrmaskFunc, new LLVMValueRef[] {
                                value,
                                LLVMHelpers.CreateConstU64(0xFFFFFFFFFFFF)
                            });
                        }
                        if (Type.TypeTag.HasFlag(LLVMTypeTag.Class) && dstType.TypeTag.HasFlag(LLVMTypeTag.Interface)) {
                            // class to interface
                            var runtimeHelpers = compiler.ResolveLLVMType(compiler.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::RuntimeHelpers"]);
                            var castToInterface = compiler.ResolveLLVMMethod(runtimeHelpers.MetadataType.FindMethodByName("CastToInterface").Entry);
                            var interfaceType = compiler.ResolveLLVMTypeFromTypeRef(dstType);
                            value = builder.BuildCall2(castToInterface.FunctionType, castToInterface.Function, new LLVMValueRef[] {
                                builder.BuildBitCast(value,castToInterface.ParamTypes[0].LLVMType),
                                LLVMHelpers.CreateConstU32(interfaceType.TypeHash)
                            });
                        }
                        value = builder.BuildBitCast(value, dstType.LLVMType);
                        return CreateValue(value, dstType);
                    }
                    if (value.TypeOf.Kind.HasFlag(LLVMTypeKind.LLVMIntegerTypeKind)) {
                        value = builder.BuildIntToPtr(value, dstType.LLVMType);
                        return CreateValue(value, dstType);
                    }
                    break;
                }
                case LLVMTypeKind.LLVMStructTypeKind: {
                    if (dstType.LLVMType == Type.LLVMType) return this;
                    throw new NotImplementedException();
                    //break;
                }
            }
            throw new NotImplementedException();
        }
    }

}
