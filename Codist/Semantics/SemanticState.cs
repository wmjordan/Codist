using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;

namespace Codist
{
	sealed class SemanticState
	{
		CompilationUnitSyntax _CompilationUnit;

		public readonly Workspace Workspace;
		public readonly SemanticModel Model;
		public readonly ITextSnapshot Snapshot;
		public readonly Document Document;

		public SemanticState(Workspace workspace, SemanticModel model, ITextSnapshot snapshot, Document document) {
			Workspace = workspace;
			Model = model;
			Snapshot = snapshot;
			Document = document;
		}

		public Task<ISymbol> GetSymbolAsync(int position, CancellationToken cancellationToken = default) {
			return Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindSymbolAtPositionAsync(Model, position, Workspace, cancellationToken);
		}

		public CompilationUnitSyntax GetCompilationUnit(CancellationToken cancellationToken = default) {
			return _CompilationUnit ?? (_CompilationUnit = Model.SyntaxTree.GetCompilationUnitRoot(cancellationToken));
		}
	}
}