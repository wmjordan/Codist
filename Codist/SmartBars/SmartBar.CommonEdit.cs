using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CLR;
using Codist.Controls;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using R = Codist.Properties.Resources;

namespace Codist.SmartBars
{
	partial class SmartBar
	{
		static readonly CommandItem[] __FindAndReplaceCommands = GetFindAndReplaceCommands();
		static readonly CommandItem[] __CaseCommands = GetCaseCommands();
		static readonly CommandItem[] __WebCommands = GetWebCommands();
		static readonly CommandItem[] __SurroundingCommands = GetSurroundingCommands();
		static readonly CommandItem[] __DebugCommands = GetDebugCommands();

		WrapText _RecentWrapText;

		static void ExecuteAndFind(CommandContext ctx, string command, string text, bool excludeLineBreakOnLineCommand) {
			if (ctx.RightClick) {
				ctx.View.ExpandSelectionToLine(excludeLineBreakOnLineCommand);
			}
			ctx.KeepToolBar(false);
			TextEditorHelper.ExecuteEditorCommand(command);
			if (Keyboard.Modifiers.HasAnyFlag(ModifierKeys.Control | ModifierKeys.Shift)
				&& FindNext(ctx, text) == false) {
				ctx.HideToolBar();
			}
		}

		protected static bool FindNext(CommandContext ctx, string t) {
			return ctx.View.FindNext(ctx.TextSearchService, t, TextEditorHelper.GetFindOptionsFromKeyboardModifiers());
		}

		protected static IEnumerable<SnapshotSpan> Replace(CommandContext ctx, Func<string, string> replaceHandler, bool selectModified) {
			ctx.KeepToolBar(false);
			string t = ctx.View.GetFirstSelectionText();
			if (t.Length == 0) {
				return null;
			}
			var edited = ctx.View.EditSelection((view, edit, item) => {
				var replacement = replaceHandler(item.GetText());
				if (replacement != null && edit.Replace(item, replacement)) {
					return new Span(item.Start, replacement.Length);
				}
				return null;
			});
			if (edited != null) {
				if (t != null && Keyboard.Modifiers.HasAnyFlag(ModifierKeys.Control | ModifierKeys.Shift) && FindNext(ctx, t) == false) {
					ctx.HideToolBar();
				}
				else if (selectModified) {
					ctx.View.SelectSpans(edited);
				}
			}
			return edited;
		}

		/// <summary>When selection is not surrounded with <paramref name="prefix"/> and <paramref name="suffix"/>, surround each span of the current selection with <paramref name="prefix"/> and <paramref name="suffix"/>, and optionally select the first modified span if <paramref name="selectModified"/> is <see langword="true"/>; when surrounded, remove them.</summary>
		/// <returns>The spans after modification. If modification is unsuccessful, <see langword="null"/> is returned.</returns>
		protected static IEnumerable<SnapshotSpan> WrapWith(CommandContext ctx, string prefix, string suffix, bool selectModified) {
			string s = ctx.View.GetFirstSelectionText();
			ctx.KeepToolBar(false);
			var m = ctx.View.WrapWith(prefix, suffix);
			if (s != null && Keyboard.Modifiers.HasAnyFlag(ModifierKeys.Control | ModifierKeys.Shift)
				&& FindNext(ctx, s) == false) {
				ctx.HideToolBar();
			}
			else if (selectModified) {
				ctx.View.SelectSpans(m);
			}
			return m;
		}
		protected static IEnumerable<SnapshotSpan> WrapWith(CommandContext ctx, WrapText wrapText, bool selectModified) {
			string s = ctx.View.GetFirstSelectionText();
			ctx.KeepToolBar(false);
			var m = ctx.View.WrapWith(wrapText);
			if (s != null && Keyboard.Modifiers.HasAnyFlag(ModifierKeys.Control | ModifierKeys.Shift)
				&& FindNext(ctx, s) == false) {
				ctx.HideToolBar();
			}
			else if (selectModified) {
				ctx.View.SelectSpans(m);
			}
			return m;
		}

		void AddCopyCommand() {
			AddCommand(ToolBar, IconIds.Copy, R.CMD_CopySelectedText, ctx => {
				if (ctx.RightClick) {
					ctx.View.ExpandSelectionToLine();
				}

				if (Keyboard.Modifiers.MatchFlags(ModifierKeys.Control)) {
					ctx.View.CopySelectionWithoutIndentation();
				}
				else {
					TextEditorHelper.ExecuteEditorCommand("Edit.Copy");
				}
			});
		}

