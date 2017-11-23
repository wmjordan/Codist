using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codist.Options
{
	public partial class CommentTaggerOptionPage : UserControl
	{
		readonly CommentTagger _Service;
		CommentLabel _ActiveLabel;
		bool _UiLock;
		bool _Loaded;

		public CommentTaggerOptionPage() {
			InitializeComponent();
		}
		internal CommentTaggerOptionPage(CommentTagger service) : this() {
			_Service = service;
		}

		protected override void OnLoad(EventArgs e) {
			base.OnLoad(e);
			if (_Loaded) {
				return;
			}

			foreach (var item in Config.Instance.Labels) {
				_SyntaxListBox.Items.Add(new ListViewItem(item.Label) { Tag = item });
			}
			var t = typeof(CommentStyles);
			foreach (var item in Enum.GetNames(t)) {
				var d = t.GetEnumDescription(item);
				if (d == null || d.StartsWith("Comment: ", StringComparison.Ordinal) == false) {
					continue;
				}
				_StyleBox.Items.Add(item);
			}

			_ApplyContentBox.CheckedChanged += StyleApplicationChanged;
			_ApplyTagBox.CheckedChanged += StyleApplicationChanged;
			_IgnoreCaseBox.CheckedChanged += (s, args) => { if (_UiLock == false) { _ActiveLabel.IgnoreCase = _IgnoreCaseBox.Checked; } };
			_EndWithPunctuationBox.CheckedChanged += (s, args) => { if (_UiLock == false) { _ActiveLabel.AllowPunctuationDelimiter = _EndWithPunctuationBox.Checked; } };
			_StyleBox.SelectedIndexChanged += (s, args) => { if (_UiLock == false) { _ActiveLabel.StyleID = (CommentStyles)_StyleBox.SelectedIndex; } };
			_TagTextBox.TextChanged += (s, args) => { if (_UiLock == false) { _ActiveLabel.Label = _TagTextBox.Text; } };
			foreach (var item in new Control[] { _StyleBox, _TagTextBox }) {
				item.Click += MarkChanged;
			}
			foreach (var item in new[] { _ApplyContentBox, _ApplyTagBox,  }) {
				item.CheckedChanged += MarkChanged;
			}
			foreach (var item in new[] { _IgnoreCaseBox, _EndWithPunctuationBox }) {
				item.CheckStateChanged += MarkChanged;
			}

			_PreviewBox.SizeChanged += (s, args) => { UpdatePreview(); };
			_SyntaxListBox.ItemSelectionChanged += _SyntaxListBox_ItemSelectionChanged;
			_Loaded = true;
		}

		void MarkChanged(object sender, EventArgs args) {
			if (_UiLock) {
				return;
			}
			UpdatePreview();
		}

		void StyleApplicationChanged(object sender, EventArgs e) {
			if (_UiLock) {
				return;
			}
			if (_ApplyContentBox.Checked) {
				_ActiveLabel.StyleApplication = CommentStyleApplication.Content;
			}
			else if (_ApplyTagBox.Checked) {
				_ActiveLabel.StyleApplication = CommentStyleApplication.Tag;
			}
		}

		void _SyntaxListBox_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e) {
			if (e.ItemIndex == -1) {
				return;
			}
			var i = e.Item.Tag as CommentLabel;
			if (i == null) {
				return;
			}
			_UiLock = true;
			_ActiveLabel = i;
			_ApplyContentBox.Checked = i.StyleApplication == CommentStyleApplication.Content;
			_ApplyTagBox.Checked = i.StyleApplication == CommentStyleApplication.Tag;
			_EndWithPunctuationBox.Checked = i.AllowPunctuationDelimiter;
			_IgnoreCaseBox.Checked = i.IgnoreCase;
			_StyleBox.SelectedIndex = (int)i.StyleID;
			_TagTextBox.Text = i.Label;
			UpdatePreview();
			_UiLock = false;
		}

		void UpdatePreview() {
			if (_ActiveLabel == null) {
				return;
			}
			var bmp = new Bitmap(_PreviewBox.Width, _PreviewBox.Height);
			var fs = _Service.GetFontSettings(new Guid(FontsAndColorsCategory.TextEditor));
			var label = _ActiveLabel;
			RenderPreview(bmp, fs, label);
			_PreviewBox.Image = bmp;
		}

		static void RenderPreview(Bitmap bmp, FontInfo fs, CommentLabel label) {
			var style = Config.Instance.Styles.Find(i => i.StyleID == label.StyleID);
			if (style == null || String.IsNullOrEmpty(label.Label)) {
				return;
			}
			using (var g = Graphics.FromImage(bmp))
			using (var f = new Font(fs.bstrFaceName, (float)(fs.wPointSize + style.FontSize), PageBase.GetFontStyle(style)))
			using (var b = new SolidBrush(style.ForeColor.ToGdiColor()))
			using (var p = new SolidBrush(style.BackColor.ToGdiColor())) {
				var t = label.StyleApplication == CommentStyleApplication.Tag ? label.Label : "Preview 01ioIOlLWM";
				var m = g.MeasureString(t, f, bmp.Size);
				g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
				g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
				g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
				g.FillRectangle(p, new Rectangle(0, 0, (int)m.Width, (int)m.Height));
				g.DrawString(t, f, b, new RectangleF(PointF.Empty, bmp.PhysicalDimension));
			}
		}
	}
}
