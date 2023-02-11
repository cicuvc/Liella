using System;

namespace System.Runtime.CompilerServices
{
    public enum MethodImplOptions
    {
        NoInlining = 8,
        ForwardRef = 16,
        Synchronized = 32,
        NoOptimization = 64,
        PreserveSig = 128,
        AggressiveInlining = 256,
        InternalCall = 4096
    }
    public sealed class MethodImplAttribute : Attribute
    {
        public MethodImplAttribute(MethodImplOptions value)
        {

        }
        public MethodImplAttribute(short value)
        {

        }
    }
    public unsafe sealed class MethodLink : Attribute {
        public MethodLink(void *value) {

        }
    }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Field, Inherited = false)]
    internal sealed class IntrinsicAttribute : Attribute
    {
    }
}
