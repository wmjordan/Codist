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

		public static bool Contains(this TextSpan token, ITextSelection selection, bool inclusive) {
			var start = selection.Start.Position.Position;
			var end = selection.End.Position.Position;
			return token.Contains(start) && (token.Contains(end) || inclusive && token.End == end);
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

		public static TextFormattingRunProperties GetRunProperties(this IClassificationFormatMap formatMap, string classificationType) {
			return formatMap.GetTextProperties(ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(classificationType));
		}

		public static StyleBase GetStyle(string classificationType) {
			lock (_syncRoot) {
				return _SyntaxStyleCache.TryGetValue(classificationType, out var r) ? r : null;
			}
		}

		public static void ExpandSelectionToLine(this IWpfTextView view) {
			view.ExpandSelectionToLine(true);
		}
		public static void ExpandSelectionToLine(this IWpfTextView view, bool includeLineBreak) {
			var start = view.TextSnapshot.GetLineFromPosition(view.Selection.Start.Position).Start;
			var end = view.Selection.End.Position;
			var endLine = view.TextSnapshot.GetLineFromPosition(end);
			if (endLine.Start != end) {
				// if selection not ended in line break, expand to line break
				end = includeLineBreak ? endLine.EndIncludingLineBreak : endLine.End;
			}
			view.Selection.Select(new SnapshotSpan(start, end), false);
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

		public static bool IsCaretInReadOnlyRegion(this IWpfTextView textView) {
			return textView.TextBuffer.IsReadOnly(textView.Caret.Position.BufferPosition);
		}

		public static bool IsMultilineSelected(this IWpfTextView textView) {
			var s = textView.Selection;
			if (s.IsEmpty || s.SelectedSpans.Count < 1) {
				return false;
			}
			var lines = textView.TextViewLines;
			return lines.GetTextViewLineContainingBufferPosition(s.Start.Position) != lines.GetTextViewLineContainingBufferPosition(s.End.Position);
		}

		public static void SelectNode(this IWpfTextView view, Microsoft.CodeAnalysis.SyntaxNode node, bool includeTrivia) {
			var span = includeTrivia ? node.FullSpan : node.Span;
			if (view.TextSnapshot.Length > span.End) {
				var ss = new SnapshotSpan(view.TextSnapshot, span.Start, span.Length);
				view.Selection.Select(ss, false);
				view.ViewScroller.EnsureSpanVisible(ss, EnsureSpanVisibleOptions.ShowStart);
			}
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
