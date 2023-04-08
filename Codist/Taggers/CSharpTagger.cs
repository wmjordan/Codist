using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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

namespace Codist.Taggers
{
	/// <summary>A classifier for C# code syntax highlight.</summary>
	sealed class CSharpTagger : IDisposable
	{
		static readonly CSharpClassifications __Classifications = CSharpClassifications.Instance;
		static readonly GeneralClassifications __GeneralClassifications = GeneralClassifications.Instance;

		readonly Dictionary<ITextBuffer, BufferTagger> _Taggers = new Dictionary<ITextBuffer, BufferTagger>();
		CSharpTaggerProvider _TaggerProvider;
		IWpfTextView _View;
		// cache the latest used tagger to improve performance
		BufferTagger _LastTagger;
		// used in Interactive window to detect #reset
		Guid _LastSolutionId;
		readonly bool _IsInteractiveWindow;

		public CSharpTagger(CSharpTaggerProvider taggerProvider, IWpfTextView view) {
			_View = view;
			_TaggerProvider = taggerProvider;
			_IsInteractiveWindow = IsInteractiveWindow(view.TextBuffer);
		}

		public ITextView View => _View;

		public ITagger<IClassificationTag> GetTagger(ITextBuffer buffer) {
			BufferTagger tagger;
			if (buffer != _LastTagger?.Buffer) {
				if (_Taggers.TryGetValue(buffer, out tagger) == false) {
					_Taggers.Add(buffer, tagger = new BufferTagger(this, _View, buffer));
				}
				if (_IsInteractiveWindow && _LastTagger != null) {
					_LastTagger.ReleaseAsyncTimer();
				}
				_LastTagger = tagger;
			}
			else {
				tagger = _LastTagger;
			}
			tagger.Ref();
			return tagger;
		}

		public void Dispose() {
			if (_TaggerProvider != null) {
				if (_Taggers.Count > 0) {
					// don't iterate _Taggers, since the Release method will change the collection
					foreach (var item in _Taggers.ToList() /* hold taggers to be disposed */) {
						item.Value.Release();
					}
					_Taggers.Clear();
				}
				_LastTagger = null;
				_TaggerProvider = null;
				_View = null;
			}
		}

		static bool IsInteractiveWindow(ITextBuffer buffer) {
			return buffer.Properties.PropertyList.Any(o => (o.Key as Type)?.Name == "InteractiveWindow");
		}

		static IEnumerable<SnapshotSpan> MapToOldSpans(NormalizedSnapshotSpanCollection spans, ITextSnapshot last) {
			foreach (var item in spans) {
				yield return item.TranslateTo(last, SpanTrackingMode.EdgeInclusive);
			}
		}

		[DebuggerDisplay("{GetDebuggerString()}")]
		sealed class BufferTagger : ITagger<IClassificationTag>, IDisposable
		{
			ConcurrentQueue<SnapshotSpan> _PendingSpans = new ConcurrentQueue<SnapshotSpan>();
			IWpfTextView _View;
			CSharpTagger _Container;
			ITextBuffer _Buffer;
			CancellationTokenSource _TaskBreaker;
			AsyncParser _Parser;
			Timer _Timer;
			ParseResult _ParseResult;
			bool _HasFocus;
			bool _HasBackgroundChange;
			ITextSnapshot _WorkingSnapshot;
			int _Ref;
			readonly bool _IsInteractiveWindow;
			// debug info
			readonly string _Name;

			public BufferTagger(CSharpTagger container, IWpfTextView view, ITextBuffer buffer) {
				_Name = buffer.GetTextDocument()?.FilePath ?? buffer.CurrentSnapshot?.GetText(0, Math.Min(buffer.CurrentSnapshot.Length, 500));
				if (view != null) {
					_View = view;
					if ((_IsInteractiveWindow = container._IsInteractiveWindow) == false) {
						view.GotAggregateFocus += View_IsFocused;
						view.LostAggregateFocus += View_LostFocus;
					}
				}
				_Container = container;
				_Buffer = buffer;
				_Timer = new Timer(StartAsyncParser);
				_Parser = new AsyncParser(ReceivedParserResult);
				SubscribeBufferEvents(buffer);
			}

			internal bool IsDisposed => _Parser == null;
			internal ITextBuffer Buffer => _Buffer;

			/// <summary>This event is to notify the IDE that some tags are classified (from the parser thread).</summary>
			public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

