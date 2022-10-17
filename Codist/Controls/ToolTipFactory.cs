using System;
using System.Linq;
using System.Windows.Controls;
using AppHelpers;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using R = Codist.Properties.Resources;
using System.Windows.Documents;
using System.Windows;

namespace Codist
{
	static class ToolTipFactory
	{
		public static ThemedToolTip CreateToolTip(ISymbol symbol, bool forMemberList, SemanticContext context) {
			var tip = new ThemedToolTip();
			if (Config.Instance.DisplayOptimizations.MatchFlags(DisplayOptimizations.CodeWindow)) {
				WpfHelper.SetUITextRenderOptions(tip, true);
			}
			if (forMemberList == false) {
				tip.Title.Append(ThemeHelper.GetImage(symbol.GetImageId()).WrapMargin(WpfHelper.GlyphMargin));
			}
			tip.Title
				.Append($"{symbol.GetAccessibility()}{symbol.GetAbstractionModifier()}{symbol.GetValueAccessModifier()}{symbol.GetSymbolKindName()} ")
				.Append(symbol.Name, true)
				.Append(symbol.GetParameterString());
			var content = tip.Content;
			var t = symbol.GetReturnType();
			if (t != null) {
				content.Append(R.T_MemberType)
					.Append(t.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true);

				if (symbol.Kind == SymbolKind.Parameter) {
					var p = symbol as IParameterSymbol;
					if (p.HasExplicitDefaultValue) {
						SymbolFormatter.Instance.AppendValue(tip.Title.Inlines, symbol, p.Type, p.ExplicitDefaultValue);
					}
				}
			}
			else if (symbol.Kind == SymbolKind.TypeParameter) {
				content.Append(R.T_DefinedInType)
					.Append(symbol.ContainingSymbol?.ToDisplayString(CodeAnalysisHelper.MemberNameFormat) ?? String.Empty, true);
				var tp = symbol as ITypeParameterSymbol;
				if (tp.HasConstraint()) {
					content.AppendLine().Append(R.T_Constraint);
					SymbolFormatter.Instance.ShowTypeConstaints(tp, content);
				}
			}
			t = symbol.ContainingType;
			if (t != null && t.TypeKind != TypeKind.Enum) {
				content.AppendLineBreak()
					.Append(t.GetSymbolKindName(), SymbolFormatter.Instance.Keyword)
					.Append(": ")
					.Append(t.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true);
			}
			if (forMemberList == false) {
				content.AppendLineBreak()
					.Append(R.T_Namespace + symbol.ContainingNamespace?.ToString()).AppendLine();
				if (symbol.Kind == SymbolKind.Namespace) {
					// hack: workaround to exclude references that returns null from GetDocument
					content.Append(R.T_Assembly)
						.Append(String.Join(", ", ((INamespaceSymbol)symbol).ConstituentNamespaces.Select(n => n.GetAssemblyModuleName()).Distinct()))
						.AppendLine()
						.Append(R.T_Project)
						.Append(String.Join(", ", symbol.GetSourceReferences().Select(r => context.GetProject(r.SyntaxTree)).Where(p => p != null).Distinct().Select(p => p.Name)))
						.AppendLine()
						.Append(R.T_Location)
						.Append(symbol.Locations.Length);
				}
				else {
					if (symbol.HasSource()) {
						content.Append(R.T_SourceFile)
							.Append(String.Join(", ", symbol.GetSourceReferences().Select(r => System.IO.Path.GetFileName(r.SyntaxTree.FilePath))))
							.AppendLine()
							.Append(R.T_Project)
							.Append(String.Join(", ", symbol.GetSourceReferences().Select(r => context.GetProject(r.SyntaxTree)).Where(p => p != null).Distinct().Select(p => p.Name)));
					}
					else {
						content.Append(R.T_Assembly)
							.Append(symbol.GetAssemblyModuleName());
					}

					if (symbol.Kind == SymbolKind.NamedType) {
						switch (((INamedTypeSymbol)symbol).TypeKind) {
							case TypeKind.Delegate:
								ShowDelegateSignature(content, (INamedTypeSymbol)symbol);
								break;
							case TypeKind.Enum:
								ShowEnumType(content, symbol);
								break;
						}
					}
				}
			}
			ShowAttributes(symbol, content);
			if (context.SemanticModel?.Compilation != null && Config.Instance.SymbolToolTipOptions.MatchFlags(SymbolToolTipOptions.XmlDocSummary)) {
				ShowXmlDocSummary(symbol, context.SemanticModel.Compilation, tip);
			}
			ShowNumericForms(symbol, tip);
			if (symbol.Kind == SymbolKind.Property && Config.Instance.SymbolToolTipOptions.MatchFlags(SymbolToolTipOptions.Colors)) {
				var preview = QuickInfo.ColorQuickInfoUI.PreviewColorProperty(symbol as IPropertySymbol, false);
				if (preview != null) {
					tip.Add(preview);
				}
			}
			return tip;
		}

