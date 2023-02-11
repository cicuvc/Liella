using System;

namespace System.Runtime.CompilerServices {
    public sealed class Unsafe {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern unsafe static T DereferenceInvariant<T>(T* ptr) where T : unmanaged;
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern unsafe static T DereferenceInvariantIndex<T>(T* ptr, ulong index) where T : unmanaged;

        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern unsafe static ulong AsPtr<T>(T obj) where T:class;
    }
}
