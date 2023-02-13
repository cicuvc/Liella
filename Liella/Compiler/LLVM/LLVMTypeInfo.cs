using Liella.Metadata;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Liella.Compiler.LLVM {
    public abstract class LLVMTypeInfo {
        protected LLVMCompiler m_Compiler;
        protected LiTypeInfo m_MetadataType;
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
        public LiTypeInfo MetadataType => m_MetadataType;
        public LLVMValueRef StaticStorage => m_StaticStorageBody;
        public HashSet<LLVMInterfaceTypeInfo> Interfaces => m_Interfaces;
        public LLVMClassTypeInfo BaseType => m_BaseType;
        public abstract ulong DataStorageSize { get; }

        protected LLVMTypeInfo(LLVMCompiler compiler, LiTypeInfo typeInfo) {
            m_Compiler = compiler;
            m_MetadataType = typeInfo;
            m_TypeName = typeInfo.Entry.ToString();

            m_StaticStorageType = m_Compiler.Context.CreateNamedStruct($"static.{m_TypeName}");
            m_StaticStorageBody = m_Compiler.Module.AddGlobal(m_StaticStorageType, $"static.val.{m_TypeName}");
            m_StaticStorageBody.Initializer = LLVMValueRef.CreateConstNull(m_StaticStorageType);
            m_VtableType = m_Compiler.Context.CreateNamedStruct($"vt.{m_TypeName}");
            if (!typeInfo.Attributes.HasFlag(TypeAttributes.Interface))
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

}
