using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace Liella.Project {
    public class PrimaryModule {

    }
    public class DummyVisL : CSharpSyntaxVisitor {

    }
    public class PrimaryVisitor : CSharpSyntaxVisitor<PrimaryModule> {
        private SemanticModel m_Model;
        public override PrimaryModule DefaultVisit(SyntaxNode node) {
            throw new NotImplementedException();
        }
        public override PrimaryModule VisitCompilationUnit(CompilationUnitSyntax node) {
           foreach(var i in node.ChildNodes()) {
                this.Visit(i);
           }
           return null;
        }
        public override PrimaryModule VisitClassDeclaration(ClassDeclarationSyntax node) {
            
            foreach (var i in node.ChildNodes()) {
                this.Visit(i);
            }
            


            return null;
        }
        public override PrimaryModule VisitPredefinedType(PredefinedTypeSyntax node) {
            return null;
        }
        public override PrimaryModule VisitMethodDeclaration(MethodDeclarationSyntax node) {
            var retType = m_Model.GetTypeInfo(node.ReturnType);
            var rtt= m_Model.GetSymbolInfo(node.ReturnType);
            var rType = m_Model.GetSpeculativeTypeInfo(node.ReturnType.SpanStart, node.ReturnType, SpeculativeBindingOption.BindAsTypeOrNamespace);
            var ccb = retType.Type;
            var mbs = ccb.GetMembers();

            
            foreach (var i in node.ChildNodes()) {
                this.Visit(i);
            }
            return null;
        }
        
        public override PrimaryModule VisitIdentifierName(IdentifierNameSyntax node) {
            return null;
        }
        public override PrimaryModule VisitNamespaceDeclaration(NamespaceDeclarationSyntax node) {
            foreach (var i in node.ChildNodes()) {
                this.Visit(i);
            }
            return null;
        }
        public override PrimaryModule VisitUsingDirective(UsingDirectiveSyntax node) {
            var usi = m_Model.GetSymbolInfo(node.Name);
            return null;
        }
    }
    public class CSharpProject {
        public static void EnumFiles(DirectoryInfo dir,List<FileInfo> files) {
            files.AddRange(dir.EnumerateFiles().Where(e=>Path.GetExtension(e.Name)==".cs"));
            var subDirs = dir.EnumerateDirectories();
            foreach(var i in subDirs) {
                EnumFiles(i, files);
            }
        }
        public static void CollectSources() {
            var path = "../FrameworkLib";
            var files = new List<FileInfo>();
            EnumFiles(new DirectoryInfo(path),files);
            var syntaxTree = new List<SyntaxTree>();
            var v0 = new DummyVisL();
            foreach (var e in files) {
                var srcFile = SourceText.From(File.ReadAllText(e.FullName), Encoding.UTF8);
                var tree = CSharpSyntaxTree.ParseText(srcFile, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp9), e.FullName);
                syntaxTree.Add(tree);

                tree.GetCompilationUnitRoot().Accept(v0);
            }

            var compOpt = new CSharpCompilationOptions(OutputKind.NetModule, true, "framework");
            compOpt = compOpt
                .WithAllowUnsafe(true)
                .WithOptimizationLevel(Microsoft.CodeAnalysis.OptimizationLevel.Debug)
                .WithPlatform(Microsoft.CodeAnalysis.Platform.X64)
                .WithConcurrentBuild(true)
                .WithNullableContextOptions(NullableContextOptions.Enable)
                .WithOverflowChecks(false)
                
                ;
            


            var emitOptions = new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb);
            emitOptions = emitOptions.WithSubsystemVersion(SubsystemVersion.None);
            emitOptions = emitOptions.WithRuntimeMetadataVersion("6.0")
                .WithDefaultSourceFileEncoding(Encoding.UTF8)
                
                .WithDebugInformationFormat(DebugInformationFormat.PortablePdb);
            
            var compilation = CSharpCompilation.Create("framework", syntaxTree, null, compOpt);

            var result = compilation.Emit("./framework.dll", "./framework.pdb");

            if (!result.Success) {
                foreach(var i in result.Diagnostics) {
                    Console.WriteLine(i);
                }
            }
            //vroot.Accept(vis);

        }
    }
    public class CSharpSolution {

    }
}
