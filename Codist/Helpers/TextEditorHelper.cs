using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CLR;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Utilities;
using VsTextView = Microsoft.VisualStudio.TextManager.Interop.IVsTextView;
using VsUserData = Microsoft.VisualStudio.TextManager.Interop.IVsUserData;

namespace Codist;

/// <summary>
/// This class assumes that the <see cref="IClassificationFormatMap"/> is shared among document editor instances and the "default" classification format map contains all needed formatting.
/// </summary>
static class TextEditorHelper
{
	static /*readonly*/ Guid __IWpfTextViewHostGuid = new Guid("8C40265E-9FDB-4f54-A0FD-EBB72B7D0476"),
		__ViewKindCodeGuid = new Guid(EnvDTE.Constants.vsViewKindCode);
	static readonly HashSet<IWpfTextView> __WpfTextViews = new HashSet<IWpfTextView>();
	static IWpfTextView __MouseOverDocumentView, __ActiveDocumentView, __ActiveInteractiveView;
	static int __ActiveViewPosition;
	static bool __ActiveViewFocused;

	#region Position
	public static SnapshotPoint GetCaretPosition(this ITextView textView) {
		return textView.Caret.Position.BufferPosition;
	}

	public static CaretPosition MoveCaret(this ITextView textView, int position) {
		return textView.Caret.MoveTo(position < textView.TextSnapshot.Length
			? new SnapshotPoint(textView.TextSnapshot, position)
			: new SnapshotPoint(textView.TextSnapshot, textView.TextSnapshot.Length));
	}

	public static bool IsCaretInReadOnlyRegion(this ITextView textView) {
		return textView.TextBuffer.IsReadOnly(textView.Caret.Position.BufferPosition);
	}
	#endregion

	#region Selection
	public static void AddSelection(this IMultiSelectionBroker selectionBroker, Span span) {
		selectionBroker.AddSelection(new Selection(span.CreateSnapshotSpan(selectionBroker.CurrentSnapshot)));
	}
	public static void AddSelections(this IMultiSelectionBroker selectionBroker, IEnumerable<Span> spans) {
		var snapshot = selectionBroker.CurrentSnapshot;
		selectionBroker.AddSelectionRange(spans.Select(s => new Selection(s.CreateSnapshotSpan(snapshot))));
	}
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
	public static SnapshotSpan GetPrimarySelectionSpan(this ITextView view) {
		return view.GetMultiSelectionBroker().PrimarySelection.Extent.SnapshotSpan;
	}
	public static string GetPrimarySelectionText(this ITextView view) {
		return view.GetMultiSelectionBroker().PrimarySelection.Extent.SnapshotSpan.GetText();
	}
	public static bool TryGetFirstSelectionSpan(this ITextView view, out SnapshotSpan span) {
		var s = view.GetMultiSelectionBroker().PrimarySelection;
		if (s.IsEmpty || (span = s.Extent.SnapshotSpan).IsEmpty) {
			span = default;
			return false;
		}
		return true;
	}

	public static TokenType GetSelectedTokenType(this ITextView view) {
		if (view.Selection.IsEmpty) {
			return TokenType.None;
		}
		TokenType r = TokenType.None, t;
		var selectedSpans = view.Selection.SelectedSpans;
		if (selectedSpans.Count > 1000) {
			return TokenType.None;
		}
		foreach (var selection in selectedSpans) {
			if (selection.Length > 128 || (t = GetSelectionTokenType(selection)) == TokenType.None) {
				return TokenType.None;
			}
			r |= t;
			if (r.MatchFlags(TokenType.Hex) && r.HasAnyFlag(TokenType.Dot | TokenType.Underscore)
				|| r.MatchFlags(TokenType.ZeroXHex) && r.HasAnyFlag(TokenType.Hex | TokenType.Digit) == false) {
				return TokenType.None;
			}
		}
		return r;
	}

	static TokenType GetSelectionTokenType(SnapshotSpan selection) {
		string s = selection.GetText();
		switch (selection.Length) {
			case 4:
				if (s.Equals("Guid", StringComparison.OrdinalIgnoreCase)) {
					return TokenType.GuidPlaceHolder;
				}
				break;
			case 36:
			case 38:
				if (Guid.TryParse(s, out _)) {
					return TokenType.Guid;
				}
				break;
		}
		var t = TokenType.None;
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
		return t;
	}

