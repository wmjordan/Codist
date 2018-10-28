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
		public MarkdownSmartBar(IWpfTextView textView) : base(textView) {
		}

		ToolBar MyToolBar => ToolBar2;

		protected override void AddCommands(CancellationToken cancellationToken) {
			AddCommand(MyToolBar, KnownImageIds.Bold, "Toggle bold\nCtrl click: toggle and select next", ctx => {
				SurroundWith(ctx, "**", "**", true);
			});
			AddCommand(MyToolBar, KnownImageIds.Italic, "Toggle italic\nCtrl click: toggle and select next", ctx => {
				SurroundWith(ctx, "_", "_", true);
			});
			AddCommand(MyToolBar, KnownImageIds.MarkupTag, "Toggle code\nCtrl click: toggle and select next", ctx => {
				SurroundWith(ctx, "`", "`", true);
			});
			AddCommand(MyToolBar, KnownImageIds.HyperLink, "Hyperlink", ctx => {
				var s = SurroundWith(ctx, "[", "](url)", false);
				if (s.Snapshot != null) {
					// select the "url"
					View.Selection.Select(new SnapshotSpan(s.Snapshot, s.Start + s.Length - 4, 3), false);
					View.Caret.MoveTo(s.End - 1);
				}
			});
			base.AddCommands(cancellationToken);
		}
	}
}
