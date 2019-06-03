using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AppHelpers;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.QuickInfo
{
	sealed class CSharpQuickInfo : IQuickInfoSource
	{
		internal const string Name = nameof(CSharpQuickInfo);

		static readonly SymbolFormatter _SymbolFormatter = SymbolFormatter.Instance;

		readonly bool _IsVsProject;
		bool _IsDisposed;
		SemanticModel _SemanticModel;
		readonly ITextBuffer _TextBuffer;

		public CSharpQuickInfo(ITextBuffer subjectBuffer) {
			ThreadHelper.ThrowIfNotOnUIThread();
			_TextBuffer = subjectBuffer;
			_TextBuffer.Changing += TextBuffer_Changing;
			var extenders = CodistPackage.DTE.ActiveDocument?.ProjectItem?.ContainingProject?.ExtenderNames as string[];
			if (extenders != null) {
				_IsVsProject = Array.IndexOf(extenders, "VsixProjectExtender") != -1;
			}
		}

		public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> qiContent, out ITrackingSpan applicableToSpan) {
			if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control) {
				applicableToSpan = null;
				return;
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.HideOriginalQuickInfo)) {
				qiContent.Clear();
			}
			var qiWrapper = Config.Instance.QuickInfoOptions.HasAnyFlag(QuickInfoOptions.QuickInfoOverride) || Config.Instance.QuickInfoMaxWidth > 0 || Config.Instance.QuickInfoMaxHeight > 0
				? QuickInfoOverrider.CreateOverrider(qiContent)
				: null;
			// Map the trigger point down to our buffer.
			var currentSnapshot = _TextBuffer.CurrentSnapshot;
			var subjectTriggerPoint = session.GetTriggerPoint(currentSnapshot).GetValueOrDefault();
			if (subjectTriggerPoint.Snapshot == null) {
				goto EXIT;
			}

			SemanticModel semanticModel;
			var container = currentSnapshot.AsText().Container;
			DocumentId docId;
			if (Workspace.TryGetWorkspace(container, out var workspace) == false
				|| (docId = workspace.GetDocumentIdInCurrentContext(container)) == null
				|| workspace.CurrentSolution.GetDocument(docId).TryGetSemanticModel(out semanticModel) == false) {
				goto EXIT;
			}

			_SemanticModel = semanticModel;
			var unitCompilation = semanticModel.SyntaxTree.GetCompilationUnitRoot();

			//look for occurrences of our QuickInfo words in the span
			var token = unitCompilation.FindToken(subjectTriggerPoint, true);
			var skipTriggerPointCheck = false;
			switch (token.Kind()) {
				case SyntaxKind.WhitespaceTrivia:
				case SyntaxKind.SingleLineCommentTrivia:
				case SyntaxKind.MultiLineCommentTrivia:
					goto EXIT;
				case SyntaxKind.OpenBraceToken:
				case SyntaxKind.CloseBraceToken:
				case SyntaxKind.SwitchKeyword: // switch info
				case SyntaxKind.ThisKeyword: // convert to type below
				case SyntaxKind.BaseKeyword:
					break;
				case SyntaxKind.NullKeyword:
				case SyntaxKind.TrueKeyword:
				case SyntaxKind.FalseKeyword:
				case SyntaxKind.NewKeyword:
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter)) {
						break;
					}
					else {
						goto EXIT;
					}
				case SyntaxKind.AsKeyword:
					var asType = (unitCompilation.FindNode(token.Span) as BinaryExpressionSyntax)?.GetLastIdentifier();
					if (asType != null) {
						token = asType.Identifier;
						skipTriggerPointCheck = true;
					}
					break;
				case SyntaxKind.ReturnKeyword:
					var statement = unitCompilation.FindNode(token.Span);
					var retStatement = statement as ReturnStatementSyntax;
					if (statement != null && retStatement != null) {
						var tb = ShowReturnInfo(statement, retStatement, token);
						if (tb != null) {
							qiContent.Add(tb);
							applicableToSpan = currentSnapshot.CreateTrackingSpan(token.SpanStart, token.Span.Length, SpanTrackingMode.EdgeInclusive);
							return;
						}
					}
					goto EXIT;
				case SyntaxKind.OpenParenToken:
				case SyntaxKind.CloseParenToken:
				case SyntaxKind.DotToken:
				case SyntaxKind.CommaToken:
				case SyntaxKind.ColonToken:
				case SyntaxKind.SemicolonToken:
					token = token.GetPreviousToken();
					skipTriggerPointCheck = true;
					break;
				default:
					if (token.Span.Contains(subjectTriggerPoint, true) == false
						|| token.IsReservedKeyword()) {
						goto EXIT;
					}
					break;
			}
			var node = unitCompilation.FindNode(token.Span, true, true);
			if (node == null ||
				skipTriggerPointCheck == false && node.Span.Contains(subjectTriggerPoint.Position, true) == false) {
				goto EXIT;
			}
			//if (node.Parent.IsKind(SyntaxKind.QualifiedName)) {
			//	node = node.Parent;
			//}
			LocateNodeInParameterList(ref node, ref token);
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter)) {
				ShowParameterInfo(qiContent, node);
			}
			ISymbol symbol;
			bool usedCandidate = false;
			if (node.IsKind(SyntaxKind.BaseExpression)) {
				symbol = semanticModel.GetTypeInfo(node).ConvertedType;
			}
			else if (node.IsKind(SyntaxKind.ThisExpression)) {
				symbol = semanticModel.GetTypeInfo(node).Type;
			}
			else if (token.IsKind(SyntaxKind.CloseBraceToken)) {
				symbol = null;
			}
			else {
				var symbolInfo = semanticModel.GetSymbolInfo(node);
				if (symbolInfo.CandidateReason != CandidateReason.None) {
					ShowCandidateInfo(qiContent, symbolInfo);
					symbol = symbolInfo.CandidateSymbols.FirstOrDefault();
					usedCandidate = true;
				}
				else {
					symbol = symbolInfo.Symbol ?? semanticModel.GetSymbolExt(node);
				}
			}
			if (symbol == null) {
				ShowMiscInfo(qiContent, currentSnapshot, node);
				goto RETURN;
			}

			if (node is PredefinedTypeSyntax/* void */) {
				goto EXIT;
			}
			if (Config.Instance.QuickInfoOptions.HasAnyFlag(QuickInfoOptions.QuickInfoOverride)
				&& usedCandidate == false) {
				var ctor = node.Parent as ObjectCreationExpressionSyntax;
				OverrideDocumentation(node, qiWrapper,
					ctor != null && ctor.Type == node ? semanticModel.GetSymbolInfo(ctor).Symbol : symbol);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Attributes)) {
				ShowAttributesInfo(qiContent, symbol);
			}
			ShowSymbolInfo(qiContent, node, symbol);
			RETURN:
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ClickAndGo)) {
				var ctor = node.Parent as ObjectCreationExpressionSyntax;
				if (ctor != null && ctor.Type == node) {
					symbol = semanticModel.GetSymbolOrFirstCandidate(ctor);
				}
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Diagnostics)) {
					qiWrapper.SetDiagnostics(semanticModel.GetDiagnostics(token.Span));
				}
				qiWrapper.ApplyClickAndGo(symbol);
			}
			qiWrapper.LimitQuickInfoItemSize(qiContent);
			applicableToSpan = qiContent.Count > 0 && session.TextView.TextSnapshot == currentSnapshot
				? currentSnapshot.CreateTrackingSpan(token.SpanStart, token.Span.Length, SpanTrackingMode.EdgeInclusive)
				: null;
			return;
			EXIT:
			qiWrapper?.LimitQuickInfoItemSize(qiContent);
			applicableToSpan = null;
		}

		static void LocateNodeInParameterList(ref SyntaxNode node, ref SyntaxToken token) {
			if (node.IsKind(SyntaxKind.Argument)) {
				node = (node as ArgumentSyntax).Expression;
			}
			else if (node.IsKind(SyntaxKind.ArgumentList)) {
				var al = node as ArgumentListSyntax;
				if (al.OpenParenToken == token) {
					node = al.Arguments.FirstOrDefault() ?? node;
				}
				else if (al.CloseParenToken == token) {
					node = al.Arguments.LastOrDefault() ?? node;
				}
				else {
					foreach (var item in al.Arguments) {
						if (item.FullSpan.Contains(token.SpanStart, true)) {
							node = item;
							break;
						}
					}
				}
			}
		}

		void OverrideDocumentation(SyntaxNode node, IQuickInfoOverrider qiWrapper, ISymbol symbol) {
			if (symbol == null
				|| Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation) == false) {
				return;
			}
			symbol = symbol.GetAliasTarget();
			var doc = new XmlDoc(symbol, _SemanticModel.Compilation);
			var tip = new ThemedTipDocument();
			var docRenderer = new XmlDocRenderer(_SemanticModel.Compilation, SymbolFormatter.Instance, symbol);
			var summary = doc.GetDescription(symbol);
			if (summary == null) {
				var inheritDoc = doc.ExplicitInheritDoc;
				if ((inheritDoc == null || (summary = inheritDoc.GetDescription(symbol)) == null)
					&& Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.DocumentationFromBaseType)) {
					foreach (var item in doc.InheritedXmlDocs) {
						if ((summary = item.GetDescription(symbol)) != null) {
							inheritDoc = item;
							break;
						}
					}
				}
				if (inheritDoc != null && summary != null) {
					tip.Append(new ThemedTipParagraph(new ThemedTipText()
							.Append("Documentation from ")
							.AddSymbol(inheritDoc.Symbol.ContainingType, _SymbolFormatter)
							.Append(".")
							.AddSymbol(inheritDoc.Symbol, _SymbolFormatter)
							.Append(":"))
					);
				}
			}
			if (summary != null) {
				if (summary.Name.LocalName == XmlDocRenderer.XmlDocNodeName && Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.TextOnlyDoc) == false) {
					return;
				}
				docRenderer.ParagraphCount = 0;
				docRenderer.Render(summary, tip);
				tip.Tag = docRenderer.ParagraphCount;
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.TypeParameters) && (symbol.Kind == SymbolKind.Method || symbol.Kind == SymbolKind.NamedType)) {
				var typeParams = symbol.GetTypeParameters();
				if (typeParams.IsDefaultOrEmpty == false) {
					var para = new ThemedTipParagraph(KnownImageIds.TypeDefinition);
					foreach (var param in typeParams) {
						var p = doc.GetTypeParameter(param.Name);
						if (p == null) {
							continue;
						}
						if (para.Content.Inlines.FirstInline != null) {
							para.Content.AppendLine();
						}
						para.Content
							.Append(param.Name, _SymbolFormatter.TypeParameter)
							.Append(": ")
							.AddXmlDoc(p, docRenderer);
					}
					if (para.Content.Inlines.FirstInline != null) {
						tip.Append(para);
					}
				}
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ReturnsDoc)
				&& (symbol.Kind == SymbolKind.Method
				|| symbol.Kind == SymbolKind.NamedType && ((INamedTypeSymbol)symbol).TypeKind == TypeKind.Delegate)) {
				var returns = doc.Returns ?? doc.ExplicitInheritDoc?.Returns ?? doc.InheritedXmlDocs.FirstOrDefault(i => i.Returns != null)?.Returns;
				if (returns != null && returns.FirstNode != null) {
					tip.Append(new ThemedTipParagraph(KnownImageIds.Return, new ThemedTipText()
						.Append("Returns", true)
						.Append(returns == doc.Returns ? ": " : " (inherited): ")
						.AddXmlDoc(returns, docRenderer))
						);
				}
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.RemarksDoc)
				&& symbol.Kind != SymbolKind.Parameter
				&& symbol.Kind != SymbolKind.TypeParameter) {
				var remarks = doc.Remarks ?? doc.ExplicitInheritDoc?.Remarks ?? doc.InheritedXmlDocs.FirstOrDefault(i => i.Remarks != null)?.Remarks;
				if (remarks != null && remarks.FirstNode != null) {
					tip.Append(new ThemedTipParagraph(KnownImageIds.CommentGroup, new ThemedTipText()
						.Append("Remarks", true)
						.Append(remarks == doc.Remarks ? ": " : " (inherited): ")
						))
						.Append(new ThemedTipParagraph(new ThemedTipText().AddXmlDoc(remarks, docRenderer)));
				}
			}
			if (node is LambdaExpressionSyntax
				|| (symbol as IMethodSymbol)?.MethodKind == MethodKind.LocalFunction) {
				var ss = node.AncestorsAndSelf().FirstOrDefault(i => i is StatementSyntax || i is ExpressionSyntax && i.Kind() != SyntaxKind.IdentifierName);
				if (ss != null) {
					var df = _SemanticModel.AnalyzeDataFlow(ss);
					var captured = ss is StatementSyntax || ss.IsKind(SyntaxKind.InvocationExpression) || ss.IsKind(SyntaxKind.ParenthesizedLambdaExpression) || ss.IsKind(SyntaxKind.SimpleLambdaExpression) ? df.DataFlowsIn : df.ReadInside;
					if (captured.Length > 0) {
						var p = new ThemedTipParagraph(KnownImageIds.ExternalVariableValue, new ThemedTipText().Append("Captured variables", true));
						int i = 0;
						foreach (var item in captured) {
							p.Content.Append(++i == 1 ? ": " : ", ").AddSymbol(item, _SymbolFormatter);
						}
						tip.Append(p);
					}
				}
			}
			if (tip.Children.Count > 0) {
				qiWrapper.OverrideDocumentation(tip);
			}

			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ExceptionDoc)
				&& (symbol.Kind == SymbolKind.Method || symbol.Kind == SymbolKind.Property || symbol.Kind == SymbolKind.Event)
				&& doc.Exceptions != null) {
				var p = new ThemedTipParagraph(KnownImageIds.StatusInvalidOutline, new ThemedTipText("Exception:", true));
				foreach (var ex in doc.Exceptions) {
					var et = ex.Attribute("cref");
					if (et != null) {
						docRenderer.RenderXmlDocSymbol(et.Value, p.Content.AppendLine().Inlines, SymbolKind.NamedType);
						docRenderer.Render(ex, p.Content.Append(": ").Inlines);
					}
				}
				if (p.Content.Inlines.Count > 1) {
					qiWrapper.OverrideException(new ThemedTipDocument().Append(p));
				}
			}
		}

		void IDisposable.Dispose() {
			if (!_IsDisposed) {
				_TextBuffer.Changing -= TextBuffer_Changing;
				GC.SuppressFinalize(this);
				_IsDisposed = true;
			}
		}

		static void ShowCandidateInfo(IList<object> qiContent, SymbolInfo symbolInfo) {
			var info = new ThemedTipDocument().AppendTitle(KnownImageIds.CodeInformation, "Maybe...");
			foreach (var item in symbolInfo.CandidateSymbols) {
				info.Append(new ThemedTipParagraph(item.GetImageId(), ToUIText(item)));
			}
			qiContent.Add(info);
		}

		void TextBuffer_Changing(object sender, TextContentChangingEventArgs e) {
			_SemanticModel = null;
		}

		void ShowSymbolInfo(IList<object> qiContent, SyntaxNode node, ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Event:
					ShowEventInfo(qiContent, symbol as IEventSymbol);
					break;
				case SymbolKind.Field:
					ShowFieldInfo(qiContent, symbol as IFieldSymbol);
					break;
				case SymbolKind.Local:
					var loc = symbol as ILocalSymbol;
					if (loc.HasConstantValue) {
						ShowConstInfo(qiContent, symbol, loc.ConstantValue);
					}
					break;
				case SymbolKind.Method:
					var m = symbol as IMethodSymbol;
					if (m.MethodKind == MethodKind.AnonymousFunction) {
						return;
					}
					ShowMethodInfo(qiContent, node, m);
					if (node.Parent.IsKind(SyntaxKind.Attribute)
						|| node.Parent.Parent.IsKind(SyntaxKind.Attribute) // qualified attribute annotation
						) {
						if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Attributes)) {
							ShowAttributesInfo(qiContent, symbol.ContainingType);
						}
						ShowTypeInfo(qiContent, node.Parent, symbol.ContainingType);
					}
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color) && m.ContainingType.Name == "Color") {
						var preview = ColorQuickInfo.PreviewColorMethodInvocation(_SemanticModel, node, symbol as IMethodSymbol);
						if (preview != null) {
							qiContent.Add(preview);
						}
					}
					break;
				case SymbolKind.NamedType:
					ShowTypeInfo(qiContent, node, symbol as INamedTypeSymbol);
					break;
				case SymbolKind.Property:
					ShowPropertyInfo(qiContent, symbol as IPropertySymbol);
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color)) {
						var preview = ColorQuickInfo.PreviewColorProperty(symbol as IPropertySymbol, _IsVsProject);
						if (preview != null) {
							qiContent.Add(preview);
						}
					}
					break;
				case SymbolKind.Namespace:
					ShowNamespaceInfo(qiContent, symbol as INamespaceSymbol);
					break;
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.SymbolLocation)) {
				string asmName = symbol.GetAssemblyModuleName();
				if (asmName != null) {
					qiContent.Add(new ThemedTipText("Assembly: ", true).Append(asmName));
				}
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
				&& (node.Parent.IsKind(SyntaxKind.Argument) == false || Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter) == false) /*the signature has already been displayed there*/) {
				var st = symbol.GetReturnType();
				if (st != null && st.TypeKind == TypeKind.Delegate) {
					var invoke = (st as INamedTypeSymbol).DelegateInvokeMethod;
					qiContent.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(KnownImageIds.Delegate,
						new ThemedTipText("Delegate signature", true).Append(":").AppendLine()
							.AddSymbol(invoke.ReturnType, _SymbolFormatter)
							.Append(" ").AddSymbol(st, _SymbolFormatter)
							.AddParameters(invoke.Parameters, _SymbolFormatter)
						)));
				}
			}

		}

		static void ShowMiscInfo(IList<object> qiContent, ITextSnapshot currentSnapshot, SyntaxNode node) {
			StackPanel infoBox = null;
			var nodeKind = node.Kind();
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues) && (nodeKind == SyntaxKind.NumericLiteralExpression || nodeKind == SyntaxKind.CharacterLiteralExpression)) {
				infoBox = ShowNumericForm(node);
			}
			else if (nodeKind == SyntaxKind.SwitchStatement) {
				var s = (node as SwitchStatementSyntax).Sections.Count;
				if (s > 1) {
					var cases = 0;
					foreach (var section in (node as SwitchStatementSyntax).Sections) {
						cases += section.Labels.Count;
					}
					qiContent.Add(s + " switch sections, " + cases + " cases");
				}
				else {
					var cases = (node as SwitchStatementSyntax).Sections.Count;
					if (cases > 1) {
						qiContent.Add("1 switch section, " + cases + " cases");
					}
				}
			}
			else if (nodeKind == SyntaxKind.StringLiteralExpression) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.String)) {
					infoBox = ShowStringInfo(node.GetFirstToken().ValueText);
				}
			}
			else if (nodeKind == SyntaxKind.Block || node.IsDeclaration()) {
				var lines = currentSnapshot.GetLineSpan(node.Span).Length + 1;
				if (lines > 100) {
					qiContent.Add(new ThemedTipText { Text = lines + " lines", FontWeight = FontWeights.Bold });
				}
				else if (lines > 1) {
					qiContent.Add(lines + " lines");
				}
			}
			if (infoBox != null) {
				qiContent.Add(infoBox);
			}
		}

		ThemedTipText ShowReturnInfo(SyntaxNode statement, ReturnStatementSyntax retStatement, SyntaxToken token) {
			var retSymbol = retStatement.Expression != null
				? _SemanticModel.GetSymbolInfo(retStatement.Expression).Symbol
				: null;
			while ((statement = statement.Parent) != null) {
				if (statement.IsMemberDeclaration() == false
					&& statement.IsKind(SyntaxKind.SimpleLambdaExpression) == false
					&& statement.IsKind(SyntaxKind.ParenthesizedLambdaExpression) == false
					&& statement.IsKind(SyntaxKind.LocalFunctionStatement) == false) {
					continue;
				}
				var name = statement.GetDeclarationSignature();
				if (name == null) {
					continue;
				}
				var symbol = _SemanticModel.GetSymbolInfo(statement).Symbol ?? _SemanticModel.GetDeclaredSymbol(statement);
				var t = new ThemedTipText();
				if (retSymbol != null) {
					var m = retSymbol as IMethodSymbol;
					if (m != null && m.MethodKind == MethodKind.AnonymousFunction) {
						t.Append("Return anonymous function for ");
					}
					else {
						t.Append("Return ")
							.AddSymbol(retSymbol.GetReturnType(), _SymbolFormatter)
							.Append(" for ");
					}
				}
				//else if (retStatement.Expression.Kind() == SyntaxKind.NullLiteralExpression) {
				//	tb.AddText("Return ").AddText("null", _SymbolFormatter.Keyword).AddText(" for ");
				//}
				else {
					t.Append("Return for ");
				}
				if (symbol != null) {
					if (statement is LambdaExpressionSyntax) {
						t.Append("lambda expression");
					}
					t.AddSymbol(symbol, name, _SymbolFormatter);
				}
				else {
					t.Append(name);
				}
				return t;
			}
			return null;
		}

		static void ShowAttributesInfo(IList<object> qiContent, ISymbol symbol) {
			// todo: show inherited attributes
			var p = ListAttributes(null, symbol.GetAttributes(), false);
			if (symbol.Kind == SymbolKind.Method) {
				p = ListAttributes(p, ((IMethodSymbol)symbol).GetReturnTypeAttributes(), true);
			}
			if (p != null) {
				qiContent.Add(new ThemedTipDocument().Append(p));
			}
		}

		static ThemedTipParagraph ListAttributes(ThemedTipParagraph p, ImmutableArray<AttributeData> attrs, bool isMethodReturnAttrs) {
			if (attrs.Length > 0) {
				if (p == null) {
					p = new ThemedTipParagraph(KnownImageIds.FormPostBodyParameterNode, new ThemedTipText().Append("Attribute:", true));
				}
				ShowAttributes(p, attrs, isMethodReturnAttrs);
			}
			return p;
		}

		static void ShowPropertyInfo(IList<object> qiContent, IPropertySymbol property) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
				&& (property.DeclaredAccessibility != Accessibility.Public || property.IsAbstract || property.IsStatic || property.IsOverride || property.IsVirtual)) {
				ShowDeclarationModifier(qiContent, property);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfaceImplementations)) {
				ShowInterfaceImplementation(qiContent, property, property.ExplicitInterfaceImplementations);
			}
		}

		static void ShowEventInfo(IList<object> qiContent, IEventSymbol ev) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)) {
				if (ev.DeclaredAccessibility != Accessibility.Public || ev.IsAbstract || ev.IsStatic || ev.IsOverride || ev.IsVirtual) {
					ShowDeclarationModifier(qiContent, ev);
				}
				var invoke = ev.Type.GetMembers("Invoke").FirstOrDefault() as IMethodSymbol;
				if (invoke != null && invoke.Parameters.Length == 2) {
					qiContent.Add(
						new ThemedTipText("Event argument: ", true)
						.AddSymbolDisplayParts(invoke.Parameters[1].Type.ToDisplayParts(WpfHelper.QuickInfoSymbolDisplayFormat), _SymbolFormatter, -1)
					);
				}
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfaceImplementations)) {
				ShowInterfaceImplementation(qiContent, ev, ev.ExplicitInterfaceImplementations);
			}
		}

		static void ShowFieldInfo(IList<object> qiContent, IFieldSymbol field) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
				&& (field.DeclaredAccessibility != Accessibility.Public || field.IsReadOnly || field.IsVolatile || field.IsStatic)
				&& field.ContainingType.TypeKind != TypeKind.Enum) {
				ShowDeclarationModifier(qiContent, field);
			}
			if (field.HasConstantValue) {
				ShowConstInfo(qiContent, field, field.ConstantValue);
			}
		}

		void ShowMethodInfo(IList<object> qiContent, SyntaxNode node, IMethodSymbol method) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
				&& (method.DeclaredAccessibility != Accessibility.Public || method.IsAbstract || method.IsStatic || method.IsVirtual || method.IsOverride || method.IsExtern || method.IsSealed)
				&& method.ContainingType.TypeKind != TypeKind.Interface) {
				ShowDeclarationModifier(qiContent, method);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.TypeParameters) && method.TypeArguments.Length > 0 && method.TypeParameters[0] != method.TypeArguments[0]) {
				ShowMethodTypeArguments(qiContent, method);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfaceImplementations)) {
				ShowInterfaceImplementation(qiContent, method, method.ExplicitInterfaceImplementations);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.SymbolLocation) && method.IsExtensionMethod) {
				ShowExtensionMethod(qiContent, method);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.MethodOverload)) {
				ShowOverloadsInfo(qiContent, node, method);
			}
		}

		void ShowOverloadsInfo(IList<object> qiContent, SyntaxNode node, IMethodSymbol method) {
			var overloads = node.Kind() == SyntaxKind.MethodDeclaration
				? method.ContainingType.GetMembers(method.Name)
				: _SemanticModel.GetMemberGroup(node);
			if (overloads.Length < 2) {
				return;
			}
			var overloadInfo = new ThemedTipDocument().AppendTitle(KnownImageIds.MethodSet, "Method overload:");
			foreach (var item in overloads) {
				if (item.Equals(method) || item.Kind != SymbolKind.Method) {
					continue;
				}
				overloadInfo.Append(new ThemedTipParagraph(item.GetImageId(), new ThemedTipText()
					.AddSymbolDisplayParts(item.ToDisplayParts(item.ContainingType == method.ContainingType ? WpfHelper.InTypeOverloadDisplayFormat : WpfHelper.QuickInfoSymbolDisplayFormat), _SymbolFormatter, -1))
				);
			}
			if (overloadInfo.Children.Count > 1) {
				qiContent.Add(overloadInfo);
			}
		}

		static void ShowMethodTypeArguments(IList<object> qiContent, IMethodSymbol method) {
			var info = new ThemedTipDocument();
			var l = method.TypeArguments.Length;
			var content = new ThemedTipText("Type argument:", true);
			info.Append(new ThemedTipParagraph(KnownImageIds.Template, content));
			for (int i = 0; i < l; i++) {
				ShowTypeParameterInfo(method.TypeParameters[i], method.TypeArguments[i], content.AppendLine());
			}
			qiContent.Add(info);
		}

		static void ShowNamespaceInfo(IList<object> qiContent, INamespaceSymbol nsSymbol) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NamespaceTypes) == false) {
				return;
			}
			var namespaces = nsSymbol.GetNamespaceMembers().ToImmutableArray().Sort(Comparer<INamespaceSymbol>.Create((x, y) => String.Compare(x.Name, y.Name, StringComparison.Ordinal)));
			if (namespaces.Length > 0) {
				var info = new ThemedTipDocument();
				info.AppendTitle(KnownImageIds.Namespace, "Namespace:");
				foreach (var ns in namespaces) {
					info.Append(new ThemedTipParagraph(KnownImageIds.Namespace, new ThemedTipText().Append(ns.Name, _SymbolFormatter.Namespace)));
				}
				qiContent.Add(info);
			}

			var members = nsSymbol.GetTypeMembers().Sort(Comparer<INamedTypeSymbol>.Create((x, y) => String.Compare(x.Name, y.Name)));
			if (members.Length > 0) {
				var info = new StackPanel();
				info.Add(new ThemedTipText("Type:", true));
				foreach (var type in members) {
					var t = new ThemedTipText().SetGlyph(ThemeHelper.GetImage(type.GetImageId()));
					_SymbolFormatter.ShowSymbolDeclaration(type, t, true, true);
					t.AddSymbol(type, _SymbolFormatter);
					info.Add(t);
				}
				qiContent.Add(info.Scrollable());
			}
		}

		void ShowTypeInfo(IList<object> qiContent, SyntaxNode node, INamedTypeSymbol typeSymbol) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseType)) {
				if (typeSymbol.TypeKind == TypeKind.Enum) {
					ShowEnumInfo(qiContent, typeSymbol, true);
				}
				else {
					ShowBaseType(qiContent, typeSymbol);
				}
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Interfaces)) {
				ShowInterfaces(qiContent, typeSymbol);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
				&& typeSymbol.TypeKind == TypeKind.Class
				&& (typeSymbol.DeclaredAccessibility != Accessibility.Public || typeSymbol.IsAbstract || typeSymbol.IsStatic || typeSymbol.IsSealed)) {
				ShowDeclarationModifier(qiContent, typeSymbol);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.MethodOverload)) {
				node = node.GetObjectCreationNode();
				if (node != null) {
					var method = _SemanticModel.GetSymbolOrFirstCandidate(node) as IMethodSymbol;
					if (method != null) {
						ShowOverloadsInfo(qiContent, node, method);
					}
				}
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfaceMembers)
				&& typeSymbol.TypeKind == TypeKind.Interface) {
				ShowInterfaceMembers(qiContent, typeSymbol);
			}
		}

		static void ShowConstInfo(IList<object> qiContent, ISymbol symbol, object value) {
			var sv = value as string;
			if (sv != null) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.String)) {
					qiContent.Add(ShowStringInfo(sv));
				}
			}
			else if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues)) {
				var s = ShowNumericForms(value, NumericForm.None);
				if (s != null) {
					ShowEnumInfo(qiContent, symbol.ContainingType, false);
					qiContent.Add(s);
				}
			}
		}

		static void ShowInterfaceImplementation<TSymbol>(IList<object> qiContent, TSymbol symbol, IEnumerable<TSymbol> explicitImplementations)
			where TSymbol : class, ISymbol {
			if (symbol.IsStatic || symbol.DeclaredAccessibility != Accessibility.Public && explicitImplementations.Any() == false) {
				return;
			}
			var interfaces = symbol.ContainingType.AllInterfaces;
			if (interfaces.Length == 0) {
				return;
			}
			var explicitIntfs = new List<ITypeSymbol>(3);
			ThemedTipDocument info = null;
			var returnType = symbol.GetReturnType();
			var parameters = symbol.GetParameters();
			foreach (var intf in interfaces) {
				foreach (var member in intf.GetMembers()) {
					if (member.Kind == symbol.Kind
						&& member.DeclaredAccessibility == Accessibility.Public
						&& member.Name == symbol.Name
						&& member.MatchSignature(symbol.Kind, returnType, parameters)) {
						explicitIntfs.Add(intf);
					}
				}
			}
			if (explicitIntfs.Count > 0) {
				info = new ThemedTipDocument().AppendTitle(KnownImageIds.ImplementInterface, "Implements:");
				foreach (var item in explicitIntfs) {
					info.Append(new ThemedTipParagraph(item.GetImageId(), ToUIText(item)));
				}
			}
			if (explicitImplementations != null) {
				explicitIntfs.Clear();
				explicitIntfs.AddRange(explicitImplementations.Select(i => i.ContainingType));
				if (explicitIntfs.Count > 0) {
					if (info == null) {
						info = new ThemedTipDocument();
					}
					info.AppendTitle(KnownImageIds.ImplementInterface, "Explicit implements:");
					foreach (var item in explicitIntfs) {
						info.Append(new ThemedTipParagraph(item.GetImageId(), ToUIText(item)));
					}
				}
			}
			if (info != null) {
				qiContent.Add(info);
			}
		}
		static void ShowInterfaceMembers(IList<object> qiContent, INamedTypeSymbol type) {
			var doc = new ThemedTipDocument();
			doc.AppendTitle(KnownImageIds.ListMembers, "Member:");
			ShowMembers(type, doc, false);
			foreach (var item in type.AllInterfaces) {
				ShowMembers(item, doc, true);
			}
			if (doc.Children.Count > 1) {
				qiContent.Add(doc);
			}
		}

		static void ShowMembers(INamedTypeSymbol type, ThemedTipDocument doc, bool isInherit) {
			var members = new List<ISymbol>(type.GetMembers().Where(m => m.CanBeReferencedByName));
			members.Sort(CodeAnalysisHelper.CompareByAccessibilityKindName);
			foreach (var member in members) {
				var t = new ThemedTipText();
				if (isInherit) {
					t.AddSymbol(type, _SymbolFormatter).Append(".");
				}
				t.AddSymbol(member, _SymbolFormatter);
				if (member.Kind == SymbolKind.Method) {
					t.AddParameters((member as IMethodSymbol).Parameters, _SymbolFormatter);
				}
				doc.Append(new ThemedTipParagraph(member.GetImageId(), t));
			}
		}

		static void ShowExtensionMethod(IList<object> qiContent, IMethodSymbol method) {
			var info = new StackPanel();
			var extType = method.ConstructedFrom.ReceiverType;
			var extTypeParameter = extType as ITypeParameterSymbol;
			if (extTypeParameter != null && (extTypeParameter.HasConstructorConstraint || extTypeParameter.HasReferenceTypeConstraint || extTypeParameter.HasValueTypeConstraint || extTypeParameter.ConstraintTypes.Length > 0)) {
				var ext = new ThemedTipText("Extending: ", true)
					.AddSymbol(extType, true, _SymbolFormatter.Class)
					.Append(" with ")
					.AddSymbolDisplayParts(method.ReceiverType.ToDisplayParts(WpfHelper.QuickInfoSymbolDisplayFormat), _SymbolFormatter, -1);
				info.Add(ext);
			}
			var def = new ThemedTipText("Extended by: ", true)
				.AddSymbolDisplayParts(method.ContainingType.ToDisplayParts(), _SymbolFormatter, -1);
			info.Add(def);
			qiContent.Add(info);
		}

		static void ShowTypeParameterInfo(ITypeParameterSymbol typeParameter, ITypeSymbol typeArgument, TextBlock text) {
			text.Append(typeParameter.Name, _SymbolFormatter.TypeParameter).Append(" is ")
				.AddSymbol(typeArgument, _SymbolFormatter);
			if (typeParameter.HasConstructorConstraint == false && typeParameter.HasReferenceTypeConstraint == false && typeParameter.HasValueTypeConstraint == false && typeParameter.ConstraintTypes.Length == 0) {
				return;
			}
			if (typeParameter.HasReferenceTypeConstraint) {
				text.Append(", ").Append("class", _SymbolFormatter.Keyword);
			}
			if (typeParameter.HasValueTypeConstraint) {
				text.Append(", ").Append("struct", _SymbolFormatter.Keyword);
			}
			if (typeParameter.HasConstructorConstraint) {
				text.Append(", ").Append("new", _SymbolFormatter.Keyword).Append("()");
			}
			if (typeParameter.ConstraintTypes.Length > 0) {
				foreach (var constraint in typeParameter.ConstraintTypes) {
					text.Append(", ").AddSymbol(constraint, _SymbolFormatter);
				}
			}
		}

		static StackPanel ShowNumericForm(SyntaxNode node) {
			return ShowNumericForms(node.GetFirstToken().Value, node.Parent.Kind() == SyntaxKind.UnaryMinusExpression ? NumericForm.Negative : NumericForm.None);
		}

		static StackPanel ShowNumericForms(object value, NumericForm form) {
			if (value is int) {
				var v = (int)value;
				if (form == NumericForm.Negative) {
					v = -v;
				}
				var bytes = new byte[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
				return ShowNumericForms(form == NumericForm.Unsigned ? ((uint)v).ToString() : v.ToString(), bytes);
			}
			else if (value is long) {
				var v = (long)value;
				if (form == NumericForm.Negative) {
					v = -v;
				}
				var bytes = new byte[] { (byte)(v >> 56), (byte)(v >> 48), (byte)(v >> 40), (byte)(v >> 32), (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
				return ShowNumericForms(form == NumericForm.Unsigned ? ((ulong)v).ToString() : v.ToString(), bytes);
			}
			else if (value is byte) {
				return ShowNumericForms(((byte)value).ToString(), new byte[] { (byte)value });
			}
			else if (value is short) {
				var v = (short)value;
				if (form == NumericForm.Negative) {
					v = (short)-v;
				}
				var bytes = new byte[] { (byte)(v >> 8), (byte)v };
				return ShowNumericForms(form == NumericForm.Unsigned ? ((ushort)v).ToString() : v.ToString(), bytes);
			}
			else if (value is char) {
				var v = (char)value;
				var bytes = new byte[] { (byte)(v >> 8), (byte)v };
				return ShowNumericForms(((ushort)v).ToString(), bytes);
			}
			else if (value is uint) {
				return ShowNumericForms((int)(uint)value, NumericForm.Unsigned);
			}
			else if (value is ulong) {
				return ShowNumericForms((long)(ulong)value, NumericForm.Unsigned);
			}
			else if (value is ushort) {
				return ShowNumericForms((short)(ushort)value, NumericForm.Unsigned);
			}
			else if (value is sbyte) {
				return ShowNumericForms(((sbyte)value).ToString(), new byte[] { (byte)(sbyte)value });
			}
			return null;
		}

		static StackPanel ShowStringInfo(string sv) {
			return new StackPanel()
				.Add(new StackPanel().MakeHorizontal().AddReadOnlyTextBox(sv.Length.ToString()).Add(new ThemedTipText("chars", true)))
				//.Add(new StackPanel().MakeHorizontal().AddReadOnlyNumericTextBox(System.Text.Encoding.UTF8.GetByteCount(sv).ToString()).AddText("UTF-8 bytes", true))
				//.Add(new StackPanel().MakeHorizontal().AddReadOnlyNumericTextBox(System.Text.Encoding.Default.GetByteCount(sv).ToString()).AddText("System bytes", true))
				.Add(new StackPanel().MakeHorizontal().AddReadOnlyTextBox(sv.GetHashCode().ToString()).Add(new ThemedTipText("Hash code", true)));
		}

		static void ShowAttributes(ThemedTipParagraph p, ImmutableArray<AttributeData> attrs, bool isReturn) {
			foreach (var item in attrs) {
				if (item.AttributeClass.IsAccessible(true)) {
					_SymbolFormatter.Format(p.Content.AppendLine().Inlines, item, isReturn);
				}
			}
		}

		static void ShowBaseType(IList<object> qiContent, ITypeSymbol typeSymbol) {
			var baseType = typeSymbol.BaseType;
			if (baseType == null || baseType.IsCommonClass() != false) {
				return;
			}
			var classList = new ThemedTipText("Base type: ", true)
				.AddSymbol(baseType, null, _SymbolFormatter.Class);
			var info = new ThemedTipDocument().Append(new ThemedTipParagraph(KnownImageIds.ParentChild, classList));
			while (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseTypeInheritence) && (baseType = baseType.BaseType) != null) {
				if (baseType.IsAccessible(false) && baseType.IsCommonClass() == false) {
					classList.Append(" - ").AddSymbol(baseType, null, _SymbolFormatter.Class);
				}
			}
			qiContent.Add(info);
		}

		static void ShowEnumInfo(IList<object> qiContent, INamedTypeSymbol type, bool fromEnum) {
			if (!Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseType)) {
				return;
			}

			var t = type.EnumUnderlyingType;
			if (t == null) {
				return;
			}
			var content = new ThemedTipText("Enum underlying type: ", true).AddSymbolDisplayParts(t.ToDisplayParts(WpfHelper.QuickInfoSymbolDisplayFormat), _SymbolFormatter);
			var s = new ThemedTipDocument()
				.Append(new ThemedTipParagraph(KnownImageIds.Enumeration, content));
			if (fromEnum == false) {
				qiContent.Add(s);
				return;
			}
			var c = 0;
			object min = null, max = null, bits = null;
			IFieldSymbol minName = null, maxName = null;
			foreach (var m in type.GetMembers()) {
				var f = m as IFieldSymbol;
				if (f == null) {
					continue;
				}
				var v = f.ConstantValue;
				if (v == null) {
					// hack: the value could somehow be null, if the semantic model is not completely loaded
					continue;
				}
				++c;
				if (min == null) {
					min = max = bits = v;
					minName = maxName = f;
					continue;
				}
				if (UnsafeArithmeticHelper.IsGreaterThan(v, max)) {
					max = v;
					maxName = f;
				}
				if (UnsafeArithmeticHelper.IsLessThan(v, min)) {
					min = v;
					minName = f;
				}
				bits = UnsafeArithmeticHelper.Or(v, bits);
			}
			if (min == null) {
				return;
			}
			content.AppendLine().Append("Field count: ", true).Append(c.ToString())
				.AppendLine().Append("Min: ", true)
					.Append(min.ToString() + "(")
					.Append(minName.Name, _SymbolFormatter.Enum)
					.Append(")")
				.AppendLine().Append("Max: ", true)
					.Append(max.ToString() + "(")
					.Append(maxName.Name, _SymbolFormatter.Enum)
					.Append(")");
			if (type.GetAttributes().Any(a => a.AttributeClass.ToDisplayString() == "System.FlagsAttribute")) {
				var d = Convert.ToString(Convert.ToInt64(bits), 2);
				content.AppendLine().Append("All flags: ", true)
					.Append(d)
					.Append(" (")
					.Append(d.Length.ToString())
					.Append(d.Length > 1 ? " bits)" : " bit)");
			}
			qiContent.Add(s);
		}

		static void ShowInterfaces(IList<object> qiContent, ITypeSymbol type) {
			const string Disposable = "IDisposable";
			var showAll = Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfacesInheritence);
			var interfaces = type.Interfaces;
			if (interfaces.Length == 0 && showAll == false) {
				return;
			}
			var declaredInterfaces = new List<INamedTypeSymbol>(interfaces.Length);
			var inheritedInterfaces = new List<INamedTypeSymbol>(5);
			INamedTypeSymbol disposable = null;
			foreach (var item in interfaces) {
				if (item.Name == Disposable) {
					disposable = item;
					continue;
				}
				if (item.DeclaredAccessibility == Accessibility.Public || item.Locations.Any(l => l.IsInSource)) {
					declaredInterfaces.Add(item);
				}
			}
			foreach (var item in type.AllInterfaces) {
				if (interfaces.Contains(item)) {
					continue;
				}
				if (item.Name == Disposable) {
					disposable = item;
					continue;
				}
				if (showAll
					&& (item.DeclaredAccessibility == Accessibility.Public || item.Locations.Any(l => l.IsInSource))) {
					inheritedInterfaces.Add(item);
				}
			}
			if (declaredInterfaces.Count == 0 && inheritedInterfaces.Count == 0 && disposable == null) {
				return;
			}
			var info = new ThemedTipDocument().AppendTitle(KnownImageIds.Interface, "Interface:");
			if (disposable != null) {
				var t = ToUIText(disposable);
				if (interfaces.Contains(disposable) == false) {
					t.Append(" (inherited)");
				}
				info.Append(new ThemedTipParagraph(KnownImageIds.PartWarning, t));
			}
			foreach (var item in declaredInterfaces) {
				if (item == disposable) {
					continue;
				}
				info.Append(new ThemedTipParagraph(item.GetImageId(), ToUIText(item)));
			}
			foreach (var item in inheritedInterfaces) {
				info.Append(new ThemedTipParagraph(item.GetImageId(), ToUIText(item).Append(" (inherited)")));
			}
			qiContent.Add(info);
		}

		static void ShowDeclarationModifier(IList<object> qiContent, ISymbol symbol) {
			qiContent.Add(new ThemedTipParagraph(KnownImageIds.ControlAltDel, _SymbolFormatter.ShowSymbolDeclaration(symbol, new ThemedTipText(), true, false)));
		}

		void ShowParameterInfo(IList<object> qiContent, SyntaxNode node) {
			var argument = node;
			if (node.Kind() == SyntaxKind.NullLiteralExpression) {
				argument = node.Parent;
			}
			int depth = 0;
			do {
				var n = argument as ArgumentSyntax ?? (SyntaxNode)(argument as AttributeArgumentSyntax);
				if (n != null) {
					ShowArgumentInfo(qiContent, n);
					return;
				}
			} while ((argument = argument.Parent) != null && ++depth < 4);
		}

		void ShowArgumentInfo(IList<object> qiContent, SyntaxNode argument) {
			var argList = argument.Parent;
			SeparatedSyntaxList<ArgumentSyntax> arguments;
			int argIndex, argCount;
			string argName;
			switch (argList.Kind()) {
				case SyntaxKind.ArgumentList:
					arguments = (argList as ArgumentListSyntax).Arguments;
					argIndex = arguments.IndexOf(argument as ArgumentSyntax);
					argCount = arguments.Count;
					argName = (argument as ArgumentSyntax).NameColon?.Name.ToString();
					break;
				//case SyntaxKind.BracketedArgumentList: arguments = (argList as BracketedArgumentListSyntax).Arguments; break;
				case SyntaxKind.AttributeArgumentList:
					var aa = (argument.Parent as AttributeArgumentListSyntax).Arguments;
					argIndex = aa.IndexOf(argument as AttributeArgumentSyntax);
					argCount = aa.Count;
					argName = (argument as AttributeArgumentSyntax).NameColon?.Name.ToString();
					break;
				default:
					return;
			}
			if (argIndex == -1) {
				return;
			}
			var symbol = _SemanticModel.GetSymbolInfo(argList.Parent);
			if (symbol.Symbol != null) {
				var m = symbol.Symbol as IMethodSymbol;
				if (m == null) { // in a very rare case m can be null
					return;
				}
				var om = m.OriginalDefinition;
				IParameterSymbol p = null;
				if (argName != null) {
					var mp = om.Parameters;
					for (int i = 0; i < mp.Length; i++) {
						if (mp[i].Name == argName) {
							argIndex = i;
							p = mp[i];
							break;
						}
					}
				}
				else if (argIndex != -1) {
					var mp = om.Parameters;
					if (argIndex < mp.Length) {
						argName = (p = mp[argIndex]).Name;
					}
					else if (mp.Length > 1 && mp[mp.Length - 1].IsParams) {
						argName = (p = mp[mp.Length - 1]).Name;
					}
				}
				var doc = argName != null ? new XmlDoc(om.MethodKind == MethodKind.DelegateInvoke ? om.ContainingSymbol : om, _SemanticModel.Compilation) : null;
				var paramDoc = doc?.GetParameter(argName);
				var content = new ThemedTipText("Argument", true)
					.Append(" of ")
					.AddSymbol(m.ReturnType, _SymbolFormatter)
					.Append(" ").AddSymbol(om.MethodKind != MethodKind.DelegateInvoke && om.MethodKind != MethodKind.Constructor ? om : (ISymbol)om.ContainingType, _SymbolFormatter)
					.AddParameters(om.Parameters, _SymbolFormatter, argIndex);
				var info = new ThemedTipDocument().Append(new ThemedTipParagraph(KnownImageIds.Parameter, content));
				if (paramDoc != null) {
					content.Append("\n" + argName, true, true, _SymbolFormatter.Parameter).Append(": ");
					new XmlDocRenderer(_SemanticModel.Compilation, _SymbolFormatter, om).Render(paramDoc, content.Inlines);
				}
				if (m.IsGenericMethod) {
					for (int i = 0; i < m.TypeArguments.Length; i++) {
						content.Append("\n");
						ShowTypeParameterInfo(m.TypeParameters[i], m.TypeArguments[i], content);
						var typeParamDoc = doc.GetTypeParameter(m.TypeParameters[i].Name);
						if (typeParamDoc != null) {
							content.Append(": ");
							new XmlDocRenderer(_SemanticModel.Compilation, _SymbolFormatter, m).Render(typeParamDoc, content.Inlines);
						}
					}
				}
				if (p != null && p.Type.TypeKind == TypeKind.Delegate) {
					var invoke = (p.Type as INamedTypeSymbol).DelegateInvokeMethod;
					info.Append(new ThemedTipParagraph(KnownImageIds.Delegate, 
						new ThemedTipText("Delegate signature", true).Append(":").AppendLine()
							.AddSymbol(invoke.ReturnType, _SymbolFormatter)
							.Append(" ").Append(p.Name, _SymbolFormatter.Parameter)
							.AddParameters(invoke.Parameters, _SymbolFormatter)
						));
				}
				foreach (var item in content.Inlines) {
					if (item.Foreground == null) {
						item.Foreground = ThemeHelper.ToolTipTextBrush;
					}
				}
				if (p != null && Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Attributes)) {
					var attrs = p.GetAttributes();
					if (attrs.Length > 0) {
						var para = new ThemedTipParagraph(
							KnownImageIds.FormPostBodyParameterNode,
							new ThemedTipText().Append("Attribute of ").Append(p.Name, true, false, _SymbolFormatter.Parameter).Append(":")
						);
						foreach (var attr in attrs) {
							_SymbolFormatter.Format(para.Content.AppendLine().Inlines, attr, false);
						}
						info.Append(para);
					}
				}
				qiContent.Add(info);
			}
			else if (symbol.CandidateSymbols.Length > 0) {
				var info = new ThemedTipDocument();
				info.Append(new ThemedTipParagraph(KnownImageIds.ParameterWarning, new ThemedTipText("Maybe", true).Append(" argument of")));
				foreach (var candidate in symbol.CandidateSymbols) {
					info.Append(
						new ThemedTipParagraph(
							candidate.GetImageId(),
							new ThemedTipText().AddSymbolDisplayParts(
								candidate.ToDisplayParts(WpfHelper.QuickInfoSymbolDisplayFormat),
								_SymbolFormatter,
								argName == null ? argIndex : Int32.MinValue)
						)
					);
				}
				qiContent.Add(info);
			}
			else if (argList.Parent.IsKind(SyntaxKind.InvocationExpression)) {
				var methodName = (argList.Parent as InvocationExpressionSyntax).Expression.ToString();
				if (methodName == "nameof" && argCount == 1) {
					return;
				}
				qiContent.Add(new ThemedTipText("Argument " + ++argIndex + " of ").Append(methodName, true));
			}
			else {
				qiContent.Add("Argument " + ++argIndex);
			}
		}
		static string ToBinString(byte[] bytes) {
			using (var sbr = ReusableStringBuilder.AcquireDefault((bytes.Length << 3) + bytes.Length)) {
				var sb = sbr.Resource;
				for (int i = 0; i < bytes.Length; i++) {
					ref var b = ref bytes[i];
					if (b == 0 && sb.Length == 0) {
						continue;
					}
					if (sb.Length > 0) {
						sb.Append(' ');
					}
					sb.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
				}
				return sb.Length == 0 ? "00000000" : sb.ToString();
			}
		}

		static string ToHexString(byte[] bytes) {
			switch (bytes.Length) {
				case 1: return bytes[0].ToString("X2");
				case 2: return bytes[0].ToString("X2") + bytes[1].ToString("X2");
				case 4:
					return bytes[0].ToString("X2") + bytes[1].ToString("X2") + " " + bytes[2].ToString("X2") + bytes[3].ToString("X2");
				case 8:
					return bytes[0].ToString("X2") + bytes[1].ToString("X2") + " " + bytes[2].ToString("X2") + bytes[3].ToString("X2") + " "
						+ bytes[4].ToString("X2") + bytes[5].ToString("X2") + " " + bytes[6].ToString("X2") + bytes[7].ToString("X2");
				default:
					return string.Empty;
			}
		}

		static StackPanel ShowNumericForms(string dec, byte[] bytes) {
			return new StackPanel()
				.Add(new StackPanel().MakeHorizontal().AddReadOnlyTextBox(dec).Add(new ThemedTipText(" DEC", true)))
				.Add(new StackPanel().MakeHorizontal().AddReadOnlyTextBox(ToHexString(bytes)).Add(new ThemedTipText(" HEX", true)))
				.Add(new StackPanel().MakeHorizontal().AddReadOnlyTextBox(ToBinString(bytes)).Add(new ThemedTipText(" BIN", true)));
		}

		static TextBlock ToUIText(ISymbol symbol) {
			return new ThemedTipText().AddSymbolDisplayParts(symbol.ToDisplayParts(WpfHelper.QuickInfoSymbolDisplayFormat), _SymbolFormatter, -1);
		}

		enum NumericForm
		{
			None,
			Negative,
			Unsigned
		}
	}
}
