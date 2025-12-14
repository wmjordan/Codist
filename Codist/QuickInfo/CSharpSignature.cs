using System;
using System.Collections.Immutable;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using CLR;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using R = Codist.Properties.Resources;

namespace Codist.QuickInfo
{
	static class CSharpSignature
	{
		/// <summary>Display the Codist optimized symbol signature for Super Quick Info alternative style.</summary>
		public static StackPanel Show(ISymbol symbol, SymbolFormatter formatter) {
			INamedTypeSymbol t;
			IMethodSymbol m;
			var s = symbol.Kind != SymbolKind.NamedType || ((INamedTypeSymbol)symbol).IsTupleType == false ? symbol.OriginalDefinition : symbol;
			var p = new StackPanel {
				Margin = WpfHelper.MenuItemMargin,
				MaxWidth = Application.Current.MainWindow.Width
			};

			#region Signature
			var signature = ShowSymbolSignature(symbol, formatter);
			p.Add(signature);
			if (s.IsObsolete()) {
				MarkSignatureObsolete(p, signature);
			}
			#endregion

			#region Containing symbol
			var c = s.ContainingSymbol; // containing symbol
			ThemedTipText b; // text block for symbol
			if (c != null) {
				var showNs = Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.SymbolLocation) == false
					&& c.Kind == SymbolKind.Namespace;
				var showContainer = !showNs
					&& s.Kind != SymbolKind.Namespace
					&& c.Kind != SymbolKind.Namespace;
				b = new ThemedTipText { FontSize = ThemeCache.ToolTipFontSize, FontFamily = ThemeCache.ToolTipFont };
				if (showContainer) {
					b.Append(VsImageHelper.GetImage(c.GetImageId()).WrapMargin(WpfHelper.GlyphMargin))
						.AddSymbol(c, false, formatter)
						.Append(" ");
				}
				formatter.ShowSymbolDeclaration(b.Inlines, s, true, false);
				p.Add(b);

				if (showNs && ((INamespaceSymbol)c).IsGlobalNamespace == false) {
					b = new ThemedTipText { FontSize = ThemeCache.ToolTipFontSize, FontFamily = ThemeCache.ToolTipFont }
						.Append(VsImageHelper.GetImage(IconIds.Namespace).WrapMargin(WpfHelper.GlyphMargin));
					formatter.ShowContainingNamespace(symbol, b);
					p.Add(b);
				}
				else if (s.Kind == SymbolKind.Method) {
					if ((m = (IMethodSymbol)s).MethodKind == MethodKind.ReducedExtension) {
						b.AddImage(IconIds.ExtensionMethod)
							.Append(" ")
							.AddSymbol(m.ReceiverType, false, formatter);
					}
				}

				if (s.Kind.CeqAny(SymbolKind.Method, SymbolKind.Property, SymbolKind.NamedType)) {
					var ep = (c as INamedTypeSymbol).GetExtensionParameter()
						?? (s as INamedTypeSymbol).GetExtensionParameter();
					if (ep != null) {
						ShowExtensionParameter(p, ep, formatter);
					}
				}
			}
			#endregion

			#region Member type
			var rt = s.GetReturnType();
			if (rt == null) {
				if (s.Kind == SymbolKind.Discard) {
					p.Add(new ThemedTipText { FontSize = ThemeCache.ToolTipFontSize, FontFamily = ThemeCache.ToolTipFont }
						.AddSymbol(((IDiscardSymbol)s).Type, false, formatter)
						.Append($" ({R.T_Discard})"));
				}
			}
			else if (s.Kind != SymbolKind.Method || ((IMethodSymbol)s).IsTypeSpecialMethod() == false) {
				b = new ThemedTipText { FontSize = ThemeCache.ToolTipFontSize, FontFamily = ThemeCache.ToolTipFont }
					.Append(VsImageHelper.GetImage(IconIds.Return).WrapMargin(WpfHelper.GlyphMargin));
				if (rt.TypeKind != TypeKind.Delegate) {
					b.Append(GetRefType(s), formatter.Keyword);
					b.AddSymbol(rt, false, formatter)
						.Append(rt.IsAwaitable() ? $" ({R.T_Awaitable})" : String.Empty);
				}
				else {
					var invoke = ((INamedTypeSymbol)rt).DelegateInvokeMethod;
					b.Append("delegate ", formatter.Keyword)
						.Append(GetRefType(invoke), formatter.Keyword)
						.AddSymbol(invoke.ReturnType, null, formatter)
						.AddParameters(invoke.Parameters, formatter);
				}
				p.Add(b);
			}
			#endregion

