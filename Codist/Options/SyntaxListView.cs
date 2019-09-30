using System;
using System.Drawing;
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
			ShowItemToolTips = true;
		}
		protected override void WndProc(ref Message m) {
			if (m.Msg == 0x0F) {
				BackColor = ThemeHelper.DocumentPageColor;
				ForeColor = ThemeHelper.DocumentTextColor;
				foreach (var item in Items) {
					(item as SyntaxListViewItem)?.ApplyTheme();
				}
			}
			base.WndProc(ref m);
		}
	}

	class SyntaxListViewItem : ListViewItem
	{
		StyleBase _Style;

		internal StyleBase Style {
			get => _Style;
			private set {
				_Style = value;
				ToolTipText = _Style?.Description;
			}
		}

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
			Style.MixStyle(out var s, out var fg, out var bg);
			if (Font.Style != s || Style.Font != Font.OriginalFontName) {
				Font = new Font(Style.Font, Font.Size, s, Font.Unit, Font.GdiCharSet);
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

		protected void SetStyle(StyleBase style) {
			Style = style;
		}
	}

	sealed class CommentTaggerListViewItem : SyntaxListViewItem
	{
		internal Taggers.CommentLabel CommentLabel { get; }

		public CommentTaggerListViewItem() : base() {
		}
		internal CommentTaggerListViewItem(string label, Taggers.CommentLabel commentLabel) : base(label) {
			CommentLabel = commentLabel ?? throw new ArgumentNullException(nameof(commentLabel));
		}

		internal override void ApplyTheme() {
			if (CommentLabel == null) {
				return;
			}
			var styleId = CommentLabel.StyleID;
			SetStyle(Config.Instance.CommentStyles.Find(i => i.StyleID == styleId));
			if (Style == null) {
				return;
			}
			base.ApplyTheme();
		}
	}
}
