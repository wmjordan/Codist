using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using CLR;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using R = Codist.Properties.Resources;

namespace Codist
{
	static class ToolTipHelper
	{
		public static ThemedToolTip CreateFileToolTip(string folder, string file) {
			return new ThemedToolTip(file, $"{R.T_Folder}{folder}{Environment.NewLine}{R.T_ClickToOpenInExplorer}");
		}

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
				.Append(symbol.GetOriginalName(), true)
				.Append(symbol.GetParameterString(true));
			var content = tip.Content;
			var t = symbol.GetReturnType();
			if (t != null) {
				content.Append(R.T_MemberType)
					.Append(t.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true);

				if (symbol.Kind == SymbolKind.Parameter) {
					var p = symbol as IParameterSymbol;
					if (p.HasExplicitDefaultValue) {
						SymbolFormatter.Instance.AppendValue(tip.Title.Inlines, symbol, p.ExplicitDefaultValue);
					}
				}
			}
			else if (symbol.Kind == SymbolKind.TypeParameter) {
				ShowTypeParameter(content, symbol);
			}
			foreach (var item in symbol.GetExplicitInterfaceImplementations()) {
				content.AppendLineBreak()
					.Append(R.T_ExplicitImplements)
					.Append(item.ContainingType.GetTypeName())
					.Append(".")
					.Append(item.Name);
			}
			t = symbol.ContainingType;
			if (t != null && t.TypeKind != TypeKind.Enum) {
				ShowSymbolKind(content, t);
			}
			if (forMemberList == false) {
				content.AppendLineBreak()
					.Append(R.T_Namespace + symbol.ContainingNamespace?.ToString()).AppendLine();
				if (symbol.Kind == SymbolKind.Namespace) {
					ShowNamespaceSource(content, (INamespaceSymbol)symbol, context);
				}
				else {
					ShowSymbolSource(content, symbol, context);

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
			if (context.SemanticModel?.Compilation != null
				&& Config.Instance.SymbolToolTipOptions.MatchFlags(SymbolToolTipOptions.XmlDocSummary)) {
				ShowXmlDocSummary(symbol, context.SemanticModel.Compilation, tip);

				if (symbol.Kind == SymbolKind.Method
					&& ((IMethodSymbol)symbol).MethodKind == MethodKind.Constructor
					&& symbol.ContainingType.IsAttributeType()) {
					ShowAttributeTypeXmlDoc(symbol, context, tip);
				}
			}

			ShowNumericRepresentations(symbol, tip);
			if (symbol.Kind == SymbolKind.Property
				&& Config.Instance.SymbolToolTipOptions.MatchFlags(SymbolToolTipOptions.Colors)) {
				ShowColorPreview(symbol, tip);
			}
			return tip;
		}

		static void ShowTypeParameter(TextBlock content, ISymbol symbol) {
			content.Append(R.T_DefinedInType)
				.Append(symbol.ContainingSymbol?.ToDisplayString(CodeAnalysisHelper.MemberNameFormat) ?? String.Empty, true);
			var tp = symbol as ITypeParameterSymbol;
			if (tp.HasConstraint()) {
				content.AppendLine().Append(R.T_Constraint);
				SymbolFormatter.Instance.ShowTypeConstraints(tp, content);
			}
		}

		static void ShowSymbolKind(TextBlock content, ITypeSymbol type) {
			content.AppendLineBreak()
				.Append(type.GetSymbolKindName(), SymbolFormatter.Instance.Keyword)
				.Append(": ")
				.Append(type.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true);
		}

		static void ShowNamespaceSource(TextBlock content, INamespaceSymbol symbol, SemanticContext context) {
			content.Append(R.T_Project)
				// hack: workaround to exclude references that returns null from GetDocument
				.Append(String.Join(", ", symbol.GetSourceReferences().Select(r => context.GetProject(r.SyntaxTree)).Where(p => p != null).Distinct().Select(p => p.Name)))
				.AppendLine()
				.Append(R.T_Location)
				.Append(symbol.GetCompilationNamespace(context.SemanticModel).Locations.Length)
				.AppendLine()
				.Append(R.T_Assembly)
				.Append(String.Join(", ", symbol.ConstituentNamespaces.Select(n => n.GetAssemblyModuleName()).Distinct()));
		}

		static void ShowSymbolSource(TextBlock content, ISymbol symbol, SemanticContext context) {
			Compilation compilation;
			if (symbol.HasSource()) {
				var refs = symbol.GetSourceReferences();
				content.Append(R.T_SourceFile)
					.Append(String.Join(", ", refs.Select(r => System.IO.Path.GetFileName(r.SyntaxTree.FilePath))))
					.AppendLine()
					.Append(R.T_Project)
					.Append(String.Join(", ", refs.Select(r => context.GetProject(r.SyntaxTree)).Where(p => p != null).Distinct().Select(p => p.Name)));
			}
			else if ((compilation = context.SemanticModel?.Compilation) != null) {
				var (p, f) = compilation.GetReferencedAssemblyPath(symbol.ContainingAssembly);
				if (String.IsNullOrEmpty(f) == false) {
					content.Append(R.T_Assembly).Append(p).Append(f, true);
				}
			}
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

		static void ShowNumericRepresentations(ISymbol symbol, ThemedToolTip tip) {
			if (Config.Instance.SymbolToolTipOptions.MatchFlags(SymbolToolTipOptions.NumericValues)
				&& symbol is IFieldSymbol f && f.IsConst) {
				var p = ShowNumericRepresentations(f);
				if (p != null) {
					tip.AddBorder().Child = p;
				}
			}
		}

		static void ShowAttributeTypeXmlDoc(ISymbol symbol, SemanticContext context, ThemedToolTip tip) {
			tip.AddTextBlock().Append(symbol.ContainingType.Name, true, false, SymbolFormatter.Instance.Class).Append(":");
			ShowXmlDocSummary(symbol.ContainingType, context.SemanticModel.Compilation, tip);
		}

		static void ShowXmlDocSummary(ISymbol symbol, Compilation compilation, ThemedToolTip tip) {
			var doc = new XmlDoc(symbol, compilation);
			var summary = doc.GetDescription(symbol)
				?? (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.DocumentationFromInheritDoc) ? doc.GetInheritedDescription(symbol, out _) : null);
			if (summary != null) {
				var docContent = tip.AddTextBlock();
				new XmlDocRenderer(compilation, SymbolFormatter.Instance).Render(summary, docContent);
				if (Config.Instance.QuickInfo.MaxWidth >= 100) {
					tip.MaxWidth = Config.Instance.QuickInfo.MaxWidth;
				}
			}
		}

		static void ShowColorPreview(ISymbol symbol, ThemedToolTip tip) {
			var preview = QuickInfo.ColorQuickInfoUI.PreviewColorProperty(symbol as IPropertySymbol, false);
			if (preview != null) {
				tip.Add(preview.WrapMargin(WpfHelper.MiddleMargin));
			}
		}

		internal static TTarget SetTipOptions<TTarget>(this TTarget target)
			where TTarget : DependencyObject {
			ToolTipService.SetBetweenShowDelay(target, 100);
			ToolTipService.SetInitialShowDelay(target, 400);
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

		internal static Grid ShowNumericRepresentations(SyntaxNode node) {
			return ShowNumericRepresentations(node.GetFirstToken().Value, node.Parent.IsKind(SyntaxKind.UnaryMinusExpression) ? NumericForm.Negative : NumericForm.None);
		}

		internal static Grid ShowNumericRepresentations(object value) {
			return ShowNumericRepresentations(value, NumericForm.None);
		}

		internal static Grid ShowNumericRepresentations(IFieldSymbol symbol) {
			return ShowNumericRepresentations(symbol.ConstantValue, NumericForm.None);
		}

		static Grid ShowNumericRepresentations(object value, NumericForm form) {
			if (value == null) {
				return null;
			}
			switch (Type.GetTypeCode(value.GetType())) {
				case TypeCode.Int32: return ShowInt((int)value, form);
				case TypeCode.Int64: return ShowInt64((long)value, form);
				case TypeCode.Byte: return ShowNumberAndBytes(((byte)value).ToString(), new byte[] { (byte)value });
				case TypeCode.Single: return ShowSingle((float)value, form);
				case TypeCode.Double: return ShowDouble((double)value, form);
				case TypeCode.Int16: return ShowInt16((short)value, form);
				case TypeCode.Char: return ShowChar((char)value);
				case TypeCode.UInt32: return ShowNumericRepresentations((int)(uint)value, NumericForm.Unsigned);
				case TypeCode.UInt16: return ShowNumericRepresentations((short)(ushort)value, NumericForm.Unsigned);
				case TypeCode.UInt64: return ShowNumericRepresentations((long)(ulong)value, NumericForm.Unsigned);
				case TypeCode.SByte: return ShowNumberAndBytes(((sbyte)value).ToString(), new byte[] { (byte)(sbyte)value });
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
						new ThemedTipText(number) {
							Background = ThemeHelper.TextBoxBackgroundBrush.Alpha(0.5),
							Foreground = ThemeHelper.TextBoxBrush,
							Padding = WpfHelper.SmallHorizontalMargin,
							FontFamily = ThemeHelper.CodeTextFont
						}.WrapBorder(ThemeHelper.TextBoxBorderBrush, WpfHelper.TinyMargin).SetValue(Grid.SetColumn, 1),
						ToHexString(new ThemedTipText() {
							Background = ThemeHelper.TextBoxBackgroundBrush.Alpha(0.5),
							Foreground = ThemeHelper.TextBoxBrush,
							Padding = WpfHelper.SmallHorizontalMargin,
							FontFamily = ThemeHelper.CodeTextFont
						}, bytes).WrapBorder(ThemeHelper.TextBoxBorderBrush, WpfHelper.TinyMargin).SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 1),
						ToBinString(new ThemedTipText() {
							Background = ThemeHelper.TextBoxBackgroundBrush.Alpha(0.5),
							Foreground = ThemeHelper.TextBoxBrush,
							Padding = WpfHelper.SmallHorizontalMargin,
							FontFamily = ThemeHelper.CodeTextFont
						}, bytes).WrapBorder(ThemeHelper.TextBoxBorderBrush, WpfHelper.TinyMargin).SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 2),
					},
					Margin = WpfHelper.MiddleBottomMargin,
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
						inlines.Add(b.ToString("X2"));
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

			Grid ShowInt(int v, NumericForm f) {
				if (f == NumericForm.Negative) {
					v = -v;
				}
				return ShowNumberAndBytes(
					f == NumericForm.Unsigned ? ((uint)v).ToString() : v.ToString(),
					new byte[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });
			}

			Grid ShowInt64(long v, NumericForm f) {
				if (f == NumericForm.Negative) {
					v = -v;
				}
				return ShowNumberAndBytes(
					f == NumericForm.Unsigned ? ((ulong)v).ToString() : v.ToString(),
					new byte[] { (byte)(v >> 56), (byte)(v >> 48), (byte)(v >> 40), (byte)(v >> 32), (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });
			}

			Grid ShowSingle(float v, NumericForm f) {
				if (f == NumericForm.Negative) {
					v = -v;
				}
				return ShowNumberAndBytes(v.ToString(), BitConverter.GetBytes(v));
			}

			Grid ShowDouble(double v, NumericForm f) {
				if (f == NumericForm.Negative) {
					v = -v;
				}
				return ShowNumberAndBytes(v.ToString(), BitConverter.GetBytes(v));
			}

			Grid ShowInt16(short v, NumericForm f) {
				if (f == NumericForm.Negative) {
					v = (short)-v;
				}
				return ShowNumberAndBytes(
					f == NumericForm.Unsigned ? ((ushort)v).ToString() : v.ToString(),
					new byte[] { (byte)(v >> 8), (byte)v });
			}

			Grid ShowChar(char v) {
				return ShowNumberAndBytes(((ushort)v).ToString(), new byte[] { (byte)(v >> 8), (byte)v });
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
