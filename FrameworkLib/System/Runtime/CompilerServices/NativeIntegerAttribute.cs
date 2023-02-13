using System;


namespace System.Runtime.CompilerServices {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter, AllowMultiple = false, Inherited = false)]
    internal sealed class NativeIntegerAttribute : Attribute {

        public NativeIntegerAttribute() {
        }

        public NativeIntegerAttribute(bool[] A_0) {
        }
    }

}
