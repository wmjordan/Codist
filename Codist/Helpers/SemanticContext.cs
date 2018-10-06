using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
	sealed class SemanticContext
	{
		readonly IWpfTextView _TextView;

		public SemanticContext(IWpfTextView textView) {
			_TextView = textView;
		}

		public Document Document { get; private set; }
		public SemanticModel SemanticModel { get; private set; }
		public CompilationUnitSyntax Compilation { get; private set; }
		public SyntaxNode Node { get; private set; }
		public SyntaxToken Token { get; private set; }
		public ISymbol Symbol { get; private set; }
		public int Position { get; set; }

		public SyntaxTrivia GetNodeTrivia() {
			if (Node != null) {
				var triviaList = Token.HasLeadingTrivia ? Token.LeadingTrivia
								: Token.HasTrailingTrivia ? Token.TrailingTrivia
								: default;
				if (triviaList.Equals(SyntaxTriviaList.Empty) == false
					&& triviaList.FullSpan.Contains(Position)) {
					return triviaList.FirstOrDefault(i => i.Span.Contains(Position));
				}
			}
			return default;
		}

		public Task<ISymbol> GetSymbolAsync(SyntaxNode node, CancellationToken cancellationToken) {
			return Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindSymbolAtPositionAsync(Document, node.SpanStart, cancellationToken);
		}

		public Task<ISymbol> GetSymbolAsync(CancellationToken cancellationToken) {
			return Node == null
				? Task.FromResult<ISymbol>(null)
				: Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindSymbolAtPositionAsync(Document, Position, cancellationToken);
		}

		public async Task<bool> UpdateAsync(CancellationToken cancellationToken) {
			try {
				Document = _TextView.TextSnapshot.GetOpenDocumentInCurrentContextWithChanges();
				SemanticModel = await Document.GetSemanticModelAsync(cancellationToken);
				Compilation = SemanticModel.SyntaxTree.GetCompilationUnitRoot(cancellationToken);
				return true;
			}
			catch (NullReferenceException) {
				Node = null;
				Token = default;
			}
			return false;
		}

		public async Task<bool> UpdateAsync(int position, CancellationToken cancellationToken) {
			if (await UpdateAsync(cancellationToken) == false) {
				return false;
			}
			Position = position;
			try {
				Token = Compilation.FindToken(position, true);
			}
			catch (ArgumentOutOfRangeException) {
				Node = null;
				Token = default;
				return false;
			}
			Node = Compilation.FindNode(Token.Span, true, true);
			return true;
		}
	}
}
