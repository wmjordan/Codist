using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using AppHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using GdiColor = System.Drawing.Color;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;
using WpfText = System.Windows.Media.FormattedText;

namespace Codist.Helpers
{
	static class InterfaceHelper
	{
		public static string GetClassificationType(this Type type, string field) {
			var f = type.GetField(field);
			var d = f.GetCustomAttribute<ClassificationTypeAttribute>();
			return d?.ClassificationTypeNames;
		}

		public static GdiColor Alpha(this GdiColor color, byte alpha) {
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
				b |= (l - '0');
			}
			else if (l >= 'A' && l <= 'F') {
				b |= (l - ('A' - 10));
			}
			else if (l >= 'a' && l <= 'f') {
				b |= (l - ('a' - 10));
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
		public static WpfColor Alpha(this WpfColor color, byte a) {
			return WpfColor.FromArgb(a, color.R, color.G, color.B);
		}
		public static TBrush Alpha<TBrush>(this TBrush brush, double opacity)
			where TBrush : WpfBrush {
			if (brush != null) {
				if (brush.IsFrozen) {
					brush = brush.Clone() as TBrush;
				}
				brush.Opacity = opacity;
			}
			return brush;
		}
		public static TPanel Add<TPanel>(this TPanel panel, UIElement control)
			where TPanel : Panel {
			panel.Children.Add(control);
			return panel;
		}
		public static TPanel AddReadOnlyTextBox<TPanel>(this TPanel panel, string text)
			where TPanel : Panel {
			panel.Children.Add(new TextBox {
				Text = text,
				IsReadOnly = true,
				TextAlignment = TextAlignment.Right,
				MinWidth = 180,
				BorderBrush = WpfBrushes.Transparent
			});
			return panel;
		}
		public static TextBlock AddText(this TextBlock block, string text) {
			block.Inlines.Add(new Run(text));
			return block;
		}
		public static TextBlock AddText(this TextBlock block, string text, bool bold) {
			return block.AddText(text, bold, false, null);
		}
		public static TextBlock AddText(this TextBlock block, string text, WpfBrush brush) {
			return block.AddText(text, false, false, brush);
		}
		public static TextBlock AddSymbol(this TextBlock block, ISymbol symbol, bool bold, WpfBrush brush) {
			var run = Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ClickAndGo)
				? new SymbolLink(symbol)
				: new Run(block.Name);
			if (bold) {
				run.FontWeight = FontWeights.Bold;
			}
			run.Foreground = brush;
			block.Inlines.Add(run);
			return block;
		}
		public static TextBlock AddText(this TextBlock block, string text, bool bold, bool italic, WpfBrush brush) {
			var run = new Run(text);
			if (bold) {
				run.FontWeight = FontWeights.Bold;
			}
			if (italic) {
				run.FontStyle = FontStyles.Italic;
			}
			if (brush != null) {
				run.Foreground = brush;
			}
			block.Inlines.Add(run);
			return block;
		}
		public static TPanel AddText<TPanel>(this TPanel parent, string text)
			where TPanel : Panel {
			return parent.AddText(text, false, false, null);
		}
		public static TPanel AddText<TPanel>(this TPanel parent, string text, bool bold)
			where TPanel : Panel {
			return parent.AddText(text, bold, false, null);
		}
		public static TPanel AddText<TPanel>(this TPanel parent, string text, bool bold, bool italic)
			where TPanel : Panel {
			return parent.AddText(text, bold, italic, null);
		}
		public static TPanel AddText<TPanel>(this TPanel parent, string text, WpfBrush brush)
			where TPanel : Panel {
			return parent.AddText(text, false, false, brush);
		}
		public static TPanel AddText<TPanel>(this TPanel parent, string text, bool bold, bool italic, WpfBrush foregroundBrush)
			where TPanel : Panel {
			var t = new TextBlock { Text = text };
			if (bold) {
				t.FontWeight = FontWeight.FromOpenTypeWeight(bold ? 800 : 400);
			}
			if (italic) {
				t.FontStyle = FontStyles.Italic;
			}
			if (foregroundBrush != null) {
				t.Foreground = foregroundBrush;
			}
			parent.Children.Add(t);
			return parent;
		}
		public static StackPanel MakeHorizontal(this StackPanel panel) {
			panel.Orientation = Orientation.Horizontal;
			return panel;
		}
		public static WpfBrush GetBrush(this IEditorFormatMap map, string formatName, string resourceId = EditorFormatDefinition.ForegroundBrushId) {
			var p = map.GetProperties(formatName);
			return p != null && p.Contains(resourceId)
				? (p[resourceId] as WpfBrush)
				: null;
		}

		static readonly System.Windows.Media.Typeface StatusText = SystemFonts.StatusFontFamily.GetTypefaces().First();
		public static WpfText ToFormattedText(string text, double size, WpfBrush brush) {
			return new WpfText(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, StatusText, size, brush);
		}
		public static WpfText SetItalic(this WpfText text) {
			text.SetFontStyle(FontStyles.Italic);
			return text;
		}
		public static WpfText SetBold(this WpfText text) {
			text.SetFontWeight(FontWeights.Bold);
			return text;
		}
		public static void ScreenShot(FrameworkElement control, string path) {
			var s = (control).RenderSize;
			var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap((int)s.Width, (int)s.Height, 96, 96, System.Windows.Media.PixelFormats.Default);
			bmp.Render(control);
			var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
			enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
			using (var f = System.IO.File.Create(path)) {
				enc.Save(f);
			}
		}
		public static TElement LimitSize<TElement>(this TElement element)
			where TElement : FrameworkElement {
			if (element == null) {
				return null;
			}
			if (Config.Instance.QuickInfoMaxHeight > 0) {
				element.MaxHeight = Config.Instance.QuickInfoMaxHeight;
			}
			if (Config.Instance.QuickInfoMaxWidth > 0) {
				element.MaxWidth = Config.Instance.QuickInfoMaxWidth;
			}
			return element;
		}

		sealed class SymbolLink : Run
		{
			ISymbol _Symbol;
			public SymbolLink(ISymbol symbol) {
				Text = symbol.Name;
				_Symbol = symbol;
				if (IsDefinedInCodeFile(symbol)) {
					Cursor = Cursors.Hand;
					MouseEnter += Highlight;
					MouseLeave += Leave;
					MouseLeftButtonUp += GotoSymbol;
				}
			}

			internal static bool IsDefinedInCodeFile(ISymbol symbol) {
				return symbol != null && symbol.DeclaringSyntaxReferences.Length > 0 && symbol.DeclaringSyntaxReferences[0].SyntaxTree.FilePath != null;
			}

			void Highlight(object sender, MouseEventArgs e) {
				Background = SystemColors.HighlightBrush.Alpha(0.3);
			}
			void Leave(object sender, MouseEventArgs e) {
				Background = WpfBrushes.Transparent;
			}

			void GotoSymbol(object sender, MouseButtonEventArgs e) {
				_Symbol.GoToSymbol();
			}
		}
	}

}
