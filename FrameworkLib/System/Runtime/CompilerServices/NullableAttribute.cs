using System;

namespace System.Runtime.CompilerServices {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter, AllowMultiple = false, Inherited = false)]
    internal sealed class NullableAttribute : Attribute {
        public NullableAttribute(byte p0) {
            //NullableFlags = new byte[] { p0 };
        }

        public NullableAttribute(byte[] A_0) {
            //NullableFlags = A_0;
        }
    }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Interface | AttributeTargets.Delegate, AllowMultiple = false, Inherited = false)]
    internal sealed class NullableContextAttribute : Attribute {
        public readonly byte Flag;

        public NullableContextAttribute(byte P_0) {
            Flag = P_0;
        }
    }
    internal sealed class IsUnmanagedAttribute : Attribute {
    }

}
