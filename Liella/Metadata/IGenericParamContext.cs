using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Metadata {
    public interface IGenericParameterContext {
        TypeEntry GetMethodGenericByIndex(CompilationUnitSet env, uint index);
        TypeEntry GetTypeGenericByIndex(CompilationUnitSet env, uint index);
    }
}
