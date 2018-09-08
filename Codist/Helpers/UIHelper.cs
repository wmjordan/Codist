using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using GdiColor = System.Drawing.Color;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;

namespace Codist
{
	static class UIHelper
	{
		public static GdiColor Alpha(this GdiColor color, byte alpha) {
			return GdiColor.FromArgb(alpha, color.R, color.G, color.B);
		}

		public static string ToHexString(this GdiColor color) {
			return "#" + color.A.ToString("X2") + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
		}
		public static SolidColorBrush GetBrush(string color) {
			return NamedColorCache.GetBrush(color);
		}
		public static WpfColor ParseColor(string colorText) {
			if (String.IsNullOrEmpty(colorText)) {
				return WpfColors.Transparent;
			}
			var l = colorText.Length;
			if (l < 6 || l > 9
				|| (l == 7 || l == 9) && Char.IsPunctuation(colorText[0]) == false) {
				return WpfColors.Transparent;
			}
			try {
				byte a = 0xFF, r, g, b;
				switch (colorText.Length) {
					case 6:
						if (ParseByte(colorText, 0, out r)
							&& ParseByte(colorText, 2, out g)
							&& ParseByte(colorText, 4, out b)) {
							return WpfColor.FromArgb(a, r, g, b);
						}
						break;
					case 7:
						if (ParseByte(colorText, 1, out r)
							&& ParseByte(colorText, 3, out g)
							&& ParseByte(colorText, 5, out b)) {
							return WpfColor.FromArgb(a, r, g, b);
						}
						break;
					case 8:
						if (ParseByte(colorText, 0, out a)
							&& ParseByte(colorText, 2, out r)
							&& ParseByte(colorText, 4, out g)
							&& ParseByte(colorText, 6, out b)) {
							return WpfColor.FromArgb(a, r, g, b);
						}
						break;
					case 9:
						if (ParseByte(colorText, 1, out a)
							&& ParseByte(colorText, 3, out r)
							&& ParseByte(colorText, 5, out g)
							&& ParseByte(colorText, 7, out b)) {
							return WpfColor.FromArgb(a, r, g, b);
						}
						break;
				}
			}
			catch (Exception ex) {
				System.Diagnostics.Debug.WriteLine(ex);
			}
			return WpfColors.Transparent;
		}

		static bool ParseByte(string text, int index, out byte value) {
			var h = text[index];
			var l = text[++index];
			var b = 0;
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
		/// <summary>
		/// Returns a new clone of <see cref="WpfBrush"/> which has a new <paramref name="opacity"/> as <see cref="WpfBrush.Opacity"/>.
		/// </summary>
		public static TBrush Alpha<TBrush>(this TBrush brush, double opacity)
			where TBrush : WpfBrush {
			if (brush != null) {
				brush = brush.Clone() as TBrush;
				brush.Opacity = opacity;
			}
			return brush;
		}

		public static TabControl AddPage(this TabControl tabs, string name, Control pageContent, bool prepend) {
			var page = new TabPage(name) { UseVisualStyleBackColor = true };
			if (prepend) {
				tabs.TabPages.Insert(0, page);
				tabs.SelectedIndex = 0;
			}
			else {
				tabs.TabPages.Add(page);
			}
			pageContent.Dock = DockStyle.Fill;
			page.Controls.Add(pageContent);
			return tabs;
		}
		public static string GetClassificationType(this Type type, string field) {
			var f = type.GetField(field);
			var d = f.GetCustomAttribute<ClassificationTypeAttribute>();
			return d?.ClassificationTypeNames;
		}

		static class NamedColorCache
		{
			static readonly Dictionary<string, SolidColorBrush> __Cache = GetBrushes();
			static readonly Dictionary<string, Func<SolidColorBrush>> __SystemColors = GetSystemColors();
			internal static SolidColorBrush GetBrush(string name) {
				var c = ParseColor(name);
				SolidColorBrush brush;
				return
					c != WpfColors.Transparent ? new SolidColorBrush(c)
					: (name.Length >= 3 && name.Length <= 20) ? __Cache.TryGetValue(name, out brush) ? brush : null
					: (name.Length >= 12 && name.Length <= 35) ? __SystemColors.TryGetValue(name, out var func) ? func() : null
					: null;
			}
			static Dictionary<string, SolidColorBrush> GetBrushes() {
				var c = Array.FindAll(typeof(WpfBrushes).GetProperties(), p => p.PropertyType == typeof(SolidColorBrush));
				var d = new Dictionary<string, SolidColorBrush>(c.Length, StringComparer.OrdinalIgnoreCase);
				foreach (var item in c) {
					d.Add(item.Name, item.GetValue(null) as SolidColorBrush);
				}
				return d;
			}
			static Dictionary<string, Func<SolidColorBrush>> GetSystemColors() {
				var c = Array.FindAll(typeof(System.Windows.SystemColors).GetProperties(), p => p.PropertyType == typeof(SolidColorBrush));
				var d = new Dictionary<string, Func<SolidColorBrush>>(c.Length, StringComparer.OrdinalIgnoreCase);
				foreach (var item in c) {
					d.Add(item.Name, (Func<SolidColorBrush>)item.GetGetMethod(false).CreateDelegate(typeof(Func<SolidColorBrush>)));
				}
				return d;
			}
		}
	}
}