			public void Ref() {
				_Ref++;
				Debug.WriteLine($"Ref+ {_Ref} {_Name}");
			}

			internal void Release() {
				if (Interlocked.Exchange(ref _Parser, null) != null) {
					Debug.WriteLine($"{_Name} tagger released");
					ReleaseResources();
					_Ref = 0;
				}
			}

			IEnumerable<ITagSpan<IClassificationTag>> ITagger<IClassificationTag>.GetTags(NormalizedSnapshotSpanCollection spans) {
				if (_Parser == null) {
					goto NA;
				}
				var snapshot = spans[0].Snapshot;
				var isNewSnapshot = _ParseResult?.Snapshot != snapshot;
				if (isNewSnapshot == false) {
					try {
						return Tagger.GetTags(spans, _ParseResult, _TaskBreaker.GetToken());
					}
					catch (OperationCanceledException) {
						goto NA;
					}
				}
				if (snapshot.TextBuffer != _Buffer) {
					goto NA;
				}
				if (snapshot != _WorkingSnapshot) {
					_WorkingSnapshot = snapshot;
					// don't schedule parsing unless snapshot is changed
					if (_Parser.State == ParserState.Working) {
						// cancel existing parsing
						_TaskBreaker?.Cancel();
					}
					_Timer.Change(_ParseResult != null ? 300 : 100, Timeout.Infinite);
				}
				if (isNewSnapshot) {
					// queue the spans to be updated after parsing
					EnqueueSpans(spans);
				}
				if (_ParseResult != null && _ParseResult.Snapshot.TextBuffer == snapshot.TextBuffer) {
					try {
						return UseOldResult(spans, snapshot, _ParseResult, _TaskBreaker.GetToken());
					}
					catch (OperationCanceledException) {
						goto NA;
					}
				}
			NA:
				return Enumerable.Empty<ITagSpan<IClassificationTag>>();
			}

			void ReceivedParserResult(ParseResult result) {
				var oldResult = Interlocked.Exchange(ref _ParseResult, result);
				if (_IsInteractiveWindow) {
					RemoveTaggersOnSolutionChange(result);
				}
				else if (oldResult == null) {
					result.Workspace.WorkspaceChanged += WorkspaceChanged;
				}
				else if (oldResult.Workspace != result.Workspace) {
					oldResult.Workspace.WorkspaceChanged -= WorkspaceChanged;
					result.Workspace.WorkspaceChanged += WorkspaceChanged;
				}
				RefreshPendingSpans();
				if (IsDisposed) {
					// prevent leak after disposal
					ReleaseResources();
				}
			}

			void RemoveTaggersOnSolutionChange(ParseResult result) {
				Guid id;
				var c = _Container;
				if (c == null || result.Workspace.CurrentSolution.Id.Id == (id = c._LastSolutionId)) {
					return;
				}

				// don't iterate _Taggers, since the Release method will change the collection
				foreach (var item in c._Taggers.ToList()) {
					if (item.Value._ParseResult?.Workspace.CurrentSolution.Id.Id != id) {
						item.Value.Release();
					}
				}
				c._LastSolutionId = result.Workspace.CurrentSolution.Id.Id;
				TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(c._View.GetVisibleLineSpan()));
			}

			void View_IsFocused(object sender, EventArgs e) {
				_HasFocus = true;
				if (_HasBackgroundChange) {
					_HasBackgroundChange = false;
					if (_ParseResult != null) {
						_Timer.Change(300, Timeout.Infinite);
						_PendingSpans.Enqueue(_View.GetVisibleLineSpan());
					}
				}
			}

			void View_LostFocus(object sender, EventArgs e) {
				_HasFocus = false;
			}

			void StartAsyncParser(object state) {
				_Parser?.Start(_Buffer, SyncHelper.CancelAndRetainToken(ref _TaskBreaker));
			}

			void EnqueueSpans(NormalizedSnapshotSpanCollection spans) {
				foreach (var item in spans) {
					Debug.WriteLine($"Enqueue span {item}");
					_PendingSpans.Enqueue(item);
				}
			}

			void RefreshPendingSpans() {
				var pendingSpans = _PendingSpans;
				if (pendingSpans != null) {
					var snapshot = _ParseResult.Snapshot;
					if (snapshot != null) {
						while (pendingSpans.TryDequeue(out var span)) {
							if (snapshot == span.Snapshot) {
								Debug.WriteLine($"Refresh span {span}");
								TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
							}
						}
					}
				}
			}

