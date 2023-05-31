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

		static readonly IEditorFormatMap __CodeFormatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(Constants.CodeText);
		static readonly IClassificationFormatMap __CodeClassificationFormatMap = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap(Constants.CodeText);
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
			var cfm = __CodeClassificationFormatMap;
			foreach (var setter in __BrushSetter) {
				setter.Value(setter.Key, this);
			}
			__CodeFormatMap.FormatMappingChanged += FormatMap_FormatMappingChanged;
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
			var signature = ShowSymbolSignature(Keyboard.Modifiers == ModifierKeys.Shift ? symbol : s);
			p.Add(signature);
			if (s.IsObsolete()) {
				p.Opacity = TransparentLevel;
				signature.Inlines.AddRange(new object[] {
					new LineBreak(),
					new InlineUIContainer (new TextBlock { Margin = WpfHelper.SmallHorizontalMargin }
						.Append(ThemeHelper.GetImage(IconIds.Obsoleted).WrapMargin(WpfHelper.GlyphMargin))
						.Append(R.T_Deprecated))
				});
			}
			#endregion

			#region Containing symbol
			var cs = s.ContainingSymbol;
			if (cs != null) {
				var showNs = Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.SymbolLocation) == false && cs.Kind == SymbolKind.Namespace;
				var showContainer = showNs == false && s.Kind != SymbolKind.Namespace && cs.Kind != SymbolKind.Namespace;
				var csb = new ThemedTipText();
				if (showContainer) {
					csb.Append(ThemeHelper.GetImage(cs.GetImageId()).WrapMargin(WpfHelper.GlyphMargin));
				}
				if (cs is INamedTypeSymbol ct && (ct = ct.ContainingType) != null) {
					ShowContainingTypes(ct, csb);
				}

				if (showContainer) {
					csb.AddSymbol(cs, false, this).Append(" ");
				}

				p.Add(ShowSymbolDeclaration(s, csb, true, false));
				if (showNs && ((INamespaceSymbol)cs).IsGlobalNamespace == false) {
					var nsb = new ThemedTipText().Append(ThemeHelper.GetImage(IconIds.Namespace)
						.WrapMargin(WpfHelper.GlyphMargin));
					ShowContainingNamespace(symbol, nsb);
					p.Add(nsb);
				}
				else if (s.Kind == SymbolKind.Method
					&& (m = (IMethodSymbol)s).MethodKind == MethodKind.ReducedExtension) {
					csb.AddImage(IconIds.ExtensionMethod)
						.Append(" ")
						.AddSymbol(m.ReducedFrom.Parameters[0].Type, false, this);
				}
			}
			#endregion

			#region Member type
			var rt = s.GetReturnType();
			if (rt == null) {
				if (s.Kind == SymbolKind.Discard) {
					p.Add(new ThemedTipText()
						.AddSymbol(((IDiscardSymbol)s).Type, false, this)
						.Append($" ({R.T_Discard})"));
				}
			}
			else if (s.Kind != SymbolKind.Method || ((IMethodSymbol)s).IsTypeSpecialMethod() == false) {
				p.Add(new ThemedTipText()
					.Append(ThemeHelper.GetImage(IconIds.Return).WrapMargin(WpfHelper.GlyphMargin))
					.Append(GetRefType(s), Keyword)
					.AddSymbol(rt, false, this)
					.Append(rt.IsAwaitable() ? $" ({R.T_Awaitable})" : String.Empty));
			}
			#endregion

			#region Generic type parameters
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
			if (cs != null && (t = symbol.GetContainingTypes().FirstOrDefault(ct => ct.IsGenericType)) != null) {
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
				Foreground = PlainText
			}.AddSymbol(symbol, true, this);
			TextEditorWrapper.CreateFor(signature);
			signature.Inlines.FirstInline.FontSize = ThemeHelper.ToolTipFontSize * 1.2;

			switch (symbol.Kind) {
				case SymbolKind.Property:
					if (symbol is IPropertySymbol p) {
						ShowPropertySignature(signature, p);
					}
					break;
				case SymbolKind.Method:
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
					if (symbol is ILocalSymbol l && l.HasConstantValue) {
						AppendValue(signature.Inlines, symbol, l.ConstantValue);
					}
					break;
				case SymbolKind.TypeParameter:
					if (symbol is ITypeParameterSymbol tp) {
						if (tp.Variance != VarianceKind.None) {
							signature.Inlines.InsertBefore(signature.Inlines.FirstInline, (tp.Variance == VarianceKind.Out ? "out " : "in ").Render(Keyword));
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

		void ShowContainingTypes(INamedTypeSymbol type, TextBlock signature) {
			var n = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
			do {
				n.Add(type);
			} while ((type = type.ContainingType) != null);
			for (int i = n.Count - 1; i >= 0; i--) {
				signature.AddSymbol(n[i], false, this).Append(".");
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
			} while (ns?.IsGlobalNamespace == false);
			for (int i = n.Count - 1; i > 0; i--) {
				loc.AddSymbol(n[i], false, Namespace).Append(".");
			}
			loc.AddSymbol(n[0], false, Namespace);
		}

		public TextBlock ShowParameters(TextBlock block, ImmutableArray<IParameterSymbol> parameters) {
			return ShowParameters(block, parameters, false, false);
		}
		public TextBlock ShowParameters(TextBlock block, ImmutableArray<IParameterSymbol> parameters, bool showParameterName, bool showDefault, int argIndex = -1, bool isProperty = false) {
			var inlines = block.Inlines;
			inlines.Add(new TextBlock {
				Text = isProperty ? " [" : " (",
				VerticalAlignment = VerticalAlignment.Top
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
				Format(inlines, p.Type, null, false);
				if (showParameterName) {
					inlines.Add(" ");
					if (String.IsNullOrEmpty(p.Name)) {
						inlines.Add(("@" + i.ToString()).Render(i == argIndex, false, Parameter));
					}
					else {
						inlines.Add(p.Render(null, i == argIndex, Parameter));
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
			inlines.Add(isProperty ? "]" : ")");
			return block;
		}

		void AddParameterModifier(InlineCollection inlines, IParameterSymbol p) {
			switch (p.RefKind) {
				case RefKind.Ref:
					inlines.Add(new Run("ref ") {
						Foreground = Keyword
					});
					return;
				case RefKind.Out:
					inlines.Add(new Run("out ") {
						Foreground = Keyword
					});
					return;
				case RefKind.In:
					inlines.Add(new Run("in ") {
						Foreground = Keyword
					});
					return;
			}
			if (p.IsParams) {
				inlines.Add(new Run("params ") {
					Foreground = Keyword
				});
			}
		}

		void ShowPropertySignature(TextBlock signature, IPropertySymbol p) {
			IMethodSymbol m;
			ExpressionSyntax exp, init = null;
			if (p.Parameters.Length > 0) {
				ShowParameters(signature, p.Parameters, true, true, -1, true);
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
				if (val.Kind().IsAny(SyntaxKind.DefaultLiteralExpression, SyntaxKind.NullLiteralExpression) == false) {
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
				text.AddRange(new object[] {
					new Run(exp.ToString().Substring(0, 300)),
					new Run(R.T_ExpressionTooLong)
				});
				return;
			}
			if (ShowCommonExpression(text, exp) == false) {
				ShowExpressionRecursive(text, exp, " ", false);
			}
		}

		bool ShowCommonExpression(InlineCollection text, SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.DefaultLiteralExpression:
					text.Add("default".Render(Keyword)); return true;
				case SyntaxKind.CharacterLiteralExpression:
				case SyntaxKind.StringLiteralExpression:
					text.Add(node.ToString().Render(Text)); return true;
				case SyntaxKind.NumericLiteralExpression:
					text.Add(node.ToString().Render(Number)); return true;
				case SyntaxKind.TrueLiteralExpression:
					text.Add("true".Render(Keyword)); return true;
				case SyntaxKind.FalseLiteralExpression:
					text.Add("false".Render(Keyword)); return true;
				case SyntaxKind.NullLiteralExpression:
					text.Add("null".Render(Keyword)); return true;
				case SyntaxKind.IdentifierName:
					text.Add(new NodeLink(node)); return true;
			}
			return false;
		}
		void ShowExpressionRecursive(InlineCollection text, SyntaxNode node, string whitespace, bool ws) {
			foreach (var item in node.ChildNodesAndTokens()) {
				if (item.IsToken) {
					var t = item.AsToken();
					if (t.HasLeadingTrivia && t.LeadingTrivia.Span.Length > 0) {
						foreach (var lt in t.LeadingTrivia) {
							ShowTrivia(text, whitespace, ref ws, lt);
						}
					}
					if (t.IsReservedKeyword()) {
						text.Add(t.ToString().Render(Keyword));
					}
					else {
						switch (t.Kind()) {
							case SyntaxKind.CharacterLiteralToken:
							case SyntaxKind.StringLiteralToken:
								text.Add(t.ToString().Render(Text)); break;
							case SyntaxKind.NumericLiteralToken:
								text.Add(t.ToString().Render(Number)); break;
							default:
								text.Add(t.ToString()); break;
						}
					}
					if (t.HasTrailingTrivia) {
						ws = false;
						foreach (var tt in t.TrailingTrivia) {
							ShowTrivia(text, whitespace, ref ws, tt);
						}
					}
				}
				else if (item.IsNode) {
					if (ShowCommonExpression(text, item.AsNode())) {
						if (item.HasTrailingTrivia) {
							ws = false;
							foreach (var tt in item.GetTrailingTrivia()) {
								ShowTrivia(text, whitespace, ref ws, tt);
							}
						}
					}
					else {
						ShowExpressionRecursive(text, item.AsNode(), " ", ws);
					}
				}
			}
		}

		static void ShowTrivia(InlineCollection text, string whitespace, ref bool ws, SyntaxTrivia trivia) {
			switch (trivia.Kind()) {
				case SyntaxKind.WhitespaceTrivia:
				case SyntaxKind.EndOfLineTrivia:
					if (ws == false) {
						text.Add(whitespace ?? trivia.ToString());
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
				var b = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = ThemeHelper.ToolTipTextBrush }
					.SetGlyph(ThemeHelper.GetImage(IconIds.GenericDefinition));
				ShowTypeArgumentInfo(tp[i], ta[i], b);
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
			var b = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = ThemeHelper.ToolTipTextBrush }
				.SetGlyph(ThemeHelper.GetImage(IconIds.GenericDefinition))
				.AddSymbol(item, false, TypeParameter)
				.Append(": ");
			ShowTypeConstraints(item, b);
			return b;
		}

		public TextBlock ShowSymbolDeclaration(ISymbol symbol, TextBlock info, bool defaultPublic, bool hideTypeKind) {
			if (defaultPublic == false || symbol.DeclaredAccessibility != Accessibility.Public) {
				info.Append(symbol.GetAccessibility(), Keyword);
			}
			if (symbol.Kind == SymbolKind.Field) {
				ShowFieldDeclaration(symbol as IFieldSymbol, info);
			}
			else if (symbol.Kind == SymbolKind.Local) {
				ShowLocalDeclaration(symbol as ILocalSymbol, info);
			}
			else if (symbol.Kind == SymbolKind.Parameter) {
				ShowParameterDeclaration(symbol as IParameterSymbol, info);
			}
			else {
				ShowSymbolDeclaration(symbol, info);
			}
			if (hideTypeKind == false) {
				info.Append(symbol.GetSymbolKindName(), symbol.Kind == SymbolKind.NamedType ? Keyword : null).Append(" ");
			}
			return info;
		}

		public void ShowTypeArgumentInfo(ITypeParameterSymbol typeParameter, ITypeSymbol typeArgument, TextBlock text) {
			text.AddSymbol(typeParameter, false, TypeParameter).Append(" is ")
				.AddSymbol(typeArgument, true, this);
			if (typeParameter.HasConstraint()) {
				text.Append(" (");
				ShowTypeConstraints(typeParameter, text);
				text.Append(")");
			}
		}

		public void ShowTypeConstraints(ITypeParameterSymbol typeParameter, TextBlock text) {
			bool hasConstraint = false;
			if (typeParameter.HasReferenceTypeConstraint) {
				text.Append("class", Keyword);
				hasConstraint = true;
			}
			else if (typeParameter.HasValueTypeConstraint) {
				text.Append("struct", Keyword);
				hasConstraint = true;
			}
			if (typeParameter.HasUnmanagedTypeConstraint) {
				AppendSeparatorIfHasConstraint(text, hasConstraint).Append("unmanaged", Keyword);
				hasConstraint = true;
			}
			if (typeParameter.HasConstructorConstraint) {
				AppendSeparatorIfHasConstraint(text, hasConstraint).Append("new", Keyword).Append("()", PlainText);
				hasConstraint = true;
			}
			foreach (var constraint in typeParameter.ConstraintTypes) {
				AppendSeparatorIfHasConstraint(text, hasConstraint).AddSymbol(constraint, false, this);
				hasConstraint = true;
			}
		}

		TextBlock AppendSeparatorIfHasConstraint(TextBlock text, bool c) {
			return c ? text.Append(", ".Render(PlainText)) : text;
		}

		static string GetRefType(ISymbol symbol) {
			if (symbol is IMethodSymbol m) {
				if (m.ReturnsByRefReadonly) {
					return "ref readonly ";
				}
				else if (m.ReturnsByRef) {
					return "ref ";
				}
			}
			else if (symbol is IPropertySymbol p) {
				if (p.ReturnsByRefReadonly) {
					return "ref readonly ";
				}
				else if (p.ReturnsByRef) {
					return "ref ";
				}
			}
			return null;
		}

		internal void Format(InlineCollection text, ISymbol symbol, string alias, bool bold) {
			switch (symbol.Kind) {
				case SymbolKind.ArrayType:
					FormatArrayType(text, (IArrayTypeSymbol)symbol, alias, bold);
					return;
				case SymbolKind.Event: FormatEventName(text, (IEventSymbol)symbol, alias, bold); return;
				case SymbolKind.Field:
					text.Add(symbol.Render(alias, bold, ((IFieldSymbol)symbol).IsConst ? Const : Field));
					return;
				case SymbolKind.Method: FormatMethodName(text, symbol, alias, bold); return;
				case SymbolKind.NamedType: FormatTypeName(text, symbol, alias, bold); return;
				case SymbolKind.Namespace: text.Add(symbol.Render(alias, bold, Namespace)); return;
				case SymbolKind.Parameter: text.Add(symbol.Render(null, bold, Parameter)); return;
				case SymbolKind.Property: FormatPropertyName(text, (IPropertySymbol)symbol, alias, bold); return;
				case SymbolKind.Local:
				case SymbolKind.RangeVariable:
					text.Add(symbol.Render(null, bold, Local)); return;
				case SymbolKind.TypeParameter:
					FormatTypeParameter(text, (ITypeParameterSymbol)symbol, alias, bold);
					return;
				case SymbolKind.PointerType:
					Format(text, ((IPointerTypeSymbol)symbol).PointedAtType, alias, bold);
					if (alias == null) {
						text.Add("*".Render(PlainText));
					}
					return;
				case SymbolKind.ErrorType:
					text.Add(((symbol as INamedTypeSymbol).GetTypeName() ?? "?").Render(PlainText));
					return;
				case CodeAnalysisHelper.FunctionPointerType:
					text.Add((symbol as ITypeSymbol).GetTypeName());
					return;
				case SymbolKind.Label: text.Add(symbol.Render(null, bold, null)); return;
				case SymbolKind.Discard: text.Add("_".Render(Keyword)); return;
				default: text.Add(symbol.Name); return;
			}
		}

		void FormatArrayType(InlineCollection text, IArrayTypeSymbol a, string alias, bool bold) {
			Format(text, a.ElementType, alias, bold);
			if (alias == null) {
				text.Add((a.Rank == 1 ? "[]" : $"[{new string(',', a.Rank - 1)}]").Render(PlainText));
			}
		}

		void FormatEventName(InlineCollection text, IEventSymbol e, string alias, bool bold) {
			text.Add(e.Render(alias ?? e.ExplicitInterfaceImplementations.FirstOrDefault()?.Name, bold, Event));
		}

		void FormatTypeParameter(InlineCollection text, ITypeParameterSymbol t, string alias, bool bold) {
			if (alias != null && t.Variance != VarianceKind.None) {
				text.Add((t.Variance == VarianceKind.Out ? "out " : "in ").Render(Keyword));
			}
			text.Add(t.Render(null, bold, TypeParameter));
		}

		void FormatMethodName(InlineCollection text, ISymbol symbol, string alias, bool bold) {
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
					inline = method.Render(method.ExplicitInterfaceImplementations[0].Name, bold, Method);
					break;
				default:
					inline = symbol.Render(alias, bold, Method);
					break;
			}
			text.Add(inline);
			if (method.IsGenericMethod) {
				AddTypeArguments(text, method.TypeArguments);
			}
		}

		void FormatPropertyName(InlineCollection text, IPropertySymbol p, string alias, bool bold) {
			text.Add(p.Render(alias ?? p.GetOriginalName(), bold, Property));
		}

		void FormatTypeName(InlineCollection text, ISymbol symbol, string alias, bool bold) {
			var type = (INamedTypeSymbol)symbol;
			var specialType = type.GetSpecialTypeAlias();
			if (specialType != null) {
				text.Add((alias ?? specialType).Render(Keyword));
				if (type.GetNullableAnnotation() == 2) {
					text.Add("?".Render(PlainText));
				}
				return;
			}
			switch (type.TypeKind) {
				case TypeKind.Class:
					text.Add(symbol.Render(alias ?? (type.IsAnonymousType ? "{anonymous}" : null), bold, Class)); break;
				case TypeKind.Delegate:
					text.Add(symbol.Render(alias, bold, Delegate)); break;
				case TypeKind.Dynamic:
					text.Add(symbol.Render(alias ?? symbol.Name, bold, Keyword)); return;
				case TypeKind.Enum:
					text.Add(symbol.Render(alias, bold, Enum)); return;
				case TypeKind.Interface:
					text.Add(symbol.Render(alias, bold, Interface)); break;
				case TypeKind.Struct:
					ITypeSymbol nullable;
					if (type.IsTupleType) {
						text.Add("(".Render(PlainText));
						for (int i = 0; i < type.TupleElements.Length; i++) {
							if (i > 0) {
								text.Add(", ".Render(PlainText));
							}
							Format(text, type.TupleElements[i].Type, null, false);
							text.Add(" ");
							text.Add(type.TupleElements[i].Render(null, Field));
						}
						text.Add(")".Render(PlainText));
					}
					else if ((nullable = type.GetNullableValueType()) != null) {
						Format(text, nullable, null, false);
						text.Add("?".Render(PlainText));
						return;
					}
					else {
						text.Add(symbol.Render(alias, bold, Struct));
					}
					break;
				case TypeKind.TypeParameter:
					text.Add(symbol.Render(alias ?? symbol.Name, bold, TypeParameter)); return;
				default:
					text.Add(symbol.MetadataName.Render(bold, false, Class)); return;
			}
			if (type.GetNullableAnnotation() == 2) {
				text.Add("?".Render(PlainText));
			}
			if (type.IsGenericType && type.IsTupleType == false) {
				AddTypeArguments(text, type.TypeArguments);
			}
		}

		Brush GetBrushForMethod(IMethodSymbol m) {
			switch (m.ContainingType?.TypeKind) {
				case TypeKind.Class: return Class;
				case TypeKind.Struct: return Struct;
			}
			return Method;
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
				case SymbolKind.Namespace: return Namespace;
				case SymbolKind.Parameter: return Parameter;
				case SymbolKind.Property: return Property;
				case SymbolKind.Local: return Local;
				case SymbolKind.TypeParameter: return TypeParameter;
				case SymbolKind.PointerType: return GetBrush(((IPointerTypeSymbol)symbol).PointedAtType);
				default: return null;
			}
		}

		internal Brush GetBrush(SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.PropertyDeclaration:
				case SyntaxKind.IndexerDeclaration: return Property;
				case SyntaxKind.FieldDeclaration: return ((BaseFieldDeclarationSyntax)node).Modifiers.Any(SyntaxKind.ConstKeyword) ? Const : Field;
				case SyntaxKind.ConstructorDeclaration:
					return GetBrush(node.Parent);
				case SyntaxKind.MethodDeclaration:
				case SyntaxKind.LocalFunctionStatement:
					return Method;
				case SyntaxKind.ClassDeclaration:
				case SyntaxKind.DestructorDeclaration:
				case CodeAnalysisHelper.RecordDeclaration:
					return Class;
				case SyntaxKind.StructDeclaration:
				case CodeAnalysisHelper.RecordStructDeclaration:
					return Struct;
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

		internal TextBlock Format(TextBlock block, ImmutableArray<SymbolDisplayPart> parts, int argIndex) {
			const SymbolDisplayPartKind ExtensionName = (SymbolDisplayPartKind)29;

			foreach (var part in parts) {
				switch (part.Kind) {
					case SymbolDisplayPartKind.AliasName:
						//todo resolve alias type
						goto default;
					case SymbolDisplayPartKind.ClassName:
						if (part.Symbol.Kind == SymbolKind.Method) {
							block.AddSymbol(part.Symbol, true, Method);
						}
						else if (((INamedTypeSymbol)part.Symbol).IsAnonymousType) {
							block.Append("?", Class);
						}
						else {
							block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, Class);
						}
						break;
					case SymbolDisplayPartKind.EnumName:
						block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, Enum);
						break;
					case SymbolDisplayPartKind.InterfaceName:
						block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, Interface);
						break;
					case SymbolDisplayPartKind.MethodName:
						block.AddSymbol(part.Symbol, argIndex != Int32.MinValue, Method);
						break;
					case SymbolDisplayPartKind.ParameterName:
						var p = part.Symbol as IParameterSymbol;
						block.AddSymbol(p, p.Ordinal == argIndex || p.IsParams && argIndex > p.Ordinal, Parameter);
						break;
					case SymbolDisplayPartKind.StructName:
						if (part.Symbol.Kind == SymbolKind.Method) {
							block.AddSymbol(part.Symbol, true, Method);
						}
						else {
							block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, Struct);
						}
						break;
					case SymbolDisplayPartKind.DelegateName:
						block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, Delegate);
						break;
					case SymbolDisplayPartKind.StringLiteral:
						block.Append(part.ToString(), false, false, Text);
						break;
					case SymbolDisplayPartKind.Keyword:
						block.Append(part.ToString(), false, false, Keyword);
						break;
					case SymbolDisplayPartKind.NamespaceName:
						block.AddSymbol(part.Symbol, false, Namespace);
						break;
					case SymbolDisplayPartKind.TypeParameterName:
						block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, TypeParameter);
						break;
					case SymbolDisplayPartKind.FieldName:
						block.AddSymbol(part.Symbol, argIndex == Int32.MinValue, Field);
						break;
					case SymbolDisplayPartKind.PropertyName:
						block.Append(part.Symbol.Name, Property);
						break;
					case SymbolDisplayPartKind.EventName:
						block.Append(part.Symbol.Name, Event);
						break;
					case ExtensionName:
						block.AddSymbol(part.Symbol, true, Method);
						break;
					default:
						block.Append(part.ToString(), PlainText);
						break;
				}
			}
			return block;
		}

		internal void Format(InlineCollection block, AttributeData item, int attributeType) {
			var a = item.AttributeClass.Name;
			block.Add("[".Render(PlainText));
			if (attributeType != 0) {
				block.Add((attributeType == 1 ? "return"
					: attributeType == 2 ? "field"
					: attributeType == 3 ? "assembly"
					: "?").Render(Keyword));
				block.Add(": ".Render(PlainText));
			}
			block.Add(WpfHelper.Render(item.AttributeConstructor ?? (ISymbol)item.AttributeClass, a.EndsWith("Attribute", StringComparison.Ordinal) ? a.Substring(0, a.Length - 9) : a, Class));
			if (item.ConstructorArguments.Length == 0 && item.NamedArguments.Length == 0) {
				var node = item.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
				if (node?.ArgumentList?.Arguments.Count > 0) {
					block.Add(node.ArgumentList.ToString().Render(ThemeHelper.SystemGrayTextBrush));
				}
				block.Add("]".Render(PlainText));
				return;
			}
			block.Add("(".Render(PlainText));
			int i = 0;
			foreach (var arg in item.ConstructorArguments) {
				if (++i > 1) {
					block.Add(", ".Render(PlainText));
				}
				Format(block, arg);
			}
			foreach (var arg in item.NamedArguments) {
				if (++i > 1) {
					block.Add(", ".Render(PlainText));
				}
				var attrMember = item.AttributeClass.GetMembers(arg.Key).FirstOrDefault(m => m.Kind == SymbolKind.Field || m.Kind == SymbolKind.Property);
				if (attrMember != null) {
					block.Add(arg.Key.Render(attrMember.Kind == SymbolKind.Property ? Property : Field));
				}
				else {
					block.Add(arg.Key.Render(false, true, null));
				}
				block.Add("=".Render(PlainText));
				Format(block, arg.Value);
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

		void Format(InlineCollection block, TypedConstant constant) {
			if (constant.IsNull) {
				block.Add("null".Render(Keyword));
				return;
			}
			switch (constant.Kind) {
				case TypedConstantKind.Primitive:
					if (constant.Value is bool b) {
						block.Add(WpfHelper.Render(b ? "true" : "false", Keyword));
					}
					else if (constant.Value is string) {
						block.Add(constant.ToCSharpString().Render(Text));
					}
					else {
						block.Add(constant.ToCSharpString().Render(Number));
					}
					break;
				case TypedConstantKind.Enum:
					var en = constant.ToCSharpString();
					int d;
					if (en.IndexOf('|') != -1) {
						var flags = (constant.Type as INamedTypeSymbol).GetFlaggedEnumFields(constant.Value).ToArray();
						for (int i = 0; i < flags.Length; i++) {
							if (i > 0) {
								block.Add(" | ".Render(PlainText));
							}
							block.Add(constant.Type.Render(null, Enum));
							block.Add(".".Render(PlainText));
							block.Add(flags[i].Render(null, EnumField));
						}
					}
					else if ((d = en.LastIndexOf('.')) != -1)  {
						block.Add(constant.Type.Render(null, Enum));
						block.Add(".".Render(PlainText));
						block.Add(en.Substring(d + 1).Render(EnumField));
					}
					else {
						block.Add(en.Render(Enum));
					}
					break;
				case TypedConstantKind.Type:
					block.Add("typeof".Render(Keyword));
					block.Add("(".Render(PlainText));
					Format(block, constant.Value as ISymbol, null, false);
					block.Add(")".Render(PlainText));
					break;
				case TypedConstantKind.Array:
					block.Add("new".Render(Keyword));
					block.Add("[] { ".Render(PlainText));
					bool c = false;
					foreach (var item in constant.Values) {
						if (c) {
							block.Add(", ".Render(PlainText));
						}
						else {
							c = true;
						}
						Format(block, item);
					}
					block.Add(" }".Render(PlainText));
					break;
				default:
					block.Add(constant.ToCSharpString());
					break;
			}
		}

		void ShowFieldDeclaration(IFieldSymbol field, TextBlock info) {
			if (field.IsConst) {
				info.Append("const ", Keyword);
			}
			else {
				if (field.IsStatic) {
					info.Append("static ", Keyword);
				}
				if (field.IsReadOnly) {
					info.Append("readonly ", Keyword);
				}
				else if (field.IsVolatile) {
					info.Append("volatile ", Keyword);
				}
			}
		}

		void ShowLocalDeclaration(ILocalSymbol local, TextBlock info) {
			if (local.IsConst) {
				info.Append("const ", Keyword);
			}
			else {
				if (local.IsStatic) {
					info.Append("static ", Keyword);
				}
				if (local.IsRef) {
					info.Append(local.RefKind == RefKind.RefReadOnly ? "ref readonly " : "ref ", Keyword);
				}
				if (local.IsFixed) {
					info.Append("fixed ", Keyword);
				}
			}
		}

		static void ShowParameterDeclaration(IParameterSymbol parameter, TextBlock info) {
			switch (parameter.RefKind) {
				case RefKind.Ref: info.Append("ref "); break;
				case RefKind.Out: info.Append("out "); break;
				case RefKind.In: info.Append("in "); break;
			}
		}

		void ShowSymbolDeclaration(ISymbol symbol, TextBlock info) {
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
					var t = o.ContainingType;
					if (t?.IsCommonBaseType() == false) {
						info.AddSymbol(t, null, this).Append(".").AddSymbol(o, null, this).Append(" ");
					}
				}
			}
			else if (symbol.IsSealed) {
				switch (symbol.Kind) {
					case SymbolKind.NamedType:
						switch (((INamedTypeSymbol)symbol).TypeKind) {
							case TypeKind.Class:
								info.Append("sealed ", Keyword); break;
							case TypeKind.Struct:
								if (((INamedTypeSymbol)symbol).IsReadOnly()) {
									info.Append("readonly ", Keyword);
								}
								if (((INamedTypeSymbol)symbol).IsRefLike()) {
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
				}
			}
			else if (symbol.Kind == SymbolKind.Property && symbol is IPropertySymbol p) {
				if (p.IsRequired()) {
					info.Append("required ", Keyword);
				}
			}
		}

		static Dictionary<string, Action<string, SymbolFormatter>> CreatePropertySetter() {
			var r = new Dictionary<string, Action<string, SymbolFormatter>>(19, StringComparer.OrdinalIgnoreCase);
			foreach (var item in typeof(SymbolFormatter).GetProperties()) {
				var ctn = item.GetCustomAttribute<ClassificationTypeAttribute>().ClassificationTypeNames;
				var setFormatBrush = ReflectionHelper.CreateSetPropertyMethod<SymbolFormatter, Brush>(item.Name);
				r.Add(ctn, (ct, f) => {
					var brush = (ct == Constants.CodePlainText ? __CodeClassificationFormatMap.DefaultTextProperties :  __CodeClassificationFormatMap.GetRunProperties(ct)).ForegroundBrush;
					if (f._BrushConfigurator != null) {
						brush = f._BrushConfigurator(brush);
					}
					setFormatBrush(f, brush);
				});
			}
			return r;
		}

		void FormatMap_FormatMappingChanged(object sender, FormatItemsEventArgs e) {
			foreach (var item in e.ChangedItems) {
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
}
