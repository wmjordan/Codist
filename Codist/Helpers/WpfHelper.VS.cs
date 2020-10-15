using System;
using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using AppHelpers;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Classification;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;

namespace Codist
{
	partial class WpfHelper
	{
		public static TPanel AddReadOnlyTextBox<TPanel>(this TPanel panel, string text, bool alignLeft = false)
		where TPanel : Panel {
			panel.Children.Add(new QuickInfoTextBox {
				Text = text,
				TextAlignment = alignLeft ? TextAlignment.Left : TextAlignment.Right,
				MinWidth = 180,
				MaxWidth = Config.Instance.QuickInfoMaxWidth > 180 ? Config.Instance.QuickInfoMaxWidth - 100 : 180,
				TextWrapping = TextWrapping.Wrap,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto
			});
			return panel;
		}
		public static TextBlock AddParameters(this TextBlock block, ImmutableArray<IParameterSymbol> parameters, SymbolFormatter formatter) {
			var inlines = block.Inlines;
			inlines.Add("(");
			for (var i = 0; i < parameters.Length; i++) {
				if (i > 0) {
					inlines.Add(", ");
				}
				var p = parameters[i];
				if (p.IsOptional) {
					inlines.Add("[");
				}
				AddParameterModifier(formatter, inlines, p);
				formatter.Format(inlines, p.Type, null, false);
				if (p.IsOptional) {
					inlines.Add("]");
				}
			}
			inlines.Add(")");
			return block;
		}
		public static TextBlock AddParameters(this TextBlock block, ImmutableArray<IParameterSymbol> parameters, SymbolFormatter formatter, int argIndex) {
			var inlines = block.Inlines;
			inlines.Add("(");
			for (var i = 0; i < parameters.Length; i++) {
				if (i > 0) {
					inlines.Add(", ");
				}
				var p = parameters[i];
				if (p.IsOptional) {
					inlines.Add("[");
				}
				AddParameterModifier(formatter, inlines, p);
				formatter.Format(inlines, p.Type, null, false);
				inlines.Add(" ");
				inlines.Add(p.Render(null, i == argIndex, formatter.Parameter));
				if (p.IsOptional) {
					inlines.Add("]");
				}
			}
			inlines.Add(")");
			return block;
		}

		static void AddParameterModifier(SymbolFormatter formatter, InlineCollection inlines, IParameterSymbol p) {
			switch (p.RefKind) {
				case RefKind.Ref:
					inlines.Add(new Run("ref ") {
						Foreground = formatter.Keyword
					});
					return;
				case RefKind.Out:
					inlines.Add(new Run("out ") {
						Foreground = formatter.Keyword
					});
					return;
				case RefKind.In:
					inlines.Add(new Run("in ") {
						Foreground = formatter.Keyword
					});
					return;
			}
			if (p.IsParams) {
				inlines.Add(new Run("params ") {
					Foreground = formatter.Keyword
				});
			}
		}

