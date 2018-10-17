using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using AppHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using GdiColor = System.Drawing.Color;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using Codist.Controls;

namespace Codist.QuickInfo
{
	sealed class ColorQuickInfoController : IQuickInfoSource
	{
		/// <summary>
		/// Provides quick info for named colors or #hex colors
		/// </summary>
		[Export(typeof(IQuickInfoSourceProvider))]
		[Name("Color Quick Info Controller")]
		[Order(After = "Default Quick Info Presenter")]
		[ContentType(Constants.CodeTypes.Text)]
		sealed class ColorQuickInfoControllerProvider : IQuickInfoSourceProvider
		{
			[Import]
			internal ITextStructureNavigatorSelectorService _NavigatorService = null;

			public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
				return Config.Instance.Features.MatchFlags(Features.SuperQuickInfo)
					? new ColorQuickInfoController(_NavigatorService)
					: null;
			}
		}

		readonly ITextStructureNavigatorSelectorService _NavigatorService;

		public ColorQuickInfoController(ITextStructureNavigatorSelectorService navigatorService) {
			_NavigatorService = navigatorService;
		}

		public void AugmentQuickInfoSession(IQuickInfoSession session, IList<Object> qiContent, out ITrackingSpan applicableToSpan) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color) == false) {
				goto EXIT;
			}
			var buffer = session.TextView.TextBuffer;
			var snapshot = session.TextView.TextSnapshot;
			var navigator = _NavigatorService.GetTextStructureNavigator(buffer);
			var extent = navigator.GetExtentOfWord(session.GetTriggerPoint(snapshot).GetValueOrDefault()).Span;
			var word = snapshot.GetText(extent);
			var brush = ColorQuickInfo.GetBrush(word);
			if (brush == null) {
				if ((extent.Length == 6 || extent.Length == 8) && extent.Span.Start > 0 && Char.IsPunctuation(snapshot.GetText(extent.Span.Start - 1, 1)[0])) {
					word = "#" + word;
				}
				brush = ColorQuickInfo.GetBrush(word);
			}
			if (brush != null) {
				qiContent.Add(ColorQuickInfo.PreviewColor(brush));
				applicableToSpan = snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeExclusive);
				return;
			}
			EXIT:
			applicableToSpan = null;
		}

		void IDisposable.Dispose() { }
	}

	static class ColorQuickInfo
	{
		public static SolidColorBrush GetBrush(string color) {
			return NamedColorCache.GetBrush(color);
		}
		public static StackPanel PreviewSystemColorProperties(IPropertySymbol symbol) {
			switch (symbol.ContainingType?.Name) {
				case nameof(System.Windows.SystemColors):
				case nameof(System.Drawing.SystemBrushes):
				case nameof(System.Drawing.KnownColor):
					return PreviewColor(GetBrush(symbol));
			}
			return null;
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
				Children = {
					new ThemedTipText().Append(new System.Windows.Shapes.Rectangle { Width = 16, Height = 16, Fill = brush }).Append("Color", true),
					new StackPanel().AddReadOnlyTextBox($"{c.A}, {c.R}, {c.G}, {c.B}").Add(new ThemedTipText(" ARGB", true)).MakeHorizontal(),
					new StackPanel().AddReadOnlyTextBox(c.ToHexString()).Add(new ThemedTipText(" HEX", true)).MakeHorizontal(),
					new StackPanel().AddReadOnlyTextBox($"{v.Hue.ToString("0.###")}, {v.Saturation.ToString("0.###")}, {v.Luminosity.ToString("0.###")}").Add(new ThemedTipText(" HSL", true)).MakeHorizontal(),
					new StackPanel {
						Children = {
							new TextBox { Text = SAMPLE, BorderBrush = WpfBrushes.Black, Foreground = brush, Background = WpfBrushes.Black },
							new TextBox { Text = SAMPLE, BorderBrush = WpfBrushes.Black, Background = brush, Foreground = WpfBrushes.Black },
							new TextBox { Text = SAMPLE, BorderBrush = WpfBrushes.Black, Foreground = brush, Background = WpfBrushes.Gray },
							new TextBox { Text = SAMPLE, BorderBrush = WpfBrushes.Black, Background = brush, Foreground = WpfBrushes.White },
							new TextBox { Text = SAMPLE, BorderBrush = WpfBrushes.Black, Foreground = brush, Background = WpfBrushes.White },
						}
					}.MakeHorizontal(),
				}
			};
		}

		public static SolidColorBrush GetBrush(ISymbol symbol) {
			switch (symbol.ContainingType?.Name) {
				case nameof(System.Windows.SystemColors):
				case nameof(System.Drawing.SystemBrushes):
					return NamedColorCache.GetSystemBrush(symbol.Name);
				case nameof(System.Drawing.KnownColor):
					return NamedColorCache.GetBrush(symbol.Name) ?? NamedColorCache.GetSystemBrush(symbol.Name);
				case nameof(System.Drawing.Color):
				case nameof(System.Drawing.Brushes):
				case nameof(Colors):
					return NamedColorCache.GetBrush(symbol.Name);
			}
			return null;
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
		static class NamedColorCache
		{
			static readonly Dictionary<string, SolidColorBrush> __Cache = GetBrushes();
			static readonly Dictionary<string, Func<SolidColorBrush>> __SystemColors = GetSystemColors();
			internal static SolidColorBrush GetBrush(string name) {
				var c = UIHelper.ParseColor(name);
				if (c != WpfColors.Transparent) {
					return new SolidColorBrush(c);
				}
				var l = name.Length;
				if (l >= 3 && l <= 20) {
					if (__Cache.TryGetValue(name, out var brush)) {
						return brush;
					}
				}
				return null;
			}
			internal static SolidColorBrush GetSystemBrush(string name) {
				if (__SystemColors.TryGetValue(name, out var func)) {
					return func();
				}
				return null;
			}
			static Dictionary<string, SolidColorBrush> GetBrushes() {
				var c = Array.FindAll(typeof(WpfBrushes).GetProperties(), p => p.PropertyType == typeof(SolidColorBrush));
				var d = new Dictionary<string, SolidColorBrush>(c.Length, StringComparer.OrdinalIgnoreCase);
				foreach (var item in c) {
					d.Add(item.Name, item.GetValue(null) as SolidColorBrush);
				}
				return d;
			}
			static Dictionary<string, Func<SolidColorBrush>> GetSystemColors() {
				var c = Array.FindAll(typeof(System.Windows.SystemColors).GetProperties(), p => p.PropertyType == typeof(SolidColorBrush) || p.PropertyType == typeof(WpfColor));
				var d = new Dictionary<string, Func<SolidColorBrush>>(c.Length, StringComparer.OrdinalIgnoreCase);
				foreach (var item in c) {
					if (item.PropertyType == typeof(SolidColorBrush)) {
						d.Add(item.Name, (Func<SolidColorBrush>)item.GetGetMethod(false).CreateDelegate(typeof(Func<SolidColorBrush>)));
					}
					else {
						var getColor = (Func<WpfColor>)item.GetGetMethod(false).CreateDelegate(typeof(Func<WpfColor>));
						d.Add(item.Name, () => new SolidColorBrush(getColor()));
					}
				}
				c = typeof(System.Drawing.SystemColors).GetProperties();
				foreach (var item in c) {
					var getColor = (Func<GdiColor>)item.GetGetMethod(false).CreateDelegate(typeof(Func<GdiColor>));
					d.Add(item.Name, () => new SolidColorBrush(getColor().ToWpfColor()));
				}
				return d;
			}
		}

	}
}
