using System;
using System.Drawing;
using System.Windows.Forms;

namespace Codist.Options
{
	public sealed class PickColorButton : Button
	{
		Color _SelectedColor;
		ContextMenuStrip _ContextMenu;

		public PickColorButton() {
			var x = Height - Padding.Top - Padding.Bottom - 2;
			Image = new Bitmap(x, x);
			TextImageRelation = TextImageRelation.ImageBeforeText;
		}

		public Color DefaultColor { get; set; }
		public Color SelectedColor {
			get { return _SelectedColor; }
			set { if (value != SelectedColor) { SetColor(value); } }
		}

		void SetColor(Color color) {
			_SelectedColor = color;
			var bmp = Image as Bitmap;
			using (var g = Graphics.FromImage(bmp))
			using (var b = new SolidBrush(color.A == 0 ? SystemColors.Control : color.Alpha(255))) {
				g.DrawRectangle(Pens.DarkGray, 0, 0, bmp.Width - 1, bmp.Height - 1);
				g.FillRectangle(b, 1, 1, bmp.Width - 2, bmp.Height - 2);
			}
			Invalidate();
		}

		protected override void OnClick(EventArgs e) {
			if (_ContextMenu == null) {
				InitContextMenu();
			}
			_ContextMenu.Show(this, 1, Height - 1);
		}

		void InitContextMenu() {
			_ContextMenu = new ContextMenuStrip {
				RenderMode = ToolStripRenderMode.Professional,
				ShowCheckMargin = false,
				ShowImageMargin = false,
				Items = {
						new ToolStripMenuItem("Pick color...") { Name = "PickColor" },
						new ToolStripMenuItem("Reset to default color") { Name = "Reset" },
						new ToolStripMenuItem("Copy color") { Name = "Copy" },
						new ToolStripMenuItem("Paste color") { Name = "Paste" },
					}
			};
			_ContextMenu.Opening += (s, args) => _ContextMenu.Items["Paste"].Enabled = GetClipboardColor().A != 0;
			_ContextMenu.ItemClicked += (s, args) => {
				switch (args.ClickedItem.Name) {
					case "Reset":
						SelectedColor = Color.Empty;
						base.OnClick(EventArgs.Empty);
						return;
					case "Copy":
						try {
							Clipboard.SetDataObject(SelectedColor.ToHexString());
						}
						catch (System.Runtime.InteropServices.ExternalException) {
							// ignore
						}
						return;
					case "Paste":
						SelectedColor = GetClipboardColor();
						return;
				}
				args.ClickedItem.GetCurrentParent().Hide();
				using (var c = new ColorDialog() {
					FullOpen = true,
					Color = SelectedColor.A == 0 ? DefaultColor : SelectedColor
				}) {
					if (c.ShowDialog() == DialogResult.OK) {
						SelectedColor = c.Color;
						base.OnClick(EventArgs.Empty);
					}
				}
			};
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			Image.Dispose();
			_ContextMenu?.Dispose();
		}

		static Color GetClipboardColor() {
			string c;
			try {
				c = Clipboard.GetText();
			}
			catch (System.Runtime.InteropServices.ExternalException) {
				return Color.Empty;
			}
			UIHelper.ParseColor(c, out var color, out _);
			return color.ToGdiColor();
		}
	}
}
