using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;

namespace Codist
{
	sealed class SemanticContext
	{
		VersionStamp _Version;
		SyntaxNode _Node, _NodeIncludeTrivia;
		IOutliningManager _OutliningManager;
		int _Position;

		public static SemanticContext GetHovered() {
			var view = TextEditorHelper.GetMouseOverDocumentView();
			return view == null ? null : GetOrCreateSingetonInstance(view);
		}
		public static SemanticContext GetOrCreateSingetonInstance(IWpfTextView view) {
			return view.Properties.GetOrCreateSingletonProperty(() => new SemanticContext(view));
		}

		SemanticContext(IWpfTextView textView) {
			View = textView;
		}

		public IWpfTextView View { get; }
		public IOutliningManager OutliningManager {
			get => _OutliningManager ?? (_OutliningManager = ServicesHelper.Instance.OutliningManager.GetOutliningManager(View));
		}
		public Workspace Workspace { get; private set; }
		public Document Document { get; private set; }
		public SemanticModel SemanticModel { get; private set; }
		public CompilationUnitSyntax Compilation { get; private set; }
		public SyntaxNode Node => _Node != null && _Node.Span.Contains(_Position)
			? _Node
			: (_Node = GetNode(_Position, false, false));
		public SyntaxNode NodeIncludeTrivia {
			get {
				return _NodeIncludeTrivia != null && _NodeIncludeTrivia.Span.Contains(_Position)
					? _NodeIncludeTrivia
					: (_NodeIncludeTrivia = GetNode(_Position, true, true));
			}
		}
		public SyntaxToken Token { get; private set; }
		public int Position {
			get => _Position;
			set { _Position = value; ResetNodeInfo(); }
		}

