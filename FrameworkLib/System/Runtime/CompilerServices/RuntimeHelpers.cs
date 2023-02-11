using System;
using System.Runtime.InteropServices;

namespace System.Runtime.CompilerServices
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct TypeMetadata
    {
        //public char* Name;
        public int Attributes;
        public int InterfaceCount;
        public int MainVTSize;
    }


    public unsafe class RuntimeHelpers
    {
        public static unsafe int OffsetToStringData => sizeof(IntPtr) + sizeof(int);
        public static byte* m_GCHeapStart;

        public static bool Equals(object objA,object? objB)
        {
            return false;
        }

        //[MethodImpl(MethodImplOptions.InternalCall)]
        public static unsafe void *GCHeapAlloc(ulong size)
        {
            var objPos = m_GCHeapStart;
            m_GCHeapStart += size;
            return (void*)objPos;
        }
        public static unsafe void SetGCHeapStart(byte* heap)
        {
            m_GCHeapStart = heap;
        }

        
        public static void ThrowCastException() {
            while (true) ;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TypeMetadata *GetTypeMetadata(ulong obj) {
            return (TypeMetadata*)((obj >> 40) & 0xFFFFFF);
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern static void RunStaticConstructors();

        public static unsafe void* CastToInterface(void* obj, uint interfaceTypeHash) {

            ulong classVtableHeader = Unsafe.DereferenceInvariant(((ulong*)obj)-1);
            var ptrMetadata = (TypeMetadata*)classVtableHeader;
            var interfaceList = (uint*)(((byte*)classVtableHeader) + Unsafe.DereferenceInvariant(&ptrMetadata->MainVTSize));
            var interfaceCount = Unsafe.DereferenceInvariant(&ptrMetadata->InterfaceCount);
            for(var i=0u;i< interfaceCount; i++) {
                var key = Unsafe.DereferenceInvariantIndex(interfaceList, i*2);
                if(key == interfaceTypeHash) {
                    var offset = (ulong)(Unsafe.DereferenceInvariantIndex(interfaceList, i * 2+1) >> 3);
                    return (void*)((((ulong)obj) & 0xFFFFFFFFFFFF) | ((offset& 0xFFFF)<<48));
                }
            }
            ThrowCastException();
            return null;
        }
        public static unsafe void *LookupInterfaceVtable(void *obj,void *rawObj, uint offset) {
            var objectIntPtr = (ulong)obj;
            var interfaceIndex = (objectIntPtr >> 48);
            ulong classVtableHeader = Unsafe.DereferenceInvariant(((ulong*)rawObj)-1);
            //var ptrMetadata = (int*)classVtableHeader;
            //var interfaceList = (uint*)(((byte*)classVtableHeader) + Unsafe.DereferenceInvariantIndex(ptrMetadata,2));
            var interfaceVtable = ((byte*)classVtableHeader) + (interfaceIndex * 8);//Unsafe.DereferenceInvariantIndex(interfaceList,interfaceIndex * 2 + 1);
            return (void*)Unsafe.DereferenceInvariant((ulong*)(interfaceVtable + offset));
        }
        public static unsafe void* IsInstClass(void* obj, ulong mainTableSize, void* mainTableAddr) {
            if (obj == null) return null;
            ulong classVtableHeader = *(ulong*)obj;
            void** pEndMark = (void**)(classVtableHeader + mainTableSize - 8);
            return *pEndMark == mainTableAddr ? obj : null;
        }
        public static unsafe void* IsInstInterface(void* obj, uint interfaceTypeHash) {
            ulong classVtableHeader = *(ulong*)obj;
            var ptrMetadata = (TypeMetadata*)classVtableHeader ;
            var interfaceList = (uint*)(((byte*)ptrMetadata) + ptrMetadata->MainVTSize);
            for (var i = 0u; ; i += 2) {
                var key = Unsafe.DereferenceInvariantIndex(interfaceList, i);
                if (interfaceTypeHash == key) {
                    return obj;
                }
                if (key == 0) break;
            }
            return null;
        }

        public static void *ToUTF16String(string s) {
            return null;
        }
    }

    public static class RuntimeFeature
    {
        public const string UnmanagedSignatureCallingConvention = nameof(UnmanagedSignatureCallingConvention);
    }
}