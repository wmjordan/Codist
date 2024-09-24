using System;
using System.Globalization;
using GdiColor = System.Drawing.Color;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;
using WpfBrush = System.Windows.Media.Brush;

namespace Codist
{
	static class UIHelper
	{
		public static GdiColor Alpha(this GdiColor color, byte alpha) {
			return GdiColor.FromArgb(alpha, color.R, color.G, color.B);
		}
		public static string ToText(this int value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}
		public static string ToHexString(this GdiColor color) {
			return "#" + color.A.ToString("X2") + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
		}
		public static void ParseColor(string colorText, out WpfColor color, out byte opacity) {
			if (String.IsNullOrEmpty(colorText) || colorText[0] != '#') {
				goto EXIT;
			}
			var l = colorText.Length;
			if (l != 7 && l != 9 && l != 3) {
				goto EXIT;
			}
			try {
				byte a, r, g, b;
				switch (l) {
					case 3:
						if (ParseByte(colorText, 1, out a)) {
							opacity = a;
							color = WpfColors.Transparent;
							return;
						}
						break;
					case 7:
						if (ParseByte(colorText, 1, out r)
							&& ParseByte(colorText, 3, out g)
							&& ParseByte(colorText, 5, out b)) {
							color = WpfColor.FromRgb(r, g, b);
							opacity = 0xFF;
							return;
						}
						break;
					case 9:
						if (ParseByte(colorText, 1, out a)
							&& ParseByte(colorText, 3, out r)
							&& ParseByte(colorText, 5, out g)
							&& ParseByte(colorText, 7, out b)) {
							if (a == 0) {
								goto EXIT;
							}
							color = WpfColor.FromRgb(r, g, b);
							opacity = a;
							return;
						}
						break;
				}
			}
			catch (Exception ex) {
				ex.Log();
			}
		EXIT:
			color = WpfColors.Transparent;
			opacity = 0;
		}

		static bool ParseByte(string text, int index, out byte value) {
			var h = text[index];
			var l = text[++index];
			int b;
			if (h >= '0' && h <= '9') {
				b = (h - '0') << 4;
			}
			else if (h >= 'A' && h <= 'F') {
				b = (h - ('A' - 10)) << 4;
			}
			else if (h >= 'a' && h <= 'f') {
				b = (h - ('a' - 10)) << 4;
			}
			else {
				value = 0;
				return false;
			}
			if (l >= '0' && l <= '9') {
				b |= l - '0';
			}
			else if (l >= 'A' && l <= 'F') {
				b |= l - ('A' - 10);
			}
			else if (l >= 'a' && l <= 'f') {
				b |= l - ('a' - 10);
			}
			else {
				value = 0;
				return false;
			}
			value = (byte)b;
			return true;
		}

		public static string ToHexString(this WpfColor color) {
			return "#" + (color.A == 0xFF ? null : color.A.ToString("X2")) + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
		}
		public static GdiColor ToGdiColor(this WpfColor color) {
			return GdiColor.FromArgb(color.A, color.R, color.G, color.B);
		}
		public static WpfColor ToWpfColor(this GdiColor color) {
			return WpfColor.FromArgb(color.A, color.R, color.G, color.B);
		}
		public static WpfColor Alpha(this WpfColor color, byte a) {
			return WpfColor.FromArgb(a, color.R, color.G, color.B);
		}
		public static WpfColor MakeOpaque(this WpfColor color) {
			return WpfColor.FromArgb(Byte.MaxValue, color.R, color.G, color.B);
		}
		/// <summary>
		/// Returns a new clone of <see cref="WpfBrush"/> which has a new <paramref name="opacity"/> as <see cref="WpfBrush.Opacity"/>.
		/// </summary>
		public static TBrush Alpha<TBrush>(this TBrush brush, double opacity)
			where TBrush : WpfBrush {
			if (brush != null) {
				brush = brush.Clone() as TBrush;
				if (brush is System.Windows.Media.SolidColorBrush cb) {
					cb.Color = cb.Color.Alpha((byte)(opacity * 255));
				}
				else {
					brush.Opacity = opacity;
				}
			}
			return brush;
		}
	}
}
