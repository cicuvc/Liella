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

    public class PrimaryVisitor : CSharpSyntaxVisitor<PrimaryModule> {
        public SemanticModel m_Model;
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
            foreach(var e in files) {
                syntaxTree.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(e.FullName), CSharpParseOptions.Default, e.FullName));
            }

            var compOpt = new CSharpCompilationOptions(OutputKind.NetModule, true, "framework");
            compOpt = compOpt
                .WithAllowUnsafe(true)
                .WithOptimizationLevel(Microsoft.CodeAnalysis.OptimizationLevel.Debug)
                .WithPlatform(Microsoft.CodeAnalysis.Platform.X64)
                .WithConcurrentBuild(true)
                .WithOverflowChecks(false);

            
            var emitOptions = new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb);
            emitOptions = emitOptions.WithSubsystemVersion(SubsystemVersion.None);

            var compilation = CSharpCompilation.Create("framework", syntaxTree, null, compOpt);

            var syt = syntaxTree[3];
            var smm = compilation.GetSemanticModel(syt);
            var vis = new PrimaryVisitor();
            vis.m_Model = smm;
            var vroot = syt.GetCompilationUnitRoot();
            vroot.Accept(vis);

        }
    }
    public class CSharpSolution {

    }
}