		void AddCutCommand() {
			AddCommand(ToolBar, IconIds.Cut, R.CMD_CutSelectedText, ctx => {
				if (ctx.RightClick) {
					ctx.View.ExpandSelectionToLine();
				}

				if (Keyboard.Modifiers.MatchFlags(ModifierKeys.Control)) {
					ctx.View.CopySelectionWithoutIndentation();
					TextEditorHelper.ExecuteEditorCommand("Edit.Delete");
				}
				else {
					TextEditorHelper.ExecuteEditorCommand("Edit.Cut");
				}
			});
		}

		void AddDeleteCommand() {
			AddCommand(ToolBar, IconIds.Delete, R.CMD_DeleteSelectedText, ctx => {
				const string REMOVE_SPACE_BEFORE = "={<>(-+*/^!|?:~&%";
				var s = ctx.View.Selection;
				var t = s.GetFirstSelectionText();
				if (s.Mode == TextSelectionMode.Stream
					&& ctx.RightClick == false
					&& s.IsEmpty == false) {
					var end = s.End.Position;
					#region remove a trailing space
					var snapshot = ctx.View.TextSnapshot;
					if (end < snapshot.Length - 1
						&& Char.IsLetterOrDigit(snapshot[end - 1])
						&& snapshot[end] == ' '
						&& (end == snapshot.Length - 2
							|| REMOVE_SPACE_BEFORE.IndexOf(snapshot[end.Position + 1]) == -1)) {
						ctx.KeepToolBar(false);
						s.Select(new SnapshotSpan(s.Start.Position, s.End.Position + 1), false);
					}
					#endregion
				}
				ExecuteAndFind(ctx, "Edit.Delete", t, false);
			});
		}

		void AddDuplicateCommand() {
			AddCommand(ToolBar, IconIds.Duplicate, R.CMD_DuplicateSelection, ctx => {
				if (ctx.RightClick) {
					ctx.View.ExpandSelectionToLine();
				}
				ctx.KeepToolBar(true);
				TextEditorHelper.ExecuteEditorCommand("Edit.Duplicate");
			});
		}

		void AddEditAllMatchingCommand() {
			AddCommand(ToolBar, IconIds.EditMatches, R.CMD_EditMatches, ctx => TextEditorHelper.ExecuteEditorCommand("Edit.InsertCaretsatAllMatching"));
		}

		void AddDiffCommands() {
			AddEditorCommand(ToolBar2, IconIds.StageSelectedRange, "Team.Git.StageSelectedRange", R.CMD_StageSelectedRange);
			AddEditorCommand(ToolBar2, IconIds.RevertSelectedRange, "Team.Git.RevertSelectedRange", R.CMD_RevertSelectedRange);
		}

		void AddFindAndReplaceCommands() {
			AddCommands(ToolBar, IconIds.FindNext, R.CMD_FindReplace, QuickFind, ctx => __FindAndReplaceCommands.Concat(
				Config.Instance.SearchEngines.ConvertAll(s => new CommandItem(IconIds.SearchWebSite, R.CMD_SearchWith.Replace("<NAME>", s.Name), c => SearchSelection(s.Pattern, c))))
			);
		}

		void QuickFind(CommandContext ctx) {
			ThreadHelper.ThrowIfNotOnUIThread();
			string t = ctx.View.GetFirstSelectionText();
			if (t.Length == 0) {
				return;
			}
			ctx.KeepToolBar(false);
			if (Keyboard.Modifiers == ModifierKeys.Alt) {
				TextEditorHelper.ExecuteEditorCommand("Edit.InsertNextMatchingCaret");
				return;
			}
			var r = ctx.TextSearchService.Find(ctx.View.Selection.StreamSelectionSpan.End.Position, t,
				FindOptions.Wrap
					.SetFlags(FindOptions.MatchCase, Keyboard.Modifiers.MatchFlags(ModifierKeys.Control))
					.SetFlags(FindOptions.WholeWord, Keyboard.Modifiers.MatchFlags(ModifierKeys.Shift))
					.SetFlags(FindOptions.Multiline, t.IndexOf('\n') >= 0)
				);
			if (r.HasValue) {
				ctx.View.SelectSpan(r.Value);
				ctx.KeepToolBar(true);
			}
			else {
				ctx.HideToolBar();
			}
		}

