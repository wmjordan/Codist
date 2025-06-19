using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using GdiColor = System.Drawing.Color;
using R = Codist.Properties.Resources;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;

namespace Codist.QuickInfo
{
	sealed class ColorInfoBlock : InfoBlock
	{
		public ColorInfoBlock(SolidColorBrush colorBrush) {
			ColorBrush = colorBrush;
		}

		public SolidColorBrush ColorBrush { get; }

		public override UIElement ToUI() {
			return ColorQuickInfoUI.PreviewColor(ColorBrush).Tag();
		}
	}

	static class ColorQuickInfoUI
	{
		const string PreviewPanelName = "ColorPreview";
		static readonly SolidColorBrush __DeepDarkGray = new SolidColorBrush(WpfColor.FromRgb(0x2C, 0x2C, 0x2C)).MakeFrozen();

		public static StackPanel PreviewColorProperty(IPropertySymbol symbol, bool includeVsColors) {
			return PreviewColor(ColorHelper.GetBrush(symbol, includeVsColors));
		}

		public static StackPanel PreviewColorMethodInvocation(SemanticModel semanticModel, SyntaxNode node, IMethodSymbol methodSymbol) {
			byte[] values;
			SeparatedSyntaxList<ArgumentSyntax> args;
			switch (methodSymbol.Name) {
				case nameof(GdiColor.FromArgb):
					if (methodSymbol.ContainingType.Name != nameof(Color)) {
						break;
					}
					args = GetMethodArguments(semanticModel, node);
					switch (args.Count) {
						case 1:
							var c = GetColorMethodArgument(semanticModel, args);
							return c.HasValue
								? PreviewColor(new SolidColorBrush(GdiColor.FromArgb(c.Value).ToWpfColor()))
								: null;
						case 3:
							values = GetColorMethodArguments(semanticModel, args, 3);
							return values != null
								? PreviewColor(new SolidColorBrush(WpfColor.FromRgb(values[0], values[1], values[2])))
								: null;
						case 4:
							values = GetColorMethodArguments(semanticModel, args, 4);
							return values != null
								? PreviewColor(new SolidColorBrush(WpfColor.FromArgb(values[0], values[1], values[2], values[3])))
								: null;
					}
					break;
				case nameof(WpfColor.FromRgb):
					if (methodSymbol.ContainingType.Name != nameof(Color)) {
						break;
					}
					args = GetMethodArguments(semanticModel, node);
					if (args.Count < 3) {
						break;
					}
					values = GetColorMethodArguments(semanticModel, args, 3);
					return values != null
						? PreviewColor(new SolidColorBrush(WpfColor.FromRgb(values[0], values[1], values[2])))
						: null;
			}
			return null;
		}

