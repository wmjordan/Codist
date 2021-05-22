using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Input;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using VsTextView = Microsoft.VisualStudio.TextManager.Interop.IVsTextView;
using VsUserData = Microsoft.VisualStudio.TextManager.Interop.IVsUserData;
using AppHelpers;

namespace Codist
{
	/// <summary>
	/// This class assumes that the <see cref="IClassificationFormatMap"/> is shared among document editor instances and the "default" classification format map contains all needed formatting.
	/// </summary>
	static class TextEditorHelper
	{
		static /*readonly*/ Guid guidIWpfTextViewHost = new Guid("8C40265E-9FDB-4f54-A0FD-EBB72B7D0476");
		static readonly HashSet<IWpfTextView> _WpfTextViews = new HashSet<IWpfTextView>();
		static IWpfTextView _MouseOverTextView, _ActiveTextView;

		#region Position
		public static SnapshotPoint GetCaretPosition(this ITextView textView) {
			return textView.Caret.Position.BufferPosition;
		}

		public static bool IsCaretInReadOnlyRegion(this ITextView textView) {
			return textView.TextBuffer.IsReadOnly(textView.Caret.Position.BufferPosition);
		}

		public static bool Contains(this TextSpan span, int position, bool inclusive) {
			return span.Contains(position) || (inclusive && span.End == position);
		}
		public static bool Contains(this TextSpan span, ITextSelection selection, bool inclusive) {
			var start = selection.Start.Position.Position;
			var end = selection.End.Position.Position;
			return span.Contains(start) && (span.Contains(end) || inclusive && span.End == end);
		}

		#endregion

		#region Spans
		public static SnapshotSpan CreateSnapshotSpan(this TextSpan span, ITextSnapshot snapshot) {
			return span.End < snapshot.Length ? new SnapshotSpan(snapshot, span.Start, span.Length) : (default);
		}
		public static Span ToSpan(this TextSpan span) {
			return new Span(span.Start, span.Length);
		}
		public static TextSpan ToTextSpan(this SnapshotSpan span) {
			return new TextSpan(span.Start, span.Length);
		}
		public static Span ToSpan(this SnapshotSpan span) {
			return new Span(span.Start, span.Length);
		}
		public static TextSpan ToTextSpan(this Span span) {
			return new TextSpan(span.Start, span.Length);
		}
		public static ITrackingSpan ToTrackingSpan(this SnapshotSpan span) {
			return span.Snapshot.CreateTrackingSpan(span.ToSpan(), SpanTrackingMode.EdgeInclusive);
		}

		public static Span GetLineSpan(this SnapshotSpan span) {
			return Span.FromBounds(span.Snapshot.GetLineNumberFromPosition(span.Start),
				span.Snapshot.GetLineNumberFromPosition(span.End));
		}
		/// <summary>
		/// <para>Gets the start and end line number from a given <see cref="TextSpan"/>.</para>
		/// <para>Note: Make sure that the <paramref name="span"/> is within the <paramref name="snapshot"/>.</para>
		/// </summary>
		public static Span GetLineSpan(this ITextSnapshot snapshot, TextSpan span) {
			return Span.FromBounds(snapshot.GetLineNumberFromPosition(span.Start),
				snapshot.GetLineNumberFromPosition(span.End));
		}
		#endregion

		public static ClassificationTag GetClassificationTag(this IClassificationTypeRegistryService registry, string clasificationType) {
			return new ClassificationTag(registry.GetClassificationType(clasificationType));
		}
		public static IClassificationType CreateClassificationType(string classificationType) {
			return new ClassificationCategory(classificationType);
		}

		public static TextFormattingRunProperties GetRunProperties(this IClassificationFormatMap formatMap, string classificationType) {
			var t = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(classificationType);
			return t == null ? null : formatMap.GetTextProperties(t);
		}

		public static bool Mark<TKey>(this IPropertyOwner owner, TKey mark) {
			if (owner.Properties.ContainsProperty(mark) == false) {
				owner.Properties.AddProperty(mark, mark);
				return true;
			}
			return false;
		}