			IEnumerable<ITagSpan<IClassificationTag>> UseOldResult(NormalizedSnapshotSpanCollection spans, ITextSnapshot snapshot, ParseResult result, CancellationToken cancellationToken) {
				foreach (var tagSpan in Tagger.GetTags(MapToOldSpans(spans, _ParseResult.Snapshot), result, cancellationToken)) {
					yield return new TagSpan<IClassificationTag>(tagSpan.Span.TranslateTo(snapshot, SpanTrackingMode.EdgeInclusive), tagSpan.Tag);
				}
			}

			void SubscribeBufferEvents(ITextBuffer buffer) {
				if (buffer is ITextBuffer2 b) {
					b.ChangedOnBackground += TextBuffer_ChangedOnBackground;
				}
				buffer.ContentTypeChanged += TextBuffer_ContentTypeChanged;
			}

			void UnsubscribeBufferEvents(ITextBuffer buffer) {
				if (buffer != null) {
					if (buffer is ITextBuffer2 b) {
						b.ChangedOnBackground -= TextBuffer_ChangedOnBackground;
					}
					buffer.ContentTypeChanged -= TextBuffer_ContentTypeChanged;
				}
			}

			void WorkspaceChanged(object sender, WorkspaceChangeEventArgs args) {
				Debug.WriteLine($"Workspace {args.Kind}: {args.DocumentId}");
				var parser = _Parser;
				if (parser == null) {
					return;
				}
				switch (args.Kind) {
					case WorkspaceChangeKind.DocumentChanged:
					case WorkspaceChangeKind.DocumentRemoved:
						if (args.DocumentId == _ParseResult?.DocumentId) {
							// skip notifications from current document
							return;
						}
						break;
				}

				// at this place, we will receive the following change notifications:
				// 1. from solution or project change
				// 2. from the same document, but various configurations (should be omitted)
				//   -- for instance, while editing a.cs (net45), we will get notified from a.cs (net50), a.cs (net60) etc. as well
				if (_HasFocus) {
					if (args.Kind != WorkspaceChangeKind.DocumentChanged) {
						// reparse occurs after current document is parsed and external document change is received
						// timer may have been disposed in other thread, thus we check it again
						_Timer?.Change(300, Timeout.Infinite);
					}
				}
				else if (parser.State == ParserState.Idle) {
					_HasBackgroundChange = true;
				}
			}

			void TextBuffer_ChangedOnBackground(object sender, TextContentChangedEventArgs e) {
				var changes = e.Changes;
				if (changes.Count == 0) {
					return;
				}
				var tc = TagsChanged;
				if (tc != null) {
					foreach (var item in changes) {
						var changedSpan = new SnapshotSpan(e.After, item.NewSpan);
						Debug.WriteLine($"{_Buffer.GetDocument().GetDocId()} changed: {changedSpan}");
						tc.Invoke(this, new SnapshotSpanEventArgs(changedSpan));
					}
				}
			}

			void TextBuffer_ContentTypeChanged(object sender, ContentTypeChangedEventArgs e) {
				if (_IsInteractiveWindow == false
					&& e.AfterContentType.IsOfType(Constants.CodeTypes.CSharp) == false) {
					Dispose();
				}
			}

			public void Dispose() {
				if (--_Ref > 0) {
					Debug.WriteLine($"Ref- {_Ref} {_Name}");
					return;
				}
				if (Interlocked.Exchange(ref _Parser, null) != null) {
					Debug.WriteLine($"{_Name} tagger disposed");
					ReleaseResources();
				}
			}

			internal void ReleaseAsyncTimer() {
				if (_TaskBreaker != null) {
					_TaskBreaker.Cancel();
					_TaskBreaker.Dispose();
					_TaskBreaker = null;
				}
				_Timer?.Dispose();
				_Timer = null;
			}

