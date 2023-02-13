using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM {
    public struct LLVMInterfaceHeader {
        public static ulong GetHeaderValue(ushort interfaceOffset, ushort interfaceLength, uint interfaceHash) {
            return (((ulong)interfaceHash) << 32) | (((ulong)interfaceLength) << 16) | interfaceOffset;
        }
    }

}
