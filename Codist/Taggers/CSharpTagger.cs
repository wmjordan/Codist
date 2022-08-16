using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AppHelpers;
using Codist.SyntaxHighlight;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using TH = Microsoft.VisualStudio.Shell.ThreadHelper;

namespace Codist.Taggers
{
	/// <summary>A classifier for C# code syntax highlight.</summary>
	sealed class CSharpTagger : ITagger<IClassificationTag>, IDisposable
	{
		static readonly CSharpClassifications __Classifications = CSharpClassifications.Instance;
		static readonly GeneralClassifications __GeneralClassifications = GeneralClassifications.Instance;
		ConcurrentQueue<SnapshotSpan> _PendingSpans = new ConcurrentQueue<SnapshotSpan>();
		CSharpTaggerProvider _TaggerProvider;
		IWpfTextView _View;
		ITextBuffer _Buffer;
		CancellationTokenSource _TaskBreaker;
		ConditionalWeakTable<ITextSnapshot, ParserTask> _ParserTasks = new ConditionalWeakTable<ITextSnapshot, ParserTask>();
		ParserTask _LastFinishedTask;
		int _Reference;
		bool _IsVisible;
		bool _HasBackgroundChange;
		// debug info
		readonly string _name;

		public CSharpTagger(CSharpTaggerProvider taggerProvider, IWpfTextView view, ITextBuffer buffer) {
			_TaggerProvider = taggerProvider;
			_name = buffer.GetTextDocument()?.FilePath ?? buffer.CurrentSnapshot?.GetText(0, Math.Min(buffer.CurrentSnapshot.Length, 500));
			if (view != null) {
				_View = view;
				if (IsInteractiveWindow(buffer) == false) {
					view.VisualElement.IsVisibleChanged += VisualElement_IsVisibleChanged;
				}
			}
			_Buffer = buffer;
			if (buffer is ITextBuffer2 b) {
				b.ChangedOnBackground += TextBuffer_ChangedOnBackground;
			}
			buffer.ContentTypeChanged += TextBuffer_ContentTypeChanged;
		}

		void VisualElement_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e) {
			_IsVisible = (bool)e.NewValue;
			if (_IsVisible && _HasBackgroundChange) {
				_HasBackgroundChange = false;
				var last = _LastFinishedTask;
				if (last != null) {
					var task = new ParserTask(_View.TextSnapshot, this);
					if (task.StartParse(SyncHelper.CancelAndRetainToken(ref _TaskBreaker))) {
						_PendingSpans.Enqueue(_View.GetVisibleLineSpan());
					}
					else {
						task.Release();
					}
				}
			}
		}

		/// <summary>This event is to notify the IDE that some tags are classified (from the parser thread).</summary>
		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		IEnumerable<ITagSpan<IClassificationTag>> ITagger<IClassificationTag>.GetTags(NormalizedSnapshotSpanCollection spans) {
			var parserTasks = _ParserTasks;
			if (parserTasks == null || spans.Count == 0) {
				return Enumerable.Empty<ITagSpan<IClassificationTag>>();
			}
			var snapshot = spans[0].Snapshot;
			SemanticModel model;
			Workspace workspace;
			if (parserTasks.TryGetValue(snapshot, out var task)) {
				if ((model = task.Model) != null && (workspace = task.Workspace) != null) {
					return Parser.GetTags(spans, workspace, model, snapshot);
				}
				// the snapshot is still under parsing
				EnqueueSpans(spans);
			}
			var finishedTask = _LastFinishedTask;
			if (task == null && (finishedTask == null || snapshot != finishedTask.Snapshot)) {
				task = new ParserTask(snapshot, this);
				if (task.StartParse(snapshot.Version.VersionNumber > 0 ? SyncHelper.CancelAndRetainToken(ref _TaskBreaker) : default)) {
					parserTasks.Add(snapshot, task);
					EnqueueSpans(spans);
				}
				else {
					task.Release();
				}
			}
			if (finishedTask != null) {
				GET_RESULT:
				model = finishedTask.Model;
				workspace = finishedTask.Workspace;
				var finishedSnapshot = finishedTask.Snapshot;
				if (finishedTask != Volatile.Read(ref _LastFinishedTask)) {
					finishedTask = Volatile.Read(ref _LastFinishedTask);
					goto GET_RESULT;
				}
				if (finishedSnapshot != null && finishedSnapshot.TextBuffer == snapshot.TextBuffer) {
					return UseOldResult(model, workspace, finishedSnapshot, spans, snapshot);
				}
			}
			return Enumerable.Empty<ITagSpan<IClassificationTag>>();
		}

		void EnqueueSpans(NormalizedSnapshotSpanCollection spans) {
			foreach (var item in spans) {
				_PendingSpans.Enqueue(item);
			}
		}

		void RefreshPendingSpans() {
			while (_PendingSpans.TryDequeue(out var span)) {
				TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
			}
		}

		void RemoveParser(ParserTask task) {
			_ParserTasks.Remove(task.Snapshot);
		}

		static IEnumerable<ITagSpan<IClassificationTag>> UseOldResult(SemanticModel model, Workspace workspace, ITextSnapshot finishedSnapshot, NormalizedSnapshotSpanCollection spans, ITextSnapshot snapshot) {
			if (workspace == null) {
				yield break;
			}
			foreach (var tagSpan in Parser.GetTags(MapToOldSpans(spans, finishedSnapshot), workspace, model, finishedSnapshot)) {
				yield return new TagSpan<IClassificationTag>(tagSpan.Span.TranslateTo(snapshot, SpanTrackingMode.EdgeInclusive), tagSpan.Tag);
			}
		}

		static IEnumerable<SnapshotSpan> MapToOldSpans(NormalizedSnapshotSpanCollection spans, ITextSnapshot last) {
			foreach (var item in spans) {
				yield return item.TranslateTo(last, SpanTrackingMode.EdgeInclusive);
			}
		}

