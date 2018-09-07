using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AppHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Classification;
using GdiColor = System.Drawing.Color;
using VisualTreeHelper = System.Windows.Media.VisualTreeHelper;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;
using WpfText = System.Windows.Media.FormattedText;

namespace Codist
{
	static class WpfHelper
	{
		static readonly Thickness GlyphMargin = new Thickness(0, 0, 5, 0);
		internal static readonly SymbolDisplayFormat QuickInfoSymbolDisplayFormat = new SymbolDisplayFormat(
			typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
			genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
			parameterOptions: SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeOptionalBrackets | SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeType,
			memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeContainingType,
			delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
			miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

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
				MinWidth = 180
			}.SetStyleResourceProperty(VsResourceKeys.TextBoxStyleKey));
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
		public static TextBlock AppendLine(this TextBlock block) {
			block.Inlines.Add(new LineBreak());
			return block;
		}
		public static TextBlock Append(this TextBlock block, Inline inline) {
			block.Inlines.Add(inline);
			return block;
		}
		public static TextBlock Append(this TextBlock block, string text) {
			block.Inlines.Add(new Run(text));
			return block;
		}
		public static TextBlock Append(this TextBlock block, string text, bool bold) {
			return block.Append(text, bold, false, null);
		}
		public static TextBlock Append(this TextBlock block, string text, WpfBrush brush) {
			return block.Append(text, false, false, brush);
		}
		public static TextBlock Append(this TextBlock block, string text, bool bold, bool italic, WpfBrush brush) {
			block.Inlines.Add(Render(text, bold, italic, brush));
			return block;
		}
		public static TextBlock AddSymbolDisplayParts(this TextBlock block, ImmutableArray<SymbolDisplayPart> parts, SymbolFormatter formatter) {
			return formatter.ToUIText(block, parts, Int32.MinValue);
		}
		public static TextBlock AddSymbolDisplayParts(this TextBlock block, ImmutableArray<SymbolDisplayPart> parts, SymbolFormatter formatter, int argIndex) {
			return formatter.ToUIText(block, parts, argIndex);
		}
		public static TextBlock AddSymbol(this TextBlock block, ISymbol symbol, string alias, SymbolFormatter formatter) {
			if (symbol != null) {
				formatter.ToUIText(block.Inlines, symbol, alias);
			}
			return block;
		}