			#region Generic type constraints
			switch (s.Kind) {
				case SymbolKind.NamedType:
					t = (INamedTypeSymbol)symbol;
					if (t.IsGenericType) {
						ShowGenericTypeConstraints(p, t, formatter);
					}
					goto END;
				case SymbolKind.Method:
					m = (IMethodSymbol)symbol;
					do {
						if (m.IsGenericMethod) {
							ShowGenericMethodConstraints(p, m, formatter);
						}
					} while ((m = m.ContainingSymbol as IMethodSymbol) != null);
					break;
				case SymbolKind.Namespace:
				case SymbolKind.ErrorType:
				case SymbolKind.Label:
				case SymbolKind.TypeParameter:
				case SymbolKind.RangeVariable:
				case SymbolKind.DynamicType:
				case SymbolKind.Discard:
					goto END;
			}
			if (c != null && (t = symbol.GetContainingTypes().FirstOrDefault(i => i.IsGenericType)) != null) {
				ShowGenericTypeConstraints(p, t, formatter);
			}
			#endregion

		END:
			return p;
		}

		static TextBlock ShowSymbolSignature(ISymbol symbol, SymbolFormatter formatter) {
			var signature = new TextBlock {
				Margin = WpfHelper.MiddleBottomMargin,
				TextWrapping = TextWrapping.Wrap,
				Foreground = formatter.PlainText,
				FontFamily = ThemeCache.ToolTipFont,
				FontSize = ThemeCache.ToolTipFontSize
			};
			formatter.Format(signature.Inlines, symbol, null, true, true, true);
			symbol = symbol.OriginalDefinition;
			TextEditorWrapper.CreateFor(signature);
			signature.Inlines.FirstInline.FontSize = ThemeCache.ToolTipFontSize * 1.2;

			switch (symbol.Kind) {
				case SymbolKind.Property:
					ShowProperty((IPropertySymbol)symbol, formatter, signature);
					break;
				case SymbolKind.Method:
					ShowMethod((IMethodSymbol)symbol, formatter, signature);
					break;
				case SymbolKind.Event:
					formatter.ShowParameters(signature, ((IEventSymbol)symbol).AddMethod.Parameters, true, true);
					break;
				case SymbolKind.NamedType:
					ShowDelegateParameters((ITypeSymbol)symbol, formatter, signature);
					break;
				case SymbolKind.Field:
					ShowField((IFieldSymbol)symbol, formatter, signature);
					break;
				case SymbolKind.Parameter:
					ShowParameter((IParameterSymbol)symbol, formatter, signature);
					break;
				case SymbolKind.Local:
					ShowLocal((ILocalSymbol)symbol, formatter, signature);
					break;
				case SymbolKind.TypeParameter:
					ShowTypeParameter((ITypeParameterSymbol)symbol, formatter, signature);
					break;
			}

			return signature;
		}

		static void ShowMethod(IMethodSymbol method, SymbolFormatter formatter, TextBlock signature) {
			formatter.ShowParameters(signature, method.Parameters, true, true, -1, method.IsVararg ? ParameterListKind.ArgList : ParameterListKind.Normal);
		}