		#region Selection
		public static void ExpandSelectionToLine(this ITextView view) {
			view.ExpandSelectionToLine(false);
		}
		public static void ExpandSelectionToLine(this ITextView view, bool excludeLineBreak) {
			var start = view.TextSnapshot.GetLineFromPosition(view.Selection.Start.Position).Start;
			var end = view.Selection.End.Position;
			var endLine = view.TextSnapshot.GetLineFromPosition(end);
			if (endLine.Start != end) {
				// if selection not ended in line break, expand to line break
				end = excludeLineBreak ? endLine.End : endLine.EndIncludingLineBreak;
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
			if (s == null) {
				s = selection.GetText();
			}
			if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
				if (s.Length > 2) {
					s = s.Substring(2);
					t = TokenType.Hex | TokenType.ZeroXHex;
				}
				else {
					return TokenType.None;
				}
			}
			foreach (var c in s) {
				if (c >= '0' && c <= '9') {
					t |= TokenType.Digit;
				}
				else if (c >= 'a' && c <= 'z') {
					if (t.MatchFlags(TokenType.Letter) == false) {
						t = t.SetFlags(TokenType.Hex, c <= 'f') | TokenType.Letter;
					}
				}
				else if (c >= 'A' && c <= 'Z') {
					if (t.MatchFlags(TokenType.Letter) == false) {
						t = t.SetFlags(TokenType.Hex, c <= 'F') | TokenType.Letter;
					}
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
			if (t.MatchFlags(TokenType.Hex) && t.HasAnyFlag(TokenType.Dot | TokenType.Underscore)
				|| t.MatchFlags(TokenType.ZeroXHex) && t.HasAnyFlag(TokenType.Hex | TokenType.Digit) == false) {
				return TokenType.None;
			}
			return t;
		}

		public static bool IsMultilineSelected(this ITextView textView) {
			var s = textView.Selection;
			if (s.IsEmpty || s.SelectedSpans.Count < 1) {
				return false;
			}
			return textView.GetTextViewLineContainingBufferPosition(s.Start.Position) != textView.GetTextViewLineContainingBufferPosition(s.End.Position);
		}

		public static void SelectNode(this SyntaxNode node, bool includeTrivia) {
			if (node == null) {
				return;
			}
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
			CodistPackage.DTE.OpenFile(node.GetLocation().SourceTree.FilePath, doc => {
				var v = GetIVsTextView(CodistPackage.Instance, doc.FullName);
				if (v != null) {
					GetWpfTextView(v)?.SelectSpan(includeTrivia ? node.GetSematicSpan(true) : node.Span.ToSpan());
				}
			});
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
		}

		public static void SelectNode(this ITextView view, SyntaxNode node, bool includeTrivia) {
			var span = includeTrivia ? node.FullSpan : node.Span;
			if (view.TextSnapshot.Length >= span.End) {
				var ss = includeTrivia
					? new SnapshotSpan(view.TextSnapshot, node.GetSematicSpan(true))
					: new SnapshotSpan(view.TextSnapshot, span.Start, span.Length);
				view.SelectSpan(ss);
			}
		}

		public static void SelectSpan(this ITextView view, SnapshotSpan span) {
			if (view.TextSnapshot != span.Snapshot) {
				// should not be here
				span = new SnapshotSpan(view.TextSnapshot, span.Span);
			}
			var m = ServicesHelper.Instance.OutliningManager.GetOutliningManager(view);
			if (m != null) {
				foreach (var c in m.GetCollapsedRegions(span)) {
					m.Expand(c);
				}
			}
			view.ViewScroller.EnsureSpanVisible(span, EnsureSpanVisibleOptions.ShowStart);
			view.Selection.Select(span, false);
			view.Caret.MoveTo(span.End);
		}

		public static void SelectSpan(this ITextView view, Span span) {
			view.SelectSpan(span.Start, span.Length);
		}

		public static void SelectSpan(this ITextView view, int start, int length) {
			if (length < 0 || start < 0 || start + length > view.TextSnapshot.Length) {
				return;
			}
			var span = new SnapshotSpan(view.TextSnapshot, start, length);
			view.ViewScroller.EnsureSpanVisible(span, EnsureSpanVisibleOptions.ShowStart);
			view.Selection.Select(span, false);
			view.Caret.MoveTo(span.End);
		}
		#endregion

		#region Edit
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
					if (s.HasValue) {
						return view.TextSnapshot.CreateTrackingSpan(s.Value, SpanTrackingMode.EdgeInclusive).GetSpan(shot);
					}
				}
				return null;
			}
		}

