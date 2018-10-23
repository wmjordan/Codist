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

		SnapshotSpan SurroundWith(CommandContext ctx, string prefix, string suffix, bool selectModified) {
			var firstModified = new SnapshotSpan();
			var psLength = prefix.Length + suffix.Length;
			var removed = false;
			string t = null;
			ctx.KeepToolBar(false);
			using (var edit = ctx.View.TextSnapshot.TextBuffer.CreateEdit()) {
				foreach (var item in View.Selection.SelectedSpans) {
					t = item.GetText();
					if (t.StartsWith(prefix, StringComparison.Ordinal)
						&& t.EndsWith(suffix, StringComparison.Ordinal)) {
						if (edit.Replace(item, t.Substring(prefix.Length, t.Length - psLength))
							&& firstModified.Snapshot == null) {
							firstModified = item;
							removed = true;
						}
					}
					else if (edit.Replace(item, prefix + t + suffix) && firstModified.Snapshot == null) {
						firstModified = item;
					}
				}
				if (edit.HasEffectiveChanges) {
					var snapsnot = edit.Apply();
					firstModified = new SnapshotSpan(snapsnot, firstModified.Start, removed ? firstModified.Length - psLength : firstModified.Length + psLength);
					if (t != null
						&& System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control) {
						FindNext(ctx, t);
					}
					else if (selectModified) {
						View.Selection.Select(firstModified, false);
						View.Caret.MoveTo(firstModified.End);
					}
				}
			}
			return firstModified;
		}
	}
}
