using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LLVMSharp;
using LLVMSharp.Interop;


namespace Liella {
    public static class LLVMWrapperFactory {
        public static LLVMTypeInfo CreateLLVMType(LLVMCompiler compiler, TypeInfo typeInfo) {
            var typeInfoObj = (LLVMTypeInfo)(typeInfo.Attribute.HasFlag(TypeAttributes.Interface) ? new LLVMInterfaceTypeInfo(compiler, typeInfo) : new LLVMClassTypeInfo(compiler, typeInfo));
            typeInfoObj.TypeHash = compiler.RegisterTypeInfo(typeInfoObj);
            return typeInfoObj;
        }
    }
    public static class LLVMHelpers {
        private static string[] m_AttributeNames = new string[] { "alwaysinline", "noduplicate", "inlinehint","noinline", "nounwind" };
        private static string[] m_MetadataNames = new string[] { "invariant.load", "invariant.group", "absolute_symbol" };
        private static Dictionary<string, uint> m_AttributeKindMap = new Dictionary<string, uint>();
        private static Dictionary<string, uint> m_MetadataKindMap = new Dictionary<string, uint>();
        unsafe static LLVMHelpers() {
            foreach (var i in m_AttributeNames) {
                var namePtr = Marshal.StringToHGlobalAnsi(i);
                var kind = LLVM.GetEnumAttributeKindForName((sbyte*)namePtr, (nuint)i.Length);
                m_AttributeKindMap.Add(i, kind);
                Marshal.FreeHGlobal(namePtr);
            }
            foreach (var i in m_MetadataNames) {
                var namePtr = Marshal.StringToHGlobalAnsi(i);
                var kind = LLVM.GetMDKindID((sbyte*)namePtr, (uint)i.Length);
                m_MetadataKindMap.Add(i, kind);
                Marshal.FreeHGlobal(namePtr);
            }
        }
        public unsafe static void AddAttributeForFunction(LLVMModuleRef module,LLVMValueRef function,string attributeNames) {
            var atttribute = LLVM.CreateEnumAttribute(module.Context, m_AttributeKindMap[attributeNames], 1);
            LLVM.AddAttributeAtIndex(function, LLVMAttributeIndex.LLVMAttributeFunctionIndex, atttribute);
        }
        public unsafe static void AddMetadataForInst(LLVMValueRef inst, string metadataName, LLVMValueRef[] values) {
            var mdNode = LLVMValueRef.CreateMDNode(values);
            inst.SetMetadata(m_MetadataKindMap[metadataName], mdNode);
        }