		public static FindOptions GetFindOptionsFromKeyboardModifiers() {
			switch (Keyboard.Modifiers) {
				case ModifierKeys.Control: return FindOptions.MatchCase | FindOptions.Wrap;
				case ModifierKeys.Shift: return FindOptions.WholeWord | FindOptions.Wrap;
				case ModifierKeys.Control | ModifierKeys.Shift: return FindOptions.MatchCase | FindOptions.WholeWord | FindOptions.Wrap;
				default: return FindOptions.Wrap;
			}
		}

		public static bool FindNext(this ITextView view, ITextSearchService2 searchService, string text, FindOptions options) {
			if (String.IsNullOrEmpty(text)) {
				return false;
			}
			var r = searchService.Find(view.Selection.StreamSelectionSpan.End.Position, text, options);
			if (r.HasValue) {
				view.SelectSpan(r.Value);
				return true;
			}
			return false;
		}

		public static SnapshotSpan WrapWith(this ITextView view, string prefix, string suffix) {
			var firstModified = new SnapshotSpan();
			var psLength = prefix.Length + suffix.Length;
			var removed = false;
			using (var edit = view.TextSnapshot.TextBuffer.CreateEdit()) {
				foreach (var item in view.Selection.SelectedSpans) {
					var t = item.GetText();
					// remove surrounding items
					if (t.Length > psLength
						&& t.StartsWith(prefix, StringComparison.Ordinal)
						&& t.EndsWith(suffix, StringComparison.Ordinal)
						&& t.IndexOf(prefix, prefix.Length, t.Length - psLength) <= t.IndexOf(suffix, prefix.Length, t.Length - psLength)) {
						if (edit.Replace(item, t.Substring(prefix.Length, t.Length - psLength))
							&& firstModified.Snapshot == null) {
							firstModified = item;
							removed = true;
						}
					}
					// surround items
					else if (edit.Replace(item, prefix + t + suffix) && firstModified.Snapshot == null) {
						firstModified = item;
					}
				}
				if (edit.HasEffectiveChanges) {
					var snapsnot = edit.Apply();
					firstModified = new SnapshotSpan(snapsnot, firstModified.Start, removed ? firstModified.Length - psLength : firstModified.Length + psLength);
				}
			}
			return firstModified;
		}


