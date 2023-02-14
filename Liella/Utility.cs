using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Liella
{
    public unsafe static class MetadataHelper
    {
        private static Dictionary<MetadataReader, uint> m_MetadataReaders = new Dictionary<MetadataReader, uint>();
        private struct MetadataReaderView
        {
            public MetadataReader Reader;
            public uint Token;
        }

        public static unsafe MetadataReader CreateMetadataReader(byte *metadata, int length)
        {
            var reader =  new MetadataReader(metadata, length);
            var ptrObject = (IntPtr *)Unsafe.AsPointer(ref reader);
            m_MetadataReaders.Add(reader,(uint)(ptrObject->ToInt64() ^ 0xFFFFFFFFu) );
            return reader;
        }
        public static unsafe uint GetReaderID(MetadataReader reader)
        {

            return m_MetadataReaders[reader];
        }
        public static uint MakeHash(ref TypeDefinition typeDef)
        {
            var pTypeDef = Unsafe.AsPointer(ref typeDef);
            var view = Unsafe.AsRef<MetadataReaderView>(pTypeDef);
            return view.Token ^ GetReaderID(view.Reader);
        }

        public static uint ExtractToken(ref TypeDefinition typeDef)
        {
            var pTypeDef = Unsafe.AsPointer(ref typeDef);
            return Unsafe.AsRef<MetadataReaderView>(pTypeDef).Token;
        }
        public static uint ExtractToken(ref MethodDefinition typeDef)
        {
            var pTypeDef = Unsafe.AsPointer(ref typeDef);
            return Unsafe.AsRef<MetadataReaderView>(pTypeDef).Token;
        }
        public static MetadataReader GetMetadataReader(ref TypeDefinition typeDef)
        {
            var pTypeDef = Unsafe.AsPointer(ref typeDef);
            return Unsafe.AsRef<MetadataReaderView>(pTypeDef).Reader;
        }
        public static MetadataReader GetMetadataReader(ref FieldDefinition typeDef)
        {
            var pTypeDef = Unsafe.AsPointer(ref typeDef);
            return Unsafe.AsRef<MetadataReaderView>(pTypeDef).Reader;
        }
        public static MetadataReader GetMetadataReader(ref MethodDefinition methodDef)
        {
            var pTypeDef = Unsafe.AsPointer(ref methodDef);
            return Unsafe.AsRef<MetadataReaderView>(pTypeDef).Reader;
        }
        public static MetadataReader GetMetadataReader(ref MethodSpecification typeDef)
        {
            var pTypeDef = Unsafe.AsPointer(ref typeDef);
            return Unsafe.AsRef<MetadataReaderView>(pTypeDef).Reader;
        }
        public static MetadataReader GetMetadataReader(ref TypeReference typeRef)
        {
            var pTypeDef = Unsafe.AsPointer(ref typeRef);
            return Unsafe.AsRef<MetadataReaderView>(pTypeDef).Reader;
        }
        public static MetadataReader GetMetadataReader(ref MethodImplementation impl)
        {
            var pTypeDef = Unsafe.AsPointer(ref impl);
            return Unsafe.AsRef<MetadataReaderView>(pTypeDef).Reader;
        }
        public static MetadataReader GetMetadataReader(ref TypeSpecification impl)
        {
            var pTypeDef = Unsafe.AsPointer(ref impl);
            return Unsafe.AsRef<MetadataReaderView>(pTypeDef).Reader;
        }
        public unsafe static EntityHandle CreateHandle(uint token)
        {
            EntityHandle handle = default;
            *((uint*)&handle) = token;
            return handle;
        }
        public unsafe static UserStringHandle CreateStringHandle(uint token)
        {
            UserStringHandle handle = default;
            *((uint*)&handle) = token;
            return handle;
        }

    }
    public static class ILCodeExtension
    {
        private static byte[] s_OperandTypeSize = new byte[] {
            4,4,4,8,4,   0,0,8,0,   4,4,4,4,4,4,   1,1,1,1,
        };
        private static OpCode[] s_ILOpCodeMap = new OpCode[256];
        private static OpCode[] s_ILOpCodeMap2 = new OpCode[256];
        private static byte[] s_ILOperandSize = new byte[256];
        private static byte[] s_ILOperandSize2 = new byte[256];
        static ILCodeExtension()
        {
            var opcodes = typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public);
            foreach (var i in opcodes)
            {
                var opcode = (OpCode)i.GetValue(null);
                if (opcode.Value < 256 && opcode.Value >= 0)
                {
                    s_ILOpCodeMap[opcode.Value] = opcode;
                    s_ILOperandSize[opcode.Value] = s_OperandTypeSize[((int)opcode.OperandType)];
                }
                else
                {
                    s_ILOpCodeMap2[((uint)opcode.Value) & 0xFF] = opcode;
                    s_ILOperandSize2[((uint)opcode.Value) & 0xFF] = s_OperandTypeSize[((int)opcode.OperandType)];
                }
            }
        }
        public static int GetOperandSize(this ILOpCode ilCode)
        {
            var ilCodeValue = ((uint)ilCode);
            if (ilCodeValue < 256 && ilCodeValue >= 0)
            {
                return s_ILOperandSize[(uint)ilCode];
            }
            else
            {
                return s_ILOperandSize2[ilCodeValue & 0xFF];
            }
        }
        public static OpCode ToOpCode(this ILOpCode ilCode)
        {
            var ilCodeValue = ((uint)ilCode);
            if (ilCodeValue < 256 && ilCodeValue >= 0)
            {
                return s_ILOpCodeMap[(uint)ilCode];
            }
            else
            {
                return s_ILOpCodeMap2[ilCodeValue & 0xFF];
            }
        }
        public static Dictionary<ILOpCode, int> StackDeltaTable => s_StackDeltaTable;
        private static Dictionary<ILOpCode, int> s_StackDeltaTable = new Dictionary<ILOpCode, int>() {
            {ILOpCode.Nop, 0 },
            {ILOpCode.Ldarg, 1 },
            {ILOpCode.Ldarga, 1 },
            {ILOpCode.Ldarga_s, 1 },
            {ILOpCode.Ldarg_0, 1 },
            {ILOpCode.Ldarg_1, 1 },
            {ILOpCode.Ldarg_2, 1 },
            {ILOpCode.Ldarg_3, 1 },
            {ILOpCode.Ldarg_s, 1 },
            {ILOpCode.Ldc_i4, 1 },
            {ILOpCode.Ldc_i4_0, 1 },
            {ILOpCode.Ldc_i4_1, 1 },
            {ILOpCode.Ldc_i4_2, 1 },
            {ILOpCode.Ldc_i4_3, 1 },
            {ILOpCode.Ldc_i4_4, 1 },
            {ILOpCode.Ldc_i4_5, 1 },
            {ILOpCode.Ldc_i4_6, 1 },
            {ILOpCode.Ldc_i4_7, 1 },
            {ILOpCode.Ldc_i4_8, 1 },
            {ILOpCode.Ldc_i4_m1, 1 },
            {ILOpCode.Ldc_i4_s, 1 },
            {ILOpCode.Ldfld, 0 },
            {ILOpCode.Ldflda, 0 },
            {ILOpCode.Ldsfld, 1 },
            {ILOpCode.Ldsflda, 1 },
            {ILOpCode.Ldnull, 1 },
            {ILOpCode.Ldind_i, 0 },
            {ILOpCode.Ldind_i1, 0 },
            {ILOpCode.Ldind_i2, 0 },
            {ILOpCode.Ldind_i4, 0 },
            {ILOpCode.Ldind_i8, 0 },
            {ILOpCode.Ldind_u1, 0 },
            {ILOpCode.Ldind_u2, 0 },
            {ILOpCode.Ldind_u4, 0 },
            {ILOpCode.Ldind_r4, 0 },
            {ILOpCode.Ldind_r8, 0 },
            {ILOpCode.Stfld, -2 },
            {ILOpCode.Stsfld, -1 },
            {ILOpCode.Add, -1 },
            {ILOpCode.Sub, -1 },
            {ILOpCode.Div, -1 },
            {ILOpCode.Xor, -1 },
            {ILOpCode.And, -1 },
            {ILOpCode.Not, 0 },
            {ILOpCode.Or, -1 },
            {ILOpCode.Mul, -1 },
            {ILOpCode.Mul_ovf_un, -1 },
            {ILOpCode.Shr, -1 },
            {ILOpCode.Shr_un, -1 },
            {ILOpCode.Shl, -1 },
            {ILOpCode.Br, 0 },
            {ILOpCode.Br_s, 0 },
            {ILOpCode.Brtrue, -1 },
            {ILOpCode.Brtrue_s, -1 },
            {ILOpCode.Brfalse, -1 },
            {ILOpCode.Brfalse_s, -1 },
            {ILOpCode.Call, int.MaxValue },
            {ILOpCode.Callvirt, int.MaxValue },
            {ILOpCode.Starg_s, -1},
            {ILOpCode.Stloc_0, -1},
            {ILOpCode.Stloc_1, -1},
            {ILOpCode.Stloc_2, -1},
            {ILOpCode.Stloc_3, -1},
            {ILOpCode.Stloc_s, -1},
            {ILOpCode.Stloc, -1},
            {ILOpCode.Ldloc, 1},
            {ILOpCode.Ldloca, 1},
            {ILOpCode.Ldloc_0, 1},
            {ILOpCode.Ldloc_1, 1},
            {ILOpCode.Ldloc_2, 1},
            {ILOpCode.Ldloc_3, 1},
            {ILOpCode.Ldloc_s, 1},
            {ILOpCode.Ldloca_s, 1},
            {ILOpCode.Ret, int.MaxValue},
            {ILOpCode.Ceq, -1},
            {ILOpCode.Cgt, -1},
            {ILOpCode.Cgt_un, -1},
            {ILOpCode.Clt, -1},
            {ILOpCode.Clt_un, -1},
            {ILOpCode.Initobj, -1},
            {ILOpCode.Box, 0},
            {ILOpCode.Isinst, 0},
            {ILOpCode.Conv_i, 0},
            {ILOpCode.Conv_i1, 0},
            {ILOpCode.Conv_i2, 0},
            {ILOpCode.Conv_i4, 0},
            {ILOpCode.Conv_i8, 0},
            {ILOpCode.Conv_u, 0},
            {ILOpCode.Conv_u4, 0},
            {ILOpCode.Conv_u8, 0},
            {ILOpCode.Neg, 0},
            {ILOpCode.Unbox_any, 0},
            {ILOpCode.Newobj, int.MaxValue},
            {ILOpCode.Sizeof, 1},
            {ILOpCode.Stind_i,-2 },
            {ILOpCode.Stind_i1,-2 },
            {ILOpCode.Stind_i2,-2 },
            {ILOpCode.Stind_i4,-2 },
            {ILOpCode.Stind_i8,-2 },
            {ILOpCode.Stind_r8,-2 },
            {ILOpCode.Stind_r4,-2 },
            {ILOpCode.Ldstr,1 },
            {ILOpCode.Localloc,0 },
            {ILOpCode.Ldftn,1 },
            {ILOpCode.Beq_s,-2 },
            {ILOpCode.Castclass,0 },
            {ILOpCode.Ldc_i8,1 },
            {ILOpCode.Pop,-1 },
            {ILOpCode.Rem_un,-1 },
            {ILOpCode.Rem,-1 },
            {ILOpCode.Div_un,-1 },
            {ILOpCode.Calli,int.MaxValue },
            {ILOpCode.Bgt_un_s,-2 },
            {ILOpCode.Bgt_un,-2 },
            {ILOpCode.Blt_un_s,-2 },
            {ILOpCode.Blt_un,-2 },
            {ILOpCode.Bgt_s,-2 },
            {ILOpCode.Bgt,-2 },
            {ILOpCode.Blt_s,-2 },
            {ILOpCode.Blt,-2 },
            {ILOpCode.Switch,-1 },
            {ILOpCode.Dup,1 },
            {ILOpCode.Ble_un_s,-2 },
            {ILOpCode.Ble_un,-2 },
            {ILOpCode.Bge_un_s,-2 },
            {ILOpCode.Bge_un,-2 },
            {ILOpCode.Ble_s,-2 },
            {ILOpCode.Ble,-2 },
            {ILOpCode.Bge_s,-2 },
            {ILOpCode.Bge,-2 },
            {ILOpCode.Constrained,0 },
            {ILOpCode.Ldc_r8,1 },
            {ILOpCode.Ldc_r4,1 },
            {ILOpCode.Conv_r8, 0 },
            {ILOpCode.Conv_r4,0 },
            {ILOpCode.Conv_u2,0 },
            {ILOpCode.Arglist,1 }
        };

    }
}
