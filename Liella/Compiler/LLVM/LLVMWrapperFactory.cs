using Liella.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM {
    public static class LLVMWrapperFactory {
        public static LLVMTypeInfo CreateLLVMType(LLVMCompiler compiler, LiTypeInfo typeInfo) {
            var typeInfoObj = (LLVMTypeInfo)(typeInfo.Attributes.HasFlag(TypeAttributes.Interface) ? new LLVMInterfaceTypeInfo(compiler, typeInfo) : new LLVMClassTypeInfo(compiler, typeInfo));
            typeInfoObj.TypeHash = compiler.RegisterTypeInfo(typeInfoObj);
            return typeInfoObj;
        }
    }
}
