using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using CLR;
using Codist.Controls;
using Codist.SyntaxHighlight;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Text.Classification;
using R = Codist.Properties.Resources;

namespace Codist
{
	sealed class SymbolFormatter
	{
		internal const double TransparentLevel = 0.6;
		static readonly Dictionary<string, Action<string, SymbolFormatter>> __BrushSetter = CreatePropertySetter();
		internal static readonly SymbolFormatter Instance = new SymbolFormatter(b => { b?.Freeze(); return b; });
		internal static readonly SymbolFormatter SemiTransparent = new SymbolFormatter(b => {
			if (b != null) {
				b = b.Alpha(TransparentLevel); b.Freeze();
			}
			return b;
		});

		readonly Func<Brush, Brush> _BrushConfigurator;
		SymbolFormatter(Func<Brush, Brush> brushConfigurator) {
			_BrushConfigurator = brushConfigurator;
			foreach (var setter in __BrushSetter) {
				setter.Value(setter.Key, this);
			}
			FormatStore.FormatItemsChanged += FormatMap_FormatMappingChanged;
		}

		/// <summary>
		/// The default text brush.
		/// </summary>
		[ClassificationType(ClassificationTypeNames = Constants.CodePlainText)]
		public Brush PlainText { get; private set; }

		[ClassificationType(ClassificationTypeNames = Constants.CodeClassName)]
		public Brush Class { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpConstFieldName)]
		public Brush Const { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CodeDelegateName)]
		public Brush Delegate { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CodeEnumName)]
		public Brush Enum { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpEnumFieldName)]
		public Brush EnumField { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpEventName)]
		public Brush Event { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpFieldName)]
		public Brush Field { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CodeInterfaceName)]
		public Brush Interface { get; private set; }
		[ClassificationType(ClassificationTypeNames =Constants.CSharpLocalVariableName)]
		public Brush Local { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CodeKeyword)]
		public Brush Keyword { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpMethodName)]
		public Brush Method { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpNamespaceName)]
		public Brush Namespace { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CodeNumber)]
		public Brush Number { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpParameterName)]
		public Brush Parameter { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpPropertyName)]
		public Brush Property { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CodeStructName)]
		public Brush Struct { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CodeString)]
		public Brush Text { get; private set; }
		[ClassificationType(ClassificationTypeNames = Constants.CSharpTypeParameterName)]
		public Brush TypeParameter { get; private set; }

		/// <summary>Display the Codist optimized symbol signature for Super Quick Info alternative style.</summary>
		public StackPanel ShowSignature(ISymbol symbol) {
			INamedTypeSymbol t;
			IMethodSymbol m;
			var s = symbol.Kind != SymbolKind.NamedType || ((INamedTypeSymbol)symbol).IsTupleType == false ? symbol.OriginalDefinition : symbol;
			var p = new StackPanel {
				Margin = WpfHelper.MenuItemMargin,
				MaxWidth = Application.Current.MainWindow.Width
			};

			#region Signature
			var signature = ShowSymbolSignature(UIHelper.IsShiftDown ? symbol : s);
			p.Add(signature);
			if (s.IsObsolete()) {
				MarkSignatureObsolete(p, signature);
			}
			#endregion

			#region Containing symbol
			var cs = s.ContainingSymbol;
			ThemedTipText b; // text block for symbol
			if (cs != null) {
				var showNs = Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.SymbolLocation) == false && cs.Kind == SymbolKind.Namespace;
				var showContainer = showNs == false && s.Kind != SymbolKind.Namespace && cs.Kind != SymbolKind.Namespace;
				b = new ThemedTipText { FontSize = ThemeCache.ToolTipFontSize, FontFamily = ThemeCache.ToolTipFont };
				if (showContainer) {
					b.Append(VsImageHelper.GetImage(cs.GetImageId()).WrapMargin(WpfHelper.GlyphMargin))
						.AddSymbol(cs, false, this)
						.Append(" ");
				}
				ShowSymbolDeclaration(b.Inlines, s, true, false);
				p.Add(b);

				if (showNs && ((INamespaceSymbol)cs).IsGlobalNamespace == false) {
					b = new ThemedTipText { FontSize = ThemeCache.ToolTipFontSize, FontFamily = ThemeCache.ToolTipFont }.Append(VsImageHelper.GetImage(IconIds.Namespace)
						.WrapMargin(WpfHelper.GlyphMargin));
					ShowContainingNamespace(symbol, b);
					p.Add(b);
				}
				else if (s.Kind == SymbolKind.Method) {
					if ((m = (IMethodSymbol)s).MethodKind == MethodKind.ReducedExtension) {
						b.AddImage(IconIds.ExtensionMethod)
							.Append(" ")
							.AddSymbol(m.ReceiverType, false, this);
					}
				}

				if (s.Kind.CeqAny(SymbolKind.Method, SymbolKind.Property, SymbolKind.NamedType)) {
					var ep = (cs as INamedTypeSymbol).GetExtensionParameter()
						?? (s as INamedTypeSymbol).GetExtensionParameter();
					if (ep != null) {
						ShowExtensionParameter(p, ep);
					}
				}
			}
			#endregion

			#region Member type
			var rt = s.GetReturnType();
			if (rt == null) {
				if (s.Kind == SymbolKind.Discard) {
					p.Add(new ThemedTipText { FontSize = ThemeCache.ToolTipFontSize, FontFamily = ThemeCache.ToolTipFont }
						.AddSymbol(((IDiscardSymbol)s).Type, false, this)
						.Append($" ({R.T_Discard})"));
				}
			}
			else if (s.Kind != SymbolKind.Method || ((IMethodSymbol)s).IsTypeSpecialMethod() == false) {
				b = new ThemedTipText { FontSize = ThemeCache.ToolTipFontSize, FontFamily = ThemeCache.ToolTipFont }
					.Append(VsImageHelper.GetImage(IconIds.Return).WrapMargin(WpfHelper.GlyphMargin));
				if (rt.TypeKind != TypeKind.Delegate) {
					b.Append(GetRefType(s), Keyword);
					b.AddSymbol(rt, false, this)
						.Append(rt.IsAwaitable() ? $" ({R.T_Awaitable})" : String.Empty);
				}
				else {
					var invoke = ((INamedTypeSymbol)rt).DelegateInvokeMethod;
					b.Append("delegate ", Keyword)
						.Append(GetRefType(invoke), Keyword)
						.AddSymbol(invoke.ReturnType, null, this)
						.AddParameters(invoke.Parameters, this);
				}
				p.Add(b);
			}
			#endregion

			#region Generic type constraints
			switch (s.Kind) {
				case SymbolKind.NamedType:
					t = (INamedTypeSymbol)symbol;
					if (t.IsGenericType) {
						ShowGenericTypeConstraints(p, t);
					}
					goto END;
				case SymbolKind.Method:
					m = (IMethodSymbol)symbol;
					do {
						if (m.IsGenericMethod) {
							ShowGenericMethodConstraints(p, m);
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
				ShowGenericTypeConstraints(p, t);
			}
			#endregion

			END:
			return p;
		}

		TextBlock ShowSymbolSignature(ISymbol symbol) {
			var signature = new TextBlock {
				Margin = WpfHelper.MiddleBottomMargin,
				TextWrapping = TextWrapping.Wrap,
				Foreground = PlainText,
				FontFamily = ThemeCache.ToolTipFont,
				FontSize = ThemeCache.ToolTipFontSize
			};
			Format(signature.Inlines, symbol, null, true, true);
			TextEditorWrapper.CreateFor(signature);
			signature.Inlines.FirstInline.FontSize = ThemeCache.ToolTipFontSize * 1.2;

			switch (symbol.Kind) {
				case SymbolKind.Property:
					if (symbol is IPropertySymbol p) {
						ShowPropertySignature(signature, p);
					}
					break;
				case SymbolKind.Method:
					ShowParameters(signature, symbol.GetParameters(), true, true, -1, ((IMethodSymbol)symbol).IsVararg ? ParameterListKind.ArgList : ParameterListKind.Normal);
					break;
				case SymbolKind.Event:
					ShowParameters(signature, symbol.GetParameters(), true, true);
					break;
				case SymbolKind.NamedType:
					if (symbol is INamedTypeSymbol t && t.TypeKind == TypeKind.Delegate) {
						ShowParameters(signature, symbol.GetParameters(), true, true);
					}
					break;
				case SymbolKind.Field:
					if (symbol is IFieldSymbol f) {
						if (f.HasConstantValue) {
							AppendValue(signature.Inlines, symbol, f.ConstantValue);
						}
						else if (f.IsReadOnly && f.ContainingAssembly.GetSourceType() != AssemblySource.Metadata) {
							var val = f.DeclaringSyntaxReferences.GetHardCodedValue();
							if (val != null) {
								signature.Inlines.Add(" = ");
								ShowExpression(signature.Inlines, val);
							}
						}
					}
					break;
				case SymbolKind.Parameter:
					if (symbol is IParameterSymbol pa && pa.HasExplicitDefaultValue) {
						AppendValue(signature.Inlines, symbol, pa.ExplicitDefaultValue);
					}
					break;
				case SymbolKind.Local:
					if (symbol is ILocalSymbol l) {
						if (l.HasConstantValue) {
							AppendValue(signature.Inlines, symbol, l.ConstantValue);
						}
					}
					break;
				case SymbolKind.TypeParameter:
					if (symbol is ITypeParameterSymbol tp) {
						if (tp.Variance != VarianceKind.None) {
							signature.Inlines.InsertBefore(signature.Inlines.FirstInline, tp.Variance.Case(VarianceKind.Out, "out ", "in ").Render(Keyword));
						}
						if (tp.HasConstraint()) {
							signature.Append(": ");
							ShowTypeConstraints(tp, signature);
						}
					}

					break;
			}

			return signature;
		}

		static void MarkSignatureObsolete(StackPanel panel, TextBlock signature) {
			panel.Opacity = TransparentLevel;
			signature.Inlines.AddRange(new object[] {
					new LineBreak(),
					new InlineUIContainer (new TextBlock { Margin = WpfHelper.SmallHorizontalMargin, FontSize = ThemeCache.ToolTipFontSize, FontFamily = ThemeCache.ToolTipFont }
						.Append(VsImageHelper.GetImage(IconIds.Obsoleted).WrapMargin(WpfHelper.GlyphMargin))
						.Append(R.T_Deprecated))
				});
		}

		void ShowContainingTypes(INamedTypeSymbol type, InlineCollection text) {
			var n = new Stack<INamedTypeSymbol>();
			do {
				n.Push(type);
				if (n.Count > 50) {
					MessageWindow.Error(String.Join(Environment.NewLine, n.Select(i => i.Name)), "Too many containing types");
					break;
				}
			} while ((type = type.ContainingType) != null);
			while (n.Count != 0) {
				FormatTypeName(text, n.Pop(), null, false);
				text.Add(".");
			}
		}

		void ShowExtensionParameter(StackPanel panel, IParameterSymbol ep) {
			var epa = ep.GetAttributes();
			if (epa.Length != 0) {
				var b = new ThemedTipText(IconIds.ExtensionParameter).Append("(".Render(PlainText));
				foreach (var item in epa) {
					Format(b.Inlines, item, 0);
					b.Append(" ");
				}
				b.AddSymbol(ep, false, Instance.Parameter).Append(")".Render(PlainText));
				panel.Add(b);
			}
		}

		public void ShowContainingNamespace(ISymbol symbol, TextBlock loc) {
			var ns = symbol.ContainingNamespace;
			if (ns == null) {
				return;
			}
			if (ns.IsGlobalNamespace) {
				loc.Append(ns.ToString().Render(Namespace));
				return;
			}
			var n = ImmutableArray.CreateBuilder<INamespaceSymbol>();
			do {
				n.Add(ns);
				ns = ns.ContainingNamespace;
				if (n.Count > 50) {
					MessageWindow.Error(String.Join(Environment.NewLine, n.Select(i => i.Name)), "Too many containing namespaces");
					break;
				}
			} while (ns?.IsGlobalNamespace == false);
			for (int i = n.Count - 1; i > 0; i--) {
				loc.AddSymbol(n[i], false, Namespace).Append(".");
			}
			loc.AddSymbol(n[0], false, Namespace);
		}

		public TextBlock ShowParameters(TextBlock block, ImmutableArray<IParameterSymbol> parameters) {
			return ShowParameters(block, parameters, false, false);
		}
		public TextBlock ShowParameters(TextBlock block, ImmutableArray<IParameterSymbol> parameters, bool showParameterName, bool showDefault, int argIndex = -1, ParameterListKind listKind = ParameterListKind.Normal) {
			ShowParameters(block.Inlines, parameters, showParameterName, showDefault, argIndex, listKind);
			return block;
		}

		public void ShowParameters(InlineCollection inlines, ImmutableArray<IParameterSymbol> parameters, bool showParameterName, bool showDefault, int argIndex, ParameterListKind listKind) {
			inlines.Add(new TextBlock {
				Text = listKind == ParameterListKind.Property ? " [" : " (",
				VerticalAlignment = VerticalAlignment.Top,
				FontFamily = ThemeCache.ToolTipFont,
				FontSize = ThemeCache.ToolTipFontSize,
			});
			var pl = parameters.Length;
			TextBlock inlineBlock = null;
			InlineCollection tmpInlines = null;
			for (var i = 0; i < pl;) {
				if (showParameterName) {
					inlineBlock = new TextBlock {
						Margin = WpfHelper.SmallHorizontalMargin,
						TextWrapping = TextWrapping.Wrap,
						VerticalAlignment = VerticalAlignment.Top
					};
					tmpInlines = inlines;
					inlines = inlineBlock.Inlines;
				}
				var p = parameters[i];
				if (p.IsOptional) {
					inlines.Add("[");
				}
				AddParameterModifier(inlines, p);
				inlines.AddSymbol(p.Type, this);
				if (showParameterName) {
					inlines.Add(" ");
					if (String.IsNullOrEmpty(p.Name)) {
						inlines.Add(("@" + i.ToString()).Render(i == argIndex, false, Parameter));
					}
					else {
						inlines.Add(p.Render(i == argIndex, Parameter));
					}
					if (showDefault && p.HasExplicitDefaultValue) {
						AppendValue(inlines, p, p.ExplicitDefaultValue);
					}
				}
				if (p.IsOptional) {
					inlines.Add("]");
				}
				if (++i < pl) {
					inlines.Add(",");
				}
				if (showParameterName) {
					inlines = tmpInlines;
					inlines.Add(inlineBlock);
				}
			}
			switch (listKind) {
				case ParameterListKind.Property: inlines.Add("]"); break;
				case ParameterListKind.ArgList: inlines.Append("__arglist", Keyword).Add(")"); break;
				default: inlines.Add(")"); break;
			}
		}

		void AddParameterModifier(InlineCollection inlines, IParameterSymbol p) {
			if (p.GetScopedKind() != 0) {
				inlines.Append("scoped ", Keyword);
			}
			switch (p.RefKind) {
				case RefKind.Ref:
					inlines.Append("ref ", Keyword);
					return;
				case RefKind.Out:
					inlines.Append("out ", Keyword);
					return;
				case RefKind.In:
					inlines.Append("in ", Keyword);
					return;
				case CodeAnalysisHelper.RefReadonly:
					inlines.Append("ref readonly ", Keyword);
					return;
			}
			if (p.IsParams) {
				inlines.Append("params ", Keyword);
			}
		}

		void ShowPropertySignature(TextBlock signature, IPropertySymbol p) {
			IMethodSymbol m;
			ExpressionSyntax exp, init = null;
			if (p.Parameters.Length > 0) {
				ShowParameters(signature, p.Parameters, true, true, -1, ParameterListKind.Property);
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
						ShowExpression(signature.Inlines, exp);
						return;
					}
				}
			}
			signature.Append(" { ");
			if ((m = p.GetMethod) != null) {
				if (m.DeclaredAccessibility != Accessibility.Public && m.DeclaredAccessibility != p.DeclaredAccessibility) {
					signature.Append(m.GetAccessibility(), false, false, Keyword);
				}
				signature.Append("get", false, false, Keyword).Append("; ");
			}
			if ((m = p.SetMethod) != null) {
				if (m.DeclaredAccessibility != Accessibility.Public && m.DeclaredAccessibility != p.DeclaredAccessibility) {
					signature.Append(m.GetAccessibility(), false, false, Keyword);
				}
				signature.Append(m.IsInitOnly() ? "init" : "set", false, false, Keyword).Append("; ");
			}
			signature.Append("}");
			if (init != null) {
				signature.Append(" = ");
				ShowExpression(signature.Inlines, init);
			}
		}

		public void AppendValue(InlineCollection text, ISymbol symbol, object value) {
			var r = symbol.DeclaringSyntaxReferences;
			ExpressionSyntax val;
			if (r.Length > 0 && (val = r.GetHardCodedValue()) != null) {
				if (val.IsAnyKind(SyntaxKind.DefaultLiteralExpression, SyntaxKind.NullLiteralExpression) == false) {
					text.Add(" = ");
					ShowExpression(text, val);
				}
				return;
			}
			if (value != null) {
				text.Add(" = ");
				if (value is string s) {
					text.Add(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(s)).ToFullString().Render(Text));
				}
				else if (value is bool b) {
					text.Add((b ? "true" : "false").Render(Keyword));
				}
				else {
					text.Add(value.ToString().Render(Number));
				}
			}
		}

		void ShowExpression(InlineCollection text, ExpressionSyntax exp) {
			if (exp.FullSpan.Length > 300) {
				ShowTruncatedExpression(text, exp);
				return;
			}
			if (ShowCommonExpression(text, exp) == false) {
				ShowExpressionRecursive(text, exp, " ", false);
			}
		}

		static void ShowTruncatedExpression(InlineCollection inlines, ExpressionSyntax exp) {
			var t = exp.ToString();
			inlines.AddRange(new Run[] {
					new Run(t.Substring(0, Math.Min(t.Length, 300))),
					new Run(R.T_ExpressionTooLong)
				});
		}

		bool ShowCommonExpression(InlineCollection inlines, SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.DefaultLiteralExpression:
					inlines.Add("default".Render(Keyword)); return true;
				case SyntaxKind.CharacterLiteralExpression:
				case SyntaxKind.StringLiteralExpression:
					inlines.Add(node.ToString().Render(Text)); return true;
				case SyntaxKind.NumericLiteralExpression:
					inlines.Add(node.ToString().Render(Number)); return true;
				case SyntaxKind.TrueLiteralExpression:
					inlines.Add("true".Render(Keyword)); return true;
				case SyntaxKind.FalseLiteralExpression:
					inlines.Add("false".Render(Keyword)); return true;
				case SyntaxKind.NullLiteralExpression:
					inlines.Add("null".Render(Keyword)); return true;
				case SyntaxKind.IdentifierName:
					inlines.Add(new NodeLink(node)); return true;
			}
			return false;
		}
		void ShowExpressionRecursive(InlineCollection inlines, SyntaxNode node, string whitespace, bool ws) {
			foreach (var item in node.ChildNodesAndTokens()) {
				if (item.IsToken) {
					var t = item.AsToken();
					if (t.HasLeadingTrivia && t.LeadingTrivia.Span.Length > 0) {
						foreach (var lt in t.LeadingTrivia) {
							ShowTrivia(inlines, whitespace, ref ws, lt);
						}
					}
					if (t.IsReservedKeyword()) {
						inlines.Add(t.ToString().Render(Keyword));
					}
					else {
						switch (t.Kind()) {
							case SyntaxKind.CharacterLiteralToken:
							case SyntaxKind.StringLiteralToken:
								inlines.Add(t.ToString().Render(Text)); break;
							case SyntaxKind.NumericLiteralToken:
								inlines.Add(t.ToString().Render(Number)); break;
							default:
								inlines.Add(t.ToString()); break;
						}
					}
					if (t.HasTrailingTrivia) {
						ws = false;
						foreach (var tt in t.TrailingTrivia) {
							ShowTrivia(inlines, whitespace, ref ws, tt);
						}
					}
				}
				else if (item.IsNode) {
					if (ShowCommonExpression(inlines, item.AsNode())) {
						if (item.HasTrailingTrivia) {
							ws = false;
							foreach (var tt in item.GetTrailingTrivia()) {
								ShowTrivia(inlines, whitespace, ref ws, tt);
							}
						}
					}
					else {
						ShowExpressionRecursive(inlines, item.AsNode(), " ", ws);
					}
				}
			}
		}

		static void ShowTrivia(InlineCollection inlines, string whitespace, ref bool ws, SyntaxTrivia trivia) {
			switch (trivia.Kind()) {
				case SyntaxKind.WhitespaceTrivia:
				case SyntaxKind.EndOfLineTrivia:
					if (ws == false) {
						inlines.Add(whitespace ?? trivia.ToString());
						ws = true;
					}
					break;
				//case SyntaxKind.SingleLineCommentTrivia:
				//case SyntaxKind.MultiLineCommentTrivia:
				//	return;
				//default:
				//	text.Add(trivia.ToString());
				//	break;
			}
		}

		void ShowGenericMethodConstraints(StackPanel panel, IMethodSymbol m) {
			if (m.IsBoundedGenericMethod()) {
				ShowTypeParameters(panel, m.TypeParameters, m.TypeArguments);
			}
			else {
				ShowTypeParameterWithConstraint(panel, m.TypeParameters);
			}
		}

		void ShowGenericTypeConstraints(StackPanel panel, INamedTypeSymbol t) {
			do {
				if (t.IsUnboundGenericType) {
					ShowTypeParameterWithConstraint(panel, t.TypeParameters);
				}
				else if (t.IsGenericType) {
					if (t.IsDefinition == false) {
						ShowTypeParameters(panel, t.TypeParameters, t.TypeArguments);
					}
					else {
						foreach (var item in t.TypeParameters) {
							if (item.HasConstraint()) {
								panel.Add(ShowTypeParameterConstraints(item));
							}
						}
					}
				}
			} while ((t = t.ContainingType) != null);
		}

		void ShowTypeParameters(StackPanel panel, ImmutableArray<ITypeParameterSymbol> tp, ImmutableArray<ITypeSymbol> ta) {
			var tpl = tp.Length;
			for (int i = 0; i < tpl; i++) {
				var b = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = ThemeCache.ToolTipTextBrush, FontFamily = ThemeCache.ToolTipFont, FontSize = ThemeCache.ToolTipFontSize }
					.SetGlyph(IconIds.GenericDefinition);
				ShowTypeArgumentInfo(tp[i], ta[i], b.Inlines);
				panel.Add(b);
			}
		}

		void ShowTypeParameterWithConstraint(StackPanel panel, ImmutableArray<ITypeParameterSymbol> parameters) {
			foreach (var item in parameters) {
				if (item.HasConstraint()) {
					panel.Add(ShowTypeParameterConstraints(item));
				}
			}
		}

		TextBlock ShowTypeParameterConstraints(ITypeParameterSymbol item) {
			var b = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = ThemeCache.ToolTipTextBrush, FontFamily = ThemeCache.ToolTipFont, FontSize = ThemeCache.ToolTipFontSize }
				.SetGlyph(IconIds.GenericDefinition)
				.AddSymbol(item, false, TypeParameter)
				.Append(": ");
			ShowTypeConstraints(item, b);
			return b;
		}

		public void ShowSymbolDeclaration(InlineCollection inlines, ISymbol symbol, bool defaultPublic, bool hideTypeKind) {
			if (defaultPublic == false || symbol.DeclaredAccessibility != Accessibility.Public) {
				inlines.Append(symbol.GetAccessibility(), Keyword);
			}
			switch (symbol.Kind) {
				case SymbolKind.Field: ShowFieldDeclaration(symbol as IFieldSymbol, inlines); break;
				case SymbolKind.Local: ShowLocalDeclaration(symbol as ILocalSymbol, inlines); break;
				case SymbolKind.Parameter: ShowParameterDeclaration(symbol as IParameterSymbol, inlines); break;
				default: ShowSymbolDeclaration(symbol, inlines); break;
			}
			if (hideTypeKind == false) {
				inlines.Append(symbol.GetSymbolKindName(), symbol.Kind == SymbolKind.NamedType ? Keyword : null)
					.Append(" ");
			}
		}

		public void ShowTypeArgumentInfo(ITypeParameterSymbol typeParameter, ITypeSymbol typeArgument, InlineCollection inlines) {
			inlines.Add(typeParameter.Render(null, TypeParameter));
			inlines.Append(" is ", PlainText)
				.AddSymbol(typeArgument, true, this);
			if (typeParameter.HasConstraint()) {
				inlines.Append(" (", PlainText);
				ShowTypeConstraints(typeParameter, inlines);
				inlines.Append(")", PlainText);
			}
		}

		public void ShowTypeConstraints(ITypeParameterSymbol typeParameter, TextBlock text) {
			ShowTypeConstraints(typeParameter, text.Inlines);
		}

		public void ShowTypeConstraints(ITypeParameterSymbol typeParameter, InlineCollection inlines) {
			bool hasConstraint = false;
			if (typeParameter.HasReferenceTypeConstraint) {
				inlines.Append("class", Keyword);
				hasConstraint = true;
			}
			else if (typeParameter.HasValueTypeConstraint) {
				inlines.Append("struct", Keyword);
				hasConstraint = true;
			}
			if (typeParameter.HasUnmanagedTypeConstraint) {
				AppendSeparatorIfHasConstraint(inlines, hasConstraint);
				inlines.Append("unmanaged", Keyword);
				hasConstraint = true;
			}
			if (typeParameter.HasNotNullConstraint()) {
				AppendSeparatorIfHasConstraint(inlines, hasConstraint);
				inlines.Append("notnull", Keyword);
				hasConstraint = true;
			}
			if (typeParameter.AllowRefLikeType()) {
				AppendSeparatorIfHasConstraint(inlines, hasConstraint);
				inlines.Append("allows ref struct", Keyword);
				hasConstraint = true;
			}
			if (typeParameter.HasConstructorConstraint) {
				AppendSeparatorIfHasConstraint(inlines, hasConstraint);
				inlines.Append("new", Keyword);
				inlines.Append("()", PlainText);
				hasConstraint = true;
			}
			foreach (var constraint in typeParameter.ConstraintTypes) {
				AppendSeparatorIfHasConstraint(inlines, hasConstraint);
				Format(inlines, constraint, null, false);
				hasConstraint = true;
			}
		}

		void AppendSeparatorIfHasConstraint(InlineCollection inlines, bool c) {
			if (c) {
				inlines.Append(", ", PlainText);
			}
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

		internal void Format(InlineCollection inlines, ISymbol symbol, string alias, bool bold, bool excludeContainingTypes = false) {
			switch (symbol.Kind) {
				case SymbolKind.ArrayType:
					FormatArrayType(inlines, (IArrayTypeSymbol)symbol, alias, bold);
					return;
				case SymbolKind.Event: FormatEventName(inlines, (IEventSymbol)symbol, alias, bold);
					return;
				case SymbolKind.Field:
					inlines.Add(symbol.Render(alias, bold, ((IFieldSymbol)symbol).IsConst ? Const : Field));
					return;
				case SymbolKind.Method:
					FormatMethodName(inlines, symbol, alias, bold);
					return;
				case SymbolKind.NamedType:
					if (excludeContainingTypes == false && symbol.ContainingType != null) {
						ShowContainingTypes(symbol.ContainingType, inlines);
					}
					FormatTypeName(inlines, symbol, alias, bold);
					return;
				case SymbolKind.Namespace:
					inlines.Add(symbol.Render(alias, bold, Namespace));
					return;
				case SymbolKind.Parameter:
					inlines.Add(symbol.Render(bold, Parameter));
					return;
				case SymbolKind.Property:
					FormatPropertyName(inlines, (IPropertySymbol)symbol, alias, bold);
					return;
				case SymbolKind.Local:
				case SymbolKind.RangeVariable:
					inlines.Add(symbol.Render(bold, Local));
					return;
				case SymbolKind.TypeParameter:
					FormatTypeParameter(inlines, (ITypeParameterSymbol)symbol, alias, bold);
					return;
				case SymbolKind.PointerType:
					Format(inlines, ((IPointerTypeSymbol)symbol).PointedAtType, alias, bold);
					if (alias == null) {
						inlines.Add("*".Render(PlainText));
					}
					return;
				case SymbolKind.ErrorType:
					inlines.Append((symbol as INamedTypeSymbol).GetTypeName() ?? "?", PlainText);
					return;
				case CodeAnalysisHelper.FunctionPointerType:
					inlines.Add((symbol as ITypeSymbol).GetTypeName());
					return;
				case SymbolKind.Label:
					inlines.Add(symbol.Render(bold, null));
					return;
				case SymbolKind.Discard:
					inlines.Add("_".Render(Keyword));
					return;
				default:
					inlines.Add(symbol.Name);
					return;
			}
		}

		void FormatArrayType(InlineCollection inlines, IArrayTypeSymbol a, string alias, bool bold) {
			Format(inlines, a.ElementType, alias, bold);
			if (alias == null) {
				inlines.Add((a.Rank == 1 ? "[]" : $"[{new string(',', a.Rank - 1)}]").Render(PlainText));
			}
		}

		void FormatEventName(InlineCollection inlines, IEventSymbol e, string alias, bool bold) {
			inlines.Add(e.Render(alias ?? e.ExplicitInterfaceImplementations.FirstOrDefault()?.Name, bold, Event));
		}

		void FormatTypeParameter(InlineCollection inlines, ITypeParameterSymbol t, string alias, bool bold) {
			if (alias != null && t.Variance != VarianceKind.None) {
				inlines.Add((t.Variance == VarianceKind.Out ? "out " : "in ").Render(Keyword));
			}
			inlines.Add(t.Render(null, bold, TypeParameter));
		}

		void FormatMethodName(InlineCollection inlines, ISymbol symbol, string alias, bool bold) {
			var method = (IMethodSymbol)symbol;
			Inline inline;
			switch (method.MethodKind) {
				case MethodKind.Constructor:
					inline = symbol.Render(alias ?? method.ContainingType.Name, bold, GetBrushForMethod(method));
					break;
				case MethodKind.LambdaMethod:
					inline = symbol.Render("lambda", true, Method);
					break;
				case CodeAnalysisHelper.FunctionPointerMethod:
					inline = symbol.Render("delegate*", true, GetBrushForMethod(method));
					break;
				case MethodKind.ExplicitInterfaceImplementation:
					var implementations = method.ExplicitInterfaceImplementations;
					inline = method.Render(implementations.Length != 0 ? implementations[0].Name : symbol.Name, bold, Method);
					break;
				default:
					inline = symbol.Render(alias, bold, Method);
					break;
			}
			inlines.Add(inline);
			if (method.IsGenericMethod) {
				AddTypeArguments(inlines, method.TypeArguments);
			}
		}

		void FormatPropertyName(InlineCollection inlines, IPropertySymbol p, string alias, bool bold) {
			inlines.Add(p.Render(alias ?? p.GetOriginalName(), bold, Property));
		}

		void FormatTypeName(InlineCollection inlines, ISymbol symbol, string alias, bool bold) {
			var type = (INamedTypeSymbol)symbol;
			var specialType = type.GetSpecialTypeAlias();
			if (specialType != null) {
				FormatSpecialType(inlines, alias, type, specialType);
				return;
			}
			switch (type.TypeKind) {
				case TypeKind.Class:
					inlines.Add(symbol.Render(alias ?? (type.IsAnonymousType ? "{anonymous}" : null), bold, Class));
					break;
				case TypeKind.Delegate:
					inlines.Add(symbol.Render(alias, bold, Delegate));
					break;
				case TypeKind.Dynamic:
					inlines.Add(symbol.Render(alias ?? symbol.Name, bold, Keyword));
					return;
				case TypeKind.Enum:
					inlines.Add(symbol.Render(alias, bold, Enum));
					return;
				case TypeKind.Interface:
					inlines.Add(symbol.Render(alias, bold, Interface));
					break;
				case TypeKind.Struct:
					if (!FormatStructName(inlines, symbol, alias, bold, type)) {
						return;
					}
					break;
				case TypeKind.TypeParameter:
					inlines.Add(symbol.Render(alias ?? symbol.Name, bold, TypeParameter));
					return;
				case CodeAnalysisHelper.Extension:
					FormatExtensionType(inlines, symbol, bold, type);
					return;
				default:
					inlines.Add(symbol.MetadataName.Render(bold, false, Class));
					return;
			}
			if (type.GetNullableAnnotation() == 2) {
				inlines.Add("?".Render(PlainText));
			}
			if (type.IsGenericType && type.IsTupleType == false) {
				AddTypeArguments(inlines, type.TypeArguments);
			}
		}

		void FormatSpecialType(InlineCollection inlines, string alias, INamedTypeSymbol type, string specialType) {
			inlines.Add((alias ?? specialType).Render(Keyword));
			if (type.GetNullableAnnotation() == 2) {
				inlines.Add("?".Render(PlainText));
			}
		}

		bool FormatStructName(InlineCollection inlines, ISymbol symbol, string alias, bool bold, INamedTypeSymbol type) {
			ITypeSymbol nullable;
			if (type.IsTupleType) {
				inlines.Add("(".Render(PlainText));
				var tupleElements = type.TupleElements;
				for (int i = 0; i < tupleElements.Length; i++) {
					if (i > 0) {
						inlines.Add(", ".Render(PlainText));
					}
					inlines.AddSymbol(tupleElements[i].Type, this)
						.Append(" ")
						.Add(tupleElements[i].Render(null, Field));
				}
				inlines.Add(")".Render(PlainText));
			}
			else if ((nullable = type.GetNullableValueType()) != null) {
				inlines.AddSymbol(nullable, this).Add("?".Render(PlainText));
				return false;
			}
			else {
				inlines.Add(symbol.Render(alias, bold, Struct));
			}

			return true;
		}

		void FormatExtensionType(InlineCollection inlines, ISymbol symbol, bool bold, INamedTypeSymbol type) {
			inlines.Add(symbol.Render(symbol.MetadataName, bold, Class));
			if (type.IsGenericType && type.IsTupleType == false) {
				AddTypeArguments(inlines, type.TypeArguments);
			}
			inlines.Add("(".Render(PlainText));
			Format(inlines, type.GetExtensionParameter().Type, null, true, true);
			inlines.Add(")".Render(PlainText));
		}

		internal Brush GetBrush(ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.ArrayType:
					return GetBrush((IArrayTypeSymbol)symbol);
				case SymbolKind.Event: return Event;
				case SymbolKind.Field: return ((IFieldSymbol)symbol).IsConst ? Const : Field;
				case SymbolKind.Method:
					var method = (IMethodSymbol)symbol;
					return method.MethodKind != MethodKind.Constructor
						? Method
						: GetBrushForMethod(method);
				case SymbolKind.NamedType:
					return GetBrushForType(symbol);
				case SymbolKind.Namespace: return Namespace;
				case SymbolKind.Parameter: return Parameter;
				case SymbolKind.Property: return Property;
				case SymbolKind.Local: return Local;
				case SymbolKind.TypeParameter: return TypeParameter;
				case SymbolKind.PointerType: return GetBrush(((IPointerTypeSymbol)symbol).PointedAtType);
				default: return null;
			}
		}

		Brush GetBrushForMethod(IMethodSymbol m) {
			switch (m.ContainingType?.TypeKind) {
				case TypeKind.Class: return Class;
				case TypeKind.Struct: return Struct;
			}
			return Method;
		}

		Brush GetBrushForType(ISymbol symbol) {
			var type = (INamedTypeSymbol)symbol;
			var specialType = type.GetSpecialTypeAlias();
			if (specialType != null) {
				return Keyword;
			}
			switch (type.TypeKind) {
				case TypeKind.Class: return Class;
				case TypeKind.Delegate: return Delegate;
				case TypeKind.Dynamic: return Keyword;
				case TypeKind.Enum: return Enum;
				case TypeKind.Interface: return Interface;
				case TypeKind.Struct: return Struct;
				case TypeKind.TypeParameter: return TypeParameter;
				default: return Class;
			}
		}

		internal Brush GetBrush(SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.PropertyDeclaration:
				case SyntaxKind.IndexerDeclaration: return Property;
				case SyntaxKind.FieldDeclaration: return ((BaseFieldDeclarationSyntax)node).Modifiers.Any(SyntaxKind.ConstKeyword) ? Const : Field;
				case SyntaxKind.ConstructorDeclaration: return GetBrush(node.Parent);
				case SyntaxKind.MethodDeclaration:
				case SyntaxKind.LocalFunctionStatement: return Method;
				case SyntaxKind.ClassDeclaration:
				case SyntaxKind.DestructorDeclaration:
				case CodeAnalysisHelper.RecordDeclaration: return Class;
				case SyntaxKind.StructDeclaration:
				case CodeAnalysisHelper.RecordStructDeclaration: return Struct;
				case SyntaxKind.InterfaceDeclaration: return Interface;
				case SyntaxKind.EventDeclaration:
				case SyntaxKind.EventFieldDeclaration: return Event;
				case SyntaxKind.DelegateDeclaration: return Delegate;
				case SyntaxKind.EnumDeclaration: return Enum;
				case SyntaxKind.EnumMemberDeclaration: return EnumField;
				case SyntaxKind.NamespaceDeclaration: return Namespace;
				case SyntaxKind.VariableDeclarator: return GetBrush(node.Parent.Parent);
				case SyntaxKind.DefaultSwitchLabel: return Keyword;
				default: return null;
			}
		}

		internal TextBlock Format(TextBlock textBlock, ImmutableArray<SymbolDisplayPart> parts, int argIndex) {
			Format(textBlock.Inlines, parts, argIndex);
			return textBlock;
		}
		internal void Format(InlineCollection inlines, ImmutableArray<SymbolDisplayPart> parts, int argIndex) {
			const SymbolDisplayPartKind ExtensionName = (SymbolDisplayPartKind)29;

			foreach (var part in parts) {
				switch (part.Kind) {
					case SymbolDisplayPartKind.AliasName:
						//todo resolve alias type
						goto default;
					case SymbolDisplayPartKind.ClassName:
						if (part.Symbol.Kind == SymbolKind.Method) {
							inlines.Add(part.Symbol.Render(null, true, Method));
						}
						else if (((INamedTypeSymbol)part.Symbol).IsAnonymousType) {
							inlines.Append("?", Class);
						}
						else {
							inlines.Add(part.Symbol.Render(null, argIndex == Int32.MinValue, Class));
						}
						break;
					case SymbolDisplayPartKind.EnumName:
						inlines.Add(part.Symbol.Render(null, argIndex == Int32.MinValue, Enum));
						break;
					case SymbolDisplayPartKind.InterfaceName:
						inlines.Add(part.Symbol.Render(null, argIndex == Int32.MinValue, Interface));
						break;
					case SymbolDisplayPartKind.MethodName:
						inlines.Add(part.Symbol.Render(null, argIndex != Int32.MinValue, Method));
						break;
					case SymbolDisplayPartKind.ParameterName:
						var p = part.Symbol as IParameterSymbol;
						inlines.Add(p.Render(null, p.Ordinal == argIndex || p.IsParams && argIndex > p.Ordinal, Parameter));
						break;
					case SymbolDisplayPartKind.StructName:
						if (part.Symbol.Kind == SymbolKind.Method) {
							inlines.Add(part.Symbol.Render(null, true, Method));
						}
						else {
							inlines.Add(part.Symbol.Render(null, argIndex == Int32.MinValue, Struct));
						}
						break;
					case SymbolDisplayPartKind.DelegateName:
						inlines.Add(part.Symbol.Render(null, argIndex == Int32.MinValue, Delegate));
						break;
					case SymbolDisplayPartKind.StringLiteral:
						inlines.Add(part.ToString().Render(Text));
						break;
					case SymbolDisplayPartKind.Keyword:
						inlines.Add(part.ToString().Render(Keyword));
						break;
					case SymbolDisplayPartKind.NamespaceName:
						inlines.Add(part.Symbol.Render(null, false, Namespace));
						break;
					case SymbolDisplayPartKind.TypeParameterName:
						inlines.Add(part.Symbol.Render(null, argIndex == Int32.MinValue, TypeParameter));
						break;
					case SymbolDisplayPartKind.FieldName:
						inlines.Add(part.Symbol.Render(null, argIndex == Int32.MinValue, Field));
						break;
					case SymbolDisplayPartKind.PropertyName:
						inlines.Add(part.Symbol.Name.Render(Property));
						break;
					case SymbolDisplayPartKind.EventName:
						inlines.Add(part.Symbol.Name.Render(Event));
						break;
					case ExtensionName:
						inlines.Add(part.Symbol.Render(null, true, Method));
						break;
					default:
						inlines.Add(part.ToString().Render(PlainText));
						break;
				}
			}
		}

		internal void Format(InlineCollection block, AttributeData item, int attributeType) {
			var a = item.AttributeClass.Name;
			block.Add("[".Render(PlainText));
			if (attributeType != 0) {
				block.Add(attributeType.Switch(String.Empty, "return", "field", "assembly", "?").Render(Keyword));
				block.Add(": ".Render(PlainText));
			}
			block.Add(WpfHelper.Render(item.AttributeConstructor ?? (ISymbol)item.AttributeClass, a.EndsWith("Attribute", StringComparison.Ordinal) ? a.Substring(0, a.Length - 9) : a, Class));
			var cas = item.ConstructorArguments;
			if (cas.Length == 0 && item.NamedArguments.Length == 0) {
				var node = item.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
				if (node?.ArgumentList?.Arguments.Count > 0) {
					block.Add(node.ArgumentList.ToString().Render(ThemeCache.SystemGrayTextBrush));
				}
				block.Add("]".Render(PlainText));
				return;
			}
			block.Add("(".Render(PlainText));
			int i = 0;
			if (cas.Length != 0) {
				var pp = item.AttributeConstructor.Parameters[cas.Length - 1].IsParams;
				var cl = pp ? cas.Length : -1;
				foreach (var arg in cas) {
					if (i != 0) {
						block.Add(", ".Render(PlainText));
					}
					Format(block, arg, ++i == cl);
				}
			}
			foreach (var arg in item.NamedArguments) {
				if (++i > 1) {
					block.Add(", ".Render(PlainText));
				}
				var attrMember = item.AttributeClass.GetMembers(arg.Key).FirstOrDefault(m => m.Kind.CeqAny(SymbolKind.Field, SymbolKind.Property));
				if (attrMember != null) {
					block.Add(arg.Key.Render(attrMember.Kind == SymbolKind.Property ? Property : Field));
				}
				else {
					block.Add(arg.Key.Render(false, true, null));
				}
				block.Add("=".Render(PlainText));
				Format(block, arg.Value, false);
			}
			block.Add(")]".Render(PlainText));
		}

		public void ShowFieldConstantText(InlineCollection text, IFieldSymbol field, bool preferHex) {
			if (field.HasConstantValue == false) {
				return;
			}
			ExpressionSyntax exp;
			if (field.HasSource()
				&& (exp = field.DeclaringSyntaxReferences.GetHardCodedValue()) != null) {
				ShowExpression(text, exp);
			}
			else {
				text.Add(preferHex && field.ConstantValue is IFormattable f
					? "0x" + f.ToString("X4", System.Globalization.CultureInfo.InvariantCulture)
					: field.ConstantValue?.ToString() ?? String.Empty);
			}
		}

		void AddTypeArguments(InlineCollection text, ImmutableArray<ITypeSymbol> arguments) {
			if (arguments.Length == 0) {
				return;
			}
			text.Add("<".Render(PlainText));
			for (int i = 0; i < arguments.Length; i++) {
				if (i > 0) {
					text.Add(", ".Render(PlainText));
				}
				var a = arguments[i];
				Format(text, a, a.TypeKind == TypeKind.TypeParameter ? String.Empty : null, false);
			}
			text.Add(">".Render(PlainText));
		}

		void Format(InlineCollection inlines, TypedConstant constant, bool isParams) {
			if (constant.IsNull) {
				inlines.Add("null".Render(Keyword));
				return;
			}
			switch (constant.Kind) {
				case TypedConstantKind.Primitive:
					if (constant.Value is bool b) {
						inlines.Add(WpfHelper.Render(b ? "true" : "false", Keyword));
					}
					else if (constant.Value is string) {
						inlines.Add(constant.ToCSharpString().Render(Text));
					}
					else {
						inlines.Add(constant.ToCSharpString().Render(Number));
					}
					break;
				case TypedConstantKind.Enum:
					var en = constant.ToCSharpString();
					int d;
					if (en.IndexOf('|') != -1) {
						var flags = (constant.Type as INamedTypeSymbol).GetFlaggedEnumFields(constant.Value).ToArray();
						for (int i = 0; i < flags.Length; i++) {
							if (i > 0) {
								inlines.Add(" | ".Render(PlainText));
							}
							inlines.Add(constant.Type.Render(null, Enum));
							inlines.Add(".".Render(PlainText));
							inlines.Add(flags[i].Render(null, EnumField));
						}
					}
					else if ((d = en.LastIndexOf('.')) != -1)  {
						inlines.Add(constant.Type.Render(null, Enum));
						inlines.Add(".".Render(PlainText));
						inlines.Add(en.Substring(d + 1).Render(EnumField));
					}
					else {
						inlines.Add(en.Render(Enum));
					}
					break;
				case TypedConstantKind.Type:
					inlines.Add("typeof".Render(Keyword));
					inlines.Add("(".Render(PlainText));
					Format(inlines, constant.Value as ISymbol, null, false);
					inlines.Add(")".Render(PlainText));
					break;
				case TypedConstantKind.Array:
					if (isParams == false) {
						inlines.Add("new".Render(Keyword));
						inlines.Add("[] { ".Render(PlainText));
					}
					bool c = false;
					foreach (var item in constant.Values) {
						if (c) {
							inlines.Add(", ".Render(PlainText));
						}
						else {
							c = true;
						}
						Format(inlines, item, false);
					}

					if (isParams == false) {
						inlines.Add(" }".Render(PlainText));
					}
					break;
				default:
					inlines.Add(constant.ToCSharpString());
					break;
			}
		}

		void ShowFieldDeclaration(IFieldSymbol field, InlineCollection inlines) {
			if (field.IsConst) {
				inlines.Append("const ", Keyword);
			}
			else {
				if (field.IsStatic) {
					inlines.Append("static ", Keyword);
				}
				if (field.IsReadOnly) {
					inlines.Append("readonly ", Keyword);
				}
				else if (field.IsVolatile) {
					inlines.Append("volatile ", Keyword);
				}
			}
		}

		void ShowLocalDeclaration(ILocalSymbol local, InlineCollection inlines) {
			if (local.IsConst) {
				inlines.Append("const ", Keyword);
			}
			else {
				if (local.IsStatic) {
					inlines.Append("static ", Keyword);
				}
				if (local.GetScopedKind() != 0) {
					inlines.Append("scoped ");
				}
				if (local.IsRef) {
					inlines.Append(local.RefKind.GetText(), Keyword);
				}
				if (local.IsFixed) {
					inlines.Append("fixed ", Keyword);
				}
			}
		}

		void ShowParameterDeclaration(IParameterSymbol parameter, InlineCollection inlines) {
			if (parameter.GetScopedKind() != 0) {
				inlines.Append("scoped ", Keyword);
			}
			var k = parameter.RefKind.GetText();
			if (k.Length != 0) {
				inlines.Append(k, Keyword);
			}
		}

		void ShowSymbolDeclaration(ISymbol symbol, InlineCollection info) {
			INamedTypeSymbol t;
			if (symbol.IsStatic) {
				if (symbol.Kind != SymbolKind.Namespace) {
					info.Append("static ", Keyword);
				}
				if (symbol.IsAbstract && symbol.ContainingType?.TypeKind == TypeKind.Interface) {
					info.Append("abstract ", Keyword);
				}
			}
			else if (symbol.IsAbstract) {
				if ((symbol as INamedTypeSymbol)?.TypeKind != TypeKind.Interface) {
					info.Append("abstract ", Keyword);
				}
			}
			else if (symbol.IsVirtual) {
				info.Append("virtual ", Keyword);
			}
			else if (symbol.IsOverride) {
				info.Append(symbol.IsSealed ? "sealed override " : "override ", Keyword);
				ISymbol o = null;
				switch (symbol.Kind) {
					case SymbolKind.Method: o = ((IMethodSymbol)symbol).OverriddenMethod; break;
					case SymbolKind.Property: o = ((IPropertySymbol)symbol).OverriddenProperty; break;
					case SymbolKind.Event: o = ((IEventSymbol)symbol).OverriddenEvent; break;
				}
				if (o != null) {
					t = o.ContainingType;
					if (t?.IsCommonBaseType() == false) {
						Format(info, o.ContainingType, null, false);
						info.Add(".");
						Format(info, o, null, false);
						info.Add(" ");
					}
				}
			}
			else if (symbol.IsSealed) {
				switch (symbol.Kind) {
					case SymbolKind.NamedType:
						t = (INamedTypeSymbol)symbol;
						switch (t.TypeKind) {
							case TypeKind.Class:
								info.Append("sealed ", Keyword); break;
							case TypeKind.Struct:
								if (t.IsReadOnly()) {
									info.Append("readonly ", Keyword);
								}
								if (t.IsRefLike()) {
									info.Append("ref ", Keyword);
								}
								break;
						}
						break;
					case SymbolKind.Method:
						info.Append("sealed ", Keyword); break;
				}
			}
			if (symbol.Kind == SymbolKind.Method) {
				if (symbol is IMethodSymbol m) {
					if (m.IsAsync) {
						info.Append("async ", Keyword);
					}
					else if (m.IsExtern) {
						info.Append("extern ", Keyword);
					}
					if (m.IsReadOnly()) {
						info.Append("readonly ", Keyword);
					}
					if (m.IsExtensionMember()) {
						info.Append("extension ", Keyword);
					}
				}
			}
			else if (symbol.Kind == SymbolKind.Property && symbol is IPropertySymbol p) {
				if (p.IsRequired()) {
					info.Append("required ", Keyword);
				}
				else if (p.IsExtensionMember()) {
					info.Append("extension ", Keyword);
				}
			}
		}

		static Dictionary<string, Action<string, SymbolFormatter>> CreatePropertySetter() {
			var r = new Dictionary<string, Action<string, SymbolFormatter>>(19, StringComparer.OrdinalIgnoreCase);
			foreach (var item in typeof(SymbolFormatter).GetProperties()) {
				var ctn = item.GetCustomAttribute<ClassificationTypeAttribute>().ClassificationTypeNames;
				var setFormatBrush = ReflectionHelper.CreateSetPropertyMethod<SymbolFormatter, Brush>(item.Name);
				r.Add(ctn, (ct, f) => {
					var brush = (ct == Constants.CodePlainText ? FormatStore.EditorDefaultTextProperties :  FormatStore.GetRunPriorities(ct) ?? FormatStore.EditorDefaultTextProperties).ForegroundBrush;
					if (f._BrushConfigurator != null) {
						brush = f._BrushConfigurator(brush);
					}
					setFormatBrush(f, brush);
				});
			}
			return r;
		}

		void FormatMap_FormatMappingChanged(object sender, EventArgs<IReadOnlyList<string>> e) {
			if (sender is IFormatCache c && c.Category != Constants.CodeText) {
				return;
			}
			foreach (var item in e.Data) {
				if (__BrushSetter.TryGetValue(item, out var updateBrush)) {
					updateBrush(item, this);
				}
			}
		}

		sealed class NodeLink : Run
		{
			SyntaxNode _Node;

			public NodeLink(SyntaxNode node) {
				_Node = node;
				Text = node.ToString();
				MouseEnter += InitEventHandlers;
				Unloaded += NodeLink_Unloaded;
			}

			void InitEventHandlers(object sender, MouseEventArgs e) {
				Cursor = Cursors.Hand;
				MouseEnter -= InitEventHandlers;
				NodeLink_MouseEnter(sender, e);
				MouseEnter += NodeLink_MouseEnter;
				MouseLeave += NodeLink_MouseLeave;
				MouseLeftButtonDown += NodeLink_MouseLeftButtonDown;
			}

			void NodeLink_Unloaded(object sender, RoutedEventArgs e) {
				Unloaded -= NodeLink_Unloaded;
				MouseEnter -= InitEventHandlers;
				MouseEnter -= NodeLink_MouseEnter;
				MouseLeave -= NodeLink_MouseLeave;
				MouseLeftButtonDown -= NodeLink_MouseLeftButtonDown;
				_Node = null;
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void NodeLink_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
				try {
					(await TextEditorHelper.GetMouseOverDocumentView()?.TextBuffer.GetDocument().Project.GetCompilationAsync())
						.GetSemanticModel(_Node.SyntaxTree)
						.GetSymbol(_Node)
						?.GoToDefinition();
				}
				catch (ArgumentException) {
					// hack: for a bug in Roslyn where TextBuffer.GetWorkspace can return null
					// fallback to go to node
					_Node.GetLocation().GoToSource();
				}
				QuickInfo.QuickInfoOverride.DismissQuickInfo(this);
				e.Handled = true;
			}

			void NodeLink_MouseLeave(object sender, MouseEventArgs e) {
				Background = Brushes.Transparent;
			}

			void NodeLink_MouseEnter(object sender, MouseEventArgs e) {
				Background = SystemColors.GrayTextBrush.Alpha(WpfHelper.DimmedOpacity);
			}
		}
	}

	enum ParameterListKind
	{
		Normal,
		Property,
		ArgList
	}
}
