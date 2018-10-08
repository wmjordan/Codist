using System;
using System.Windows;
using System.Windows.Input;
using AppHelpers;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.SmartBars
{
	partial class SmartBar
	{
		static void ExecuteAndFind(CommandContext ctx, string command) {
			ThreadHelper.ThrowIfNotOnUIThread();
			if (ctx.RightClick) {
				ctx.View.ExpandSelectionToLine(false);
			}
			string t = null;
			if (Keyboard.Modifiers == ModifierKeys.Control && ctx.View.Selection.IsEmpty == false) {
				t = ctx.View.TextSnapshot.GetText(ctx.View.Selection.SelectedSpans[0]);
			}
			TextEditorHelper.ExecuteEditorCommand(command);
			if (t != null) {
				var p = (CodistPackage.DTE.ActiveDocument.Object() as EnvDTE.TextDocument).Selection;
				if (p != null && p.FindText(t, (int)(EnvDTE.vsFindOptions.vsFindOptionsMatchCase))) {
					ctx.KeepToolBarAsync(true);
				}
			}
		}

		void AddCopyCommand() {
			AddCommand(ToolBar, KnownImageIds.Copy, "Copy selected text\nRight click: Copy line", ctx => {
				if (ctx.RightClick) {
					ctx.View.ExpandSelectionToLine();
				}
				TextEditorHelper.ExecuteEditorCommand("Edit.Copy");
			});
		}

		void AddCutCommand() {
			AddCommand(ToolBar, KnownImageIds.Cut, "Cut selected text\nRight click: Cut line", ctx => {
				if (ctx.RightClick) {
					ctx.View.ExpandSelectionToLine();
				}
				TextEditorHelper.ExecuteEditorCommand("Edit.Cut");
			});
		}

		void AddDeleteCommand() {
			AddCommand(ToolBar, KnownImageIds.Cancel, "Delete selected text\nRight click: Delete line\nCtrl click: Delete and select next", ctx => {
				var s = View.Selection;
				if (s.Mode == TextSelectionMode.Stream && ctx.RightClick == false && Keyboard.Modifiers != ModifierKeys.Control && s.IsEmpty == false) {
					var end = s.End.Position;
					// remove a trailing space
					if (end < View.TextSnapshot.Length - 1) {
						var trailer = View.TextSnapshot.GetText(end - 1, 2);
						if (Char.IsLetterOrDigit(trailer[0]) && trailer[1] == ' ') {
							s.Select(new SnapshotSpan(s.Start.Position, s.End.Position + 1), false);
						}
					}
				}
				ExecuteAndFind(ctx, "Edit.Delete");
			});
		}

		void AddDuplicateCommand() {
			AddCommand(ToolBar, KnownImageIds.CopyItem, "Duplicate selection\nRight click: Duplicate line", ctx => {
				if (ctx.RightClick) {
					ctx.View.ExpandSelectionToLine();
				}
				TextEditorHelper.ExecuteEditorCommand("Edit.Duplicate");
				ctx.KeepToolBarAsync(true);
			});
		}

		void AddFindAndReplaceCommands() {
			AddCommands(ToolBar, KnownImageIds.FindNext, "Find next selected text\nCtrl click: Find match case\nRight click: Find and replace...", ctx => {
				ThreadHelper.ThrowIfNotOnUIThread();
				string t = ctx.View.Selection.IsEmpty == false
					? ctx.View.TextSnapshot.GetText(ctx.View.Selection.SelectedSpans[0])
					: null;
				if (t == null) {
					return;
				}
				var p = (CodistPackage.DTE.ActiveDocument.Object() as EnvDTE.TextDocument).Selection;
				var option = Keyboard.Modifiers.MatchFlags(ModifierKeys.Control) ? EnvDTE.vsFindOptions.vsFindOptionsMatchCase : 0;
				if (p != null && p.FindText(t, (int)option)) {
					ctx.KeepToolBarAsync(true);
				}
			}, ctx => {
				return new CommandItem[] {
					new CommandItem("Find...", KnownImageIds.QuickFind, null, _ => TextEditorHelper.ExecuteEditorCommand("Edit.Find")),
					new CommandItem("Replace...", KnownImageIds.QuickReplace, null, _ => TextEditorHelper.ExecuteEditorCommand("Edit.Replace")),
					new CommandItem("Find in files...", KnownImageIds.FindInFile, null, _ => TextEditorHelper.ExecuteEditorCommand("Edit.FindinFiles")),
					new CommandItem("Replace in files...", KnownImageIds.ReplaceInFolder, null, _ => TextEditorHelper.ExecuteEditorCommand("Edit.ReplaceinFiles")),
				};
			});
		}
		void AddPasteCommand() {
			if (Clipboard.ContainsText()) {
				AddCommand(ToolBar, KnownImageIds.Paste, "Paste text from clipboard\nRight click: Paste over line\nCtrl click: Paste and select next", ctx => ExecuteAndFind(ctx, "Edit.Paste"));
			}
		}

		void AddSpecialDataFormatCommand() {
			switch (View.GetSelectedTokenType()) {
				case TokenType.None:
					AddEditorCommand(ToolBar, KnownImageIds.FormatSelection, "Edit.FormatSelection", "Format selected text\nRight click: Format document", "Edit.FormatDocument");
					break;
				case TokenType.Digit:
					AddCommand(ToolBar, KnownImageIds.Counter, "Increment number", ctx => {
						var span = ctx.View.Selection.SelectedSpans[0];
						var t = span.GetText();
						long l;
						if (long.TryParse(t, out l)) {
							using (var ed = ctx.View.TextBuffer.CreateEdit()) {
								t = (++l).ToString(System.Globalization.CultureInfo.InvariantCulture);
								if (ed.Replace(span.Span, t)) {
									ed.Apply();
									ctx.View.Selection.Select(new Microsoft.VisualStudio.Text.SnapshotSpan(ctx.View.TextSnapshot, span.Start, t.Length), false);
									ctx.KeepToolBarAsync(false);
								}
							}
						}
					});
					break;
				case TokenType.Guid:
				case TokenType.GuidPlaceHolder:
					AddCommand(ToolBar, KnownImageIds.NewNamedSet, "New GUID\nHint: To create a new GUID, type 'guid' (without quotes) and select it", ctx => {
						var span = ctx.View.Selection.SelectedSpans[0];
						using (var ed = ctx.View.TextBuffer.CreateEdit()) {
							var t = Guid.NewGuid().ToString(span.Length == 36 || span.Length == 4 ? "D" : span.GetText()[0] == '(' ? "P" : "B").ToUpperInvariant();
							if (ed.Replace(span, t)) {
								ed.Apply();
								ctx.View.Selection.Select(new Microsoft.VisualStudio.Text.SnapshotSpan(ctx.View.TextSnapshot, span.Start, t.Length), false);
								ctx.KeepToolBarAsync(false);
							}
						}
					});
					break;
			}
		}
	}
}
