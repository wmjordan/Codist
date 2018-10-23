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
	sealed class MarkDownSmartBar : SmartBar
	{
		public MarkDownSmartBar(IWpfTextView textView) : base(textView) {
		}

		ToolBar MyToolBar => ToolBar2;

		protected override void AddCommands(CancellationToken cancellationToken) {
			AddCommand(MyToolBar, KnownImageIds.Bold, "Bold", ctx => {
				SurroundWith(ctx, "**", "**", true);
			});
			AddCommand(MyToolBar, KnownImageIds.Italic, "Italic", ctx => {
				SurroundWith(ctx, "_", "_", true);
			});
			AddCommand(MyToolBar, KnownImageIds.MarkupTag, "Code", ctx => {
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

		SnapshotSpan SurroundWith(CommandContext ctx, string prefix, string suffix, bool selectModified) {
			var firstModified = new SnapshotSpan();
			ctx.KeepToolBar(false);
			using (var edit = ctx.View.TextSnapshot.TextBuffer.CreateEdit()) {
				foreach (var item in View.Selection.SelectedSpans) {
					if (edit.Replace(item, prefix + item.GetText() + suffix) && firstModified.Snapshot == null) {
						firstModified = item;
					}
				}
				if (edit.HasEffectiveChanges) {
					var snapsnot = edit.Apply();
					firstModified = new SnapshotSpan(snapsnot, firstModified.Start, prefix.Length + firstModified.Length + suffix.Length);
					if (selectModified) {
						View.Selection.Select(firstModified, false);
						View.Caret.MoveTo(firstModified.End);
					}
				}
			}
			return firstModified;
		}
	}
}
