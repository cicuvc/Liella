using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM {
    public enum LLVMTypeBuildPass {
        Construct = 1,
        SolveDependencies = 2,
        SetupTypes = 3,
        GenerateVTable = 4
    }
}
