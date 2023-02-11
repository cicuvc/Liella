using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System
{
    public class Object 
    {
        public virtual int GetHashCode()
        {
            return (int)(Unsafe.AsPtr(this) & 0x7FFFFFFF);
        }
        public virtual bool Equals(object? obj)
        {
            return RuntimeHelpers.Equals(this, obj);
        }
        public virtual object? GetType()
        {
            return null;
        }
        public static unsafe implicit operator void*(object pthis) {
            return (void*)Unsafe.AsPtr(pthis);
        }
    }
    public struct Void { }

    // The layout of primitive types is special cased because it would be recursive.
    // These really don't need any fields to work.
    public struct Boolean { }
    public struct Char { }
    public struct SByte { }
    public struct Byte { }
    public struct Int16 { }
    public struct UInt16 { }
    public struct Int32 {
        public int m_RealValue;
        public override int GetHashCode() {
            return m_RealValue;
        }
    }
    public struct UInt32 { }
    public struct Int64 { }
    public struct UInt64 { 
        public void Test() { }
    }
    public unsafe struct IntPtr
    {
        public static readonly IntPtr Zero = new IntPtr();
        private unsafe void* value;

        public IntPtr(void *ptrValue)
        {
            value = ptrValue;
        }

        public static bool operator !=(IntPtr a, IntPtr b)
        {
            return a.value != b.value;
        }
        public static bool operator ==(IntPtr a, IntPtr b)
        {
            return a.value == b.value;
        }
        public unsafe override int GetHashCode()
        {
            long num = (long)value;
            return (int)num ^ (int)(num >> 32);
        }
        public unsafe override bool Equals(object? obj)
        {
            if (obj is IntPtr)
            {
                IntPtr intPtr = (IntPtr)obj;
                return value == intPtr.value;
            }
            return false;
        }
        public unsafe static explicit operator long(IntPtr value)
        {
            return *(long*)&value.value;
        }
        public unsafe static explicit operator int(IntPtr value) {
            return (int)value.value;
        }
        public unsafe static explicit operator void*(IntPtr value) {
            return value.value;
        }
    }
    public struct UIntPtr { }
    public struct Single { }
    public struct Double { }
    public ref struct TypedReference
    {

    }

    public abstract class ValueType { }
    public abstract class Enum : ValueType { }

    public struct Nullable<T> where T : struct { }

    public unsafe struct RuntimeVaList {
        public void* m_Value4;
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern T? GetNextValue<T>();
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern void End();
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RuntimeArgumentHandle {
        public RuntimeVaList m_Valist;
        
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe sealed class String
    {
        public readonly byte* buffer;
        public readonly int Length;
        
        public static string Format(string format, object o1)
        {
            return format;
        }
        public static string Format(string format, __arglist) {
            return format;
        }

        public unsafe static implicit operator void*(String value) {
            return value.buffer;
        }

    }
    public abstract class Array
    {
        public class Nested
        {

        }
    }
    public class Array<T> : Array { }
    public unsafe abstract class Delegate {
        public object? Context;
        public void *FunctionPtr;
        public Delegate(object context, nint funcPtr)
        {
            Context = context;
            FunctionPtr = (void *)funcPtr;
        }
    }
    public abstract class MulticastDelegate : Delegate {
        public MulticastDelegate(object context, nint funcPtr)
            : base(context, funcPtr)
        { }
    }

    public struct RuntimeTypeHandle { }
    public struct RuntimeMethodHandle { }
    public struct RuntimeFieldHandle { }

    public class Attribute { }
    public class Exception { }

    public sealed class AttributeUsageAttribute : Attribute
    {
        public AttributeUsageAttribute(AttributeTargets validOn)
        {
            ValidOn = validOn;
        }
        public bool AllowMultiple { get; set; }
        public bool Inherited { get; set; }
        public AttributeTargets ValidOn { get; }
    }
    public enum AttributeTargets
    {
        Assembly = 1,
        Module = 2,
        Class = 4,
        Struct = 8,
        Enum = 16,
        Constructor = 32,
        Method = 64,
        Property = 128,
        Field = 256,
        Event = 512,
        Interface = 1024,
        Parameter = 2048,
        Delegate = 4096,
        ReturnValue = 8192,
        GenericParameter = 16384,
        All = 32767
    }
}