		public static TextBlock AddSymbol(this TextBlock block, ISymbol symbol, string alias, bool bold, SymbolFormatter formatter) {
			if (symbol != null) {
				formatter.Format(block.Inlines, symbol, alias, bold);
			}
			return block;
		}
		public static TextBlock AddSymbol(this TextBlock block, ISymbol symbol, bool bold, SymbolFormatter formatter) {
			if (symbol != null) {
				formatter.Format(block.Inlines, symbol, null, bold);
			}
			return block;
		}
		public static TextBlock AddSymbol(this TextBlock block, ISymbol symbol, string alias, SymbolFormatter formatter) {
			if (symbol != null) {
				formatter.Format(block.Inlines, symbol, alias, false);
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
		public static TextBlock AddSymbolDisplayParts(this TextBlock block, ImmutableArray<SymbolDisplayPart> parts, SymbolFormatter formatter) {
			return formatter.Format(block, parts, Int32.MinValue);
		}
		public static TextBlock AddSymbolDisplayParts(this TextBlock block, ImmutableArray<SymbolDisplayPart> parts, SymbolFormatter formatter, int argIndex) {
			return formatter.Format(block, parts, argIndex);
		}
		public static TextBlock AddXmlDoc(this TextBlock paragraph, XElement content, XmlDocRenderer docRenderer) {
			docRenderer.Render(content, paragraph.Inlines);
			return paragraph;
		}

		public static double? GetFontSize(this ResourceDictionary resource) {
			return resource.GetNullable<double>(Constants.EditorFormatKeys.FontRenderingSize);
		}
		public static bool? GetItalicStyle(this ResourceDictionary resource) {
			return resource.GetNullable<bool>(Constants.EditorFormatKeys.IsItalic);
		}
		public static bool? GetBoldStyle(this ResourceDictionary resource) {
			return resource.GetNullable<bool>(Constants.EditorFormatKeys.IsBold);
		}
		public static double? GetOpacity(this ResourceDictionary resource) {
			return resource.GetNullable<double>(Constants.EditorFormatKeys.ForegroundOpacity);
		}
		public static double? GetBackgroundOpacity(this ResourceDictionary resource) {
			return resource.GetNullable<double>(Constants.EditorFormatKeys.BackgroundOpacity);
		}
		public static WpfBrush GetBrush(this ResourceDictionary resource, string resourceId = EditorFormatDefinition.ForegroundBrushId) {
			return resource.Get<WpfBrush>(resourceId);
		}
		public static WpfBrush GetBackgroundBrush(this ResourceDictionary resource) {
			return resource.Get<WpfBrush>(EditorFormatDefinition.BackgroundBrushId);
		}
		public static TextDecorationCollection GetTextDecorations(this ResourceDictionary resource) {
			return resource.Get<TextDecorationCollection>(Constants.EditorFormatKeys.TextDecorations);
		}
		public static WpfColor GetColor(this IEditorFormatMap map, string formatName, string resourceId = EditorFormatDefinition.ForegroundColorId) {
			var p = map.GetProperties(formatName);
			return p != null && p.Contains(resourceId) && (p[resourceId] is WpfColor color)
				? color
				: EmptyColor;
		}
		public static WpfColor GetColor(this ResourceDictionary resource, string resourceId = EditorFormatDefinition.ForegroundColorId) {
			return resource != null && resource.Contains(resourceId) && (resource[resourceId] is WpfColor color)
				? color
				: EmptyColor;
		}
		public static WpfColor GetBackgroundColor(this ResourceDictionary resource) {
			return resource.GetColor(EditorFormatDefinition.BackgroundColorId);
		}
		public static ResourceDictionary SetBrush(this ResourceDictionary resource, Brush brush) {
			brush.Freeze();
			//var p = new ResourceDictionary().MergeWith(resource);
			resource[EditorFormatDefinition.ForegroundBrushId] = brush;
			return resource;
		}
		public static void Remove(this IEditorFormatMap map, string formatName, string key) {
			map.GetProperties(formatName).Remove(key);
		}
		public static Inline Render(this ISymbol symbol, string alias, WpfBrush brush) {
			return symbol.Render(alias, brush == null, brush);
		}
		public static Inline Render(this ISymbol symbol, string alias, bool bold, WpfBrush brush) {
			var run = new SymbolLink(symbol, alias, Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ClickAndGo));
			if (bold) {
				run.FontWeight = FontWeights.Bold;
			}
			if (brush != null) {
				run.Foreground = brush;
			}
			return run;
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
				Padding = ScrollerMargin
			}.ReferenceStyle(VsResourceKeys.GetScrollViewerStyleKey(true));
		}

		public static void SetUITextRenderOptions(DependencyObject element, bool optimize) {
			if (element == null) {
				return;
			}
			//TextOptions.SetTextFormattingMode(element, optimize ? TextFormattingMode.Ideal : TextFormattingMode.Display);
			TextOptions.SetTextHintingMode(element, optimize ? TextHintingMode.Fixed : TextHintingMode.Auto);
			TextOptions.SetTextRenderingMode(element, optimize ? TextRenderingMode.Grayscale : TextRenderingMode.Auto);
		}

