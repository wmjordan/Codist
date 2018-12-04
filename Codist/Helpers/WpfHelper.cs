using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using VisualTreeHelper = System.Windows.Media.VisualTreeHelper;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfText = System.Windows.Media.FormattedText;

namespace Codist
{
	static partial class WpfHelper
	{
		internal static readonly WpfColor EmptyColor = new WpfColor();
		internal static readonly Thickness NoMargin = new Thickness(0);
		internal static readonly Thickness TinyMargin = new Thickness(1);
		internal static readonly Thickness SmallMargin = new Thickness(3);
		internal static readonly Thickness MiddleMargin = new Thickness(6);
		internal static readonly Thickness GlyphMargin = new Thickness(0, 0, 5, 0);
		internal static readonly Thickness ScrollerMargin = new Thickness(0, 0, 3, 0);
		internal static readonly Thickness TopItemMargin = new Thickness(0, 3, 0, 0);
		internal static readonly Thickness SmallHorizontalMargin = new Thickness(3, 0, 3, 0);
		internal static readonly Thickness MenuItemMargin = new Thickness(6, 0, 6, 0);

		#region TextBlock and Run
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
		public static TextBlock SetGlyph(this TextBlock block, Image image) {
			var first = block.Inlines.FirstInline;
			image.Margin = GlyphMargin;
			var glyph = new InlineUIContainer(image) { BaselineAlignment = BaselineAlignment.TextTop };
			if (first != null) {
				block.Inlines.InsertBefore(first, glyph);
			}
			else {
				block.Inlines.Add(glyph);
			}
			return block;
		}
		public static TTextBlock AppendLine<TTextBlock>(this TTextBlock block)
			where TTextBlock : TextBlock {
			block.Inlines.Add(new LineBreak());
			return block;
		}
		public static TTextBlock Append<TTextBlock>(this TTextBlock block, Inline inline)
			where TTextBlock : TextBlock {
			block.Inlines.Add(inline);
			return block;
		}
		public static TTextBlock Append<TTextBlock>(this TTextBlock block, UIElement element)
			where TTextBlock : TextBlock {
			block.Inlines.Add(new InlineUIContainer(element) { BaselineAlignment = BaselineAlignment.TextTop });
			return block;
		}
		public static TTextBlock Append<TTextBlock>(this TTextBlock block, string text)
			where TTextBlock : TextBlock {
			block.Inlines.Add(new Run(text));
			return block;
		}
		public static TTextBlock Append<TTextBlock>(this TTextBlock block, string text, bool bold)
			where TTextBlock : TextBlock {
			return block.Append(text, bold, false, null);
		}
		public static TTextBlock Append<TTextBlock>(this TTextBlock block, string text, WpfBrush brush)
			where TTextBlock : TextBlock {
			return block.Append(text, false, false, brush);
		}
		public static TTextBlock Append<TTextBlock>(this TTextBlock block, string text, bool bold, bool italic, WpfBrush brush)
			where TTextBlock : TextBlock {
			block.Inlines.Add(Render(text, bold, italic, brush));
			return block;
		}

		/// <summary>
		/// Gets the <see cref="TextBlock.Text"/> of a <see cref="TextBlock"/>, or the concatenated <see cref="Run.Text"/>s of <see cref="Run"/> instances in the <see cref="TextBlock.Inlines"/>.
		/// </summary>
		public static string GetText(this TextBlock block) {
			if (block.Inlines.Count == 0) {
				return block.Text;
			}
			using (var sbr = Microsoft.VisualStudio.Utilities.ReusableStringBuilder.AcquireDefault(50)) {
				var sb = sbr.Resource;
				foreach (var inline in block.Inlines) {
					sb.Append((inline as Run)?.Text);
				}
				return sb.ToString();
			}
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
		#endregion

		#region FormattedText
		public static WpfText SetItalic(this WpfText text) {
			text.SetFontStyle(FontStyles.Italic);
			return text;
		}
		public static WpfText SetBold(this WpfText text) {
			text.SetFontWeight(FontWeights.Bold);
			return text;
		}
		static readonly System.Windows.Media.Typeface StatusText = SystemFonts.StatusFontFamily.GetTypefaces().First();
		public static WpfText ToFormattedText(string text, double size, WpfBrush brush) {
			return new WpfText(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, StatusText, size, brush);
		}
		#endregion

		#region Panel
		public static TPanel Add<TPanel>(this TPanel panel, UIElement control)
			where TPanel : Panel {
			panel.Children.Add(control);
			return panel;
		}
		public static StackPanel MakeHorizontal(this StackPanel panel) {
			panel.Orientation = Orientation.Horizontal;
			return panel;
		} 
		#endregion

		#region Margin and size
		public static bool Contains(this FrameworkElement element, Point point) {
			return point.X >= 0 && point.X <= element.ActualWidth
				&& point.Y >= 0 && point.Y <= element.ActualHeight;
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
				var t = element as TextBlock;
				if (t != null && t.TextWrapping == TextWrapping.NoWrap) {
					t.TextWrapping = TextWrapping.Wrap;
				}
			}
			return element;
		}
		public static TElement WrapMargin<TElement>(this TElement element, Thickness thickness)
			where TElement : FrameworkElement {
			element.Margin = thickness;
			return element;
		}
		public static Border WrapMargin(this UIElement element, Thickness thickness) {
			return new Border { Margin = thickness, Child = element };
		}
		#endregion
		#region WPF tree
		public static DependencyObject GetParent(this DependencyObject obj) {
			return obj is System.Windows.Media.Visual ? VisualTreeHelper.GetParent(obj) : LogicalTreeHelper.GetParent(obj);
		}
		public static DependencyObject GetFirstVisualChild(this DependencyObject obj) {
			return VisualTreeHelper.GetChild(obj, 0);
		}
		public static TParent GetParent<TParent>(this DependencyObject obj, Predicate<TParent> predicate = null)
			where TParent : DependencyObject {
			if (obj == null) {
				return null;
			}
			var p = obj;
			TParent r;
			while ((p = p.GetParent()) != null) {
				r = p as TParent;
				if (r != null && (predicate == null || predicate(r))) {
					return r;
				}
			}
			return null;
		}
		public static TChild GetFirstVisualChild<TChild>(this DependencyObject obj, Predicate<TChild> predicate = null)
			where TChild : DependencyObject {
			var count = VisualTreeHelper.GetChildrenCount(obj);
			for (int i = 0; i < count; i++) {
				var c = VisualTreeHelper.GetChild(obj, i);
				var r = c as TChild;
				if (r != null && (predicate == null || predicate(r))) {
					return r;
				}
				else {
					r = GetFirstVisualChild(c, predicate);
					if (r != null) {
						return r;
					}
				}
			}
			return null;
		}
		public static DependencyObject GetLogicalParent(this DependencyObject obj) {
			return LogicalTreeHelper.GetParent(obj);
		}
		#endregion

