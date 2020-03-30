using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VisualTreeHelper = System.Windows.Media.VisualTreeHelper;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfText = System.Windows.Media.FormattedText;
using System.Collections.Generic;

namespace Codist
{
	static partial class WpfHelper
	{
		static readonly string DummyToolTip = String.Empty;
		internal const int IconRightMargin = 5;
		internal const int SmallMarginSize = 3;
		internal static readonly WpfColor EmptyColor = new WpfColor();
		internal static readonly Thickness NoMargin = new Thickness(0);
		internal static readonly Thickness TinyMargin = new Thickness(1);
		internal static readonly Thickness SmallMargin = new Thickness(SmallMarginSize);
		internal static readonly Thickness MiddleMargin = new Thickness(6);
		internal static readonly Thickness GlyphMargin = new Thickness(0, 0, IconRightMargin, 0);
		internal static readonly Thickness ScrollerMargin = new Thickness(0, 0, 3, 0);
		internal static readonly Thickness TopItemMargin = new Thickness(0, 3, 0, 0);
		internal static readonly Thickness SmallHorizontalMargin = new Thickness(SmallMarginSize, 0, SmallMarginSize, 0);
		internal static readonly Thickness SmallVerticalMargin = new Thickness(0, SmallMarginSize, 0, SmallMarginSize);
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
		public static TTextBlock AppendLine<TTextBlock>(this TTextBlock block, bool withMargin)
			where TTextBlock : TextBlock {
			if (withMargin) {
				block.Inlines.AppendLineWithMargin();
			}
			else {
				block.Inlines.Add(new LineBreak());
			}
			return block;
		}
		public static InlineCollection AppendLineWithMargin(this InlineCollection inlines) {
			inlines.Add(new LineBreak());
			inlines.Add(new InlineUIContainer(new Border { Width = 1, Height = 3 }));
			inlines.Add(new LineBreak());
			return inlines;
		}
		public static TTextBlock Clear<TTextBlock>(this TTextBlock block)
			where TTextBlock : TextBlock {
			block.Inlines.Clear();
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
		public static TTextBlock Append<TTextBlock>(this TTextBlock block, int value)
			where TTextBlock : TextBlock {
			block.Inlines.Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
			return block;
		}
		public static Paragraph Append(this Paragraph block, string text) {
			block.Inlines.Add(text);
			return block;
		}
		public static Paragraph Append(this Paragraph block, string text, bool bold) {
			block.Inlines.Add(Render(text, bold, false, null));
			return block;
		}
		public static Paragraph Append(this Paragraph block, string text, bool bold, WpfBrush brush) {
			block.Inlines.Add(Render(text, bold, false, brush));
			return block;
		}
		public static Paragraph Append(this Paragraph block, Inline inline) {
			block.Inlines.Add(inline);
			return block;
		}
		public static Paragraph Append(this Paragraph block, UIElement element) {
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
			var run = new Run(text);
			if (brush != null) {
				run.Foreground = brush;
			}
			return run;
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

		public static Hyperlink ClickToNavigate(this Hyperlink hyperlink) {
			hyperlink.Click += (s, args) => { System.Diagnostics.Process.Start(((Hyperlink)s).NavigateUri.AbsoluteUri); };
			return hyperlink;
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
		public static TPanel Add<TPanel>(this TPanel panel, Func<UIElement, UIElement> template, params UIElement[] controls)
			where TPanel : Panel {
			foreach (var item in controls) {
				panel.Children.Add(template(item));
			}
			return panel;
		}
		public static StackPanel MakeHorizontal(this StackPanel panel) {
			panel.Orientation = Orientation.Horizontal;
			return panel;
		}
		public static TPanel ForEachChild<TPanel, TChild>(this TPanel panel, Action<TChild> action)
			where TPanel : Panel {
			foreach (object item in panel.Children) {
				if (item is TChild c) {
					action(c);
				}
			}
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
		public static TElement ClearBorder<TElement>(this TElement element)
			where TElement : Control {
			element.BorderThickness = NoMargin;
			return element;
		}
		public static TElement ClearMargin<TElement>(this TElement element)
			where TElement : FrameworkElement {
			element.Margin = NoMargin;
			return element;
		}
		public static TElement Collapse<TElement>(this TElement element)
			where TElement : UIElement {
			if (element != null) {
				element.Visibility = Visibility.Collapsed;
			}
			return element;
		}
		public static TElement ToggleVisibility<TElement>(this TElement element, bool visible)
			where TElement : UIElement {
			element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
			return element;
		}
		public static TElement WrapMargin<TElement>(this TElement element, Thickness thickness)
			where TElement : FrameworkElement {
			element.Margin = thickness;
			return element;
		}
		public static Border WrapBorder(this UIElement element, Thickness thickness) {
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
		public static TObject GetParentOrSelf<TObject>(this DependencyObject obj, Predicate<TObject> predicate = null)
			where TObject : DependencyObject {
			if (obj == null) {
				return null;
			}
			var o = obj as TObject;
			return o != null && (predicate == null || predicate(o)) ? o : obj.GetParent<TObject>(predicate);
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
			if (obj == null) {
				return null;
			}
			var count = VisualTreeHelper.GetChildrenCount(obj);
			for (int i = 0; i < count; i++) {
				var c = VisualTreeHelper.GetChild(obj, i);
				var r = c as TChild;
				if (r != null && (predicate == null || predicate(r))) {
					return r;
				}
				r = GetFirstVisualChild(c, predicate);
				if (r != null) {
					return r;
				}
			}
			return null;
		}
		public static TChild GetLastVisualChild<TChild>(this DependencyObject obj, Predicate<TChild> predicate = null)
			where TChild : DependencyObject {
			if (obj == null) {
				return null;
			}
			var count = VisualTreeHelper.GetChildrenCount(obj);
			for (int i = count - 1; i >= 0; i--) {
				var c = VisualTreeHelper.GetChild(obj, i);
				var r = c as TChild;
				if (r != null && (predicate == null || predicate(r))) {
					return r;
				}
				r = GetLastVisualChild(c, predicate);
				if (r != null) {
					return r;
				}
			}
			return null;
		}

		public static DependencyObject GetLogicalParent(this DependencyObject obj) {
			return obj != null ? LogicalTreeHelper.GetParent(obj) : null;
		}

		public static bool OccursOn<TObject>(this RoutedEventArgs args) where TObject : DependencyObject {
			return (args.OriginalSource as DependencyObject).GetParent<TObject>() != null;
		}

		public static TDependencyObject ClearValues<TDependencyObject>(this TDependencyObject obj, params DependencyProperty[] dependencies) where TDependencyObject : DependencyObject {
			foreach (var item in dependencies) {
				obj.ClearValue(item);
			}
			return obj;
		}
		public static TDependencyObject SetValue<TDependencyObject, TValue>(this TDependencyObject obj, Action<TDependencyObject, TValue> setter, TValue value) where TDependencyObject : DependencyObject {
			setter(obj, value);
			return obj;
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
		public static ResourceDictionary MergeWith(this ResourceDictionary dictionary, ResourceDictionary resourceDictionary) {
			dictionary.MergedDictionaries.Add(resourceDictionary);
			return dictionary;
		}
		#endregion

		#region Others
		public static TItem GetFirst<TItem>(this ItemCollection items, Predicate<TItem> predicate)
			where TItem : UIElement {
			foreach (var item in items) {
				var i = item as TItem;
				if (i != null && (predicate == null || predicate(i))) {
					return i;
				}
			}
			return null;
		}
		public static void AddRange(this ItemCollection items, IEnumerable<object> objects) {
			foreach (var item in objects) {
				items.Add(item);
			}
		}
		public static bool FocusFirst<TItem>(this ItemCollection items)
			where TItem : UIElement {
			foreach (var item in items) {
				if ((item as TItem)?.Focus() == true) {
					return true;
				}
			}
			return false;
		}
		public static bool FocusLast<TItem>(this ItemCollection items)
			where TItem : UIElement {
			for (int i = items.Count - 1; i >= 0; i--) {
				if ((items[i] as TItem)?.Focus() == true) {
					return true;
				}
			}
			return false;
		}
		public static int ItemCount(this ListBox listBox) {
			return listBox.ItemContainerGenerator.Items.Count;
		}

		public static void ScrollToSelectedItem(this ListBox listBox) {
			if (listBox.SelectedIndex == -1) {
				return;
			}
			try {
				listBox.UpdateLayout();
			}
			catch (InvalidOperationException) {
				// ignore
#if DEBUG
				throw;
#endif
			}
			listBox.ScrollIntoView(listBox.ItemContainerGenerator.Items[listBox.SelectedIndex]);
		}

		public static bool GetFocus(this UIElement control) {
			if (control.IsFocused) {
				return true;
			}
			if (control.IsVisible) {
				return control.Focus();
			}
			return false;
		}

		public static TextBox SetOnVisibleSelectAll(this TextBox textBox) {
			textBox.IsVisibleChanged -= TextBox_VisibleSelectAll;
			textBox.IsVisibleChanged += TextBox_VisibleSelectAll;
			return textBox;
		}

		static void TextBox_VisibleSelectAll(object sender, DependencyPropertyChangedEventArgs e) {
			var b = sender as TextBox;
			if (b.IsVisible) {
				b.Focus();
				b.SelectAll();
			}
		}

		public static TItem Get<TItem>(this ResourceDictionary items, object key) {
			return (items != null && items.Contains(key) && items[key] is TItem item)
				? item
				: default;
		}
		public static TItem? GetNullable<TItem>(this ResourceDictionary items, object key)
			where TItem : struct {
			return (items != null && items.Contains(key) && items[key] is TItem item)
				? (TItem?)item
				: null;
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
				b.FindTemplateElement<FrameworkElement>("OverflowGrid").Collapse();
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

		public static IEnumerable<InstalledFont> GetInstalledFonts() {
			return InstalledFonts.All;
		}

		public static TObject UseDummyToolTip<TObject>(this TObject item)
			where TObject : FrameworkElement {
			item.ToolTip = DummyToolTip;
			return item;
		}

		public static bool HasDummyToolTip(this FrameworkElement item) {
			return ReferenceEquals(item.ToolTip, DummyToolTip);
		}

		public static TObject SetLazyToolTip<TObject>(this TObject item, Func<object> toolTipProvider)
			where TObject : FrameworkElement {
			item.ToolTip = DummyToolTip;
			item.ToolTipOpening += ShowLazyToolTip;
			return item;

			void ShowLazyToolTip(object sender, ToolTipEventArgs args) {
				var s = args.Source as TObject;
				var v = toolTipProvider();
				if (v is string t) {
					v = new TextBlock {
						Text = t
					}.LimitSize();
				}
				s.ToolTip = v;
				s.ToolTipOpening -= ShowLazyToolTip;
			}
		}

		public static string GetTypefaceAdjustedName(this FamilyTypeface typeface) {
			var names = typeface.AdjustedFaceNames;
			if (names.Count == 1) {
				return names.First().Value;
			}
			if (names.Count == 0) {
				return "Regular";
			}
			foreach (var item in names) {
				if (String.Equals(item.Key.IetfLanguageTag, InstalledFont.SystemLang, StringComparison.OrdinalIgnoreCase)) {
					return item.Value;
				}
			}
			return names.First().Value;
		}

		public static bool IsStandardStyle(this FamilyTypeface typeface) {
			return (typeface.Weight == FontWeights.Normal || typeface.Weight == FontWeights.Bold)
				 && (typeface.Style == FontStyles.Normal || typeface.Style == FontStyles.Italic || typeface.Style == FontStyles.Oblique)
				 && (typeface.Stretch == FontStretches.Normal);
		}
		#endregion

		static class InstalledFonts
		{
			static readonly InstalledFont[] _InstalledFonts = Init();

			public static IEnumerable<InstalledFont> All => _InstalledFonts;

			static InstalledFont[] Init() {
				var l = new List<InstalledFont>(100);
				foreach (var item in Fonts.SystemFontFamilies) {
					l.Add(new InstalledFont(item));
				}
				l.Sort((x, y) => x.Name.CompareTo(y.Name));
				return l.ToArray();
			}
		}
	}

	sealed class InstalledFont
	{
		internal static readonly string SystemLang = System.Globalization.CultureInfo.CurrentCulture.IetfLanguageTag;

		public InstalledFont(FontFamily font) {
			Font = font;
			foreach (var item in Font.FamilyNames) {
				if (String.Equals(item.Key.IetfLanguageTag, SystemLang, StringComparison.OrdinalIgnoreCase)) {
					Name = item.Value;
					break;
				}
			}
			if (Name == null) {
				Name = Font.Source;
			}
			ExtraTypefaces = font.FamilyTypefaces
				.Where(i => i.IsStandardStyle() == false)
				.ToArray();
		}
		public FontFamily Font { get; }
		public string Name { get; }
		public FamilyTypeface[] ExtraTypefaces { get; }
		public override string ToString() {
			return Name;
		}
	}
}
