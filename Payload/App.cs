using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Payload
{
    interface IA {
        void Func1(int x);
        void Func2(int x);
    }
    class ClassA:IA {
        public ulong m_Value = 0;
        public virtual void Inc1(uint delta) {
            m_Value+=delta;
        }
        public void Func1(int x) {
            m_Value *= (uint)x;
        }
        public void Func2(int x) {
            m_Value /= (uint)x;
        }
    }
    interface IAddable<T> where T : IAddable<T> {
        T Add(T a, T b);
    }
    
    class ClassB:ClassA {
        public override void Inc1(uint delta) {
            m_Value += delta*2;
        }
        public void BF2(uint g) {
            m_Value += g;
        }
    }
    interface IF {
        void P2();
    }
    class ISRC:IDisposable {
        public void Dispose() {
            App.Printf("Dispose called\n");
        }
        public void Func1() {

        }
    }
    abstract class ACS {
        public abstract void M1();
        public abstract void M2();
        public void M3() {
            App.Printf("M3 called\n");
        }
    }
    class ACP : ACS {
        public override void M1() {
            App.Printf("M1 called\n");
        }
        public override void M2() {
            App.Printf("M2 called\n");
        }
    }
    public unsafe class App {
        private static void* funcPtr;
        [DllImport("kernel32")]
        private extern static int GetStdHandle(int type);
        [DllImport("kernel32")]
        private extern static int WriteFile(int handle,void *buffer,int length, int *writeLength, void *overlapped);
        [DllImport("kernel32")]
        private extern static IntPtr LoadLibraryA(void* path);
        [DllImport("kernel32")]
        private extern static void* GetProcAddress(IntPtr handle, void* name);


        private static void* printfPtr;

        public static ulong Fib(uint x) {
            if (x <= 2) return 1;
            return Fib(x - 1) + Fib(x - 2);
        }
        public static void Printf<T1,T2>(string fmt, T1 t1, T2 t2){
            ((delegate*<void*,T1,T2,int>)printfPtr)(fmt, t1,t2);
        }
        public static void Printf<T1>(string fmt, T1 t1) {
            ((delegate*<void*, T1, int>)printfPtr)(fmt, t1);
        }
        public static void Printf(string fmt) {
            ((delegate*<void*, int>)printfPtr)(fmt);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void CallF1(IA ifc) {
            //Printf("Interface base: %llu\n", Unsafe.AsPtr(ifc));
            ifc.Func1(12);
            ifc.Func2(6);
        }
        static delegate*<ulong, void*> malloc;
        static void OutFunc(ref int xv) {
            xv *=12;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        static int Wrap(object obj) {
            return obj.GetHashCode();
        }
        public static void F64(int x) {
            Printf("Hello %d\n",x);
        }

        delegate void FP3(int x);
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void RealMain() {
            
            var acp = new ACP();
            acp.M1();
            acp.M2();
            acp.M3();

        }
        public static long SumVa(int cnt, __arglist) {
            var result = 0l;
            var valist = __arglist;
            ref var list = ref valist.m_Valist;
            while ((cnt--) != 0) {
                var fv = list.GetNextValue<long>();
                Printf("Arg = %d\n", fv);
                result += fv;
            }
            list.End();
            return result;
        }

        public static int Main() {
            var hLib = LoadLibraryA("msvcrt");
            printfPtr = GetProcAddress(hLib, "printf");
            malloc = (delegate*<ulong, void*>)GetProcAddress(hLib, "malloc");
            var gcBase = malloc(4096 * 16);
            if (gcBase == null) {
                Printf("GC Heap initialization failed. Exit\n");
                return 11451419;
            }
            
            RuntimeHelpers.SetGCHeapStart((byte*)gcBase);

            RealMain();
            //Printf("Ans = %d\n", SumVa(7, __arglist(1, 2, 3,4,5,6,7)));


            return 0;
        }
    }
}