		public SyntaxNode GetNode(int position, bool includeTrivia, bool deep) {
			SyntaxNode node = Compilation.FindNode(new TextSpan(position, 0), includeTrivia, deep);
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
					&& triviaList.FullSpan.Contains(_Position)) {
					return triviaList.FirstOrDefault(i => i.Span.Contains(_Position));
				}
			}
			return default;
		}

		public Task<ISymbol> GetSymbolAsync(int position, CancellationToken cancellationToken = default) {
			return Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindSymbolAtPositionAsync(Document, position, cancellationToken);
		}

		/// <summary>
		/// Locate symbol in case when the semantic model has been changed.
		/// </summary>
		public async Task<ISymbol> RelocateSymbolAsync(ISymbol symbol, CancellationToken cancellationToken = default) {
			Document doc;
			if (await UpdateAsync(cancellationToken) == false || Workspace == null) {
				return symbol;
			}
			var s = Workspace.CurrentSolution;
			var sr = symbol.DeclaringSyntaxReferences.FirstOrDefault(r => r.SyntaxTree != null);
			try {
				doc = GetDocument(sr?.SyntaxTree);
				return Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindSimilarSymbols(symbol, (await doc.GetSemanticModelAsync(cancellationToken)).Compilation, cancellationToken)
				.FirstOrDefault() ?? symbol;
			}
			catch (NullReferenceException) {
				// ignore
				return symbol;
			}
		}

		/// <summary>
		/// Roughly finds a new <see cref="SyntaxNode"/> in updated semantic model corresponding to the member or type declaration node.
		/// </summary>
		/// <param name="node">The old node.</param>
		/// <returns>The new node.</returns>
		public SyntaxNode RelocateDeclarationNode(SyntaxNode node) {
			if (node.SyntaxTree == SemanticModel.SyntaxTree) {
				// the syntax tree is the same (not changed)
				return node;
			}
			if (node.IsKind(SyntaxKind.VariableDeclarator)) {
				node = node.Parent.Parent;
			}
			if (node is MemberDeclarationSyntax == false) {
				return null;
			}
			var root = Compilation;
			if (String.Equals(node.SyntaxTree.FilePath, SemanticModel.SyntaxTree.FilePath, StringComparison.OrdinalIgnoreCase) == false) {
				// not the same document
				var d = GetDocument(node.SyntaxTree);
				if (d == null || (root = SyncHelper.RunSync(() => d.GetSemanticModelAsync())?.SyntaxTree.GetCompilationUnitRoot()) == null) {
					// document no longer exists
					return null;
				}
			}
			var matches = new List<MemberDeclarationSyntax>(3);
			var s = node.GetDeclarationSignature(node.SpanStart);
			foreach (var item in root.Members) {
				MatchDeclarationNode(item, matches, s, node);
			}
			switch (matches.Count) {
				case 1: return matches[0];
				case 0: return null;
			}
			var match = matches[0];
			matches = matches.FindAll(i => i.MatchSignature(node));
			switch (matches.Count) {
				case 1: return matches[0];
				case 0: return match;
			}
			matches = matches.FindAll(i => i.MatchAncestorDeclaration(node));
			switch (matches.Count) {
				case 1: return matches[0];
				case 0: return match;
				default:
					var p = node.SpanStart;
					return matches.OrderBy(i => Math.Abs(i.SpanStart - p)).First();
			}
		}

		/// <summary>Locates document despite of version changes.</summary>
		public Document GetDocument(SyntaxTree syntaxTree) {
			if (Workspace == null || syntaxTree == null) {
				return null;
			}
			var s = Workspace.CurrentSolution;
			var d = s.GetDocumentId(syntaxTree) ?? s.GetDocumentIdsWithFilePath(syntaxTree.FilePath).FirstOrDefault();
			return d is null ? null : s.GetDocument(d);
		}
		public Project GetProject(SyntaxTree syntaxTree) {
			return GetDocument(syntaxTree)?.Project;
		}

		static void MatchDeclarationNode(MemberDeclarationSyntax member, List<MemberDeclarationSyntax> matches, string signature, SyntaxNode node) {
			if (member.RawKind == node.RawKind
				&& member.GetDeclarationSignature() == signature) {
				matches.Add(member);
			}
			if (member.IsKind(SyntaxKind.NamespaceDeclaration)) {
				foreach (var item in ((NamespaceDeclarationSyntax)member).Members) {
					MatchDeclarationNode(item, matches, signature, node);
				}
			}
			else if (member is TypeDeclarationSyntax) {
				foreach (var item in ((TypeDeclarationSyntax)member).Members) {
					MatchDeclarationNode(item, matches, signature, node);
				}
			}
			else if (node.IsKind(SyntaxKind.EnumMemberDeclaration) && member.IsKind(SyntaxKind.EnumDeclaration)) {
				foreach (var item in ((EnumDeclarationSyntax)member).Members) {
					MatchDeclarationNode(item, matches, signature, node);
				}
			}
		}

		public async Task<ISymbol> GetSymbolAsync(SyntaxNode node, CancellationToken cancellationToken = default) {
			var sm = SemanticModel;
			if (node.SyntaxTree != sm.SyntaxTree) {
				var doc = GetDocument(node.SyntaxTree);
				// doc no longer exists
				if (doc == null) {
					return null;
				}
				sm = await doc.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
				if (node.SpanStart >= sm.SyntaxTree.Length) {
					return null;
				}
				// find the new node in the new model
				var p = node.SpanStart;
				foreach (var item in sm.SyntaxTree.GetChanges(node.SyntaxTree)) {
					if (item.Span.Start > p) {
						break;
					}
					p += item.NewText.Length - item.Span.Length;
				}
				var newNode = sm.SyntaxTree.GetCompilationUnitRoot(cancellationToken).FindNode(new TextSpan(p, 0));
				if (newNode.RawKind != node.RawKind) {
					return null;
				}
				node = newNode;
			}
			return sm.GetSymbol(node, cancellationToken);
		}

		public async Task<ISymbol> GetSymbolAsync(CancellationToken cancellationToken) {
			return Node == null ? null : await GetSymbolAsync(_Position, cancellationToken).ConfigureAwait(false);
		}

		public Task<bool> UpdateAsync(CancellationToken cancellationToken) {
			return UpdateAsync(View.TextBuffer, cancellationToken);
		}

		public async Task<bool> UpdateAsync(ITextBuffer textBuffer, CancellationToken cancellationToken) {
			try {
				var textContainer = textBuffer.AsTextContainer();
				Document doc = null;
				if (Workspace.TryGetWorkspace(textContainer, out var workspace)) {
					var id = workspace.GetDocumentIdInCurrentContext(textContainer);
					if (id != null && workspace.CurrentSolution.ContainsDocument(id)) {
						doc = workspace.CurrentSolution.WithDocumentText(id, textContainer.CurrentText, PreservationMode.PreserveIdentity).GetDocument(id);
					}
				}
				else {
					return false;
				}
				if (doc != Document) {
					Workspace = workspace;
					Document = doc;
					SemanticModel = await Document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
					Compilation = SemanticModel.SyntaxTree.GetCompilationUnitRoot(cancellationToken);
				}
				return true;
			}
			catch (NullReferenceException) {
				System.Diagnostics.Debug.WriteLine("Update sematic context failed.");
				ResetNodeInfo();
			}
			return false;
		}

		public Task<bool> UpdateAsync(int position, CancellationToken cancellationToken) {
			_Position = position;
			return UpdateAsync(position, View.TextBuffer, cancellationToken);
		}

		public async Task<bool> UpdateAsync(int position, ITextBuffer textBuffer, CancellationToken cancellationToken) {
			bool versionChanged;
			try {
				var textContainer = textBuffer.AsTextContainer();
				Document = null;
				if (Workspace.TryGetWorkspace(textContainer, out var workspace)) {
					Workspace = workspace;
					var id = workspace.GetDocumentIdInCurrentContext(textContainer);
					if (id != null && workspace.CurrentSolution.ContainsDocument(id)) {
						Document = workspace.CurrentSolution.WithDocumentText(id, textContainer.CurrentText, PreservationMode.PreserveIdentity).GetDocument(id);
					}
				}
				else {
					return false;
				}
				if (Document == null) {
					return Reset();
				}
				var ver = await Document.GetTextVersionAsync(cancellationToken).ConfigureAwait(false);
				if (versionChanged = ver != _Version) {
					_Version = ver;
					SemanticModel = await Document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
					if (SemanticModel == null) {
						return Reset();
					}
					Compilation = SemanticModel.SyntaxTree.GetCompilationUnitRoot(cancellationToken);
					ResetNodeInfo();
				}
			}
			catch (NullReferenceException) {
				return Reset();
			}
			try {
				if (versionChanged || Token.Span.Contains(position) == false) {
					Token = Compilation.FindToken(position, true);
					_Node = _NodeIncludeTrivia = null;
				}
			}
			catch (ArgumentOutOfRangeException) {
				return Reset();
			}
			return true;
		}

		public ImmutableArray<SyntaxNode> GetContainingNodes(bool includeSyntaxDetails, bool includeRegions) {
			var node = Node;
			var nodes = ImmutableArray.CreateBuilder<SyntaxNode>(5);
			while (node != null) {
				if (node.FullSpan.Contains(_Position)) {
					var nodeKind = node.Kind();
					if (nodeKind != SyntaxKind.VariableDeclaration
						&& (includeSyntaxDetails && nodeKind.IsSyntaxBlock() || nodeKind.IsDeclaration() || nodeKind == SyntaxKind.Attribute)) {
						nodes.Add(node);
					}
				}
				node = node.Parent;
			}
			nodes.Reverse();
			if (includeRegions == false || OutliningManager == null) {
				return nodes.ToImmutable();
			}
			foreach (var region in GetRegions(_Position)) {
				node = Compilation.FindTrivia(region.Extent.GetStartPoint(View.TextSnapshot)).GetStructure();
				if (node == null || node is DirectiveTriviaSyntax == false) {
					continue;
				}
				for (int i = 0; i < nodes.Count; i++) {
					if (node.SpanStart < nodes[i].SpanStart) {
						nodes.Insert(i, node);
						break;
					}
				}
			}
			return nodes.ToImmutable();
		}

		public IEnumerable<ICollapsible> GetRegions(int position) {
			return GetRegions(new SnapshotSpan(View.TextSnapshot, position, 0));
		}

		public IEnumerable<ICollapsible> GetRegions(SnapshotSpan span) {
			return OutliningManager?.GetAllRegions(span)
				.Where(c => c.CollapsedForm as string != "..."); // skip code blocks
		}

		void ResetNodeInfo() {
			_Node = _NodeIncludeTrivia = null;
			Token = default;
		}
		bool Reset() {
			ResetNodeInfo();
			return false;
		}
	}
}
