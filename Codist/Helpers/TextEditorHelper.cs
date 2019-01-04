using System;
using System.Collections.Generic;
using System.Reflection;
using Codist.SyntaxHighlight;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using VsTextView = Microsoft.VisualStudio.TextManager.Interop.IVsTextView;
using VsUserData = Microsoft.VisualStudio.TextManager.Interop.IVsUserData;

namespace Codist
{
	/// <summary>
	/// This class assumes that the <see cref="IClassificationFormatMap"/> is shared among document editor instances and the "default" classification format map contains all needed formatting.
	/// </summary>
	static class TextEditorHelper
	{
		static /*readonly*/ Guid guidIWpfTextViewHost = new Guid("8C40265E-9FDB-4f54-A0FD-EBB72B7D0476");
		static readonly object _syncRoot = new object();
		internal static readonly IClassificationFormatMap DefaultClassificationFormatMap = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap("text");
		internal static readonly IEditorFormatMap DefaultEditorFormatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap("text");
		static bool _IdentifySymbolSource;
		static Dictionary<string, StyleBase> _SyntaxStyleCache = InitSyntaxStyleCache();
		static Dictionary<string, TextFormattingRunProperties> _BackupFormattings = LoadFormattings(new Dictionary<string, TextFormattingRunProperties>(80));
		static TextFormattingRunProperties _DefaultFormatting;

		internal static bool IdentifySymbolSource => _IdentifySymbolSource;

		public static bool AnyTextChanges(ITextVersion oldVersion, ITextVersion currentVersion) {
			while (oldVersion != currentVersion) {
				if (oldVersion.Changes.Count > 0) {
					return true;
				}
				oldVersion = oldVersion.Next;
			}
			return false;
		}
		public static bool Contains(this TextSpan span, int position, bool inclusive) {
			return span.Contains(position) || (inclusive && span.End == position);
		}
		public static bool Contains(this TextSpan span, ITextSelection selection, bool inclusive) {
			var start = selection.Start.Position.Position;
			var end = selection.End.Position.Position;
			return span.Contains(start) && (span.Contains(end) || inclusive && span.End == end);
		}

		public static SnapshotSpan CreateSnapshotSpan(this TextSpan span, ITextSnapshot snapshot) {
			if (span.End < snapshot.Length) {
				return new SnapshotSpan(snapshot, span.Start, span.Length);
			}
			return default;
		}

		public static TextFormattingRunProperties GetBackupFormatting(string classificationType) {
			lock (_syncRoot) {
				return _BackupFormattings.TryGetValue(classificationType, out var r) ? r : null;
			}
		}

		public static Span GetLineSpan(this SnapshotSpan span) {
			return Span.FromBounds(span.Snapshot.GetLineNumberFromPosition(span.Start),
				span.Snapshot.GetLineNumberFromPosition(span.End));
		}

		public static Span GetLineSpan(this ITextSnapshot snapshot, TextSpan span) {
			return Span.FromBounds(snapshot.GetLineNumberFromPosition(span.Start),
				snapshot.GetLineNumberFromPosition(span.End));
		}

		public static TextFormattingRunProperties GetRunProperties(this IClassificationFormatMap formatMap, string classificationType) {
			return formatMap.GetTextProperties(ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(classificationType));
		}

		public static StyleBase GetStyle(string classificationType) {
			lock (_syncRoot) {
				return _SyntaxStyleCache.TryGetValue(classificationType, out var r) ? r : null;
			}
		}