		public static TextBlock AddSymbol(this TextBlock block, ISymbol symbol, string alias, WpfBrush brush) {
			if (symbol != null) {
				block.Inlines.Add(symbol.Render(alias, false, brush));
			}
			return block;
		}
		public static TextBlock AddSymbol(this TextBlock block, ISymbol symbol, bool bold, WpfBrush brush) {
			if (symbol != null) {
				block.Inlines.Add(symbol.Render(null, bold, brush));
			}
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
		public static Run Render(this ISymbol symbol, string alias, WpfBrush brush) {
			return symbol.Render(alias, brush == null, brush);
		}
		public static Run Render(this ISymbol symbol, string alias, bool bold, WpfBrush brush) {
			var run = new SymbolLink(symbol, alias, Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ClickAndGo));
			if (bold || brush == null) {
				run.FontWeight = FontWeights.Bold;
			}
			if (brush != null) {
				run.Foreground = brush;
			}
			return run;
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

		public static StackPanel MakeHorizontal(this StackPanel panel) {
			panel.Orientation = Orientation.Horizontal;
			return panel;
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
				var overflow = b.Template.FindName("OverflowGrid", b) as FrameworkElement;
				if (overflow != null) {
					overflow.Visibility = Visibility.Collapsed;
				}
				var mainPanelBorder = b.Template.FindName("MainPanelBorder", b) as FrameworkElement;
				if (mainPanelBorder != null) {
					mainPanelBorder.Margin = new Thickness(0);
				}
			}
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
		public static ScrollViewer Scrollable<TElement>(this TElement element)
			where TElement : DependencyObject {
			var t = element as TextBlock;
			if (t != null && t.TextWrapping == TextWrapping.NoWrap) {
				t.TextWrapping = TextWrapping.Wrap;
			}
			return new ScrollViewer {
				Content = element,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				Padding = new Thickness(0, 0, 3, 0)
			}.SetStyleResourceProperty(VsResourceKeys.GetScrollViewerStyleKey(true));
		}
		public static DependencyObject GetVisualParent(this DependencyObject obj) {
			return VisualTreeHelper.GetParent(obj);
		}
		public static DependencyObject GetFirstVisualChild(this DependencyObject obj) {
			return VisualTreeHelper.GetChild(obj, 0);
		}
		public static TParent GetVisualParent<TParent>(this DependencyObject obj)
			where TParent : DependencyObject {
			if (obj == null) {
				return null;
			}
			var p = obj;
			TParent r;
			while ((p = p.GetVisualParent()) != null) {
				r = p as TParent;
				if (r != null) {
					return r;
				}
			}
			return null;
		}
		public static TChild GetFirstVisualChild<TChild>(this DependencyObject obj)
			where TChild : DependencyObject {
			var count = VisualTreeHelper.GetChildrenCount(obj);
			for (int i = 0; i < count; i++) {
				var c = VisualTreeHelper.GetChild(obj, i);
				var r = c as TChild;
				if (r != null) {
					return r;
				}
				else {
					r = GetFirstVisualChild<TChild>(c);
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
		public static ContextMenu CreateContextMenuForSourceLocations(string symbolName, ImmutableArray<Location> refs) {
			var menu = new ContextMenu().SetStyleResourceProperty("EditorContextMenu");
			menu.Opened += (sender, e) => {
				var m = sender as ContextMenu;
				m.Items.Add(new MenuItem {
					Header = new TextBlock().Append(symbolName, true).Append(" is defined in ").Append(refs.Length.ToString(), true).Append(" places"),
					IsEnabled = false
				});
				foreach (var loc in refs.Sort(System.Collections.Generic.Comparer<Location>.Create((a,b) => {
					return String.Compare(System.IO.Path.GetFileName(a.SourceTree.FilePath), System.IO.Path.GetFileName(b.SourceTree.FilePath), StringComparison.OrdinalIgnoreCase);
				}))) {
					var pos = loc.SourceTree.GetLineSpan(loc.SourceSpan);
					var item = new MenuItem {
						Header = new TextBlock().Append(System.IO.Path.GetFileName(loc.SourceTree.FilePath)).Append("(line: " + (pos.StartLinePosition.Line + 1).ToString() + ")", WpfBrushes.Gray),
						Tag = loc
					};
					item.Click += (s, args) => ((Location)((MenuItem)s).Tag).GoToSource();
					m.Items.Add(item);
				}
			};
			return menu;
		}

		public static TControl SetStyleResourceProperty<TControl>(this TControl control, object resourceKey)
			where TControl : FrameworkElement {
			control.SetResourceReference(FrameworkElement.StyleProperty, resourceKey);
			return control;
		}

		sealed class SymbolLink : Run
		{
			readonly ISymbol _Symbol;
			ImmutableArray<Location> _References;
			public SymbolLink(ISymbol symbol, string alias, bool clickAndGo) {
				Text = alias ?? symbol.Name;
				_Symbol = symbol;
				if (clickAndGo) {
					MouseEnter += InitInteraction;
				}
				ToolTipOpening += ShowSymbolToolTip;
				ToolTip = String.Empty;
			}

			void InitInteraction(object sender, MouseEventArgs e) {
				MouseEnter -= InitInteraction;

				_References = _Symbol.GetSourceLocations();
				if (_References.Length > 0) {
					Cursor = Cursors.Hand;
					Highlight(sender, e);
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
						ContextMenu = CreateContextMenuForSourceLocations(_Symbol.MetadataName, _References);
					}
					ContextMenu.IsOpen = true;
				}
			}

			void ShowSymbolToolTip(object sender, ToolTipEventArgs e) {
				var tip = new TextBlock()
					.Append(_Symbol.GetAccessibility() + _Symbol.GetAbstractionModifier() + _Symbol.GetSymbolKindName() + " ")
					.Append(_Symbol.GetSignatureString(), true);
				ITypeSymbol t = _Symbol.ContainingType;
				if (t != null) {
					tip.Append("\n" + t.GetSymbolKindName() + ": ")
						.Append(t.ToDisplayString(QuickInfoSymbolDisplayFormat));
				}
				t = _Symbol.GetReturnType();
				if (t != null) {
					tip.Append("\nreturn value: " + t.ToDisplayString(QuickInfoSymbolDisplayFormat));
				}
				tip.Append("\nnamespace: " + _Symbol.ContainingNamespace?.ToString())
					.Append("\nassembly: " + _Symbol.GetAssemblyModuleName());
				var f = _Symbol as IFieldSymbol;
				if (f != null && f.IsConst) {
					tip.Append("\nconst: " + f.ConstantValue.ToString());
				}

				tip.TextWrapping = TextWrapping.Wrap;
				ToolTip = tip;
				ToolTipOpening -= ShowSymbolToolTip;
			}

		}
	}

}
