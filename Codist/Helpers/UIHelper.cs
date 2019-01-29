using System;
using System.Reflection;
using System.Windows.Forms;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using GdiColor = System.Drawing.Color;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;
using WpfBrush = System.Windows.Media.Brush;
using FontStyle = System.Drawing.FontStyle;

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
							opacity = Byte.MaxValue;
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
				System.Diagnostics.Debug.WriteLine(ex);
			}
			EXIT:
			color = WpfColors.Transparent;
			opacity = 0;
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

		internal static void MixStyle(SyntaxHighlight.StyleBase style, out FontStyle fontStyle, out GdiColor forecolor, out GdiColor backcolor) {
			forecolor = ThemeHelper.DocumentTextColor.Alpha(style.ForeColorOpacity);
			backcolor = ThemeHelper.DocumentPageColor.Alpha(style.BackColorOpacity);
			fontStyle = style.GetFontStyle();
			if (style.ClassificationType == null) {
				return;
			}
			var p = TextEditorHelper.DefaultClassificationFormatMap.GetRunProperties(style.ClassificationType);
			if (p == null) {
				return;
			}
			SolidColorBrush colorBrush;
			if (style.ForeColor.A == 0) {
				colorBrush = p.ForegroundBrushEmpty ? null : p.ForegroundBrush as SolidColorBrush;
				if (colorBrush != null) {
					forecolor = (style.ForeColorOpacity > 0 ? colorBrush.Color.Alpha(style.ForeColorOpacity) : colorBrush.Color).ToGdiColor();
				}
			}
			else if (style.ForeColorOpacity > 0) {
				forecolor = style.AlphaForeColor.ToGdiColor();
			}
			if (style.BackColor.A == 0) {
				colorBrush = p.BackgroundBrushEmpty ? null : p.BackgroundBrush as SolidColorBrush;
				if (colorBrush != null) {
					backcolor = (style.BackColorOpacity > 0 ? colorBrush.Color.Alpha(style.BackColorOpacity) : colorBrush.Color).ToGdiColor();
				}
			}
			else if (style.BackColorOpacity > 0) {
				backcolor = style.AlphaBackColor.ToGdiColor();
			}
			if (p.BoldEmpty == false && p.Bold && style.Bold != false) {
				fontStyle |= FontStyle.Bold;
			}
			if (p.ItalicEmpty == false && p.Italic && style.Italic != false) {
				fontStyle |= FontStyle.Italic;
			}
			if (p.TextDecorationsEmpty == false) {
				foreach (var decoration in p.TextDecorations) {
					if (decoration.Location == System.Windows.TextDecorationLocation.Underline && style.Underline != false) {
						fontStyle |= FontStyle.Underline;
					}
					else if (decoration.Location == System.Windows.TextDecorationLocation.Strikethrough && style.Strikethrough != false) {
						fontStyle |= FontStyle.Strikeout;
					}
				}
			}
		}

		internal static FontStyle GetFontStyle(this SyntaxHighlight.StyleBase activeStyle) {
			var f = FontStyle.Regular;
			if (activeStyle.Bold == true) {
				f |= FontStyle.Bold;
			}
			if (activeStyle.Italic == true) {
				f |= FontStyle.Italic;
			}
			if (activeStyle.Underline == true) {
				f |= FontStyle.Underline;
			}
			if (activeStyle.Strikethrough == true) {
				f |= FontStyle.Strikeout;
			}
			return f;
		}
	}
}
