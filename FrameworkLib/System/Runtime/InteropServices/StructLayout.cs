using System;

namespace System.Runtime.InteropServices
{
    public class UnmanagedType { }

    public enum CharSet {
      
        None = 1,
        Ansi = 2,
        Unicode = 3,
        Auto = 4
    }

    public sealed class StructLayoutAttribute : Attribute
    {
        public CharSet CharSet;
        public int Pack;
        public int Size;
        public StructLayoutAttribute(LayoutKind layoutKind)
        {
        }
    }

    public enum LayoutKind {
        Sequential = 0, // 0x00000008,
        Explicit = 2, // 0x00000010,
        Auto = 3, // 0x00000000,
    }
    public sealed class FieldOffsetAttribute : Attribute {
        public FieldOffsetAttribute(int offset) {

        }
    }
}