	public static bool HasSingleSelection(this ITextView textView) {
		return !textView.Selection.IsEmpty
			&& !textView.GetMultiSelectionBroker().HasMultipleSelections;
	}

	public static bool IsMultilineSelected(this ITextView textView) {
		var s = textView.Selection;
		if (s.IsEmpty) {
			return false;
		}
		var ss = textView.GetMultiSelectionBroker().SelectionExtent.SnapshotSpan;
		var sl = textView.TextSnapshot.GetLineFromPosition(ss.Start);
		var el = textView.TextSnapshot.GetLineFromPosition(ss.End);
		return sl.Start.Position != el.Start.Position;
	}

	public static void SelectNode(this SyntaxNode node, bool includeTrivia) {
		if (node == null) {
			return;
		}
		OpenFile(node.GetLocation().SourceTree.FilePath,
			view => view.SelectSpan(includeTrivia ? node.GetSematicSpan(true) : node.Span.ToSpan()));
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
		view.SelectSpan(span.Start, span.Length, -1);
	}

	public static void SelectSpan(this ITextView view, int start, int length, int moveCaret) {
		if (length < 0 || start < 0 || start + length > view.TextSnapshot.Length) {
			return;
		}
		var span = new SnapshotSpan(view.TextSnapshot, start, length);
		view.ViewScroller.EnsureSpanVisible(span, EnsureSpanVisibleOptions.ShowStart);
		view.Selection.Select(span, false);
		if (moveCaret != 0) {
			view.Caret.MoveTo(moveCaret > 0 ? span.End : span.Start);
		}
	}

	public static void SelectSpans(this ITextView view, IEnumerable<SnapshotSpan> spans) {
		if (spans == null || spans == Enumerable.Empty<SnapshotSpan>()) {
			return;
		}
		var b = view.GetMultiSelectionBroker();
		var m = ServicesHelper.Instance.OutliningManager.GetOutliningManager(view);
		SnapshotSpan primary = default;
		var caret = view.GetCaretPosition();
		using (var o = b.BeginBatchOperation()) {
			foreach (var span in spans) {
				if (view.TextSnapshot != span.Snapshot) {
					// should not be here
					continue;
				}
				if (m != null) {
					foreach (var c in m.GetCollapsedRegions(span)) {
						m.Expand(c);
					}
				}
				if (primary.IsEmpty && span.Contains(caret)) {
					primary = span;
				}
				b.AddSelection(new Selection(span));
			}
		}

		if (primary.IsEmpty == false) {
			view.ViewScroller.EnsureSpanVisible(primary, EnsureSpanVisibleOptions.ShowStart);
		}
	}

	public static IEnumerable<ITextSnapshotLine> GetSelectedLines(this ITextView view) {
		ITextSnapshotLine l = null, startLine;
		int end;
		var snapshot = view.TextBuffer.CurrentSnapshot;
		foreach (var span in view.Selection.SelectedSpans) {
			startLine = snapshot.GetLineFromPosition(span.Start.Position);
			end = snapshot.GetLineFromPosition(span.End.Position).Start.Position;
			if (l == null || startLine.Start.Position != l.Start.Position) {
				l = startLine;
				yield return l;
			}
			while (startLine.Start.Position < end) {
				startLine = snapshot.GetLineFromLineNumber(startLine.LineNumber + 1);
				if (startLine != l) {
					l = startLine;
					yield return l;
				}
			}
		}
	}
	#endregion

