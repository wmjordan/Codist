using System;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using VsTextView = Microsoft.VisualStudio.TextManager.Interop.IVsTextView;
using VsUserData = Microsoft.VisualStudio.TextManager.Interop.IVsUserData;

namespace Codist
{
	static class TextEditorHelper
	{
		static /*readonly*/ Guid guidIWpfTextViewHost = new Guid("8C40265E-9FDB-4f54-A0FD-EBB72B7D0476");

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
			var t = TokenType.None;
			foreach (var c in selection.GetText()) {
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
		public static void SelectNode(this IWpfTextView view, Microsoft.CodeAnalysis.SyntaxNode node, bool includeTrivia) {
			if (includeTrivia) {
				view.Selection.Select(new SnapshotSpan(view.TextSnapshot, node.FullSpan.Start, node.FullSpan.Length), false);
			}
			else {
				view.Selection.Select(new SnapshotSpan(view.TextSnapshot, node.Span.Start, node.Span.Length), false);
			}
		}

		public static void TryExecuteCommand(this EnvDTE.DTE dte, string command) {
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
			var doc = CodistPackage.DTE.ActiveDocument;
			if (doc == null) {
				return null;
			}
			var textView = GetIVsTextView(service, doc.FullName);
			return textView == null ? null : GetWpfTextView(textView);
		}

		static VsTextView GetIVsTextView(IServiceProvider service, string filePath) {
			IVsWindowFrame windowFrame;
			return VsShellUtilities.IsDocumentOpen(service, filePath, Guid.Empty, out IVsUIHierarchy uiHierarchy, out uint itemID, out windowFrame)
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
	}

	[Flags]
	public enum TokenType
	{
		None,
		Letter = 1,
		Digit = 2,
		Dot = 4,
		Underscore = 8
	}
}
