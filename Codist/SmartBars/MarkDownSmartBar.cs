using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.SmartBars
{
	sealed class MarkdownSmartBar : SmartBar
	{
		public MarkdownSmartBar(IWpfTextView textView, Microsoft.VisualStudio.Text.Operations.ITextSearchService2 textSearchService) : base(textView, textSearchService) {
		}

		ToolBar MyToolBar => ToolBar2;

		protected override void AddCommands(CancellationToken cancellationToken) {
			AddCommand(MyToolBar, KnownImageIds.Bold, "Toggle bold\nCtrl click: toggle and select next", ctx => WrapWith(ctx, "**", "**", true));
			AddCommand(MyToolBar, KnownImageIds.Italic, "Toggle italic\nCtrl click: toggle and select next", ctx => WrapWith(ctx, "_", "_", true));
			AddCommand(MyToolBar, KnownImageIds.MarkupTag, "Toggle code\nCtrl click: toggle and select next", ctx => WrapWith(ctx, "`", "`", true));
			AddCommand(MyToolBar, KnownImageIds.HyperLink, "Hyperlink", MakeUrl);
			AddCommand(MyToolBar, KnownImageIds.StrikeThrough, "Toggle strike through\nCtrl click: toggle and select next", ctx => WrapWith(ctx, "~~", "~~", true));
			base.AddCommands(cancellationToken);
		}

		void MakeUrl(CommandContext ctx) {
			var t = ctx.View.GetFirstSelectionText();
			if (t.StartsWith("http://", StringComparison.Ordinal) || t.StartsWith("https://", StringComparison.Ordinal)) {
				var s = WrapWith(ctx, "[title](", ")", false);
				if (s.Snapshot != null) {
					// select the "title"
					View.Selection.Select(new SnapshotSpan(s.Snapshot, s.Start + 1, 5), false);
					View.Caret.MoveTo(s.Start + 6);
				}
			}
			else {
				var s = WrapWith(ctx, "[", "](url)", false);
				if (s.Snapshot != null) {
					// select the "url"
					View.Selection.Select(new SnapshotSpan(s.Snapshot, s.Start + s.Length - 4, 3), false);
					View.Caret.MoveTo(s.End - 1);
				}
			}
		}
	}
}
