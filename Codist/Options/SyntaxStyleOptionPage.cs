using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codist.Options
{
	public partial class SyntaxStyleOptionPage : UserControl
	{
		readonly PageBase _service;
		CommentStyleOption _activeStyle;
		bool _uiLock;
		bool _loaded;

		public SyntaxStyleOptionPage() {
			InitializeComponent();
		}
		internal SyntaxStyleOptionPage(PageBase service) : this() {
			_service = service;
		}

		protected override void OnLoad(EventArgs e) {
			base.OnLoad(e);
			if (_loaded) {
				return;
			}
			foreach (var item in Config.Instance.Styles) {
				_SyntaxListBox.Items.Add(new ListViewItem(item.StyleID.ToString()) { Tag = item });
			}
			_BackColorButton.Click += SetBackColor;
			_BackColorTransBox.ValueChanged += SetBackColor;
			_ForeColorButton.Click += SetForeColor;
			_ForeColorTransBox.ValueChanged += SetForeColor;
			_BoldBox.CheckStateChanged += (s, args) => { if (_uiLock == false) { _activeStyle.Bold = ToBool(_BoldBox.CheckState); } };
			_ItalicBox.CheckStateChanged += (s, args) => { if (_uiLock == false) { _activeStyle.Italic = ToBool(_ItalicBox.CheckState); } };
			_UnderlineBox.CheckStateChanged += (s, args) => { if (_uiLock == false) { _activeStyle.Underline = ToBool(_UnderlineBox.CheckState); } };
			_StrikeBox.CheckStateChanged += (s, args) => { if (_uiLock == false) { _activeStyle.StrikeThrough = ToBool(_StrikeBox.CheckState); } };
			_FontSizeBox.ValueChanged += (s, args) => { if (_uiLock == false) { _activeStyle.FontSize = (double)_FontSizeBox.Value; } };
			foreach (var item in new Control[] { _BackColorButton, _ForeColorButton }) {
				item.Click += MarkChanged;
			}
			foreach (var item in new [] { _BoldBox, _ItalicBox, _UnderlineBox, _StrikeBox }) {
				item.CheckStateChanged += MarkChanged;
			}
			foreach (var item in new []{ _BackColorTransBox, _BackColorTransBox, _FontSizeBox}) {
				item.ValueChanged += MarkChanged;
			}
			_PreviewBox.SizeChanged += (s, args) => { UpdatePreview(); };
			_SyntaxListBox.ItemSelectionChanged += _SyntaxListBox_ItemSelectionChanged;
			_loaded = true;
		}

		void MarkChanged(object sender, EventArgs args) {
			if (_uiLock) {
				return;
			}
			UpdatePreview();
		}

		private void SetForeColor(object sender, EventArgs args) {
			if (_uiLock) {
				return;
			}
			_activeStyle.ForeColor = _ForeColorButton.SelectedColor.ChangeTrasparency((byte)_ForeColorTransBox.Value).ToWpfColor();
		}

		private void SetBackColor(object sender, EventArgs args) {
			if (_uiLock) {
				return;
			}
			_activeStyle.BackColor = _BackColorButton.SelectedColor.ChangeTrasparency((byte)_BackColorTransBox.Value).ToWpfColor();
		}

		void _SyntaxListBox_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e) {
			if (e.ItemIndex == -1) {
				return;
			}
			var i = e.Item.Tag as CommentStyleOption;
			if (i == null) {
				return;
			}
			_uiLock = true;
			_activeStyle = i;
			_BoldBox.CheckState = ToCheckState(i.Bold);
			_ItalicBox.CheckState = ToCheckState(i.Italic);
			_StrikeBox.CheckState = ToCheckState(i.StrikeThrough);
			_UnderlineBox.CheckState = ToCheckState(i.Underline);

			_FontSizeBox.Value = i.FontSize > 100 ? 100m : i.FontSize < -10 ? -10m : (decimal)i.FontSize;
			_ForeColorTransBox.Value = i.ForeColor.A;
			_BackColorTransBox.Value = i.BackColor.A;
			_ForeColorButton.SelectedColor = ToColor(i.ForeColor);
			_BackColorButton.SelectedColor = ToColor(i.BackColor);
			
			UpdatePreview();
			_uiLock = false;
		}

		static Color ToColor(System.Windows.Media.Color color) {
			return Color.FromArgb(255, color.R, color.G, color.B);
		}

		void UpdatePreview() {
			if (_activeStyle == null) {
				return;
			}
			var bmp = new Bitmap(_PreviewBox.Width, _PreviewBox.Height);
			var fs = _service.GetFontSettings(new Guid(FontsAndColorsCategory.TextEditor));
			var style = _activeStyle;
			RenderPreview(bmp, fs, style);
			_PreviewBox.Image = bmp;
		}

		static void RenderPreview(Bitmap bmp, FontInfo fs, CommentStyleOption style) {
			using (var g = Graphics.FromImage(bmp))
			using (var f = new Font(fs.bstrFaceName, (float)(fs.wPointSize + style.FontSize), PageBase.GetFontStyle(style)))
			using (var b = new SolidBrush(style.ForeColor.ToGdiColor()))
			using (var p = new SolidBrush(style.BackColor.ToGdiColor())) {
				const string t = "Preview 01ioIOlLWM";
				var m = g.MeasureString(t, f, bmp.Size);
				g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
				g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
				g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
				g.FillRectangle(p, new Rectangle(0, 0, (int)m.Width, (int)m.Height));
				g.DrawString(t, f, b, new RectangleF(PointF.Empty, bmp.PhysicalDimension));
			}
		}

		static CheckState ToCheckState(bool? value) {
			return value.HasValue == false
				? CheckState.Indeterminate
				: value.Value
				? CheckState.Checked
				: CheckState.Unchecked;
		}
		static bool? ToBool(CheckState state) {
			switch (state) {
				case CheckState.Unchecked: return false;
				case CheckState.Checked: return true;
				default: return null;
			}
		}
	}
}
