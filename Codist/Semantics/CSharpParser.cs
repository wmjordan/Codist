using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist
{
	/// <summary>A classifier for C# code syntax highlight.</summary>
	sealed class CSharpParser : IDisposable
	{
		// cache parsed results
		// note: don't iterate _Parsers to release TextBufferParser, since the Release method will change the collection
		readonly Dictionary<ITextBuffer, TextBufferParser> _Parsers = new Dictionary<ITextBuffer, TextBufferParser>();
		readonly object _SyncObj = new object();
		IWpfTextView _View;
		// cache the latest used parser to improve performance
		TextBufferParser _LastParser;
		// used in Interactive window to detect #reset
		Guid _LastSolutionId;
		bool _IsVisible;
		readonly bool _IsInteractiveWindow;

		CSharpParser(IWpfTextView view) {
			_View = view;
			_IsInteractiveWindow = IsInteractiveWindow(view.TextBuffer);
			view.VisualElement.IsVisibleChanged += VisualElement_IsVisibleChanged;
		}

		public static CSharpParser GetOrCreate(IWpfTextView view) {
			if (view.Properties.TryGetProperty(typeof(CSharpParser), out CSharpParser parser)) {
				return parser;
			}
			view.Properties.AddProperty(typeof(CSharpParser), parser = new CSharpParser(view));
			view.Closed += parser.View_Closed;
			return parser;
		}

		public ITextView View => _View;

		public bool IsVisible => _IsVisible;

		/// <summary>
		/// Gets the <see cref="TextBufferParser"/> from specific <see cref="ITextBuffer"/>.
		/// </summary>
		/// <remarks>A <see cref="IWpfTextView"/> may contain several <see cref="ITextBuffer"/>s, thus we can have multiple <see cref="TextBufferParser"/> for the same view at the same time.</remarks>
		public ITextBufferParser GetParser(ITextBuffer buffer) {
			TextBufferParser parser;
			if (buffer != _LastParser?.TextBuffer) {
				if (_Parsers.TryGetValue(buffer, out parser) == false) {
					_Parsers.Add(buffer, parser = new TextBufferParser(this, buffer));
				}
				if (_IsInteractiveWindow && _LastParser != null) {
					_LastParser.ReleaseAsyncTimer();
				}
				_LastParser = parser;
			}
			else {
				parser = _LastParser;
			}
			parser.Ref();
			return parser;
		}

		public void Dispose() {
			if (_View != null) {
				_View.VisualElement.IsVisibleChanged -= VisualElement_IsVisibleChanged;
				if (_Parsers.Count > 0) {
					lock (_SyncObj) {
						foreach (var parser in GetTextBufferParsers()) {
							parser.Release();
						}
						_Parsers.Clear();
					}
				}
				_LastParser = null;
				_View = null;
			}
		}

		// Provide a cached list for TextBufferParser instances for subsequent call for TextBufferParser.Release
		List<TextBufferParser> GetTextBufferParsers() {
			return _Parsers.Values.ToList() /* hold parsers to be disposed */;
		}

		void View_Closed(object sender, EventArgs e) {
			_View.Closed -= View_Closed;
			Dispose();
		}

		static bool IsInteractiveWindow(ITextBuffer buffer) {
			return buffer.Properties.PropertyList.Any(o => (o.Key as Type)?.Name == "InteractiveWindow");
		}

		void VisualElement_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e) {
			var v = _IsVisible = (bool)e.NewValue;
			lock (_SyncObj) {
				foreach (var tagger in _Parsers.Values) {
					tagger.OnViewVisibilityChanged(v);
				}
			}
		}

		[DebuggerDisplay("{GetDebuggerString()}")]
		sealed class TextBufferParser : ITextBufferParser
		{
			CSharpParser _Container;
			ITextBuffer _Buffer;
			CancellationTokenSource _ParserBreaker;
			AsyncParser _Parser;
			// we use a timer to ensure the parsing always takes place in a thread other than the UI thread
			Timer _Timer;
			SemanticState _State;
			bool _HasBackgroundChange;
			ITextSnapshot _WorkingSnapshot;
			int _Ref;
			readonly bool _IsInteractiveWindow;
			// debug info
			readonly string _Name;

			public TextBufferParser(CSharpParser container, ITextBuffer buffer) {
				_Name = buffer.GetTextDocument()?.FilePath ?? buffer.CurrentSnapshot?.GetText(0, Math.Min(buffer.CurrentSnapshot.Length, 500));
				_Container = container;
				_Buffer = buffer;
				_Timer = new Timer(StartAsyncParser);
				_Parser = new AsyncParser(OnStateUpdated);
				SubscribeBufferEvents(buffer);
			}

			public event EventHandler<EventArgs<SemanticState>> StateUpdated;

			public bool IsDisposed => _Parser == null;
			public ITextBuffer TextBuffer => _Buffer;

			public void Ref() {
				_Ref++;
				$"Ref+ {_Ref} {_Name}".Log(LogCategory.SyntaxHighlight);
			}

			internal void Release() {
				if (Interlocked.Exchange(ref _Parser, null) != null) {
					$"{_Name} tagger released".Log(LogCategory.SyntaxHighlight);
					ReleaseResources();
					_Ref = 0;
				}
			}

			public bool TryGetSemanticState(ITextSnapshot snapshot, out SemanticState state) {
				if (_Parser == null) {
					goto NA;
				}
				state = _State;
				var isNewSnapshot = state?.Snapshot != snapshot;
				if (isNewSnapshot == false) {
					return true;
				}
				if (snapshot.TextBuffer != _Buffer) {
					goto NA;
				}

				// don't schedule parsing unless snapshot is changed
				if (snapshot != _WorkingSnapshot) {
					_WorkingSnapshot = snapshot;
					if (_Parser.State == ParserState.Working) {
						// cancel existing parsing
						_ParserBreaker?.Cancel();
					}
					_Timer.Change(state != null ? 300 : 100, Timeout.Infinite);
				}

				if (state != null && state.Snapshot.TextBuffer == snapshot.TextBuffer) {
					return false;
				}
			NA:
				state = null;
				return false;
			}

			public Task<SemanticState> GetSemanticStateAsync(ITextSnapshot snapshot, CancellationToken cancellationToken = default) {
				if (_Parser == null) {
					return Task.FromResult<SemanticState>(null);
				}
				var result = _State;
				var isNewSnapshot = result?.Snapshot != snapshot;
				if (isNewSnapshot == false) {
					return Task.FromResult(result);
				}
				if (snapshot.TextBuffer != _Buffer) {
					return Task.FromResult<SemanticState>(null);
				}
				if (snapshot != _WorkingSnapshot) {
					_WorkingSnapshot = snapshot;
					if (_Parser.State == ParserState.Working) {
						// cancel existing parsing
						_ParserBreaker?.Cancel();
					}
					_Timer.Change(result != null ? 300 : 100, Timeout.Infinite);
				}
				return GetStateAsync(result, cancellationToken);

				async Task<SemanticState> GetStateAsync(SemanticState oldResult, CancellationToken ct) {
					return await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.RunAsync(() => {
						SpinWait.SpinUntil(() => Volatile.Read(ref _State) != oldResult || ct.IsCancellationRequested);
						return Task.FromResult(ct.IsCancellationRequested
							? null
							: _State);
					});
				}
			}

			void OnStateUpdated(SemanticState result) {
				var oldResult = Interlocked.Exchange(ref _State, result);
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
				StateUpdated?.Invoke(this, new EventArgs<SemanticState>(result));

				if (IsDisposed) {
					// prevent leak after disposal
					ReleaseResources();
				}
			}

			void RemoveTaggersOnSolutionChange(SemanticState result) {
				Guid id;
				var c = _Container;
				if (c == null || result.Workspace.CurrentSolution.Id.Id == (id = c._LastSolutionId)) {
					return;
				}

				lock (c._SyncObj) {
					foreach (var tagger in c.GetTextBufferParsers()) {
						if (tagger._State?.Workspace.CurrentSolution.Id.Id != id) {
							tagger.Release();
						}
					}
				}
			}

			internal void OnViewVisibilityChanged(bool isVisible) {
				if (isVisible && _HasBackgroundChange) {
					_HasBackgroundChange = false;
					if (_State != null) {
						// schedule a refresh
						_Timer?.Change(300, Timeout.Infinite);
					}
				}
			}

			void StartAsyncParser(object state) {
				_Parser?.Start(_Buffer, SyncHelper.CancelAndRetainToken(ref _ParserBreaker));
			}

			void SubscribeBufferEvents(ITextBuffer buffer) {
				buffer.ContentTypeChanged += TextBuffer_ContentTypeChanged;
			}

			void UnsubscribeBufferEvents(ITextBuffer buffer) {
				if (buffer != null) {
					buffer.ContentTypeChanged -= TextBuffer_ContentTypeChanged;
				}
			}

			void WorkspaceChanged(object sender, WorkspaceChangeEventArgs args) {
				$"Workspace {args.Kind}: {args.DocumentId}".Log(LogCategory.SyntaxHighlight);
				var parser = _Parser;
				if (parser == null) {
					return;
				}
				switch (args.Kind) {
					case WorkspaceChangeKind.DocumentChanged:
					case WorkspaceChangeKind.DocumentRemoved:
						if (args.DocumentId == _State?.Document.Id) {
							// skip notifications from current document
							return;
						}
						break;
				}

				// at this place, we will receive the following change notifications:
				// 1. from solution or project change
				// 2. from the same document, but various configurations (should be omitted)
				//   -- for instance, while editing a.cs (net45), we will get notified from a.cs (net50), a.cs (net60) etc. as well
				if (_Container._IsVisible) {
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

			void TextBuffer_ContentTypeChanged(object sender, ContentTypeChangedEventArgs e) {
				if (_IsInteractiveWindow == false
					&& e.AfterContentType.IsOfType(Constants.CodeTypes.CSharp) == false) {
					$"ContentType changed to {e.AfterContentType.DisplayName}".Log(LogCategory.SyntaxHighlight);
					Dispose();
				}
			}

			public void Dispose() {
				if (--_Ref > 0) {
					$"Ref- {_Ref} {_Name}".Log(LogCategory.SyntaxHighlight);
					return;
				}
				if (Interlocked.Exchange(ref _Parser, null) != null) {
					$"{_Name} tagger disposed".Log(LogCategory.SyntaxHighlight);
					ReleaseResources();
				}
			}

			internal void ReleaseAsyncTimer() {
				if (_ParserBreaker != null) {
					_ParserBreaker.Cancel();
					_ParserBreaker.Dispose();
					_ParserBreaker = null;
				}
				_Timer?.Dispose();
				_Timer = null;
			}

			void ReleaseResources() {
				_ParserBreaker.CancelAndDispose();
				if (_Buffer != null) {
					UnsubscribeBufferEvents(_Buffer);
					lock (_Container._SyncObj) {
						_Container._Parsers.Remove(_Buffer);
					}
					_Container = null;
					_Buffer = null;
				}

				if (_IsInteractiveWindow == false && _State?.Workspace != null) {
					_State.Workspace.WorkspaceChanged -= WorkspaceChanged;
				}
				_State = default;
				_Timer?.Dispose();
				_Timer = null;
				_WorkingSnapshot = null;
				StateUpdated = null;
			}

			string GetDebuggerString() {
				return $"{_Parser?.State} {_Ref} ({_Name})";
			}
		}

		sealed class AsyncParser
		{
			readonly Action<SemanticState> _Callback;
			int _State;

			public AsyncParser(Action<SemanticState> callback) {
				_Callback = callback;
			}

			public ParserState State => (ParserState)_State;

			public bool Start(ITextBuffer buffer, CancellationToken cancellationToken) {
				return Interlocked.CompareExchange(ref _State, (int)ParserState.Working, (int)ParserState.Idle) == (int)ParserState.Idle
					&& !Task.Run(() => {
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
					}).IsCanceled;
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
					$"{snapshot.TextBuffer.GetDocument().GetDocId()} end parsing {snapshot.Version} on thread {Thread.CurrentThread.ManagedThreadId}".Log(LogCategory.SyntaxHighlight);
					_Callback(new SemanticState(workspace, model, snapshot, document));
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
	}
}