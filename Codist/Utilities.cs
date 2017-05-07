using System;
using System.Diagnostics;
using System.Reflection;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;
using GdiColor = System.Drawing.Color;

namespace Codist
{
	static class Utilities
	{
		public static string GetEnumDescription(this Type type, string field) {
			var f = type.GetField(field);
			var d = f.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
			return d?.Description;
		}

		public static GdiColor ChangeTrasparency(this GdiColor color, byte alpha) {
			return GdiColor.FromArgb(alpha, color.R, color.G, color.B);
		}

		public static WpfColor ParseColor(string colorText) {
			if (String.IsNullOrEmpty(colorText) || colorText[0] != '#' || colorText.Length != 7 && colorText.Length != 9) {
				return WpfColors.Transparent;
			}
			try {
				byte a = 0xFF, r, g, b;
				if (colorText.Length == 7
					&& ParseByte(colorText, 1, out r)
					&& ParseByte(colorText, 3, out g)
					&& ParseByte(colorText, 5, out b)) {
					return WpfColor.FromArgb(a, r, g, b);
				}
				if (colorText.Length == 9
					&& ParseByte(colorText, 1, out a)
					&& ParseByte(colorText, 3, out r)
					&& ParseByte(colorText, 5, out g)
					&& ParseByte(colorText, 7, out b)) {
					return WpfColor.FromArgb(a, r, g, b);
				}
			}
			catch (Exception ex) {
				Debug.WriteLine(ex);
			}
			return WpfColors.Transparent;
		}

		public static bool ParseByte(string text, int index, out byte value) {
			var h = text[index];
			var l = text[++index];
			var b = 0;
			if (h >= '0' && h <= '9') {
				b = (h - '0') << 4;
			}
			else if (h >= 'A' && h <= 'F') {
				b = (h - 'A' + 10) << 4;
			}
			else if (h >= 'a' && h <= 'f') {
				b = (h - 'a' + 10) << 4;
			}
			else {
				value = 0;
				return false;
			}
			if (l >= '0' && l <= '9') {
				b |= (l - '0');
			}
			else if (l >= 'A' && l <= 'F') {
				b |= (l - 'A' + 10);
			}
			else if (l >= 'a' && l <= 'f') {
				b |= (l - 'a' + 10);
			}
			else {
				value = 0;
				return false;
			}
			value = (byte)b;
			return true;
		}

		public static string ToHexString(this GdiColor color) {
			return "#" + color.A.ToString("X2") + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
		}
		public static string ToHexString(this WpfColor color) {
			return "#" + color.A.ToString("X2") + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
		}
		public static GdiColor ToGdiColor(this WpfColor color) {
			return GdiColor.FromArgb(color.A, color.R, color.G, color.B);
		}
		public static WpfColor ToWpfColor (this GdiColor color) {
			return WpfColor.FromArgb(color.A, color.R, color.G, color.B);
		}
	}
}
