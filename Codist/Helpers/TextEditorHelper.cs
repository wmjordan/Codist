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
	static class TextEditorHelper
	{
		static /*readonly*/ Guid guidIWpfTextViewHost = new Guid("8C40265E-9FDB-4f54-A0FD-EBB72B7D0476");
		internal static readonly IClassificationFormatMap DefaultClassificationFormatMap = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap("text");
		static bool _IdentifySymbolSource;
		internal static readonly Dictionary<string, StyleBase> SyntaxStyleCache = InitSyntaxStyleCache();
		internal static readonly Dictionary<string, TextFormattingRunProperties> BackupFormattings = new Dictionary<string, TextFormattingRunProperties>(30);
		internal static TextFormattingRunProperties DefaultFormatting;

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

		public static TextFormattingRunProperties GetRunProperties(this IClassificationFormatMap formatMap, string classificationType) {
			return formatMap.GetTextProperties(ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(classificationType));
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
			var buffer = textView.TextViewLines;
			IWpfTextViewLine line = null, line2;
			foreach (var item in s.SelectedSpans) {
				line2 = buffer.GetTextViewLineContainingBufferPosition(item.Start);
				if (line == null) {
					line = line2;
					continue;
				}
				if (line2 != line) {
					return true;
				}
				line2 = buffer.GetTextViewLineContainingBufferPosition(item.End);
				if (line2 != line) {
					return true;
				}
			}
			return false;
		}

		public static void SelectNode(this IWpfTextView view, Microsoft.CodeAnalysis.SyntaxNode node, bool includeTrivia) {
			if (includeTrivia) {
				view.Selection.Select(new SnapshotSpan(view.TextSnapshot, node.FullSpan.Start, node.FullSpan.Length), false);
			}
			else {
				view.Selection.Select(new SnapshotSpan(view.TextSnapshot, node.Span.Start, node.Span.Length), false);
			}
		}

		public static void TryExecuteCommand(this EnvDTE.DTE dte, string command) {
			ThreadHelper.ThrowIfNotOnUIThread();
			try {
				if (dte.Commands.Item(command).IsAvailable) {
					dte.ExecuteCommand(command);
				}
			}
			catch (System.Runtime.InteropServices.COMException ex) {
				System.Windows.Forms.MessageBox.Show(ex.ToString());
				if (System.Diagnostics.Debugger.IsAttached) {
					System.Diagnostics.Debugger.Break();
				}
			}
		}

		public static void ExecuteEditorCommand(string command) {
			CodistPackage.DTE.TryExecuteCommand(command);
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
			var r = new Dictionary<string, StyleBase>(100);
			LoadSyntaxStyleCache(r);
			Config.Loaded += (s, args) => ResetStyleCache();
			DefaultClassificationFormatMap.ClassificationFormatMappingChanged += UpdateFormatCache;
			return r;
		}

		static void ResetStyleCache() {
			SyntaxStyleCache.Clear();
			LoadSyntaxStyleCache(SyntaxStyleCache);
		}

		static void UpdateFormatCache(object sender, EventArgs args) {
			var defaultFormat = DefaultClassificationFormatMap.DefaultTextProperties;
			if (DefaultFormatting == null) {
				DefaultFormatting = defaultFormat;
			}
			else if (DefaultFormatting.ForegroundBrushSame(defaultFormat.ForegroundBrush) == false) {
				System.Diagnostics.Debug.WriteLine("DefaultFormatting Changed");
				// theme changed
				BackupFormattings.Clear();
				DefaultFormatting = defaultFormat;
			}
			UpdateIdentifySymbolSource(SyntaxStyleCache);
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