		public static void GoToDefinition(this ISymbol symbol) {
			var r = symbol.GetSourceReferences();
			if (r.Length == 1) {
				r[0].GoToSource();
			}
			else {
				var ctx = SemanticContext.GetHovered();
				if (ctx != null) {
					if (r.Length == 0) {
						if (ctx.Document != null) {
							ServicesHelper.Instance.VisualStudioWorkspace.TryGoToDefinition(symbol, ctx.Document.Project, default);
						}
					}
					else {
						CSharpSymbolContextMenu.ShowLocations(symbol, r, ctx);
					}
				}
			}
		}

		sealed class SymbolLink : Run
		{
			readonly ISymbol _Symbol;

			public SymbolLink(ISymbol symbol, string alias, bool clickAndGo) {
				Text = alias ?? symbol.GetOriginalName();
				_Symbol = symbol;
				if (clickAndGo) {
					MouseEnter += InitInteraction;
				}
				ToolTip = String.Empty;
			}

			void InitInteraction(object sender, MouseEventArgs e) {
				MouseEnter -= InitInteraction;

				Cursor = Cursors.Hand;
				Highlight(sender, e);
				MouseEnter += Highlight;
				MouseLeave += Leave;
				MouseLeftButtonDown += GotoSymbol;
			}

			protected override void OnToolTipOpening(ToolTipEventArgs e) {
				base.OnToolTipOpening(e);
				if (ReferenceEquals(ToolTip, String.Empty)) {
					ToolTip = ToolTipFactory.CreateToolTip(_Symbol, false, SemanticContext.GetHovered().SemanticModel?.Compilation);
				}
			}
			protected override void OnMouseRightButtonDown(MouseButtonEventArgs e) {
				base.OnMouseRightButtonDown(e);
				if (ContextMenu != null) {
					//ContextMenu.IsOpen = true;
					return;
				}
				var ctx = SemanticContext.GetHovered();
				if (ctx != null) {
					SyncHelper.RunSync(() => ctx.UpdateAsync(default));
					var m = new CSharpSymbolContextMenu(ctx) {
						Symbol = _Symbol,
						SyntaxNode = _Symbol.GetSyntaxNode()
					};
					m.AddAnalysisCommands();
					if (m.HasItems) {
						m.Items.Add(new Separator());
					}
					m.AddSymbolNodeCommands();
					m.AddTitleItem(_Symbol.GetOriginalName());
					m.ItemClicked += DismissQuickInfo;
					ContextMenu = m;
					e.Handled = true;
					//m.IsOpen = true;
				}
			}

			void DismissQuickInfo(object sender, RoutedEventArgs e) {
				QuickInfo.QuickInfoOverrider.DismissQuickInfo(this);
			}

			protected override void OnContextMenuOpening(ContextMenuEventArgs e) {
				QuickInfo.QuickInfoOverrider.HoldQuickInfo(this, true);
				if (ContextMenu != null) {
					ContextMenu.IsOpen = true;
					e.Handled = true;
				}
				base.OnContextMenuOpening(e);
			}

			protected override void OnContextMenuClosing(ContextMenuEventArgs e) {
				QuickInfo.QuickInfoOverrider.HoldQuickInfo(this, false);
				base.OnContextMenuClosing(e);
			}

			void Highlight(object sender, MouseEventArgs e) {
				Background = (_Symbol.HasSource() ? SystemColors.HighlightBrush : SystemColors.GrayTextBrush).Alpha(0.3);
			}
			void Leave(object sender, MouseEventArgs e) {
				Background = WpfBrushes.Transparent;
			}

			void GotoSymbol(object sender, RoutedEventArgs e) {
				_Symbol.GoToDefinition();
				QuickInfo.QuickInfoOverrider.DismissQuickInfo(this);
			}
		}
	}
}