		public static StackPanel PreviewColor(SolidColorBrush brush) {
			const string SAMPLE = "Hi,WM.";
			if (brush == null) {
				return null;
			}
			var c = brush.Color;
			var v = Microsoft.VisualStudio.Imaging.HslColor.FromColor(c);
			return new StackPanel {
				Name = PreviewPanelName,
				Children = {
					new ThemedTipText().Append(new System.Windows.Shapes.Rectangle { Width = 16, Height = 16, Fill = brush, Margin = WpfHelper.GlyphMargin }).Append(R.T_Color, true),
					new Grid {
						HorizontalAlignment = HorizontalAlignment.Left,
						RowDefinitions = {
							new RowDefinition(), new RowDefinition(), new RowDefinition()
						},
						ColumnDefinitions = {
							new ColumnDefinition(), new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }
						},
						Children = {
							new ThemedTipText("ARGB", true) { Margin = WpfHelper.GlyphMargin, TextAlignment = TextAlignment.Right },
							new ThemedTipText("HEX", true) { Margin = WpfHelper.GlyphMargin, TextAlignment = TextAlignment.Right }.SetValue(Grid.SetRow, 1),
							new ThemedTipText("HSL", true) { Margin = WpfHelper.GlyphMargin, TextAlignment = TextAlignment.Right }.SetValue(Grid.SetRow, 2),
							new ThemedTipText($"{c.A}, {c.R}, {c.G}, {c.B}") { Background = ThemeCache.TextBoxBackgroundBrush.Alpha(0.5), Foreground = ThemeCache.TextBoxBrush, Padding = WpfHelper.SmallHorizontalMargin }
								.WrapBorder(ThemeCache.TextBoxBorderBrush, WpfHelper.TinyMargin)
								.SetValue(Grid.SetColumn, 1),
							new ThemedTipText(c.ToHexString()) { Background = ThemeCache.TextBoxBackgroundBrush.Alpha(0.5), Foreground = ThemeCache.TextBoxBrush, Padding = WpfHelper.SmallHorizontalMargin }
								.WrapBorder(ThemeCache.TextBoxBorderBrush, WpfHelper.TinyMargin)
								.SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 1),
							new ThemedTipText($"{v.Hue:0.###}, {v.Saturation:0.###}, {v.Luminosity:0.###}") { Background = ThemeCache.TextBoxBackgroundBrush.Alpha(0.5), Foreground = ThemeCache.TextBoxBrush, Padding = WpfHelper.SmallHorizontalMargin }
								.WrapBorder(ThemeCache.TextBoxBorderBrush, WpfHelper.TinyMargin)
								.SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 2),
						}
					},
					new StackPanel {
						Children = {
							CreateSampleBlock(brush, WpfBrushes.White),
							CreateSampleBlock(brush, WpfBrushes.LightGray),
							CreateSampleBlock(brush, WpfBrushes.DarkGray),
							CreateSampleBlock(brush, WpfBrushes.Gray),
							CreateSampleBlock(brush, WpfBrushes.DimGray),
							CreateSampleBlock(brush, __DeepDarkGray),
							CreateSampleBlock(brush, WpfBrushes.Black),
						}
					}.MakeHorizontal(),
					new StackPanel {
						Children = {
							CreateSampleBlock(WpfBrushes.White, brush),
							CreateSampleBlock(WpfBrushes.LightGray, brush),
							CreateSampleBlock(WpfBrushes.DarkGray, brush),
							CreateSampleBlock(WpfBrushes.Gray, brush),
							CreateSampleBlock(WpfBrushes.DimGray, brush),
							CreateSampleBlock(__DeepDarkGray, brush),
							CreateSampleBlock(WpfBrushes.Black, brush),
						}
					}.MakeHorizontal()
				}
			};
			Border CreateSampleBlock(Brush foreground, Brush background) {
				return new Border {
					BorderBrush = foreground,
					BorderThickness = WpfHelper.TinyMargin,
					Margin = WpfHelper.TinyMargin,
					Child = new TextBlock {
						Text = SAMPLE,
						Padding = new Thickness(2),
						Background = background,
						Foreground = foreground,
					}
				};
			}
		}

		static SeparatedSyntaxList<ArgumentSyntax> GetMethodArguments(SemanticModel semanticModel, SyntaxNode node) {
			return ((node?.Parent?.Parent as InvocationExpressionSyntax)?.ArgumentList.Arguments) ?? default;
		}

		static int? GetColorMethodArgument(SemanticModel semanticModel, SeparatedSyntaxList<ArgumentSyntax> args) {
			var a1 = args[0].Expression;
			switch (a1.Kind()) {
				case SyntaxKind.NumericLiteralExpression:
					try {
						return Convert.ToInt32(((LiteralExpressionSyntax)a1).Token.Value);
					}
					catch (Exception) {
						return null;
					}
				case SyntaxKind.SimpleMemberAccessExpression:
				case SyntaxKind.IdentifierName:
					var s = semanticModel.GetSymbolInfo(a1).Symbol;
					if (s == null) {
						return null;
					}
					if (s.Kind == SymbolKind.Field) {
						var f = (IFieldSymbol)s;
						if (f.HasConstantValue && f.Type.SpecialType == SpecialType.System_Int32) {
							return (int)f.ConstantValue;
						}
					}
					else if (s.Kind == SymbolKind.Local) {
						var l = (ILocalSymbol)s;
						if (l.HasConstantValue && l.Type.SpecialType == SpecialType.System_Int32) {
							return (int)l.ConstantValue;
						}
					}
					break;
			}
			return null;
		}

		static byte[] GetColorMethodArguments(SemanticModel semanticModel, SeparatedSyntaxList<ArgumentSyntax> args, int length) {
			var r = new byte[length];
			for (int i = 0; i < length; i++) {
				var a1 = args[i].Expression;
				switch (a1.Kind()) {
					case SyntaxKind.NumericLiteralExpression:
						try {
							r[i] = Convert.ToByte((a1 as LiteralExpressionSyntax).Token.Value);
						}
						catch (Exception) {
							return null;
						}
						continue;
					case SyntaxKind.SimpleMemberAccessExpression: {
							var s = semanticModel.GetSymbolInfo(a1).Symbol;
							if (s == null) {
								return null;
							}
							if (s.Kind == SymbolKind.Field) {
								var f = s as IFieldSymbol;
								if (f.HasConstantValue) {
									try {
										r[i] = Convert.ToByte(f.ConstantValue);
									}
									catch (Exception) {
										return null;
									}
									continue;
								}
							}
							break;
						}
				}
				return null;
			}
			return r;
		}
	}
}