		void TextBuffer_ChangedOnBackground(object sender, TextContentChangedEventArgs e) {
			var changes = e.Changes;
			if (changes.Count == 0) {
				return;
			}
			var start = changes[0].NewPosition;
			var changedSpan = new SnapshotSpan(e.After, start, changes[changes.Count - 1].NewEnd - start);
			Debug.WriteLine("Buffer changed: " + changedSpan);
			TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(changedSpan));
		}

		void TextBuffer_ContentTypeChanged(object sender, ContentTypeChangedEventArgs e) {
			if (e.AfterContentType.IsOfType(Constants.CodeTypes.CSharp) == false) {
				ReleaseResources();
				_Reference = 0;
			}
		}

		public void IncrementReference() {
			_Reference++;
		}

		public void DecrementReference() {
			if (--_Reference == 0) {
				ReleaseResources();
			}
		}

		public void Dispose() {
			DecrementReference();
			Debug.WriteLine(_Buffer?.GetTextDocument()?.FilePath + " ref: " + _Reference);
		}

		void ReleaseResources() {
			if (_Buffer != null) {
				if (_TaskBreaker != null) {
					_TaskBreaker.Dispose();
					_TaskBreaker = null;
				}
				var last = _LastFinishedTask;
				if (last != null) {
					_LastFinishedTask = null;
					last.Release();
				}
				if (_Buffer is ITextBuffer2 b) {
					b.ChangedOnBackground -= TextBuffer_ChangedOnBackground;
				}
				_Buffer.ContentTypeChanged -= TextBuffer_ContentTypeChanged;
				if (_View != null) {
					_View.VisualElement.IsVisibleChanged -= VisualElement_IsVisibleChanged;
				}
				_TaggerProvider.DetachTagger(_Buffer);
				_ParserTasks = null;
				_Buffer = null;
				_View = null;
				_TaggerProvider = null;
				_PendingSpans = null;
			}
		}

		static bool IsInteractiveWindow(ITextBuffer buffer) {
			return buffer.Properties.PropertyList.Any(o => (o.Key as Type)?.Name == "InteractiveWindow");
		}

		sealed class ParserTask
		{
			Workspace _Workspace;
			SemanticModel _Model;
			ITextSnapshot _Snapshot;
			Document _Document;
			ITextBuffer _Buffer;
			CSharpTagger _Tagger;
			int _State;

			public ITextSnapshot Snapshot => _Snapshot;
			public Workspace Workspace => _Workspace;
			public SemanticModel Model => _Model;

			public ParserTask(ITextSnapshot snapshot, CSharpTagger tagger) {
				_Buffer = snapshot.TextBuffer;
				_Snapshot = snapshot;
				_Tagger = tagger;
			}

			public bool StartParse(CancellationToken cancellationToken) {
				if (Interlocked.CompareExchange(ref _State, 1, 0) != 0) {
					return false;
				}
				var parserTask = Task.Run(() => {
					Debug.WriteLine("Start parsing " + Snapshot.Version + " on " + (TH.JoinableTaskContext.IsOnMainThread ? "MainThread" : "worker thread"));
					if (cancellationToken.CanBeCanceled
						&& Snapshot.Length > 1000
						&& CancellableWait(Snapshot.Length > 60000 ? 100 : 30, cancellationToken) == false) {
						CancelOrFault();
						return Task.FromCanceled(cancellationToken);
					}
					return ParseAsync(cancellationToken);
				}, cancellationToken);
				return parserTask.IsCanceled == false;
			}

			async Task ParseAsync(CancellationToken cancellationToken) {
				if (_Workspace == null) {
					var w = Workspace.GetWorkspaceRegistration(_Buffer.AsTextContainer()).Workspace;
					if (w != null) {
						_Workspace = w;
						_Document = w.GetDocument(_Buffer);
						if (IsInteractiveWindow(_Buffer) == false) {
							w.WorkspaceChanged += WorkspaceChanged;
						}
					}
				}
				SemanticModel model;
				try {
					model = await _Document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException) {
					CancelOrFault();
					throw;
				}
				Debug.WriteLine("End parsing " + _Snapshot.Version);
				_State = (int)ParserState.Completed;
				_Model = model;
				SubstituteParserResult();
				if (_Model != null) {
					_Tagger?.RefreshPendingSpans();
				}
			}

			void SubstituteParserResult() {
				var tagger = _Tagger;
				START:
				var last = Interlocked.CompareExchange(ref tagger._LastFinishedTask, this, null);
				if (last != null) {
					var lastSnapshot = last.Snapshot;
					if (lastSnapshot == null || Snapshot.Version.VersionNumber >= lastSnapshot.Version.VersionNumber) {
						if (Interlocked.CompareExchange(ref tagger._LastFinishedTask, this, last) != last) {
							goto START;
						}
						last._State = (int)ParserState.Retired;
						last.Disconnect();
						last._Model = null;
						last._Snapshot = null;
					}
					else {
						// release the current parser if it is not used
						Release();
						return;
					}
				}
				tagger.RemoveParser(this);
			}

			public void Release() {
				_State = (int)ParserState.Released;
				Disconnect();
				_Snapshot = null;
				_Model = null;
			}

			void CancelOrFault() {
				Debug.WriteLine("Cancelled parsing " + _Snapshot.Version);
				_State = (int)ParserState.CancelledOrFaulted;
				Disconnect();
				_Snapshot = null;
			}

			void Disconnect() {
				var tagger = Interlocked.Exchange(ref _Tagger, null);
				if (tagger == null) {
					return;
				}
				tagger.RemoveParser(this);
				Debug.WriteLine($"{_Document?.Id} Disconnected from workspace.");
				var w = _Workspace;
				if (w != null) {
					w.WorkspaceChanged -= WorkspaceChanged;
					_Workspace = null;
				}
				_Document = null;
				_Buffer = null;
			}

			void WorkspaceChanged(object sender, WorkspaceChangeEventArgs args) {
				Debug.WriteLine($"Workspace {args.Kind}: {args.DocumentId}");
				switch (args.Kind) {
					case WorkspaceChangeKind.DocumentChanged:
					case WorkspaceChangeKind.DocumentRemoved:
						if (args.DocumentId == _Document?.Id) {
							return;
						}
						break;
				}
				var t = _Tagger;
				if (t != null) {
					if (t._IsVisible) {
						ReparseAfterWorkspaceChange();
					}
					else if (Interlocked.CompareExchange(ref _State, 0, (int)ParserState.Completed) == (int)ParserState.Completed){
						t._HasBackgroundChange = true;
					}
				}
			}

			void ReparseAfterWorkspaceChange() {
				var tagger = _Tagger;
				var snapshot = _Snapshot;
				if (tagger?._PendingSpans != null && snapshot != null) {
					var task = new ParserTask(snapshot, tagger);
					if (task.StartParse(SyncHelper.CancelAndRetainToken(ref tagger._TaskBreaker))) {
						tagger._PendingSpans.Enqueue(tagger._View.GetVisibleLineSpan());
					}
				}
			}

			static bool CancellableWait(int loop, CancellationToken cancellationToken) {
				var p = 0;
				do {
					Thread.Sleep(10);
					++p;
					if (cancellationToken.IsCancellationRequested) {
						return false;
					}
				}
				while (p < loop);
				return true;
			}
		}

		enum ParserState
		{
			Idle,
			Working,
			CancelledOrFaulted,
			Completed,
			Retired,
			Released
		}

		static class Parser
		{
			public static IEnumerable<ITagSpan<IClassificationTag>> GetTags(IEnumerable<SnapshotSpan> spans, Workspace workspace, SemanticModel semanticModel, ITextSnapshot snapshot) {
				foreach (var span in spans) {
					var textSpan = new TextSpan(span.Start.Position, span.Length);
					var unitCompilation = semanticModel.SyntaxTree.GetCompilationUnitRoot();
					var classifiedSpans = Classifier.GetClassifiedSpans(semanticModel, textSpan, workspace);
					var lastTriviaSpan = default(TextSpan);
					SyntaxNode node;
					TagSpan<IClassificationTag> tag = null;
					var r = GetAttributeNotationSpan(snapshot, textSpan, unitCompilation);
					if (r != null) {
						yield return r;
					}

					foreach (var item in classifiedSpans) {
						var ct = item.ClassificationType;
						switch (ct) {
							case "keyword":
							case Constants.CodeKeywordControl:
								node = unitCompilation.FindNode(item.TextSpan, true, true);
								if (node is MemberDeclarationSyntax || node is AccessorDeclarationSyntax) {
									tag = ClassifyDeclarationKeyword(item.TextSpan, snapshot, node, unitCompilation, out var tag2);
									if (tag2 != null) {
										yield return tag2;
									}
								}
								else {
									tag = ClassifyKeyword(item.TextSpan, snapshot, node, unitCompilation);
								}
								break;
							case Constants.CodeOperator:
							case Constants.CodeOverloadedOperator:
								tag = ClassifyOperator(item.TextSpan, snapshot, semanticModel, unitCompilation);
								break;
							case Constants.CodePunctuation:
								tag = ClassifyPunctuation(item.TextSpan, snapshot, semanticModel, unitCompilation);
								break;
							case Constants.XmlDocDelimiter:
								tag = ClassifyXmlDoc(item.TextSpan, snapshot, unitCompilation, ref lastTriviaSpan);
								break;
							default:
								tag = null;
								break;
						}
						if (tag != null) {
							yield return tag;
							continue;
						}
						if (ct == Constants.CodeIdentifier
							//|| ct == Constants.CodeStaticSymbol
							|| ct.EndsWith("name", StringComparison.Ordinal)) {
							var itemSpan = item.TextSpan;
							node = unitCompilation.FindNode(itemSpan, true);
							foreach (var type in GetClassificationType(node, semanticModel)) {
								yield return CreateClassificationSpan(snapshot, itemSpan, type);
							}
						}
					}
				}
			}

			static TagSpan<IClassificationTag> GetAttributeNotationSpan(ITextSnapshot snapshot, TextSpan textSpan, CompilationUnitSyntax unitCompilation) {
				var spanNode = unitCompilation.FindNode(textSpan, true, false);
				if (spanNode.HasLeadingTrivia && spanNode.GetLeadingTrivia().FullSpan.Contains(textSpan)) {
					return null;
				}
				switch (spanNode.Kind()) {
					case SyntaxKind.AttributeArgument:
					//case SyntaxKind.AttributeList:
					case SyntaxKind.AttributeArgumentList:
						return CreateClassificationSpan(snapshot, textSpan, __Classifications.AttributeNotation);
				}
				return null;
			}

			static TagSpan<IClassificationTag> ClassifyDeclarationKeyword(TextSpan itemSpan, ITextSnapshot snapshot, SyntaxNode node, CompilationUnitSyntax unitCompilation, out TagSpan<IClassificationTag> secondaryTag) {
				secondaryTag = null;
				switch (unitCompilation.FindToken(itemSpan.Start).Kind()) {
					case SyntaxKind.SealedKeyword:
					case SyntaxKind.OverrideKeyword:
					case SyntaxKind.AbstractKeyword:
					case SyntaxKind.VirtualKeyword:
					case SyntaxKind.ProtectedKeyword:
					case SyntaxKind.NewKeyword:
						return CreateClassificationSpan(snapshot, itemSpan, __Classifications.AbstractionKeyword);
					case SyntaxKind.ThisKeyword:
						return CreateClassificationSpan(snapshot, itemSpan, __Classifications.Declaration);
					case SyntaxKind.UnsafeKeyword:
					case SyntaxKind.FixedKeyword:
						return CreateClassificationSpan(snapshot, itemSpan, __Classifications.ResourceKeyword);
					case SyntaxKind.ExplicitKeyword:
					case SyntaxKind.ImplicitKeyword:
						secondaryTag = CreateClassificationSpan(snapshot, ((ConversionOperatorDeclarationSyntax)node).Type.Span, __Classifications.NestedDeclaration);
						return CreateClassificationSpan(snapshot, itemSpan, __GeneralClassifications.TypeCastKeyword);
					case SyntaxKind.ReadOnlyKeyword:
						return CreateClassificationSpan(snapshot, itemSpan, __GeneralClassifications.TypeCastKeyword);
					case CodeAnalysisHelper.RecordKeyword: // workaround for missing classification type for record identifier
						itemSpan = ((TypeDeclarationSyntax)node).Identifier.Span;
						return CreateClassificationSpan(snapshot, itemSpan, node.Kind() == CodeAnalysisHelper.RecordDeclaration ? TransientTags.StructDeclaration : TransientTags.ClassDeclaration);
				}
				return null;
			}

			static TagSpan<IClassificationTag> ClassifyKeyword(TextSpan itemSpan, ITextSnapshot snapshot, SyntaxNode node, CompilationUnitSyntax unitCompilation) {
				switch (node.Kind()) {
					case SyntaxKind.BreakStatement:
						if (node.Parent is SwitchSectionSyntax) {
							return null;
						}
						goto case SyntaxKind.ReturnStatement;
					// highlights: return, yield return, yield break, throw and continue
					case SyntaxKind.ReturnKeyword:
					case SyntaxKind.GotoCaseStatement:
					case SyntaxKind.GotoDefaultStatement:
					case SyntaxKind.GotoStatement:
					case SyntaxKind.ContinueStatement:
					case SyntaxKind.ReturnStatement:
					case SyntaxKind.YieldReturnStatement:
					case SyntaxKind.YieldBreakStatement:
					case SyntaxKind.ThrowStatement:
					case SyntaxKind.ThrowExpression:
						return CreateClassificationSpan(snapshot, itemSpan, __GeneralClassifications.ControlFlowKeyword);
					case SyntaxKind.IfStatement:
					case SyntaxKind.ElseClause:
					case SyntaxKind.SwitchStatement:
					case SyntaxKind.CaseSwitchLabel:
					case SyntaxKind.DefaultSwitchLabel:
					case CodeAnalysisHelper.SwitchExpression:
					case SyntaxKind.CasePatternSwitchLabel:
					case SyntaxKind.WhenClause:
						return CreateClassificationSpan(snapshot, itemSpan, __GeneralClassifications.BranchingKeyword);
					case SyntaxKind.ForStatement:
					case SyntaxKind.ForEachStatement:
					case SyntaxKind.ForEachVariableStatement:
					case SyntaxKind.WhileStatement:
					case SyntaxKind.DoStatement:
					case SyntaxKind.SelectClause:
					case SyntaxKind.FromClause:
						return CreateClassificationSpan(snapshot, itemSpan, __GeneralClassifications.LoopKeyword);
					case SyntaxKind.UsingStatement:
					case SyntaxKind.FixedStatement:
					case SyntaxKind.LockStatement:
					case SyntaxKind.UnsafeStatement:
					case SyntaxKind.TryStatement:
					case SyntaxKind.CatchClause:
					case SyntaxKind.CatchFilterClause:
					case SyntaxKind.FinallyClause:
					case SyntaxKind.StackAllocArrayCreationExpression:
					case CodeAnalysisHelper.ImplicitStackAllocArrayCreationExpression:
					case CodeAnalysisHelper.FunctionPointerCallingConvention:
						return CreateClassificationSpan(snapshot, itemSpan, __Classifications.ResourceKeyword);
					case SyntaxKind.LocalDeclarationStatement:
						if (unitCompilation.FindToken(itemSpan.Start, true).Kind() == SyntaxKind.UsingKeyword) {
							goto case SyntaxKind.UsingStatement;
						}
						return null;
					case SyntaxKind.IsExpression:
					case SyntaxKind.IsPatternExpression:
					case SyntaxKind.AsExpression:
					case SyntaxKind.RefExpression:
					case SyntaxKind.RefType:
					case SyntaxKind.CheckedExpression:
					case SyntaxKind.CheckedStatement:
					case SyntaxKind.UncheckedExpression:
					case SyntaxKind.UncheckedStatement:
						return CreateClassificationSpan(snapshot, itemSpan, __GeneralClassifications.TypeCastKeyword);
					case SyntaxKind.Argument:
					case SyntaxKind.Parameter:
					case SyntaxKind.CrefParameter:
						switch (unitCompilation.FindToken(itemSpan.Start, true).Kind()) {
							case SyntaxKind.InKeyword:
							case SyntaxKind.OutKeyword:
							case SyntaxKind.RefKeyword:
								return CreateClassificationSpan(snapshot, itemSpan, __GeneralClassifications.TypeCastKeyword);
						}
						return null;
					case SyntaxKind.IdentifierName:
						if (node.Parent.IsKind(SyntaxKind.TypeConstraint) && itemSpan.Length == 9 && node.ToString() == "unmanaged") {
							goto case SyntaxKind.UnsafeStatement;
						}
						return null;
				}
				return null;
			}
			static TagSpan<IClassificationTag> ClassifyOperator(TextSpan itemSpan, ITextSnapshot snapshot, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation) {
				var node = unitCompilation.FindNode(itemSpan);
				if (node.RawKind == (int)SyntaxKind.DestructorDeclaration) {
					return CreateClassificationSpan(snapshot, itemSpan, TransientTags.DestructorDeclaration);
				}
				var opMethod = semanticModel.GetSymbol(node.IsKind(SyntaxKind.Argument) ? ((ArgumentSyntax)node).Expression : node) as IMethodSymbol;
				if (opMethod?.MethodKind == MethodKind.UserDefinedOperator) {
					if (node.RawKind == (int)SyntaxKind.OperatorDeclaration) {
						return CreateClassificationSpan(snapshot, itemSpan, TransientTags.OverrideDeclaration);
					}
					else {
						return CreateClassificationSpan(snapshot, itemSpan, __Classifications.OverrideMember);
					}
				}
				else if (opMethod?.MethodKind == MethodKind.LambdaMethod) {
					var l = ClassifyLambdaExpression(itemSpan, snapshot, semanticModel, unitCompilation);
					if (l != null) {
						return l;
					}
				}
				return null;
			}

			static TagSpan<IClassificationTag> ClassifyPunctuation(TextSpan itemSpan, ITextSnapshot snapshot, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation) {
				if (HighlightOptions.AllBraces && itemSpan.Length == 1) {
					switch (snapshot[itemSpan.Start]) {
						case '(':
						case ')':
							return HighlightOptions.AllParentheses ? ClassifyParentheses(itemSpan, snapshot, semanticModel, unitCompilation) : null;
						case '{':
						case '}':
							return ClassifyCurlyBraces(itemSpan, snapshot, unitCompilation);
						case '[':
						case ']':
							return ClassifyBrackets(itemSpan, snapshot, unitCompilation);
					}
				}
				return null;
			}

			static TagSpan<IClassificationTag> ClassifyCurlyBraces(TextSpan itemSpan, ITextSnapshot snapshot, CompilationUnitSyntax unitCompilation) {
				var node = unitCompilation.FindNode(itemSpan, true, true);
				if (node is BaseTypeDeclarationSyntax == false
					&& node is ExpressionSyntax == false
					&& node is NamespaceDeclarationSyntax == false
					&& node.Kind() != SyntaxKind.SwitchStatement && (node = node.Parent) == null) {
					return null;
				}
				var type = ClassifySyntaxNode(node, node is ExpressionSyntax ? HighlightOptions.MemberBraceTags : HighlightOptions.MemberDeclarationBraceTags, HighlightOptions.KeywordBraceTags);
				if (type != null) {
					return CreateClassificationSpan(snapshot, itemSpan, type);
				}
				return null;
			}

			static TagSpan<IClassificationTag> ClassifyParentheses(TextSpan itemSpan, ITextSnapshot snapshot, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation) {
				var node = unitCompilation.FindNode(itemSpan, true, true);
				switch (node.Kind()) {
					case SyntaxKind.CastExpression:
						return HighlightOptions.KeywordBraceTags.TypeCast != null
							&& semanticModel.GetSymbolInfo(((CastExpressionSyntax)node).Type).Symbol != null
							? CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.KeywordBraceTags.TypeCast)
							: null;
					case SyntaxKind.ParenthesizedExpression:
						return (HighlightOptions.KeywordBraceTags.TypeCast != null
							&& node.ChildNodes().FirstOrDefault().IsKind(SyntaxKind.AsExpression)
							&& semanticModel.GetSymbolInfo(((BinaryExpressionSyntax)node.ChildNodes().First()).Right).Symbol != null)
							? CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.KeywordBraceTags.TypeCast)
							: null;
					case SyntaxKind.SwitchStatement:
					case SyntaxKind.SwitchSection:
					case SyntaxKind.IfStatement:
					case SyntaxKind.ElseClause:
					case CodeAnalysisHelper.PositionalPatternClause:
						return CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.KeywordBraceTags.Branching);
					case SyntaxKind.ForStatement:
					case SyntaxKind.ForEachStatement:
					case SyntaxKind.ForEachVariableStatement:
					case SyntaxKind.WhileStatement:
					case SyntaxKind.DoStatement:
						return CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.KeywordBraceTags.Loop);
					case SyntaxKind.UsingStatement:
					case SyntaxKind.FixedStatement:
					case SyntaxKind.LockStatement:
					case SyntaxKind.UnsafeStatement:
					case SyntaxKind.TryStatement:
					case SyntaxKind.CatchDeclaration:
					case SyntaxKind.CatchClause:
					case SyntaxKind.CatchFilterClause:
					case SyntaxKind.FinallyClause:
						return CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.KeywordBraceTags.Resource);
					case SyntaxKind.ParenthesizedVariableDesignation:
						return CreateClassificationSpan(snapshot, itemSpan, node.Parent.IsKind(CodeAnalysisHelper.VarPattern) ? HighlightOptions.KeywordBraceTags.Branching : HighlightOptions.MemberBraceTags.Constructor);
					case SyntaxKind.TupleExpression:
					case SyntaxKind.TupleType:
						return CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.MemberBraceTags.Constructor);
					case SyntaxKind.CheckedExpression:
					case SyntaxKind.UncheckedExpression:
						return CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.KeywordBraceTags.TypeCast);
				}
				if (HighlightOptions.MemberBraceTags.Constructor != null) {
					// SpecialHighlightOptions.ParameterBrace or SpecialHighlightOptions.SpecialPunctuation is ON
					node = (node as BaseArgumentListSyntax
					   ?? node as BaseParameterListSyntax
					   ?? (CSharpSyntaxNode)(node as CastExpressionSyntax)
					   )?.Parent;
					if (node != null) {
						var type = ClassifySyntaxNode(node, HighlightOptions.MemberBraceTags, HighlightOptions.KeywordBraceTags);
						if (type != null) {
							return CreateClassificationSpan(snapshot, itemSpan, type);
						}
					}
				}
				return null;
			}

			static TagSpan<IClassificationTag> ClassifyBrackets(TextSpan itemSpan, ITextSnapshot snapshot, CompilationUnitSyntax unitCompilation) {
				// highlight attribute annotation
				var node = unitCompilation.FindNode(itemSpan, true, false);
				switch (node.Kind()) {
					case SyntaxKind.AttributeList:
						return CreateClassificationSpan(snapshot, node.Span, __Classifications.AttributeNotation);
					case SyntaxKind.ArrayRankSpecifier:
						return node.Parent.Parent.IsKind(SyntaxKind.ArrayCreationExpression)
							? CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.MemberBraceTags.Constructor)
							: null;
					case SyntaxKind.ImplicitStackAllocArrayCreationExpression:
					case CodeAnalysisHelper.ImplicitArrayCreationExpression:
						return CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.MemberBraceTags.Constructor);
					case SyntaxKind.Argument:
						return ((ArgumentSyntax)node).Expression.IsKind(SyntaxKind.ImplicitStackAllocArrayCreationExpression) ? CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.MemberBraceTags.Constructor) : null;
				}
				return null;
			}

			static TagSpan<IClassificationTag> ClassifyLambdaExpression(TextSpan itemSpan, ITextSnapshot snapshot, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation) {
				if (HighlightOptions.CapturingLambda) {
					var node = unitCompilation.FindNode(itemSpan, true, true);
					if (node is LambdaExpressionSyntax) {
						var ss = node.AncestorsAndSelf().FirstOrDefault(i => i is StatementSyntax || i is ExpressionSyntax && i.Kind() != SyntaxKind.IdentifierName);
						if (ss != null) {
							var df = semanticModel.AnalyzeDataFlow(ss);
							if (df.ReadInside.Any(i => (i as ILocalSymbol)?.IsConst != true && df.VariablesDeclared.Contains(i) == false)) {
								return CreateClassificationSpan(snapshot, itemSpan, __Classifications.ResourceKeyword);
							}
						}
					}
				}
				return null;
			}

			static TagSpan<IClassificationTag> ClassifyXmlDoc(TextSpan itemSpan, ITextSnapshot snapshot, CompilationUnitSyntax unitCompilation, ref TextSpan lastTriviaSpan) {
				if (lastTriviaSpan.Contains(itemSpan)) {
					return null;
				}
				var trivia = unitCompilation.FindTrivia(itemSpan.Start);
				switch (trivia.Kind()) {
					case SyntaxKind.SingleLineDocumentationCommentTrivia:
					case SyntaxKind.MultiLineDocumentationCommentTrivia:
					case SyntaxKind.DocumentationCommentExteriorTrivia:
						lastTriviaSpan = trivia.FullSpan;
						return CreateClassificationSpan(snapshot, lastTriviaSpan, __Classifications.XmlDoc);
				}
				return null;
			}

			static ClassificationTag ClassifySyntaxNode(SyntaxNode node, TransientMemberTagHolder tag, TransientKeywordTagHolder keyword) {
				switch (node.Kind()) {
					case SyntaxKind.MethodDeclaration:
					case SyntaxKind.AnonymousMethodExpression:
					case SyntaxKind.SimpleLambdaExpression:
					case SyntaxKind.ParenthesizedLambdaExpression:
					case SyntaxKind.LocalFunctionStatement:
					case SyntaxKind.ConversionOperatorDeclaration:
					case SyntaxKind.OperatorDeclaration:
						return tag.Method;
					case SyntaxKind.InvocationExpression:
						return ((((InvocationExpressionSyntax)node).Expression as IdentifierNameSyntax)?.Identifier.ValueText == "nameof") ? null : tag.Method;
					case SyntaxKind.ConstructorDeclaration:
					case SyntaxKind.BaseConstructorInitializer:
					case SyntaxKind.AnonymousObjectCreationExpression:
					case SyntaxKind.ObjectInitializerExpression:
					case SyntaxKind.ObjectCreationExpression:
					case SyntaxKind.ComplexElementInitializerExpression:
					case SyntaxKind.CollectionInitializerExpression:
					case SyntaxKind.ArrayInitializerExpression:
					case SyntaxKind.ThisConstructorInitializer:
					case SyntaxKind.DestructorDeclaration:
					case CodeAnalysisHelper.ImplicitObjectCreationExpression:
					case CodeAnalysisHelper.WithInitializerExpression:
						return tag.Constructor;
					case SyntaxKind.IndexerDeclaration:
					case SyntaxKind.PropertyDeclaration: return tag.Property;
					case SyntaxKind.ClassDeclaration:
					case CodeAnalysisHelper.RecordDeclaration:
						return tag.Class;
					case SyntaxKind.InterfaceDeclaration: return tag.Interface;
					case SyntaxKind.EnumDeclaration: return tag.Enum;
					case CodeAnalysisHelper.RecordStructDesclaration:
					case SyntaxKind.StructDeclaration:
						return tag.Struct;
					case SyntaxKind.Attribute: return __Classifications.AttributeName;
					case SyntaxKind.EventDeclaration: return tag.Event;
					case SyntaxKind.DelegateDeclaration: return tag.Delegate;
					case SyntaxKind.NamespaceDeclaration:
						return tag.Namespace;
					case SyntaxKind.IfStatement:
					case SyntaxKind.ElseClause:
					case SyntaxKind.SwitchStatement:
					case SyntaxKind.SwitchSection:
					case CodeAnalysisHelper.SwitchExpression:
					case CodeAnalysisHelper.RecursivePattern:
						return keyword.Branching;
					case SyntaxKind.ForStatement:
					case SyntaxKind.ForEachStatement:
					case SyntaxKind.ForEachVariableStatement:
					case SyntaxKind.WhileStatement:
					case SyntaxKind.DoStatement:
						return keyword.Loop;
					case SyntaxKind.UsingStatement:
					case SyntaxKind.LockStatement:
					case SyntaxKind.FixedStatement:
					case SyntaxKind.UnsafeStatement:
					case SyntaxKind.TryStatement:
					case SyntaxKind.CatchClause:
					case SyntaxKind.CatchFilterClause:
					case SyntaxKind.FinallyClause:
						return keyword.Resource;
					case SyntaxKind.CheckedStatement:
					case SyntaxKind.UncheckedStatement:
						return keyword.TypeCast;
				}
				return null;
			}

			static IEnumerable<ClassificationTag> GetClassificationType(SyntaxNode node, SemanticModel semanticModel) {
				node = node.Kind() == SyntaxKind.Argument ? ((ArgumentSyntax)node).Expression : node;
				//System.Diagnostics.Debug.WriteLine(node.GetType().Name + node.Span.ToString());
				var symbol = semanticModel.GetSymbolInfo(node).Symbol;
				if (symbol is null) {
					symbol = semanticModel.GetDeclaredSymbol(node);
					if (symbol is null) {
						// NOTE: handle alias in using directive
						if ((node.Parent as NameEqualsSyntax)?.Parent is UsingDirectiveSyntax) {
							yield return __Classifications.AliasNamespace;
						}
						else if (node is AttributeArgumentSyntax attributeArgument) {
							symbol = semanticModel.GetSymbolInfo(attributeArgument.Expression).Symbol;
							if (symbol?.Kind == SymbolKind.Field && (symbol as IFieldSymbol)?.IsConst == true) {
								yield return __Classifications.ConstField;
								yield return __Classifications.StaticMember;
							}
						}
						symbol = FindSymbolOrSymbolCandidateForNode(node, semanticModel);
						if (symbol is null) {
							yield break;
						}
					}
					else {
						switch (symbol.Kind) {
							case SymbolKind.NamedType:
								yield return symbol.ContainingType != null ? __Classifications.NestedDeclaration : __Classifications.Declaration;
								break;
							case SymbolKind.Event:
								yield return __Classifications.NestedDeclaration;
								break;
							case SymbolKind.Method:
								if (HighlightOptions.LocalFunctionDeclaration
									|| ((IMethodSymbol)symbol).MethodKind != MethodKind.LocalFunction) {
									yield return __Classifications.NestedDeclaration;
								}
								break;
							case SymbolKind.Property:
								if (symbol.ContainingType.IsAnonymousType == false) {
									yield return __Classifications.NestedDeclaration;
								}
								break;
							case SymbolKind.Field:
								if (node.IsKind(SyntaxKind.TupleElement)) {
									if (((TupleElementSyntax)node).Identifier.IsKind(SyntaxKind.None)) {
										symbol = semanticModel.GetTypeInfo(((TupleElementSyntax)node).Type).Type;
										if (symbol is null) {
											yield break;
										}
									}
								}
								else if (HighlightOptions.NonPrivateField
									&& symbol.DeclaredAccessibility >= Accessibility.ProtectedAndInternal
									&& symbol.ContainingType.TypeKind != TypeKind.Enum) {
									yield return __Classifications.NestedDeclaration;
								}
								break;
							case SymbolKind.Local:
								yield return __Classifications.LocalDeclaration;
								break;
						}
					}
				}
				switch (symbol.Kind) {
					case SymbolKind.Alias:
					case SymbolKind.ArrayType:
					case SymbolKind.Assembly:
					case SymbolKind.DynamicType:
					case SymbolKind.ErrorType:
					case SymbolKind.NetModule:
					case SymbolKind.PointerType:
					case SymbolKind.RangeVariable:
					case SymbolKind.Preprocessing:
						//case SymbolKind.Discard:
						yield break;

					case SymbolKind.Label:
						yield return __Classifications.Label;
						yield break;

					case SymbolKind.TypeParameter:
						yield return __Classifications.TypeParameter;
						yield break;

					case SymbolKind.Field:
						var f = symbol as IFieldSymbol;
						if (f.IsConst) {
							yield return f.ContainingType.TypeKind == TypeKind.Enum ? __Classifications.EnumField : __Classifications.ConstField;
						}
						else {
							yield return f.IsReadOnly ? __Classifications.ReadOnlyField
								: f.IsVolatile ? __Classifications.VolatileField
								: __Classifications.Field;
						}
						break;

					case SymbolKind.Property:
						yield return __Classifications.Property;
						break;

					case SymbolKind.Event:
						yield return __Classifications.Event;
						break;

					case SymbolKind.Local:
						yield return ((ILocalSymbol)symbol).IsConst ? __Classifications.ConstField : __Classifications.LocalVariable;
						break;

					case SymbolKind.Namespace:
						yield return __Classifications.Namespace;
						yield break;

					case SymbolKind.Parameter:
						yield return __Classifications.Parameter;
						break;

					case SymbolKind.Method:
						var methodSymbol = symbol as IMethodSymbol;
						switch (methodSymbol.MethodKind) {
							case MethodKind.Constructor:
								yield return
									node is AttributeSyntax || node.Parent is AttributeSyntax || node.Parent?.Parent is AttributeSyntax
										? __Classifications.AttributeName
										: HighlightOptions.StyleConstructorAsType
										? (methodSymbol.ContainingType.TypeKind == TypeKind.Struct ? __Classifications.StructName : __Classifications.ClassName)
										: __Classifications.ConstructorMethod;
								break;
							case MethodKind.Destructor:
							case MethodKind.StaticConstructor:
								yield return HighlightOptions.StyleConstructorAsType
										? (methodSymbol.ContainingType.TypeKind == TypeKind.Struct ? __Classifications.StructName : __Classifications.ClassName)
										: __Classifications.ConstructorMethod;
								break;
							default:
								yield return methodSymbol.IsExtensionMethod ? __Classifications.ExtensionMethod
									: methodSymbol.IsExtern ? __Classifications.ExternMethod
									: __Classifications.Method;
								break;
						}
						break;

					case SymbolKind.NamedType:
						break;

					default:
						yield break;
				}

				if (SymbolMarkManager.HasBookmark) {
					var markerStyle = SymbolMarkManager.GetSymbolMarkerStyle(symbol);
					if (markerStyle != null) {
						yield return markerStyle;
					}
				}

				if (FormatStore.IdentifySymbolSource && symbol.IsMemberOrType() && symbol.ContainingAssembly != null) {
					yield return symbol.ContainingAssembly.GetSourceType() == AssemblySource.Metadata
						? __Classifications.MetadataSymbol
						: __Classifications.UserSymbol;
				}

				if (symbol.IsStatic) {
					if (symbol.Kind != SymbolKind.Namespace) {
						yield return __Classifications.StaticMember;
					}
				}
				else if (symbol.IsSealed) {
					ITypeSymbol type;
					if (symbol.Kind == SymbolKind.NamedType
						&& (type = (ITypeSymbol)symbol).TypeKind == TypeKind.Struct) {
						if (type.IsReadOnly()) {
							yield return __Classifications.ReadOnlyStruct;
						}
						if (type.IsRefLike()) {
							yield return __Classifications.RefStruct;
						}
						yield break;
					}
					yield return __Classifications.SealedMember;
				}
				else if (symbol.IsOverride) {
					yield return __Classifications.OverrideMember;
				}
				else if (symbol.IsVirtual) {
					yield return __Classifications.VirtualMember;
				}
				else if (symbol.IsAbstract) {
					yield return __Classifications.AbstractMember;
				}
			}

			static ISymbol FindSymbolOrSymbolCandidateForNode(SyntaxNode node, SemanticModel semanticModel) {
				return node.Parent.IsKind(SyntaxKind.SimpleMemberAccessExpression) ? semanticModel.GetSymbolInfo(node.Parent).CandidateSymbols.FirstOrDefault()
					: node.Parent.IsKind(SyntaxKind.Argument) ? semanticModel.GetSymbolInfo(((ArgumentSyntax)node.Parent).Expression).CandidateSymbols.FirstOrDefault()
					: node.IsKind(SyntaxKind.SimpleBaseType) ? semanticModel.GetTypeInfo(((SimpleBaseTypeSyntax)node).Type).Type
					: node.IsKind(SyntaxKind.TypeConstraint) ? semanticModel.GetTypeInfo(((TypeConstraintSyntax)node).Type).Type
					: node.IsKind(SyntaxKind.ExpressionStatement) ? semanticModel.GetSymbolInfo(((ExpressionStatementSyntax)node).Expression).CandidateSymbols.FirstOrDefault()
					: semanticModel.GetSymbolInfo(node).CandidateSymbols.FirstOrDefault();
			}

			static TagSpan<IClassificationTag> CreateClassificationSpan(ITextSnapshot snapshotSpan, TextSpan span, ClassificationTag tag) {
				return tag != null ? new TagSpan<IClassificationTag>(new SnapshotSpan(snapshotSpan, span.Start, span.Length), tag) : null;
			}
		}

		static class TransientTags
		{
			public static readonly ClassificationTag ClassDeclaration = ClassificationTagHelper.CreateTransientClassificationTag(__Classifications.ClassName.ClassificationType, ClassificationTagHelper.Declaration);
			public static readonly ClassificationTag StructDeclaration = ClassificationTagHelper.CreateTransientClassificationTag(__Classifications.StructName.ClassificationType, ClassificationTagHelper.Declaration);
			public static readonly ClassificationTag DestructorDeclaration = ClassificationTagHelper.CreateTransientClassificationTag(__Classifications.ResourceKeyword, __Classifications.Declaration);
			public static readonly ClassificationTag OverrideDeclaration = ClassificationTagHelper.CreateTransientClassificationTag(__Classifications.OverrideMember, __Classifications.Declaration);
		}

		static class HighlightOptions
		{
			static readonly bool __Dummy = Init();
			public static TransientKeywordTagHolder KeywordBraceTags { get; private set; }
			public static TransientMemberTagHolder MemberDeclarationBraceTags { get; private set; }
			public static TransientMemberTagHolder MemberBraceTags { get; private set; }

			// use fields to cache option flags
			public static bool AllBraces, AllParentheses, LocalFunctionDeclaration, NonPrivateField, StyleConstructorAsType, CapturingLambda;

			static bool Init() {
				Config.RegisterUpdateHandler(UpdateCSharpHighlighterConfig);
				UpdateCSharpHighlighterConfig(new ConfigUpdatedEventArgs(Config.Instance, Features.SyntaxHighlight));
				return true;
			}

			static void UpdateCSharpHighlighterConfig(ConfigUpdatedEventArgs e) {
				if (e.UpdatedFeature.MatchFlags(Features.SyntaxHighlight) == false) {
					return;
				}
				var o = e.Config.SpecialHighlightOptions;
				AllBraces = o.HasAnyFlag(SpecialHighlightOptions.AllBraces);
				AllParentheses = o.HasAnyFlag(SpecialHighlightOptions.AllParentheses);
				CapturingLambda = o.MatchFlags(SpecialHighlightOptions.CapturingLambdaExpression);
				var sp = o.MatchFlags(SpecialHighlightOptions.SpecialPunctuation);
				if (sp) {
					KeywordBraceTags = TransientKeywordTagHolder.BoldBraces.Clone();
					MemberBraceTags = TransientMemberTagHolder.BoldBraces.Clone();
					MemberDeclarationBraceTags = TransientMemberTagHolder.BoldDeclarationBraces.Clone();
				}
				else {
					KeywordBraceTags = TransientKeywordTagHolder.Default.Clone();
					MemberBraceTags = TransientMemberTagHolder.Default.Clone();
					MemberDeclarationBraceTags = TransientMemberTagHolder.DeclarationBraces.Clone();
				}
				if (o.MatchFlags(SpecialHighlightOptions.BranchBrace) == false) {
					KeywordBraceTags.Branching = sp ? ClassificationTagHelper.BoldBraceTag : null;
				}
				if (o.MatchFlags(SpecialHighlightOptions.CastBrace) == false) {
					KeywordBraceTags.TypeCast = sp ? ClassificationTagHelper.BoldBraceTag : null;
				}
				if (o.MatchFlags(SpecialHighlightOptions.LoopBrace) == false) {
					KeywordBraceTags.Loop = sp ? ClassificationTagHelper.BoldBraceTag : null;
				}
				if (o.MatchFlags(SpecialHighlightOptions.ResourceBrace) == false) {
					KeywordBraceTags.Resource = sp ? ClassificationTagHelper.BoldBraceTag : null;
				}
				if (o.MatchFlags(SpecialHighlightOptions.ParameterBrace) == false) {
					MemberBraceTags.Constructor = sp ? ClassificationTagHelper.BoldBraceTag : null;
					MemberBraceTags.Method = sp ? ClassificationTagHelper.BoldBraceTag : null;
				}
				if (o.MatchFlags(SpecialHighlightOptions.DeclarationBrace) == false) {
					MemberDeclarationBraceTags.Class
						= MemberDeclarationBraceTags.Constructor
						= MemberDeclarationBraceTags.Delegate
						= MemberDeclarationBraceTags.Enum
						= MemberDeclarationBraceTags.Event
						= MemberDeclarationBraceTags.Field
						= MemberDeclarationBraceTags.Interface
						= MemberDeclarationBraceTags.Method
						= MemberDeclarationBraceTags.Namespace
						= MemberDeclarationBraceTags.Property
						= MemberDeclarationBraceTags.Struct
						= sp ? ClassificationTagHelper.BoldDeclarationBraceTag : ClassificationTagHelper.DeclarationBraceTag;
				}
				LocalFunctionDeclaration = o.MatchFlags(SpecialHighlightOptions.LocalFunctionDeclaration);
				NonPrivateField = o.MatchFlags(SpecialHighlightOptions.NonPrivateField);
				StyleConstructorAsType = o.MatchFlags(SpecialHighlightOptions.UseTypeStyleOnConstructor);
			}
		}
	}
}