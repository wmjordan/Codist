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

namespace Codist.SmartBars
{
	partial class SmartBar
	{
		static readonly CommandItem[] __FindAndReplaceCommands = GetFindAndReplaceCommands();
		static readonly CommandItem[] __CaseCommands = GetCaseCommands();
		static readonly CommandItem[] __SurroundingCommands = GetSurroundingCommands();
		static readonly CommandItem[] __FormatCommands = GetFormatCommands();
		static readonly CommandItem[] __DebugCommands = GetDebugCommands();

		protected static IEnumerable<CommandItem> DebugCommands => __DebugCommands;

		static void ExecuteAndFind(CommandContext ctx, string command, string text) {
			if (ctx.RightClick) {
				ctx.View.ExpandSelectionToLine(false);
			}
			ctx.KeepToolBar(false);
			TextEditorHelper.ExecuteEditorCommand(command);
			if (Keyboard.Modifiers == ModifierKeys.Control && FindNext(ctx, text) == false) {
				ctx.HideToolBar();
			}
		}

		protected static bool FindNext(CommandContext ctx, string t) {
			return ctx.View.FindNext(ctx.TextSearchService, t);
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
				if (t != null && Keyboard.Modifiers == ModifierKeys.Control && FindNext(ctx, t) == false) {
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
			if (s != null && Keyboard.Modifiers == ModifierKeys.Control && FindNext(ctx, s) == false) {
				ctx.HideToolBar();
			}
			else if (selectModified) {
				ctx.View.SelectSpan(firstModified);
			}
			return firstModified;
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
			AddCommand(ToolBar, KnownImageIds.CopyItem, "Duplicate selection\nRight click: Duplicate line", ctx => {
				if (ctx.RightClick) {
					ctx.View.ExpandSelectionToLine();
				}
				ctx.KeepToolBar(true);
				TextEditorHelper.ExecuteEditorCommand("Edit.Duplicate");
			});
		}

		void AddFindAndReplaceCommands() {
			AddCommands(ToolBar, KnownImageIds.FindNext, "Find next selected text\nCtrl click: Find match case\nRight click: Find and replace...", ctx => {
				ThreadHelper.ThrowIfNotOnUIThread();
				string t = ctx.View.GetFirstSelectionText();
				if (t.Length == 0) {
					return;
				}
				ctx.KeepToolBar(false);
				var r = ctx.TextSearchService.Find(ctx.View.Selection.StreamSelectionSpan.End.Position, t, Keyboard.Modifiers.MatchFlags(ModifierKeys.Control) ? FindOptions.MatchCase : FindOptions.None);
				if (r.HasValue) {
					ctx.View.SelectSpan(r.Value);
					ctx.KeepToolBar(true);
				}
				else {
					ctx.HideToolBar();
				}
			}, ctx => __FindAndReplaceCommands.Concat(Config.Instance.SearchEngines.ConvertAll(s => new CommandItem(KnownImageIds.OpenWebSite, "Search with " + s.Name, c => SearchSelection(s.Pattern, c)))));
		}

		void AddPasteCommand() {
			if (Clipboard.ContainsText()) {
				AddCommand(ToolBar, KnownImageIds.Paste, "Paste text from clipboard\nRight click: Paste over line\nCtrl click: Paste and select next", ctx => ExecuteAndFind(ctx, "Edit.Paste", ctx.View.GetFirstSelectionText()));
			}
		}

		void AddSpecialFormatCommand() {
			switch (View.GetSelectedTokenType()) {
				case TokenType.None:
					AddCommands(ToolBar, KnownImageIds.FormatSelection, "Format selection...", null, GetFormatItems);
					break;
				case TokenType.Digit:
					AddCommand(ToolBar, KnownImageIds.Counter, "Increment number", ctx => {
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
					AddCommand(ToolBar, KnownImageIds.NewNamedSet, "New GUID\nHint: To create a new GUID, type 'guid' (without quotes) and select it", ctx => {
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
					AddCommands(ToolBar, KnownImageIds.FormatSelection, "Formatting...", null, _ => __CaseCommands);
					break;
				default:
					AddCommands(ToolBar, KnownImageIds.FormatSelection, "Formatting...", null, _ => __CaseCommands);
					break;
			}
		}

		void AddClassificationInfoCommand() {
			AddCommand(ToolBar, KnownImageIds.CodeInformation, "Show syntax classification info", ctx => {
				if (ctx.View.TryGetFirstSelectionSpan(out var span)) {
					var cs = ServicesHelper.Instance.ClassifierAggregator.GetClassifier(ctx.View.TextBuffer)
						.GetClassificationSpans(span);
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
				}
			});
		}

		List<CommandItem> GetFormatItems(CommandContext arg) {
			var r = new List<CommandItem>(10);
			var selection = View.Selection;
			if (selection.Mode == TextSelectionMode.Stream) {
				r.AddRange(__SurroundingCommands);
				r.AddRange(__FormatCommands);
				if (View.IsMultilineSelected()) {
					r.Add(new CommandItem(KnownImageIds.Join, "Join Lines", ctx => ctx.View.JoinSelectedLines()));
				}
				var t = View.GetTextViewLineContainingBufferPosition(selection.Start.Position).Extent.GetText();
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
		static CommandItem[] GetDebugCommands() {
			return new CommandItem[] {
				new CommandItem(KnownImageIds.Watch, "Add Watch", c => TextEditorHelper.ExecuteEditorCommand("Debug.AddWatch")),
				new CommandItem(KnownImageIds.Watch, "Add Parallel Watch", c => TextEditorHelper.ExecuteEditorCommand("Debug.AddParallelWatch")),
				new CommandItem(KnownImageIds.DeleteBreakpoint, "Delete All Breakpoints", c => TextEditorHelper.ExecuteEditorCommand("Debug.DeleteAllBreakpoints"))
			};
		}

		static CommandItem[] GetFormatCommands() {
			return new CommandItem[] {
				new CommandItem(KnownImageIds.FormatSelection, "Format Selection", _ => TextEditorHelper.ExecuteEditorCommand("Edit.FormatSelection")),
				new CommandItem(KnownImageIds.FormatDocument, "Format Document", _ => TextEditorHelper.ExecuteEditorCommand("Edit.FormatDocument")),
			};
		}
		static CommandItem[] GetFindAndReplaceCommands() {
			return new CommandItem[] {
					new CommandItem(KnownImageIds.QuickFind, "Find...", _ => TextEditorHelper.ExecuteEditorCommand("Edit.Find")),
					new CommandItem(KnownImageIds.QuickReplace, "Replace...", _ => TextEditorHelper.ExecuteEditorCommand("Edit.Replace")),
					new CommandItem(KnownImageIds.FindInFile, "Find in Files...", _ => TextEditorHelper.ExecuteEditorCommand("Edit.FindinFiles")),
					new CommandItem(KnownImageIds.ReplaceInFolder, "Replace in Files...", _ => TextEditorHelper.ExecuteEditorCommand("Edit.ReplaceinFiles"))
				};
		}
		static CommandItem[] GetSurroundingCommands() {
			return new CommandItem[] {
				new CommandItem(KnownImageIds.AddSnippet, "Surround with...", ctx => {
					TextEditorHelper.ExecuteEditorCommand("Edit.SurroundWith");
				}),
				new CommandItem(KnownImageIds.MaskedTextBox, "Toggle Parentheses", ctx => {
					if (ctx.View.TryGetFirstSelectionSpan(out var span)) {
						WrapWith(ctx, "(", ")", true);
					}
				}),
			};
		}
		static void SearchSelection(string url, CommandContext ctx) {
			ThreadHelper.ThrowIfNotOnUIThread();
			try {
				url = url.Replace("%s", System.Net.WebUtility.UrlEncode(ctx.View.GetFirstSelectionText()));
				if (Keyboard.Modifiers == ModifierKeys.Control) {
					CodistPackage.DTE.ItemOperations.Navigate(url);
				}
				else if (String.IsNullOrEmpty(Config.Instance.BrowserPath) == false) {
					System.Diagnostics.Process.Start(Config.Instance.BrowserPath, String.IsNullOrEmpty(Config.Instance.BrowserParameter) ? url : Config.Instance.BrowserParameter.Replace("%u", url));
				}
				else {
					System.Diagnostics.Process.Start(url);
				}
			}
			catch (Exception ex) {
				MessageBox.Show("Failed to launch web browser to search selected items:\n" + ex.Message, nameof(Codist), MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
		}
	}
}
