using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell.Interop;
using System.Drawing.Drawing2D;

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
			var t = typeof(CommentStyleTypes);
			foreach (var item in Enum.GetNames(t)) {
				var d = t.GetClassificationType(item);
				if (d == null || d.StartsWith("Comment: ", StringComparison.Ordinal) == false) {
					continue;
				}
				_StyleBox.Items.Add(item);
			}
			_StyleBox.SelectedIndex = 0;

			_AddTagButton.Click += (s, args) => {
				var label = new CommentLabel(_TagTextBox.Text.Length > 0 ? _TagTextBox.Text : "tag", (CommentStyleTypes)Enum.Parse(typeof(CommentStyleTypes), _StyleBox.Text));
				Config.Instance.Labels.Add(label);
				_SyntaxListBox.Items.Add(new ListViewItem(label.Label) { Tag = label, Selected = true });
				_ActiveLabel = label;
			};
			_RemoveTagButton.Click += (s, args) => {
				if (_ActiveLabel == null) {
					return;
				}
				var i = Config.Instance.Labels.IndexOf(_ActiveLabel);
				if (i == -1) {
					return;
				}
				Config.Instance.Labels.RemoveAt(i);
				_SyntaxListBox.Items.RemoveAt(i);
				_ActiveLabel = null;
			};
			_IgnoreCaseBox.CheckedChanged += (s, args) => {
				if (_UiLock == false && _ActiveLabel != null) {
					_ActiveLabel.IgnoreCase = _IgnoreCaseBox.Checked;
				}
			};
			_EndWithPunctuationBox.CheckedChanged += (s, args) => {
				if (_UiLock == false && _ActiveLabel != null) {
					_ActiveLabel.AllowPunctuationDelimiter = _EndWithPunctuationBox.Checked;
				}
			};
			_StyleBox.SelectedIndexChanged += (s, args) => {
				if (_UiLock || _ActiveLabel == null) {
					return;
				}
				_ActiveLabel.StyleID = (CommentStyleTypes)Enum.Parse(typeof(CommentStyleTypes), _StyleBox.Text);
				MarkChanged(s, args);
			};
			_TagTextBox.TextChanged += (s, args) => {
				if (_UiLock == false && _ActiveLabel != null) {
					_ActiveLabel.Label = _TagTextBox.Text;
					foreach (ListViewItem item in _SyntaxListBox.Items) {
						if (item.Tag == _ActiveLabel) {
							item.Text = _TagTextBox.Text;
						}
					}
				}
			};
			foreach (var item in new Control[] { _TagTextBox }) {
				item.Click += MarkChanged;
			}
			foreach (var item in new[] { _ApplyContentBox, _ApplyTagBox, _ApplyContentTagBox  }) {
				item.CheckedChanged += StyleApplicationChanged;
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
			if (_UiLock || _ActiveLabel == null) {
				return;
			}
			UpdatePreview();
		}

		void StyleApplicationChanged(object sender, EventArgs e) {
			if (_UiLock || _ActiveLabel == null) {
				return;
			}
			if (_ApplyContentBox.Checked) {
				_ActiveLabel.StyleApplication = CommentStyleApplication.Content;
			}
			else if (_ApplyTagBox.Checked) {
				_ActiveLabel.StyleApplication = CommentStyleApplication.Tag;
			}
			else if (_ApplyContentTagBox.Checked) {
				_ActiveLabel.StyleApplication = CommentStyleApplication.TagAndContent;
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
			_ApplyContentTagBox.Checked = i.StyleApplication == CommentStyleApplication.TagAndContent;
			_EndWithPunctuationBox.Checked = i.AllowPunctuationDelimiter;
			_IgnoreCaseBox.Checked = i.IgnoreCase;
			var s = i.StyleID.ToString();
			for (int n = 0; n < _StyleBox.Items.Count; n++) {
				if ((string)_StyleBox.Items[n] == s) {
					_StyleBox.SelectedIndex = n;
					break;
				}
			}
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
			var style = Config.Instance.CommentStyles.Find(i => i.StyleID == label.StyleID);
			if (style == null || String.IsNullOrEmpty(label.Label)) {
				return;
			}
			using (var g = Graphics.FromImage(bmp))
			using (var f = new Font(fs.bstrFaceName, (float)(fs.wPointSize + style.FontSize), ConfigPage.GetFontStyle(style)))
			using (var b = style.ForeColor.A == 0 ? (Brush)Brushes.Black.Clone() : new SolidBrush(style.ForeColor.ToGdiColor())) {
				var t = label.StyleApplication == CommentStyleApplication.Tag ? label.Label : "Preview 01ioIOlLWM";
				var m = g.MeasureString(t, f, bmp.Size);
				g.SmoothingMode = SmoothingMode.HighQuality;
				g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
				g.CompositingQuality = CompositingQuality.HighQuality;
				using (var bb = ConfigPage.GetPreviewBrush(style.BackgroundEffect, style.BackColor, ref m)) {
					g.FillRectangle(bb, new Rectangle(0, 0, (int)m.Width, (int)m.Height));
				}
				g.DrawString(t, f, b, new RectangleF(PointF.Empty, bmp.PhysicalDimension));
			}
		}
	}
}
