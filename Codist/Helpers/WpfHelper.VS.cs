using System;
using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
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
			}.SetStyleResourceProperty(VsResourceKeys.TextBoxStyleKey));
			return panel;
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
		public static TextBlock AddSymbolDisplayParts(this TextBlock block, ImmutableArray<SymbolDisplayPart> parts, SymbolFormatter formatter) {
			return formatter.ToUIText(block, parts, Int32.MinValue);
		}
		public static TextBlock AddSymbolDisplayParts(this TextBlock block, ImmutableArray<SymbolDisplayPart> parts, SymbolFormatter formatter, int argIndex) {
			return formatter.ToUIText(block, parts, argIndex);
		}
		public static ContextMenu CreateContextMenuForSourceLocations(string symbolName, ImmutableArray<Location> refs) {
			var menu = new ContextMenu {
				Resources = SharedDictionaryManager.ContextMenu
			};
			menu.Opened += (sender, e) => {
				var m = sender as ContextMenu;
				m.Items.Add(new MenuItem {
					Header = new TextBlock().Append(symbolName, true).Append(" is defined in ").Append(refs.Length.ToString(), true).Append(" places"),
					IsEnabled = false
				});
				foreach (var loc in refs.Sort(System.Collections.Generic.Comparer<Location>.Create((a, b) => {
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
		public static WpfBrush GetBrush(this IEditorFormatMap map, string formatName, string resourceId = EditorFormatDefinition.ForegroundBrushId) {
			var p = map.GetProperties(formatName);
			return p != null && p.Contains(resourceId)
				? (p[resourceId] as WpfBrush)
				: null;
		}
		public static WpfColor GetColor(this IEditorFormatMap map, string formatName, string resourceId = EditorFormatDefinition.ForegroundColorId) {
			var p = map.GetProperties(formatName);
			return p != null && p.Contains(resourceId) && (p[resourceId] is WpfColor color)
				? color
				: EmptyColor;
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
			}.SetStyleResourceProperty(VsResourceKeys.GetScrollViewerStyleKey(true));
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
				var tooltip = new ThemedToolTip();
				tooltip.Title
					.Append(_Symbol.GetAccessibility() + _Symbol.GetAbstractionModifier() + _Symbol.GetSymbolKindName() + " ")
					.Append(_Symbol.GetSignatureString(), true);

				var content = tooltip.Content
					.Append("namespace: " + _Symbol.ContainingNamespace?.ToString())
					.Append("\nassembly: " + _Symbol.GetAssemblyModuleName());
				ITypeSymbol t = _Symbol.ContainingType;
				if (t != null) {
					content.Append("\n" + t.GetSymbolKindName() + ": ")
						.Append(t.ToDisplayString(QuickInfoSymbolDisplayFormat));
				};
				t = _Symbol.GetReturnType();
				if (t != null) {
					content.Append("\nreturn value: ").Append(t.ToDisplayString(QuickInfoSymbolDisplayFormat), true);
				}
				var f = _Symbol as IFieldSymbol;
				if (f != null && f.IsConst) {
					content.Append("\nconst: " + f.ConstantValue.ToString());
				}
				ToolTip = tooltip;
				ToolTipOpening -= ShowSymbolToolTip;
			}

		}
	}
}
