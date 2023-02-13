using Liella.Metadata;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM {
    public class LLVMClassTypeInfo : LLVMTypeInfo {
        protected static Dictionary<string, Func<LLVMCompiler, LLVMCompType>> s_PrimitiveTypesMap = new Dictionary<string, Func<LLVMCompiler, LLVMCompType>>() {
            {"System::Boolean",e=>LLVMCompType.CreateType(LLVMTypeTag.Struct,LLVMTypeRef.Int1) },
            {"System::Byte",e=>LLVMCompType.CreateType(LLVMTypeTag.Struct|LLVMTypeTag.UnsignedInt,LLVMTypeRef.Int8) },
            {"System::SByte",e=>LLVMCompType.CreateType(LLVMTypeTag.Struct|LLVMTypeTag.SignedInt,LLVMTypeRef.Int8) },
            {"System::Int16",e=>LLVMCompType.CreateType(LLVMTypeTag.Struct|LLVMTypeTag.SignedInt,LLVMTypeRef.Int16) },
            {"System::UInt16",e=>LLVMCompType.CreateType(LLVMTypeTag.Struct|LLVMTypeTag.UnsignedInt,LLVMTypeRef.Int16) },
            {"System::Int32",e=>LLVMCompType.CreateType(LLVMTypeTag.Struct|LLVMTypeTag.SignedInt,LLVMTypeRef.Int32) },
            {"System::UInt32",e=>LLVMCompType.CreateType(LLVMTypeTag.Struct|LLVMTypeTag.UnsignedInt,LLVMTypeRef.Int32) },
            {"System::Int64",e=>LLVMCompType.CreateType(LLVMTypeTag.Struct|LLVMTypeTag.SignedInt,LLVMTypeRef.Int64) },
            {"System::UInt64",e=>LLVMCompType.CreateType(LLVMTypeTag.Struct|LLVMTypeTag.UnsignedInt,LLVMTypeRef.Int64) },
            {"System::Double",e=>LLVMCompType.CreateType(LLVMTypeTag.Struct|LLVMTypeTag.Real | LLVMTypeTag.FP64,LLVMTypeRef.Double) },
            {"System::Single",e=>LLVMCompType.CreateType(LLVMTypeTag.Struct|LLVMTypeTag.Real,LLVMTypeRef.Float) },
            {"System::Void",e=>LLVMCompType.CreateType(LLVMTypeTag.Struct,LLVMTypeRef.Void) },
            {"System::IntPtr",e=>LLVMCompType.CreateType(LLVMTypeTag.Struct|LLVMTypeTag.SignedInt|LLVMTypeTag.Pointer,LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8,0)) },
            {"System.Runtime.CompilerServices::TypeMetadata",e=>LLVMCompType.CreateType(LLVMTypeTag.Struct, e.TypeMetadataType) }
        };

        protected LLVMTypeRef m_DataStorageType = LLVMTypeRef.Void;
        protected LLVMCompType m_ReferenceType;
        protected LLVMCompType m_HeapPtrType;

        protected ulong m_DataStorageSize;

        protected LLVMValueRef[] m_VirtualTables = null;
        protected LLVMValueRef[] m_MainTableValues = null;

        protected MethodInstance[] m_MainTableMethods = null;

        public LLVMValueRef[] VirtualTables => m_VirtualTables;
        public LLVMValueRef[] MainTableValues => m_MainTableValues;
        public LLVMValueRef StaticStorageValue => m_StaticStorageBody;
        public override ulong DataStorageSize => m_DataStorageSize;

        public LLVMTypeRef DataStorageType => m_DataStorageType;
        public LLVMCompType ReferenceType => m_ReferenceType;
        public LLVMCompType HeapPtrType => m_HeapPtrType;


        /*
         * Static Data Type : Type of static data storage 
         * Data storage Type: Type containing instance fields
         * Reference Type: MT+DataStorageType
         * Instacne Type: Pointer to reference type for classes; Same as data storage type for structures
         * 
         */
        public LLVMClassTypeInfo(LLVMCompiler compiler, LiTypeInfo typeInfo)
            : base(compiler, typeInfo) {

            m_DataStorageType = m_Compiler.Context.CreateNamedStruct($"data.{m_TypeName}");
            m_HeapPtrType = LLVMCompType.CreateType(LLVMTypeTag.Class, LLVMTypeRef.CreatePointer(m_DataStorageType, 0));

            var refType = m_Compiler.Context.CreateNamedStruct($"ref.{m_TypeName}");
            refType.StructSetBody(new LLVMTypeRef[] {
                LLVMTypeRef.CreatePointer(m_VtableType,0),
                m_DataStorageType
            }, false);
            m_ReferenceType = LLVMCompType.CreateType(LLVMTypeTag.Class, LLVMTypeRef.CreatePointer(refType, 0));

            if (s_PrimitiveTypesMap.ContainsKey(m_TypeName)) {
                m_InstanceType = s_PrimitiveTypesMap[m_TypeName](compiler);
                return;
            }
            if (typeInfo.BaseType != null && typeInfo.BaseType.Entry == m_Compiler.TypeEnvironment.IntrinicsTypes["System::Enum"]) {
                var field0 = typeInfo.Fields.First().Value.Type;
                m_InstanceType = s_PrimitiveTypesMap[field0.ToString()](compiler);
                return;
            }
            m_InstanceType = LLVMCompType.CreateType(
                m_MetadataType.IsValueType ? LLVMTypeTag.Struct : LLVMTypeTag.Class,
                m_MetadataType.IsValueType ? m_DataStorageType : m_HeapPtrType.LLVMType);

        }
        protected override LLVMCompType SetupLLVMTypesImpl() {
            base.SetupLLVMTypesImpl();

            var layout = m_MetadataType.Layout;

            foreach (var i in m_MetadataType.Fields) {
                var llvmType = m_Compiler.ResolveDepLLVMType(i.Value.Type);
                llvmType.SetupLLVMTypes();
            }

            // explicit layout
            if (m_MetadataType.Attributes.HasFlag(TypeAttributes.ExplicitLayout)) {
                throw new NotImplementedException();
                var structSize = 0ul;
                foreach (var i in m_MetadataType.Fields) {
                    //var llvmType = i.Value.Type is PointerTypeEntry ? 8: m_Compiler.ResolveLLVMInstanceType(i.Value.Type);
                    //structSize = Math.Max(structSize, (uint)i.Value.Definition.GetOffset() + llvmType.DataStorageSize);
                }
                structSize = Math.Max(structSize, (ulong)layout.Size);

                m_DataStorageSize = structSize;
                m_DataStorageType.StructSetBody(new LLVMTypeRef[] { LLVMTypeRef.CreateArray(LLVMTypeRef.Int8, (uint)structSize) }, false);

                return m_InstanceType;
            }
            // sequential and auto
            // TODO: Auto layout optimization
            //if (m_TypeName.Contains("EFI_SYSTEM_TABLE")) Debugger.Break();

            var fieldList = m_MetadataType.Fields.Values.ToList();
            fieldList.Sort((u, v) => u.FieldIndex.CompareTo(v.FieldIndex));
            var instanceFields = fieldList.Select(e => m_Compiler.ResolveLLVMInstanceType(e.Type).LLVMType).ToList();
            if (m_BaseType != null) instanceFields.Insert(0, m_BaseType.m_DataStorageType);
            m_DataStorageType.StructSetBody(instanceFields.ToArray(), false);
            var neturalSize = LLVMHelpers.EvaluateConstIntValue(m_DataStorageType.SizeOf, m_Compiler);

            if (layout.IsDefault) {
                m_DataStorageSize = neturalSize;
            } else {
                var realSize = (uint)(Math.Ceiling(1.0 * layout.Size / 4) * 4);
                if (realSize >= neturalSize) {
                    instanceFields.Add(LLVMTypeRef.CreateArray(LLVMTypeRef.Int8, (uint)(realSize - neturalSize)));
                    m_DataStorageType.StructSetBody(instanceFields.ToArray(), false);
                    m_DataStorageSize = realSize;
                }
            }

            return m_InstanceType;
        }


        protected void FillInterface(LLVMInterfaceTypeInfo interfaceType, LLVMValueRef[] interfaceValues, ref int unknownTerms) {
            foreach (var i in m_MetadataType.Methods) {
                var llvmMethod = m_Compiler.ResolveLLVMMethod(i.Key);
                var index = interfaceType.LocateMethodInMainTable(llvmMethod);
                if (index >= 0 && interfaceValues[index + 1] == default) {
                    interfaceValues[index + 1] = llvmMethod.Function;
                    unknownTerms--;
                }
            }

            if (unknownTerms != 0 && m_BaseType != null) m_BaseType.FillInterface(interfaceType, interfaceValues, ref unknownTerms);
        }
        protected LLVMValueRef GenerateMainVTable() {
            //if (m_MetadataType.Entry.ToString().Contains("ClassB")) Debugger.Break();
            var metadataType = m_MetadataType;
            var mainVTTypes = new List<LLVMTypeRef>();
            var mainVTValues = new List<LLVMValueRef>();
            var mainVTMethods = new List<MethodInstance>();

            if (m_BaseType != null) {
                m_BaseType.GenerateVTable();
                mainVTTypes.AddRange(m_BaseType.m_VirtualTables[0].TypeOf.StructElementTypes);
                mainVTValues.AddRange(m_BaseType.m_MainTableValues);
                mainVTMethods.AddRange(m_BaseType.m_MainTableMethods);
            } else {
                mainVTTypes.Add(m_Compiler.TypeMetadataType);
                mainVTValues.Add(default); // padding
                mainVTMethods.Add(null);
            }



            var baseMainTableMethods = m_BaseType != null ? m_BaseType.m_MainTableMethods : Array.Empty<MethodInstance>();

            foreach (var i in metadataType.Methods.Values) {
                var attrib = i.Attributes;
                var llvmMethod = m_Compiler.ResolveLLVMMethod(i.Entry);
                var llvmMethodPtrType = LLVMTypeRef.CreatePointer(llvmMethod.FunctionType, 0);

                if (!attrib.HasFlag(MethodAttributes.Virtual)) continue;
                if (!attrib.HasFlag(MethodAttributes.NewSlot)) {
                    var vtableIndex = 0;
                    foreach (var j in baseMainTableMethods) {
                        if (j != null && j.Entry.Name == i.Entry.Name && j.EqualsSignature(i)) {

                            mainVTValues[vtableIndex] = llvmMethod.Function;
                            mainVTTypes[vtableIndex] = llvmMethodPtrType;
                            mainVTMethods[vtableIndex] = i;
                            break;
                        }
                        vtableIndex++;
                    }
                } else {
                    mainVTMethods.Add(i);
                    mainVTTypes.Add(llvmMethodPtrType);
                    mainVTValues.Add(attrib.HasFlag(MethodAttributes.Abstract) ? (LLVMValueRef.CreateConstNull(llvmMethodPtrType)) : llvmMethod.Function);
                }
            }

            mainVTTypes.Add(LLVMTypeRef.CreatePointer(m_VtableType, 0));
            mainVTValues.Add(m_VtableBody);
            mainVTMethods.Add(null);

            // Insert a pointer to main table itself to mark the end of the table
            var mainVTLLVMType = m_Compiler.Context.CreateNamedStruct($"tMT.{m_TypeName}");
            mainVTLLVMType.StructSetBody(mainVTTypes.ToArray(), false);
            mainVTValues[0] = LLVMValueRef.CreateConstNamedStruct(m_Compiler.TypeMetadataType, new LLVMValueRef[] {
                LLVMHelpers.CreateConstU32(((uint)m_MetadataType.Attributes)),
                LLVMHelpers.CreateConstU32(m_Interfaces.Count),
                LLVMValueRef.CreateConstIntCast(mainVTLLVMType.SizeOf,LLVMTypeRef.Int32,false)
            });
            var mainVTable = LLVMValueRef.CreateConstNamedStruct(mainVTLLVMType, mainVTValues.ToArray());

            m_MainTableMethods = mainVTMethods.ToArray();
            m_MainTableValues = mainVTValues.ToArray();

            return mainVTable;
        }
        protected override void GenerateVTableImpl() {


            var vtableTypes = new LLVMTypeRef[m_Interfaces.Count + 2];

            m_VirtualTables = new LLVMValueRef[m_Interfaces.Count + 2];
            m_VirtualTables[0] = GenerateMainVTable();
            vtableTypes[0] = m_VirtualTables[0].TypeOf;
            vtableTypes[1] = LLVMTypeRef.CreateArray(LLVMTypeRef.Int32, (uint)(2u * m_Interfaces.Count));
            var typeIndex = 2;
            foreach (var i in m_Interfaces) vtableTypes[typeIndex++] = i.VtableType;
            m_VtableType.StructSetBody(vtableTypes, false);

            var interfaceLookupTable = new LLVMValueRef[2u * m_Interfaces.Count];
            var lookupIndex = 0;
            foreach (var i in m_Interfaces) {
                interfaceLookupTable[lookupIndex * 2] = LLVMHelpers.CreateConstU32(i.TypeHash);
                var nullPtr = LLVMValueRef.CreateConstNull(LLVMTypeRef.CreatePointer(m_VtableType, 0));
                interfaceLookupTable[lookupIndex * 2 + 1] = LLVMValueRef.CreateConstPtrToInt(
                    LLVMValueRef.CreateConstGEP(nullPtr,
                    new LLVMValueRef[] {
                        LLVMHelpers.CreateConstU32(0),
                        LLVMHelpers.CreateConstU32(lookupIndex+2)
                    }), LLVMTypeRef.Int32);
            }
            m_VirtualTables[1] = LLVMValueRef.CreateConstArray(LLVMTypeRef.Int32, interfaceLookupTable);

            var index = 2;
            foreach (var i in m_Interfaces) {
                var interfaceValue = new LLVMValueRef[i.InterfaceTerms.Count + 1];
                var interfaceOffset = interfaceLookupTable[(index - 2) * 2 + 1];
                var interfaceLength = LLVMHelpers.CreateConstU32(0);

                interfaceValue[0] = LLVMValueRef.CreateConstNamedStruct(m_Compiler.InterfaceHeaderType, new LLVMValueRef[] {
                    LLVMValueRef.CreateConstTrunc(interfaceOffset,LLVMTypeRef.Int16),
                    LLVMValueRef.CreateConstTrunc(interfaceLength,LLVMTypeRef.Int16),
                    LLVMHelpers.CreateConstU32(i.TypeHash)
                });

                var unkTerms = i.InterfaceTerms.Count;
                FillInterface(i, interfaceValue, ref unkTerms);

                var implInfo = m_MetadataType.ImplInfo;

                foreach (var j in implInfo) {
                    var implMethod = m_Compiler.ResolveLLVMMethod(j.ImplBody);
                    var declMethod = m_Compiler.ResolveLLVMMethod(j.InterfaceDecl);
                    var interfaceType = (LLVMInterfaceTypeInfo)m_Compiler.ResolveLLVMType(j.InterfaceDecl.TypeEntry);
                    if (interfaceType == i || i.Interfaces.Contains(interfaceType)) {
                        var vtableIndex = i.LocateMethodInMainTableStrict(declMethod, interfaceType);
                        if (vtableIndex >= 0) {
                            if (interfaceValue[vtableIndex + 1] == default) unkTerms--;
                            interfaceValue[vtableIndex + 1] = implMethod.Function;
                        }
                    }
                }


                if (unkTerms != 0) throw new Exception($"Incomplete vtable for interface {i}");
                var types = i.VtableType.StructElementTypes;
                for (var k = 0; k < interfaceValue.Length; k++) {
                    interfaceValue[k] = LLVMValueRef.CreateConstBitCast(interfaceValue[k], types[k]);
                }
                m_VirtualTables[index++] = LLVMValueRef.CreateConstNamedStruct(i.VtableType, interfaceValue);
            }

            m_VtableBody.Initializer = LLVMValueRef.CreateConstNamedStruct(m_VtableType, m_VirtualTables);
            m_VtableBody.IsGlobalConstant = true;
        }
        public override int LocateMethodInMainTable(LLVMMethodInfoWrapper method) {
            for (var i = 0; i < m_MainTableMethods.Length; i++) {
                if (m_MainTableMethods[i] == method.Method) return i;
            }
            return -1;
        }
    }

}