	#region Edit
	/// <inheritdoc cref="Edit{TView, TArg}(TView, TArg, Action{TView, TArg, ITextEdit})"/>
	public static ITextSnapshot Edit<TView>(this TView view, Action<TView, ITextEdit> action)
		where TView : ITextView {
		using (var edit = view.TextSnapshot.TextBuffer.CreateEdit()) {
			action(view, edit);
			return edit.HasEffectiveChanges
				? edit.Apply()
				: null;
		}
	}
	/// <summary>
	/// Begins an edit operation to the <paramref name="view"/>.
	/// </summary>
	/// <typeparam name="TView">The type of the view.</typeparam>
	/// <typeparam name="TArg">The type of the argument.</typeparam>
	/// <param name="view">The <see cref="ITextView"/> to be edited.</param>
	/// <param name="arg">The argument for <paramref name="action"/>.</param>
	/// <param name="action">The edit operation.</param>
	/// <returns>Returns a new <see cref="ITextSnapshot"/> if <see cref="ITextEdit.HasEffectiveChanges"/>  returns <see langword="true"/>, otherwise, returns <see langword="null"/>.</returns>
	public static ITextSnapshot Edit<TView, TArg>(this TView view, TArg arg, Action<TView, TArg, ITextEdit> action)
		where TView : ITextView {
		using (var edit = view.TextSnapshot.TextBuffer.CreateEdit()) {
			action(view, arg, edit);
			return edit.HasEffectiveChanges
				? edit.Apply()
				: null;
		}
	}
	/// <summary>
	/// Performs edit operation to each selected spans in the <paramref name="view"/>.
	/// </summary>
	/// <typeparam name="TView">The type of the view.</typeparam>
	/// <param name="view">The <see cref="ITextView"/> to be edited.</param>
	/// <param name="action">The edit operation against each selected span.</param>
	/// <returns>Returns a collections of new <see cref="SnapshotSpan"/>s if <see cref="ITextEdit.HasEffectiveChanges"/> returns <see langword="true"/> and any <paramref name="action"/> returns a <see cref="Span"/>, otherwise, returns <see langword="null"/>.</returns>
	public static IEnumerable<SnapshotSpan> EditSelection<TView>(this TView view, Func<TView, ITextEdit, SnapshotSpan, Span?> action)
		where TView : ITextView {
		Chain<Span> changedSpans = new Chain<Span>();
		Span? s;
		using (var edit = view.TextSnapshot.TextBuffer.CreateEdit()) {
			foreach (var item in view.Selection.SelectedSpans) {
				s = action(view, edit, item);
				if (s.HasValue) {
					changedSpans.Add(s.Value);
				}
			}
			if (edit.HasEffectiveChanges) {
				var oldSnapshot = view.TextSnapshot;
				var newSnapshot = edit.Apply();
				if (changedSpans.IsEmpty == false) {
					return changedSpans.Select(i => oldSnapshot.MapTo(i, newSnapshot));
				}
			}
		}
		return null;
	}

	/// <summary>
	/// Performs edit operation to each selected spans in the <paramref name="view"/>.
	/// </summary>
	/// <typeparam name="TView">The type of the view.</typeparam>
	/// <param name="view">The <see cref="ITextView"/> to be edited.</param>
	/// <param name="action">The edit operation against each selected span, returns a collection of spans for select.</param>
	/// <returns>Returns a collections of new <see cref="SnapshotSpan"/>s if <see cref="ITextEdit.HasEffectiveChanges"/> returns <see langword="true"/> and any <paramref name="action"/> returns a <see cref="Span"/>, otherwise, returns <see langword="null"/>.</returns>
	public static IEnumerable<SnapshotSpan> EditSelection<TView>(this TView view, Func<TView, ITextEdit, SnapshotSpan, IEnumerable<Span>> action)
		where TView : ITextView {
		var changedSpans = new Chain<Span>();
		using (var edit = view.TextSnapshot.TextBuffer.CreateEdit()) {
			foreach (var item in view.Selection.SelectedSpans) {
				foreach (var span in action(view, edit, item)) {
					changedSpans.Add(span);
				}
			}
			if (edit.HasEffectiveChanges) {
				var oldSnapshot = view.TextSnapshot;
				var newSnapshot = edit.Apply();
				if (changedSpans.IsEmpty == false) {
					return changedSpans.Select(i => oldSnapshot.MapTo(i, newSnapshot));
				}
			}
		}
		return null;
	}

