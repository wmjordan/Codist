using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AppHelpers;
using Microsoft.VisualStudio.Imaging;
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
		static readonly CommandItem[] __SurroundingCommands = GetSurroundingCommands();
		static readonly CommandItem[] __DebugCommands = GetDebugCommands();

		protected static IEnumerable<CommandItem> DebugCommands => __DebugCommands;

		static void ExecuteAndFind(CommandContext ctx, string command, string text) {
			if (ctx.RightClick) {
				ctx.View.ExpandSelectionToLine(false);
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

		protected static SnapshotSpan Replace(CommandContext ctx, Func<string, string> replaceHandler, bool selectModified) {
			ctx.KeepToolBar(false);
			var firstModified = new SnapshotSpan();
			string t = ctx.View.GetFirstSelectionText();
			if (t.Length == 0) {
				return firstModified;
			}
			var edited = ctx.View.EditSelection((view, edit, item) => {
				var replacement = replaceHandler(item.GetText());
				if (replacement != null && edit.Replace(item, replacement)) {
					return new Span(item.Start, replacement.Length);
				}
				return null;
			});
			if (edited != null) {
				firstModified = edited.Value;
				if (t != null && Keyboard.Modifiers.HasAnyFlag(ModifierKeys.Control | ModifierKeys.Shift) && FindNext(ctx, t) == false) {
					ctx.HideToolBar();
				}
				else if (selectModified) {
					ctx.View.SelectSpan(firstModified);
				}
			}
			return firstModified;
		}

		/// <summary>When selection is not surrounded with <paramref name="prefix"/> and <paramref name="suffix"/>, surround each span of the corrent selection with <paramref name="prefix"/> and <paramref name="suffix"/>, and optionally select the first modified span if <paramref name="selectModified"/> is <see langword="true"/>; when surrounded, remove them.</summary>
		/// <returns>The new span after modification. If modification is unsuccessful, the default of <see cref="SnapshotSpan"/> is returned.</returns>
		protected static SnapshotSpan WrapWith(CommandContext ctx, string prefix, string suffix, bool selectModified) {
			string s = ctx.View.GetFirstSelectionText();
			ctx.KeepToolBar(false);
			var firstModified = ctx.View.WrapWith(prefix, suffix);
			if (s != null && Keyboard.Modifiers.HasAnyFlag(ModifierKeys.Control | ModifierKeys.Shift)
				&& FindNext(ctx, s) == false) {
				ctx.HideToolBar();
			}
			else if (selectModified) {
				ctx.View.SelectSpan(firstModified);
			}
			return firstModified;
		}

		void AddCopyCommand() {
			AddCommand(ToolBar, IconIds.Copy, R.CMD_CopySelectedText, ctx => {
				if (ctx.RightClick) {
					ctx.View.ExpandSelectionToLine();
				}
				TextEditorHelper.ExecuteEditorCommand("Edit.Copy");
			});
		}

		void AddCutCommand() {
			AddCommand(ToolBar, IconIds.Cut, R.CMD_CutSelectedText, ctx => {
				if (ctx.RightClick) {
					ctx.View.ExpandSelectionToLine();
				}
				TextEditorHelper.ExecuteEditorCommand("Edit.Cut");
			});
		}

		void AddDeleteCommand() {
			AddCommand(ToolBar, IconIds.Delete, R.CMD_DeleteSelectedText, ctx => {
				var s = ctx.View.Selection;
				var t = s.GetFirstSelectionText();
				if (s.Mode == TextSelectionMode.Stream
					&& ctx.RightClick == false
					&& s.IsEmpty == false) {
					var end = s.End.Position;
					// remove a trailing space
					var snapshot = ctx.View.TextSnapshot;
					if (end < snapshot.Length - 1
						&& Char.IsLetterOrDigit(snapshot[end - 1])
						&& snapshot[end] == ' '
						&& (end == snapshot.Length - 2
							|| "={<>(-+*/^!|?:~&%".IndexOf(snapshot[end + 1]) == -1)) {
						s.Select(new SnapshotSpan(s.Start.Position, s.End.Position + 1), false);
					}
				}
				ExecuteAndFind(ctx, "Edit.Delete", t);
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

		void AddFindAndReplaceCommands() {
			AddCommands(ToolBar, IconIds.FindNext, R.CMD_FindReplace, ctx => {
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
					Keyboard.Modifiers == ModifierKeys.Control ? FindOptions.MatchCase | FindOptions.Wrap
					: Keyboard.Modifiers == ModifierKeys.Shift ? FindOptions.Wrap | FindOptions.WholeWord
					: FindOptions.None);
				if (r.HasValue) {
					ctx.View.SelectSpan(r.Value);
					ctx.KeepToolBar(true);
				}
				else {
					ctx.HideToolBar();
				}
			}, ctx => __FindAndReplaceCommands.Concat(
				Config.Instance.SearchEngines.ConvertAll(s => new CommandItem(IconIds.SearchWebSite, R.CMD_SearchWith.Replace("<NAME>", s.Name), c => SearchSelection(s.Pattern, c))))
			);
		}

		void AddPasteCommand() {
			if (Clipboard.ContainsText()) {
				AddCommand(ToolBar, IconIds.Paste, R.CMD_Paste, ctx => ExecuteAndFind(ctx, "Edit.Paste", ctx.View.GetFirstSelectionText()));
			}
		}

		void AddSpecialFormatCommand() {
			switch (View.GetSelectedTokenType()) {
				case TokenType.None:
					AddCommands(ToolBar, IconIds.FormatSelection, R.CMD_Formatting, null, GetFormatItems);
					break;
				case TokenType.Digit:
					AddCommand(ToolBar, IconIds.IncrementNumber, R.CMD_IncrementNumber, ctx => {
						if (ctx.View.TryGetFirstSelectionSpan(out var span)) {
							ctx.KeepToolBar(false);
							var u = ctx.View.EditSelection((v, edit, s) => {
								if (long.TryParse(s.GetText(), out var value)) {
									var t = (++value).ToString(System.Globalization.CultureInfo.InvariantCulture);
									if (edit.Replace(s.Span, t)) {
										return new Span(s.Start, t.Length);
									}
								}
								return null;
							});
							if (u.HasValue) {
								ctx.View.SelectSpan(u.Value);
							}
						}
					});
					break;
				case TokenType.Guid:
				case TokenType.GuidPlaceHolder:
					AddCommand(ToolBar, IconIds.NewGuid, R.CMD_GUID, ctx => {
						if (ctx.View.TryGetFirstSelectionSpan(out var span)) {
							using (var ed = ctx.View.TextBuffer.CreateEdit()) {
								var t = Guid.NewGuid().ToString(span.Length == 36 || span.Length == 4 ? "D" : span.GetText()[0] == '(' ? "P" : "B").ToUpperInvariant();
								if (ed.Replace(span, t)) {
									ed.Apply();
									ctx.View.Selection.Select(new SnapshotSpan(ctx.View.TextSnapshot, span.Start, t.Length), false);
									ctx.KeepToolBar(false);
								}
							}
						}
					});
					AddCommands(ToolBar, IconIds.FormatSelection, R.CMD_Formatting, null, _ => __CaseCommands);
					break;
				default:
					AddCommands(ToolBar, IconIds.FormatSelection, R.CMD_Formatting, null, _ => __CaseCommands);
					break;
			}
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
					sb.AppendLine($"Classifications for selected {span.Length} characters:");
					foreach (var s in cs) {
						sb.Append(s.Span.Span.ToString())
							.Append(' ')
							.Append(s.ClassificationType.Classification)
							.Append(':')
							.Append(' ')
							.Append(s.Span.GetText())
							.AppendLine();
					}
					sb.AppendLine().AppendLine("Tags:");
					foreach (var item in t) {
						sb.Append(item.Span.GetSpans(ctx.View.TextBuffer).ToString())
							.Append(' ')
							.AppendLine(item.Tag.ClassificationType.Classification);
					}
					System.Windows.Forms.MessageBox.Show(sb.ToString(), nameof(Codist));
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
				var t = View.GetTextViewLineContainingBufferPosition(selection.Start.Position).Extent.GetText();
				if (t.Length > 0 && (t[0] == ' ' || t[0] == '\t')) {
					r.Add(new CommandItem(IconIds.Unindent, R.CMD_Unindent, ctx => {
						ctx.KeepToolBarOnClick = true;
						TextEditorHelper.ExecuteEditorCommand("Edit.DecreaseLineIndent");
					}));
				}
				r.Add(new CommandItem(IconIds.Indent, R.CMD_Indent, ctx => {
					ctx.KeepToolBarOnClick = true;
					TextEditorHelper.ExecuteEditorCommand("Edit.IncreaseLineIndent");
				}));
			}
			r.AddRange(__CaseCommands);
			return r;
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
		static CommandItem[] GetDebugCommands() {
			return new CommandItem[] {
				new CommandItem(IconIds.Watch, R.CMD_AddWatch, c => TextEditorHelper.ExecuteEditorCommand("Debug.AddWatch")),
				new CommandItem(IconIds.Watch, R.CMD_AddParallelWatch, c => TextEditorHelper.ExecuteEditorCommand("Debug.AddParallelWatch")),
				new CommandItem(IconIds.DeleteBreakpoint, R.CMD_DeleteAllBreakpoints, c => TextEditorHelper.ExecuteEditorCommand("Debug.DeleteAllBreakpoints"))
			};
		}
		static CommandItem[] GetFindAndReplaceCommands() {
			return new CommandItem[] {
					new CommandItem(IconIds.Find, R.CMD_Find, _ => TextEditorHelper.ExecuteEditorCommand("Edit.Find")),
					new CommandItem(IconIds.Replace, R.CMD_Replace, _ => TextEditorHelper.ExecuteEditorCommand("Edit.Replace")),
					new CommandItem(IconIds.FindInFile, R.CMD_FindInFiles, _ => TextEditorHelper.ExecuteEditorCommand("Edit.FindinFiles")),
					new CommandItem(IconIds.ReplaceInFolder, R.CMD_ReplaceInFiles, _ => TextEditorHelper.ExecuteEditorCommand("Edit.ReplaceinFiles")),
					new CommandItem(IconIds.EditMatches, R.CMD_EditMatches, ctx => TextEditorHelper.ExecuteEditorCommand("Edit.InsertCaretsatAllMatching")),
				};
		}
		static CommandItem[] GetSurroundingCommands() {
			return new CommandItem[] {
				new CommandItem(IconIds.SurroundWith, R.CMD_SurroundWith, ctx => {
					TextEditorHelper.ExecuteEditorCommand("Edit.SurroundWith");
				}),
				new CommandItem(IconIds.ToggleParentheses, R.CMD_ToggleParentheses, ctx => {
					if (ctx.View.TryGetFirstSelectionSpan(out var span)) {
						WrapWith(ctx, "(", ")", true);
					}
				}),
			};
		}
		static void SearchSelection(string url, CommandContext ctx) {
			Controls.ExternalCommand.OpenWithWebBrowser(url, ctx.View.GetFirstSelectionText());
		}
	}
}
