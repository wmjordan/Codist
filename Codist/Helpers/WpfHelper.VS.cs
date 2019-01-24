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
		internal static readonly SymbolDisplayFormat QuickInfoSymbolDisplayFormat = new SymbolDisplayFormat(
			typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
			genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
			parameterOptions: SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeOptionalBrackets | SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeType,
			memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeContainingType,
			delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
			miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);
		internal static readonly SymbolDisplayFormat MemberNameFormat = new SymbolDisplayFormat(
			typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
			parameterOptions: SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeOptionalBrackets,
			genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
			miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

		public static TPanel AddReadOnlyTextBox<TPanel>(this TPanel panel, string text)
		where TPanel : Panel {
			panel.Children.Add(new TextBox {
				Text = text,
				IsReadOnly = true,
				TextAlignment = TextAlignment.Right,
				MinWidth = 180
			}.ReferenceStyle(VsResourceKeys.TextBoxStyleKey));
			return panel;
		}
		public static TextBlock AddParameters(this TextBlock block, ImmutableArray<IParameterSymbol> parameters, SymbolFormatter formatter) {
			var inlines = block.Inlines;
			inlines.Add("(");
			for (var i = 0; i < parameters.Length; i++) {
				if (i > 0) {
					inlines.Add(", ");
				}
				formatter.ToUIText(inlines, parameters[i].Type, null);
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
				formatter.ToUIText(inlines, parameters[i].Type, null);
				inlines.Add(new Run(" " + parameters[i].Name) {
					Foreground = formatter.Parameter,
					FontWeight = i == argIndex ? FontWeights.Bold : FontWeights.Normal
				});
			}
			inlines.Add(")");
			return block;
		}
		public static TextBlock AddSymbol(this TextBlock block, ISymbol symbol, SymbolFormatter formatter) {
			if (symbol != null) {
				formatter.ToUIText(block.Inlines, symbol, null);
			}
			return block;
		}
		public static TextBlock AddSymbol(this TextBlock block, ISymbol symbol, string alias, SymbolFormatter formatter) {
			if (symbol != null) {
				formatter.ToUIText(block.Inlines, symbol, alias);
			}
			return block;
		}
		public static Paragraph AddSymbol(this Paragraph paragraph, ISymbol symbol, string alias, SymbolFormatter formatter) {
			if (symbol != null) {
				formatter.ToUIText(paragraph.Inlines, symbol, alias);
			}
			return paragraph;
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
			return formatter.ToUIText(block, parts, Int32.MinValue);
		}
		public static TextBlock AddSymbolDisplayParts(this TextBlock block, ImmutableArray<SymbolDisplayPart> parts, SymbolFormatter formatter, int argIndex) {
			return formatter.ToUIText(block, parts, argIndex);
		}
		public static TextBlock AddXmlDoc(this TextBlock paragraph, XElement content, XmlDocRenderer docRenderer) {
			docRenderer.Render(content, paragraph.Inlines);
			return paragraph;
		}
		public static ContextMenu CreateContextMenuForSourceLocations(string symbolName, ImmutableArray<Location> refs) {
			var menu = new ContextMenu {
				Resources = SharedDictionaryManager.ContextMenu
			};
			menu.Opened += (sender, e) => {
				var m = sender as ContextMenu;
				m.Items.Add(new MenuItem {
					Header = new ThemedMenuText().Append(symbolName, true).Append(" is defined in ").Append(refs.Length.ToString(), true).Append(" places"),
					IsEnabled = false
				});
				foreach (var loc in refs.Sort(System.Collections.Generic.Comparer<Location>.Create((a, b) => {
					return String.Compare(System.IO.Path.GetFileName(a.SourceTree.FilePath), System.IO.Path.GetFileName(b.SourceTree.FilePath), StringComparison.OrdinalIgnoreCase);
				}))) {
					//var pos = loc.SourceTree.GetLineSpan(loc.SourceSpan);
					var item = new MenuItem {
						Header = new ThemedMenuText(System.IO.Path.GetFileName(loc.SourceTree.FilePath))/*.Append("(line: " + (pos.StartLinePosition.Line + 1).ToString() + ")", WpfBrushes.Gray)*/,
						Tag = loc
					};
					item.Click += (s, args) => ((Location)((MenuItem)s).Tag).GoToSource();
					m.Items.Add(item);
				}
			};
			return menu;
		}
		public static TItem Get<TItem>(this IEditorFormatMap map, string formatName, string resourceId) {
			return map.GetProperties(formatName).Get<TItem>(resourceId);
		}
		public static WpfBrush GetBrush(this IEditorFormatMap map, string formatName, string resourceId = EditorFormatDefinition.ForegroundBrushId) {
			return map.Get<WpfBrush>(formatName, resourceId);
		}
		public static WpfColor GetColor(this IEditorFormatMap map, string formatName, string resourceId = EditorFormatDefinition.ForegroundColorId) {
			var p = map.GetProperties(formatName);
			return p != null && p.Contains(resourceId) && (p[resourceId] is WpfColor color)
				? color
				: EmptyColor;
		}
		public static Inline Render(this ISymbol symbol, string alias, WpfBrush brush) {
			return symbol.Render(alias, brush == null, brush);
		}
		public static Inline Render(this ISymbol symbol, string alias, bool bold, WpfBrush brush) {
			var run = new SymbolLink(symbol, alias, Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ClickAndGo));
			if (bold || brush == null) {
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
			//TextOptions.SetTextFormattingMode(element, optimize ? TextFormattingMode.Ideal : TextFormattingMode.Display);
			TextOptions.SetTextHintingMode(element, optimize ? TextHintingMode.Fixed : TextHintingMode.Auto);
			TextOptions.SetTextRenderingMode(element, optimize ? TextRenderingMode.Grayscale : TextRenderingMode.Auto);
		}

		sealed class SymbolLink : Run
		{
			readonly ISymbol _Symbol;

			public SymbolLink(ISymbol symbol, string alias, bool clickAndGo) {
				Text = alias ?? symbol.Name;
				_Symbol = symbol;
				TextDecorations = null;
				
				if (clickAndGo && symbol.ContainingAssembly.GetSourceType() != AssemblySource.Metadata) {
					MouseEnter += InitInteraction;
				}
				ToolTipOpening += ShowSymbolToolTip;
				ToolTip = String.Empty;
			}

			void InitInteraction(object sender, MouseEventArgs e) {
				MouseEnter -= InitInteraction;

				Highlight(sender, e);
				MouseEnter += Highlight;
				MouseLeave += Leave;
				MouseLeftButtonDown += GotoSymbol;
			}

			void Highlight(object sender, MouseEventArgs e) {
				Background = SystemColors.HighlightBrush.Alpha(0.3);
				Cursor = Cursors.Hand;
			}
			void Leave(object sender, MouseEventArgs e) {
				Background = WpfBrushes.Transparent;
			}

			void GotoSymbol(object sender, RoutedEventArgs e) {
				var r = _Symbol.GetSourceLocations();
				if (r.Length == 1) {
					r[0].GoToSource();
				}
				else {
					if (ContextMenu == null) {
						ContextMenu = CreateContextMenuForSourceLocations(_Symbol.MetadataName, r);
					}
					ContextMenu.IsOpen = true;
				}
			}

			void ShowSymbolToolTip(object sender, ToolTipEventArgs e) {
				var tooltip = new ThemedToolTip();
				tooltip.Title
					.Append(_Symbol.GetAccessibility() + _Symbol.GetAbstractionModifier() + _Symbol.GetSymbolKindName() + " ")
					.Append(_Symbol.Name, true)
					.Append(_Symbol.GetParameterString());

				var content = tooltip.Content;
				ITypeSymbol t = _Symbol.ContainingType;
				bool c = false;
				if (t != null) {
					content.Append(t.GetSymbolKindName() + ": ")
						.Append(t.ToDisplayString(QuickInfoSymbolDisplayFormat));
					c = true;
				};
				t = _Symbol.GetReturnType();
				if (t != null) {
					if (c) {
						content.AppendLine();
					}
					c = true;
					content.Append("return value: ").Append(t.ToDisplayString(QuickInfoSymbolDisplayFormat), true);
				}
				var f = _Symbol as IFieldSymbol;
				if (f != null && f.IsConst) {
					if (c) {
						content.AppendLine();
					}
					c = true;
					content.Append("const: " + f.ConstantValue.ToString());
				}
				if (c) {
					content.AppendLine();
				}
				content.Append("namespace: " + _Symbol.ContainingNamespace?.ToString())
					.Append("\nassembly: " + _Symbol.GetAssemblyModuleName());
				ToolTip = tooltip;
				ToolTipOpening -= ShowSymbolToolTip;
			}

		}
	}
}