		static void ShowProperty(IPropertySymbol property, SymbolFormatter formatter, TextBlock signature) {
			IMethodSymbol m;
			ExpressionSyntax exp, init = null;
			if (property.Parameters.Length > 0) {
				formatter.ShowParameters(signature, property.Parameters, true, true, -1, ParameterListKind.Property);
			}
			if (property.IsReadOnly) {
				var r = property.DeclaringSyntaxReferences;
				if (r.Length > 0 && r[0].GetSyntax() is BasePropertyDeclarationSyntax s) {
					if (s.IsKind(SyntaxKind.PropertyDeclaration)) {
						var pd = (PropertyDeclarationSyntax)s;
						exp = pd.ExpressionBody?.Expression;
						init = pd.Initializer?.Value;
					}
					else if (s.IsKind(SyntaxKind.IndexerDeclaration)) {
						exp = ((IndexerDeclarationSyntax)s).ExpressionBody?.Expression;
					}
					else {
						exp = null;
					}
					if (exp != null) {
						signature.Append(" => ");
						formatter.ShowExpression(signature.Inlines, exp);
						return;
					}
				}
			}
			signature.Append(" { ");
			if ((m = property.GetMethod) != null) {
				if (m.DeclaredAccessibility != Accessibility.Public && m.DeclaredAccessibility != property.DeclaredAccessibility) {
					signature.Append(m.GetAccessibility(), false, false, formatter.Keyword);
				}
				signature.Append("get", false, false, formatter.Keyword).Append("; ");
			}
			if ((m = property.SetMethod) != null) {
				if (m.DeclaredAccessibility != Accessibility.Public && m.DeclaredAccessibility != property.DeclaredAccessibility) {
					signature.Append(m.GetAccessibility(), false, false, formatter.Keyword);
				}
				signature.Append(m.IsInitOnly() ? "init" : "set", false, false, formatter.Keyword).Append("; ");
			}
			signature.Append("}");
			if (init != null) {
				signature.Append(" = ");
				formatter.ShowExpression(signature.Inlines, init);
			}
		}

		static void ShowDelegateParameters(ITypeSymbol t, SymbolFormatter formatter, TextBlock signature) {
			if (t.TypeKind == TypeKind.Delegate) {
				formatter.ShowParameters(signature, ((INamedTypeSymbol)t).DelegateInvokeMethod.GetParameters(), true, true);
			}
		}

		static void ShowField(IFieldSymbol field, SymbolFormatter formatter, TextBlock signature) {
			if (field.HasConstantValue) {
				formatter.AppendValue(signature.Inlines, field, field.ConstantValue);
			}
			else if (field.IsReadOnly && field.ContainingAssembly.GetSourceType() != AssemblySource.Metadata) {
				var val = field.DeclaringSyntaxReferences.GetHardCodedValue();
				if (val != null) {
					signature.Inlines.Add(" = ");
					formatter.ShowExpression(signature.Inlines, val);
				}
			}
		}

		static void ShowParameter(IParameterSymbol parameter, SymbolFormatter formatter, TextBlock signature) {
			if (parameter.HasExplicitDefaultValue) {
				formatter.AppendValue(signature.Inlines, parameter, parameter.ExplicitDefaultValue);
			}
		}

		static void ShowLocal(ILocalSymbol local, SymbolFormatter formatter, TextBlock signature) {
			if (local.HasConstantValue) {
				formatter.AppendValue(signature.Inlines, local, local.ConstantValue);
			}
		}

		static void ShowTypeParameter(ITypeParameterSymbol tp, SymbolFormatter formatter, TextBlock signature) {
			if (tp.Variance != VarianceKind.None) {
				signature.Inlines.InsertBefore(signature.Inlines.FirstInline, tp.Variance.Case(VarianceKind.Out, "out ", "in ").Render(formatter.Keyword));
			}
			if (tp.HasConstraint()) {
				signature.Append(": ");
				formatter.ShowTypeConstraints(tp, signature);
			}
		}

		static void MarkSignatureObsolete(StackPanel panel, TextBlock signature) {
			panel.Opacity = SymbolFormatter.TransparentLevel;
			signature.Inlines.AddRange(new object[] {
					new LineBreak(),
					new InlineUIContainer (new TextBlock {
							Margin = WpfHelper.SmallHorizontalMargin,
							FontSize = ThemeCache.ToolTipFontSize,
							FontFamily = ThemeCache.ToolTipFont
						}.Append(VsImageHelper.GetImage(IconIds.Obsoleted).WrapMargin(WpfHelper.GlyphMargin))
						.Append(R.T_Deprecated)
					)
				});
		}