		static void ShowDelegateSignature(TextBlock content, INamedTypeSymbol type) {
			content.AppendLineBreak().Append(R.T_Signature);
			var invoke = type.OriginalDefinition.DelegateInvokeMethod;
			content.AddSymbol(invoke.ReturnType, false, SymbolFormatter.Instance)
				.Append(" ").AddSymbol(type, true, SymbolFormatter.Instance)
				.AddParameters(invoke.Parameters, SymbolFormatter.Instance);
		}

		static void ShowEnumType(TextBlock content, ISymbol symbol) {
			var t = ((INamedTypeSymbol)symbol).EnumUnderlyingType.ToDisplayString(CodeAnalysisHelper.QuickInfoSymbolDisplayFormat);
			if (t != "int") {
				content.AppendLineBreak().Append(R.T_Type + t);
			}
		}

		static void ShowAttributes(ISymbol symbol, TextBlock content) {
			if (Config.Instance.SymbolToolTipOptions.MatchFlags(SymbolToolTipOptions.Attributes)) {
				foreach (var attr in symbol.GetAttributes()) {
					SymbolFormatter.Instance.Format(content.AppendLine().Inlines, attr, 0);
				}
				if (symbol.Kind == SymbolKind.Method) {
					foreach (var attr in ((IMethodSymbol)symbol).GetReturnTypeAttributes()) {
						SymbolFormatter.Instance.Format(content.AppendLine().Inlines, attr, 1);
					}
				}
			}
		}

		static void ShowNumericForms(ISymbol symbol, ThemedToolTip tip) {
			if (Config.Instance.SymbolToolTipOptions.MatchFlags(SymbolToolTipOptions.NumericValues)) {
				var f = symbol as IFieldSymbol;
				if (f != null && f.IsConst) {
					var p = ShowNumericForms(f);
					if (p != null) {
						tip.AddBorder().Child = p;
					}
				}
			}
		}

		static void ShowXmlDocSummary(ISymbol symbol, Compilation compilation, ThemedToolTip tip) {
			var doc = new XmlDoc(symbol, compilation);
			var summary = doc.GetDescription(symbol)
				?? (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.DocumentationFromInheritDoc) ? doc.GetInheritedDescription(symbol, out doc) : null);
			if (summary != null) {
				var docContent = tip.AddTextBlock();
				new XmlDocRenderer(compilation, SymbolFormatter.Instance).Render(summary, docContent);
				if (Config.Instance.QuickInfoMaxWidth >= 100) {
					tip.MaxWidth = Config.Instance.QuickInfoMaxWidth;
				}
			}
		}

		internal static TTarget SetTipOptions<TTarget>(this TTarget target)
			where TTarget : DependencyObject {
			ToolTipService.SetBetweenShowDelay(target, 0);
			ToolTipService.SetInitialShowDelay(target, 1000);
			ToolTipService.SetShowDuration(target, 15000);
			return target;
		}

		internal static TTarget SetTipPlacementBottom<TTarget>(this TTarget target)
			where TTarget : DependencyObject {
			ToolTipService.SetPlacement(target, System.Windows.Controls.Primitives.PlacementMode.Bottom);
			return target;
		}

		internal static TTarget SetTipPlacementTop<TTarget>(this TTarget target)
			where TTarget : DependencyObject {
			ToolTipService.SetPlacement(target, System.Windows.Controls.Primitives.PlacementMode.Top);
			return target;
		}

		internal static Grid ShowNumericForms(SyntaxNode node) {
			return ShowNumericForms(node.GetFirstToken().Value, node.Parent.Kind() == SyntaxKind.UnaryMinusExpression ? NumericForm.Negative : NumericForm.None);
		}

		internal static Grid ShowNumericForms(object value) {
			return ShowNumericForms(value, NumericForm.None);
		}

		internal static Grid ShowNumericForms(IFieldSymbol symbol) {
			return ShowNumericForms(symbol.ConstantValue, NumericForm.None);
		}

