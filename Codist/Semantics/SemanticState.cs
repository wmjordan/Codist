using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Text;

namespace Codist;

sealed class SemanticState(Workspace workspace, SemanticModel model, ITextSnapshot snapshot, Document document)
{
	CompilationUnitSyntax _CompilationUnit;

	public readonly Workspace Workspace = workspace;
	public readonly SemanticModel Model = model;
	public readonly ITextSnapshot Snapshot = snapshot;
	public readonly Document Document = document;

	public Task<ISymbol> GetSymbolAsync(int position, CancellationToken cancellationToken = default) {
		return Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindSymbolAtPositionAsync(Model, position, Workspace, cancellationToken);
	}

	public CompilationUnitSyntax GetCompilationUnit(CancellationToken cancellationToken = default) {
		return _CompilationUnit ??= Model.SyntaxTree.GetCompilationUnitRoot(cancellationToken);
	}
}