        public unsafe static LLVMValueRef GetIntrinsicFunction(LLVMModuleRef module,string name, LLVMTypeRef[] types) {
            var namePtr = Marshal.StringToHGlobalAnsi(name);
            var kind = LLVM.LookupIntrinsicID((sbyte*)namePtr, (nuint)name.Length);

            fixed(LLVMTypeRef *typesPtr = types) {
                return (LLVMValueRef)LLVM.GetIntrinsicDeclaration(module, kind, (LLVMOpaqueType**)typesPtr, (nuint)types.Length);
            }
        }
        public static LLVMValueRef CreateConstU32(uint value) => LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, value);
        public static LLVMValueRef CreateConstU64(ulong value) => LLVMValueRef.CreateConstInt(LLVMTypeRef.Int64, value);
        public static LLVMValueRef CreateConstU32(int value) => LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (uint)value);
        public static LLVMValueRef CreateConstU64(long value) => LLVMValueRef.CreateConstInt(LLVMTypeRef.Int64, (ulong)value);
    
        public static unsafe ulong EvaluateConstIntValue(LLVMValueRef value, LLVMCompiler compiler) {
            var tempFunction = compiler.Module.AddFunction($"eval_func_{Environment.TickCount}", LLVMTypeRef.CreateFunction(value.TypeOf, new LLVMTypeRef[] { }));
            var defaultBlock = tempFunction.AppendBasicBlock("block0");
            var builder = compiler.EvalBuilder;
            builder.PositionAtEnd(defaultBlock);
            builder.BuildRet(value);

            compiler.Evaluator.RecompileAndRelinkFunction(tempFunction);
            var result = compiler.Evaluator.RunFunction(tempFunction, new LLVMGenericValueRef[] { });
            var valueResult = LLVM.GenericValueToInt(result, 0);

            defaultBlock.Delete();
            tempFunction.DeleteFunction();

            return valueResult;
        }
    }
    public enum LLVMTypeBuildPass {
        Construct = 1,
        SolveDependencies = 2,
        SetupTypes = 3,
        GenerateVTable = 4
    }
    public abstract class LLVMTypeInfo {
        protected LLVMCompiler m_Compiler;
        protected TypeInfo m_MetadataType;
        protected string m_TypeName;
        protected LLVMCompType m_InstanceType;
        protected LLVMClassTypeInfo m_BaseType;
        protected HashSet<LLVMInterfaceTypeInfo> m_Interfaces = new HashSet<LLVMInterfaceTypeInfo>();
        protected LLVMTypeBuildPass m_BuildState = LLVMTypeBuildPass.Construct;
       


        protected LLVMTypeRef m_StaticStorageType = LLVMTypeRef.Void;
        protected LLVMValueRef m_StaticStorageBody;
        protected LLVMTypeRef m_VtableType = LLVMTypeRef.Void;
        protected LLVMValueRef m_VtableBody = null;

        public uint TypeHash { get; set; }
        public LLVMTypeRef VtableType => m_VtableType;
        public LLVMValueRef VtableBody => m_VtableBody;
        public LLVMCompType InstanceType => m_InstanceType;
        public TypeInfo MetadataType => m_MetadataType;
        public LLVMValueRef StaticStorage => m_StaticStorageBody;
        public HashSet<LLVMInterfaceTypeInfo> Interfaces => m_Interfaces;
        public LLVMClassTypeInfo BaseType => m_BaseType;
        public abstract ulong DataStorageSize { get; }

        protected LLVMTypeInfo(LLVMCompiler compiler, TypeInfo typeInfo) {
            m_Compiler = compiler;
            m_MetadataType = typeInfo;
            m_TypeName = typeInfo.Entry.ToString();

            m_StaticStorageType = m_Compiler.Context.CreateNamedStruct($"static.{m_TypeName}");
            m_StaticStorageBody = m_Compiler.Module.AddGlobal(m_StaticStorageType, $"static.val.{m_TypeName}");
            m_StaticStorageBody.Initializer = LLVMValueRef.CreateConstNull(m_StaticStorageType);
            m_VtableType = m_Compiler.Context.CreateNamedStruct($"vt.{m_TypeName}");
            if(!typeInfo.Attribute.HasFlag(TypeAttributes.Interface))
                m_VtableBody = m_Compiler.Module.AddGlobal(m_VtableType, $"vt.val.{m_TypeName}");
        }

        public void ProcessDependence() {
            if (m_BuildState >= LLVMTypeBuildPass.SolveDependencies) return;
            ProcessDependenceImpl();
            m_BuildState = LLVMTypeBuildPass.SolveDependencies;
        }
        public LLVMCompType SetupLLVMTypes() {
            if (m_BuildState >= LLVMTypeBuildPass.SetupTypes) return m_InstanceType;
            m_BuildState = LLVMTypeBuildPass.SetupTypes;
            var llvmType = SetupLLVMTypesImpl();
            
            return llvmType;
        }
        public void GenerateVTable() {
            if (m_BuildState >= LLVMTypeBuildPass.GenerateVTable) return;
            GenerateVTableImpl();
            m_BuildState = LLVMTypeBuildPass.GenerateVTable;
        }


        protected virtual void ProcessDependenceImpl() {
            //if (m_MetadataType.Entry.ToString().Contains("ClassB")) Debugger.Break();
            m_BaseType = m_MetadataType.BaseType != null ? (LLVMClassTypeInfo)m_Compiler.ResolveLLVMType(m_MetadataType.BaseType.Entry) : null;
            foreach (var i in m_MetadataType.Interfaces) {
                var interfaceType = (LLVMInterfaceTypeInfo)m_Compiler.ResolveLLVMType(i.Entry);
                interfaceType.ProcessDependence();
                m_Interfaces.Add(interfaceType);
                foreach (var j in interfaceType.m_Interfaces) {
                    if (!m_Interfaces.Contains(j)) m_Interfaces.Add(j);
                }
            }
            if (m_BaseType != null) {
                foreach (var j in m_BaseType.m_Interfaces) {
                    if (!m_Interfaces.Contains(j)) m_Interfaces.Add(j);
                }
            }
            
        }

        protected virtual LLVMCompType SetupLLVMTypesImpl() {
            if (m_MetadataType.Entry.ToString().Contains("App")) Debugger.Break();
            var staticFieldList = m_MetadataType.StaticFields.Values.ToList();
            staticFieldList.Sort((u, v) => {
                return u.FieldIndex.CompareTo(v.FieldIndex);
            });
            var staticFields = staticFieldList.Select(e => m_Compiler.ResolveLLVMInstanceType(e.Type).LLVMType).ToArray();
            m_StaticStorageType.StructSetBody(staticFields, false);
            m_StaticStorageBody.Linkage = LLVMLinkage.LLVMCommonLinkage;

            return default;
        }
        protected abstract void GenerateVTableImpl();
        public abstract int LocateMethodInMainTable(LLVMMethodInfoWrapper method);
        public override string ToString() {
            return m_MetadataType.Entry.ToString();
        }

    }
    public class LLVMInterfaceTerm {
        protected LLVMCompiler m_Compiler;
        protected uint m_InterfaceIndex = 0;
        protected string m_MethodName;
        protected TypeEntry m_ReturnType;
        protected ImmutableArray<TypeEntry> m_ParamTypes = ImmutableArray<TypeEntry>.Empty;
        protected int m_HashCode = 0;
        protected MethodEntry m_MethodEntry;

        public uint InterfaceIndex => m_InterfaceIndex;
        public MethodEntry TemplateEntry => m_MethodEntry;
        public LLVMInterfaceTerm() { }
        public LLVMInterfaceTerm(LLVMCompiler compiler, MethodInstanceInfo template, uint index) {
            m_Compiler = compiler;
            m_InterfaceIndex = index;
            UpdateTermInfo(template, index);

        }
        public LLVMInterfaceTerm Clone(uint index) {
            return new LLVMInterfaceTerm() {
                m_Compiler = m_Compiler,
                m_MethodName = m_MethodName,
                m_ReturnType = m_ReturnType,
                m_ParamTypes = m_ParamTypes,
                m_HashCode = m_HashCode,
                m_InterfaceIndex = index,
                m_MethodEntry = m_MethodEntry
            };
        }
        public void UpdateTermInfo(MethodInstanceInfo template, uint index) {
            m_MethodName = template.Entry.Name;
            m_ReturnType = template.Signature.ReturnType;
            m_ParamTypes = template.Signature.ParameterTypes;
            var hashCode = m_MethodName.GetHashCode() ^ m_ReturnType.GetHashCode();
            foreach (var i in m_ParamTypes) hashCode ^= i.GetHashCode();
            m_HashCode = hashCode;
            m_InterfaceIndex = index;
            m_MethodEntry = template.Entry;
        }

        public override bool Equals(object obj) {
            if (obj is LLVMInterfaceTerm method) {
                if (method.m_MethodName != m_MethodName) return false;
                if (method.m_ParamTypes.SequenceEqual(m_ParamTypes) && method.m_ReturnType == m_ReturnType) {
                    return true;
                }
            }
            return base.Equals(obj);
        }
        public override int GetHashCode() {
            return m_HashCode;
        }

    }

    public class LLVMInterfaceTypeInfo : LLVMTypeInfo {

        protected LLVMTypeRef m_InterfaceType = LLVMTypeRef.Void;
        protected HashSet<LLVMInterfaceTerm> m_InterfaceTerms = new HashSet<LLVMInterfaceTerm>();
        public HashSet<LLVMInterfaceTerm> InterfaceTerms => m_InterfaceTerms;
        public LLVMTypeRef InterfaceType => m_InterfaceType;
        public override ulong DataStorageSize => 8; // fix: other target

        public LLVMInterfaceTypeInfo(LLVMCompiler compiler, TypeInfo typeInfo)
            : base(compiler, typeInfo) {

        }

        protected override void ProcessDependenceImpl() {
            base.ProcessDependenceImpl();
            var index = 0u;
            foreach (var i in m_MetadataType.Methods) {
                m_InterfaceTerms.Add(new LLVMInterfaceTerm(m_Compiler, i.Value, index++));
            }
            foreach (var i in m_Interfaces) {
                foreach (var j in i.m_InterfaceTerms) {
                    if (!m_InterfaceTerms.Contains(j)) m_InterfaceTerms.Add(j.Clone(index++));
                }
            }
        }

        protected override void GenerateVTableImpl() {
            var interfaceTerms = m_InterfaceTerms.ToList();
            var vtableTypes = new List<LLVMTypeRef>();
            interfaceTerms.Sort((a, b) => a.InterfaceIndex.CompareTo(b.InterfaceIndex));

            vtableTypes.Add(m_Compiler.InterfaceHeaderType); // interface VTable header
            vtableTypes.AddRange(interfaceTerms.Select((e) => LLVMTypeRef.CreatePointer(m_Compiler.ResolveLLVMMethod(e.TemplateEntry).FunctionType,0)));
            m_VtableType.StructSetBody(vtableTypes.ToArray(), false);
        }

        protected override LLVMCompType SetupLLVMTypesImpl() {
            base.SetupLLVMTypesImpl();

            m_InterfaceType = m_Compiler.Context.CreateNamedStruct($"ref.{m_MetadataType.FullName}");
            m_InterfaceType.StructSetBody(new LLVMTypeRef[] { },false);
            
            m_InstanceType = LLVMCompType.CreateType(LLVMTypeTag.Pointer | LLVMTypeTag.Interface,LLVMTypeRef.CreatePointer(m_InterfaceType, 0));
            return m_InstanceType;
        }
        public override int LocateMethodInMainTable(LLVMMethodInfoWrapper method) {
            var key = new LLVMInterfaceTerm(m_Compiler, method.Method, 0);
            if (!m_InterfaceTerms.TryGetValue(key, out var term)) return -1;
            return (int)term.InterfaceIndex;
        }
        public int LocateMethodInMainTableStrict(LLVMMethodInfoWrapper method, LLVMInterfaceTypeInfo fromInterface) {
            var key = new LLVMInterfaceTerm(m_Compiler, method.Method, 0);
            if (!m_InterfaceTerms.TryGetValue(key, out var term)) return -1;
            if (m_Compiler.ResolveLLVMMethod(term.TemplateEntry).DeclType != fromInterface) return -1;
            return (int)term.InterfaceIndex;
        }

    }

    public struct LLVMInterfaceHeader {
        public static ulong GetHeaderValue(ushort interfaceOffset, ushort interfaceLength, uint interfaceHash) {
            return (((ulong)interfaceHash) << 32) | (((ulong)interfaceLength) << 16) | interfaceOffset;
        }
    }

    public class LLVMClassTypeInfo : LLVMTypeInfo {
        protected static Dictionary<string, Func<LLVMCompiler,LLVMCompType>> s_PrimitiveTypesMap = new Dictionary<string, Func<LLVMCompiler, LLVMCompType>>() {
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

        protected MethodInstanceInfo[] m_MainTableMethods = null;

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
        public LLVMClassTypeInfo(LLVMCompiler compiler, TypeInfo typeInfo)
            : base(compiler, typeInfo) {
            
            m_DataStorageType = m_Compiler.Context.CreateNamedStruct($"data.{m_TypeName}");
            m_HeapPtrType = LLVMCompType.CreateType(LLVMTypeTag.Class, LLVMTypeRef.CreatePointer(m_DataStorageType, 0));

            var refType = m_Compiler.Context.CreateNamedStruct($"ref.{m_TypeName}");
            refType.StructSetBody(new LLVMTypeRef[] {
                LLVMTypeRef.CreatePointer(m_VtableType,0),
                m_DataStorageType
            }, false);
            m_ReferenceType = LLVMCompType.CreateType(LLVMTypeTag.Class, LLVMTypeRef.CreatePointer(refType,0));

            if (s_PrimitiveTypesMap.ContainsKey(m_TypeName)) {
                m_InstanceType = s_PrimitiveTypesMap[m_TypeName](compiler);
                return;
            } 
            if(typeInfo.BaseType != null && typeInfo.BaseType.Entry == m_Compiler.TypeEnvironment.IntrinicsTypes["System::Enum"]) {
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

            var layout = m_MetadataType.Definition.GetLayout();

            foreach (var i in m_MetadataType.Fields) {
                var llvmType = m_Compiler.ResolveDepLLVMType(i.Value.Type);
                llvmType.SetupLLVMTypes();
            }

            // explicit layout
            if (m_MetadataType.Definition.Attributes.HasFlag(TypeAttributes.ExplicitLayout)) {
                throw new NotImplementedException();
                var structSize = 0ul;
                foreach(var i in m_MetadataType.Fields) {
                    //var llvmType = i.Value.Type is PointerTypeEntry ? 8: m_Compiler.ResolveLLVMInstanceType(i.Value.Type);
                    //structSize = Math.Max(structSize, (uint)i.Value.Definition.GetOffset() + llvmType.DataStorageSize);
                }
                structSize = Math.Max(structSize, (ulong)layout.Size);

                m_DataStorageSize = structSize;
                m_DataStorageType.StructSetBody(new LLVMTypeRef[] { LLVMTypeRef.CreateArray(LLVMTypeRef.Int8, (uint)structSize) },false);

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
                if(realSize >= neturalSize) {
                    instanceFields.Add(LLVMTypeRef.CreateArray(LLVMTypeRef.Int8, (uint)(realSize - neturalSize)));
                    m_DataStorageType.StructSetBody(instanceFields.ToArray(), false);
                    m_DataStorageSize = realSize;
                }
            }

            return m_InstanceType;
        }


        protected void FillInterface(LLVMInterfaceTypeInfo interfaceType, LLVMValueRef[] interfaceValues,ref int unknownTerms) {
            foreach (var i in m_MetadataType.Methods) {
                var llvmMethod = m_Compiler.ResolveLLVMMethod(i.Key);
                var index = interfaceType.LocateMethodInMainTable(llvmMethod);
                if (index >= 0 && interfaceValues[index + 1] == default) {
                    interfaceValues[index + 1] = llvmMethod.Function;
                    unknownTerms--;
                }
            }
            
            if (unknownTerms != 0 && m_BaseType != null) m_BaseType.FillInterface(interfaceType, interfaceValues,ref unknownTerms);
        }
        protected LLVMValueRef GenerateMainVTable() {
            //if (m_MetadataType.Entry.ToString().Contains("ClassB")) Debugger.Break();
            var metadataType = m_MetadataType;
            var mainVTTypes = new List<LLVMTypeRef>();
            var mainVTValues = new List<LLVMValueRef>();
            var mainVTMethods = new List<MethodInstanceInfo>();

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

            

            var baseMainTableMethods = m_BaseType != null ? m_BaseType.m_MainTableMethods : Array.Empty<MethodInstanceInfo>();

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
                LLVMHelpers.CreateConstU32(((uint)m_MetadataType.Attribute)),
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
                interfaceLookupTable[lookupIndex*2] = LLVMHelpers.CreateConstU32(i.TypeHash);
                var nullPtr = LLVMValueRef.CreateConstNull(LLVMTypeRef.CreatePointer(m_VtableType,0));
                interfaceLookupTable[lookupIndex*2 + 1] = LLVMValueRef.CreateConstPtrToInt(
                    LLVMValueRef.CreateConstGEP(nullPtr, 
                    new LLVMValueRef[] { 
                        LLVMHelpers.CreateConstU32(0),
                        LLVMHelpers.CreateConstU32(lookupIndex+2)
                    }),LLVMTypeRef.Int32);
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
                FillInterface(i, interfaceValue,ref unkTerms);

                var reader = m_MetadataType.Reader;
                foreach (var j in m_MetadataType.Definition.GetMethodImplementations()) {
                    var methodImpl = reader.GetMethodImplementation(j);

                    var currentType = m_Compiler.TypeEnvironment.ResolveTypeByHandle(methodImpl.Type, reader, m_MetadataType);
                    var interfaceDecl = m_Compiler.TypeEnvironment.ResolveMethodByHandle(methodImpl.MethodDeclaration, reader, m_MetadataType, out var interfaceEntry,out _);
                    var implBody = m_Compiler.TypeEnvironment.ResolveMethodByHandle(methodImpl.MethodBody, reader, m_MetadataType, out var implClass,out _);

                    var implMethod = m_Compiler.ResolveLLVMMethod(implBody);
                    var declMethod = m_Compiler.ResolveLLVMMethod(interfaceDecl);
                    var interfaceType = (LLVMInterfaceTypeInfo)m_Compiler.ResolveLLVMType(interfaceEntry);
                    if(interfaceType == i || i.Interfaces.Contains(interfaceType)) {
                        var vtableIndex = i.LocateMethodInMainTableStrict(declMethod, interfaceType);
                        if (vtableIndex >= 0) {
                            if (interfaceValue[vtableIndex + 1] == default) unkTerms--;
                            interfaceValue[vtableIndex+1] = implMethod.Function;
                        }
                    }
                }
                if (unkTerms != 0) throw new Exception($"Incomplete vtable for interface {i}");
                var types = i.VtableType.StructElementTypes;
                for (var k=0;k< interfaceValue.Length; k++) {
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
    public static class IRHelper {
        public static LLVMValueRef m_ObjectDataStorageMask = LLVMHelpers.CreateConstU64(0xFFFFFFFFFF);
        public unsafe static LLVMValueRef GetUndef(LLVMTypeRef type) {
            return (LLVMValueRef)LLVM.GetUndef(type);
        }
        public static LLVMValueRef AllocObjectDefault(LLVMCompiler compiler,LLVMClassTypeInfo declType,LLVMBuilderRef builder) {
            var runtimeHelpers = compiler.ResolveLLVMType(compiler.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::RuntimeHelpers"]);
            var gcHeapAlloc = compiler.ResolveLLVMMethod(runtimeHelpers.MetadataType.FindMethodByName("GCHeapAlloc").Entry);

            var dataStorAddr = builder.BuildCall2(gcHeapAlloc.FunctionType,gcHeapAlloc.Function, new LLVMValueRef[] {
                            declType.ReferenceType.LLVMType.ElementType.SizeOf
                        });
            var objectBody = builder.BuildBitCast(dataStorAddr, ((LLVMClassTypeInfo)declType).ReferenceType.LLVMType);
            var vtblPtr = builder.BuildGEP(objectBody, new LLVMValueRef[] {
                            LLVMHelpers.CreateConstU32(0),
                            LLVMHelpers.CreateConstU32(0)
                        });
            builder.BuildStore(declType.VtableBody, vtblPtr);

            var pthis = builder.BuildGEP(objectBody, new LLVMValueRef[] {
                            LLVMHelpers.CreateConstU32(0),
                            LLVMHelpers.CreateConstU32(1)
                        });
            return pthis;
        }
        public static void MakeCall(LLVMMethodInfoWrapper targetMethod, LLVMValueRef targetFunction, LLVMValueRef[] argumentList,LLVMBuilderRef builder,Stack<LLVMCompValue> evalStack) {
            var returnValue = builder.BuildCall2(targetFunction.TypeOf.ElementType,targetFunction, argumentList);

            if (targetMethod.ReturnType.LLVMType != LLVMTypeRef.Void)
                evalStack.Push(LLVMCompValue.CreateValue(returnValue, targetMethod.ReturnType));
        }
        public static LLVMValueRef GetInstanceFieldAddress(LLVMCompValue obj, uint fieldToken, LLVMMethodInfoWrapper context, LLVMBuilderRef builder, out LLVMCompType fieldType) {
            var unkToken = MetadataHelper.CreateHandle(fieldToken);
            var typeEnv = context.Context.TypeEnvironment;
            var methodDef = context.Method.Definition;
            var reader = MetadataHelper.GetMetadataReader(ref methodDef);
            TypeInfo typeInfo = null;
            FieldInfo fieldInfo = null;

            if (unkToken.Kind == HandleKind.FieldDefinition) {
                var fieldDef = reader.GetFieldDefinition((FieldDefinitionHandle)unkToken);
                var declType = reader.GetTypeDefinition(fieldDef.GetDeclaringType());
                var typeEntry = typeEnv.TypeEntryFactory.CreateTypeEntry(declType);
                typeInfo = typeEnv.ActiveTypes[typeEntry];
                fieldInfo = typeInfo.Fields[reader.GetString(fieldDef.Name)];
            } else {
                var memberRef = reader.GetMemberReference((MemberReferenceHandle)unkToken); ;
                var name = reader.GetString(memberRef.Name);
                if (memberRef.Parent.Kind == HandleKind.TypeReference) {
                    var parent = reader.GetTypeReference((TypeReferenceHandle)memberRef.Parent);
                    var fullName = typeEnv.SignatureDecoder.GetTypeReferenceFullName(reader, parent);
                    var declTypeDef = typeEnv.ResolveTypeByPrototypeName(fullName);
                    var declTypeEntry = typeEnv.TypeEntryFactory.CreateTypeEntry(declTypeDef);
                    typeInfo = typeEnv.ActiveTypes[declTypeEntry];
                    fieldInfo = typeInfo.Fields[(name)];
                } else {
                    var typeSpec = reader.GetTypeSpecification((TypeSpecificationHandle)memberRef.Parent);
                    var genericTypes = typeSpec.DecodeSignature(typeEnv.SignatureDecoder, context.Method);
                    typeInfo = typeEnv.ActiveTypes[genericTypes];
                    fieldInfo = typeInfo.Fields[(name)];
                }
            }
            var llvmType = (LLVMClassTypeInfo)context.Context.ResolveLLVMType(typeInfo.Entry);
            var dataStorPtrType = LLVMTypeRef.CreatePointer(llvmType.DataStorageType, 0);

            var objDataAddr = builder.BuildBitCast(obj.Value, dataStorPtrType);
            
            fieldType = context.Context.ResolveLLVMInstanceType(fieldInfo.Type);
            var fieldIndex = fieldInfo.FieldIndex;
            if (llvmType.BaseType != null) fieldIndex++; // skip base storage

            var fieldPtr = builder.BuildGEP(objDataAddr, new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0), LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, fieldIndex) });
            return fieldPtr;
        }
        public static LLVMTypeInfo GetTypeInfo(uint token, LLVMMethodInfoWrapper context, LLVMBuilderRef builder) {
            var unkHandle = MetadataHelper.CreateHandle(token);
            var typeEnv = context.Context.TypeEnvironment;
            var factory = typeEnv.TypeEntryFactory;
            var reader = context.Method.Reader;
            switch (unkHandle.Kind) {
                case HandleKind.TypeDefinition: {
                    var typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)unkHandle);
                    var typeEntry = factory.CreateTypeEntry(typeDef);
                    var llvmType = context.Context.ResolveLLVMType(typeEntry);
                    return llvmType;
                }
                case HandleKind.TypeSpecification: {
                    var typeSepc = reader.GetTypeSpecification((TypeSpecificationHandle)unkHandle);
                    var paramType = typeSepc.DecodeSignature(context.Context.TypeEnvironment.SignatureDecoder, context.Method);
                    var llvmType = context.Context.ResolveLLVMType(paramType);
                    return llvmType;
                }
                case HandleKind.TypeReference: {
                    var parent = reader.GetTypeReference((TypeReferenceHandle)unkHandle);
                    var fullName = typeEnv.SignatureDecoder.GetTypeReferenceFullName(reader, parent);
                    var declTypeDef = typeEnv.ResolveTypeByPrototypeName(fullName);
                    var declTypeEntry = typeEnv.TypeEntryFactory.CreateTypeEntry(declTypeDef);
                    var llvmType = context.Context.ResolveLLVMType(declTypeEntry);
                    return llvmType;
                }
                default: {
                    throw new NotImplementedException();
                }
            }
        }

        public static LLVMValueRef LookupVtable(LLVMValueRef pthis, int index, LLVMTypeRef typeVTable, LLVMBuilderRef builder) {
            var vtablePtrType = LLVMTypeRef.CreatePointer(typeVTable, 0);
            var objectHeaderPtrType = LLVMTypeRef.CreatePointer(vtablePtrType, 0);
            var instancePtr = builder.BuildBitCast(pthis, objectHeaderPtrType);
            var pVtbl = builder.BuildGEP(instancePtr, new LLVMValueRef[] {
                LLVMHelpers.CreateConstU32(-1)
            });
            var pTypedVtbl = builder.BuildLoad(pVtbl);
            var funcPtr = builder.BuildGEP(pTypedVtbl,
                new LLVMValueRef[] { 
                    LLVMHelpers.CreateConstU32(0),LLVMHelpers.CreateConstU32(index)
                });
            var vptr= builder.BuildLoad(funcPtr);
            //LLVMHelpers.AddMetadataForInst(vptr, "invariant.load", builder);
            return vptr;
        }
        public static LLVMMethodInfoWrapper GetMethodInfo(uint token, LLVMMethodInfoWrapper context, LLVMBuilderRef builder, out LLVMTypeInfo llvmType, out MethodSignature<TypeEntry> callSiteSig) {
            var methodToken = MetadataHelper.CreateHandle(token);
            var factory = context.Context.TypeEnvironment.TypeEntryFactory;
            var method = context.Method;

            switch (methodToken.Kind) {
                case HandleKind.MethodDefinition: {
                    var methodDef = method.Reader.GetMethodDefinition((MethodDefinitionHandle)methodToken);
                    var declType = method.Reader.GetTypeDefinition(methodDef.GetDeclaringType());
                    var typeEntry = factory.CreateTypeEntry(declType);
                    llvmType = context.Context.ResolveLLVMType(typeEntry);

                    var methodEntry = factory.CreateMethodEntry(typeEntry, methodDef, System.Collections.Immutable.ImmutableArray<TypeEntry>.Empty);
                    var llvmMethod = context.Context.ResolveLLVMMethod(methodEntry);
                    callSiteSig = llvmMethod.Method.Signature;
                    return llvmMethod ;
                }
                case HandleKind.MemberReference: {
                    var methodDef = context.Context.TypeEnvironment.ResolveMethodDefFromMemberRef(methodToken, method.Reader, method, out var declType,out callSiteSig);
                    var methodEntry = factory.CreateMethodEntry(declType, methodDef, System.Collections.Immutable.ImmutableArray<TypeEntry>.Empty);
                    llvmType = context.Context.ResolveLLVMType(declType);
                    return context.Context.ResolveLLVMMethod(methodEntry);
                }
                case HandleKind.MethodSpecification: {
                    var methodDef = context.Context.TypeEnvironment.ResolveMethodDefFromMethodSpec((MethodSpecificationHandle)methodToken, method.Reader, method, out var declType, out var specTypes);
                    var methodEntry = context.Context.TypeEnvironment.TypeEntryFactory.CreateMethodEntry(declType, methodDef, specTypes);
                    llvmType = context.Context.ResolveLLVMType(declType);
                    var llvmMethod = context.Context.ResolveLLVMMethod(methodEntry);
                    callSiteSig = llvmMethod.Method.Signature;
                    return llvmMethod;
                }
            }
            throw new InvalidProgramException();
        }
        public static LLVMValueRef GetStaticFieldAddress(uint operand, LLVMMethodInfoWrapper context, LLVMBuilderRef builder, out LLVMCompType fieldType) {
            var typeEnv = context.Context.TypeEnvironment;
            var fieldToken = MetadataHelper.CreateHandle(operand);
            var methodDef = context.Method.Definition;
            var reader = MetadataHelper.GetMetadataReader(ref methodDef);
            FieldInfo fieldInfo = null;
            TypeEntry typeEntry = default;
            if (fieldToken.Kind == HandleKind.FieldDefinition) {
                var fieldDef = reader.GetFieldDefinition((FieldDefinitionHandle)fieldToken);
                var declType = reader.GetTypeDefinition(fieldDef.GetDeclaringType());
                typeEntry = typeEnv.TypeEntryFactory.CreateTypeEntry(declType);
                var typeInfo = typeEnv.ActiveTypes[typeEntry];
                fieldInfo = typeInfo.StaticFields[reader.GetString(fieldDef.Name)];
            } else {
                var memberRef = reader.GetMemberReference((MemberReferenceHandle)fieldToken); ;
                var name = reader.GetString(memberRef.Name);
                var parent = reader.GetTypeReference((TypeReferenceHandle)memberRef.Parent);
                var fullName = typeEnv.SignatureDecoder.GetTypeReferenceFullName(reader, parent);
                var declTypeDef = typeEnv.ResolveTypeByPrototypeName(fullName);
                typeEntry = typeEnv.TypeEntryFactory.CreateTypeEntry(declTypeDef);
                var typeInfo = typeEnv.ActiveTypes[typeEntry];
                fieldInfo = typeInfo.StaticFields[(name)];
            }

            fieldType = context.Context.ResolveLLVMInstanceType(fieldInfo.Type);

            var staticClass = context.Context.ResolveLLVMType(typeEntry);

            var staticStorage = staticClass.StaticStorage;
            return builder.BuildGEP(staticStorage, new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0), LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, fieldInfo.FieldIndex) }); 
        }
    }
    public enum LLVMTypeTag {
        Unsigned = 0,
        Signed = 1,
        Integer = 2,
        Real = 4,
        Pointer = 8,
        Struct = 16,


        UnsignedInt = 2,
        SignedInt = 3,
        Interface = 32,
        StackAlloc = 64,
        ConstObj = 128,
        Class = 256,
        FP64 = 512
    }
    public struct LLVMCompType {
        public LLVMTypeTag TypeTag;
        public LLVMTypeRef LLVMType;
        public static LLVMCompType Int1 = CreateType(LLVMTypeTag.UnsignedInt, LLVMTypeRef.Int1);
        public static LLVMCompType Int8 = CreateType(LLVMTypeTag.SignedInt, LLVMTypeRef.Int8);
        public static LLVMCompType Int16 = CreateType(LLVMTypeTag.SignedInt, LLVMTypeRef.Int16);
        public static LLVMCompType Int32 = CreateType(LLVMTypeTag.SignedInt, LLVMTypeRef.Int32);
        public static LLVMCompType Int64 = CreateType(LLVMTypeTag.SignedInt, LLVMTypeRef.Int64);
        public static LLVMCompType UInt8 = CreateType(LLVMTypeTag.UnsignedInt, LLVMTypeRef.Int8);
        public static LLVMCompType UInt16 = CreateType(LLVMTypeTag.UnsignedInt, LLVMTypeRef.Int16);
        public static LLVMCompType UInt32 = CreateType(LLVMTypeTag.UnsignedInt, LLVMTypeRef.Int32);
        public static LLVMCompType UInt64 = CreateType(LLVMTypeTag.UnsignedInt, LLVMTypeRef.Int64);
        public static LLVMCompType IntPtr = CreateType(LLVMTypeTag.Pointer, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8,0));
        public static LLVMCompType Float64 = CreateType(LLVMTypeTag.Real | LLVMTypeTag.FP64, LLVMTypeRef.Double);
        public static LLVMCompType Float32 = CreateType(LLVMTypeTag.Real, LLVMTypeRef.Float);
        public static LLVMCompType CreateType(LLVMTypeTag tag, LLVMTypeRef type) {
            LLVMCompType result = default;
            result.LLVMType = type;
            result.TypeTag = tag;
            return result;
        }
        public LLVMCompType ToPointerType() {
            return CreateType(LLVMTypeTag.Pointer, LLVMTypeRef.CreatePointer(LLVMType, 0));
        }
        public LLVMCompType ToDerefType() {
            return CreateType(TypeTag, LLVMType.ElementType);
        }
        public LLVMCompType WithTag(LLVMTypeTag tag) {
            this.TypeTag |= tag;
            return this;
        }
        public override string ToString() {
            return $"[{LLVMType}]({TypeTag})";
        }
    }
    public struct LLVMCompValue {
        public LLVMValueRef Value { get; set; }
        public LLVMCompType Type { get; set; }
        public static LLVMCompValue CreateValue(LLVMValueRef value, LLVMCompType type) {
            LLVMCompValue result = default;
            result.Value = value;
            result.Type = type;
            return result;
        }
        public static LLVMCompValue CreateValue(LLVMValueRef value, LLVMTypeTag tag) {
            LLVMCompValue result = default;
            result.Value = value;
            result.Type = LLVMCompType.CreateType(tag,value.TypeOf);
            return result;
        }
        public static LLVMCompValue CreateConstI32(uint value) {
            return CreateValue(LLVMHelpers.CreateConstU32(value), LLVMCompType.Int32);
        }
        public static LLVMCompValue CreateConstI32(int value) {
            return CreateValue(LLVMHelpers.CreateConstU32(value), LLVMCompType.Int32);
        }
        public static LLVMCompValue CreateConstI64(ulong value) {
            return CreateValue(LLVMHelpers.CreateConstU64(value), LLVMCompType.Int64);
        }
        public static LLVMCompValue CreateConstI64(long value) {
            return CreateValue(LLVMHelpers.CreateConstU64(value), LLVMCompType.Int64);
        }
        public unsafe LLVMCompValue TryCastComparable(LLVMBuilderRef builder) {
            switch (Type.LLVMType.Kind) {
                case LLVMTypeKind.LLVMIntegerTypeKind:
                case LLVMTypeKind.LLVMFloatTypeKind:
                case LLVMTypeKind.LLVMDoubleTypeKind: {
                    return this;
                }
                case LLVMTypeKind.LLVMPointerTypeKind: {
                    var ptrType = LLVMCompType.Int8.ToPointerType();
                    return CreateValue(builder.BuildBitCast(Value, ptrType.LLVMType), ptrType);
                }
                case LLVMTypeKind.LLVMStructTypeKind: {
                    throw new NotImplementedException();
                }
            }
            throw new NotImplementedException();
        }
        public unsafe LLVMCompValue TryCastCond(LLVMBuilderRef builder) {
            switch (Type.LLVMType.Kind) {
                case LLVMTypeKind.LLVMPointerTypeKind:
                case LLVMTypeKind.LLVMIntegerTypeKind: {
                    var result = builder.BuildICmp(LLVMIntPredicate.LLVMIntNE, Value, LLVMValueRef.CreateConstNull(Type.LLVMType));
                    return CreateValue(result, LLVMCompType.Int1);
                }
                case LLVMTypeKind.LLVMFloatTypeKind:
                case LLVMTypeKind.LLVMDoubleTypeKind: {
                    var result = builder.BuildFCmp(LLVMRealPredicate.LLVMRealONE, Value, LLVMValueRef.CreateConstNull(Type.LLVMType));
                    return CreateValue(result, LLVMCompType.Int1);
                }
                case LLVMTypeKind.LLVMStructTypeKind: {
                    throw new NotImplementedException();
                }
            }
            throw new NotImplementedException();
        }
        public unsafe LLVMCompValue TryCast(LLVMCompType dstType,LLVMBuilderRef builder, LLVMCompiler compiler = null) {
            var value = Value;
            var tag = Type.TypeTag;
            switch (dstType.LLVMType.Kind) {
                case LLVMTypeKind.LLVMIntegerTypeKind: {
                    if (value.TypeOf.Kind.HasFlag(LLVMTypeKind.LLVMPointerTypeKind)) {
                        value = builder.BuildPtrToInt(value, LLVMCompType.Int64.LLVMType);
                        tag = LLVMTypeTag.SignedInt;
                    }
                    if (value.TypeOf.Kind.HasFlag(LLVMTypeKind.LLVMFloatTypeKind)) {
                        value = dstType.TypeTag.HasFlag(LLVMTypeTag.Signed) ? builder.BuildFPToSI(value, dstType.LLVMType) : builder.BuildFPToUI(value, dstType.LLVMType);
                        tag = dstType.TypeTag;
                    }
                    if (value.TypeOf.Kind.HasFlag(LLVMTypeKind.LLVMIntegerTypeKind)) {
                        if (value.TypeOf.IntWidth == dstType.LLVMType.IntWidth) return CreateValue(value, dstType);
                        if(value.TypeOf.IntWidth < dstType.LLVMType.IntWidth) {
                            return CreateValue(tag.HasFlag(LLVMTypeTag.Signed) ? builder.BuildSExt(value, dstType.LLVMType): builder.BuildZExt(value, dstType.LLVMType), dstType);
                        } else {
                            return CreateValue(builder.BuildTrunc(value, dstType.LLVMType), dstType);
                        }
                    }
                    throw new InvalidCastException();
                }
                case LLVMTypeKind.LLVMFloatTypeKind: {
                    throw new NotImplementedException();
                }
                case LLVMTypeKind.LLVMDoubleTypeKind: {
                    if (value.TypeOf.Kind.HasFlag(LLVMTypeKind.LLVMDoubleTypeKind)) return this;
                    if (value.TypeOf.Kind.HasFlag(LLVMTypeKind.LLVMFloatTypeKind)) {
                        return CreateValue(builder.BuildFPExt(value,LLVMTypeRef.Double), dstType);
                    }
                    if (value.TypeOf.Kind.HasFlag(LLVMTypeKind.LLVMIntegerTypeKind)) {
                        if(tag.HasFlag(LLVMTypeTag.Signed))
                            return CreateValue(builder.BuildSIToFP(value, LLVMTypeRef.Double), dstType);
                        return CreateValue(builder.BuildUIToFP(value, LLVMTypeRef.Double), dstType);
                    }
                    throw new NotImplementedException();
                   
                }
                case LLVMTypeKind.LLVMPointerTypeKind: {
                    if (value.TypeOf.Kind.HasFlag(LLVMTypeKind.LLVMPointerTypeKind)) {
                        if(Type.TypeTag.HasFlag(LLVMTypeTag.Interface) && dstType.TypeTag.HasFlag(LLVMTypeTag.Class)) {
                            // interface to class
                            var ptrmaskFunc = LLVMHelpers.GetIntrinsicFunction(compiler.Module, "llvm.ptrmask", new LLVMTypeRef[] { 
                                Type.LLVMType, LLVMTypeRef.Int64
                            });
                            value = builder.BuildCall2(ptrmaskFunc.TypeOf.ElementType,ptrmaskFunc, new LLVMValueRef[] { 
                                value,
                                LLVMHelpers.CreateConstU64(0xFFFFFFFFFFFF)
                            });
                        }
                        if(Type.TypeTag.HasFlag(LLVMTypeTag.Class) && dstType.TypeTag.HasFlag(LLVMTypeTag.Interface)) {
                            // class to interface
                            var runtimeHelpers = compiler.ResolveLLVMType(compiler.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::RuntimeHelpers"]);
                            var castToInterface = compiler.ResolveLLVMMethod(runtimeHelpers.MetadataType.FindMethodByName("CastToInterface").Entry);
                            var interfaceType = compiler.ResolveLLVMTypeFromTypeRef(dstType);
                            value = builder.BuildCall2(castToInterface.FunctionType,castToInterface.Function, new LLVMValueRef[] {
                                builder.BuildBitCast(value,castToInterface.ParamTypes[0].LLVMType),
                                LLVMHelpers.CreateConstU32(interfaceType.TypeHash)
                            });
                        }
                        value = builder.BuildBitCast(value, dstType.LLVMType);
                        return CreateValue(value, dstType);
                    }
                    if (value.TypeOf.Kind.HasFlag(LLVMTypeKind.LLVMIntegerTypeKind)) {
                        value = builder.BuildIntToPtr(value, dstType.LLVMType);
                        return CreateValue(value, dstType);
                    }
                    break;
                }
                case LLVMTypeKind.LLVMStructTypeKind: {
                    if (dstType.LLVMType == Type.LLVMType) return this;
                    throw new NotImplementedException();
                    //break;
                }
            }
            throw new NotImplementedException();
        }
    }
    public class IRGenerator {
        protected LLVMBuilderRef m_Builder;
        public LLVMBuilderRef Builder => m_Builder;
        public IRGenerator(LLVMBuilderRef builder) {
            m_Builder = builder;
        }
        public unsafe Stack<LLVMCompValue> GenerateForBasicBlock(MethodBasicBlock basicBlock, LLVMMethodInfoWrapper context, Stack<LLVMCompValue> evalStack) {

            var method = basicBlock.Method;
            var llvmBasicBlock = context.GetLLVMBasicBlock(basicBlock);
            var callvirtTypeHint = (LLVMTypeInfo)null;
            m_Builder.PositionAtEnd(llvmBasicBlock);

            //if (method.Entry.ToString().Contains("MainX")) Debugger.Break();
            method.ForEachIL(basicBlock.Interval, (opcode, operand) => {
                switch (opcode) {
                    case ILOpCode.Nop: break;
                    case ILOpCode.Ldarg_0:
                    case ILOpCode.Ldarg_1:
                    case ILOpCode.Ldarg_2:
                    case ILOpCode.Ldarg_3:
                    case ILOpCode.Ldarg_s: {
                        var index = opcode == ILOpCode.Ldarg_s ? ((int)operand) : (opcode - ILOpCode.Ldarg_0);
                        var paramValue = context.ParamValueRef[index];
                        evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildLoad(paramValue.Value), paramValue.Type.TypeTag));
                        break;
                    }
                    case ILOpCode.Starg:
                    case ILOpCode.Starg_s: {
                        var argValue = context.ParamValueRef[(int)operand];
                        var value = evalStack.Pop().TryCast(argValue.Type.ToDerefType(), m_Builder);
                        m_Builder.BuildStore(value.Value, argValue.Value);
                        break;
                    }
                    case ILOpCode.Stloc_0:
                    case ILOpCode.Stloc_1:
                    case ILOpCode.Stloc_2:
                    case ILOpCode.Stloc_3:
                    case ILOpCode.Stloc_s: {
                        var index = opcode == ILOpCode.Stloc_s ? ((int)operand) : (opcode - ILOpCode.Stloc_0);
                        
                        var localVarValue = context.LocalValueRef[index];
                        var value = evalStack.Pop().TryCast(localVarValue.Type.ToDerefType(),m_Builder);

                        m_Builder.BuildStore(value.Value, localVarValue.Value);
                        break;
                    }
                    case ILOpCode.Ldloc_0:
                    case ILOpCode.Ldloc_1:
                    case ILOpCode.Ldloc_2:
                    case ILOpCode.Ldloc_3:
                    case ILOpCode.Ldloc_s: {
                        var index = opcode == ILOpCode.Ldloc_s ? ((int)operand) : (opcode - ILOpCode.Ldloc_0);
                        var localVarValue = context.LocalValueRef[index];
                        evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildLoad(localVarValue.Value), localVarValue.Type.ToDerefType()));

                        break;
                    }
                    case ILOpCode.Ldc_i4: {
                        evalStack.Push(LLVMCompValue.CreateConstI32((uint)operand));
                        break;
                    }
                    case ILOpCode.Ldc_i4_s: {
                        evalStack.Push(LLVMCompValue.CreateConstI32((int)(sbyte)operand));
                        break;
                    }
                    case ILOpCode.Ldc_i4_0:
                    case ILOpCode.Ldc_i4_1:
                    case ILOpCode.Ldc_i4_2:
                    case ILOpCode.Ldc_i4_3:
                    case ILOpCode.Ldc_i4_4:
                    case ILOpCode.Ldc_i4_5:
                    case ILOpCode.Ldc_i4_6:
                    case ILOpCode.Ldc_i4_7:
                    case ILOpCode.Ldc_i4_8: {
                        evalStack.Push(LLVMCompValue.CreateConstI32(opcode - ILOpCode.Ldc_i4_0));
                        break;
                    }
                    case ILOpCode.Add: {

                        var value2 = evalStack.Pop();
                        var value1 = evalStack.Pop();

                        if(value1.Type.TypeTag.HasFlag(LLVMTypeTag.Pointer) || value2.Type.TypeTag.HasFlag(LLVMTypeTag.Pointer)) {
                            var ptrValue = value1.Type.TypeTag.HasFlag(LLVMTypeTag.Pointer) ? value1: value2;
                            var intValue = value1.Type.TypeTag.HasFlag(LLVMTypeTag.Pointer) ? value2 : value1;
                            var resultInt = m_Builder.BuildAdd(intValue.TryCast(LLVMCompType.Int64, m_Builder).Value, m_Builder.BuildPtrToInt(ptrValue.Value, LLVMTypeRef.Int64));
                            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildIntToPtr(resultInt, ptrValue.Type.LLVMType), ptrValue.Type.TypeTag));
                        } else {
                            value2 = value2.TryCast(value1.Type, m_Builder);
                            if (value1.Type.TypeTag.HasFlag(LLVMTypeTag.Real)) {
                                evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildFAdd(value1.Value, value2.Value), value1.Type));
                            } else {
                                evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildAdd(value1.Value, value2.Value), value1.Type));
                            }
                        }

                        break;
                    }
                    case ILOpCode.Sub: {
                        var value2 = evalStack.Pop();
                        var value1 = evalStack.Pop();

                        if (value1.Type.TypeTag.HasFlag(LLVMTypeTag.Pointer) && !value2.Type.TypeTag.HasFlag(LLVMTypeTag.Pointer)) {
                            var resultInt = m_Builder.BuildSub(m_Builder.BuildPtrToInt(value1.Value, LLVMTypeRef.Int64), value2.TryCast(LLVMCompType.Int64, m_Builder).Value);
                            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildIntToPtr(resultInt, value1.Type.LLVMType), value1.Type.TypeTag));
                        } else {
                            value2 = value2.TryCast(value1.Type, m_Builder);
                            if (value1.Type.TypeTag.HasFlag(LLVMTypeTag.Real)) {
                                evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildFSub(value1.Value, value2.Value), value1.Type));
                            } else {
                                evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildSub(value1.Value, value2.Value), value1.Type));
                            }
                            //evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildSub(value1.Value, value2.Value), value1.Type));
                        }
                        break;
                    }
                    case ILOpCode.Div_un: {
                        var value2 = evalStack.Pop();
                        var value1 = evalStack.Pop();
                        value2 = value2.TryCast(value1.Type, m_Builder);
                        evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildUDiv(value1.Value, value2.Value), value1.Type));
                        break;
                    }
                    case ILOpCode.Div: {
                        var value2 = evalStack.Pop();
                        var value1 = evalStack.Pop();
                        value2 = value2.TryCast(value1.Type, m_Builder);
                        if (value1.Type.TypeTag.HasFlag(LLVMTypeTag.Real)) {
                            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildFDiv(value1.Value, value2.Value), value1.Type));
                        } else {
                            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildSDiv(value1.Value, value2.Value), value1.Type));
                        }
                        //evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildSDiv(value1.Value, value2.Value), value1.Type));
                        break;
                    }
                    case ILOpCode.Rem_un: {
                        var value2 = evalStack.Pop();
                        var value1 = evalStack.Pop();
                        value2 = value2.TryCast(value1.Type, m_Builder);
                        evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildURem(value1.Value, value2.Value), value1.Type));
                        break;
                    }
                    case ILOpCode.Rem: {
                        var value2 = evalStack.Pop();
                        var value1 = evalStack.Pop();
                        value2 = value2.TryCast(value1.Type, m_Builder);
                        evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildSRem(value1.Value, value2.Value), value1.Type));
                        break;
                    }
                    case ILOpCode.Mul_ovf:
                    case ILOpCode.Mul_ovf_un:
                    case ILOpCode.Mul: {
                        var value2 = evalStack.Pop();
                        var value1 = evalStack.Pop();

                        value2 = value2.TryCast(value1.Type, m_Builder);
                        if(value1.Type.TypeTag.HasFlag(LLVMTypeTag.Real)) {
                            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildFMul(value1.Value, value2.Value), value1.Type));
                        } else {
                            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildMul(value1.Value, value2.Value), value1.Type));
                        }
                        break;
                        
                    }
                    case ILOpCode.Shr: {
                        var value2 = evalStack.Pop();
                        var value1 = evalStack.Pop();

                        value2 = value2.TryCast(value1.Type, m_Builder);

                        evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildAShr(value1.Value, value2.Value), value1.Type));
                        break;
                    }
                    case ILOpCode.Shr_un: {
                        var value2 = evalStack.Pop();
                        var value1 = evalStack.Pop();

                        value2 = value2.TryCast(value1.Type, m_Builder);


                        evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildLShr(value1.Value, value2.Value), value1.Type));
                        break;
                    }
                    case ILOpCode.Shl: {
                        var value2 = evalStack.Pop();
                        var value1 = evalStack.Pop();

                        value2 = value2.TryCast(value1.Type, m_Builder);

                        evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildShl(value1.Value, value2.Value), value1.Type));
                        break;
                    }
                    case ILOpCode.And: {
                        var value1 = evalStack.Pop();
                        var value2 = evalStack.Pop();
                        evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildAnd(value1.Value, value2.Value), value1.Type));
                        break;
                    }
                    case ILOpCode.Or: {
                        var value1 = evalStack.Pop();
                        var value2 = evalStack.Pop();
                        evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildOr(value1.Value, value2.Value), value1.Type));
                        break;
                    }
                    case ILOpCode.Xor: {
                        var value1 = evalStack.Pop();
                        var value2 = evalStack.Pop();
                        evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildXor(value1.Value, value2.Value), value1.Type));
                        break;
                    }
                    case ILOpCode.Cgt: {
                        var value2 = evalStack.Pop();
                        var value1 = evalStack.Pop();

                        var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntSGT, value1.Value, value2.Value);
                        evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildZExt(result, LLVMTypeRef.Int32), LLVMTypeTag.SignedInt));
                        break;
                    }
                    case ILOpCode.Cgt_un: {
                        var value2 = evalStack.Pop().TryCastComparable(m_Builder);
                        var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
                        var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntUGT, value1.Value, value2.TryCast(value1.Type,m_Builder).Value);
                        evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildZExt(result, LLVMTypeRef.Int32), LLVMTypeTag.SignedInt));
                        break;
                    }
                    case ILOpCode.Bgt:
                    case ILOpCode.Bgt_s: {
                        var value2 = evalStack.Pop().TryCastComparable(m_Builder);
                        var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
                        var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntSGT, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);

                        var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
                        var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
                        m_Builder.BuildCondBr(result, target, falseTarget);

                        break;
                    }
                    case ILOpCode.Bgt_un:
                    case ILOpCode.Bgt_un_s: {
                        var value2 = evalStack.Pop().TryCastComparable(m_Builder);
                        var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
                        var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntUGT, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);

                        var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
                        var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
                        m_Builder.BuildCondBr(result, target, falseTarget);
                        
                        break;
                    }
                    case ILOpCode.Blt:
                    case ILOpCode.Blt_s: {
                        var value2 = evalStack.Pop().TryCastComparable(m_Builder);
                        var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
                        var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);

                        var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
                        var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
                        m_Builder.BuildCondBr(result, target, falseTarget);

                        break;
                    }
                    case ILOpCode.Ble:
                    case ILOpCode.Ble_s: {
                        var value2 = evalStack.Pop().TryCastComparable(m_Builder);
                        var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
                        var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLE, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);

                        var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
                        var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
                        m_Builder.BuildCondBr(result, target, falseTarget);

                        break;
                    }
                    case ILOpCode.Ble_un_s:
                    case ILOpCode.Ble_un: {
                        var value2 = evalStack.Pop().TryCastComparable(m_Builder);
                        var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
                        var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntULE, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);

                        var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
                        var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
                        m_Builder.BuildCondBr(result, target, falseTarget);

                        break;
                    }
                    case ILOpCode.Bge:
                    case ILOpCode.Bge_s: {
                        var value2 = evalStack.Pop().TryCastComparable(m_Builder);
                        var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
                        var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntSGE, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);

                        var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
                        var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
                        m_Builder.BuildCondBr(result, target, falseTarget);

                        break;
                    }
                    case ILOpCode.Bge_un_s:
                    case ILOpCode.Bge_un: {
                        var value2 = evalStack.Pop().TryCastComparable(m_Builder);
                        var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
                        var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntUGE, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);

                        var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
                        var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
                        m_Builder.BuildCondBr(result, target, falseTarget);

                        break;
                    }
                    case ILOpCode.Beq:
                    case ILOpCode.Beq_s: {
                        var value2 = evalStack.Pop().TryCastComparable(m_Builder);
                        var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
                        var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);

                        var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
                        var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
                        m_Builder.BuildCondBr(result, target, falseTarget);

                        break;
                    }
                    case ILOpCode.Blt_un:
                    case ILOpCode.Blt_un_s: {
                        var value2 = evalStack.Pop().TryCastComparable(m_Builder);
                        var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
                        var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntULT, value1.Value, value2.TryCast(value1.Type, m_Builder).Value);

                        var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
                        var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
                        m_Builder.BuildCondBr(result, target, falseTarget);

                        break;
                    }
                    case ILOpCode.Clt: {
                        var value2 = evalStack.Pop().TryCastComparable(m_Builder);
                        var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
                        var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, value1.Value, value2.Value);
                        evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildZExt(result,LLVMTypeRef.Int32), LLVMTypeTag.SignedInt));

                        break;
                    }
                    case ILOpCode.Clt_un: {
                        var value2 = evalStack.Pop().TryCastComparable(m_Builder); ;
                        var value1 = evalStack.Pop().TryCast(value2.Type, m_Builder);
                        var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntULT, value1.Value, value2.Value);
                        evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildZExt(result, LLVMTypeRef.Int32), LLVMTypeTag.SignedInt));

                        break;
                    }
                    case ILOpCode.Ceq: {
                        var value2 = evalStack.Pop().TryCastComparable(m_Builder); ;
                        var value1 = evalStack.Pop().TryCast(value2.Type,m_Builder);
                        var result = m_Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, value1.Value, value2.Value);
                        evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildZExt(result, LLVMTypeRef.Int32), LLVMTypeTag.SignedInt));
                        break;
                    }
                    case ILOpCode.Conv_r8: {
                        var value = evalStack.Pop();

                        evalStack.Push(value.TryCast(LLVMCompType.Float64, m_Builder));
                        break;
                    }
                    case ILOpCode.Conv_r4: {
                        var value = evalStack.Pop();

                        evalStack.Push(value.TryCast(LLVMCompType.Float32, m_Builder));
                        break;
                    }
                    case ILOpCode.Conv_u: {
                        var value = evalStack.Pop();

                        evalStack.Push(value.TryCast(LLVMCompType.Int64, m_Builder));
                        break;
                    }
                    case ILOpCode.Conv_i: {
                        var value = evalStack.Pop();
                        evalStack.Push(value.TryCast(LLVMCompType.Int64, m_Builder));
                        break;
                    }
                    case ILOpCode.Conv_i8: {
                        var value = evalStack.Pop();
                        evalStack.Push(value.TryCast(LLVMCompType.Int64, m_Builder));
                        break;
                    }
                    case ILOpCode.Conv_i4: {
                        var value = evalStack.Pop();
                        evalStack.Push(value.TryCast(LLVMCompType.Int32, m_Builder));
                        break;
                    }
                    case ILOpCode.Conv_i2: {
                        var value = evalStack.Pop();
                        evalStack.Push(value.TryCast(LLVMCompType.Int16, m_Builder));
                        break;
                    }
                    case ILOpCode.Conv_i1: {
                        var value = evalStack.Pop();
                        evalStack.Push(value.TryCast(LLVMCompType.Int8, m_Builder));
                        break;
                    }
                    case ILOpCode.Conv_u8: {
                        var value = evalStack.Pop();
                        evalStack.Push(value.TryCast(LLVMCompType.UInt64, m_Builder));
                        break;
                    }
                    case ILOpCode.Conv_u4: {
                        var value = evalStack.Pop();
                        evalStack.Push(value.TryCast(LLVMCompType.UInt32, m_Builder));
                        break;
                    }
                    case ILOpCode.Conv_u2: {
                        var value = evalStack.Pop();
                        evalStack.Push(value.TryCast(LLVMCompType.UInt16, m_Builder));
                        break;
                    }
                    case ILOpCode.Conv_u1: {
                        var value = evalStack.Pop();
                        evalStack.Push(value.TryCast(LLVMCompType.UInt8, m_Builder));
                        break;
                    }
                    case ILOpCode.Ret: {
                        if (method.Signature.ReturnType.ToString() != "System::Void") {
                            var retValue = evalStack.Pop().TryCast(context.ReturnType,m_Builder);
                           
                            m_Builder.BuildRet(retValue.Value);
                        } else {
                            m_Builder.BuildRetVoid();
                        }
                        if (evalStack.Count != 0) throw new Exception("Stack analysis fault");
                        break;
                    }
                    case ILOpCode.Switch: {
                        var branches = (uint)operand;
                        var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
                        var value = evalStack.Pop().TryCast(LLVMCompType.UInt32,m_Builder);
                        var switchInst = m_Builder.BuildSwitch(value.Value, falseTarget, branches);
                        for(var i = 0u; i < branches; i++) {
                            var trueTarget = context.GetLLVMBasicBlock(basicBlock.TrueExit[i]);
                            switchInst.AddCase(LLVMHelpers.CreateConstU32(i), trueTarget);
                        }
                        break;
                    }
                    case ILOpCode.Br:
                    case ILOpCode.Br_s: {
                        var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
                        m_Builder.BuildBr(target);
                        break;
                    }
                    case ILOpCode.Brtrue:
                    case ILOpCode.Brtrue_s: {
                        var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
                        var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
                        var cond = evalStack.Pop().TryCastCond(m_Builder);
                        m_Builder.BuildCondBr(cond.Value, target, falseTarget);

                        break;
                    }
                    case ILOpCode.Brfalse:
                    case ILOpCode.Brfalse_s: {
                        var target = context.GetLLVMBasicBlock(basicBlock.TrueExit[0]);
                        var falseTarget = context.GetLLVMBasicBlock(basicBlock.FalseExit);
                        var cond = evalStack.Pop().TryCastCond(m_Builder);
                        m_Builder.BuildCondBr(cond.Value, falseTarget, target);

                        break;
                    }
                    case ILOpCode.Ldarga:
                    case ILOpCode.Ldarga_s: {
                        evalStack.Push(context.ParamValueRef[operand]);
                        break;
                    }
                    case ILOpCode.Ldfld: {
                        var instancePtr = (evalStack.Pop());
                        //if (instancePtr.Type.TypeTag.HasFlag(LLVMTypeTag.Class)|| instancePtr.Type.TypeTag.HasFlag(LLVMTypeTag.Pointer)) {
                            var fieldPtr = IRHelper.GetInstanceFieldAddress(instancePtr, (uint)operand, context, m_Builder, out var fieldType);
                            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildLoad(fieldPtr), fieldType.TypeTag));
                            break;
                            
                        //}

                        throw new NotImplementedException();
                    }
                    case ILOpCode.Stfld: {
                        var value = evalStack.Pop();
                        var instancePtr = (evalStack.Pop());
                        var fieldPtr = IRHelper.GetInstanceFieldAddress(instancePtr, (uint)operand, context, m_Builder, out var fieldType);
                        value = value.TryCast(fieldType, m_Builder);
                        m_Builder.BuildStore(value.Value, fieldPtr);

                        break;
                    }
                    case ILOpCode.Ldflda: {
                        var instancePtr = (evalStack.Pop());
                        var fieldPtr = IRHelper.GetInstanceFieldAddress(instancePtr, (uint)operand, context, m_Builder, out var fieldType);
                        evalStack.Push(LLVMCompValue.CreateValue(fieldPtr,LLVMTypeTag.Pointer));

                        break;
                    }
                    case ILOpCode.Ldc_r8: {
                        var value = *(double*)(&operand);
                        var doubleValue = LLVMValueRef.CreateConstReal(LLVMTypeRef.Double, value);
                        evalStack.Push(LLVMCompValue.CreateValue(doubleValue, LLVMTypeTag.Real | LLVMTypeTag.FP64));
                        break;
                    }
                    case ILOpCode.Ldc_r4: {
                        var value = *(float*)(&operand);
                        var doubleValue = LLVMValueRef.CreateConstReal(LLVMTypeRef.Float, value);
                        evalStack.Push(LLVMCompValue.CreateValue(doubleValue, LLVMTypeTag.Real));
                        break;
                    }
                    case ILOpCode.Ldftn: {
                        var factory = context.Context.TypeEnvironment.TypeEntryFactory;

                        var llvmFunction = IRHelper.GetMethodInfo((uint)operand, context, m_Builder, out var llvmType,out _);
                        var ptrType = LLVMCompType.Int8.ToPointerType();
                        var ptrValue = m_Builder.BuildBitCast(llvmFunction.Function, ptrType.LLVMType);
                        evalStack.Push(LLVMCompValue.CreateValue(ptrValue, ptrType));
                        break;
                    }
                    case ILOpCode.Dup: {
                        evalStack.Push(evalStack.Peek());
                        break;
                    }
                    case ILOpCode.Ldind_i: {
                        var pointerValue = evalStack.Pop();
                        var castPtr = pointerValue.TryCast(LLVMCompType.Int8.ToPointerType().ToPointerType(), m_Builder);
                        var value = m_Builder.BuildLoad(castPtr.Value);

                        evalStack.Push(LLVMCompValue.CreateValue(value, LLVMCompType.Int8.ToPointerType()));
                        break;
                    }
                    case ILOpCode.Ldind_u1:
                    case ILOpCode.Ldind_i1: {
                        var pointerValue = evalStack.Pop();
                        var type = (opcode == ILOpCode.Ldind_u1) ? LLVMCompType.UInt8 : LLVMCompType.Int8;
                        var castPtr = pointerValue.TryCast(type.ToPointerType(), m_Builder);
                        var value = m_Builder.BuildLoad(castPtr.Value);
                        
                        evalStack.Push(LLVMCompValue.CreateValue(value, type));
                        break;
                    }
                    case ILOpCode.Ldind_u2:
                    case ILOpCode.Ldind_i2: {
                        var pointerValue = evalStack.Pop();
                        var type = (opcode == ILOpCode.Ldind_u2) ? LLVMCompType.UInt16 : LLVMCompType.Int16;
                        var castPtr = pointerValue.TryCast(type.ToPointerType(), m_Builder);
                        var value = m_Builder.BuildLoad(castPtr.Value);
                        evalStack.Push(LLVMCompValue.CreateValue(value, type));
                        break;
                    }
                    case ILOpCode.Ldind_u4:
                    case ILOpCode.Ldind_i4: {
                        var pointerValue = evalStack.Pop();
                        var type = (opcode == ILOpCode.Ldind_u4) ? LLVMCompType.UInt32 : LLVMCompType.Int32;
                        var castPtr = pointerValue.TryCast(type.ToPointerType(), m_Builder);
                        var value = m_Builder.BuildLoad(castPtr.Value);
                        evalStack.Push(LLVMCompValue.CreateValue(value, type));
                        break;
                    } 
                    case ILOpCode.Ldind_i8: {
                        var pointerValue = evalStack.Pop();
                        var castPtr = pointerValue.TryCast(LLVMCompType.Int64.ToPointerType(), m_Builder);
                        var value = m_Builder.BuildLoad(castPtr.Value);
                        evalStack.Push(LLVMCompValue.CreateValue(value, LLVMCompType.Int64));
                        break;
                    }
                    case ILOpCode.Stind_i: {
                        var value = evalStack.Pop();
                        var pointerValue = evalStack.Pop();
                        var castPtr = pointerValue.TryCast(LLVMCompType.Int8.ToPointerType().ToPointerType(), m_Builder);
                        m_Builder.BuildStore(value.Value, castPtr.Value);
                        break;
                    }
                    case ILOpCode.Neg: {
                        var value = evalStack.Pop();
                        if (value.Type.TypeTag.HasFlag(LLVMTypeTag.Real)) {
                            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildFNeg(value.Value), value.Type));
                        } else {
                            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildNeg(value.Value), value.Type));
                        }
                        
                        break;
                    }
                    case ILOpCode.Stind_i1: {
                        var value = evalStack.Pop().TryCast(LLVMCompType.Int8, m_Builder);
                        var pointerValue = evalStack.Pop();
                        var castPtr = pointerValue.TryCast(LLVMCompType.Int8.ToPointerType(), m_Builder);
                        m_Builder.BuildStore(value.Value, castPtr.Value);
                        break;
                    }
                    case ILOpCode.Stind_i2: {
                        var value = evalStack.Pop().TryCast(LLVMCompType.Int16, m_Builder);
                        var pointerValue = evalStack.Pop();
                        var castPtr = pointerValue.TryCast(LLVMCompType.Int16.ToPointerType(), m_Builder);
                        m_Builder.BuildStore(value.Value, castPtr.Value);
                        break;
                    }
                    case ILOpCode.Stind_i4: {
                        var value = evalStack.Pop().TryCast(LLVMCompType.Int32, m_Builder);
                        var pointerValue = evalStack.Pop();
                        var castPtr = pointerValue.TryCast(LLVMCompType.Int32.ToPointerType(), m_Builder);
                        m_Builder.BuildStore(value.Value, castPtr.Value);
                        break;
                    }
                    case ILOpCode.Stind_i8: {
                        var value = evalStack.Pop().TryCast(LLVMCompType.Int64, m_Builder);
                        var pointerValue = evalStack.Pop();
                        var castPtr = pointerValue.TryCast(LLVMCompType.Int64.ToPointerType(), m_Builder);
                        m_Builder.BuildStore(value.Value, castPtr.Value);
                        break;
                    }
                    case ILOpCode.Ldsfld: {
                        var fieldAddr = IRHelper.GetStaticFieldAddress((uint)operand, context, m_Builder, out var fieldType);
                        var fieldValue = m_Builder.BuildLoad(fieldAddr);
                        evalStack.Push(LLVMCompValue.CreateValue(fieldValue,fieldType.TypeTag));
                        break;
                    }
                    case ILOpCode.Stsfld: {
                        var fieldAddr = IRHelper.GetStaticFieldAddress((uint)operand, context, m_Builder, out var fieldType);

                        var value = evalStack.Pop().TryCast(fieldType, m_Builder) ;
                        m_Builder.BuildStore(value.Value, fieldAddr);
                        break;
                    }
                    case ILOpCode.Ldsflda: {
                        var fieldAddress = IRHelper.GetStaticFieldAddress((uint)operand, context, m_Builder, out var fieldType);
                        evalStack.Push(LLVMCompValue.CreateValue(fieldAddress,fieldType.TypeTag));
                        break;
                    }
                    case ILOpCode.Calli: {
                        var signatureToken = (StandaloneSignatureHandle)MetadataHelper.CreateHandle((uint)operand);
                        var signature = context.Method.Reader.GetStandaloneSignature(signatureToken);
                        var callSite = signature.DecodeMethodSignature(context.Method.TypeEnv.SignatureDecoder, context.Method);
                        var argType = new LLVMTypeRef[callSite.ParameterTypes.Length];
                        var argValue = new LLVMValueRef[callSite.ParameterTypes.Length];
                        var functionPtr = evalStack.Pop();
                        for(var i = callSite.ParameterTypes.Length - 1; i >= 0; i--) {
                            var paramType = context.Context.ResolveLLVMInstanceType(callSite.ParameterTypes[i]);
                            argType[i] = paramType.LLVMType;
                            argValue[i] = evalStack.Pop().TryCast(paramType,m_Builder).Value;
                        }
                        var retType = context.Context.ResolveLLVMInstanceType(callSite.ReturnType);
                        var funcType = LLVMTypeRef.CreatePointer(LLVMTypeRef.CreateFunction(retType.LLVMType, argType),0);
                        functionPtr = functionPtr.TryCast(LLVMCompType.CreateType(LLVMTypeTag.Pointer, funcType),m_Builder);

                        var result = m_Builder.BuildCall2(funcType.ElementType, functionPtr.Value, argValue);
                        if (retType.LLVMType != LLVMTypeRef.Void) evalStack.Push(LLVMCompValue.CreateValue(result, retType));
                        break;
                    }
                    case ILOpCode.Castclass: {
                        var targetType = IRHelper.GetTypeInfo((uint)operand, context, m_Builder);
                        var srcObject = evalStack.Pop();
                        
                        var runtimeHelpers = context.Context.ResolveLLVMType(context.Context.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::RuntimeHelpers"]);
                        var castBack = context.Context.ResolveLLVMMethod(runtimeHelpers.MetadataType.FindMethodByName("CastBack").Entry);
                        var castToInterface = context.Context.ResolveLLVMMethod(runtimeHelpers.MetadataType.FindMethodByName("CastToInterface").Entry);
                        
                        if (targetType is LLVMClassTypeInfo) {
                            if (srcObject.Type.TypeTag.HasFlag(LLVMTypeTag.Interface)) {
                                // interface to class
                                var dstObject = m_Builder.BuildCall2(castBack.FunctionType,castBack.Function, new LLVMValueRef[] {
                                    srcObject.TryCast(castBack.ParamTypes[0],m_Builder).Value
                                });
                                evalStack.Push(LLVMCompValue.CreateValue(dstObject, targetType.InstanceType));
                            } else {
                                // class to class
                                evalStack.Push(LLVMCompValue.CreateValue(srcObject.Value, targetType.InstanceType));
                            }
                        } else {
                            if (srcObject.Type.TypeTag.HasFlag(LLVMTypeTag.Interface)) {
                                // interface to interface. Should be avoid if possible
                                var rawObject = m_Builder.BuildCall2(castBack.FunctionType,castBack.Function, new LLVMValueRef[] {
                                    srcObject.TryCast(castBack.ParamTypes[0],m_Builder).Value
                                });
                                var dstObject = m_Builder.BuildCall2(castToInterface.FunctionType,castToInterface.Function, new LLVMValueRef[] {
                                    m_Builder.BuildBitCast(rawObject,castToInterface.ParamTypes[0].LLVMType), 
                                    LLVMHelpers.CreateConstU32(targetType.TypeHash) 
                                });
                                evalStack.Push(LLVMCompValue.CreateValue(dstObject, targetType.InstanceType));
                            } else {
                                // class to interface
                                var dstObject = m_Builder.BuildCall2(castToInterface.FunctionType,castToInterface.Function, new LLVMValueRef[] {
                                    srcObject.TryCast(castToInterface.ParamTypes[0],m_Builder).Value, 
                                    LLVMHelpers.CreateConstU32(targetType.TypeHash) 
                                });
                                evalStack.Push(LLVMCompValue.CreateValue(dstObject, targetType.InstanceType));
                            }
                        }

                        break;
                    }
                    case ILOpCode.Call: {
                        var targetMethod = IRHelper.GetMethodInfo((uint)operand, context, m_Builder, out var declType, out var callSiteSig);
                        var callSiteParamCount = callSiteSig.ParameterTypes.Length + (targetMethod.Method.Attributes.HasFlag(MethodAttributes.Static)?0:1);
                        var defParams = targetMethod.ParamCount;
                        var argumentList = new LLVMValueRef[callSiteParamCount];

                        var runtimeHelpers = context.Context.ResolveLLVMType(context.Context.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::RuntimeHelpers"]);
                        var toUTF16String = context.Context.ResolveLLVMMethod(runtimeHelpers.MetadataType.FindMethodByName("ToUTF16String").Entry);

                        if(targetMethod == toUTF16String) {
                            var stringValue = context.Context.GetInternedString(evalStack.Pop().Value);
                            var buffer = new char[stringValue.Length + 1];
                            stringValue.CopyTo(0,buffer,0,stringValue.Length);
                            fixed (char *pString = buffer) {
                                
                                var utf16String = (LLVMValueRef)LLVM.ConstStringInContext(context.Context.Context, (sbyte*)pString, (uint)buffer.Length * 2, 1);
                                var utf16Global = context.Context.Module.AddGlobal(utf16String.TypeOf, $"U16_{buffer.GetHashCode()}");
                                utf16Global.IsGlobalConstant = true;
                                utf16Global.Initializer = utf16String;

                                evalStack.Push(LLVMCompValue.CreateValue(utf16Global, targetMethod.ReturnType));
                                break;
                            }
                        }

                        if (targetMethod.Method.Signature.Header.CallingConvention.HasFlag(SignatureCallingConvention.VarArgs)) {
                            // va args
                            for (var i = callSiteParamCount - 1; i >= defParams; i--) {
                                var paramValue = evalStack.Pop();
                                var paramType = context.Context.ResolveLLVMInstanceType(callSiteSig.ParameterTypes[i]);
                                argumentList[i] = paramValue.TryCast(paramType, m_Builder).Value;
                            }

                            var asmHelpers = context.Context.ResolveLLVMType(context.Context.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::UnsafeAsm"]);
                            
                            
                            if(targetMethod.DeclType == asmHelpers) {
                                var constrainString = context.Context.GetInternedString(evalStack.Pop().Value);
                                var asmCode = context.Context.GetInternedString(evalStack.Pop().Value);
                                
                                var newArgList = new LLVMValueRef[callSiteParamCount - defParams];
                                for (var i = defParams; i < callSiteParamCount; i++) newArgList[i - defParams] = argumentList[i];
                                var asmTypes = newArgList.Select(e => e.TypeOf).ToArray();
                                var asmFuncType = LLVMTypeRef.CreateFunction(targetMethod.ReturnType.LLVMType, asmTypes);
                                var asmStmt = LLVMValueRef.CreateConstInlineAsm(asmFuncType, asmCode, constrainString, true, false);
                                var asmResult = m_Builder.BuildCall(asmStmt, newArgList);
                                if(targetMethod.ReturnType.LLVMType != LLVMTypeRef.Void) {
                                    evalStack.Push(LLVMCompValue.CreateValue(asmResult, targetMethod.ReturnType));
                                }
                                
                                break;
                            }
                        }
                        
                        for(var i = defParams - 1; i >= 0; i--) {
                            argumentList[i] = evalStack.Pop().TryCast(targetMethod.ParamTypes[i], m_Builder,context.Context).Value;
                        }
                        var callReturn = m_Builder.BuildCall2(targetMethod.FunctionType,targetMethod.Function, argumentList);
                        if(targetMethod.ReturnType.LLVMType!=LLVMTypeRef.Void)
                            evalStack.Push(LLVMCompValue.CreateValue(callReturn, targetMethod.ReturnType));
                        break;
                    }
                    case ILOpCode.Callvirt: {
                        var targetMethod = IRHelper.GetMethodInfo((uint)operand, context, m_Builder, out var declType,out _);

                        var argumentList = new LLVMValueRef[targetMethod.ParamCount];
                        for (var i = targetMethod.ParamCount-1; i >= 1; i--) {
                            var argValue = evalStack.Pop();
                            argumentList[i] = argValue.TryCast(targetMethod.ParamTypes[i], m_Builder,context.Context).Value;
                        }
                        var pthis = evalStack.Pop();
                        argumentList[0] = pthis.TryCast(targetMethod.ParamTypes[0], m_Builder).Value;

                        // target is not virtual
                        if (!targetMethod.Method.Attributes.HasFlag(MethodAttributes.Virtual)) {
                            IRHelper.MakeCall(targetMethod, targetMethod.Function, argumentList, m_Builder, evalStack);
                            break;
                        }

                        // delegate invoke
                        var delegateBaseType = (LLVMClassTypeInfo)context.Context.ResolveLLVMType(context.Context.TypeEnvironment.IntrinicsTypes["System::MulticastDelegate"]);
                        if(delegateBaseType == declType.BaseType) {
                            IRHelper.MakeCall(targetMethod, targetMethod.Function, argumentList, m_Builder, evalStack);
                            break;
                        }

                        // constrained
                        var skipLookup = false;
                        if(callvirtTypeHint != null) {
                            if (callvirtTypeHint.MetadataType.IsValueType) {
                                foreach(var i in callvirtTypeHint.MetadataType.Methods) {
                                    if(i.Value.Entry.Name == targetMethod.Method.Entry.Name && i.Value.EqualsSignature(targetMethod.Method)) {
                                        skipLookup = true;
                                        var llvmMethod = context.Context.ResolveLLVMMethod(i.Value.Entry);
                                        argumentList[0] = pthis.TryCast(llvmMethod.ParamTypes[0], m_Builder).Value;

                                        IRHelper.MakeCall(targetMethod, llvmMethod.Function, argumentList, m_Builder, evalStack);
                                        break;
                                    }
                                }
                                if (!skipLookup) {
                                    // should be boxed
                                    throw new NotImplementedException();
                                }
                            } else {
                                // should dereference pthis
                                throw new NotImplementedException();
                            }
                            callvirtTypeHint = null;
                            if (skipLookup) break;
                        }

                        var vtableIndex = 0;
                        var realPtrThis = pthis.Value;
                        LLVMValueRef targetFunction = default;

                        var runtimeHelpers = context.Context.ResolveLLVMType(context.Context.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::RuntimeHelpers"]);
                        var lookupInterfaceVtable = context.Context.ResolveLLVMMethod(runtimeHelpers.MetadataType.FindMethodByName("LookupInterfaceVtable").Entry);
                        var castToInterface = context.Context.ResolveLLVMMethod(runtimeHelpers.MetadataType.FindMethodByName("CastToInterface").Entry);
                        
                        vtableIndex = declType.LocateMethodInMainTable(targetMethod);

                        if (declType is LLVMClassTypeInfo) {
                            if (pthis.Type.TypeTag.HasFlag(LLVMTypeTag.Interface)) {
                                // call heleprs
                                throw new InvalidProgramException();
                            } else {
                                // class to class
                                targetFunction = IRHelper.LookupVtable(realPtrThis, vtableIndex, declType.VtableType.StructElementTypes[0],m_Builder);
                                argumentList[0] = pthis.TryCast(targetMethod.ParamTypes[0], m_Builder).Value;
                            }
                        } else {
                            if (pthis.Type.TypeTag.HasFlag(LLVMTypeTag.Interface)) {
                                // interface target => interface method
                                var llvmType = (LLVMInterfaceTypeInfo)context.Context.ResolveLLVMTypeFromTypeRef(pthis.Type);
                                if (llvmType.Interfaces.Contains(declType) || llvmType == declType) {
                                    vtableIndex = llvmType.LocateMethodInMainTable(targetMethod);

                                    var vtableOffset = LLVMValueRef.CreateConstPtrToInt(
                                        LLVMValueRef.CreateConstGEP(LLVMValueRef.CreateConstNull(LLVMTypeRef.CreatePointer(llvmType.VtableType,0)),
                                        new LLVMValueRef[] { 
                                            LLVMHelpers.CreateConstU32(0),
                                            LLVMHelpers.CreateConstU32(vtableIndex + 1) // skip interface header
                                        })
                                        , LLVMTypeRef.Int32);

                                    var ptrMaskFunc = LLVMHelpers.GetIntrinsicFunction(context.Context.Module, "llvm.ptrmask", new LLVMTypeRef[] {
                                        declType.InstanceType.LLVMType,
                                        LLVMTypeRef.Int64
                                    });
                                    var realPthis = m_Builder.BuildCall2(ptrMaskFunc.TypeOf.ElementType,ptrMaskFunc, new LLVMValueRef[] {
                                        pthis.TryCast(declType.InstanceType,m_Builder).Value,
                                        LLVMHelpers.CreateConstU64(0xFFFFFFFFFFFF)
                                    });

                                    targetFunction = m_Builder.BuildBitCast(
                                        m_Builder.BuildCall2(lookupInterfaceVtable.FunctionType,lookupInterfaceVtable.Function, new LLVMValueRef[] {
                                            pthis.TryCast(lookupInterfaceVtable.ParamTypes[0],m_Builder).Value,
                                            m_Builder.BuildBitCast(realPthis,lookupInterfaceVtable.ParamTypes[1].LLVMType),
                                            vtableOffset
                                        }),
                                        targetMethod.FunctionPtrType);

                                    argumentList[0] = m_Builder.BuildBitCast(realPthis, targetMethod.ParamTypes[0].LLVMType);
                                } else {
                                    throw new NotImplementedException();
                                }
                            } else {
                                // class instance => interface method
                                throw new NotImplementedException();
                            }
                        }


                        IRHelper.MakeCall(targetMethod, targetFunction, argumentList, m_Builder, evalStack);

                        break;
                    }
                    case ILOpCode.Newobj: {
                        var ctor = IRHelper.GetMethodInfo((uint)operand, context, m_Builder, out var declType, out _);

                        var pthis = declType.MetadataType.IsValueType ? 
                            m_Builder.BuildAlloca(((LLVMClassTypeInfo)declType).DataStorageType)
                            : IRHelper.AllocObjectDefault(context.Context, (LLVMClassTypeInfo)declType, m_Builder);
                        var argumentList = new LLVMValueRef[ctor.ParamCount];
                        for (var i = ctor.ParamCount-1; i >= 1; i--) {
                            var argValue = evalStack.Pop();
                            argumentList[i] = argValue.TryCast(ctor.ParamTypes[i], m_Builder).Value;
                        }
                        argumentList[0] = pthis;
                        m_Builder.BuildCall2(ctor.FunctionType,ctor.Function, argumentList);

                        if (declType.MetadataType.IsValueType) {
                            evalStack.Push(LLVMCompValue.CreateValue(m_Builder.BuildLoad(pthis), declType.InstanceType));
                        } else {
                            evalStack.Push(LLVMCompValue.CreateValue(pthis, declType.InstanceType));
                        }
                        

                        break;
                    }
                    case ILOpCode.Pop: {
                        evalStack.Pop();
                        break;
                    }
                    
                    case ILOpCode.Initobj: {
                        evalStack.Pop();
                        break;
                    }
                    case ILOpCode.Sizeof: {
                        var targetType = (LLVMClassTypeInfo)IRHelper.GetTypeInfo((uint)operand, context, m_Builder);
                        evalStack.Push(LLVMCompValue.CreateValue(targetType.DataStorageType.SizeOf, LLVMCompType.Int64));
                        break;
                    }
                    case ILOpCode.Isinst: {
                        var runtimeHelpers = context.Context.ResolveLLVMType(context.Context.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::RuntimeHelpers"]);
                        var isInstClass = context.Context.ResolveLLVMMethod(runtimeHelpers.MetadataType.FindMethodByName("IsInstClass").Entry);
                        var isInstInterface = context.Context.ResolveLLVMMethod(runtimeHelpers.MetadataType.FindMethodByName("IsInstInterface").Entry);
                        var value = evalStack.Pop();
                        var targetType = IRHelper.GetTypeInfo((uint)operand, context, m_Builder);

                        var objectType = (LLVMClassTypeInfo)context.Context.ResolveLLVMType(context.Context.TypeEnvironment.IntrinicsTypes["System::Object"]);
                       
                        if(targetType is LLVMClassTypeInfo) {     
                            var result = m_Builder.BuildCall2(isInstClass.FunctionType,isInstClass.Function, new LLVMValueRef[] {
                                m_Builder.BuildBitCast(value.Value,isInstClass.ParamTypes[0].LLVMType),
                                targetType.VtableType.StructElementTypes[0].SizeOf,
                                m_Builder.BuildBitCast(targetType.VtableBody,isInstClass.ParamTypes[2].LLVMType),
                            });
                            var retObject = m_Builder.BuildBitCast(result, value.Type.LLVMType);
                            evalStack.Push(LLVMCompValue.CreateValue(retObject, value.Type));
                        } else {
                            var result = m_Builder.BuildCall2(isInstInterface.FunctionType,isInstInterface.Function, new LLVMValueRef[] {
                                m_Builder.BuildBitCast(value.Value,isInstInterface.ParamTypes[0].LLVMType),
                                LLVMHelpers.CreateConstU32(targetType.TypeHash),
                            });
                            var retObject = m_Builder.BuildBitCast(result, value.Type.LLVMType);
                            evalStack.Push(LLVMCompValue.CreateValue(retObject, value.Type));
                        }
                        break;
                    }
                    case ILOpCode.Ldnull: {
                        evalStack.Push(LLVMCompValue.CreateValue(LLVMValueRef.CreateConstNull(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)),LLVMTypeTag.Pointer));
                        break;
                    }
                    case ILOpCode.Constrained: {
                        callvirtTypeHint = IRHelper.GetTypeInfo((uint)operand, context, m_Builder);
                        break;
                    }
                    case ILOpCode.Unbox_any: {
                        var targetType = (LLVMClassTypeInfo)IRHelper.GetTypeInfo((uint)operand, context, m_Builder);
                        var value = evalStack.Pop();

                        var ptrDataStor = m_Builder.BuildBitCast(value.Value, LLVMTypeRef.CreatePointer(targetType.InstanceType.LLVMType, 0));
                        var dataStorVal = m_Builder.BuildLoad(ptrDataStor);
                        evalStack.Push(LLVMCompValue.CreateValue(dataStorVal, targetType.InstanceType.TypeTag));
                        break;
                    }
                    case ILOpCode.Ldloca_s: {

                        var index = opcode == ILOpCode.Ldloca_s ? ((int)operand) : (opcode - ILOpCode.Ldloc_0);
                        evalStack.Push((context.LocalValueRef[index]));
                        break;
                    }
                    case ILOpCode.Ldc_i4_m1: {
                        evalStack.Push(LLVMCompValue.CreateConstI32(-1));
                        break;
                    }
                    case ILOpCode.Ldc_i8: {
                        evalStack.Push(LLVMCompValue.CreateConstI64(operand));
                        break;
                    }
                    case ILOpCode.Box: {
                        var boxType = (LLVMClassTypeInfo)IRHelper.GetTypeInfo((uint)operand, context, m_Builder);
                        var boxObject = IRHelper.AllocObjectDefault(context.Context, boxType, m_Builder);
                        var dataStorValue = evalStack.Pop();
                        var boxDataStorPtr = m_Builder.BuildBitCast(boxObject, dataStorValue.Type.ToPointerType().LLVMType);
                        m_Builder.BuildStore(dataStorValue.Value, boxDataStorPtr);
                        evalStack.Push(LLVMCompValue.CreateValue(boxObject, boxType.HeapPtrType));

                        break;
                    }
                    case ILOpCode.Ldstr: {
                        var token = MetadataHelper.CreateStringHandle(0xFFFFFF & ((uint)operand));
                        var reader = method.Reader;
                        var constString = reader.GetUserString(token);
                        var stringValue = context.Context.InternString(constString);
                        /*var stringType = (LLVMClassTypeInfo)context.Context.ResolveLLVMType(context.Context.TypeEnvironment.IntrinicsTypes["System::String"]);
                        var runtimeHelpers = context.Context.ResolveLLVMType(context.Context.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::RuntimeHelpers"]);
                        var stringHeapPtr = m_Builder.BuildGEP(stringValue.TypeOf.ElementType,stringValue, new LLVMValueRef[] {
                            LLVMHelpers.CreateConstU32(0),
                            LLVMHelpers.CreateConstU32(1)
                        });*/
                        
                        evalStack.Push(LLVMCompValue.CreateValue(stringValue, LLVMTypeTag.Class));
                        break;
                    }
                    case ILOpCode.Localloc: {
                        var length = evalStack.Pop().TryCast(LLVMCompType.Int64,m_Builder);
                        var ptr = m_Builder.BuildArrayAlloca(LLVMTypeRef.Int8, length.Value);
                        evalStack.Push(LLVMCompValue.CreateValue(ptr, LLVMCompType.Int8.ToPointerType()));
                        break;
                    }
                    case ILOpCode.Arglist: {
                        //var llvmFunc = context.Function;
                        //llvmFunc.FunctionCallConv = (uint)LLVMCallConv.LLVMWin64CallConv;
                        var valistWrapperType = context.Context.TypeEnvironment.IntrinicsTypes["System::RuntimeArgumentHandle"];
                        var valistLLVMType = (LLVMClassTypeInfo)context.Context.ResolveLLVMType(valistWrapperType);

                        var valistStorage = m_Builder.BuildAlloca(valistLLVMType.DataStorageType);
                        var valistPtr = m_Builder.BuildGEP(valistStorage, new LLVMValueRef[] { 
                            LLVMHelpers.CreateConstU32(0),
                            LLVMHelpers.CreateConstU32(1)
                        });

                        var vaStartFunc = LLVMHelpers.GetIntrinsicFunction(context.Context.Module, "llvm.va_start", new LLVMTypeRef[] { 
                        });
                        m_Builder.BuildCall(vaStartFunc, new LLVMValueRef[] { 
                            m_Builder.BuildBitCast(valistPtr,LLVMCompType.Int8.ToPointerType().LLVMType)
                        });
                        var valistValue = m_Builder.BuildLoad(valistStorage); ;
                        evalStack.Push(LLVMCompValue.CreateValue(valistValue, valistLLVMType.InstanceType));

                        break;
                    }

                    default: {
                        throw new NotImplementedException();
                    }
                }
            }, true);

            return evalStack;
        }
    }
    public class LLVMMethodInfoWrapper {
        protected static Queue<MethodBasicBlock> s_Queue = new Queue<MethodBasicBlock>();
        protected static Stack<LLVMCompValue> s_EvalStack = new Stack<LLVMCompValue>();

        protected MethodInstanceInfo m_Method;
        protected LLVMTypeInfo m_DeclType;
        protected LLVMCompiler m_Compiler;
        protected LLVMTypeRef m_FunctionType;
        protected LLVMValueRef m_Function;
        protected LLVMTypeRef m_FunctionPtrType;
        protected LLVMCompValue[] m_LocalVarValues = null;
        protected SortedList<Interval, LLVMBasicBlockRef> m_LLVMBasicBlocks;
        protected LLVMCompValue[] m_ParamValues = null;
        protected LLVMCompType m_FunctionRetType;
        protected LLVMCompType[] m_ParamType = null;

        public bool IsAbstract => m_Method.Attributes.HasFlag(MethodAttributes.Abstract);
        public LLVMCompValue[] ParamValueRef => m_ParamValues;
        public LLVMCompType[] ParamTypes => m_ParamType;
        public int ParamCount => m_ParamType.Length;
        public LLVMCompValue[] LocalValueRef => m_LocalVarValues;
        public LLVMCompiler Context => m_Compiler;
        public LLVMValueRef Function => m_Function;
        public LLVMTypeRef FunctionType => m_FunctionType;
        public LLVMTypeRef FunctionPtrType => m_FunctionPtrType;
        public LLVMCompType ReturnType => m_FunctionRetType;
        public MethodInstanceInfo Method => m_Method;
        public LLVMTypeInfo DeclType => m_DeclType;


        public LLVMMethodInfoWrapper(LLVMCompiler compiler, MethodInstanceInfo method) {
            m_Compiler = compiler;
            m_Method = method;
            m_DeclType = compiler.ResolveLLVMType(method.DeclType.Entry);
            var signature = method.Signature;
            var returnType = compiler.ResolveLLVMInstanceType(signature.ReturnType);

            var idx = 0u;
            var isStatic = m_Method.Attributes.HasFlag(MethodAttributes.Static);

            m_ParamType = new LLVMCompType[signature.ParameterTypes.Length + (isStatic ? 0 : 1)];
            idx = 0;
            if (!isStatic) {
                var declType = m_Method.DeclType;
                var llvmType = m_Compiler.ResolveLLVMType(declType.Entry);
                m_ParamType[idx++] = llvmType is LLVMClassTypeInfo classType ? classType.HeapPtrType : (llvmType.InstanceType.WithTag(LLVMTypeTag.StackAlloc));
            }
            foreach (var i in signature.ParameterTypes) m_ParamType[idx++] = m_Compiler.ResolveLLVMInstanceType(i);

            var isVaArg = method.Signature.Header.CallingConvention.HasFlag(SignatureCallingConvention.VarArgs);
            m_FunctionRetType = returnType;
            m_FunctionType = LLVMTypeRef.CreateFunction(returnType.LLVMType, m_ParamType.Select(e=>e.LLVMType).ToArray(), isVaArg);

            m_FunctionPtrType = LLVMTypeRef.CreatePointer(m_FunctionType, 0);

            if (!m_Method.Attributes.HasFlag(MethodAttributes.Abstract)) {
                m_Function = compiler.Module.AddFunction(method.Entry.ToString(), m_FunctionType);
                
                LLVMHelpers.AddAttributeForFunction(compiler.Module, m_Function, "nounwind");

                if (m_Method.Definition.ImplAttributes.HasFlag(MethodImplAttributes.NoInlining)) {
                    LLVMHelpers.AddAttributeForFunction(compiler.Module, m_Function, "noinline");
                }
                if (m_Method.Definition.ImplAttributes.HasFlag(MethodImplAttributes.AggressiveInlining)) {
                    LLVMHelpers.AddAttributeForFunction(compiler.Module, m_Function, "inlinehint");
                }
            }
            
            // static ctor
            if(method.Attributes.HasFlag(MethodAttributes.Static | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName)) {
                if (method.Entry.Name.Contains("ctor")) {
                    m_Compiler.StaticConstructorList.Add(this);
                }
            }

        }
        public LLVMBasicBlockRef GetLLVMBasicBlock(MethodBasicBlock basicBlock) => m_LLVMBasicBlocks[basicBlock.Interval];
        public void GeneratePrologue(IRGenerator irGenerator) {
            if (m_Method.Body == null) return;
            m_Method.MakeBasicBlocks();
            var builder = irGenerator.Builder;
            var basicBlocks = m_Method.BasicBlocks;
            var llvmBasicBlocks = m_LLVMBasicBlocks = new SortedList<Interval, LLVMBasicBlockRef>(new Interval.IntervalTreeComparer());
            var signature = m_Method.Signature;
            var isStatic = m_Method.Attributes.HasFlag(MethodAttributes.Static);

            var prologue = m_Function.AppendBasicBlock("Prologue");

            var idx = 0;
            foreach (var i in basicBlocks) {
                llvmBasicBlocks.Add(i.Key, m_Function.AppendBasicBlock($"Block{idx++}"));
            }
            builder.PositionAtEnd(prologue);


            m_LocalVarValues = new LLVMCompValue[m_Method.LocalVaribleTypes.Length];
            idx = 0;
            foreach (var i in m_Method.LocalVaribleTypes) {
                var llvmType = m_Compiler.ResolveLLVMInstanceType(i);
                if (i is PointerTypeEntry || m_Compiler.TypeEnvironment.ActiveTypes[i].IsValueType)llvmType.TypeTag |= LLVMTypeTag.StackAlloc;

                m_LocalVarValues[idx++] = LLVMCompValue.CreateValue(builder.BuildAlloca(llvmType.LLVMType),llvmType.TypeTag);
            }

            m_ParamValues = new LLVMCompValue[signature.ParameterTypes.Length + (isStatic ? 0 : 1)];
            idx = 0;
            foreach (var i in m_ParamType) {
                var paramValues = m_ParamValues[idx] = LLVMCompValue.CreateValue(builder.BuildAlloca(i.LLVMType),i.TypeTag);
                builder.BuildStore(m_Function.GetParam((uint)(idx++)), paramValues.Value);
            }


            builder.BuildBr(llvmBasicBlocks.First().Value);

        }
        public void AddMergePhiNode(IRGenerator irGenerator, MethodBasicBlock basicBlock, MethodBasicBlock predBasicBlock, LLVMCompValue[] predStack) {
            var builder = irGenerator.Builder;
            var predLLVMBlock = new LLVMBasicBlockRef[] { m_LLVMBasicBlocks[predBasicBlock.Interval] };
            var currentLLVMBlock = m_LLVMBasicBlocks[basicBlock.Interval];
            if (basicBlock.PreStack != null) {
                builder.PositionBefore(predLLVMBlock[0].LastInstruction);
                //builder.PositionAtEnd(predLLVMBlock[0]);
                for (var i = 0; i < basicBlock.PreStack.Length; i++) {
                    basicBlock.PreStack[i].Value.AddIncoming(new LLVMValueRef[] { predStack[i].TryCast(basicBlock.PreStack[i].Type,builder,m_Compiler).Value }, predLLVMBlock, 1);
                }
            } else {
                builder.PositionAtEnd(currentLLVMBlock);
                basicBlock.PreStack = predStack.Select(e => {
                    var node = builder.BuildPhi(e.Type.LLVMType);
                    node.AddIncoming(new LLVMValueRef[] { e.Value }, predLLVMBlock, 1);
                    return LLVMCompValue.CreateValue(node,e.Type);
                }).ToArray();
            }
        }
        public void GenerateCode(IRGenerator irGenerator) {
            var intrinsicFuncDef = m_Compiler.TryFindFunctionImpl(this);
            if (intrinsicFuncDef != null) {
                intrinsicFuncDef.FillFunctionBody(this, irGenerator.Builder);

            }
            if (m_Method.Body == null) return;

            RunStackConsistencyCheck();
            foreach (var i in m_Method.BasicBlocks.Values) {
                if (i.FalseExit != null) {
                    i.FalseExit.Predecessor.Add(i);
                }
                if (i.TrueExit != null) {
                    foreach (var j in i.TrueExit) {
                        if (j != null && i.FalseExit != j) {
                            j.Predecessor.Add(i);
                        }
                    }
                }
                
                
            }
            foreach (var i in m_Method.BasicBlocks.Values) {
                if (i.Predecessor.Count == 0 || i.ExitStackDepth == i.StackDepthDelta) s_Queue.Enqueue(i);
            }
            while (s_Queue.Count != 0) {
                var current = s_Queue.Dequeue();
                s_EvalStack.Clear();
                var finalStack = irGenerator.GenerateForBasicBlock(current, this, current.PreStack != null ? new Stack<LLVMCompValue>(current.PreStack.Reverse()) : s_EvalStack);

                if (current.FalseExit != null) {
                    if (finalStack.Count != 0) AddMergePhiNode(irGenerator, current.FalseExit, current, finalStack.ToArray());
                    current.FalseExit.Predecessor.Remove(current);
                    if (current.FalseExit.Predecessor.Count == 0 && current.FalseExit.StackDepthDelta != current.FalseExit.ExitStackDepth) {
                        s_Queue.Enqueue(current.FalseExit);
                    }
                }
                if (current.TrueExit != null) {
                    foreach (var k in current.TrueExit) {
                        if (current.FalseExit != k) {
                            if (finalStack.Count != 0) AddMergePhiNode(irGenerator, k, current, finalStack.ToArray());
                            k.Predecessor.Remove(current);
                            if (k.Predecessor.Count == 0 && k.StackDepthDelta != k.ExitStackDepth) {
                                s_Queue.Enqueue(k);
                            }
                        }
                    }
                }
                
                
            }
        }

        public void RunStackConsistencyCheck() {
            if (m_Method.Body == null) return;
            var entryBlock = m_Method.BasicBlocks[new Interval(0, 0)];
            entryBlock.ExitStackDepth = entryBlock.StackDepthDelta;
            s_Queue.Enqueue(entryBlock);
            while (s_Queue.Count != 0) {
                var current = s_Queue.Dequeue();
                if (current.FalseExit != null) {
                    var branch = current.FalseExit;
                    if (branch.ExitStackDepth != int.MinValue) {
                        if (branch.ExitStackDepth != branch.StackDepthDelta + current.ExitStackDepth) {
                            throw new InvalidProgramException("Stack analysis fault");
                        }
                    } else {
                        branch.ExitStackDepth = branch.StackDepthDelta + current.ExitStackDepth;
                        if (branch.ExitStackDepth < 0) throw new InvalidProgramException("Stack underflow");
                        s_Queue.Enqueue(branch);
                    }
                }
                if (current.TrueExit != null) {
                    foreach (var j in current.TrueExit) {
                        if (j != current.FalseExit) {
                            var branch = j;
                            if (branch.ExitStackDepth != int.MinValue) {
                                if (branch.ExitStackDepth != branch.StackDepthDelta + current.ExitStackDepth) {
                                    throw new InvalidProgramException("Stack analysis fault");
                                }
                            } else {
                                branch.ExitStackDepth = branch.StackDepthDelta + current.ExitStackDepth;
                                if (branch.ExitStackDepth < 0) throw new InvalidProgramException("Stack underflow");
                                s_Queue.Enqueue(branch);
                            }
                        }
                    }
                }
                
                
            }
        }
        public override string ToString() {
            return m_Method.Entry.ToString();
        }
    }
    public class LLVMCompiler {
        protected TypeEnvironment m_TypeEnv;
        protected LLVMContextRef m_Context;
        protected LLVMModuleRef m_Module;
        protected LLVMBuilderRef m_Builder;
        protected LLVMBuilderRef m_EvalBuilder;
        protected LLVMExecutionEngineRef m_Evaluator;
        protected uint m_OptimizeIterationCount = 3;
        protected Random m_PrimaryRNG = new Random(1145141919);
        protected Dictionary<uint, LLVMTypeInfo> m_TypeHash = new Dictionary<uint, LLVMTypeInfo>();
        protected Dictionary<TypeEntry, LLVMTypeInfo> m_LLVMTypeList = new Dictionary<TypeEntry, LLVMTypeInfo>();
        protected Dictionary<MethodEntry, LLVMMethodInfoWrapper> m_LLVMMethodList = new Dictionary<MethodEntry, LLVMMethodInfoWrapper>();
        protected Dictionary<string, LLVMValueRef> m_GlobalStringPool = new Dictionary<string, LLVMValueRef>();
        protected Dictionary<LLVMValueRef, string> m_InvGlobalStringPool = new Dictionary<LLVMValueRef, string>();
        protected Dictionary<LLVMCompType, LLVMTypeInfo> m_InterfaceInstanceTypeMap = new Dictionary<LLVMCompType, LLVMTypeInfo>();
        protected LLVMTypeRef m_TypeMetadataType = LLVMTypeRef.Void;
        protected LLVMTypeRef m_InterfaceHeaderType = LLVMTypeRef.Void;

        protected List<LLVMMethodInfoWrapper> m_StaticConstructorList = new List<LLVMMethodInfoWrapper>();
        
        protected List<LLVMIntrinsicFunctionDef> m_IntrinsicFunction = new List<LLVMIntrinsicFunctionDef>() {
            new LLVMUnsafeDerefInvariant(),
            new LLVMUnsafeDerefInvariantIndex(),
            new LLVMPInvoke(),
            new LLVMUnsafeAsPtr(),
            new LLVMDelegate(),
            new LLVMRuntimeExport(),
            new LLVMVaList(),
            new LLVMRuntimeHelpersIntrinsic(),
        };

        public LLVMContextRef Context => m_Context;
        public LLVMModuleRef Module => m_Module;
        public TypeEnvironment TypeEnvironment => m_TypeEnv;
        public LLVMTypeRef TypeMetadataType => m_TypeMetadataType;
        public LLVMTypeRef InterfaceHeaderType => m_InterfaceHeaderType;
        public LLVMExecutionEngineRef Evaluator => m_Evaluator;
        public LLVMBuilderRef EvalBuilder => m_EvalBuilder;

        public List<LLVMMethodInfoWrapper> StaticConstructorList => m_StaticConstructorList;
        


        public string GetInternedString(LLVMValueRef value) {
            if (!m_InvGlobalStringPool.ContainsKey(value)) return null;
            return m_InvGlobalStringPool[value];
        }
        public uint RegisterTypeInfo(LLVMTypeInfo typeInfoObj) {
            var hash = (uint)m_PrimaryRNG.Next();
            while (m_TypeHash.ContainsKey(hash)) hash = (uint)m_PrimaryRNG.Next();
            m_TypeHash.Add(hash, typeInfoObj);
            return hash;
        }
        public LLVMValueRef InternString(string s) {
            
            if (!m_GlobalStringPool.ContainsKey(s)) {
                var stringType = (LLVMClassTypeInfo)ResolveLLVMType(m_TypeEnv.IntrinicsTypes["System::String"]);
                var stringPtr = m_Module.Context.GetConstString(s,false);
                
                var globalStringPtr = m_Module.AddGlobal(stringPtr.TypeOf, $"SC_Body{m_GlobalStringPool.Count}");
                globalStringPtr.Initializer = stringPtr;
                globalStringPtr.IsGlobalConstant = true;

                var globalPtr = m_Module.AddGlobal(stringType.ReferenceType.LLVMType.ElementType, $"SC@{m_GlobalStringPool.Count}");
                globalPtr.Initializer = LLVMValueRef.CreateConstNamedStruct(stringType.ReferenceType.LLVMType.ElementType, new LLVMValueRef[] {
                    stringType.VtableBody,
                    LLVMValueRef.CreateConstNamedStruct(stringType.DataStorageType,new LLVMValueRef[]{
                        LLVMValueRef.CreateConstNull(stringType.BaseType.DataStorageType),
                        LLVMValueRef.CreateConstBitCast(globalStringPtr,LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8,0)),
                        LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32,(ulong)s.Length),
                    })
                }); ;

                globalPtr.IsGlobalConstant = true;

                var stringHeapPtr = LLVMValueRef.CreateConstGEP(globalPtr, new LLVMValueRef[] {
                    LLVMHelpers.CreateConstU32(0),
                    LLVMHelpers.CreateConstU32(1)
                });

                m_GlobalStringPool.Add(s, stringHeapPtr);
                m_InvGlobalStringPool.Add(stringHeapPtr, s);
            }
            return m_GlobalStringPool[s];
        }
        public LLVMTypeInfo ResolveLLVMType(TypeEntry entry) {
            return m_LLVMTypeList[entry];
        }
        public LLVMTypeInfo ResolveDepLLVMType(TypeEntry entry) {
            if (entry is PointerTypeEntry ptrEntry) {
                return ResolveDepLLVMType(ptrEntry.ElementEntry);
            }
            return m_LLVMTypeList[entry];
        }
        public LLVMCompType ResolveLLVMInstanceType(TypeEntry entry) {
            if (entry is PointerTypeEntry ptrEntry) {
                if (ptrEntry.ElementEntry.ToString() == "System::Void") return LLVMCompType.Int8.ToPointerType();
                return LLVMCompType.CreateType(LLVMTypeTag.Pointer,LLVMTypeRef.CreatePointer(ResolveLLVMInstanceType(((PointerTypeEntry)entry).ElementEntry).LLVMType, 0));
            }
            return m_LLVMTypeList[entry].InstanceType;
        }
        public LLVMTypeInfo ResolveLLVMTypeFromTypeRef(LLVMCompType compType) {
            return m_InterfaceInstanceTypeMap[compType];
        }

        public LLVMMethodInfoWrapper ResolveLLVMMethod(MethodEntry entry) => m_LLVMMethodList[entry];

        public LLVMMethodInfoWrapper ResolveLLVMMethod(EntityHandle token, MetadataReader reader, IGenericParameterContext context, out LLVMTypeInfo declType) {
            var methodEntry = m_TypeEnv.ResolveMethodByHandle(token, reader, context, out var declEntry, out _);
            declType = m_LLVMTypeList[declEntry];
            return m_LLVMMethodList[methodEntry];
        }
        public LLVMTypeInfo ResolveLLVMType(EntityHandle token, MetadataReader reader, IGenericParameterContext context) {
            return m_LLVMTypeList[m_TypeEnv.ResolveTypeByHandle(token, reader, context)];
        }
        public LLVMIntrinsicFunctionDef TryFindFunctionImpl(LLVMMethodInfoWrapper method) {
            foreach(var i in m_IntrinsicFunction) {
                if (i.MatchFunction(method)) return i;
            }
            return null;
        }
        static LLVMCompiler() {
            LLVM.InitializeX86Target();
            LLVM.InitializeX86AsmPrinter();
            LLVM.InitializeX86TargetInfo();
            LLVM.InitializeX86TargetMC();
            LLVM.InitializeX86AsmParser();
            
        }

        public LLVMCompiler(TypeEnvironment typeEnv, string name) {
            //var targetArch = LLVMTargetRef.First.CreateTargetMachine(LLVMTargetRef.DefaultTriple, "znver3", "", LLVMCodeGenOptLevel.LLVMCodeGenLevelDefault, LLVMRelocMode.LLVMRelocDefault, LLVMCodeModel.LLVMCodeModelDefault);
            m_TypeEnv = typeEnv;
            m_Module = LLVMModuleRef.CreateWithName(name);
            //m_Module.Target = "x86_64-pc-windows-coff";
            m_Evaluator = m_Module.CreateInterpreter();
            m_Context = m_Module.Context;
            m_Builder = LLVMBuilderRef.Create(m_Context);
            m_EvalBuilder = LLVMBuilderRef.Create(m_Context);
            m_TypeMetadataType = m_Module.Context.CreateNamedStruct("TypeMetadata");
            m_TypeMetadataType.StructSetBody(new LLVMTypeRef[] {
                //LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8,0),
                LLVMTypeRef.Int32,
                LLVMTypeRef.Int32,
                LLVMTypeRef.Int32
            }, false);
            m_InterfaceHeaderType = m_Module.Context.CreateNamedStruct("InterfaceHeader");
            m_InterfaceHeaderType.StructSetBody(new LLVMTypeRef[] {
                LLVMTypeRef.Int16,
                LLVMTypeRef.Int16,
                LLVMTypeRef.Int32,
            }, false);
        }

        public unsafe void BuildAssembly() {

            var irGenerator = new IRGenerator(m_Builder);
            foreach (var i in m_TypeEnv.ActiveTypes) {
                if (i.Key.ToString() == "::<Module>") continue;
                m_LLVMTypeList.Add(i.Key, LLVMWrapperFactory.CreateLLVMType(this, i.Value));
            }
            foreach (var i in m_LLVMTypeList)
                i.Value.ProcessDependence();
            foreach (var i in m_LLVMTypeList) {
                var instanceType = i.Value.SetupLLVMTypes();
                if (i.Value.MetadataType.Attribute.HasFlag(TypeAttributes.Interface)) {
                    m_InterfaceInstanceTypeMap.Add(instanceType, i.Value);
                }
            }
                


            foreach (var i in m_TypeEnv.ActiveMethods) {

                m_LLVMMethodList.Add(i.Key, new LLVMMethodInfoWrapper(this, i.Value));

            }
            foreach (var i in m_LLVMMethodList) {
                if (i.Value.ToString().Contains("FP")) Debugger.Break();
                if (!i.Value.Method.Attributes.HasFlag(MethodAttributes.Abstract)) {
                    i.Value.GeneratePrologue(irGenerator);
                }
            }
            foreach (var i in m_LLVMTypeList) i.Value.GenerateVTable();

            foreach (var i in m_LLVMMethodList) {
                if (!i.Value.Method.Attributes.HasFlag(MethodAttributes.Abstract)) {
                    i.Value.GenerateCode(irGenerator);
                }
            }

            

        }
        [DllImport("libLLVM")]
        public unsafe static extern void LLVMPassManagerBuilderSetOptLevel(LLVMOpaquePassManagerBuilder* op, uint level);
        public unsafe void PostProcess() {

            var cpm = m_Module.CreateFunctionPassManager();
            cpm.AddVerifierPass();


            cpm.AddEarlyCSEPass();
            cpm.AddSCCPPass();
            cpm.AddCFGSimplificationPass();
            cpm.AddScalarReplAggregatesPass();
            cpm.AddMergedLoadStoreMotionPass();
            cpm.AddCorrelatedValuePropagationPass();
            cpm.AddMergedLoadStoreMotionPass();
            cpm.AddInstructionCombiningPass();
            cpm.AddReassociatePass();
            cpm.AddGVNPass();
            cpm.AddInstructionCombiningPass();
            cpm.AddGVNPass();
            

            cpm.InitializeFunctionPassManager();


            var mpm = (LLVMPassManagerRef)LLVM.CreatePassManager();
            var fpm = m_Module.CreateFunctionPassManager();
            var builder = (LLVMPassManagerBuilderRef)LLVM.PassManagerBuilderCreate();
            LLVMPassManagerBuilderSetOptLevel(builder, 3);

            builder.UseInlinerWithThreshold(10);

            mpm.AddVerifierPass();
            mpm.AddAlwaysInlinerPass();

            

            builder.PopulateFunctionPassManager(fpm);
            builder.PopulateModulePassManager(mpm);

            foreach (var j in m_LLVMMethodList) if (j.Value.Method.Body != null) {
                    cpm.RunFunctionPassManager(j.Value.Function);
                }

            for (var i = 0; i < m_OptimizeIterationCount; i++) {
                foreach (var j in m_LLVMMethodList) 
                    if (j.Value.Method.Body != null) {
                        
                        fpm.RunFunctionPassManager(j.Value.Function);
                    }
                mpm.Run(m_Module);

            }
            

        }
        public string PrintIRCode() {
            return m_Module.PrintToString();
        }
        public unsafe void GenerateBinary(string fileName) {

            var targetArch = LLVMTargetRef.First.CreateTargetMachine(LLVMTargetRef.DefaultTriple, "znver3", "", LLVMCodeGenOptLevel.LLVMCodeGenLevelAggressive, LLVMRelocMode.LLVMRelocDefault, LLVMCodeModel.LLVMCodeModelDefault);
            targetArch.EmitToFile(m_Module, fileName, LLVMCodeGenFileType.LLVMObjectFile);
            targetArch.EmitToFile(m_Module, Path.ChangeExtension(fileName,"asm"), LLVMCodeGenFileType.LLVMAssemblyFile);
        }

    }
    
    public abstract class LLVMCompileTimeFunctionDef {
        public abstract bool MatchFunction(LLVMMethodInfoWrapper method);
        public abstract void EmitInst(LLVMMethodInfoWrapper context,Stack<LLVMCompValue> evalStack, LLVMBuilderRef builder);
    }

    public abstract class LLVMIntrinsicFunctionDef {
        public abstract bool MatchFunction(LLVMMethodInfoWrapper method);
        public abstract void FillFunctionBody(LLVMMethodInfoWrapper method,LLVMBuilderRef builder);
    }
    public class LLVMUnsafeDerefInvariant : LLVMIntrinsicFunctionDef {
        public override void FillFunctionBody(LLVMMethodInfoWrapper method, LLVMBuilderRef builder) {
            var mainBlock = method.Function.AppendBasicBlock("Block0");
            builder.PositionAtEnd(mainBlock);
            var val = builder.BuildLoad(method.Function.GetParam(0));
            LLVMHelpers.AddMetadataForInst(val, "invariant.load", new LLVMValueRef[] { });
            builder.BuildRet(val);
        }

        public override bool MatchFunction(LLVMMethodInfoWrapper method) {
            return method.Method.Entry.ToString().StartsWith("System.Runtime.CompilerServices::Unsafe.DereferenceInvariant<");
        }
    }
    public class LLVMUnsafeDerefInvariantIndex : LLVMIntrinsicFunctionDef {
        public override void FillFunctionBody(LLVMMethodInfoWrapper method, LLVMBuilderRef builder) {
            var mainBlock = method.Function.AppendBasicBlock("Block0");
            builder.PositionAtEnd(mainBlock);
            var parma0 = method.Function.GetParam(0);
            var ptrAddr = builder.BuildGEP(parma0, new LLVMValueRef[] { method.Function.GetParam(1) });
            var val = builder.BuildLoad(ptrAddr);
            LLVMHelpers.AddMetadataForInst(val, "invariant.load",new LLVMValueRef[] { });
            builder.BuildRet(val);
        }

        public override bool MatchFunction(LLVMMethodInfoWrapper method) {
            return method.Method.Entry.ToString().StartsWith("System.Runtime.CompilerServices::Unsafe.DereferenceInvariantIndex<");
        }
    }
    public class LLVMUnsafeAsPtr : LLVMIntrinsicFunctionDef {
        public override void FillFunctionBody(LLVMMethodInfoWrapper method, LLVMBuilderRef builder) {
            var mainBlock = method.Function.AppendBasicBlock("Block0");
            builder.PositionAtEnd(mainBlock);
            
            var val = builder.BuildPtrToInt(method.Function.GetParam(0), LLVMTypeRef.Int64);
            builder.BuildRet(val);
        }

        public override bool MatchFunction(LLVMMethodInfoWrapper method) {
            return method.Method.Entry.ToString().StartsWith("System.Runtime.CompilerServices::Unsafe.AsPtr<");
        }
    }
    public class LLVMPInvoke : LLVMIntrinsicFunctionDef {
        public override void FillFunctionBody(LLVMMethodInfoWrapper method, LLVMBuilderRef builder) {
            var llvmFunction = method.Function;
            llvmFunction.Name = method.Method.Entry.Name;
            llvmFunction.Linkage = LLVMLinkage.LLVMExternalLinkage;
            llvmFunction.FunctionCallConv = (uint)LLVMCallConv.LLVMWin64CallConv;
        }

        public override bool MatchFunction(LLVMMethodInfoWrapper method) {
            return method.Method.Attributes.HasFlag(MethodAttributes.PinvokeImpl);
        }
    }
    public class LLVMRuntimeExport : LLVMIntrinsicFunctionDef {
        public override void FillFunctionBody(LLVMMethodInfoWrapper method, LLVMBuilderRef builder) {
            var value = method.Method.CustomAttributes[(method.Context.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::RuntimeExport"])].DecodeValue(method.Context.TypeEnvironment.SignatureDecoder);
            var llvmFunction = method.Function;
            llvmFunction.Name = (string)value.FixedArguments[0].Value;
        }

        public override bool MatchFunction(LLVMMethodInfoWrapper method) {
            return method.Method.CustomAttributes.ContainsKey(method.Context.TypeEnvironment.IntrinicsTypes["System.Runtime.CompilerServices::RuntimeExport"]);
        }
    }
    public class LLVMDelegate : LLVMIntrinsicFunctionDef {
        public override void FillFunctionBody(LLVMMethodInfoWrapper method, LLVMBuilderRef builder) {
            var delegateType = (LLVMClassTypeInfo)method.Context.ResolveLLVMType(method.Context.TypeEnvironment.IntrinicsTypes["System::Delegate"]);
            switch (method.Method.Entry.Name) {
                case "Invoke": {
                    var basicBlock = method.Function.AppendBasicBlock("Block0");
                    builder.PositionAtEnd(basicBlock);

                    var delegateDataPtr = builder.BuildBitCast(method.Function.GetParam(0), delegateType.HeapPtrType.LLVMType);
                    var instancePtr = builder.BuildGEP(delegateDataPtr, new LLVMValueRef[] {
                        LLVMHelpers.CreateConstU32(0),
                        LLVMHelpers.CreateConstU32(1)
                    });
                    var instance = builder.BuildLoad(instancePtr);
                    var funcPtr = builder.BuildGEP(delegateDataPtr, new LLVMValueRef[] {
                        LLVMHelpers.CreateConstU32(0),
                        LLVMHelpers.CreateConstU32(2)
                    });
                    var funcValue = builder.BuildLoad(funcPtr);
                    var targetDelegateFunction = builder.BuildBitCast(funcValue, method.FunctionPtrType);
                    var argumentList = new LLVMValueRef[method.ParamCount];
                    for (var i = 1u; i < method.ParamCount; i++) argumentList[i] = method.Function.GetParam(i);
                    argumentList[0] = builder.BuildBitCast(instance, method.ParamTypes[0].LLVMType);

                    var result = builder.BuildCall2(targetDelegateFunction.TypeOf.ElementType,targetDelegateFunction, argumentList);
                    if (method.ReturnType.LLVMType != LLVMTypeRef.Void) {
                        builder.BuildRet(result);
                    } else {
                        builder.BuildRetVoid();
                    }
                    break;
                }
                case ".ctor": {

                    var basicBlock = method.Function.AppendBasicBlock("Block0");
                    builder.PositionAtEnd(basicBlock);

                    var delegateDataPtr = builder.BuildBitCast(method.Function.GetParam(0), delegateType.HeapPtrType.LLVMType);
                    var instancePtr = builder.BuildGEP(delegateDataPtr, new LLVMValueRef[] {
                        LLVMHelpers.CreateConstU32(0),
                        LLVMHelpers.CreateConstU32(1)
                    });
                    builder.BuildStore(method.Function.GetParam(1), instancePtr);
                    var funcPtr = builder.BuildGEP(delegateDataPtr, new LLVMValueRef[] {
                        LLVMHelpers.CreateConstU32(0),
                        LLVMHelpers.CreateConstU32(2)
                    });
                    builder.BuildStore(method.Function.GetParam(2), funcPtr);
                    builder.BuildRetVoid();
                    break;
                }
                default: {
                    throw new NotImplementedException();
                }
            }
            
        }

        public override bool MatchFunction(LLVMMethodInfoWrapper method) {
            return method.Method.DeclType.BaseType?.Entry == method.Method.TypeEnv.IntrinicsTypes["System::MulticastDelegate"];
        }
    }

    public class LLVMVaList : LLVMIntrinsicFunctionDef {
        public override void FillFunctionBody(LLVMMethodInfoWrapper method, LLVMBuilderRef builder) {
            var delegateType = (LLVMClassTypeInfo)method.Context.ResolveLLVMType(method.Context.TypeEnvironment.IntrinicsTypes["System::Delegate"]);
            switch (method.Method.Entry.Name) {
                case "GetList": {
                    var basicBlock = method.Function.AppendBasicBlock("Block0");
                    builder.PositionAtEnd(basicBlock);

                    var dstPtr = method.Function.GetParam(1);
                    var srcValue = builder.BuildBitCast(method.Function.GetParam(0),dstPtr.TypeOf);
                    
                    var copyFunc = LLVMHelpers.GetIntrinsicFunction(method.Context.Module, "llvm.va_copy", new LLVMTypeRef[] {
                    });
                    var i8p = LLVMCompType.Int8.ToPointerType().LLVMType;
                    builder.BuildCall2(copyFunc.TypeOf.ElementType, copyFunc, new LLVMValueRef[] { 
                        builder.BuildBitCast(dstPtr,i8p),
                        builder.BuildBitCast(srcValue,i8p)
                    });
                    builder.BuildRetVoid();
                    break;
                }
                case "GetNextValue": {

                    var basicBlock = method.Function.AppendBasicBlock("Block0");
                    builder.PositionAtEnd(basicBlock);


                    var ptrList = method.Function.GetParam(0);
                    var valType = method.ReturnType.LLVMType;
                    var value = builder.BuildVAArg(ptrList, valType);
                    builder.BuildRet(value);
                    break;
                }
                case "End": {
                    var basicBlock = method.Function.AppendBasicBlock("Block0");
                    builder.PositionAtEnd(basicBlock);

                    LLVMHelpers.AddAttributeForFunction(method.Context.Module, method.Function, "alwaysinline");

                    var ptrList = method.Function.GetParam(0);
                    var endVaFunc = LLVMHelpers.GetIntrinsicFunction(method.Context.Module, "llvm.va_end", new LLVMTypeRef[] { 
                    });
                    var i8p = LLVMCompType.Int8.ToPointerType().LLVMType;
                    builder.BuildCall(endVaFunc, new LLVMValueRef[] { 
                        builder.BuildBitCast(ptrList,i8p)
                    });
                    builder.BuildRetVoid();
                    break;
                }
                default: {
                    throw new NotImplementedException();
                }
            }

        }

        public override bool MatchFunction(LLVMMethodInfoWrapper method) {
            return method.Method.DeclType.Entry == method.Method.TypeEnv.IntrinicsTypes["System::RuntimeArgumentHandle"]
                || method.Method.DeclType.Entry == method.Method.TypeEnv.IntrinicsTypes["System::RuntimeVaList"];
        }
    }
    public class LLVMRuntimeHelpersIntrinsic : LLVMIntrinsicFunctionDef {
        public override void FillFunctionBody(LLVMMethodInfoWrapper method, LLVMBuilderRef builder) {
            var basicBlock = method.Function.AppendBasicBlock("Block0");
            builder.PositionAtEnd(basicBlock);

            var staticCtorList = method.Context.StaticConstructorList;
            foreach(var i in staticCtorList) {
                builder.BuildCall(i.Function, new LLVMValueRef[] { });
            }
            builder.BuildRetVoid();

        }

        public override bool MatchFunction(LLVMMethodInfoWrapper method) {
            return method.Method.Entry.ToString().StartsWith("System.Runtime.CompilerServices::RuntimeHelpers.RunStaticConstructors");
        }
    }



}
