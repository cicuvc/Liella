using Liella.Compiler;
using Liella.Compiler.LLVM;
using Liella.Image;
using Liella.Metadata;
using Liella.Project;
using LLVMSharp.Interop;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Liella {
    
    public class App {
        public unsafe static void LoadObj() {
            var obj = MemoryMappedFile.CreateFromFile("./loader_elf.obj");
            var view = obj.CreateViewAccessor();
            byte* ptr = null;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            var elfModule = new ELFReader(ptr, (int)view.SafeMemoryMappedViewHandle.ByteLength);
            elfModule.ReadFile();

            view.Dispose();
            obj.Dispose();
        }
        class Vis0: CSharpSyntaxRewriter {
            public override Microsoft.CodeAnalysis.SyntaxNode VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node) {
                return base.VisitLocalDeclarationStatement(node);
            }
            public override Microsoft.CodeAnalysis.SyntaxNode VisitLineSpanDirectiveTrivia(LineSpanDirectiveTriviaSyntax node) {
                return base.VisitLineSpanDirectiveTrivia(node);
            }
            public override Microsoft.CodeAnalysis.SyntaxNode VisitLineDirectiveTrivia(LineDirectiveTriviaSyntax node) {
                return base.VisitLineDirectiveTrivia(node);
            }
            public override Microsoft.CodeAnalysis.SyntaxNode VisitXmlElement(XmlElementSyntax node) {
                return base.VisitXmlElement(node);
            }
            public override Microsoft.CodeAnalysis.SyntaxNode VisitBlock(BlockSyntax node) {
                return base.VisitBlock(node);
            }
        }
        static SyntaxTrivia EmptyTrivia(SyntaxTrivia t1, SyntaxTrivia t2) {
            if(t1.IsKind(SyntaxKind.MultiLineCommentTrivia) || t1.IsKind(SyntaxKind.SingleLineCommentTrivia)) {
                return default;
            } else {
                return t1;
            }
        }
        public unsafe static void Main() {
            

            //CSharpProject.CollectSources();

            //LoadObj();
            //System.Environment.Exit(0);

            var envPath = System.Environment.GetEnvironmentVariable("Path");
            envPath += ";./llvm/";
            System.Environment.SetEnvironmentVariable("Path", envPath);

            var stream = new FileStream("./EFILoader.dll", FileMode.Open,FileAccess.Read);
            var auxLib = new FileStream("./FrameworkLib.dll", FileMode.Open, FileAccess.Read);
            var typeEnv = new ImageCompilationUnitSet(stream, new string[] {
                "System.Runtime.CompilerServices::RuntimeHelpers",
                "System.Runtime.CompilerServices::UnsafeAsm",
                "System::String",
                "System::IntPtr",
                "System::Delegate",
                "System.Runtime.InteropServices::DllImportAttribute",
                "System::Object",
                "System::MulticastDelegate",
                "System.Runtime.CompilerServices::RuntimeExport",
                "System::Enum",
                "System::RuntimeArgumentHandle",
                "System::RuntimeVaList"
            });
            typeEnv.AddReference(auxLib);
            typeEnv.LoadTypes();
            typeEnv.CollectTypes();

            Console.WriteLine("###### Collected Types ######");
            foreach (var i in typeEnv.ActiveTypes.Keys) Console.WriteLine(i.ToString());

            Console.WriteLine("###### Collected Methods ######");
            foreach (var i in typeEnv.ActiveMethods.Keys) Console.WriteLine(i.ToString());

            var compiler = new LLVMCompiler(typeEnv, "payload");

            compiler.BuildAssembly();

            var irCode = compiler.PrintIRCode();
            Console.WriteLine("=========Start of IR Section==========");
            Console.WriteLine(irCode);
            Console.WriteLine("=========End of IR Section==========");

            compiler.PostProcess();

            File.WriteAllText("payload.ll", compiler.PrintIRCode());
            compiler.Module.WriteBitcodeToFile("./payload.bc");
            compiler.GenerateBinary("./payload.obj");

            nuint len = 0;
            var linker = LTOCodeGenCreate();
            LTOCodeGenSetCPU(linker, (sbyte*)Marshal.StringToHGlobalAnsi("znver3"));
            
            var lnkModule = LTOModuleCreate((sbyte*)Marshal.StringToHGlobalAnsi("./payload.bc"));
            
            
            LTOCodeGenSetDebugModel(linker, lto_debug_model.LTO_DEBUG_MODEL_DWARF);
            LTOCodeGenSetPICModel(linker, lto_codegen_model.LTO_CODEGEN_PIC_MODEL_DYNAMIC);
            LTOModuleSetTargetTriple(lnkModule, (sbyte*)Marshal.StringToHGlobalAnsi("x86_64-pc-none-eabi"));
            LTOCodeGenAddModule(linker, lnkModule);
            LTOCodeGenAddExportSymbol(linker, (sbyte*)Marshal.StringToHGlobalAnsi("EFILoader::App.EfiMain"));
            LTOCodeGenAddExportSymbol(linker, (sbyte*)Marshal.StringToHGlobalAnsi("__chkstk"));
            //lto_codegen_add_must_preserve_symbol(linker, (sbyte*)Marshal.StringToHGlobalAnsi("WriteFile"));
            //lto_codegen_optimize(linker);
            var compileResult = LTOCodeGenCompile(linker, &len);

            
            
            
            //
            var data = new byte[len];
            Marshal.Copy((IntPtr)compileResult, data, 0, (int)len);

            var memory = new ReadOnlyMemory<byte>(data);
            //reader.ParseFile();
            

            File.WriteAllBytes("./loader_elf.obj", data);



            Console.WriteLine("Complete");
            Console.ReadLine();
        }
        [DllImport("LTO", EntryPoint = "lto_codegen_set_debug_model", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private unsafe static extern void LTOCodeGenSetDebugModel(LLVMOpaqueLTOCodeGenerator* cg, lto_debug_model model);
        [DllImport("LTO", EntryPoint = "lto_codegen_set_pic_model", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private unsafe static extern void LTOCodeGenSetPICModel(LLVMOpaqueLTOCodeGenerator* cg, lto_codegen_model model);
        [DllImport("LTO", EntryPoint = "lto_module_set_target_triple", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private unsafe static extern void LTOModuleSetTargetTriple(LLVMOpaqueLTOModule* cg, sbyte* symbol);
        [DllImport("LTO", EntryPoint = "lto_codegen_set_cpu", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private unsafe static extern void LTOCodeGenSetCPU(LLVMOpaqueLTOCodeGenerator* cg, sbyte* symbol);
        [DllImport("LTO", EntryPoint = "lto_codegen_add_must_preserve_symbol", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private unsafe static extern void LTOCodeGenAddExportSymbol(LLVMOpaqueLTOCodeGenerator* cg, sbyte* symbol);
        [DllImport("LTO", EntryPoint = "lto_module_get_symbol_name", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private unsafe static extern sbyte* LTOModuleGetSymbolName(LLVMOpaqueLTOModule* mod, uint index);
        [DllImport("LTO",EntryPoint = "lto_codegen_optimize", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private unsafe static extern byte LTOCodeGenOptimize(LLVMOpaqueLTOCodeGenerator* cg);
        

        [DllImport("LTO", EntryPoint = "lto_codegen_compile", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private unsafe static extern void* LTOCodeGenCompile(LLVMOpaqueLTOCodeGenerator* cg, nuint* length);

        [DllImport("LTO", EntryPoint = "lto_codegen_create", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private unsafe static extern LLVMOpaqueLTOCodeGenerator* LTOCodeGenCreate();
        [DllImport("LTO", EntryPoint = "lto_module_create", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private unsafe static extern LLVMOpaqueLTOModule* LTOModuleCreate(sbyte* path);
        [DllImport("LTO",EntryPoint = "lto_codegen_add_module", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private unsafe static extern byte LTOCodeGenAddModule(LLVMOpaqueLTOCodeGenerator* cg, LLVMOpaqueLTOModule* mod);

    }



}
