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
			var cs = s.ContainingSymbol;
			ThemedTipText b; // text block for symbol
			if (cs != null) {
				var showNs = Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.SymbolLocation) == false
					&& cs.Kind == SymbolKind.Namespace;
				var showContainer = !showNs
					&& s.Kind != SymbolKind.Namespace
					&& cs.Kind != SymbolKind.Namespace;
				b = new ThemedTipText { FontSize = ThemeCache.ToolTipFontSize, FontFamily = ThemeCache.ToolTipFont };
				if (showContainer) {
					b.Append(VsImageHelper.GetImage(cs.GetImageId()).WrapMargin(WpfHelper.GlyphMargin))
						.AddSymbol(cs, false, formatter)
						.Append(" ");
				}
				formatter.ShowSymbolDeclaration(b.Inlines, s, true, false);
				p.Add(b);

				if (showNs && ((INamespaceSymbol)cs).IsGlobalNamespace == false) {
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
					var ep = (cs as INamedTypeSymbol).GetExtensionParameter()
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
			if (cs != null && (t = symbol.GetContainingTypes().FirstOrDefault(i => i.IsGenericType)) != null) {
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
					if (symbol is IPropertySymbol p) {
						ShowPropertySignature(signature, p, formatter);
					}
					break;
				case SymbolKind.Method:
					formatter.ShowParameters(signature, symbol.GetParameters(), true, true, -1, ((IMethodSymbol)symbol).IsVararg ? ParameterListKind.ArgList : ParameterListKind.Normal);
					break;
				case SymbolKind.Event:
					formatter.ShowParameters(signature, symbol.GetParameters(), true, true);
					break;
				case SymbolKind.NamedType:
					if (symbol is INamedTypeSymbol t && t.TypeKind == TypeKind.Delegate) {
						formatter.ShowParameters(signature, symbol.GetParameters(), true, true);
					}
					break;
				case SymbolKind.Field:
					if (symbol is IFieldSymbol f) {
						if (f.HasConstantValue) {
							formatter.AppendValue(signature.Inlines, symbol, f.ConstantValue);
						}
						else if (f.IsReadOnly && f.ContainingAssembly.GetSourceType() != AssemblySource.Metadata) {
							var val = f.DeclaringSyntaxReferences.GetHardCodedValue();
							if (val != null) {
								signature.Inlines.Add(" = ");
								formatter.ShowExpression(signature.Inlines, val);
							}
						}
					}
					break;
				case SymbolKind.Parameter:
					if (symbol is IParameterSymbol pa && pa.HasExplicitDefaultValue) {
						formatter.AppendValue(signature.Inlines, symbol, pa.ExplicitDefaultValue);
					}
					break;
				case SymbolKind.Local:
					if (symbol is ILocalSymbol l) {
						if (l.HasConstantValue) {
							formatter.AppendValue(signature.Inlines, symbol, l.ConstantValue);
						}
					}
					break;
				case SymbolKind.TypeParameter:
					if (symbol is ITypeParameterSymbol tp) {
						if (tp.Variance != VarianceKind.None) {
							signature.Inlines.InsertBefore(signature.Inlines.FirstInline, tp.Variance.Case(VarianceKind.Out, "out ", "in ").Render(formatter.Keyword));
						}
						if (tp.HasConstraint()) {
							signature.Append(": ");
							formatter.ShowTypeConstraints(tp, signature);
						}
					}

					break;
			}

			return signature;
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

		static void ShowExtensionParameter(StackPanel panel, IParameterSymbol ep, SymbolFormatter formatter) {
			var epa = ep.GetAttributes();
			if (epa.Length != 0) {
				var b = new ThemedTipText(IconIds.ExtensionParameter)
					.Append("(".Render(formatter.PlainText));
				foreach (var item in epa) {
					formatter.Format(b.Inlines, item, 0);
					b.Append(" ");
				}
				b.AddSymbol(ep, false, formatter.Parameter)
					.Append(")".Render(formatter.PlainText));
				panel.Add(b);
			}
		}

		static void ShowPropertySignature(TextBlock signature, IPropertySymbol p, SymbolFormatter formatter) {
			IMethodSymbol m;
			ExpressionSyntax exp, init = null;
			if (p.Parameters.Length > 0) {
				formatter.ShowParameters(signature, p.Parameters, true, true, -1, ParameterListKind.Property);
			}
			if (p.IsReadOnly) {
				var r = p.DeclaringSyntaxReferences;
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
			if ((m = p.GetMethod) != null) {
				if (m.DeclaredAccessibility != Accessibility.Public && m.DeclaredAccessibility != p.DeclaredAccessibility) {
					signature.Append(m.GetAccessibility(), false, false, formatter.Keyword);
				}
				signature.Append("get", false, false, formatter.Keyword).Append("; ");
			}
			if ((m = p.SetMethod) != null) {
				if (m.DeclaredAccessibility != Accessibility.Public && m.DeclaredAccessibility != p.DeclaredAccessibility) {
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

		static void ShowGenericMethodConstraints(StackPanel panel, IMethodSymbol m, SymbolFormatter formatter) {
			if (m.IsBoundedGenericMethod()) {
				ShowTypeParameters(panel, m.TypeParameters, m.TypeArguments, formatter);
			}
			else {
				ShowTypeParameterWithConstraint(panel, m.TypeParameters, formatter);
			}
		}

		static void ShowGenericTypeConstraints(StackPanel panel, INamedTypeSymbol t, SymbolFormatter formatter) {
			do {
				if (t.IsUnboundGenericType) {
					ShowTypeParameterWithConstraint(panel, t.TypeParameters, formatter);
				}
				else if (t.IsGenericType) {
					if (t.IsDefinition == false) {
						ShowTypeParameters(panel, t.TypeParameters, t.TypeArguments, formatter);
					}
					else {
						foreach (var item in t.TypeParameters) {
							if (item.HasConstraint()) {
								panel.Add(ShowTypeParameterConstraints(item, formatter));
							}
						}
					}
				}
			} while ((t = t.ContainingType) != null);
		}

		static void ShowTypeParameters(StackPanel panel, ImmutableArray<ITypeParameterSymbol> tp, ImmutableArray<ITypeSymbol> ta, SymbolFormatter formatter) {
			var tpl = tp.Length;
			for (int i = 0; i < tpl; i++) {
				var b = new TextBlock {
					TextWrapping = TextWrapping.Wrap,
					Foreground = ThemeCache.ToolTipTextBrush,
					FontFamily = ThemeCache.ToolTipFont,
					FontSize = ThemeCache.ToolTipFontSize
				}.SetGlyph(IconIds.GenericDefinition);
				formatter.ShowTypeArgumentInfo(tp[i], ta[i], b.Inlines);
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

		static TextBlock ShowTypeParameterConstraints(ITypeParameterSymbol item, SymbolFormatter formatter) {
			var b = new TextBlock {
				TextWrapping = TextWrapping.Wrap,
				Foreground = ThemeCache.ToolTipTextBrush,
				FontFamily = ThemeCache.ToolTipFont,
				FontSize = ThemeCache.ToolTipFontSize
			}.SetGlyph(IconIds.GenericDefinition)
				.AddSymbol(item, false, formatter.TypeParameter)
				.Append(": ");
			formatter.ShowTypeConstraints(item, b);
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