		public static void CopyOrMoveSyntaxNode(this IWpfTextView view, SyntaxNode sourceNode, SyntaxNode targetNode, bool copy, bool before) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var tSpan = (targetNode.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.VariableDeclarator) ? targetNode.Parent.Parent : targetNode).GetSematicSpan(false);
			var sNode = sourceNode.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.VariableDeclarator) ? sourceNode.Parent.Parent : sourceNode;
			var sSpan = sNode.GetSematicSpan(true);
			var target = before ? tSpan.Start : tSpan.End;
			var sPath = sourceNode.SyntaxTree.FilePath;
			var tPath = targetNode.SyntaxTree.FilePath;
			if (String.Equals(sPath, tPath, StringComparison.OrdinalIgnoreCase)) {
				using (var edit = view.TextBuffer.CreateEdit()) {
					edit.Insert(target, view.TextSnapshot.GetText(sSpan));
					if (copy == false) {
						edit.Delete(sSpan.Start, sSpan.Length);
					}
					if (edit.HasEffectiveChanges) {
						edit.Apply();
						view.SelectSpan(sSpan.Start > tSpan.Start ? target : target - sSpan.Length, sSpan.Length);
					}
				}
			}
			else if (String.Equals(sPath, CodistPackage.DTE.ActiveDocument.FullName, StringComparison.OrdinalIgnoreCase)) {
				// drag & drop from current file to external file
				if (copy == false) {
					using (var edit = view.TextBuffer.CreateEdit()) {
						edit.Delete(sSpan.Start, sSpan.Length);
						if (edit.HasEffectiveChanges) {
							edit.Apply();
						}
					}
				}
				CodistPackage.DTE.OpenFile(tPath, d => {
					var v = d.GetActiveWpfDocumentView();
					using (var edit = v.TextBuffer.CreateEdit()) {
						edit.Insert(target, sNode.ToFullString());
						if (edit.HasEffectiveChanges) {
							edit.Apply();
							v.SelectSpan(target, sSpan.Length);
						}
					}
				});
			}
			else {
				// drag & drop from external file to current file
				if (copy == false) {
					CodistPackage.DTE.OpenFile(sPath, d => {
						using (var edit = d.GetActiveWpfDocumentView().TextBuffer.CreateEdit()) {
							edit.Delete(sSpan.Start, sSpan.Length);
							if (edit.HasEffectiveChanges) {
								edit.Apply();
							}
						}
					});
				}
				using (var edit = view.TextBuffer.CreateEdit()) {
					edit.Insert(target, sNode.ToFullString());
					if (edit.HasEffectiveChanges) {
						edit.Apply();
						view.SelectSpan(target, sSpan.Length);
					}
				}
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
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
			CodistPackage.DTE.TryExecuteCommand(command, args);
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
		}

		public static void OpenFile(this EnvDTE.DTE dte, string file) {
			OpenFile(dte, file, null);
		}
		public static void OpenFile(this EnvDTE.DTE dte, string file, Action<EnvDTE.Document> action) {
			ThreadHelper.ThrowIfNotOnUIThread();
			if (String.IsNullOrEmpty(file)) {
				return;
			}
			file = System.IO.Path.GetFullPath(file);
			if (System.IO.File.Exists(file) == false) {
				return;
			}
			using (new NewDocumentStateScope(Keyboard.Modifiers == ModifierKeys.Shift ? __VSNEWDOCUMENTSTATE.NDS_Unspecified : __VSNEWDOCUMENTSTATE.NDS_Provisional, Microsoft.VisualStudio.VSConstants.NewDocumentStateReason.Navigation)) {
				dte.ItemOperations.OpenFile(file);
				if (action != null) {
					try {
						action.Invoke(dte.ActiveDocument);
					}
					catch (NullReferenceException) { /* ignore */ }
					catch (ArgumentException) { /* ignore */ }
				}
			}
		}
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
		public static void OpenFile(this EnvDTE.DTE dte, string file, int line, int column) {
			dte.OpenFile(file, d => ((EnvDTE.TextSelection)d.Selection).MoveToLineAndOffset(line, column));
		}
		public static void OpenFile(this EnvDTE.DTE dte, string file, int location) {
			if (location != 0) {
				dte.OpenFile(file, d => ((EnvDTE.TextSelection)d.Selection).MoveToAbsoluteOffset(location));
			}
			else {
				dte.OpenFile(file, _ => { });
			}
		}
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread

		public static bool AnyTextChanges(ITextVersion oldVersion, ITextVersion currentVersion) {
			while (oldVersion != currentVersion) {
				if (oldVersion.Changes.Count > 0) {
					return true;
				}
				oldVersion = oldVersion.Next;
			}
			return false;
		}
		#endregion

		#region Edit commands
		public static void JoinSelectedLines(this ITextView view) {
			if (view.TryGetFirstSelectionSpan(out var span) == false) {
				return;
			}
			var t = span.GetText();
			var b = new ReusableResourceHolder<System.Text.StringBuilder>();
			var sb = b.Resource;
			var p = 0;
			bool nl = false, noSlash = NoSlash(view);
			for (int i = 0; i < t.Length; i++) {
				switch (t[i]) {
					case '\r':
					case '\n':
						nl = true;
						goto case ' ';
					case ' ':
					case '\t':
						int j;
						for (j = i + 1; j < t.Length; j++) {
							switch (t[j]) {
								case ' ':
								case '\t':
									continue;
								case '/':
									if (nl && noSlash && (t[j-1] == '/' || j+1 < t.Length && t[j+1] == '/')) {
										continue;
									}
									goto default;
								case '\n':
								case '\r':
									nl = true;
									continue;
								default:
									goto CHECK_NEW_LINE;
							}
						}
					CHECK_NEW_LINE:
						if (nl == false || j <= i) {
							continue;
						}
						if (sb == null) {
							b = ReusableStringBuilder.AcquireDefault(t.Length);
							sb = b.Resource;
						}
						if (p == 0) {
							span = new SnapshotSpan(span.Snapshot, span.Start + i, span.Length - i);
						}
						else {
							sb.Append(t, p, i - p);
						}
						if (i > 0 && NeedSpace("(['\"", t[i - 1]) && j < t.Length && NeedSpace(")]'\"", t[j])) {
							sb.Append(' ');
						}
						i = j;
						p = j;
						nl = false;
						break;
				}
			}

			if (p > 0) {
				if (p < t.Length) {
					span = new SnapshotSpan(span.Snapshot, span.Start, span.Length - (t.Length - p));
				}
				view.TextBuffer.Replace(span, sb.ToString());
				b.Dispose();
			}

			bool NeedSpace(string tokens, char c) {
				return tokens.IndexOf(c) == -1 && c < 0x2E80;
			}
			bool NoSlash(ITextView v) {
				var ct = v.TextBuffer.ContentType;
				return ct.IsOfType(Constants.CodeTypes.CSharp) || ct.IsOfType(Constants.CodeTypes.CPlusPlus) || ct.LikeContentType("TypeScript") || ct.LikeContentType("Java");
			}
		}
		#endregion

		#region TextView and editor
		public static event EventHandler<TextViewCreatedEventArgs> ActiveTextViewChanged;

		public static ITextDocument GetTextDocument(this ITextBuffer textBuffer) {
			return textBuffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out var d) ? d : null;
		}
		public static bool LikeContentType(this ITextBuffer textBuffer, string typeName) {
			return textBuffer.ContentType.TypeName.IndexOf(typeName) != -1;
		}
		public static bool LikeContentType(this IContentType contentType, string typeName) {
			return contentType.TypeName.IndexOf(typeName) != -1;
		}

		public static IWpfTextView GetMouseOverDocumentView() {
			return _MouseOverTextView;
		}
		public static IWpfTextView GetActiveWpfDocumentView() {
			return _ActiveTextView;
			//ThreadHelper.ThrowIfNotOnUIThread();
			//return ServiceProvider.GlobalProvider.GetActiveWpfDocumentView();
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
		public static IWpfTextView GetActiveWpfDocumentView(this EnvDTE.Document doc) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var textView = GetIVsTextView(ServiceProvider.GlobalProvider, doc.FullName);
			return textView == null ? null : GetWpfTextView(textView);
		}

		public static IWpfTextView GetWpfTextView(this System.Windows.UIElement element) {
			foreach (var item in _WpfTextViews) {
				if (item.VisualElement.IsVisible == false) {
					continue;
				}
				if (item.VisualElement.Contains(element.TranslatePoint(new System.Windows.Point(0,0), item.VisualElement))) {
					return item;
				}
			}
			return null;
		}

		public static (string platformName, string configName) GetActiveBuildConfiguration() {
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
			return GetActiveBuildConfiguration(CodistPackage.DTE.ActiveDocument);
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
		}
		public static void SetActiveBuildConfiguration(string configName) {
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
			SetActiveBuildConfiguration(CodistPackage.DTE.ActiveDocument, configName);
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
		}

		public static List<string> GetBuildConfigNames() {
			ThreadHelper.ThrowIfNotOnUIThread();
			var configs = new List<string>();
			foreach (EnvDTE80.SolutionConfiguration2 conf in CodistPackage.DTE.Solution.SolutionBuild.SolutionConfigurations) {
				foreach (EnvDTE.SolutionContext context in conf.SolutionContexts) {
					if (configs.Contains(context.ConfigurationName) == false) {
						configs.Add(context.ConfigurationName);
					}
				}
			}
			return configs;
		}

		public static (string platformName, string configName) GetActiveBuildConfiguration(EnvDTE.Document document) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var pn = document.ProjectItem.ContainingProject.UniqueName;
			foreach (EnvDTE80.SolutionConfiguration2 conf in CodistPackage.DTE.Solution.SolutionBuild.SolutionConfigurations) {
				foreach (EnvDTE.SolutionContext context in conf.SolutionContexts) {
					if (context.ProjectName == pn) {
						return (conf.PlatformName, context.ConfigurationName);
					}
				}
			}
			return default;
		}

		public static void SetActiveBuildConfiguration(EnvDTE.Document document, string configName) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var pn = document.ProjectItem.ContainingProject.UniqueName;
			foreach (EnvDTE80.SolutionConfiguration2 conf in CodistPackage.DTE.Solution.SolutionBuild.SolutionConfigurations) {
				foreach (EnvDTE.SolutionContext context in conf.SolutionContexts) {
					if (context.ProjectName == pn) {
						context.ConfigurationName = configName;
						return;
					}
				}
			}
		}

		public static EnvDTE.Project GetProject(string projectName) {
			ThreadHelper.ThrowIfNotOnUIThread();
			return CodistPackage.DTE.Solution.Projects.Item(projectName);
		}

		public static bool IsVsixProject(this EnvDTE.Project project) {
			var extenders = project?.ExtenderNames as string[];
			return extenders != null && Array.IndexOf(extenders, "VsixProjectExtender") != -1;
		}

		public static bool IsVsixProject() {
			ThreadHelper.ThrowIfNotOnUIThread();
			try {
				var extenders = CodistPackage.DTE.ActiveDocument?.ProjectItem?.ContainingProject?.ExtenderNames as string[];
				return extenders != null && Array.IndexOf(extenders, "VsixProjectExtender") != -1;
			}
			catch (ArgumentException) {
				// hack: for https://github.com/wmjordan/Codist/issues/124
				return false;
			}
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

		#endregion

		[Export(typeof(IWpfTextViewCreationListener))]
		[ContentType(Constants.CodeTypes.Code)]
		[ContentType(Constants.CodeTypes.InteractiveContent)]
		[TextViewRole(PredefinedTextViewRoles.Document)]
		[TextViewRole(PredefinedTextViewRoles.Editable)]
		sealed class ActiveViewTrackerFactory : IWpfTextViewCreationListener
		{
			public void TextViewCreated(IWpfTextView textView) {
				new ActiveViewTracker(textView);
			}

			sealed class ActiveViewTracker
			{
				readonly IWpfTextView _View;

				public ActiveViewTracker(IWpfTextView view) {
					_ActiveTextView = _View = view;
					ActiveTextViewChanged?.Invoke(view, new TextViewCreatedEventArgs(view));
					view.Closed += TextViewClosed_UnloadView;
					view.VisualElement.MouseEnter += TextViewMouseEnter_SetActiveView;
					view.GotAggregateFocus += TextViewGotFocus_SetActiveView;
					_WpfTextViews.Add(view);
				}

				void TextViewGotFocus_SetActiveView(object sender, EventArgs e) {
					_ActiveTextView = _View;
					ActiveTextViewChanged?.Invoke(_View, new TextViewCreatedEventArgs(_View));
				}

				void TextViewMouseEnter_SetActiveView(object sender, MouseEventArgs e) {
					_MouseOverTextView = _View;
				}

				void TextViewClosed_UnloadView(object sender, EventArgs e) {
					var v = sender as IWpfTextView;
					v.Closed -= TextViewClosed_UnloadView;
					v.VisualElement.MouseEnter -= TextViewMouseEnter_SetActiveView;
					v.GotAggregateFocus -= TextViewGotFocus_SetActiveView;
					_WpfTextViews.Remove(v);
					System.Threading.Interlocked.CompareExchange(ref _MouseOverTextView, null, _View);
					System.Threading.Interlocked.CompareExchange(ref _ActiveTextView, null, _View);
				}
			}
		}

		/// <summary>
		/// A dummy classification type simply to serve the purpose of grouping classification types in the configuration list
		/// </summary>
		internal sealed class ClassificationCategory : IClassificationType
		{
			public ClassificationCategory(string classification) {
				Classification = classification;
			}

			public string Classification { get; }
			public IEnumerable<IClassificationType> BaseTypes => Array.Empty<IClassificationType>();

			public bool IsOfType(string type) { return false; }
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
		GuidPlaceHolder = 32,
		Hex = 64,
		ZeroXHex = 128
	}
}