		/// <summary>
		/// Begins an edit operation to the <paramref name="view"/>.
		/// </summary>
		/// <typeparam name="TView">The type of the view.</typeparam>
		/// <param name="view">The <see cref="ITextView"/> to be edited.</param>
		/// <param name="action">The edit operation.</param>
		/// <returns>Returns a new <see cref="ITextSnapshot"/> if <see cref="ITextEdit.HasEffectiveChanges"/>  returns <see langword="true"/>, otherwise, returns <see langword="null"/>.</returns>
		public static ITextSnapshot Edit<TView>(this TView view, Action<TView, ITextEdit> action)
			where TView : ITextView {
			using (var edit = view.TextSnapshot.TextBuffer.CreateEdit()) {
				action(view, edit);
				if (edit.HasEffectiveChanges) {
					return edit.Apply();
				}
				return null;
			}
		}
		/// <summary>
		/// Performs edit operation to each selected spans in the <paramref name="view"/>.
		/// </summary>
		/// <typeparam name="TView">The type of the view.</typeparam>
		/// <param name="view">The <see cref="ITextView"/> to be edited.</param>
		/// <param name="action">The edit operation against each selected span.</param>
		/// <returns>Returns a new <see cref="SnapshotSpan"/> if <see cref="ITextEdit.HasEffectiveChanges"/> returns <see langword="true"/> and <paramref name="action"/> returns a <see cref="Span"/>, otherwise, returns <see langword="null"/>.</returns>
		public static SnapshotSpan? EditSelection<TView>(this TView view, Func<TView, ITextEdit, SnapshotSpan, Span?> action)
			where TView : ITextView {
			using (var edit = view.TextSnapshot.TextBuffer.CreateEdit()) {
				Span? s = null;
				foreach (var item in view.Selection.SelectedSpans) {
					if (s == null) {
						s = action(view, edit, item);
					}
					else {
						action(view, edit, item);
					}
				}
				if (edit.HasEffectiveChanges) {
					var shot = edit.Apply();
					return s.HasValue ? view.TextSnapshot.CreateTrackingSpan(s.Value, SpanTrackingMode.EdgeInclusive)
						.GetSpan(shot)
						: (SnapshotSpan?)null;
				}
				return null;
			}
		}

		public static void ExpandSelectionToLine(this ITextView view) {
			view.ExpandSelectionToLine(true);
		}
		public static void ExpandSelectionToLine(this ITextView view, bool includeLineBreak) {
			var start = view.TextSnapshot.GetLineFromPosition(view.Selection.Start.Position).Start;
			var end = view.Selection.End.Position;
			var endLine = view.TextSnapshot.GetLineFromPosition(end);
			if (endLine.Start != end) {
				// if selection not ended in line break, expand to line break
				end = includeLineBreak ? endLine.EndIncludingLineBreak : endLine.End;
			}
			view.Selection.Select(new SnapshotSpan(start, end), false);
		}
		public static string GetFirstSelectionText(this ITextSelection selection) {
			return selection.IsEmpty ? String.Empty : selection.SelectedSpans[0].GetText();
		}
		public static string GetFirstSelectionText(this ITextView view) {
			return view.TryGetFirstSelectionSpan(out var span) ? span.GetText() : String.Empty;
		}
		public static bool TryGetFirstSelectionSpan(this ITextView view, out SnapshotSpan span) {
			if (view.Selection.IsEmpty || view.Selection.SelectedSpans.Count < 1) {
				span = new SnapshotSpan();
				return false;
			}
			else {
				span = view.Selection.SelectedSpans[0];
				return true;
			}
		}

		public static TokenType GetSelectedTokenType(this ITextView view) {
			if (view.Selection.IsEmpty || view.Selection.SelectedSpans.Count > 1) {
				return TokenType.None;
			}
			var selection = view.Selection.SelectedSpans[0];
			if (selection.Length >= 128) {
				return TokenType.None;
			}
			string s = null;
			if ((selection.Length == 36 || selection.Length == 38) && Guid.TryParse(s = selection.GetText(), out var result)) {
				return TokenType.Guid;
			}
			if (selection.Length == 4 && (s = selection.GetText()).Equals("Guid", StringComparison.OrdinalIgnoreCase)) {
				return TokenType.GuidPlaceHolder;
			}
			var t = TokenType.None;
			foreach (var c in s ?? (s = selection.GetText())) {
				if (c >= '0' && c <= '9') {
					t |= TokenType.Digit;
				}
				else if (c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z') {
					t |= TokenType.Letter;
				}
				else if (c == '_') {
					t |= TokenType.Underscore;
				}
				else if (c == '.') {
					t |= TokenType.Dot;
				}
				else {
					return TokenType.None;
				}
			}
			return t;
		}

