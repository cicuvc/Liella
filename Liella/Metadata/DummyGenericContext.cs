using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Metadata {

    public class DummyGenericParameterContext : IGenericParameterContext {
        public static DummyGenericParameterContext Dummy { get; } = new DummyGenericParameterContext();

        public TypeEntry GetMethodGenericByIndex(CompilationUnitSet env, uint index) {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        public TypeEntry GetTypeGenericByIndex(CompilationUnitSet env, uint index) {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
