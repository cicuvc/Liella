using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM {
    public struct LLVMCompType {
        public LLVMTypeTag TypeTag;
        public LLVMTypeRef LLVMType;
        public static LLVMCompType Int1 = CreateType(LLVMTypeTag.UnsignedInt, LLVMTypeRef.Int1);
        public static LLVMCompType Int8 = CreateType(LLVMTypeTag.SignedInt, LLVMTypeRef.Int8);
        public static LLVMCompType Int16 = CreateType(LLVMTypeTag.SignedInt, LLVMTypeRef.Int16);
        public static LLVMCompType Int32 = CreateType(LLVMTypeTag.SignedInt, LLVMTypeRef.Int32);
        public static LLVMCompType Int64 = CreateType(LLVMTypeTag.SignedInt, LLVMTypeRef.Int64);
        public static LLVMCompType UInt8 = CreateType(LLVMTypeTag.UnsignedInt, LLVMTypeRef.Int8);
        public static LLVMCompType UInt16 = CreateType(LLVMTypeTag.UnsignedInt, LLVMTypeRef.Int16);
        public static LLVMCompType UInt32 = CreateType(LLVMTypeTag.UnsignedInt, LLVMTypeRef.Int32);
        public static LLVMCompType UInt64 = CreateType(LLVMTypeTag.UnsignedInt, LLVMTypeRef.Int64);
        public static LLVMCompType IntPtr = CreateType(LLVMTypeTag.Pointer, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0));
        public static LLVMCompType Float64 = CreateType(LLVMTypeTag.Real | LLVMTypeTag.FP64, LLVMTypeRef.Double);
        public static LLVMCompType Float32 = CreateType(LLVMTypeTag.Real, LLVMTypeRef.Float);
        public static LLVMCompType CreateType(LLVMTypeTag tag, LLVMTypeRef type) {
            LLVMCompType result = default;
            result.LLVMType = type;
            result.TypeTag = tag;
            return result;
        }
        public LLVMCompType ToPointerType() {
            return CreateType(LLVMTypeTag.Pointer, LLVMTypeRef.CreatePointer(LLVMType, 0));
        }
        public LLVMCompType ToDerefType() {
            return CreateType(TypeTag, LLVMType.ElementType);
        }
        public LLVMCompType WithTag(LLVMTypeTag tag) {
            this.TypeTag |= tag;
            return this;
        }
        public override string ToString() {
            return $"[{LLVMType}]({TypeTag})";
        }
    }
}
