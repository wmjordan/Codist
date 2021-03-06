﻿using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using AppHelpers;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using GdiColor = System.Drawing.Color;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using R = Codist.Properties.Resources;

namespace Codist.QuickInfo
{
	sealed class ColorQuickInfoController : IAsyncQuickInfoSource
	{
		public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			return Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color) == false
				? System.Threading.Tasks.Task.FromResult<QuickInfoItem>(null)
				: InternalGetQuickInfoItemAsync(session, cancellationToken);
		}

		static async Task<QuickInfoItem> InternalGetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
			var buffer = session.TextView.TextBuffer;
			var snapshot = session.TextView.TextSnapshot;
			var navigator = ServicesHelper.Instance.TextStructureNavigator.GetTextStructureNavigator(buffer);
			var extent = navigator.GetExtentOfWord(session.GetTriggerPoint(snapshot).GetValueOrDefault()).Span;
			var word = snapshot.GetText(extent);
			var brush = ColorHelper.GetBrush(word);
			if (brush == null) {
				if ((extent.Length == 6 || extent.Length == 8) && extent.Span.Start > 0 && Char.IsPunctuation(snapshot.GetText(extent.Span.Start - 1, 1)[0])) {
					word = "#" + word;
				}
				brush = ColorHelper.GetBrush(word);
			}
			return brush != null && session.Mark(nameof(ColorQuickInfo))
				? new QuickInfoItem(extent.ToTrackingSpan(), ColorQuickInfo.PreviewColor(brush))
				: null;
		}

		void IDisposable.Dispose() { }
	}

	static class ColorQuickInfo
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
					new ThemedTipText().Append(new System.Windows.Shapes.Rectangle { Width = 16, Height = 16, Fill = brush }).Append(R.T_Color, true),
					new StackPanel().AddReadOnlyTextBox($"{c.A}, {c.R}, {c.G}, {c.B}").Add(new ThemedTipText(" ARGB", true)).MakeHorizontal(),
					new StackPanel().AddReadOnlyTextBox(c.ToHexString()).Add(new ThemedTipText(" HEX", true)).MakeHorizontal(),
					new StackPanel().AddReadOnlyTextBox($"{v.Hue.ToString("0.###")}, {v.Saturation.ToString("0.###")}, {v.Luminosity.ToString("0.###")}").Add(new ThemedTipText(" HSL", true)).MakeHorizontal(),
					new StackPanel {
						Children = {
							new TextBlock { Text = SAMPLE, Margin = WpfHelper.TinyMargin, Padding = WpfHelper.TinyMargin, Foreground = brush, Background = WpfBrushes.Black },
							new TextBlock { Text = SAMPLE, Margin = WpfHelper.TinyMargin, Padding = WpfHelper.TinyMargin, Background = brush, Foreground = WpfBrushes.Black },
							new TextBlock { Text = SAMPLE, Margin = WpfHelper.TinyMargin, Padding = WpfHelper.TinyMargin, Foreground = brush, Background = WpfBrushes.Gray },
							new TextBlock { Text = SAMPLE, Margin = WpfHelper.TinyMargin, Padding = WpfHelper.TinyMargin, Background = brush, Foreground = WpfBrushes.White },
							new TextBlock { Text = SAMPLE, Margin = WpfHelper.TinyMargin, Padding = WpfHelper.TinyMargin, Foreground = brush, Background = WpfBrushes.White },
						}
					}.MakeHorizontal(),
				}
			};
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
					var m = a1 as MemberAccessExpressionSyntax;
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