		static Grid ShowNumericForms(object value, NumericForm form) {
			if (value == null) {
				return null;
			}
			switch (Type.GetTypeCode(value.GetType())) {
				case TypeCode.Int32: {
					var v = (int)value;
					if (form == NumericForm.Negative) {
						v = -v;
					}
					return ShowNumberAndBytes(
						form == NumericForm.Unsigned ? ((uint)v).ToString() : v.ToString(),
						new byte[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });
				}
				case TypeCode.Int64: {
					var v = (long)value;
					if (form == NumericForm.Negative) {
						v = -v;
					}
					return ShowNumberAndBytes(
						form == NumericForm.Unsigned ? ((ulong)v).ToString() : v.ToString(),
						new byte[] { (byte)(v >> 56), (byte)(v >> 48), (byte)(v >> 40), (byte)(v >> 32), (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });
				}
				case TypeCode.Byte:
					return ShowNumberAndBytes(((byte)value).ToString(), new byte[] { (byte)value });
				case TypeCode.Int16: {
					var v = (short)value;
					if (form == NumericForm.Negative) {
						v = (short)-v;
					}
					return ShowNumberAndBytes(
						form == NumericForm.Unsigned ? ((ushort)v).ToString() : v.ToString(),
						new byte[] { (byte)(v >> 8), (byte)v });
				}
				case TypeCode.Char: {
					var v = (char)value;
					return ShowNumberAndBytes(((ushort)v).ToString(), new byte[] { (byte)(v >> 8), (byte)v });
				}
				case TypeCode.UInt32:
					return ShowNumericForms((int)(uint)value, NumericForm.Unsigned);
				case TypeCode.UInt16:
					return ShowNumericForms((short)(ushort)value, NumericForm.Unsigned);
				case TypeCode.UInt64:
					return ShowNumericForms((long)(ulong)value, NumericForm.Unsigned);
				case TypeCode.SByte:
					return ShowNumberAndBytes(((sbyte)value).ToString(), new byte[] { (byte)(sbyte)value });
			}
			return null;

			Grid ShowNumberAndBytes(string number, byte[] bytes) {
				return new Grid {
					HorizontalAlignment = HorizontalAlignment.Left,
					RowDefinitions = {
						new RowDefinition(), new RowDefinition(), new RowDefinition()
					},
					ColumnDefinitions = {
						new ColumnDefinition(), new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }
					},
					Children = {
						new ThemedTipText(R.T_Decimal, true) { Margin = WpfHelper.GlyphMargin, TextAlignment = TextAlignment.Right },
						new ThemedTipText(R.T_Hexadecimal, true) { Margin = WpfHelper.GlyphMargin, TextAlignment = TextAlignment.Right }.SetValue(Grid.SetRow, 1),
						new ThemedTipText(R.T_Binary, true) { Margin = WpfHelper.GlyphMargin, TextAlignment = TextAlignment.Right }.SetValue(Grid.SetRow, 2),
						new ThemedTipText(number) { Background = ThemeHelper.TextBoxBackgroundBrush.Alpha(0.5), Foreground = ThemeHelper.TextBoxBrush, Padding = WpfHelper.SmallHorizontalMargin }.WrapBorder(ThemeHelper.TextBoxBorderBrush, WpfHelper.TinyMargin).SetValue(Grid.SetColumn, 1),
						ToHexString(new ThemedTipText() { Background = ThemeHelper.TextBoxBackgroundBrush.Alpha(0.5), Foreground = ThemeHelper.TextBoxBrush, Padding = WpfHelper.SmallHorizontalMargin }, bytes).WrapBorder(ThemeHelper.TextBoxBorderBrush, WpfHelper.TinyMargin).SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 1),
						ToBinString(new ThemedTipText() { Background = ThemeHelper.TextBoxBackgroundBrush.Alpha(0.5), Foreground = ThemeHelper.TextBoxBrush, Padding = WpfHelper.SmallHorizontalMargin }, bytes).WrapBorder(ThemeHelper.TextBoxBorderBrush, WpfHelper.TinyMargin).SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 2),
					}
				};
			}

			ThemedTipText ToBinString(ThemedTipText text, byte[] bytes) {
				var inlines = text.Inlines;
				inlines.Add(new Run("0b") { FontWeight = FontWeights.Bold });
				var hasValue = false;
				for (int i = 0; i < bytes.Length; i++) {
					ref var b = ref bytes[i];
					if (hasValue || b != 0) {
						hasValue = true;
						inlines.Add(Convert.ToString(b, 2).PadLeft(8, '0'));
						if ((i & 1) == 1) {
							AddBackground(inlines);
						}
					}
				}
				return hasValue ? text : text.Append("00000000");
			}

			ThemedTipText ToHexString(ThemedTipText text, byte[] bytes) {
				var inlines = text.Inlines;
				inlines.Add(new Run("0x") { FontWeight = FontWeights.Bold });
				if (bytes.Length == 1) {
					inlines.Add(bytes[0].ToString("X2"));
					return text;
				}
				var hasValue = false;
				for (int i = 0; i < bytes.Length; i++) {
					ref var b = ref bytes[i];
					if (hasValue || b != 0) {
						hasValue = true;
						inlines.Add(bytes[i].ToString("X2"));
						if ((i & 1) == 1) {
							AddBackground(inlines);
						}
					}
				}
				return hasValue ? text : text.Append("00");
			}

			void AddBackground(InlineCollection inlines) {
				inlines.LastInline.Background = ThemeHelper.TextSelectionHighlightBrush.Alpha(0.2);
			}
		}

		enum NumericForm
		{
			None,
			Negative,
			Unsigned
		}

	}
}
