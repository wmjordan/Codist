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
		VersionStamp _Version;
		SyntaxNode _Node, _NodeIncludeTrivia;

		public SemanticContext(IWpfTextView textView) {
			_TextView = textView;
		}

		public Document Document { get; private set; }
		public SemanticModel SemanticModel { get; private set; }
		public CompilationUnitSyntax Compilation { get; private set; }
		public SyntaxNode Node => _Node != null && _Node.Span.Contains(Position)
			? _Node
			: (_Node = GetNode(Position, false));
		public SyntaxNode NodeIncludeTrivia {
			get {
				return _NodeIncludeTrivia != null && _NodeIncludeTrivia.Span.Contains(Position)
					? _NodeIncludeTrivia
					: (_NodeIncludeTrivia = GetNode(Position, true));
			}
		}
		public SyntaxToken Token { get; private set; }
		public ISymbol Symbol { get; private set; }
		public int Position { get; set; }

		public SyntaxNode GetNode(int position, bool includeTrivia) {
			SyntaxNode node = Compilation.FindNode(Token.Span, includeTrivia, false);
			SeparatedSyntaxList<VariableDeclaratorSyntax> variables;
			if (node.IsKind(SyntaxKind.FieldDeclaration) || node.IsKind(SyntaxKind.EventFieldDeclaration)) {
				variables = (node as BaseFieldDeclarationSyntax).Declaration.Variables;
			}
			else if (node.IsKind(SyntaxKind.VariableDeclaration)) {
				variables = (node as VariableDeclarationSyntax).Variables;
			}
			else if (node.IsKind(SyntaxKind.LocalDeclarationStatement)) {
				variables = (node as LocalDeclarationStatementSyntax).Declaration.Variables;
			}
			else {
				return node;
			}
			foreach (var variable in variables) {
				if (variable.Span.Contains(position)) {
					return node;
				}
			}
			return node.FullSpan.Contains(position) ? node : null;
		}
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

		public Task<ISymbol> GetSymbolAsync(int position, CancellationToken cancellationToken) {
			return Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindSymbolAtPositionAsync(Document, position, cancellationToken);
		}

		public ISymbol GetSymbol(SyntaxNode node, CancellationToken cancellationToken) {
			var sm = SemanticModel;
			if (node.SyntaxTree != sm.SyntaxTree) {
				var doc = Document.Project.Solution.GetDocument(node.SyntaxTree);
				if (doc == null) {
					var nodeFilePath = node.SyntaxTree.FilePath;
					doc = Document.FilePath == nodeFilePath ? Document : Document.Project.Documents.FirstOrDefault(d => d.FilePath == nodeFilePath);
					if (doc == null) {
						return null;
					}
					sm = doc.GetSemanticModelAsync(cancellationToken).Result;
					var newNode = sm.SyntaxTree.GetCompilationUnitRoot(cancellationToken).FindNode(node.Span);
					//todo find out the new node
					if (newNode.IsKind(node.Kind())) {
						node = newNode;
					}
					else {
						return null;
					}
				}
				sm = doc.GetSemanticModelAsync(cancellationToken).Result;
			}
			var info = sm.GetSymbolInfo(node, cancellationToken);
			if (info.Symbol != null) {
				return info.Symbol;
			}
			var symbol = sm.GetDeclaredSymbol(node, cancellationToken);
			if (symbol != null) {
				return symbol;
			}
			var type = sm.GetTypeInfo(node, cancellationToken);
			if (type.Type != null) {
				return type.Type;
			}
			return null;
		}

		public Task<ISymbol> GetSymbolAsync(CancellationToken cancellationToken) {
			return Node == null
				? Task.FromResult<ISymbol>(null)
				: GetSymbolAsync(Position, cancellationToken);
		}

		public async Task<bool> UpdateAsync(CancellationToken cancellationToken) {
			try {
				Document = _TextView.TextSnapshot.GetOpenDocumentInCurrentContextWithChanges();
				SemanticModel = await Document.GetSemanticModelAsync(cancellationToken);
				Compilation = SemanticModel.SyntaxTree.GetCompilationUnitRoot(cancellationToken);
				return true;
			}
			catch (NullReferenceException) {
				ResetNodeInfo();
			}
			return false;
		}

		public async Task<bool> UpdateAsync(int position, CancellationToken cancellationToken) {
			bool versionChanged = false;
			try {
				Document = _TextView.TextSnapshot.GetOpenDocumentInCurrentContextWithChanges();
				var ver = await Document.GetTextVersionAsync(cancellationToken);
				if (versionChanged = ver != _Version) {
					_Version = ver;
					SemanticModel = await Document.GetSemanticModelAsync(cancellationToken);
					Compilation = SemanticModel.SyntaxTree.GetCompilationUnitRoot(cancellationToken);
					ResetNodeInfo();
				}
			}
			catch (NullReferenceException) {
				ResetNodeInfo();
				return false;
			}
			Position = position;
			try {
				if (versionChanged || Token.Span.Contains(position) == false) {
					Token = Compilation.FindToken(position, true);
					_Node = _NodeIncludeTrivia = null;
				}
			}
			catch (ArgumentOutOfRangeException) {
				ResetNodeInfo();
				return false;
			}
			return true;
		}

		private void ResetNodeInfo() {
			_Node = _NodeIncludeTrivia = null;
			Token = default;
		}
	}
}
