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
	static class ColorQuickInfoUI
	{
		const string PreviewPanelName = "ColorPreview";

		public static StackPanel PreviewColorProperty(IPropertySymbol symbol, bool includeVsColors) {
			return PreviewColor(ColorHelper.GetBrush(symbol, includeVsColors));
		}

		public static StackPanel PreviewColorMethodInvocation(SemanticModel semanticModel, SyntaxNode node, IMethodSymbol methodSymbol) {
			switch (methodSymbol.Name) {
				case nameof(WpfColor.FromArgb):
					if (methodSymbol.ContainingType.Name == nameof(Color)) {
						var args = GetColorMethodArguments(semanticModel, node, 4);
						if (args != null) {
							return PreviewColor(new SolidColorBrush(WpfColor.FromArgb(args[0], args[1], args[2], args[3])));
						}
						args = GetColorMethodArguments(semanticModel, node, 3);
						if (args != null) {
							return PreviewColor(new SolidColorBrush(GdiColor.FromArgb(args[0], args[1], args[2]).ToWpfColor()));
						}
						var c = GetColorMethodArgument(semanticModel, node);
						if (c.HasValue) {
							return PreviewColor(new SolidColorBrush(GdiColor.FromArgb(c.Value).ToWpfColor()));
						}
					}
					break;
				case nameof(WpfColor.FromRgb): {
						var args = GetColorMethodArguments(semanticModel, node, 3);
						if (args != null) {
							return PreviewColor(new SolidColorBrush(WpfColor.FromRgb(args[0], args[1], args[2])));
						}
					}
					break;
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
							new ThemedTipText($"{c.A}, {c.R}, {c.G}, {c.B}") { Background = ThemeHelper.TextBoxBackgroundBrush.Alpha(0.5), Foreground = ThemeHelper.TextBoxBrush, Padding = WpfHelper.SmallHorizontalMargin }
								.WrapBorder(ThemeHelper.TextBoxBorderBrush, WpfHelper.TinyMargin)
								.SetValue(Grid.SetColumn, 1),
							new ThemedTipText(c.ToHexString()) { Background = ThemeHelper.TextBoxBackgroundBrush.Alpha(0.5), Foreground = ThemeHelper.TextBoxBrush, Padding = WpfHelper.SmallHorizontalMargin }
								.WrapBorder(ThemeHelper.TextBoxBorderBrush, WpfHelper.TinyMargin)
								.SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 1),
							new ThemedTipText($"{v.Hue:0.###}, {v.Saturation:0.###}, {v.Luminosity:0.###}") { Background = ThemeHelper.TextBoxBackgroundBrush.Alpha(0.5), Foreground = ThemeHelper.TextBoxBrush, Padding = WpfHelper.SmallHorizontalMargin }
								.WrapBorder(ThemeHelper.TextBoxBorderBrush, WpfHelper.TinyMargin)
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
							CreateSampleBlock(WpfBrushes.Black, brush),
						}
					}.MakeHorizontal()
				}
			};
			TextBlock CreateSampleBlock(Brush foreground, Brush background) {
				return new TextBlock { Text = SAMPLE, Margin = WpfHelper.TinyMargin, Padding = WpfHelper.TinyMargin, Background = background, Foreground = foreground };
			}
		}

		static int? GetColorMethodArgument(SemanticModel semanticModel, SyntaxNode node) {
			var invoke = node?.Parent?.Parent as InvocationExpressionSyntax;
			if (invoke == null) {
				return null;
			}
			var args = invoke.ArgumentList.Arguments;
			if (args.Count != 1) {
				return null;
			}
			var a1 = args[0].Expression;
			if (a1.Kind() == SyntaxKind.NumericLiteralExpression) {
				return Convert.ToInt32((a1 as LiteralExpressionSyntax).Token.Value);
			}
			if (a1.Kind() == SyntaxKind.SimpleMemberAccessExpression || a1.Kind() == SyntaxKind.IdentifierName) {
				var s = semanticModel.GetSymbolInfo(a1).Symbol;
				if (s == null) {
					return null;
				}
				if (s.Kind == SymbolKind.Field) {
					var f = s as IFieldSymbol;
					if (f.HasConstantValue && f.Type.SpecialType == SpecialType.System_Int32) {
						return (int)f.ConstantValue;
					}
				}
				else if (s.Kind == SymbolKind.Local) {
					var f = s as ILocalSymbol;
					if (f.HasConstantValue && f.Type.SpecialType == SpecialType.System_Int32) {
						return (int)f.ConstantValue;
					}
				}

			}
			return null;
		}

		static byte[] GetColorMethodArguments(SemanticModel semanticModel, SyntaxNode node, int length) {
			var invoke = node?.Parent?.Parent as InvocationExpressionSyntax;
			if (invoke == null) {
				return null;
			}
			var args = invoke.ArgumentList.Arguments;
			if (args.Count != length) {
				return null;
			}
			var r = new byte[length];
			for (int i = 0; i < length; i++) {
				var a1 = args[i].Expression;
				if (a1.Kind() == SyntaxKind.NumericLiteralExpression) {
					r[i] = Convert.ToByte((a1 as LiteralExpressionSyntax).Token.Value);
					continue;
				}
				if (a1.Kind() == SyntaxKind.SimpleMemberAccessExpression) {
					var s = semanticModel.GetSymbolInfo(a1).Symbol;
					if (s == null) {
						return null;
					}
					if (s.Kind == SymbolKind.Field) {
						var f = s as IFieldSymbol;
						if (f.HasConstantValue) {
							r[i] = Convert.ToByte(f.ConstantValue);
							continue;
						}
					}
				}
				return null;
			}
			return r;
		}
	}
}