		#region Template and style
		public static string GetTemplate(this Control element) {
			if (element.Template == null) {
				return String.Empty;
			}
			using (var r = Microsoft.VisualStudio.Utilities.ReusableStringBuilder.AcquireDefault(30))
			using (var writer = System.Xml.XmlWriter.Create(r.Resource, new System.Xml.XmlWriterSettings { Indent = true, IndentChars = "\t" })) {
				System.Windows.Markup.XamlWriter.Save(element.Template, writer);
				return r.Resource.ToString();
			}
		}
		public static TElement FindTemplateElement<TElement>(this Control control, string name)
			where TElement : FrameworkElement {
			return control.Template.FindName(name, control) as TElement;
		}
		public static ResourceDictionary LoadComponent(string uri) {
			return (ResourceDictionary)Application.LoadComponent(new Uri("/" + nameof(Codist) + ";component/" + uri, UriKind.Relative));
		}
		public static TControl ReferenceProperty<TControl>(this TControl control, DependencyProperty dependency, object resourceKey)
			where TControl : FrameworkElement {
			control.SetResourceReference(dependency, resourceKey);
			return control;
		}
		public static TControl ReferenceStyle<TControl>(this TControl control, object resourceKey)
			where TControl : FrameworkElement {
			control.SetResourceReference(FrameworkElement.StyleProperty, resourceKey);
			return control;
		}
		#endregion

		#region Others
		public static TItem Get<TItem>(this ResourceDictionary items, object key) {
			return (items != null && items.Contains(key) && items[key] is TItem item)
				? item
				: default;
		}
		public static ToolBar HideOverflow(this ToolBar toolBar) {
			if (toolBar.IsLoaded) {
				HideOverflowInternal(toolBar);
				return toolBar;
			}
			toolBar.Loaded -= ToolBarLoaded;
			toolBar.Loaded += ToolBarLoaded;
			return toolBar;
			void ToolBarLoaded(object sender, RoutedEventArgs args) {
				var b = sender as ToolBar;
				HideOverflowInternal(b);
				b.Loaded -= ToolBarLoaded;
			}
			void HideOverflowInternal(ToolBar b) {
				var overflow = b.FindTemplateElement<FrameworkElement>("OverflowGrid");
				if (overflow != null) {
					overflow.Visibility = Visibility.Collapsed;
				}
				var mainPanelBorder = b.FindTemplateElement<FrameworkElement>("MainPanelBorder");
				if (mainPanelBorder != null) {
					mainPanelBorder.Margin = NoMargin;
				}
			}
		}

		public static void ScreenShot(FrameworkElement control, string path, int width, int height) {
			var bmp = new RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Default);
			//var sourceBrush = new System.Windows.Media.VisualBrush(control) { Stretch = System.Windows.Media.Stretch.None };
			//var drawingVisual = new System.Windows.Media.DrawingVisual();
			//using (var dc = drawingVisual.RenderOpen()) {
			//	dc.DrawRectangle(sourceBrush, null, new Rect(0, 0, width, height));
			//	dc.Close();
			//}
			//bmp.Render(drawingVisual);
			bmp.Render(control);
			var enc = new PngBitmapEncoder();
			enc.Frames.Add(BitmapFrame.Create(bmp));
			using (var f = System.IO.File.Create(path)) {
				enc.Save(f);
			}
		}

		#endregion
	}

}
