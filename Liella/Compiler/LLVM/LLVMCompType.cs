using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM {
    public struct LLVMCompType {
        private LLVMTypeTag m_TypeTag;
        private LLVMTypeRef m_LLVMType;
        public LLVMTypeTag TypeTag => m_TypeTag;
        public LLVMTypeRef LLVMType => m_LLVMType;
        public static LLVMCompType Int1 { get;  } = CreateType(LLVMTypeTag.UnsignedInt, LLVMTypeRef.Int1);
        public static LLVMCompType Integer8 { get; } = CreateType(LLVMTypeTag.SignedInt, LLVMTypeRef.Int8);
        public static LLVMCompType Integer16 { get; } = CreateType(LLVMTypeTag.SignedInt, LLVMTypeRef.Int16);
        public static LLVMCompType Integer32 { get; } = CreateType(LLVMTypeTag.SignedInt, LLVMTypeRef.Int32);
        public static LLVMCompType Integer64 { get; } = CreateType(LLVMTypeTag.SignedInt, LLVMTypeRef.Int64);
        public static LLVMCompType UInteger8 { get; } = CreateType(LLVMTypeTag.UnsignedInt, LLVMTypeRef.Int8);
        public static LLVMCompType UInteger16 { get; } = CreateType(LLVMTypeTag.UnsignedInt, LLVMTypeRef.Int16);
        public static LLVMCompType UInteger32 { get; } = CreateType(LLVMTypeTag.UnsignedInt, LLVMTypeRef.Int32);
        public static LLVMCompType UInteger64 { get; } = CreateType(LLVMTypeTag.UnsignedInt, LLVMTypeRef.Int64);
        public static LLVMCompType IntegerPtr { get; } = CreateType(LLVMTypeTag.TypePointer, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0));
        public static LLVMCompType FloatPoint64 { get; } = CreateType(LLVMTypeTag.NumberReal | LLVMTypeTag.FP64, LLVMTypeRef.Double);
        public static LLVMCompType FloatPoint32 { get; } = CreateType(LLVMTypeTag.NumberReal, LLVMTypeRef.Float);
        public static LLVMCompType CreateType(LLVMTypeTag tag, LLVMTypeRef type) {
            LLVMCompType result = default;
            result.m_LLVMType = type;
            result.m_TypeTag = tag;
            return result;
        }
        public LLVMCompType ToPointerType() {
            return CreateType(LLVMTypeTag.TypePointer, LLVMTypeRef.CreatePointer(LLVMType, 0));
        }
        public LLVMCompType ToDerefType() {
            return CreateType(TypeTag, LLVMType.ElementType);
        }

        public override string ToString() {
            return $"[{LLVMType}]({TypeTag})";
        }
    }
}