	public static FindOptions GetFindOptionsFromKeyboardModifiers() {
		return Keyboard.Modifiers.Case(
			ModifierKeys.Control, FindOptions.MatchCase | FindOptions.Wrap,
			ModifierKeys.Shift, FindOptions.WholeWord | FindOptions.Wrap,
			ModifierKeys.Control | ModifierKeys.Shift, FindOptions.MatchCase | FindOptions.WholeWord | FindOptions.Wrap,
			FindOptions.Wrap);
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
					view.SelectSpan(sSpan.Start > tSpan.Start ? target : target - sSpan.Length, sSpan.Length, -1);
				}
			}
		}
		else if (String.Equals(sPath, view.TextBuffer.GetTextDocument()?.FilePath, StringComparison.OrdinalIgnoreCase)) {
			// drag & drop from current file to external file
			if (copy == false) {
				using (var edit = view.TextBuffer.CreateEdit()) {
					edit.Delete(sSpan.Start, sSpan.Length);
					if (edit.HasEffectiveChanges) {
						edit.Apply();
					}
				}
			}
			OpenFile(tPath, v => {
				using (var edit = v.TextBuffer.CreateEdit()) {
					edit.Insert(target, sNode.ToFullString());
					if (edit.HasEffectiveChanges) {
						edit.Apply();
						v.SelectSpan(target, sSpan.Length, -1);
					}
				}
			});
		}
		else {
			// drag & drop from external file to current file
			if (copy == false) {
				OpenFile(sPath, v => {
					using (var edit = v.TextBuffer.CreateEdit()) {
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
					view.SelectSpan(target, sSpan.Length, -1);
				}
			}
		}
	}

	public static bool IsCommandAvailable(string command) {
		ThreadHelper.ThrowIfNotOnUIThread();
		try {
			return CodistPackage.DTE.Commands.Item(command).IsAvailable;
		}
		catch (ArgumentException) {
			return false;
		}
	}

	public static void ExecuteEditorCommand(string command, string args = "") {
		ThreadHelper.ThrowIfNotOnUIThread();
		try {
			if (IsCommandAvailable(command)) {
				CodistPackage.DTE.ExecuteCommand(command, args);
			}
		}
		catch (System.Runtime.InteropServices.COMException ex) {
			System.Windows.Forms.MessageBox.Show(ex.ToString());
			if (System.Diagnostics.Debugger.IsAttached) {
				System.Diagnostics.Debugger.Break();
			}
		}
	}

	/// <summary>
	/// Gets the trigger point and the containing <see cref="ITextBuffer"/> of <see cref="IAsyncQuickInfoSession"/>.
	/// </summary>
	public static ITextBuffer GetSourceBuffer(this IAsyncQuickInfoSession session, out SnapshotPoint snapshotPoint) {
		var buffer = session.TextView.TextBuffer;
		ITrackingPoint triggerPoint;
		if (buffer is IProjectionBuffer projection) {
			foreach (var sb in projection.SourceBuffers) {
				if ((triggerPoint = session.GetTriggerPoint(sb)) != null) {
					snapshotPoint = triggerPoint.GetPoint(sb.CurrentSnapshot);
					return sb;
				}
			}
		}
		snapshotPoint = session.GetTriggerPoint(buffer).GetPoint(buffer.CurrentSnapshot);
		return buffer;
	}

	public static SnapshotSpan GetTriggerSpan(this IAsyncQuickInfoSession session) {
		return session.ApplicableToSpan.GetSpan(session.TextView.TextSnapshot);
	}

	/// <summary>
	/// <para>When we click from the Symbol Link or the context menu command on the Quick Info,
	/// <see cref="OpenFile"/> command will be executed and caret will be moved to the new place.</para>
	/// <para>While we <i>Navigate Backward</i>, the caret will be located at the place before Symbol Link 
	/// or the context symbol command was executed, instead of the place that triggered
	/// the Quick Info. This is inconvenient while browsing code files.</para>
	/// <para>We use this function to keep the view position that triggers the Quick Info,
	/// and use the position in the <see cref="MoveCaretToKeptViewPosition"/> function, before
	/// <see cref="InternalOpenFile"/>.</para>
	/// <para>So <i>Navigate Backward</i> command will restore the caret position to that point.</para>
	/// </summary>
	public static void KeepViewPosition(this IAsyncQuickInfoSession session) {
		if (session.TextView is IWpfTextView v) {
			__ActiveDocumentView = v;
			__ActiveViewPosition = session.GetTriggerPoint(v.TextSnapshot)?.Position ?? -1;
			if (session.Options == QuickInfoSessionOptions.TrackMouse) {
				__MouseOverDocumentView = v;
			}
			session.StateChanged += QuickInfoSession_StateChanged;
		}
	}

	static void QuickInfoSession_StateChanged(object sender, QuickInfoSessionStateChangedEventArgs e) {
		if (e.NewState == QuickInfoSessionState.Dismissed) {
			ForgetViewPosition();
			((IAsyncQuickInfoSession)sender).StateChanged -= QuickInfoSession_StateChanged;
		}
	}

	public static void ForgetViewPosition() {
		__ActiveViewPosition = -1;
	}
	public static void OpenFile(string file) {
		OpenFile(file, (VsTextView _) => { });
	}
	public static void OpenFile(string file, Action<VsTextView> action) {
		ThreadHelper.ThrowIfNotOnUIThread();
		if (String.IsNullOrEmpty(file)) {
			return;
		}
		file = Path.GetFullPath(file);
		if (File.Exists(file) == false) {
			return;
		}
		if (__ActiveViewPosition > -1) {
			MoveCaretToKeptViewPosition();
		}

		InternalOpenFile(file, action);
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	static void InternalOpenFile(string file, Action<VsTextView> action) {
		try {
			using (new NewDocumentStateScope(UIHelper.IsShiftDown ? __VSNEWDOCUMENTSTATE.NDS_Unspecified : __VSNEWDOCUMENTSTATE.NDS_Provisional, Microsoft.VisualStudio.VSConstants.NewDocumentStateReason.Navigation)) {
				VsShellUtilities.OpenDocument(ServiceProvider.GlobalProvider, file, __ViewKindCodeGuid, out var hierarchy, out var itemId, out var windowFrame, out var view);
				action?.Invoke(view);
			}
		}
		catch (Exception ex) {
			ex.Log();
			/* ignore */
		}
	}

	static void MoveCaretToKeptViewPosition() {
		var v = __MouseOverDocumentView ?? __ActiveDocumentView;
		SnapshotPoint p;
		if (v != null
			&& __ActiveViewPosition < v.TextSnapshot.Length
			&& v.Caret.ContainingTextViewLine.ContainsBufferPosition(p = new SnapshotPoint(v.TextSnapshot, __ActiveViewPosition)) == false) {
			v.Caret.MoveTo(p);
			__ActiveViewPosition = -1;
		}
	}

	public static void OpenFile(string file, Action<IWpfTextView> action) {
		OpenFile(file, (VsTextView view) => action(GetWpfTextView(view)));
	}
	public static void OpenFile(string file, int line, int column) {
		OpenFile(file, d => {
			d.SetTopLine(Math.Max(0, line - 5));
			d.SetCaretPos(line, column);
		});
	}
	#endregion

	#region Properties
	public static bool Mark<TKey>(this IPropertyOwner owner, TKey mark) {
		if (owner.Properties.ContainsProperty(mark) == false) {
			owner.Properties.AddProperty(mark, mark);
			return true;
		}
		return false;
	}
	public static TObject GetOrCreateSingletonProperty<TObject>(this IPropertyOwner propertyOwner)
		where TObject : class, new() {
		return propertyOwner.Properties.GetOrCreateSingletonProperty(() => new TObject());
	}
	public static TObject GetOrCreateSingletonProperty<TObject>(this IPropertyOwner propertyOwner, Func<TObject> factory)
		where TObject : class {
		return propertyOwner.Properties.GetOrCreateSingletonProperty(typeof(TObject), factory);
	}
	public static TObject CreateProperty<TObject>(this IPropertyOwner propertyOwner)
		where TObject : class, new() {
		var o = new TObject();
		propertyOwner.Properties.AddProperty(typeof(TObject), o);
		return o;
	}
	public static bool TryGetProperty<TObject>(this IPropertyOwner propertyOwner, out TObject value) {
		return propertyOwner.Properties.TryGetProperty(typeof(TObject), out value);
	}
	public static bool RemoveProperty<TObject>(this IPropertyOwner propertyOwner) {
		return propertyOwner.Properties.RemoveProperty(typeof(TObject));
	}
	#endregion

	#region TextView and editor
	public static event EventHandler<TextViewCreatedEventArgs> ActiveTextViewChanged;

	public static string GetViewCategory(this ITextView view) {
		return view.Options.GetOptionValue(DefaultWpfViewOptions.AppearanceCategory);
	}

	/// <summary>Gets the floating point zoom factor <c>(<see cref="IWpfTextView.ZoomLevel"/> / 100)</c> from specific view</summary>
	public static double ZoomFactor(this IWpfTextView view) {
		return view.ZoomLevel / 100;
	}

	/// <summary>A rough method to detect whether a document can be edited.</summary>
	public static bool MayBeEditor(this ITextBuffer textBuffer) {
		return (textBuffer.IsReadOnly(0) == false
				|| textBuffer.Properties.ContainsProperty(typeof(ITextDocument))
				|| textBuffer is IProjectionBuffer pb && pb.SourceBuffers.Any(MayBeEditor))
			&& textBuffer.ContentType.IsOfType("RoslynPreviewContentType") == false;
	}

	public static ITextDocument GetTextDocument(this ITextBuffer textBuffer) {
		return textBuffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out var d) ? d : null;
	}
	public static string GetText(this ITextBuffer textBuffer) {
		return textBuffer.CurrentSnapshot.GetText();
	}

	public static string GetText(this ITextBuffer textBuffer, int start, int end) {
		var e = textBuffer.CurrentSnapshot.Length;
		if (start >= e) {
			start = e;
		}
		if (end >= e) {
			end = e;
		}
		return end <= start
			? String.Empty
			: textBuffer.CurrentSnapshot.GetText(start, end - start);
	}

	public static void ClearUndoHistory(this ITextBuffer textBuffer) {
		var s = ServicesHelper.Instance.TextUndoHistory;
		if (s.TryGetHistory(textBuffer, out var history)) {
			s.RemoveHistory(history);
		}
	}

	public static bool IsEmptyOrWhitespace(this ITextSnapshotLine line) {
		int i, end = line.End.Position;
		var ts = line.Snapshot;
		for (i = line.Start.Position; i < end && ts[i].IsCodeWhitespaceChar(); i++) { }
		return i == end;
	}

	public static int CountLinePrecedingWhitespace(this ITextSnapshotLine line) {
		return CountPrecedingWhitespace(line.Snapshot, line.Start.Position, line.End.Position);
	}

	public static int CountPrecedingWhitespace(this ITextSnapshot ts, int start, int end) {
		int i;
		for (i = start; i < end && ts[i].IsCodeWhitespaceChar(); i++) { }
		return i - start;
	}

	public static string GetLinePrecedingWhitespace(this ITextSnapshotLine line) {
		return line.Snapshot.GetText(line.Start, line.CountLinePrecedingWhitespace());
	}

	public static string GetLinePrecedingWhitespaceAtPosition(this ITextSnapshot textSnapshot, int position) {
		return GetLinePrecedingWhitespace(textSnapshot.GetLineFromPosition(position));
	}

	public static int GetIndentStringSize(this IEditorOptions options) {
		return options.IsConvertTabsToSpacesEnabled() ? options.GetIndentSize() : -1;
	}
	public static string GetIndentString(this IEditorOptions options, int amount = 1) {
		return options.IsConvertTabsToSpacesEnabled()
			? IndentStrings.GetSpaces(options.GetIndentSize() * amount)
			: IndentStrings.GetTab( amount);
	}

	public static bool IsCodeWhitespaceChar(this char ch) {
		return ch.CeqAny(' ', '\t');
	}
	public static bool IsCodeWhitespaceOrNewLine(this char ch) {
		return ch.CeqAny(' ', '\t', '\r', '\n');
	}
	public static bool IsNewLine(this char ch) {
		return ch.CeqAny('\r', '\n');
	}
	public static bool LikeContentType(this ITextBuffer textBuffer, string typeName) {
		var n = textBuffer.ContentType.TypeName;
		return n.IndexOf(typeName) != -1
			|| n.StartsWith(typeName, StringComparison.OrdinalIgnoreCase)
			|| n.EndsWith(typeName, StringComparison.OrdinalIgnoreCase);
	}
	public static bool LikeContentType(this IContentType contentType, string typeName) {
		return contentType.TypeName.IndexOf(typeName) != -1;
	}
	public static bool IsContentTypeIncludingProjection(this ITextBuffer textBuffer, string typeName) {
		return textBuffer.ContentType.IsOfType(typeName)
			|| textBuffer is IProjectionBuffer p && p.SourceBuffers.Any(i => i.IsContentTypeIncludingProjection(typeName));
	}

	public static SnapshotSpan GetVisibleLineSpan(this IWpfTextView view) {
		return new SnapshotSpan(view.TextViewLines.FirstVisibleLine.Start, view.TextViewLines.LastVisibleLine.End);
	}

	public static IWpfTextView GetMouseOverDocumentView() {
		return __MouseOverDocumentView;
	}
	// note: Due to a bug in VS, mouse focus could sometimes not in sync, but we can check the keyboard focus if the window is keyboard focusable
	public static bool ActiveViewFocused() {
		return __ActiveViewFocused;
	}
	public static IWpfTextView GetActiveWpfDocumentView() {
		return __ActiveDocumentView;
		//ThreadHelper.ThrowIfNotOnUIThread();
		//return ServiceProvider.GlobalProvider.GetActiveWpfDocumentView();
	}
	public static IWpfTextView GetActiveWpfInteractiveView() {
		return __ActiveInteractiveView;
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
	public static Microsoft.VisualStudio.TextManager.Interop.IVsTextView GetActiveDocumentView(this EnvDTE.Document doc) {
		ThreadHelper.ThrowIfNotOnUIThread();
		return GetIVsTextView(ServiceProvider.GlobalProvider, doc.FullName);
	}
	public static IWpfTextView GetActiveWpfDocumentView(this EnvDTE.Document doc) {
		var textView = doc.GetActiveDocumentView();
		return textView == null ? null : GetWpfTextView(textView);
	}

	public static IWpfTextView GetWpfTextView(this UIElement element) {
		foreach (var item in __WpfTextViews) {
			if (item.VisualElement.IsVisible == false) {
				continue;
			}
			if (item.VisualElement.Contains(element.TranslatePoint(new Point(0,0), item.VisualElement))) {
				return item;
			}
		}
		return null;
	}

	static VsTextView GetIVsTextView(IServiceProvider service, string filePath) {
		return VsShellUtilities.IsDocumentOpen(service, filePath, Guid.Empty, out _, out _, out IVsWindowFrame windowFrame)
			? VsShellUtilities.GetTextView(windowFrame)
			: null;
	}
	static IWpfTextView GetWpfTextView(VsTextView vTextView) {
		if (vTextView is VsUserData userData) {
			var guidViewHost = __IWpfTextViewHostGuid;
			userData.GetData(ref guidViewHost, out object holder);
			return ((IWpfTextViewHost)holder).TextView;
		}
		return null;
	}

	#endregion

	#region Action Repeater
	/// <summary>
	/// Register an <see cref="Action"/> which associated with a <see cref="IPropertyOwner"/> and can be invoked later.
	/// </summary>
	public static void RegisterRepeatingAction(this IPropertyOwner propertyOwner, Action action, Action unloadAction) {
		if (action != null) {
			propertyOwner.Properties[typeof(RepeatingAction)] = new RepeatingAction(action, unloadAction);
		}
		else {
			propertyOwner.RemoveProperty<RepeatingAction>();
		}
	}
	public static bool UnregisterRepeatingAction(this IPropertyOwner propertyOwner) {
		if (propertyOwner.Properties.TryGetProperty(typeof(RepeatingAction), out RepeatingAction action)) {
			action.Unregister();
			propertyOwner.RemoveProperty<RepeatingAction>();
			return true;
		}
		return false;
	}
	public static bool HasRepeatingAction(this IPropertyOwner propertyOwner) {
		return propertyOwner.Properties.TryGetProperty(typeof(RepeatingAction), out RepeatingAction _);
	}
	/// <summary>
	/// Try to invoke the repeatable <see cref="Action"/> registered by <see cref="RegisterRepeatingAction(IPropertyOwner, Action)"/>.
	/// </summary>
	public static void TryRepeatAction(this IPropertyOwner propertyOwner) {
		if (propertyOwner.Properties.TryGetProperty(typeof(RepeatingAction), out RepeatingAction action)) {
			action.Run();
		}
	}
	#endregion

	[Export(typeof(IWpfTextViewCreationListener))]
	[ContentType(Constants.CodeTypes.Code)]
	[ContentType(Constants.CodeTypes.Text)]
	//[ContentType(Constants.CodeTypes.Output)]
	[ContentType(Constants.CodeTypes.InteractiveContent)]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	sealed class ActiveViewTrackerFactory : IWpfTextViewCreationListener
	{
		public void TextViewCreated(IWpfTextView textView) {
			new ActiveViewTracker(textView);
		}

		sealed class ActiveViewTracker
		{
			readonly bool _IsDocument;
			IWpfTextView _View;

			public ActiveViewTracker(IWpfTextView view) {
				__ActiveInteractiveView = _View = view;
				ActiveTextViewChanged?.Invoke(view, new TextViewCreatedEventArgs(view));
				view.Closed += TextView_CloseView;
				view.VisualElement.Loaded += TextView_SetActiveView;
				view.VisualElement.MouseEnter += TextViewMouseEnter_SetActiveView;
				view.VisualElement.GotFocus += TextView_GotFocus;
				view.VisualElement.LostFocus += TextView_LostFocus;
				view.GotAggregateFocus += TextView_SetActiveView;
				if (view.Roles.Contains(PredefinedTextViewRoles.Document)) {
					_IsDocument = true;
					__ActiveDocumentView = view;
					__WpfTextViews.Add(view);
				}
			}

			void TextView_GotFocus(object sender, EventArgs e) {
				if (__ActiveInteractiveView == _View) {
					__ActiveViewFocused = true;
				}
			}
			void TextView_LostFocus(object sender, EventArgs e) {
				if (__ActiveInteractiveView == _View) {
					__ActiveViewFocused = false;
				}
			}

			void TextView_SetActiveView(object sender, EventArgs e) {
				if (__ActiveInteractiveView != _View && _View.HasAggregateFocus) {
					__ActiveInteractiveView = _View;
					if (_IsDocument && __ActiveDocumentView != _View) {
						__ActiveDocumentView = _View;
					}
					ForgetViewPosition();
					ActiveTextViewChanged?.Invoke(_View, new TextViewCreatedEventArgs(_View));
				}
				if (_IsDocument && __MouseOverDocumentView == null) {
					__MouseOverDocumentView = _View;
				}
				__ActiveViewFocused = true;
			}

			void TextViewMouseEnter_SetActiveView(object sender, MouseEventArgs e) {
				if (_IsDocument) {
					__MouseOverDocumentView = _View;
				}
			}

			void TextView_CloseView(object sender, EventArgs e) {
				ForgetViewPosition();
				var v = _View;
				if (_IsDocument) {
					if (__MouseOverDocumentView == v) {
						__MouseOverDocumentView = null;
					}
					if (__ActiveDocumentView == v) {
						__ActiveDocumentView = null;
					}
					__WpfTextViews.Remove(v);
				}
				if (__ActiveInteractiveView == v) {
					__ActiveInteractiveView = null;
				}
				_View = null;
				v.Closed -= TextView_CloseView;
				v.VisualElement.Loaded -= TextView_SetActiveView;
				v.VisualElement.MouseEnter -= TextViewMouseEnter_SetActiveView;
				v.VisualElement.GotFocus -= TextView_GotFocus;
				v.VisualElement.LostFocus -= TextView_LostFocus;
				v.GotAggregateFocus -= TextView_SetActiveView;
			}
		}
	}

	// hack: workaround for compatibility with VS 2017, 2019 and VS 2022
	// taken from Microsoft.VisualStudio.Shell.NewDocumentStateScope
	sealed class NewDocumentStateScope : DisposableObject
	{
		readonly IVsNewDocumentStateContext _Context;

		private NewDocumentStateScope(uint state, Guid reason) {
			ThreadHelper.ThrowIfNotOnUIThread();
			if (Package.GetGlobalService(typeof(IVsUIShellOpenDocument)) is IVsUIShellOpenDocument3 doc) {
				_Context = doc.SetNewDocumentState(state, ref reason);
			}
		}

		public NewDocumentStateScope(__VSNEWDOCUMENTSTATE state, Guid reason)
			: this((uint)state, reason) {
		}

		public NewDocumentStateScope(__VSNEWDOCUMENTSTATE2 state, Guid reason)
			: this((uint)state, reason) {
		}

		protected override void DisposeNativeResources() {
			ThreadHelper.ThrowIfNotOnUIThread();
			_Context?.Restore();
			base.DisposeNativeResources();
		}
	}

	sealed class RepeatingAction
	{
		readonly Action _RepeatAction, _UnregisterAction;

		public RepeatingAction(Action repeatAction, Action unregisterAction) {
			_RepeatAction = repeatAction;
			_UnregisterAction = unregisterAction;
		}
		public void Run() {
			_RepeatAction();
		}
		public void Unregister() {
			_UnregisterAction();
		}
	}

	static class IndentStrings
	{
		static readonly string[] __Spaces = MakeSpaceIndentStrings();
		const int MAX_CACHED_ITEMS = 17;
		const string Tab = "\t", DoubleTab = "\t\t";

		static string[] MakeSpaceIndentStrings() {
			var s = new string[MAX_CACHED_ITEMS];
			s[0] = String.Empty;
			for (int i = 1; i < MAX_CACHED_ITEMS; i++) {
				s[i] = new string(' ', i);
			}
			return s;
		}

		public static string GetSpaces(int amount) {
			return amount < MAX_CACHED_ITEMS ? __Spaces[amount] : new string(' ', amount);
		}
		public static string GetTab(int amount) {
			return amount == 1 ? Tab : amount == 2 ? DoubleTab : new string('\t', amount);
		}
	}
}
