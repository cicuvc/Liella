using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM {
    public enum LLVMTypeTag {
        NumberUnsigned = 0,
        NumberSigned = 1,
        NumberInteger = 2,
        NumberReal = 4,
        TypePointer = 8,
        Struct = 16,


        UnsignedInt = NumberInteger,
        SignedInt = NumberInteger | NumberSigned,
        Interface = 32,
        StackAlloc = 64,
        ConstObj = 128,
        Class = 256,
        FP64 = 512
    }
}
