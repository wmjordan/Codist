using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist
{
	sealed class SemanticState
	{
		Document _Document;
		CompilationUnitSyntax _CompilationUnit;

		public readonly Workspace Workspace;
		public readonly SemanticModel Model;
		public readonly ITextSnapshot Snapshot;
		public readonly DocumentId DocumentId;

		public SemanticState(Workspace workspace, SemanticModel model, ITextSnapshot snapshot, DocumentId documentId) {
			Workspace = workspace;
			Model = model;
			Snapshot = snapshot;
			DocumentId = documentId;
		}

		public Document GetDocument() {
			return _Document ?? (_Document = Workspace.CurrentSolution.GetDocument(DocumentId));
		}

		public Task<ISymbol> GetSymbolAsync(int position, CancellationToken cancellationToken = default) {
			return Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindSymbolAtPositionAsync(GetDocument(), position, cancellationToken);
		}

		public CompilationUnitSyntax GetCompilationUnit(CancellationToken cancellationToken = default) {
			return _CompilationUnit ?? (_CompilationUnit = Model.SyntaxTree.GetCompilationUnitRoot(cancellationToken));
		}
	}
}