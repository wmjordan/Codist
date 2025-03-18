using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CLR;
using Screen = System.Windows.Forms.Screen;
using VisualTreeHelper = System.Windows.Media.VisualTreeHelper;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfText = System.Windows.Media.FormattedText;

namespace Codist
{
	static partial class WpfHelper
	{
		static readonly string __DummyToolTip = String.Empty;

		internal const int IconRightMargin = 5;
		internal const int SmallMarginSize = 3;
		internal const double DimmedOpacity = 0.3;
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
		internal static readonly Thickness MiddleHorizontalMargin = new Thickness(6, 0, 6, 0);
		internal static readonly Thickness MiddleVerticalMargin = new Thickness(0, 6, 0, 6);
		internal static readonly Thickness MiddleTopMargin = new Thickness(0, 6, 0, 0);
		internal static readonly Thickness MiddleBottomMargin = new Thickness(0, 0, 0, 6);
		internal static readonly Thickness TinyBottomMargin = new Thickness(0, 0, 0, 1);
		internal static readonly Thickness MenuItemMargin = new Thickness(6, 0, 6, 0);

		#region TextBlock and Run
		public static TextBlock SetGlyph(this TextBlock block, int iconId) {
			return block.SetGlyph(VsImageHelper.GetImage(iconId));
		}
		public static TextBlock SetGlyph(this TextBlock block, ImageSource image) {
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
		public static TextBlock SetGlyph(this TextBlock block, FrameworkElement glyph) {
			var first = block.Inlines.FirstInline;
			glyph.Margin = GlyphMargin;
			var container = new InlineUIContainer(glyph) { BaselineAlignment = BaselineAlignment.TextTop };
			if (first != null) {
				block.Inlines.InsertBefore(first, container);
			}
			else {
				block.Inlines.Add(container);
			}
			return block;
		}
		public static TTextBlock AppendLine<TTextBlock>(this TTextBlock block)
			where TTextBlock : TextBlock {
			block.Inlines.Add(new LineBreak());
			return block;
		}
		public static TTextBlock AppendLineBreak<TTextBlock>(this TTextBlock block)
			where TTextBlock : TextBlock {
			if (block.Inlines.FirstInline != null) {
				block.Inlines.Add(new LineBreak());
			}
			return block;
		}
		public static InlineCollection AppendLineWithMargin(this InlineCollection inlines) {
			inlines.Add(new LineBreak());
			inlines.Add(new InlineUIContainer(new FrameworkElement { Width = 1, Height = 5 }));
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
			if (text != null) {
				block.Inlines.Add(new Run(text));
			}
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
			if (text != null) {
				block.Inlines.Add(Render(text, bold, italic, brush));
			}
			return block;
		}
		public static TTextBlock AppendLink<TTextBlock>(this TTextBlock block, string text, string uri, string toolTip, Brush foreground = null)
			where TTextBlock : TextBlock {
			var link = new Hyperlink(new Run(text)) { NavigateUri = new Uri(uri), ToolTip = toolTip }.ClickToNavigate();
			if (foreground != null) {
				link.Foreground = foreground;
			}
			block.Inlines.Add(link);
			return block;
		}
		public static TTextBlock AppendLink<TTextBlock>(this TTextBlock block, string text, Action<Hyperlink> clickHandler, string toolTip, Brush foreground = null)
			where TTextBlock : TextBlock {
			var link = new Hyperlink(new Run(text)) { ToolTip = toolTip }.ClickToNavigate(clickHandler);
			if (foreground != null) {
				link.Foreground = foreground;
			}
			block.Inlines.Add(link);
			return block;
		}
		public static TTextBlock AppendFileLink<TTextBlock>(this TTextBlock block, string file, string folder)
			where TTextBlock : TextBlock {
			block.Inlines.Add(new FileLink(folder, file));
			return block;
		}

		/// <summary>
		/// Gets the <see cref="TextBlock.Text"/> of a <see cref="TextBlock"/>, or the concatenated <see cref="Run.Text"/>s of <see cref="Run"/> instances in the <see cref="TextBlock.Inlines"/>.
		/// </summary>
		public static string GetText(this TextBlock text) {
			if (text.Inlines.Count == 0) {
				return text.Text;
			}
			using (var sbr = Microsoft.VisualStudio.Utilities.ReusableStringBuilder.AcquireDefault(50)) {
				var sb = sbr.Resource;
				GetInlinesText(text.Inlines, sb);
				return sb.ToString();
			}
		}

		static void GetInlinesText(InlineCollection inlines, System.Text.StringBuilder stringBuilder) {
			foreach (var inline in inlines) {
				if (inline is Run r) {
					stringBuilder.Append(r.Text);
				}
				else if (inline is Span s) {
					GetInlinesText(s.Inlines, stringBuilder);
				}
				else if (inline is InlineUIContainer c && c.Child is TextBlock tb) {
					GetInlinesText(tb.Inlines, stringBuilder);
				}
			}
		}

		public static TTextBlock WrapAtWidth<TTextBlock>(this TTextBlock text, double width)
			where TTextBlock : TextBlock {
			text.MaxWidth = width;
			text.TextWrapping = TextWrapping.Wrap;
			return text;
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
			hyperlink.Click += (s, args) => System.Diagnostics.Process.Start(((Hyperlink)s).NavigateUri.AbsoluteUri);
			return hyperlink;
		}
		public static Hyperlink ClickToNavigate(this Hyperlink hyperlink, Action<Hyperlink> clickHandler) {
			hyperlink.Click += (s, args) => clickHandler((Hyperlink)s);
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
		static readonly Typeface __StatusText = SystemFonts.StatusFontFamily.GetTypefaces().First();
		public static WpfText ToFormattedText(string text, double size, WpfBrush brush) {
			return new WpfText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, __StatusText, size, brush);
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
		public static TPanel Add<TPanel>(this TPanel panel, params UIElement[] controls)
			where TPanel : Panel {
			var c = panel.Children;
			foreach (var item in controls) {
				c.Add(item);
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
		public static StackPanel Stack(this UIElement[] elements, bool horizontal) {
			var p = new StackPanel();
			if (horizontal) {
				p.Orientation = Orientation.Horizontal;
			}
			return Add(p, elements);
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
			if (Config.Instance.QuickInfo.MaxHeight > 0) {
				element.MaxHeight = Config.Instance.QuickInfo.MaxHeight;
			}
			if (Config.Instance.QuickInfo.MaxWidth > 0) {
				element.MaxWidth = Config.Instance.QuickInfo.MaxWidth;
				if (element is TextBlock t && t.TextWrapping == TextWrapping.NoWrap) {
					t.TextWrapping = TextWrapping.Wrap;
				}
			}
			return element;
		}
		public static TElement BindWidthAsAncestor<TElement>(this TElement element, Type ancestor)
			where TElement : FrameworkElement {
			return element.Bind(FrameworkElement.WidthProperty, new Binding {
				RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, ancestor, 1),
				Path = new PropertyPath("ActualWidth")
			});
		}

		public static TElement ClearSpacing<TElement>(this TElement element)
			where TElement : Control {
			element.Margin = element.Padding = element.BorderThickness = NoMargin;
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
		public static Border WrapBorder(this UIElement element, WpfBrush borderBrush, Thickness borderThickness) {
			return new Border { BorderBrush = borderBrush, BorderThickness = borderThickness, Child = element };
		}
		#endregion

		#region WPF tree
		public static DependencyObject GetParent(this DependencyObject obj) {
			return obj is Visual ? VisualTreeHelper.GetParent(obj) : LogicalTreeHelper.GetParent(obj);
		}
		public static IEnumerable<TChild> GetDescendantChildren<TChild>(this DependencyObject obj, Predicate<TChild> predicate = null, Predicate<DependencyObject> precedeToChildren = null)
			where TChild : DependencyObject {
			if (obj == null) {
				yield break;
			}
			var count = VisualTreeHelper.GetChildrenCount(obj);
			for (int i = 0; i < count; i++) {
				var c = VisualTreeHelper.GetChild(obj, i);
				if (c is TChild r && (predicate == null || predicate(r))) {
					yield return r;
				}
				if (precedeToChildren == null || precedeToChildren(c)) {
					foreach (var item in c.GetDescendantChildren(predicate, precedeToChildren)) {
						yield return item;
					}
				}
			}
		}
		public static DependencyObject GetFirstVisualChild(this DependencyObject obj) {
			return VisualTreeHelper.GetChild(obj, 0);
		}
		public static TObject GetParentOrSelf<TObject>(this DependencyObject obj, Predicate<TObject> predicate = null)
			where TObject : DependencyObject {
			if (obj == null) {
				return null;
			}
			return obj is TObject o && (predicate == null || predicate(o)) ? o : obj.GetParent(predicate);
		}
		public static TParent GetParent<TParent>(this DependencyObject obj, Predicate<TParent> predicate = null)
			where TParent : DependencyObject {
			if (obj == null) {
				return null;
			}
			var p = obj;
			while ((p = p.GetParent()) != null) {
				if (p is TParent r && (predicate == null || predicate(r))) {
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
				if (c is TChild r && (predicate == null || predicate(r))) {
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
				if (c is TChild r && (predicate == null || predicate(r))) {
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
		public static void InheritStyle<TBaseControl>(this FrameworkElement control, ResourceDictionary resourceDictionary)
			where TBaseControl : FrameworkElement {
			control.Style = new Style(control.GetType(), resourceDictionary.Get<Style>(typeof(TBaseControl)));
		}
		public static ResourceDictionary MergeWith(this ResourceDictionary dictionary, ResourceDictionary resourceDictionary) {
			dictionary.MergedDictionaries.Add(resourceDictionary);
			return dictionary;
		}
		#endregion

		#region Others
		public static bool IsControlDown => (Keyboard.Modifiers & ModifierKeys.Control) != 0;
		public static bool IsShiftDown => (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
		public static void Toggle(this UIElement control, bool visible) {
			control.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
		}
		public static TElement SetOpacity<TElement>(this TElement control, double opacity)
			where TElement : UIElement {
			control.Opacity = opacity;
			return control;
		}

		public static TObj Set<TObj>(this TObj obj, ref TObj field) where TObj : System.Windows.Threading.DispatcherObject {
			return field = obj;
		}

		public static TDependencyObject SetValue<TDependencyObject, TValue>(this TDependencyObject obj, Action<TDependencyObject, TValue> setter, TValue value) where TDependencyObject : DependencyObject {
			setter(obj, value);
			return obj;
		}
		public static TDependencyObject SetProperty<TDependencyObject>(this TDependencyObject owner, DependencyProperty property, object value)
			where TDependencyObject : DependencyObject {
			owner.SetValue(property, value);
			return owner;
		}

		public static TElement HandleEvent<TElement>(this TElement control, RoutedEvent routedEvent, RoutedEventHandler handler) where TElement : UIElement {
			control.AddHandler(routedEvent, handler);
			return control;
		}

		public static TElement DetachEvent<TElement>(this TElement control, RoutedEvent routedEvent, RoutedEventHandler handler) where TElement : UIElement {
			control.RemoveHandler(routedEvent, handler);
			return control;
		}

		public static TElement Bind<TElement>(this TElement control, DependencyProperty dependency, string binding) where TElement : FrameworkElement {
			control.SetBinding(dependency, binding);
			return control;
		}
		public static TElement Bind<TElement>(this TElement control, DependencyProperty dependency, System.Windows.Data.BindingBase binding) where TElement : FrameworkElement {
			control.SetBinding(dependency, binding);
			return control;
		}

		public static TItem GetFirst<TItem>(this ItemCollection items, Predicate<TItem> predicate)
			where TItem : UIElement {
			foreach (var item in items) {
				if (item is TItem i && (predicate == null || predicate(i))) {
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
		/// <summary>
		/// Calls <see cref="IDisposable.Dispose"/> on each item in <paramref name="control"/> and empties the <see cref="ItemsControl"/>.
		/// </summary>
		public static void DisposeCollection(this ItemsControl control) {
			foreach (var item in control.Items) {
				if (item is IDisposable d) {
					d.Dispose();
				}
			}
			control.Items.Clear();
		}
		/// <summary>
		/// Calls <see cref="IDisposable.Dispose"/> on each item in <paramref name="items"/> and empties the <see cref="ItemsControl"/>.
		/// </summary>
		public static void DisposeCollection(this UIElementCollection items) {
			foreach (var item in items) {
				if (item is IDisposable d) {
					d.Dispose();
				}
			}
			items.Clear();
		}
		/// <summary>
		/// Calls <see cref="IDisposable.Dispose"/> on the item and then removes it from the collection at <paramref name="index"/>.
		/// </summary>
		public static void RemoveAndDisposeAt(this System.Collections.IList items, int index) {
			if (items[index] is IDisposable d) {
				d.Dispose();
			}
			items.RemoveAt(index);
		}
		/// <summary>
		/// Calls <see cref="IDisposable.Dispose"/> on <paramref name="item"/> and then removes it from the collection.
		/// </summary>
		public static void RemoveAndDispose(this System.Collections.IList items, object item) {
			int index;
			if ((index = items.IndexOf(item)) >= 0) {
				if (items[index] is IDisposable d) {
					d.Dispose();
				}
				items.RemoveAt(index);
			}
		}
		public static int ItemCount(this ListBox listBox) {
			return listBox.ItemContainerGenerator.Items.Count;
		}

		public static void ScrollToSelectedItem(this ListBox listBox) {
			if (listBox.SelectedItem == null) {
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
			listBox.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action<ListBox>(lb => lb.ScrollIntoView(lb.SelectedItem)), listBox);
		}

		public static bool GetFocus(this UIElement control) {
			return control.IsFocused || control.IsVisible && control.Focus();
		}

		public static TextBox SetOnVisibleSelectAll(this TextBox textBox) {
			textBox.IsVisibleChanged -= TextBox_VisibleSelectAll;
			textBox.IsVisibleChanged += TextBox_VisibleSelectAll;
			return textBox;

			void TextBox_VisibleSelectAll(object sender, DependencyPropertyChangedEventArgs e) {
				var b = sender as TextBox;
				if (b.IsVisible) {
					b.Focus();
					b.SelectAll();
				}
			}
		}

		public static TFreezable MakeFrozen<TFreezable>(this TFreezable freezable)
			where TFreezable : Freezable {
			freezable.Freeze();
			return freezable;
		}

		public static TItem Get<TItem>(this ResourceDictionary items, object key) {
			return (items?.Contains(key) == true && items[key] is TItem item)
				? item
				: default;
		}
		public static TItem? GetNullable<TItem>(this ResourceDictionary items, object key)
			where TItem : struct {
			return (items?.Contains(key) == true && items[key] is TItem item)
				? (TItem?)item
				: null;
		}
		public static void SetBrush(this ResourceDictionary resource, object brushKey, object colorKey, WpfBrush brush) {
			if (brush != null) {
				brush.Freeze();
				resource[brushKey] = brush;
				if (brush is SolidColorBrush c) {
					resource[colorKey] = c.Color;
				}
				else {
					resource.Remove(colorKey);
				}
			}
			else {
				resource.Remove(colorKey);
				resource.Remove(brushKey);
			}
		}
		public static void SetColor(this ResourceDictionary resource, object key, WpfColor color) {
			if (color.A != 0) {
				resource[key] = color;
			}
			else {
				resource.Remove(key);
			}
		}
		public static void SetValue<TStruct>(this ResourceDictionary resource, object key, TStruct? value)
			where TStruct : struct {
			if (value != null) {
				resource[key] = value.Value;
			}
			else {
				resource.Remove(key);
			}
		}
		public static void SetValue<TValue>(this ResourceDictionary resource, object key, TValue value) {
			if (Op.IsTrue(value)) {
				resource[key] = value;
			}
			else {
				resource.Remove(key);
			}
		}

		public static TElement NullIfMouseOver<TElement>(this TElement uiElement)
			where TElement : UIElement {
			return uiElement?.IsMouseOver == true ? null : uiElement;
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
			// see: https://stackoverflow.com/questions/1918877/how-can-i-get-the-dpi-in-wpf
			var source = PresentationSource.FromVisual(control);

			RenderTargetBitmap bmp;
			if (source != null) {
				var s1 = source.CompositionTarget.TransformToDevice.M11;
				var s2 = source.CompositionTarget.TransformToDevice.M22;
				bmp = new RenderTargetBitmap((int)(width * s1), (int)(height * s2), 96 * s1, 96 * s2, PixelFormats.Default);
			}
			else {
				bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Default);
			}
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
			item.ToolTip = __DummyToolTip;
			return item;
		}

		public static bool HasDummyToolTip(this FrameworkElement item) {
			return ReferenceEquals(item.ToolTip, __DummyToolTip);
		}

		public static TObject SetLazyToolTip<TObject>(this TObject item, Func<object> toolTipProvider)
			where TObject : FrameworkElement {
			item.ToolTip = __DummyToolTip;
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
		public static TObject SetLazyToolTip<TObject>(this TObject item, Func<TObject, object> toolTipProvider)
			where TObject : FrameworkElement {
			item.ToolTip = __DummyToolTip;
			item.ToolTipOpening += ShowLazyToolTip;
			return item;

			void ShowLazyToolTip(object sender, ToolTipEventArgs args) {
				var s = args.Source as TObject;
				var v = toolTipProvider(s);
				if (v is string t) {
					v = new TextBlock {
						Text = t
					}.LimitSize();
				}
				s.ToolTip = v;
				s.ToolTipOpening -= ShowLazyToolTip;
			}
		}

		public static ResourceDictionary Copy(this ResourceDictionary resources) {
			if (resources == null) {
				return null;
			}
			var copy = new ResourceDictionary();
			foreach (var key in resources.Keys) { copy[key] = resources[key]; }
			return copy;
		}

		public static string GetTypefaceName(this Typeface typeface) {
			var names = typeface.FaceNames;
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

		#region Screen size
		static Size __ScreenSize = GetMainWindowScreenSize();
		public static Size GetActiveScreenSize() {
			return __ScreenSize;
		}
		static Size GetMainWindowScreenSize() {
			var screen = Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(Application.Current.MainWindow).Handle);
			var size = screen.Bounds.Size;
			if (__ScreenSize.Width == 0) {
				Application.Current.MainWindow.LocationChanged += MainWindow_LocationChanged;
			}
			return new Size(size.Width, size.Height);
		}

		static void MainWindow_LocationChanged(object sender, EventArgs e) {
			__ScreenSize = GetMainWindowScreenSize();
		}
		#endregion

		abstract class InteractiveRun : Run
		{
			protected InteractiveRun() {
				MouseEnter += InitInteraction;
				Unloaded += Unload;
			}

			protected virtual WpfBrush HighlightBrush {
				get => SystemColors.HighlightBrush;
			}

			void InitInteraction(object sender, MouseEventArgs e) {
				MouseEnter -= InitInteraction;

				Cursor = Cursors.Hand;
				ToolTip = String.Empty;
				Highlight(sender, e);
				MouseEnter += Highlight;
				MouseLeave += Leave;

				OnInitInteraction();
			}

			protected virtual void OnInitInteraction() { }
			protected virtual void OnUnload() { }
			protected virtual object CreateToolTip() => null;

			protected void DoHighlight() {
				Background = HighlightBrush.Alpha(DimmedOpacity);
			}

			protected override void OnToolTipOpening(ToolTipEventArgs e) {
				base.OnToolTipOpening(e);
				if (ReferenceEquals(ToolTip, String.Empty)) {
					ToolTip = CreateToolTip();
				}
			}

			void Highlight(object sender, MouseEventArgs e) {
				DoHighlight();
			}

			void Leave(object sender, MouseEventArgs e) {
				Background = WpfBrushes.Transparent;
			}

			void Unload(object sender, RoutedEventArgs e) {
				//MouseEnter -= InitInteraction;
				//MouseEnter -= Highlight;
				//MouseLeave -= Leave;
				//Unloaded -= Unload;

				OnUnload();
			}
		}

		sealed class FileLink : InteractiveRun
		{
			public FileLink(string folder, string file) {
				Text = File = file;
				Folder = folder;
			}

			public string Folder { get; }
			public string File { get; }

			protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
				base.OnMouseLeftButtonDown(e);
				FileHelper.OpenInExplorer(Folder, File);
			}

			protected override object CreateToolTip() {
				return ToolTipHelper.CreateFileToolTip(Folder, File);
			}
		}

		static class InstalledFonts
		{
			static readonly InstalledFont[] __InstalledFonts = Init();

			public static IEnumerable<InstalledFont> All => __InstalledFonts;

			static InstalledFont[] Init() {
				var systemFonts = Fonts.SystemFontFamilies;
				var l = new InstalledFont[systemFonts.Count];
				var i = 0;
				foreach (var item in systemFonts) {
					l[i++] = new InstalledFont(item);
				}
				Array.Sort(l, (x, y) => x.Name.CompareTo(y.Name));
				return l;
			}
		}
	}
}