		public static SnapshotPoint GetCaretPosition(this ITextView textView) {
			return textView.Caret.Position.BufferPosition;
		}

		public static bool IsCaretInReadOnlyRegion(this ITextView textView) {
			return textView.TextBuffer.IsReadOnly(textView.Caret.Position.BufferPosition);
		}

		public static bool IsMultilineSelected(this ITextView textView) {
			var s = textView.Selection;
			if (s.IsEmpty || s.SelectedSpans.Count < 1) {
				return false;
			}
			return textView.GetTextViewLineContainingBufferPosition(s.Start.Position) != textView.GetTextViewLineContainingBufferPosition(s.End.Position);
		}

		public static void SelectNode(this ITextView view, Microsoft.CodeAnalysis.SyntaxNode node, bool includeTrivia) {
			var span = includeTrivia ? node.FullSpan : node.Span;
			if (view.TextSnapshot.Length > span.End) {
				SnapshotSpan ss;
				if (includeTrivia && node.ContainsDirectives) {
					var start = span.Start;
					var end = span.End;
					var trivias = node.GetLeadingTrivia();
					for (int i = trivias.Count - 1; i >= 0; i--) {
						if (trivias[i].IsDirective) {
							start = trivias[i].FullSpan.End;
							break;
						}
					}
					ss = new SnapshotSpan(view.TextSnapshot, start, end - start);
				}
				else {
					ss = new SnapshotSpan(view.TextSnapshot, span.Start, span.Length);
				}
				view.SelectSpan(ss);
			}
		}

		public static void SelectSpan(this ITextView view, SnapshotSpan span) {
			if (view.TextSnapshot != span.Snapshot) {
				// should not be here
				span = new SnapshotSpan(view.TextSnapshot, span.Span);
			}
			view.ViewScroller.EnsureSpanVisible(span, EnsureSpanVisibleOptions.ShowStart);
			view.Selection.Select(span, false);
			view.Caret.MoveTo(span.End);
		}

		public static void TryExecuteCommand(this EnvDTE.DTE dte, string command, string args = "") {
			ThreadHelper.ThrowIfNotOnUIThread();
			try {
				if (dte.Commands.Item(command).IsAvailable) {
					dte.ExecuteCommand(command, args);
				}
			}
			catch (System.Runtime.InteropServices.COMException ex) {
				System.Windows.Forms.MessageBox.Show(ex.ToString());
				if (System.Diagnostics.Debugger.IsAttached) {
					System.Diagnostics.Debugger.Break();
				}
			}
		}

		public static void ExecuteEditorCommand(string command, string args = "") {
			CodistPackage.DTE.TryExecuteCommand(command, args);
		}

		public static IWpfTextView GetActiveWpfDocumentView(this IServiceProvider service) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var doc = CodistPackage.DTE.ActiveDocument;
			if (doc == null) {
				return null;
			}
			var textView = GetIVsTextView(service, doc.FullName);
			return textView == null ? null : GetWpfTextView(textView);
		}

		static VsTextView GetIVsTextView(IServiceProvider service, string filePath) {
			IVsWindowFrame windowFrame;
			return VsShellUtilities.IsDocumentOpen(service, filePath, Guid.Empty, out var uiHierarchy, out uint itemID, out windowFrame)
				? VsShellUtilities.GetTextView(windowFrame)
				: null;
		}
		static IWpfTextView GetWpfTextView(VsTextView vTextView) {
			var userData = vTextView as VsUserData;
			if (userData == null) {
				return null;
			}
			var guidViewHost = guidIWpfTextViewHost;
			userData.GetData(ref guidViewHost, out object holder);
			return ((IWpfTextViewHost)holder).TextView;
		}
		static Dictionary<string, StyleBase> InitSyntaxStyleCache() {
			var cache = new Dictionary<string, StyleBase>(100);
			LoadSyntaxStyleCache(cache);
			Config.Loaded += (s, args) => ResetStyleCache();
			DefaultClassificationFormatMap.ClassificationFormatMappingChanged += UpdateFormatCache;
			return cache;
		}