			void ReleaseResources() {
				if (_View != null) {
					_View.GotAggregateFocus -= View_IsFocused;
					_View.LostAggregateFocus -= View_LostFocus;
					_View = null;
				}
				if (_TaskBreaker != null) {
					_TaskBreaker.Cancel();
					_TaskBreaker.Dispose();
					_TaskBreaker = null;
				}
				if (_Buffer != null) {
					UnsubscribeBufferEvents(_Buffer);
					_Container._Taggers.Remove(_Buffer);
					_Container = null;
					_Buffer = null;
				}
				_PendingSpans = null;
				if (_IsInteractiveWindow == false && _ParseResult?.Workspace != null) {
					_ParseResult.Workspace.WorkspaceChanged -= WorkspaceChanged;
				}
				_ParseResult = default;
				_Timer?.Dispose();
				_Timer = null;
				_WorkingSnapshot = null;
			}

			string GetDebuggerString() {
				return $"{_Parser?.State} {_Ref} ({_Name})";
			}
		}

		sealed class ParseResult
		{
			public readonly Workspace Workspace;
			public readonly SemanticModel Model;
			public readonly ITextSnapshot Snapshot;
			public readonly DocumentId DocumentId;

			public ParseResult(Workspace workspace, SemanticModel model, ITextSnapshot snapshot, DocumentId documentId) {
				Workspace = workspace;
				Model = model;
				Snapshot = snapshot;
				DocumentId = documentId;
			}
		}

		sealed class AsyncParser
		{
			readonly Action<ParseResult> _Callback;
			int _State;

			public AsyncParser(Action<ParseResult> callback) {
				_Callback = callback;
			}

			public ParserState State => (ParserState)_State;

			public bool Start(ITextBuffer buffer, CancellationToken cancellationToken) {
				return Interlocked.CompareExchange(ref _State, (int)ParserState.Working, (int)ParserState.Idle) == (int)ParserState.Idle
					&& Task.Run(() => {
						ITextSnapshot snapshot = buffer.CurrentSnapshot;
						var workspace = Workspace.GetWorkspaceRegistration(buffer.AsTextContainer()).Workspace;
						if (workspace == null) {
							goto QUIT;
						}
						Document document;
						try {
							document = workspace.GetDocument(buffer);
						}
						catch (InvalidOperationException) {
							goto QUIT;
						}
						return ParseAsync(workspace, document, snapshot, cancellationToken);
					QUIT:
						Interlocked.CompareExchange(ref _State, (int)ParserState.Idle, (int)ParserState.Working);
						return Task.CompletedTask;
					}).IsCanceled == false;
			}

			async Task ParseAsync(Workspace workspace, Document document, ITextSnapshot snapshot, CancellationToken cancellationToken) {
				SemanticModel model;
				try {
					model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException) {
					Interlocked.CompareExchange(ref _State, (int)ParserState.Idle, (int)ParserState.Working);
					throw;
				}
				if (Interlocked.CompareExchange(ref _State, (int)ParserState.Completed, (int)ParserState.Working) == (int)ParserState.Working) {
					Debug.WriteLine($"{snapshot.TextBuffer.GetDocument().GetDocId()} end parsing {snapshot.Version} on thread {Thread.CurrentThread.ManagedThreadId}");
					_Callback(new ParseResult(workspace, model, snapshot, document.Id));
					Interlocked.CompareExchange(ref _State, (int)ParserState.Idle, (int)ParserState.Completed);
				}
				else {
					Interlocked.CompareExchange(ref _State, (int)ParserState.Idle, (int)ParserState.Working);
				}
			}
		}

		enum ParserState
		{
			Idle,
			Working,
			Completed,
		}

