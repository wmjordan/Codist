using System;
using System.Drawing;
using System.Windows.Forms;

namespace Codist.Options
{
	public partial class PickColorButton : Button
	{
		public Color SelectedColor {
			get { return ((Bitmap)Image).GetPixel(1, 1); }
			set { if (value != SelectedColor) { SetColor(value); } }
		}

		public PickColorButton() {
			var x = Height - Padding.Top - Padding.Bottom;
			Image = new Bitmap(x, x);
			TextImageRelation = TextImageRelation.ImageBeforeText;
		}

		void SetColor(Color color) {
			var bmp = Image as Bitmap;
			using (var g = Graphics.FromImage(bmp))
			using (var b = new SolidBrush(color)) {
				g.DrawRectangle(Pens.DarkGray, 0, 0, bmp.Width - 1, bmp.Height - 1);
				g.FillRectangle(b, 1, 1, bmp.Width - 2, bmp.Height - 2);
			}
			Invalidate();
		}

		protected override void OnClick(EventArgs e) {
			using (var c = new ColorDialog() { FullOpen = true, Color = SelectedColor }) {
				if (c.ShowDialog() == DialogResult.OK) {
					SelectedColor = c.Color;
					base.OnClick(e);
				}
			}
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			Image.Dispose();
		}
	}
}
