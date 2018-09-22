using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Codist.SyntaxHighlight;

namespace Codist.Options
{
	/// <summary>
	/// This listview can contain item of type <see cref="SyntaxListViewItem"/> or <see cref="CommentTaggerListViewItem"/>.
	/// </summary>
	public class SyntaxListView : ListView
	{
		public SyntaxListView() {
			SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
		}
		protected override void WndProc(ref Message m) {
			if (m.Msg == 0x0F) {
				BackColor = ThemeHelper.DocumentPageColor;
				ForeColor = ThemeHelper.DocumentTextColor;
				foreach (SyntaxListViewItem item in Items) {
					item.ApplyTheme();
				}
			}
			base.WndProc(ref m);
		}
	}

	public class SyntaxListViewItem : ListViewItem
	{
		internal StyleBase Style { get; set; }

		public SyntaxListViewItem() : base() {
		}
		internal SyntaxListViewItem(string label) : base(label) { }
		internal SyntaxListViewItem(string label, StyleBase style) : base(label) {
			Style = style;
		}

		internal virtual void ApplyTheme() {
			if (Style == null) {
				return;
			}
			UIHelper.MixStyle(Style, out var s, out var fg, out var bg);
			if (Font.Style != s) {
				Font = new Font(Font, s);
			}
			fg = Style.ForeColor.A != 0 ? Style.ForeColor.ToGdiColor() : fg;
			if (ForeColor != fg) {
				ForeColor = fg;
			}
			bg = Style.BackColor.A != 0 ? Style.BackColor.ToGdiColor() : bg;
			if (BackColor != bg) {
				BackColor = bg;
			}
		}
	}

	public class CommentTaggerListViewItem : SyntaxListViewItem
	{
		internal Classifiers.CommentLabel CommentLabel { get; }

		public CommentTaggerListViewItem() : base() {
		}
		internal CommentTaggerListViewItem(string label, Classifiers.CommentLabel commentLabel) : base(label) {
			CommentLabel = commentLabel ?? throw new ArgumentNullException(nameof(commentLabel));
		}

		internal override void ApplyTheme() {
			if (CommentLabel == null) {
				return;
			}
			var styleId = CommentLabel.StyleID;
			Style = Config.Instance.CommentStyles.Find(i => i.StyleID == styleId);
			if (Style == null) {
				return;
			}
			base.ApplyTheme();
		}
	}
}
