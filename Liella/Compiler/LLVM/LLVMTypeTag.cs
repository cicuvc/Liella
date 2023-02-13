using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM {
    public enum LLVMTypeTag {
        Unsigned = 0,
        Signed = 1,
        Integer = 2,
        Real = 4,
        Pointer = 8,
        Struct = 16,


        UnsignedInt = 2,
        SignedInt = 3,
        Interface = 32,
        StackAlloc = 64,
        ConstObj = 128,
        Class = 256,
        FP64 = 512
    }
}
