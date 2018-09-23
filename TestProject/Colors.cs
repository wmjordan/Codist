using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GdiColor = System.Drawing.Color;
using GdiBrush = System.Drawing.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;
using WpfBrush = System.Windows.Media.Brush;

namespace TestProject
{
	class Colors
	{
		const int _c3 = 64 << 24 | 128 << 16 | 64 << 8 | 192;
		GdiColor c1 = GdiColor.FromArgb(128, 64, 64);
		GdiColor c2 = GdiColor.FromArgb(64, 128, 64, 192);
		GdiColor c3 = GdiColor.FromArgb(_c3);
		GdiColor c4 = GdiColor.FromArgb(1082147008);
		GdiColor n1 = GdiColor.Transparent;
		GdiColor n2 = GdiColor.OliveDrab;
		GdiColor n3 = GdiColor.MediumOrchid;
		GdiColor s1 = System.Drawing.SystemColors.Window;
		GdiColor s2 = System.Drawing.SystemColors.Control;
		GdiColor s3 = System.Drawing.SystemColors.ControlText;
		GdiBrush b1 = System.Drawing.SystemBrushes.Window;
		GdiBrush b2 = System.Drawing.SystemBrushes.Control;
		GdiBrush b3 = System.Drawing.SystemBrushes.ControlText;

		void M() {
			const int c = 64 << 24 | 128 << 16 | 64 << 8 | 192;
			GdiColor c4 = GdiColor.FromArgb(c);
			GdiColor c5 = GdiColor.FromArgb(_c3);
		}
	}

	class MediaColors
	{
		WpfColor c1 = WpfColor.FromRgb(128, 64, 64);
		WpfColor c2 = WpfColor.FromArgb(64, 128, 64, 192);
		WpfColor c3 = WpfColor.FromArgb(byte.MaxValue, 128, 64, 192);
		WpfColor c4 = WpfColor.FromArgb(0xF0, 128, 64, 192);
		WpfColor n1 = WpfColors.Transparent;
		WpfColor n2 = WpfColors.OliveDrab;
		WpfColor n3 = WpfColors.MediumOrchid;
		WpfColor s1 = System.Windows.SystemColors.WindowColor;
		WpfColor s2 = System.Windows.SystemColors.ControlColor;
		WpfColor s3 = System.Windows.SystemColors.ControlTextColor;
		WpfBrush b1 = System.Windows.SystemColors.WindowBrush;
		WpfBrush b2 = System.Windows.SystemColors.ControlBrush;
		WpfBrush b3 = System.Windows.SystemColors.ControlTextBrush;
	}
}
