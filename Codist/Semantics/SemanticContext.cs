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
		static SnapshotPoint __DummyPosition;

		IOutliningManager _OutliningManager;
		SyntaxModel _Model = SyntaxModel.Empty;
		IWpfTextView _View;
		SnapshotPointSyntax _HitPointSyntax;

		public static SemanticContext GetHovered() {
			var view = TextEditorHelper.GetMouseOverDocumentView();
			return view == null ? null : GetOrCreateSingletonInstance(view);
		}
		public static SemanticContext GetOrCreateSingletonInstance(IWpfTextView view) {
			return view.Properties.GetOrCreateSingletonProperty(() => new SemanticContext(view));
		}
		public static SemanticContext GetActive() {
			var view = TextEditorHelper.GetActiveWpfDocumentView();
			return view == null ? null : GetOrCreateSingletonInstance(view);
		}

		SemanticContext(IWpfTextView textView) {
			_View = textView;
			textView.Closed += View_Closed;
		}

		public IWpfTextView View => _View;
		public IOutliningManager OutliningManager {
			get => _OutliningManager ?? (_OutliningManager = ServicesHelper.Instance.OutliningManager.GetOutliningManager(View));
		}
		public bool IsReady => _Model.IsEmpty == false;
		public Workspace Workspace => _Model.Workspace;
		public Document Document => _Model.Document;
		public SemanticModel SemanticModel => _Model.SemanticModel;
		public CompilationUnitSyntax Compilation => _Model.Compilation;
		public bool IsSourceBufferInView => _Model.IsSourceBufferInView(_View);
		public SyntaxNode Node => _HitPointSyntax?.Node;
		public SyntaxNode NodeIncludeTrivia => _HitPointSyntax?.NodeIncludeTrivia;
		public SyntaxTrivia NodeTrivia => _HitPointSyntax?.GetNodeTrivia() ?? default;
		public SyntaxToken Token => _HitPointSyntax?.Token ?? default;
		public int Position => _HitPointSyntax?.SourcePosition ?? 0;

		public SyntaxNode GetNode(SnapshotPoint position, bool includeTrivia, bool deep) {
			return _Model.GetSnapshotSyntax(position, _View)?.GetNode(includeTrivia, deep);
		}

		public Task<ISymbol> GetSymbolAsync(int position, CancellationToken cancellationToken = default) {
			return Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindSymbolAtPositionAsync(Document, position, cancellationToken);
		}

		/// <summary>
		/// Locate symbol in case when the semantic model has been changed.
		/// </summary>
		public async Task<ISymbol> RelocateSymbolAsync(ISymbol symbol, CancellationToken cancellationToken = default) {
			Document doc;
			Workspace w;
			if (await UpdateAsync(cancellationToken) == false || (w = Workspace) == null) {
				return symbol;
			}
			var s = w.CurrentSolution;
			var sr = symbol.GetSourceReferences().FirstOrDefault(r => r.SyntaxTree != null);
			try {
				doc = GetDocument(sr?.SyntaxTree);
				return Microsoft.CodeAnalysis.FindSymbols.SymbolFinder
					.FindSimilarSymbols(symbol, (await doc.GetSemanticModelAsync(cancellationToken)).Compilation, cancellationToken)
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
		public async Task<SyntaxNode> RelocateDeclarationNodeAsync(SyntaxNode node) {
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
				if (d == null || (root = (await d.GetSemanticModelAsync())?.SyntaxTree.GetCompilationUnitRoot()) == null) {
					// document no longer exists
					return null;
				}
			}
			var matches = new List<MemberDeclarationSyntax>(3);
			int nodeStart = node.SpanStart;
			var s = node.GetDeclarationSignature(nodeStart);
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
				default: return matches.OrderBy(i => Math.Abs(i.SpanStart - nodeStart)).First();
			}
		}

		/// <summary>Locates document despite of version changes.</summary>
		public Document GetDocument(SyntaxTree syntaxTree) {
			Workspace w;
			if (syntaxTree == null || (w = Workspace) == null) {
				return null;
			}
			var s = w.CurrentSolution;
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
			else if (member is TypeDeclarationSyntax t) {
				foreach (var item in t.Members) {
					MatchDeclarationNode(item, matches, signature, node);
				}
			}
			else if (node.IsKind(SyntaxKind.EnumMemberDeclaration) && member.IsKind(SyntaxKind.EnumDeclaration)) {
				foreach (var item in ((EnumDeclarationSyntax)member).Members) {
					MatchDeclarationNode(item, matches, signature, node);
				}
			}
		}

		public Task<ISymbol> GetSymbolAsync(SyntaxNode node, CancellationToken cancellationToken = default) {
			var sm = SemanticModel;
			return node.SyntaxTree == sm.SyntaxTree
				? Task.FromResult(sm.GetSymbol(node, cancellationToken))
				: RefreshSymbol(this, node, sm, cancellationToken);

			async Task<ISymbol> RefreshSymbol(SemanticContext me, SyntaxNode n, SemanticModel m, CancellationToken ct) {
				var doc = me.GetDocument(n.SyntaxTree);
				// doc no longer exists
				if (doc == null) {
					return null;
				}
				m = await doc.GetSemanticModelAsync(ct).ConfigureAwait(false);
				var p = n.SpanStart;
				if (p >= m.SyntaxTree.Length) {
					return null;
				}
				// find the new node in the new model
				foreach (var item in m.SyntaxTree.GetChanges(n.SyntaxTree)) {
					if (item.Span.Start > p) {
						break;
					}
					p += item.NewText.Length - item.Span.Length;
				}
				var newNode = m.SyntaxTree.GetCompilationUnitRoot(ct).FindNode(new TextSpan(p, 0)).AncestorsAndSelf().FirstOrDefault(i => i is MemberDeclarationSyntax || i is BaseTypeDeclarationSyntax || i is VariableDeclaratorSyntax);
				if (newNode.RawKind != n.RawKind) {
					return null;
				}
				n = newNode;
				return m.GetSymbol(n, ct);
			}
		}

		public async Task<ISymbol> GetSymbolAsync(CancellationToken cancellationToken) {
			return _HitPointSyntax.Symbol ?? await _HitPointSyntax.GetSymbolAsync(cancellationToken).ConfigureAwait(false);
		}

		public SnapshotSpan MapSourceSpan(TextSpan span) {
			return _Model.MapSourceSpan(span, _View);
		}
		public NormalizedSnapshotSpanCollection MapDownToSourceSpan(SnapshotSpan span) {
			return _Model.MapDownToSourceSpan(span, _View);
		}

		public Task<bool> UpdateAsync(CancellationToken cancellationToken) {
			return UpdateAsync(View.TextBuffer, cancellationToken);
		}

		public Task<bool> UpdateAsync(ITextBuffer textBuffer, CancellationToken cancellationToken) {
			if (UpdateDocumentAndWorkspace(ref __DummyPosition, ref textBuffer, out Document doc, out Workspace workspace) == false) {
				return Task.FromResult(false);
			}
			return UpdateAsync(textBuffer, doc, workspace, cancellationToken);
		}

		async Task<bool> UpdateAsync(ITextBuffer textBuffer, Document doc, Workspace workspace, CancellationToken cancellationToken) {
			try {
				SyntaxModel m = _Model;
				var ver = await doc.GetSyntaxVersionAsync(cancellationToken).ConfigureAwait(false);
				if (doc == Document && ver == m.Version) {
					return true;
				}
				SemanticModel model;
				_Model = new SyntaxModel(
					workspace,
					textBuffer,
					doc,
					model = await doc.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false),
					model.SyntaxTree.GetCompilationUnitRoot(cancellationToken),
					ver
					);
				return true;
			}
			catch (NullReferenceException) {
				System.Diagnostics.Debug.WriteLine("Update semantic context failed.");
			}
			return false;
		}

		public Task<bool> UpdateAsync(SnapshotPoint position, CancellationToken cancellationToken) {
			var textBuffer = View.TextBuffer;
			if (UpdateDocumentAndWorkspace(ref position, ref textBuffer, out Document document, out Workspace workspace) == false) {
				return Task.FromResult(false);
			}
			if (document == null) {
				return Task.FromResult(Reset());
			}
			return UpdateAsync(position, textBuffer, document, workspace, cancellationToken);
		}

		async Task<bool> UpdateAsync(SnapshotPoint position, ITextBuffer textBuffer, Document document, Workspace workspace, CancellationToken cancellationToken) {
			bool versionChanged;
			try {
				var ver = await document.GetSyntaxVersionAsync(cancellationToken).ConfigureAwait(false);
				if (versionChanged = ver != _Model.Version) {
					var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
					if (model == null) {
						return Reset();
					}
					_Model = new SyntaxModel(
						workspace,
						textBuffer,
						document,
						model,
						model.SyntaxTree.GetCompilationUnitRoot(cancellationToken),
						ver
					);
				}
				_HitPointSyntax = _Model.IsEmpty || position.Snapshot == null ? null : _Model.GetSnapshotSyntax(position, _View);
			}
			catch (NullReferenceException) {
				return Reset();
			}
			return true;
		}

		bool UpdateDocumentAndWorkspace(ref SnapshotPoint position, ref ITextBuffer textBuffer, out Document document, out Workspace workspace) {
			SnapshotPoint? p;
			var textContainer = textBuffer.AsTextContainer();
			document = null;
			if (Workspace.TryGetWorkspace(textContainer, out workspace)) {
				GetDocumentFromTextContainer(textContainer, workspace, ref document);
				return true;
			}
			else {
				foreach (var item in View.BufferGraph.GetTextBuffers(_ => true)) {
					textContainer = item.AsTextContainer();
					if (Workspace.TryGetWorkspace(textContainer, out workspace)) {
						GetDocumentFromTextContainer(textContainer, workspace, ref document);
						if (position.Snapshot != null
							&& (p = View.BufferGraph.MapDownToBuffer(position, PointTrackingMode.Positive, item, PositionAffinity.Predecessor)).HasValue) {
							position = p.Value;
							textBuffer = item;
							return true;
						}
					}
				}
				return false;
			}
		}

		static void GetDocumentFromTextContainer(SourceTextContainer textContainer, Workspace workspace, ref Document document) {
			var id = workspace.GetDocumentIdInCurrentContext(textContainer);
			if ((id is null) == false && workspace.CurrentSolution.ContainsDocument(id)) {
				document = workspace.CurrentSolution.WithDocumentText(id, textContainer.CurrentText, PreservationMode.PreserveIdentity).GetDocument(id);
			}
		}

		public ImmutableArray<SyntaxNode> GetContainingNodes(SnapshotPoint position, bool includeSyntaxDetails, bool includeRegions) {
			var model = _Model;
			SyntaxNode node;
			if (model.IsEmpty || (node = model.GetSnapshotSyntax(position, _View).Node) == null) {
				return ImmutableArray<SyntaxNode>.Empty;
			}
			var nodes = ImmutableArray.CreateBuilder<SyntaxNode>(5);
			do {
				if (node.FullSpan.Contains(position)) {
					var nodeKind = node.Kind();
					if (nodeKind != SyntaxKind.VariableDeclaration
						&& (includeSyntaxDetails && nodeKind.IsSyntaxBlock() || nodeKind.IsDeclaration() || nodeKind == SyntaxKind.Attribute)) {
						nodes.Add(node);
					}
				}
			}
			while ((node = node.Parent) != null);
			nodes.Reverse();
			if (nodes.Count > 0) {
				var members = model.Compilation.Members;
				if (members.Count > 1 && members[0].IsKind(CodeAnalysisHelper.FileScopedNamespaceDeclaration)) {
					nodes.Insert(0, members[0]);
				}
			}
			if (includeRegions == false
				|| OutliningManager == null
				|| position >= View.TextSnapshot.Length) {
				return nodes.ToImmutable();
			}
			foreach (var region in GetRegions(position)) {
				node = model.Compilation.FindTrivia(region.Extent.GetStartPoint(View.TextSnapshot)).GetStructure();
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

		public SyntaxTrivia GetLineComment() {
			var token = Token;
			var triviaList = token.HasLeadingTrivia ? token.LeadingTrivia : token.HasTrailingTrivia ? token.TrailingTrivia : default;
			return triviaList.Equals(SyntaxTriviaList.Empty) == false && triviaList.FullSpan.Contains(View.Selection.Start.Position)
				? triviaList.FirstOrDefault(i => i.IsLineComment())
				: default;
		}

		bool Reset() {
			_HitPointSyntax = null;
			return false;
		}

		void View_Closed(object sender, EventArgs e) {
			var view = _View;
			if (view != null) {
				_View = null;
				view.Closed -= View_Closed;
				view.Properties.RemoveProperty(typeof(SemanticContext));
				_OutliningManager = null;
				_HitPointSyntax = null;
			}
		}

		readonly struct SyntaxModel
		{
			internal static readonly SyntaxModel Empty = new SyntaxModel(null, null, null, null, null, VersionStamp.Default);

			public readonly Workspace Workspace;
			public readonly ITextBuffer SourceBuffer;
			public readonly Document Document;
			public readonly SemanticModel SemanticModel;
			public readonly CompilationUnitSyntax Compilation;
			public readonly VersionStamp Version;

			public SyntaxModel(Workspace workspace, ITextBuffer textBuffer, Document document, SemanticModel semanticModel, CompilationUnitSyntax compilation, VersionStamp version) {
				Workspace = workspace;
				Document = document;
				SourceBuffer = textBuffer;
				SemanticModel = semanticModel;
				Compilation = compilation;
				Version = version;
			}

			public bool IsEmpty => Document == null;

			public SnapshotPointSyntax GetSnapshotSyntax(SnapshotPoint visualSnapshotPoint, ITextView view) {
				return new SnapshotPointSyntax(this, visualSnapshotPoint, view);
			}

			public bool IsSourceBufferInView(ITextView view) {
				return view.TextBuffer == SourceBuffer;
			}

			public SnapshotSpan MapSourceSpan(TextSpan span, ITextView view) {
				return view.BufferGraph.MapUpToBuffer(new SnapshotSpan(SourceBuffer.CurrentSnapshot, span.ToSpan()), SpanTrackingMode.EdgeInclusive, view.TextBuffer)[0];
			}

			public NormalizedSnapshotSpanCollection MapDownToSourceSpan(SnapshotSpan span, ITextView view) {
				return view.BufferGraph.MapDownToBuffer(span, SpanTrackingMode.EdgeInclusive, SourceBuffer);
			}
		}

		sealed class SnapshotPointSyntax
		{
			public readonly SnapshotPoint VisualPosition, SourcePosition;
			readonly SyntaxModel _Model;
			SyntaxNode _Node, _NodeIncludeTrivia;
			SyntaxToken _Token;
			ISymbol _Symbol;

			public SnapshotPointSyntax(SyntaxModel model, SnapshotPoint visualPosition, ITextView view) {
				_Model = model;
				VisualPosition = visualPosition;
				SourcePosition = visualPosition.Snapshot.TextBuffer != model.SourceBuffer && view?.BufferGraph != null
					? view.BufferGraph.MapDownToSnapshot(visualPosition, PointTrackingMode.Positive, model.SourceBuffer.CurrentSnapshot, PositionAffinity.Successor).GetValueOrDefault(visualPosition)
					: visualPosition;
			}

			public SyntaxNode Node => _Node ?? (_Node = GetNode(false, false));
			public SyntaxNode NodeIncludeTrivia => _NodeIncludeTrivia ?? (_NodeIncludeTrivia = GetNode(true, true));
			public SyntaxToken Token => _Token.SyntaxTree != null ? _Token : (_Token = GetToken());
			public ISymbol Symbol => _Symbol;

			public SyntaxNode GetNode(bool includeTrivia, bool deep) {
				var c = _Model.Compilation;
				var p = SourcePosition;
				if (c == null || c.FullSpan.Contains(p) == false) {
					return null;
				}
				var node = c.FindNode(new TextSpan(p, 0), includeTrivia, deep);
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
					if (variable.Span.Contains(p)) {
						return node;
					}
				}
				return node.FullSpan.Contains(p) ? node : null;
			}

			public Task<ISymbol> GetSymbolAsync(CancellationToken cancellationToken) {
				return _Symbol != null ? Task.FromResult(_Symbol) : FindSymbolAsync(cancellationToken);
			}

			async Task<ISymbol> FindSymbolAsync(CancellationToken cancellationToken) {
				return _Model.Document != null
					? _Symbol = await Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindSymbolAtPositionAsync(_Model.Document, SourcePosition, cancellationToken).ConfigureAwait(false)
					: null;
			}

			SyntaxToken GetToken() {
				return _Model.Compilation?.FindToken(SourcePosition, true) ?? default;
			}

			public SyntaxTrivia GetNodeTrivia() {
				if (Node != null) {
					var token = Token;
					var triviaList = token.HasLeadingTrivia ? token.LeadingTrivia
						: token.HasTrailingTrivia ? token.TrailingTrivia
						: default;
					if (triviaList.Count != 0) {
						var p = SourcePosition.Position;
						if (triviaList.FullSpan.Contains(p)) {
							foreach (var trivia in triviaList) {
								if (trivia.Span.Contains(p)) {
									return trivia;
								}
							}
						}
					}
				}
				return default;
			}
		}
	}
}
