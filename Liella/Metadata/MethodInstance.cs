using Liella.MSIL;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Metadata {
    public abstract class MethodInstance : IGenericParameterContext {
        public abstract MethodEntry Entry { get; }
        public abstract MethodSignature<TypeEntry> Signature { get; }
        public abstract CompilationUnitSet TypeEnv { get; }
        public abstract MethodAttributes Attributes { get; }
        public abstract MethodImplAttributes ImplAttributes { get; }
        public abstract LiTypeInfo DeclType { get; }
        public abstract SortedList<Interval, MethodBasicBlock> BasicBlocks { get; }
        public abstract ImmutableArray<TypeEntry> LocalVaribleTypes { get; }
        public abstract bool IsDummy { get; }
        public abstract ImmutableDictionary<TypeEntry, CustomAttribute> CustomAttributes { get; }
        public abstract TypeEntry ResolveTypeToken(EntityHandle handle);
        public abstract MethodEntry ResolveMethodToken(EntityHandle methodToken, out TypeEntry typeEntry, out MethodSignature<TypeEntry> callSiteSig);
        public abstract string ResolveStringToken(UserStringHandle stringHandle);
        public abstract StandaloneSignature ResolveSignatureToken(EntityHandle sigToken);
        public abstract LiFieldInfo ResolveFieldToken(EntityHandle fieldToken, out TypeEntry declType);
        public abstract LiFieldInfo ResolveStaticFieldToken(EntityHandle fieldToken, out TypeEntry declType);
        public abstract void MakeBasicBlocks();
        public bool EqualsSignature(MethodInstance oth) {
            var othSig = oth.Signature;
            return EqualsSignature(othSig);
        }
        public bool EqualsSignature(MethodSignature<TypeEntry> othSig) {
            var cSig = this.Signature;
            if (cSig.ReturnType != othSig.ReturnType) return false;
            if (cSig.ParameterTypes.Length != othSig.ParameterTypes.Length) return false;
            for (var i = 0; i < cSig.ParameterTypes.Length; i++) {
                if (!cSig.ParameterTypes[i].Equals(othSig.ParameterTypes[i])) return false;
            }
            return true;
        }

        public abstract TypeEntry GetMethodGenericByIndex(CompilationUnitSet env, uint index);

        public abstract TypeEntry GetTypeGenericByIndex(CompilationUnitSet env, uint index);

        public abstract void ForEachIL(Interval iv, Action<ILOpCode, ulong> callback, bool makeVirtualExit);
    }

}
