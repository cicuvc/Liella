using System;

namespace System {
    public static class Math {
        public static ulong AlignCeil(ulong size, ulong unit) {
            return (size + unit - 1) / unit;
        }
        public static uint AlignCeil(uint size, uint unit) {
            return (size + unit - 1) / unit;
        }
    }
}
 