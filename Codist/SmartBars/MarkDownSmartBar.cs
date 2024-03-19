using System;
using System.Windows.Controls;
using CLR;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using R = Codist.Properties.Resources;

namespace Codist.SmartBars
{
	sealed class MarkdownSmartBar : SmartBar
	{
		public MarkdownSmartBar(IWpfTextView textView, Microsoft.VisualStudio.Text.Operations.ITextSearchService2 textSearchService) : base(textView, textSearchService) {
		}

		protected override BarType Type => BarType.Markdown;
		protected override bool JoinLinesCommandOnToolBar => true;

		ToolBar MyToolBar => ToolBar2;

		protected override void AddCommands() {
			base.AddCommands();
			AddCommand(MyToolBar, IconIds.TagBold, R.CMD_MarkBold, MarkBold);
			AddCommand(MyToolBar, IconIds.TagItalic, R.CMD_MarkItalic, MarkItalic);
			AddCommand(MyToolBar, IconIds.TagCode, R.CMD_MarkCode, MarkCode);
			AddCommand(MyToolBar, IconIds.TagHyperLink, R.CMD_MarkLink, MarkUrl);
			AddCommand(MyToolBar, IconIds.TagHighlight, R.CMD_MarkHighlight, MarkHighlight);
			AddCommand(MyToolBar, IconIds.TagStrikeThrough, R.CMD_MarkStrikeThrough, ctx => WrapWith(ctx, "~~", "~~", true));
			AddCommand(MyToolBar, IconIds.Heading1, R.CMD_Heading1, MarkHeading1);
			AddCommand(MyToolBar, IconIds.Heading2, R.CMD_Heading2, MarkHeading2);
			AddCommand(MyToolBar, IconIds.Heading3, R.CMD_Heading3, MarkHeading3);
			AddCommand(MyToolBar, IconIds.UnorderedList, R.CMD_UnorderedList, MarkUnorderedList);
			AddCommand(MyToolBar, IconIds.Indent, R.CMD_Indent, ctx => TextEditorHelper.ExecuteEditorCommand("Edit.IncreaseLineIndent"));
		}

		void MarkBold(CommandContext ctx) {
			if (Config.Instance.SmartBarOptions.MatchFlags(SmartBarOptions.UnderscoreBold)) {
				WrapWith(ctx, "__", "__", true);
			}
			else {
				WrapWith(ctx, "**", "**", true);
			}
		}

		void MarkItalic(CommandContext ctx) {
			if (Config.Instance.SmartBarOptions.MatchFlags(SmartBarOptions.UnderscoreItalic)) {
				WrapWith(ctx, "_", "_", true);
			}
			else {
				WrapWith(ctx, "*", "*", true);
			}
		}

		void MarkCode(CommandContext ctx) {
			if (ctx.RightClick) {
				WrapWith(ctx, "``", "``", true);
			}
			else {
				WrapWith(ctx, "`", "`", true);
			}
		}

		void MarkHighlight(CommandContext ctx) {
			WrapWith(ctx, "==", "==", true);
		}

		void MarkUrl(CommandContext ctx) {
			var t = ctx.View.GetFirstSelectionText();
			if (MaybeUrl(t)) {
				foreach (var s in WrapWith(ctx, "[title](", ")", false)) {
					if (s.Snapshot != null) {
						// select the "title"
						ctx.View.Selection.Select(new SnapshotSpan(s.Snapshot, s.Start.Position + 1, 5), false);
						ctx.View.Caret.MoveTo(s.Start + 6);
						return;
					}
				}
			}
			else {
				string clip;
				try {
					clip = System.Windows.Clipboard.GetText();
				}
				catch (SystemException) {
					// ignore
					clip = null;
				}
				var u = MaybeUrl(clip);
				var m = u && clip.IndexOf(')') < 0
					? WrapWith(ctx, "[", $"]({clip})", false)
					: WrapWith(ctx, "[", "](url)", false);
				foreach (var s in m) {
					if (s.Snapshot != null) {
						// select the "url"
						if (u) {
							ctx.View.Selection.Select(new SnapshotSpan(s.Snapshot, s.Start.Position + s.Length - clip.Length - 1, clip.Length), false);
						}
						else {
							ctx.View.Selection.Select(new SnapshotSpan(s.Snapshot, s.Start.Position + s.Length - 4, 3), false);
						}
						ctx.View.Caret.MoveTo(s.End - 1);
						return;
					}
				}
			}
		}

		static bool MaybeUrl(string text) {
			return text?.Length > 7
				&& (text.StartsWith("http://", StringComparison.Ordinal) || text.StartsWith("https://", StringComparison.Ordinal));
		}

		void MarkUnorderedList(CommandContext ctx) {
			MarkList(ctx, MarkdownHelper.UnorderedList, true, default, true);
		}

		void MarkHeading1(CommandContext ctx) {
			MarkList(ctx, MarkdownHelper.Heading1, true, MarkdownHelper.HeadingGlyph);
		}

		void MarkHeading2(CommandContext ctx) {
			MarkList(ctx, MarkdownHelper.Heading2, true, MarkdownHelper.HeadingGlyph);
		}

		void MarkHeading3(CommandContext ctx) {
			MarkList(ctx, MarkdownHelper.Heading3, true, MarkdownHelper.HeadingGlyph);
		}

		static void MarkList(CommandContext ctx, string leadingText, bool skipEmptyLine, char replaceLeadingGlyph = '\0', bool allowLeadingWhitespace = false) {
			MarkdownHelper.MarkList(ctx.View, leadingText, skipEmptyLine, replaceLeadingGlyph, allowLeadingWhitespace);
		}
	}
}
