using Liella.Metadata;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using LLVMInterop = LLVMSharp.Interop.LLVM;

namespace Liella.Compiler.LLVM {
    public class LLVMCompiler {
        protected CompilationUnitSet m_TypeEnv;
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
        public CompilationUnitSet TypeEnvironment => m_TypeEnv;
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
                var stringPtr = m_Module.Context.GetConstString(s, false);

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
                return LLVMCompType.CreateType(LLVMTypeTag.Pointer, LLVMTypeRef.CreatePointer(ResolveLLVMInstanceType(((PointerTypeEntry)entry).ElementEntry).LLVMType, 0));
            }
            return m_LLVMTypeList[entry].InstanceType;
        }
        public LLVMTypeInfo ResolveLLVMTypeFromTypeRef(LLVMCompType compType) {
            return m_InterfaceInstanceTypeMap[compType];
        }

        public LLVMMethodInfoWrapper ResolveLLVMMethod(MethodEntry entry) => m_LLVMMethodList[entry];

        public LLVMTypeInfo ResolveLLVMType(EntityHandle token, MethodInstance context) {
            return m_LLVMTypeList[context.ResolveTypeToken(token)];
        }
        public LLVMIntrinsicFunctionDef TryFindFunctionImpl(LLVMMethodInfoWrapper method) {
            foreach (var i in m_IntrinsicFunction) {
                if (i.MatchFunction(method)) return i;
            }
            return null;
        }
        static LLVMCompiler() {
            LLVMInterop.InitializeX86Target();
            LLVMInterop.InitializeX86AsmPrinter();
            LLVMInterop.InitializeX86TargetInfo();
            LLVMInterop.InitializeX86TargetMC();
            LLVMInterop.InitializeX86AsmParser();

        }

        public LLVMCompiler(CompilationUnitSet typeEnv, string name) {
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
                if (i.Value.MetadataType.Attributes.HasFlag(TypeAttributes.Interface)) {
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


            var mpm = (LLVMPassManagerRef)LLVMInterop.CreatePassManager();
            var fpm = m_Module.CreateFunctionPassManager();
            var builder = (LLVMPassManagerBuilderRef)LLVMInterop.PassManagerBuilderCreate();
            LLVMPassManagerBuilderSetOptLevel(builder, 3);

            builder.UseInlinerWithThreshold(10);

            mpm.AddVerifierPass();
            mpm.AddAlwaysInlinerPass();



            builder.PopulateFunctionPassManager(fpm);
            builder.PopulateModulePassManager(mpm);

            foreach (var j in m_LLVMMethodList) if (!j.Value.Method.IsDummy) {
                    cpm.RunFunctionPassManager(j.Value.Function);
                }

            for (var i = 0; i < m_OptimizeIterationCount; i++) {
                foreach (var j in m_LLVMMethodList)
                    if (!j.Value.Method.IsDummy) {

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
            targetArch.EmitToFile(m_Module, Path.ChangeExtension(fileName, "asm"), LLVMCodeGenFileType.LLVMAssemblyFile);
        }

    }

}
