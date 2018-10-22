using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
		static readonly CommandItem[] __FindAndReplaceCommands = GetFindAndReplaceCommands();
		static readonly CommandItem[] __CaseCommands = GetCaseCommands();
		static readonly CommandItem[] __SurroundingCommands = GetSurroundingCommands();
		static readonly CommandItem[] __FormatCommands = GetFormatCommands();

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
				if (p != null && p.FindText(t, (int)EnvDTE.vsFindOptions.vsFindOptionsMatchCase)) {
					ctx.KeepToolBar(true);
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
				ctx.KeepToolBar(true);
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
					ctx.KeepToolBar(true);
				}
			}, ctx => __FindAndReplaceCommands);
		}

		void AddPasteCommand() {
			if (Clipboard.ContainsText()) {
				AddCommand(ToolBar, KnownImageIds.Paste, "Paste text from clipboard\nRight click: Paste over line\nCtrl click: Paste and select next", ctx => ExecuteAndFind(ctx, "Edit.Paste"));
			}
		}

		void AddSpecialFormatCommand() {
			switch (View.GetSelectedTokenType()) {
				case TokenType.None:
					AddCommands(ToolBar, KnownImageIds.FormatSelection, "Format selection...", null, GetFormatItems);
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
									ctx.View.Selection.Select(new SnapshotSpan(ctx.View.TextSnapshot, span.Start, t.Length), false);
									ctx.KeepToolBar(false);
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
								ctx.View.Selection.Select(new SnapshotSpan(ctx.View.TextSnapshot, span.Start, t.Length), false);
								ctx.KeepToolBar(false);
							}
						}
					});
					AddCommands(ToolBar, KnownImageIds.FormatSelection, "Formatting...", null, _ => __CaseCommands);
					break;
				default:
					AddCommands(ToolBar, KnownImageIds.FormatSelection, "Formatting...", null, _ => __CaseCommands);
					break;
			}
		}

		List<CommandItem> GetFormatItems(CommandContext arg) {
			var r = new List<CommandItem>(10);
			var selection = View.Selection;
			if (selection.Mode == TextSelectionMode.Stream) {
				r.AddRange(__SurroundingCommands);
				r.AddRange(__FormatCommands);
				if (View.IsMultilineSelected()) {
					r.Add(new CommandItem(KnownImageIds.Join, "Join lines", _ => {
						var span = View.Selection.SelectedSpans[0];
						View.TextBuffer.Replace(span, System.Text.RegularExpressions.Regex.Replace(span.GetText(), @"[ \t]*\r?\n[ \t]*", " "));
					}));
				}
				var t = View.TextViewLines.GetTextViewLineContainingBufferPosition(selection.Start.Position).Extent.GetText();
				if (t.Length > 0 && (t[0] == ' ' || t[0] == '\t')) {
					r.Add(new CommandItem(KnownImageIds.DecreaseIndent, "Unindent", ctx => {
						ctx.KeepToolBarOnClick = true;
						TextEditorHelper.ExecuteEditorCommand("Edit.DecreaseLineIndent");
					}));
				}
				r.Add(new CommandItem(KnownImageIds.IncreaseIndent, "Indent", ctx => {
					ctx.KeepToolBarOnClick = true;
					TextEditorHelper.ExecuteEditorCommand("Edit.IncreaseLineIndent");
				}));
			}
			r.AddRange(__CaseCommands);
			return r;
		}


		static CommandItem[] GetCaseCommands() {
			return new CommandItem[] {
				new CommandItem(KnownImageIds.Font, "Capitalize", ctx => {
					ctx.KeepToolBarOnClick = true;
					TextEditorHelper.ExecuteEditorCommand("Edit.Capitalize");
				}),
				new CommandItem(KnownImageIds.ASerif, "Uppercase", ctx => {
					ctx.KeepToolBarOnClick = true;
					TextEditorHelper.ExecuteEditorCommand("Edit.MakeUppercase");
				}),
				new CommandItem(KnownImageIds.Blank, "Lowercase", ctx => {
					ctx.KeepToolBarOnClick = true;
					TextEditorHelper.ExecuteEditorCommand("Edit.MakeLowercase");
				}),
			};
		}
		static CommandItem[] GetFormatCommands() {
			return new CommandItem[] {
				new CommandItem(KnownImageIds.FormatSelection, "Format selection", _ => TextEditorHelper.ExecuteEditorCommand("Edit.FormatSelection")),
				new CommandItem(KnownImageIds.FormatDocument, "Format document", _ => TextEditorHelper.ExecuteEditorCommand("Edit.FormatDocument")),
			};
		}
		static CommandItem[] GetFindAndReplaceCommands() {
			return new CommandItem[] {
					new CommandItem(KnownImageIds.QuickFind, "Find...", _ => TextEditorHelper.ExecuteEditorCommand("Edit.Find")),
					new CommandItem(KnownImageIds.QuickReplace, "Replace...", _ => TextEditorHelper.ExecuteEditorCommand("Edit.Replace")),
					new CommandItem(KnownImageIds.FindInFile, "Find in files...", _ => TextEditorHelper.ExecuteEditorCommand("Edit.FindinFiles")),
					new CommandItem(KnownImageIds.ReplaceInFolder, "Replace in files...", _ => TextEditorHelper.ExecuteEditorCommand("Edit.ReplaceinFiles")),
				};
		}
		static CommandItem[] GetSurroundingCommands() {
			return new CommandItem[] {
				new CommandItem(KnownImageIds.AddSnippet, "Surround with...", ctx => {
					TextEditorHelper.ExecuteEditorCommand("Edit.SurroundWith");
				}),
				new CommandItem(KnownImageIds.MaskedTextBox, "Toggle parentheses", ctx => {
					var span = ctx.View.Selection.SelectedSpans[0];
					using (var ed = ctx.View.TextBuffer.CreateEdit()) {
						var t = span.GetText();
						if (t.Length > 1 && t[0] == '(' && t[t.Length - 1] == ')') {
							t = t.Substring(1, t.Length - 2);
						}
						else {
							t = "(" + t + ")";
						}
						if (ed.Replace(span, t)) {
							ed.Apply();
							ctx.View.Selection.Select(new SnapshotSpan(ctx.View.TextSnapshot, span.Start, t.Length), false);
							ctx.KeepToolBar(false);
						}
					}
				}),
			};
		}
	}
}
