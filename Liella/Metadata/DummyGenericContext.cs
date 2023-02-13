using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Metadata {

    public class DummyGenericParameterContext : IGenericParameterContext {
        public static DummyGenericParameterContext Dummy = new DummyGenericParameterContext();
        public uint GenericParameterCount => 0;

        public TypeEntry GetMethodGenericByIndex(CompilationUnitSet env, uint index) {
            throw new ArgumentOutOfRangeException();
        }

        public TypeEntry GetTypeGenericByIndex(CompilationUnitSet env, uint index) {
            throw new ArgumentOutOfRangeException();
        }
    }
}