		static void ResetStyleCache() {
			lock (_syncRoot) {
				var cache = new Dictionary<string, StyleBase>(_SyntaxStyleCache.Count);
				LoadSyntaxStyleCache(cache);
				_SyntaxStyleCache = cache;
			}
		}

		static void UpdateFormatCache(object sender, EventArgs args) {
			var defaultFormat = DefaultClassificationFormatMap.DefaultTextProperties;
			if (_DefaultFormatting == null) {
				_DefaultFormatting = defaultFormat;
			}
			else if (_DefaultFormatting.ForegroundBrushSame(defaultFormat.ForegroundBrush) == false) {
				System.Diagnostics.Debug.WriteLine("DefaultFormatting Changed");
				// theme changed
				lock (_syncRoot) {
					var formattings = new Dictionary<string, TextFormattingRunProperties>(_BackupFormattings.Count);
					LoadFormattings(formattings);
					_BackupFormattings = formattings;
					_DefaultFormatting = defaultFormat;
				}
			}
			lock (_syncRoot) {
				UpdateIdentifySymbolSource(_SyntaxStyleCache);
			}
		}

		static Dictionary<string, TextFormattingRunProperties> LoadFormattings(Dictionary<string, TextFormattingRunProperties> formattings) {
			var m = DefaultClassificationFormatMap;
			foreach (var item in m.CurrentPriorityOrder) {
				if (item != null && _SyntaxStyleCache.ContainsKey(item.Classification)) {
					formattings[item.Classification] = m.GetExplicitTextProperties(item);
				}
			}
			return formattings;
		}

		static void LoadSyntaxStyleCache(Dictionary<string, StyleBase> cache) {
			var service = ServicesHelper.Instance.ClassificationTypeRegistry;
			InitStyleClassificationCache<CodeStyleTypes, CodeStyle>(cache, service, Config.Instance.GeneralStyles);
			InitStyleClassificationCache<CommentStyleTypes, CommentStyle>(cache, service, Config.Instance.CommentStyles);
			InitStyleClassificationCache<CppStyleTypes, CppStyle>(cache, service, Config.Instance.CppStyles);
			InitStyleClassificationCache<CSharpStyleTypes, CSharpStyle>(cache, service, Config.Instance.CodeStyles);
			InitStyleClassificationCache<XmlStyleTypes, XmlCodeStyle>(cache, service, Config.Instance.XmlCodeStyles);
			InitStyleClassificationCache<SymbolMarkerStyleTypes, SymbolMarkerStyle>(cache, service, Config.Instance.SymbolMarkerStyles);
			UpdateIdentifySymbolSource(cache);
		}

		static void UpdateIdentifySymbolSource(Dictionary<string, StyleBase> cache) {
			StyleBase style;
			_IdentifySymbolSource = cache.TryGetValue(Constants.CSharpMetadataSymbol, out style) && style.IsSet
					|| cache.TryGetValue(Constants.CSharpUserSymbol, out style) && style.IsSet;
		}

		static void InitStyleClassificationCache<TStyleEnum, TCodeStyle>(Dictionary<string, StyleBase> styleCache, IClassificationTypeRegistryService service, List<TCodeStyle> styles)
			where TCodeStyle : StyleBase {
			var cs = typeof(TStyleEnum);
			var codeStyles = Enum.GetNames(cs);
			foreach (var styleName in codeStyles) {
				var f = cs.GetField(styleName);
				var cso = styles.Find(i => i.Id == (int)f.GetValue(null));
				if (cso == null) {
					continue;
				}
				var cts = f.GetCustomAttributes<ClassificationTypeAttribute>(false);
				foreach (var item in cts) {
					var n = item.ClassificationTypeNames;
					if (String.IsNullOrWhiteSpace(n)) {
						continue;
					}
					var ct = service.GetClassificationType(n);
					if (ct != null) {
						styleCache[ct.Classification] = cso;
					}
				}
			}
		}
	}

	[Flags]
	public enum TokenType
	{
		None,
		Letter = 1,
		Digit = 2,
		Dot = 4,
		Underscore = 8,
		Guid = 16,
		GuidPlaceHolder = 32
	}
}