		static class Tagger
		{
			public static Chain<ITagSpan<IClassificationTag>> GetTags(IEnumerable<SnapshotSpan> spans, ParseResult result, CancellationToken cancellationToken) {
				var workspace = result.Workspace;
				var semanticModel = result.Model;
				var unitCompilation = semanticModel.SyntaxTree.GetCompilationUnitRoot(cancellationToken);
				var snapshot = result.Snapshot;
				var l = semanticModel.SyntaxTree.Length;
				var tags = new Chain<ITagSpan<IClassificationTag>>();
				foreach (var span in spans) {
					if (span.End > l || cancellationToken.IsCancellationRequested) {
						return tags;
					}
					var textSpan = new TextSpan(span.Start.Position, span.Length);
					var classifiedSpans = Classifier.GetClassifiedSpans(semanticModel, textSpan, workspace, cancellationToken);
					var lastTriviaSpan = default(TextSpan);
					SyntaxNode node;
					TagSpan<IClassificationTag> tag = null;
					var r = GetAttributeNotationSpan(snapshot, textSpan, unitCompilation);
					if (r != null) {
						tags.Add(r);
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
										tags.Add(tag2);
									}
								}
								else {
									tag = ClassifyKeyword(item.TextSpan, snapshot, node, unitCompilation);
								}
								break;
							case Constants.CodeOperator:
							case Constants.CodeOverloadedOperator:
								tag = ClassifyOperator(item.TextSpan, snapshot, semanticModel, unitCompilation, cancellationToken);
								break;
							case Constants.CodePunctuation:
								tag = ClassifyPunctuation(item.TextSpan, snapshot, semanticModel, unitCompilation, cancellationToken);
								break;
							case Constants.XmlDocDelimiter:
								tag = ClassifyXmlDoc(item.TextSpan, snapshot, unitCompilation, ref lastTriviaSpan);
								break;
							default:
								tag = null;
								break;
						}
						if (tag != null) {
							tags.Add(tag);
							continue;
						}
						if (ct == Constants.CodeIdentifier
							//|| ct == Constants.CodeStaticSymbol
							|| ct.EndsWith("name", StringComparison.Ordinal)) {
							var itemSpan = item.TextSpan;
							node = unitCompilation.FindNode(itemSpan, true);
							foreach (var type in GetClassificationType(node, semanticModel, cancellationToken)) {
								tags.Add(CreateClassificationSpan(snapshot, itemSpan, type));
							}
						}
					}
				}
				return tags;
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
						return CreateClassificationSpan(snapshot,
							itemSpan,
							node.IsKind(CodeAnalysisHelper.RecordDeclaration)
								? TransientTags.StructDeclaration
								: TransientTags.ClassDeclaration);
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
					case SyntaxKind.ImplicitStackAllocArrayCreationExpression:
					case CodeAnalysisHelper.FunctionPointerCallingConvention:
						return CreateClassificationSpan(snapshot, itemSpan, __Classifications.ResourceKeyword);
					case SyntaxKind.LocalDeclarationStatement:
						if (unitCompilation.FindToken(itemSpan.Start, true).IsKind(SyntaxKind.UsingKeyword)) {
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
						if (node.Parent.IsKind(SyntaxKind.TypeConstraint)
							&& itemSpan.Length == 9
							&& node.ToString() == "unmanaged") {
							goto case SyntaxKind.UnsafeStatement;
						}
						return null;
				}
				return null;
			}
			static TagSpan<IClassificationTag> ClassifyOperator(TextSpan itemSpan, ITextSnapshot snapshot, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation, CancellationToken cancellationToken) {
				var node = unitCompilation.FindNode(itemSpan);
				if (node.RawKind == (int)SyntaxKind.DestructorDeclaration) {
					return CreateClassificationSpan(snapshot, itemSpan, TransientTags.DestructorDeclaration);
				}
				var opMethod = semanticModel.GetSymbol(node.IsKind(SyntaxKind.Argument) ? ((ArgumentSyntax)node).Expression : node, cancellationToken) as IMethodSymbol;
				if (opMethod?.MethodKind == MethodKind.UserDefinedOperator) {
					return CreateClassificationSpan(snapshot,
						itemSpan,
						node.RawKind == (int)SyntaxKind.OperatorDeclaration
							? TransientTags.OverrideDeclaration
							: __Classifications.OverrideMember);
				}
				if (opMethod?.MethodKind == MethodKind.LambdaMethod) {
					var l = ClassifyLambdaExpression(itemSpan, snapshot, semanticModel, unitCompilation);
					if (l != null) {
						return l;
					}
				}
				return null;
			}

			static TagSpan<IClassificationTag> ClassifyPunctuation(TextSpan itemSpan, ITextSnapshot snapshot, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation, CancellationToken cancellationToken) {
				if (HighlightOptions.AllBraces && itemSpan.Length == 1) {
					switch (snapshot[itemSpan.Start]) {
						case '(':
						case ')':
							return HighlightOptions.AllParentheses ? ClassifyParentheses(itemSpan, snapshot, semanticModel, unitCompilation, cancellationToken) : null;
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
					&& !node.IsKind(SyntaxKind.SwitchStatement)
					&& (node = node.Parent) == null) {
					return null;
				}
				var type = ClassifySyntaxNode(node, node is ExpressionSyntax ? HighlightOptions.MemberBraceTags : HighlightOptions.MemberDeclarationBraceTags, HighlightOptions.KeywordBraceTags);
				return type != null
					? CreateClassificationSpan(snapshot, itemSpan, type)
					: null;
			}

			static TagSpan<IClassificationTag> ClassifyParentheses(TextSpan itemSpan, ITextSnapshot snapshot, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation, CancellationToken cancellationToken) {
				var node = unitCompilation.FindNode(itemSpan, true, true);
				switch (node.Kind()) {
					case SyntaxKind.CastExpression:
						return HighlightOptions.KeywordBraceTags.TypeCast != null
								&& semanticModel.GetSymbolInfo(((CastExpressionSyntax)node).Type, cancellationToken).Symbol != null
							? CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.KeywordBraceTags.TypeCast)
							: null;
					case SyntaxKind.ParenthesizedExpression:
						return (HighlightOptions.KeywordBraceTags.TypeCast != null
								&& node.ChildNodes().FirstOrDefault().IsKind(SyntaxKind.AsExpression)
								&& semanticModel.GetSymbolInfo(((BinaryExpressionSyntax)node.ChildNodes().First()).Right, cancellationToken).Symbol != null)
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
						return CreateClassificationSpan(snapshot, itemSpan, node.Parent.IsKind(CodeAnalysisHelper.VarPattern)
							? HighlightOptions.KeywordBraceTags.Branching
							: HighlightOptions.MemberBraceTags.Constructor);
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
					case SyntaxKind.ImplicitArrayCreationExpression:
						return CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.MemberBraceTags.Constructor);
					case SyntaxKind.Argument:
						return ((ArgumentSyntax)node).Expression.IsKind(SyntaxKind.ImplicitStackAllocArrayCreationExpression)
							? CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.MemberBraceTags.Constructor)
							: null;
				}
				return null;
			}

			static TagSpan<IClassificationTag> ClassifyLambdaExpression(TextSpan itemSpan, ITextSnapshot snapshot, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation) {
				if (HighlightOptions.CapturingLambda) {
					var node = unitCompilation.FindNode(itemSpan, true, true);
					if (node is LambdaExpressionSyntax) {
						var ss = node.AncestorsAndSelf().FirstOrDefault(i => i is StatementSyntax || i is ExpressionSyntax && i.IsKind(SyntaxKind.IdentifierName) == false);
						if (ss != null) {
							var df = semanticModel.AnalyzeDataFlow(ss);
							if (df.ReadInside.Any(i => (i as ILocalSymbol)?.IsConst != true && df.VariablesDeclared.Contains(i) == false)) {
								return CreateClassificationSpan(snapshot, itemSpan, TransientKeywordTagHolder.Bold.Resource);
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
					case CodeAnalysisHelper.RecordStructDeclaration:
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

			static Chain<ClassificationTag> GetClassificationType(SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken) {
				node = node.IsKind(SyntaxKind.Argument) ? ((ArgumentSyntax)node).Expression : node;
				//System.Diagnostics.Debug.WriteLine(node.GetType().Name + node.Span.ToString());
				var symbol = semanticModel.GetSymbolInfo(node, cancellationToken).Symbol;
				var tags = new Chain<ClassificationTag>();
				if (symbol is null) {
					symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken);
					if (symbol is null) {
						// NOTE: handle alias in using directive
						if ((node.Parent as NameEqualsSyntax)?.Parent is UsingDirectiveSyntax) {
							tags.Add(__Classifications.AliasNamespace);
						}
						else if (node is AttributeArgumentSyntax attributeArgument) {
							symbol = semanticModel.GetSymbolInfo(attributeArgument.Expression, cancellationToken).Symbol;
							if (symbol?.Kind == SymbolKind.Field && (symbol as IFieldSymbol)?.IsConst == true) {
								tags.Add(__Classifications.ConstField);
								tags.Add(__Classifications.StaticMember);
							}
						}
						symbol = FindSymbolOrSymbolCandidateForNode(node, semanticModel, cancellationToken);
						if (symbol is null) {
							goto EXIT;
						}
					}
					else {
						switch (symbol.Kind) {
							case SymbolKind.NamedType:
								tags.Add(symbol.ContainingType != null
									? __Classifications.NestedDeclaration
									: __Classifications.Declaration);
								break;
							case SymbolKind.Event:
								tags.Add(__Classifications.NestedDeclaration);
								break;
							case SymbolKind.Method:
								if (HighlightOptions.LocalFunctionDeclaration
									|| ((IMethodSymbol)symbol).MethodKind != MethodKind.LocalFunction) {
									tags.Add(__Classifications.NestedDeclaration);
								}
								break;
							case SymbolKind.Property:
								if (symbol.ContainingType.IsAnonymousType == false) {
									tags.Add(__Classifications.NestedDeclaration);
								}
								break;
							case SymbolKind.Field:
								if (node.IsKind(SyntaxKind.TupleElement)) {
									if (((TupleElementSyntax)node).Identifier.IsKind(SyntaxKind.None)) {
										symbol = semanticModel.GetTypeInfo(((TupleElementSyntax)node).Type, cancellationToken).Type;
										if (symbol is null) {
											goto EXIT;
										}
									}
								}
								else if (HighlightOptions.NonPrivateField
									&& symbol.DeclaredAccessibility >= Accessibility.ProtectedAndInternal
									&& symbol.ContainingType.TypeKind != TypeKind.Enum) {
									tags.Add(__Classifications.NestedDeclaration);
								}
								break;
							case SymbolKind.Local:
								tags.Add(__Classifications.LocalDeclaration);
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
						goto EXIT;

					case SymbolKind.Label:
						tags.Add(__Classifications.Label);
						goto EXIT;

					case SymbolKind.TypeParameter:
						tags.Add(__Classifications.TypeParameter);
						goto EXIT;

					case SymbolKind.Field:
						var f = symbol as IFieldSymbol;
						if (f.IsConst) {
							tags.Add(f.ContainingType.TypeKind == TypeKind.Enum
								? __Classifications.EnumField
								: __Classifications.ConstField);
						}
						else {
							tags.Add(f.IsReadOnly ? __Classifications.ReadOnlyField
								: f.IsVolatile ? __Classifications.VolatileField
								: __Classifications.Field);
						}
						break;

					case SymbolKind.Property:
						tags.Add(__Classifications.Property);
						break;

					case SymbolKind.Event:
						tags.Add(__Classifications.Event);
						break;

					case SymbolKind.Local:
						tags.Add(((ILocalSymbol)symbol).IsConst
							? __Classifications.ConstField
							: __Classifications.LocalVariable);
						break;

					case SymbolKind.Namespace:
						tags.Add(__Classifications.Namespace);
						goto EXIT;

					case SymbolKind.Parameter:
						tags.Add(__Classifications.Parameter);
						break;

					case SymbolKind.Method:
						var methodSymbol = symbol as IMethodSymbol;
						switch (methodSymbol.MethodKind) {
							case MethodKind.Constructor:
								tags.Add(
									node is AttributeSyntax || node.Parent is AttributeSyntax || node.Parent?.Parent is AttributeSyntax
										? __Classifications.AttributeName
										: HighlightOptions.StyleConstructorAsType
										? (methodSymbol.ContainingType.TypeKind == TypeKind.Struct ? __Classifications.StructName : __Classifications.ClassName)
										: __Classifications.ConstructorMethod);
								break;
							case MethodKind.Destructor:
							case MethodKind.StaticConstructor:
								tags.Add(HighlightOptions.StyleConstructorAsType
										? (methodSymbol.ContainingType.TypeKind == TypeKind.Struct
											? __Classifications.StructName
											: __Classifications.ClassName)
										: __Classifications.ConstructorMethod);
								break;
							default:
								tags.Add(methodSymbol.IsExtensionMethod ? __Classifications.ExtensionMethod
									: methodSymbol.IsExtern ? __Classifications.ExternMethod
									: __Classifications.Method);
								break;
						}
						break;

					case SymbolKind.NamedType:
						break;

					default:
						goto EXIT;
				}

				if (SymbolMarkManager.HasBookmark) {
					var markerStyle = SymbolMarkManager.GetSymbolMarkerStyle(symbol);
					if (markerStyle != null) {
						tags.Add(markerStyle);
					}
				}

				if (FormatStore.IdentifySymbolSource && symbol.IsMemberOrType() && symbol.ContainingAssembly != null) {
					tags.Add(symbol.ContainingAssembly.GetSourceType() == AssemblySource.Metadata
						? __Classifications.MetadataSymbol
						: __Classifications.UserSymbol);
				}

				if (symbol.DeclaredAccessibility == Accessibility.Private) {
					switch (symbol.Kind) {
						case SymbolKind.Property:
						case SymbolKind.Method:
						case SymbolKind.Field:
						case SymbolKind.NamedType:
						case SymbolKind.Event:
							tags.Add(__Classifications.PrivateMember);
							break;
					}
				}
				if (symbol.IsStatic) {
					if (symbol.Kind != SymbolKind.Namespace) {
						tags.Add(__Classifications.StaticMember);
						if (symbol.IsAbstract && symbol.ContainingType?.TypeKind == TypeKind.Interface) {
							tags.Add(__Classifications.AbstractMember);
						}
					}
				}
				else if (symbol.IsSealed) {
					ITypeSymbol type;
					if (symbol.Kind == SymbolKind.NamedType
						&& (type = (ITypeSymbol)symbol).TypeKind == TypeKind.Struct) {
						if (type.IsReadOnly()) {
							tags.Add(__Classifications.ReadOnlyStruct);
						}
						if (type.IsRefLike()) {
							tags.Add(__Classifications.RefStruct);
						}
						goto EXIT;
					}
					tags.Add(__Classifications.SealedMember);
				}
				else if (symbol.IsOverride) {
					tags.Add(__Classifications.OverrideMember);
				}
				else if (symbol.IsVirtual) {
					tags.Add(__Classifications.VirtualMember);
				}
				else if (symbol.IsAbstract) {
					tags.Add(__Classifications.AbstractMember);
				}
				EXIT:
				return tags;
			}

			static ISymbol FindSymbolOrSymbolCandidateForNode(SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken) {
				return node.Parent.IsKind(SyntaxKind.SimpleMemberAccessExpression) ? semanticModel.GetSymbolInfo(node.Parent, cancellationToken).CandidateSymbols.FirstOrDefault()
					: node.Parent.IsKind(SyntaxKind.Argument) ? semanticModel.GetSymbolInfo(((ArgumentSyntax)node.Parent).Expression, cancellationToken).CandidateSymbols.FirstOrDefault()
					: node.IsKind(SyntaxKind.SimpleBaseType) ? semanticModel.GetTypeInfo(((SimpleBaseTypeSyntax)node).Type, cancellationToken).Type
					: node.IsKind(SyntaxKind.TypeConstraint) ? semanticModel.GetTypeInfo(((TypeConstraintSyntax)node).Type, cancellationToken).Type
					: node.IsKind(SyntaxKind.ExpressionStatement) ? semanticModel.GetSymbolInfo(((ExpressionStatementSyntax)node).Expression, cancellationToken).CandidateSymbols.FirstOrDefault()
					: semanticModel.GetSymbolInfo(node, cancellationToken).CandidateSymbols.FirstOrDefault();
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
					KeywordBraceTags = TransientKeywordTagHolder.Bold.Clone();
					MemberBraceTags = TransientMemberTagHolder.BoldBraces.Clone();
					MemberDeclarationBraceTags = TransientMemberTagHolder.BoldDeclarationBraces.Clone();
				}
				else {
					KeywordBraceTags = TransientKeywordTagHolder.Default.Clone();
					MemberBraceTags = TransientMemberTagHolder.Default.Clone();
					MemberDeclarationBraceTags = TransientMemberTagHolder.DeclarationBraces.Clone();
				}
				if (o.MatchFlags(SpecialHighlightOptions.BranchBrace) == false) {
					KeywordBraceTags.Branching = sp ? ClassificationTagHelper.BoldTag : null;
				}
				if (o.MatchFlags(SpecialHighlightOptions.CastBrace) == false) {
					KeywordBraceTags.TypeCast = sp ? ClassificationTagHelper.BoldTag : null;
				}
				if (o.MatchFlags(SpecialHighlightOptions.LoopBrace) == false) {
					KeywordBraceTags.Loop = sp ? ClassificationTagHelper.BoldTag : null;
				}
				if (o.MatchFlags(SpecialHighlightOptions.ResourceBrace) == false) {
					KeywordBraceTags.Resource = sp ? ClassificationTagHelper.BoldTag : null;
				}
				if (o.MatchFlags(SpecialHighlightOptions.ParameterBrace) == false) {
					MemberBraceTags.Constructor = sp ? ClassificationTagHelper.BoldTag : null;
					MemberBraceTags.Method = sp ? ClassificationTagHelper.BoldTag : null;
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