		void AddPasteCommand() {
			if (Clipboard.ContainsText()) {
				AddCommand(ToolBar, IconIds.Paste, R.CMD_Paste, ctx => ExecuteAndFind(ctx, "Edit.Paste", ctx.View.GetFirstSelectionText(), true));
			}
		}

		void AddSpecialFormatCommand() {
			if (View.Selection.IsEmpty) {
				return;
			}
			var tokenType = View.GetSelectedTokenType();
			switch (tokenType) {
				case TokenType.None:
					AddCommands(ToolBar, IconIds.FormatSelection, R.CMD_Formatting, null, GetFormatItems);
					break;
				case TokenType.Digit:
				case TokenType.Digit | TokenType.Hex:
				case TokenType.Digit | TokenType.Hex | TokenType.ZeroXHex:
				case TokenType.Digit | TokenType.Hex | TokenType.Letter:
				case TokenType.Digit | TokenType.Hex | TokenType.ZeroXHex | TokenType.Letter:
					AddCommand(ToolBar, IconIds.IncrementNumber, R.CMD_IncrementNumber, ctx => {
						ctx.KeepToolBar(false);
						var m = ctx.View.EditSelection((v, edit, s) => {
							var t = s.GetText();
							var hex = tokenType.MatchFlags(TokenType.Hex);
							if (long.TryParse(
								hex ? t.Substring(2) : t,
								hex ? NumberStyles.HexNumber : NumberStyles.Integer,
								CultureInfo.InvariantCulture,
								out var value)
								) {
								if (hex) {
									t = tokenType.MatchFlags(TokenType.ZeroXHex)
										? t.Substring(0, 2) + (++value).ToString("X" + (t.Length - 2).ToString(CultureInfo.InvariantCulture))
										: (++value).ToString("X" + t.Length.ToString(CultureInfo.InvariantCulture));
								}
								else {
									t = (++value).ToString("D" + t.Length.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
								}
								if (edit.Replace(s.Span, t)) {
									return new Span(s.Start, t.Length);
								}
							}
							return null;
						});
						if (m != null) {
							ctx.View.SelectSpans(m);
						}
					});
					break;
				case TokenType.Guid:
				case TokenType.GuidPlaceHolder:
					AddCommand(ToolBar, IconIds.NewGuid, R.CMD_GUID, ctx => {
						ctx.KeepToolBar(false);
						var m = ctx.View.EditSelection((v, ed, s) => {
							var t = Guid.NewGuid().ToString(s.Length == 36 || s.Length == 4 ? "D" : s.GetText()[0] == '(' ? "P" : "B").ToUpperInvariant();
							if (ed.Replace(s, t)) {
								return new Span(s.Start, t.Length);
							}
							return null;
						});
						if (m != null) {
							ctx.View.SelectSpans(m);
						}
					});
					AddCommands(ToolBar, IconIds.FormatSelection, R.CMD_Formatting, null, _ => __CaseCommands);
					break;
				default:
					AddCommands(ToolBar, IconIds.FormatSelection, R.CMD_Formatting, null, _ => __CaseCommands);
					break;
			}
		}

		void AddWrapTextCommand() {
			AddCommands(ToolBar, IconIds.WrapText, R.CMD_WrapText, WrapRecent, CreateWrapTextMenu);
		}

		void WrapRecent(CommandContext ctx) {
			if (_RecentWrapText == null) {
				if (Config.Instance.WrapTexts.Count == 0) {
					WrapWith(ctx, "(", ")", true);
				}
				_RecentWrapText = Config.Instance.WrapTexts[0];
			}
			WrapWith(ctx, _RecentWrapText, true);
		}
		IEnumerable<CommandItem> CreateWrapTextMenu(CommandContext context) {
			foreach (var item in Config.Instance.WrapTexts) {
				yield return new CommandItem(IconIds.WrapText, String.IsNullOrEmpty(item.Name) ? item.Pattern : item.Name, ctx => {
					if (WrapWith(ctx, item, true) != Enumerable.Empty<SnapshotSpan>()) {
						_RecentWrapText = item;
					}
				});
			}
			yield return new CommandItem(IconIds.WrapText, R.CMD_Customize, ctx => CodistPackage.Instance.ShowOptionPage(typeof(Options.WrapTextPage)));
		}

		void AddClassificationInfoCommand() {
			AddCommand(ToolBar, IconIds.ShowClassificationInfo, R.CMD_ShowSyntaxClassificationInfo, ctx => {
				if (ctx.View.TryGetFirstSelectionSpan(out var span) == false) {
					return;
				}
				var cs = ServicesHelper.Instance.ClassifierAggregator.GetClassifier(ctx.View.TextBuffer)
					.GetClassificationSpans(span);
				Taggers.TaggerResult.IsLocked = true;
				var t = ServicesHelper.Instance.ViewTagAggregatorFactory.CreateTagAggregator<Microsoft.VisualStudio.Text.Tagging.IClassificationTag>(ctx.View).GetTags(span);
				using (var b = ReusableStringBuilder.AcquireDefault(100)) {
					var sb = b.Resource;
					sb.AppendLine(R.T_ClassificationForSelectedContent);
					foreach (var s in cs) {
						sb.Append(s.Span.Span.ToString())
							.Append(' ')
							.Append(s.ClassificationType.Classification)
							.Append(':')
							.Append(' ')
							.AppendLine(s.Span.GetText());
					}
					sb.AppendLine().AppendLine(R.T_ClassificationTags);
					foreach (var item in t) {
						sb.Append(item.Span.GetSpans(ctx.View.TextBuffer).ToString())
							.Append(' ')
							.AppendLine(item.Tag.ClassificationType.Classification);
					}
					MessageWindow.Show(sb.ToString(), R.T_SyntaxClassificationInfo);
				}
				Taggers.TaggerResult.IsLocked = false;
			});
		}

		List<CommandItem> GetFormatItems(CommandContext arg) {
			var r = new List<CommandItem>(10);
			var selection = View.Selection;
			if (selection.Mode == TextSelectionMode.Stream) {
				r.AddRange(__SurroundingCommands);
				r.Add(new CommandItem(IconIds.FormatSelection, R.CMD_FormatSelection, _ => TextEditorHelper.ExecuteEditorCommand("Edit.FormatSelection")));
				if (View.IsMultilineSelected()) {
					r.Add(new CommandItem(IconIds.JoinLines, R.CMD_JoinLines, ctx => ctx.View.JoinSelectedLines()));
				}
			}
			r.AddRange(__WebCommands);
			r.AddRange(__CaseCommands);
			return r;
		}

		void AddViewInBrowserCommand() {
			var s = View.Selection;
			SnapshotSpan span;
			int l;
			if (s.IsEmpty || (l = (span = s.SelectedSpans[0]).Length) < 10 || l > 1023) {
				return;
			}
			var ts = View.TextSnapshot;
			if (ts[l = span.Start] != 'h' || ts[++l] != 't' || ts[++l] != 't' || ts[++l] != 'p') {
				return;
			}
			if (ts[++l] == 's') {
				if (ts[++l] != ':' || ts[++l] != '/' || ts[++l] != '/') {
					return;
				}
			}
			else if (ts[l] == ':') {
				if (ts[++l] != '/' || ts[++l] != '/') {
					return;
				}
			}
			else {
				return;
			}
			AddCommand(ToolBar, IconIds.SearchWebSite, $"{R.CMD_ViewUrlInBrowser}{Environment.NewLine}{span.GetText()}", ctx => ExternalCommand.OpenWithWebBrowser(ctx.View.GetFirstSelectionText(), String.Empty));
		}

		void AddDebuggerCommands() {
			if (CodistPackage.DebuggerStatus != DebuggerStatus.Design) {
				AddCommand(ToolBar2, IconIds.RunToCursor, R.CMD_RunToCursor, ctx => {
					TextEditorHelper.ExecuteEditorCommand(
						Keyboard.Modifiers.MatchFlags(ModifierKeys.Control) ? "Debug.RunFlaggedThreadsToCursor"
							: ctx.RightClick ? "Debug.Threads"
							: "Debug.RunToCursor");
				});
				AddEditorCommand(ToolBar2, IconIds.SetNextStatement, "Debug.SetNextStatement", R.CMD_SetNextStatement);
				AddCommands(ToolBar2, IconIds.ToggleBreakpoint, R.CMD_Debugger, ctx => TextEditorHelper.ExecuteEditorCommand("Debug.ToggleBreakpoint"), ctx => __DebugCommands);
			}
		}

		static CommandItem[] GetCaseCommands() {
			return new CommandItem[] {
				new CommandItem(IconIds.Capitalize, R.CMD_Capitalize, ctx => {
					ctx.KeepToolBarOnClick = true;
					TextEditorHelper.ExecuteEditorCommand("Edit.Capitalize");
				}),
				new CommandItem(IconIds.Uppercase, R.CMD_Uppercase, ctx => {
					ctx.KeepToolBarOnClick = true;
					TextEditorHelper.ExecuteEditorCommand("Edit.MakeUppercase");
				}),
				new CommandItem(IconIds.None, R.CMD_Lowercase, ctx => {
					ctx.KeepToolBarOnClick = true;
					TextEditorHelper.ExecuteEditorCommand("Edit.MakeLowercase");
				}),
			};
		}
		static CommandItem[] GetWebCommands() {
			return new CommandItem[] {
				new CommandItem(IconIds.UrlEncode, R.CMD_UrlEncode, ctx => {
					ctx.KeepToolBarOnClick = true;
					Replace(ctx, System.Net.WebUtility.UrlEncode, true);
				}),
				new CommandItem(IconIds.UrlEncode, R.CMD_UrlDecode, ctx => {
					ctx.KeepToolBarOnClick = true;
					Replace(ctx, System.Net.WebUtility.UrlDecode, true);
				}),
				new CommandItem(IconIds.HtmlEncode, R.CMD_HtmlEncode, ctx => {
					ctx.KeepToolBarOnClick = true;
					Replace(ctx, System.Net.WebUtility.HtmlEncode, true);
				}),
				new CommandItem(IconIds.HtmlEncode, R.CMD_HtmlDecode, ctx => {
					ctx.KeepToolBarOnClick = true;
					Replace(ctx, System.Net.WebUtility.HtmlDecode, true);
				}),
			};
		}
		static CommandItem[] GetDebugCommands() {
			return new CommandItem[] {
				new CommandItem(IconIds.Watch, R.CMD_AddWatch, c => TextEditorHelper.ExecuteEditorCommand("Debug.AddWatch")),
				new CommandItem(IconIds.Watch, R.CMD_AddParallelWatch, c => TextEditorHelper.ExecuteEditorCommand("Debug.AddParallelWatch")),
				new CommandItem(IconIds.DeleteBreakpoint, R.CMD_DeleteAllBreakpoints, c => TextEditorHelper.ExecuteEditorCommand("Debug.DeleteAllBreakpoints")),
			};
		}
		static CommandItem[] GetFindAndReplaceCommands() {
			return new CommandItem[] {
				new CommandItem(IconIds.Find, R.CMD_Find, _ => TextEditorHelper.ExecuteEditorCommand("Edit.Find")),
				new CommandItem(IconIds.Replace, R.CMD_Replace, _ => TextEditorHelper.ExecuteEditorCommand("Edit.Replace")),
				new CommandItem(IconIds.FindInFile, R.CMD_FindInFiles, _ => TextEditorHelper.ExecuteEditorCommand("Edit.FindinFiles")),
				new CommandItem(IconIds.ReplaceInFolder, R.CMD_ReplaceInFiles, _ => TextEditorHelper.ExecuteEditorCommand("Edit.ReplaceinFiles")),
			};
		}
		static CommandItem[] GetSurroundingCommands() {
			return new CommandItem[] {
				new CommandItem(IconIds.SurroundWith, R.CMD_SurroundWith, ctx => TextEditorHelper.ExecuteEditorCommand("Edit.SurroundWith")),
				new CommandItem(IconIds.ToggleParentheses, R.CMD_ToggleParentheses, ctx => {
					if (ctx.View.TryGetFirstSelectionSpan(out var span)) {
						WrapWith(ctx, "(", ")", true);
					}
				}),
			};
		}
		static void SearchSelection(string url, CommandContext ctx) {
			ExternalCommand.OpenWithWebBrowser(url, ctx.View.GetFirstSelectionText());
		}
	}
}