		static void ShowExtensionParameter(StackPanel panel, IParameterSymbol parameter, SymbolFormatter formatter) {
			var epa = parameter.GetAttributes();
			if (epa.Length != 0) {
				var b = new ThemedTipText(IconIds.ExtensionParameter)
					.Append("(".Render(formatter.PlainText));
				foreach (var item in epa) {
					formatter.Format(b.Inlines, item, 0);
					b.Append(" ");
				}
				b.AddSymbol(parameter, false, formatter.Parameter)
					.Append(")".Render(formatter.PlainText));
				panel.Add(b);
			}
		}

		static void ShowGenericMethodConstraints(StackPanel panel, IMethodSymbol method, SymbolFormatter formatter) {
			if (method.IsBoundedGenericMethod()) {
				ShowTypeParameters(panel, method.TypeParameters, method.TypeArguments, formatter);
			}
			else {
				ShowTypeParameterWithConstraint(panel, method.TypeParameters, formatter);
			}
		}

		static void ShowGenericTypeConstraints(StackPanel panel, INamedTypeSymbol type, SymbolFormatter formatter) {
			do {
				if (type.IsUnboundGenericType) {
					ShowTypeParameterWithConstraint(panel, type.TypeParameters, formatter);
				}
				else if (type.IsGenericType) {
					if (type.IsDefinition == false) {
						ShowTypeParameters(panel, type.TypeParameters, type.TypeArguments, formatter);
					}
					else {
						foreach (var item in type.TypeParameters) {
							if (item.HasConstraint()) {
								panel.Add(ShowTypeParameterConstraints(item, formatter));
							}
						}
					}
				}
			} while ((type = type.ContainingType) != null);
		}

		static void ShowTypeParameters(StackPanel panel, ImmutableArray<ITypeParameterSymbol> parameters, ImmutableArray<ITypeSymbol> arguments, SymbolFormatter formatter) {
			var tpl = parameters.Length;
			for (int i = 0; i < tpl; i++) {
				var b = new TextBlock {
					TextWrapping = TextWrapping.Wrap,
					Foreground = ThemeCache.ToolTipTextBrush,
					FontFamily = ThemeCache.ToolTipFont,
					FontSize = ThemeCache.ToolTipFontSize
				}.SetGlyph(IconIds.GenericDefinition);
				formatter.ShowTypeArgumentInfo(parameters[i], arguments[i], b.Inlines);
				panel.Add(b);
			}
		}

		static void ShowTypeParameterWithConstraint(StackPanel panel, ImmutableArray<ITypeParameterSymbol> parameters, SymbolFormatter formatter) {
			foreach (var item in parameters) {
				if (item.HasConstraint()) {
					panel.Add(ShowTypeParameterConstraints(item, formatter));
				}
			}
		}

		static TextBlock ShowTypeParameterConstraints(ITypeParameterSymbol parameter, SymbolFormatter formatter) {
			var b = new TextBlock {
				TextWrapping = TextWrapping.Wrap,
				Foreground = ThemeCache.ToolTipTextBrush,
				FontFamily = ThemeCache.ToolTipFont,
				FontSize = ThemeCache.ToolTipFontSize
			}.SetGlyph(IconIds.GenericDefinition)
				.AddSymbol(parameter, false, formatter.TypeParameter)
				.Append(": ");
			formatter.ShowTypeConstraints(parameter, b);
			return b;
		}

		static string GetRefType(ISymbol symbol) {
			if (symbol is IMethodSymbol m) {
				if (m.ReturnsByRefReadonly) {
					return "ref readonly ";
				}
				if (m.ReturnsByRef) {
					return "ref ";
				}
			}
			else if (symbol is IPropertySymbol p) {
				if (p.ReturnsByRefReadonly) {
					return "ref readonly ";
				}
				if (p.ReturnsByRef) {
					return "ref ";
				}
			}
			return null;
		}
	}
}
