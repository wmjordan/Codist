using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Codist.Controls
{
	public partial class TitleLabel : Label
	{
		protected override void OnPaint(PaintEventArgs e) {
			base.OnPaint(e);
			var g = e.Graphics;
			g.DrawLine(SystemPens.ControlDark, 0, Height - 1, Width, Height - 1);
		}
	}

	public class CustomGroupBox : GroupBox
	{
		protected override void OnPaint(PaintEventArgs e) {
			var g = e.Graphics;
			var m = g.MeasureString(Text, Font, Width);
			var tw = m.Width;
			var th = m.Height;
			var tb = new RectangleF(8, 1, tw + 3, th + 2);
			g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
			g.DrawString(Text, Font, SystemBrushes.ControlText, tb);
			tb.Offset(-1, 0);
			g.DrawString(Text, Font, SystemBrushes.ControlText, tb);
			g.DrawLine(SystemPens.ControlDark, 0, th + 1, Width, th + 1);
		}
	}
}
