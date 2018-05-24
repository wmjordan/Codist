using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using AppHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Text.Classification;
using GdiColor = System.Drawing.Color;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;
using WpfText = System.Windows.Media.FormattedText;

namespace Codist
{
	static class UIHelper
	{
		static readonly Thickness GlyphMargin = new Thickness(0, 0, 5, 0);
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
		public static TPanel Add<TPanel>(this TPanel panel, string text)
			where TPanel : Panel {
			panel.Children.Add(new TextBlock() { Text = text });
			return panel;
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
		public static TextBlock SetGlyph(this TextBlock block, System.Windows.Media.ImageSource image) {
			var first = block.Inlines.FirstInline;
			var glyph = new InlineUIContainer(new Image { Source = image, Width = image.Width, Margin = GlyphMargin }) { BaselineAlignment = BaselineAlignment.TextTop };
			if (first != null) {
				block.Inlines.InsertBefore(first, glyph);
			}
			else {
				block.Inlines.Add(glyph);
			}
			return block;
		}
		public static TextBlock Add(this TextBlock block, Inline inline) {
			block.Inlines.Add(inline);
			return block;
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
		public static TextBlock AddSymbolDisplayParts(this TextBlock block, ImmutableArray<SymbolDisplayPart> parts, SymbolFormatter formatter) {
			return formatter.ToUIText(block, parts, Int32.MinValue);
		}
		public static TextBlock AddSymbolDisplayParts(this TextBlock block, ImmutableArray<SymbolDisplayPart> parts, SymbolFormatter formatter, int argIndex) {
			return formatter.ToUIText(block, parts, argIndex);
		}
		public static TextBlock AddSymbol(this TextBlock block, ISymbol symbol, string alias, SymbolFormatter formatter) {
			formatter.ToUIText(block.Inlines, symbol, alias);
			return block;
		}
		public static TextBlock AddSymbol(this TextBlock block, ISymbol symbol, string alias, WpfBrush brush) {
			block.Inlines.Add(symbol.Render(alias, false, brush));
			return block;
		}
		public static TextBlock AddSymbol(this TextBlock block, ISymbol symbol, bool bold, WpfBrush brush) {
			block.Inlines.Add(symbol.Render(null, bold, brush));
			return block;
		}

		public static Run Render(this ISymbol symbol, string alias, WpfBrush brush) {
			return symbol.Render(alias, false, brush);
		}
		public static Run Render(this ISymbol symbol, string alias, bool bold, WpfBrush brush) {
			var run = Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ClickAndGo)
							? new SymbolLink(symbol, alias)
							: new Run(alias ?? symbol.Name);
			if (bold) {
				run.FontWeight = FontWeights.Bold;
			}
			if (brush != null) {
				run.Foreground = brush;
			}
			run.ToolTip = symbol.ToString();
			return run;
		}

		public static TextBlock AddText(this TextBlock block, string text, bool bold, bool italic, WpfBrush brush) {
			block.Inlines.Add(Render(text, bold, italic, brush));
			return block;
		}
		public static Run Render(this string text, WpfBrush brush) {
			return text.Render(false, false, brush);
		}
		public static Run Render(this string text, bool bold, bool italic, WpfBrush brush) {
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
			return run;
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
				//element.MouseLeftButtonUp += (s, args) => (s as TElement).MaxHeight = Double.PositiveInfinity;
			}
			if (Config.Instance.QuickInfoMaxWidth > 0) {
				element.MaxWidth = Config.Instance.QuickInfoMaxWidth;
				var t = element as TextBlock;
				if (t != null && t.TextWrapping == TextWrapping.NoWrap) {
					t.TextWrapping = TextWrapping.Wrap;
				}
			}
			return element;
		}
		public static ScrollViewer Scrollable<TElement>(this TElement element) {
			var t = element as TextBlock;
			if (t != null && t.TextWrapping == TextWrapping.NoWrap) {
				t.TextWrapping = TextWrapping.Wrap;
			}
			return new ScrollViewer {
				Content = element,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				Padding = new Thickness(0, 0, 3, 0)
			};
		}
		public static DependencyObject GetVisualParent(this DependencyObject obj) {
			return System.Windows.Media.VisualTreeHelper.GetParent(obj);
		}
		public static TParent GetVisualParent<TParent>(this DependencyObject obj)
			where TParent : DependencyObject {
			if (obj == null) {
				return null;
			}
			DependencyObject p = obj;
			TParent r;
			while ((p = p.GetVisualParent()) != null) {
				r = p as TParent;
				if (r != null) {
					return r;
				}
			}
			return null;
		}
		public static DependencyObject GetLogicalParent(this DependencyObject obj) {
			return LogicalTreeHelper.GetParent(obj);
		}
		public static ContextMenu CreateContextMenuForSourceLocations(ImmutableArray<SyntaxReference> refs) {
			var menu = new ContextMenu();
			foreach (var loc in refs) {
				var pos = loc.SyntaxTree.GetLineSpan(loc.Span);
				var item = new MenuItem { Header = System.IO.Path.GetFileName(loc.SyntaxTree.FilePath) + "(line: " + (pos.StartLinePosition.Line + 1).ToString() + ")", Tag = loc };
				item.Click += (s, args) => ((SyntaxReference)((MenuItem)s).Tag).GoToSource();
				menu.Items.Add(item);
			}
			return menu;
		}

		sealed class SymbolLink : Run
		{
			readonly ISymbol _Symbol;
			readonly ImmutableArray<SyntaxReference> _References;
			public SymbolLink(ISymbol symbol, string alias) {
				Text = alias ?? symbol.Name;
				_Symbol = symbol;
				_References = symbol.GetSourceLocations();
				if (_References.Length > 0) {
					Cursor = Cursors.Hand;
					MouseEnter += Highlight;
					MouseLeave += Leave;
					MouseLeftButtonUp += GotoSymbol;
				}
			}

			void Highlight(object sender, MouseEventArgs e) {
				Background = SystemColors.HighlightBrush.Alpha(0.3);
			}
			void Leave(object sender, MouseEventArgs e) {
				Background = WpfBrushes.Transparent;
			}

			void GotoSymbol(object sender, MouseButtonEventArgs e) {
				if (_References.Length == 1) {
					_References[0].GoToSource();
				}
				else {
					if (ContextMenu == null) {
						ContextMenu = CreateContextMenuForSourceLocations(_References);
					}
					ContextMenu.IsOpen = true;
				}
			}

		}
	}

}
