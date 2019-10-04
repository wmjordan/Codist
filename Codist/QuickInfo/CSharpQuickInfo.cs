using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

namespace Codist.QuickInfo
{
	sealed class CSharpQuickInfo : IAsyncQuickInfoSource
	{
		internal const string Name = nameof(CSharpQuickInfo);

		static readonly SymbolFormatter _SymbolFormatter = SymbolFormatter.Instance;

		readonly bool _IsVsProject;
		readonly ITextBuffer _TextBuffer;
		SemanticModel _SemanticModel;
		bool _isCandidate;

		public CSharpQuickInfo(ITextBuffer subjectBuffer) {
			ThreadHelper.ThrowIfNotOnUIThread();
			_TextBuffer = subjectBuffer;
			_IsVsProject = TextEditorHelper.IsVsixProject();
		}

		public async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
			if (Keyboard.Modifiers == ModifierKeys.Control) {
				return null;
			}
			var qiWrapper = Config.Instance.QuickInfoOptions.HasAnyFlag(QuickInfoOptions.QuickInfoOverride)
				? QuickInfoOverrider.CreateOverrider(session)
				: null;
			var qiContent = new QiContainer(qiWrapper);
			// Map the trigger point down to our buffer.
			// It is weird that the session.TextView.TextBuffer != _TextBuffer and we can't get a Workspace from the former one
			var currentSnapshot = _TextBuffer.CurrentSnapshot;
			var subjectTriggerPoint = session.GetTriggerPoint(currentSnapshot).GetValueOrDefault();
			if (subjectTriggerPoint.Snapshot == null) {
				return null;
			}

			SemanticModel semanticModel;
			var container = _TextBuffer.AsTextContainer();
			DocumentId docId;
			if (Workspace.TryGetWorkspace(container, out var workspace) == false
				|| (docId = workspace.GetDocumentIdInCurrentContext(container)) == null
				|| workspace.CurrentSolution.GetDocument(docId).TryGetSemanticModel(out semanticModel) == false) {
				return null;
			}

			_SemanticModel = semanticModel;
			var unitCompilation = semanticModel.SyntaxTree.GetCompilationUnitRoot(cancellationToken);

			//look for occurrences of our QuickInfo words in the span
			var token = unitCompilation.FindToken(subjectTriggerPoint, true);
			var skipTriggerPointCheck = false;
			SyntaxNode node;
			switch (token.Kind()) {
				case SyntaxKind.WhitespaceTrivia:
				case SyntaxKind.SingleLineCommentTrivia:
				case SyntaxKind.MultiLineCommentTrivia:
					return null;
				case SyntaxKind.OpenBraceToken:
				case SyntaxKind.CloseBraceToken:
					qiWrapper.KeepBuiltInXmlDoc = true;
					break;
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
					return null;
				case SyntaxKind.AsKeyword:
					var asType = (unitCompilation.FindNode(token.Span) as BinaryExpressionSyntax)?.GetLastIdentifier();
					if (asType != null) {
						token = asType.Identifier;
						skipTriggerPointCheck = true;
					}
					break;
				case SyntaxKind.ReturnKeyword:
					var tb = await ShowReturnInfoAsync(unitCompilation.FindNode(token.Span) as ReturnStatementSyntax, cancellationToken).ConfigureAwait(false);
					return tb != null ? new QuickInfoItem(token.Span.CreateSnapshotSpan(currentSnapshot).ToTrackingSpan(), tb) : null;
				case SyntaxKind.AwaitKeyword:
					node = (unitCompilation.FindNode(token.Span) as AwaitExpressionSyntax)?.Expression;
					goto PROCESS;
				case SyntaxKind.OpenParenToken:
				case SyntaxKind.CloseParenToken:
				case SyntaxKind.DotToken:
				case SyntaxKind.CommaToken:
				case SyntaxKind.ColonToken:
				case SyntaxKind.SemicolonToken:
					token = token.GetPreviousToken();
					skipTriggerPointCheck = true;
					break;
				case SyntaxKind.LessThanToken:
				case SyntaxKind.GreaterThanToken:
					node = unitCompilation.FindNode(token.Span, false, false);
					if (node is BinaryExpressionSyntax) {
						goto PROCESS;
					}
					else {
						goto case SyntaxKind.OpenParenToken;
					}
				case SyntaxKind.EndRegionKeyword:
					qiContent.Add(new ThemedTipText("End of region ")
						.SetGlyph(ThemeHelper.GetImage(KnownImageIds.Numeric))
						.Append((unitCompilation.FindNode(token.Span, true, false) as EndRegionDirectiveTriviaSyntax).GetRegion()?.GetDeclarationSignature(), true)
						);
					return new QuickInfoItem(currentSnapshot.CreateTrackingSpan(token.SpanStart, token.Span.Length, SpanTrackingMode.EdgeInclusive), qiContent.ToUI());
				case SyntaxKind.VoidKeyword:
					return null;
				default:
					if (token.Span.Contains(subjectTriggerPoint, true) == false
						|| token.IsReservedKeyword()) {
						node = unitCompilation.FindNode(token.Span, false, false);
						if (node is StatementSyntax) {
							ShowBlockInfo(qiContent, currentSnapshot, node, semanticModel);
						}
						if (qiContent.Count > 0) {
							return new QuickInfoItem(currentSnapshot.CreateTrackingSpan(token.SpanStart, token.Span.Length, SpanTrackingMode.EdgeInclusive), qiContent.ToUI());
						}
						return null;
					}
					break;
			}
			node = unitCompilation.FindNode(token.Span, true, true);
			if (node == null
				|| skipTriggerPointCheck == false && node.Span.Contains(subjectTriggerPoint.Position, true) == false) {
				return null;
			}
			node = node.UnqualifyExceptNamespace();
			LocateNodeInParameterList(ref node, ref token);

			PROCESS:
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter)) {
				ShowParameterInfo(qiContent, node);
			}
			ImmutableArray<ISymbol> candidates;
			var symbol = token.IsKind(SyntaxKind.CloseBraceToken) ? null
				: GetSymbol(semanticModel, node, ref candidates, cancellationToken);
			if (token.IsKind(SyntaxKind.AwaitKeyword)
				&& symbol != null && symbol.Kind == SymbolKind.Method) {
				symbol = (symbol.GetReturnType() as INamedTypeSymbol).TypeArguments.FirstOrDefault();
			}
			if (_isCandidate = candidates.IsDefaultOrEmpty == false) {
				ShowCandidateInfo(qiContent, candidates);
			}
			if (symbol == null) {
				ShowMiscInfo(qiContent, currentSnapshot, node);
				if (node.IsKind(SyntaxKind.Block) || node.IsKind(SyntaxKind.SwitchStatement)) {
					ShowBlockInfo(qiContent, currentSnapshot, node, semanticModel);
				}
				goto RETURN;
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation)) {
				qiContent.Add(await ShowAvailabilityAsync(docId, workspace, token, cancellationToken));
				var ctor = node.Parent as ObjectCreationExpressionSyntax;
				OverrideDocumentation(node, qiWrapper,
					ctor?.Type == node ? semanticModel.GetSymbolInfo(ctor, cancellationToken).Symbol
						: node.Parent.IsKind(SyntaxKind.Attribute) ? symbol.ContainingType
						: symbol);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Attributes)) {
				ShowAttributesInfo(qiContent, symbol);
			}
			ShowSymbolInfo(session, qiContent, node, symbol);
		RETURN:
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ClickAndGo)) {
				var ctor = node.Parent as ObjectCreationExpressionSyntax;
				if (ctor != null && ctor.Type == node) {
					symbol = semanticModel.GetSymbolOrFirstCandidate(ctor, cancellationToken);
					if (symbol == null) {
						return null;
					}
					if (symbol.IsImplicitlyDeclared) {
						symbol = symbol.ContainingType;
					}
				}
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Diagnostics)) {
					qiWrapper.SetDiagnostics(semanticModel.GetDiagnostics(token.Span, cancellationToken));
				}
				qiWrapper.ApplyClickAndGo(symbol, _TextBuffer, session);
			}
			return new QuickInfoItem(qiContent.Count > 0 && session.TextView.TextSnapshot == currentSnapshot
				? currentSnapshot.CreateTrackingSpan(token.SpanStart, token.Span.Length, SpanTrackingMode.EdgeExclusive)
				: null, qiContent.ToUI());
		}

		static async Task<ThemedTipDocument> ShowAvailabilityAsync(DocumentId docId, Workspace workspace, SyntaxToken token, CancellationToken cancellationToken) {
			ThemedTipDocument r = null;
			if (workspace.CurrentSolution.ProjectIds.Count > 0) {
				var linkedDocuments = workspace.CurrentSolution.GetDocument(docId).GetLinkedDocumentIds();
				if (linkedDocuments.Length > 0) {
					ImmutableArray<ISymbol> candidates;
					foreach (var id in linkedDocuments) {
						var d = workspace.CurrentSolution.GetDocument(id);
						var sm = await d.GetSemanticModelAsync(cancellationToken);
						if (GetSymbol(sm, sm.SyntaxTree.GetCompilationUnitRoot(cancellationToken).FindNode(token.Span, true, true), ref candidates, cancellationToken) == null) {
							if (r == null) {
								r = new ThemedTipDocument().AppendTitle(KnownImageIds.StatusWarning, "Symbol unavailable in...");
							}
							r.Append(new ThemedTipParagraph(KnownImageIds.CSProjectNode, new ThemedTipText(d.Project.Name)));
						}
					}
				}
			}
			return r;
		}

		static ISymbol GetSymbol(SemanticModel semanticModel, SyntaxNode node, ref ImmutableArray<ISymbol> candidates, CancellationToken cancellationToken) {
			if (node.IsKind(SyntaxKind.BaseExpression)) {
				return semanticModel.GetTypeInfo(node, cancellationToken).ConvertedType;
			}
			else if (node.IsKind(SyntaxKind.ThisExpression)) {
				return semanticModel.GetTypeInfo(node, cancellationToken).Type;
			}
			var symbolInfo = semanticModel.GetSymbolInfo(node, cancellationToken);
			if (symbolInfo.CandidateReason != CandidateReason.None) {
				candidates = symbolInfo.CandidateSymbols;
				return symbolInfo.CandidateSymbols.FirstOrDefault();
			}
			return symbolInfo.Symbol
				?? (node.Kind().IsDeclaration() || node.Kind() == SyntaxKind.VariableDeclarator
					? semanticModel.GetDeclaredSymbol(node, cancellationToken)
					: semanticModel.GetSymbolExt(node, cancellationToken));
		}

		static void LocateNodeInParameterList(ref SyntaxNode node, ref SyntaxToken token) {
			if (node.IsKind(SyntaxKind.Argument)) {
				node = ((ArgumentSyntax)node).Expression;
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

		ThemedTipDocument OverrideDocumentation(SyntaxNode node, IQuickInfoOverrider qiWrapper, ISymbol symbol) {
			if (symbol == null) {
				return null;
			}
			symbol = symbol.GetAliasTarget();
			var doc = new XmlDoc(symbol, _SemanticModel.Compilation);
			var docRenderer = new XmlDocRenderer(_SemanticModel.Compilation, SymbolFormatter.Instance, symbol);
			var tip = docRenderer.RenderXmlDoc(node, symbol, doc, _SemanticModel);

			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ExceptionDoc)
				&& (symbol.Kind == SymbolKind.Method || symbol.Kind == SymbolKind.Property || symbol.Kind == SymbolKind.Event)
				&& doc.Exceptions != null) {
				var p = new ThemedTipParagraph(KnownImageIds.StatusInvalidOutline, new ThemedTipText("Exception:", true));
				foreach (var ex in doc.Exceptions) {
					var et = ex.Attribute("cref");
					if (et != null) {
						docRenderer.RenderXmlDocSymbol(et.Value, p.Content.AppendLine().Inlines, SymbolKind.NamedType);
						p.Content.Inlines.LastInline.FontWeight = FontWeights.Bold;
						docRenderer.Render(ex, p.Content.Append(": ").Inlines);
					}
				}
				if (p.Content.Inlines.Count > 1) {
					qiWrapper.OverrideException(new ThemedTipDocument().Append(p));
				}
			}

			// show type XML Doc for constructors
			if ((symbol as IMethodSymbol)?.MethodKind == MethodKind.Constructor) {
				symbol = symbol.ContainingType;
				var summary = new XmlDoc(symbol, _SemanticModel.Compilation)
					.GetDescription(symbol);
				if (summary != null) {
					tip.Append(new ThemedTipParagraph(new ThemedTipText("Documentation from ").AddSymbol(symbol, true, SymbolFormatter.Instance).Append(":")));
					new XmlDocRenderer(_SemanticModel.Compilation, SymbolFormatter.Instance, symbol)
						.Render(summary, tip);
				}
			}

			ShowCapturedVariables(node, symbol, tip);

			if (tip.Children.Count > 0) {
				qiWrapper.OverrideDocumentation(tip);
			}
			return tip;
		}

		void ShowCapturedVariables(SyntaxNode node, ISymbol symbol, ThemedTipDocument tip) {
			if (node is LambdaExpressionSyntax
				|| (symbol as IMethodSymbol)?.MethodKind == MethodKind.LocalFunction) {
				var ss = node is LambdaExpressionSyntax
					? node.AncestorsAndSelf().FirstOrDefault(i => i is StatementSyntax || i is ExpressionSyntax && i.Kind() != SyntaxKind.IdentifierName)
					: symbol.GetSyntaxNode();
				if (ss != null) {
					var df = _SemanticModel.AnalyzeDataFlow(ss);
					var captured = df.ReadInside.RemoveAll(i => df.VariablesDeclared.Contains(i));
					if (captured.Length > 0) {
						var p = new ThemedTipParagraph(KnownImageIds.ExternalVariableValue, new ThemedTipText().Append("Captured variables", true));
						int i = 0;
						foreach (var item in captured) {
							p.Content.Append(++i == 1 ? ": " : ", ").AddSymbol(item, false, _SymbolFormatter);
						}
						tip.Append(p);
					}
				}
			}
		}

		static void ShowCandidateInfo(QiContainer qiContent, ImmutableArray<ISymbol> candidates) {
			var info = new ThemedTipDocument().AppendTitle(KnownImageIds.CodeInformation, "Maybe...");
			foreach (var item in candidates) {
				info.Append(new ThemedTipParagraph(item.GetImageId(), ToUIText(item)));
			}
			qiContent.Add(info);
		}

		void ShowSymbolInfo(IAsyncQuickInfoSession session, QiContainer qiContent, SyntaxNode node, ISymbol symbol) {
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
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color)
						&& m.ContainingType?.Name == "Color"
						&& session.Mark(nameof(ColorQuickInfo))) {
						qiContent.Add(ColorQuickInfo.PreviewColorMethodInvocation(_SemanticModel, node, symbol as IMethodSymbol));
					}
					if (m.MethodKind == MethodKind.BuiltinOperator && node is ExpressionSyntax) {
						var value = _SemanticModel.GetConstantValue(node);
						if (value.HasValue) {
							ShowConstInfo(qiContent, null, value.Value);
						}
					}
					break;
				case SymbolKind.NamedType:
					ShowTypeInfo(qiContent, node, symbol as INamedTypeSymbol);
					break;
				case SymbolKind.Property:
					ShowPropertyInfo(qiContent, symbol as IPropertySymbol);
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color)
						&& session.Mark(nameof(ColorQuickInfo))) {
						qiContent.Add(ColorQuickInfo.PreviewColorProperty(symbol as IPropertySymbol, _IsVsProject));
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
					var invoke = ((INamedTypeSymbol)st).DelegateInvokeMethod;
					qiContent.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(KnownImageIds.Delegate,
						new ThemedTipText("Delegate signature", true).Append(":").AppendLine()
							.AddSymbol(invoke.ReturnType, false, _SymbolFormatter)
							.Append(" ").AddSymbol(st, true, _SymbolFormatter)
							.AddParameters(invoke.Parameters, _SymbolFormatter)
						)));
				}
			}

		}

		static void ShowBlockInfo(QiContainer qiContent, ITextSnapshot textSnapshot, SyntaxNode node, SemanticModel semanticModel) {
			var lines = textSnapshot.GetLineSpan(node.Span).Length + 1;
			if (lines > 100) {
				qiContent.Add(new ThemedTipText(lines + " lines", true));
			}
			else if (lines > 1) {
				qiContent.Add(lines + " lines");
			}
			var df = semanticModel.AnalyzeDataFlow(node);
			var vd = df.VariablesDeclared;
			if (vd.IsEmpty == false) {
				var p = new ThemedTipText("Declared variable: ", true).Append(vd.Length).AppendLine();
				var s = false;
				foreach (var item in vd) {
					if (s) {
						p.Append(", ");
					}
					p.AddSymbol(item, false, _SymbolFormatter.Local);
					s = true;
				}
				qiContent.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(KnownImageIds.LocalVariable, p)));
			}
			vd = df.DataFlowsIn;
			if (vd.IsEmpty == false) {
				var p = new ThemedTipText("Read variable: ", true).Append(vd.Length).AppendLine();
				var s = false;
				foreach (var item in vd) {
					if (s) {
						p.Append(", ");
					}
					if (item.IsImplicitlyDeclared) {
						p.Append(item.Name);
					}
					else {
						p.AddSymbol(item, false, _SymbolFormatter);
					}
					s = true;
				}
				qiContent.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(KnownImageIds.ExternalVariableValue, p)));
			}
		}
		static void ShowMiscInfo(QiContainer qiContent, ITextSnapshot currentSnapshot, SyntaxNode node) {
			StackPanel infoBox = null;
			var nodeKind = node.Kind();
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues) && (nodeKind == SyntaxKind.NumericLiteralExpression || nodeKind == SyntaxKind.CharacterLiteralExpression)) {
				infoBox = ToolTipFactory.ShowNumericForms(node);
			}
			else if (nodeKind == SyntaxKind.SwitchStatement) {
				var s = ((SwitchStatementSyntax)node).Sections.Count;
				if (s > 1) {
					var cases = 0;
					foreach (var section in ((SwitchStatementSyntax)node).Sections) {
						cases += section.Labels.Count;
					}
					qiContent.Add($"{s} switch sections, {cases} cases");
				}
				else {
					s = ((SwitchStatementSyntax)node).Sections[0].Labels.Count;
					if (s > 1) {
						qiContent.Add($"1 switch section, {s} cases");
					}
				}
			}
			else if (nodeKind == SyntaxKind.StringLiteralExpression) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.String)) {
					infoBox = ShowStringInfo(node.GetFirstToken().ValueText, false);
				}
			}

			if (infoBox != null) {
				qiContent.Add(infoBox);
			}
		}

		async Task<ThemedTipText> ShowReturnInfoAsync(ReturnStatementSyntax returns, CancellationToken cancellationToken) {
			if (returns == null) {
				return null;
			}
			SyntaxNode node = returns;
			var method = returns.Expression != null
				? _SemanticModel.GetSymbolInfo(returns.Expression, cancellationToken).Symbol as IMethodSymbol
				: null;
			while ((node = node.Parent) != null) {
				var nodeKind = node.Kind();
				if (nodeKind.IsMemberDeclaration() == false
					&& nodeKind != SyntaxKind.SimpleLambdaExpression
					&& nodeKind != SyntaxKind.ParenthesizedLambdaExpression
					&& nodeKind != SyntaxKind.LocalFunctionStatement) {
					continue;
				}
				var name = node.GetDeclarationSignature();
				if (name == null) {
					continue;
				}
				var symbol = _SemanticModel.GetSymbolInfo(node, cancellationToken).Symbol ?? _SemanticModel.GetDeclaredSymbol(node, cancellationToken);
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
				var t = new ThemedTipText();
				t.SetGlyph(ThemeHelper.GetImage(KnownImageIds.ReturnValue));
				if (method != null) {
					if (method.MethodKind == MethodKind.AnonymousFunction) {
						t.Append("Return anonymous function for ");
					}
					else {
						t.Append("Return ")
							.AddSymbol(method.GetReturnType(), false, _SymbolFormatter)
							.Append(" for ");
					}
				}
				else {
					t.Append("Return ")
						.AddSymbol(symbol?.GetReturnType(), false, _SymbolFormatter)
						.Append(" for ");
				}
				if (symbol != null) {
					t.AddSymbol(symbol, node is LambdaExpressionSyntax ? "lambda expression" + name : null, _SymbolFormatter);
				}
				else {
					t.Append(name);
				}
				return t;
			}
			return null;
		}

		ITypeSymbol GetAwaitReturnType(SemanticModel semanticModel, AwaitExpressionSyntax expression, CancellationToken cancellationToken) {
			return expression == null ? null : (_SemanticModel.GetSymbolOrFirstCandidate(expression.Expression, cancellationToken) as IMethodSymbol)?.GetReturnType();
		}

		static void ShowAttributesInfo(QiContainer qiContent, ISymbol symbol) {
			// todo: show inherited attributes
			var p = ListAttributes(null, symbol.GetAttributes(), false);
			if (symbol.Kind == SymbolKind.Method) {
				p = ListAttributes(p, ((IMethodSymbol)symbol).GetReturnTypeAttributes(), true);
			}
			if (p != null) {
				qiContent.Add(new ThemedTipDocument().Append(p));
			}

			ThemedTipParagraph ListAttributes(ThemedTipParagraph paragraph, ImmutableArray<AttributeData> attributes, bool isMethodReturnAttrs) {
				if (attributes.Length > 0) {
					foreach (var item in attributes) {
						if (item.AttributeClass.IsAccessible(true)) {
							if (paragraph == null) {
								paragraph = new ThemedTipParagraph(KnownImageIds.FormPostBodyParameterNode, new ThemedTipText().Append("Attribute:", true));
							}
							_SymbolFormatter.Format(paragraph.Content.AppendLine().Inlines, item, isMethodReturnAttrs);
						}
					}
				}
				return paragraph;
			}
		}

		static void ShowPropertyInfo(QiContainer qiContent, IPropertySymbol property) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation)) {
				ShowAnonymousTypeInfo(qiContent, property);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
				&& property.ContainingType?.TypeKind != TypeKind.Interface
				&& (property.DeclaredAccessibility != Accessibility.Public || property.IsAbstract || property.IsStatic || property.IsOverride || property.IsVirtual)) {
				ShowDeclarationModifier(qiContent, property);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfaceImplementations)) {
				ShowInterfaceImplementation(qiContent, property, property.ExplicitInterfaceImplementations);
			}
		}

		static void ShowEventInfo(QiContainer qiContent, IEventSymbol ev) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)) {
				if ((ev.DeclaredAccessibility != Accessibility.Public || ev.IsAbstract || ev.IsStatic || ev.IsOverride || ev.IsVirtual)
				&& ev.ContainingType?.TypeKind != TypeKind.Interface) {
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

		static void ShowFieldInfo(QiContainer qiContent, IFieldSymbol field) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
				&& (field.DeclaredAccessibility != Accessibility.Public || field.IsReadOnly || field.IsVolatile || field.IsStatic)
				&& field.ContainingType.TypeKind != TypeKind.Enum) {
				ShowDeclarationModifier(qiContent, field);
			}
			if (field.HasConstantValue) {
				ShowConstInfo(qiContent, field, field.ConstantValue);
			}
			if (field.IsReadOnly && field.IsStatic) {
				qiContent.ShowOpCodeInfo(field);
			}
		}

		void ShowMethodInfo(QiContainer qiContent, SyntaxNode node, IMethodSymbol method) {
			var options = Config.Instance.QuickInfoOptions;
			if (options.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation)) {
				ShowAnonymousTypeInfo(qiContent, method);
			}
			if (options.MatchFlags(QuickInfoOptions.Declaration)
				&& method.ContainingType?.TypeKind != TypeKind.Interface
				&& (method.DeclaredAccessibility != Accessibility.Public || method.IsAbstract || method.IsStatic || method.IsVirtual || method.IsOverride || method.IsExtern || method.IsSealed)) {
				ShowDeclarationModifier(qiContent, method);
			}
			if (options.MatchFlags(QuickInfoOptions.TypeParameters) && method.TypeArguments.Length > 0 && method.TypeParameters[0] != method.TypeArguments[0]) {
				ShowMethodTypeArguments(qiContent, method);
			}
			if (options.MatchFlags(QuickInfoOptions.InterfaceImplementations)) {
				ShowInterfaceImplementation(qiContent, method, method.ExplicitInterfaceImplementations);
			}
			if (options.MatchFlags(QuickInfoOptions.SymbolLocation) && method.IsExtensionMethod) {
				ShowExtensionMethod(qiContent, method);
			}
			if (options.MatchFlags(QuickInfoOptions.MethodOverload)) {
				ShowOverloadsInfo(qiContent, node, method);
			}
		}

		void ShowOverloadsInfo(QiContainer qiContent, SyntaxNode node, IMethodSymbol method) {
			if (_isCandidate) {
				return;
			}
			var overloads = node.Kind() == SyntaxKind.MethodDeclaration || node.Kind() == SyntaxKind.ConstructorDeclaration
				? method.ContainingType.GetMembers(method.Name)
				: _SemanticModel.GetMemberGroup(node);
			if (overloads.Length < 2) {
				return;
			}
			var re = method.MethodKind == MethodKind.ReducedExtension;
			method = method.OriginalDefinition;
			if (method.ReducedFrom != null) {
				method = method.ReducedFrom;
			}
			var rt = method.ReturnType;
			var mps = method.Parameters;
			var ct = method.ContainingType;
			var overloadInfo = new ThemedTipDocument().AppendTitle(KnownImageIds.MethodSet, "Method overload:");
			foreach (var overload in overloads) {
				var om = overload.OriginalDefinition as IMethodSymbol;
				if (om == null) {
					continue;
				}
				var ore = re && om.MethodKind == MethodKind.ReducedExtension;
				if (ore) {
					if (method.Equals(om.ReducedFrom)) {
						continue;
					}
				}
				else if (om.ReducedFrom != null) {
					om = om.ReducedFrom;
				}
				if (om.Equals(method)) {
					continue;
				}
				var t = new ThemedTipText();
				if (om.MethodKind != MethodKind.Constructor) {
					t.AddSymbol(om.ReturnType, false, CodeAnalysisHelper.AreEqual(om.ReturnType, rt) ? SymbolFormatter.SemiTransparent : _SymbolFormatter).Append(" ");
				}
				if (ore) {
					t.AddSymbol(om.ContainingType, "this", (om.ContainingType != ct ?  _SymbolFormatter : SymbolFormatter.SemiTransparent).Class).Append(".");
				}
				else if (om.ContainingType != ct) {
					t.AddSymbol(om.ContainingType, false, _SymbolFormatter).Append(".");
				}
				t.AddSymbol(om, true, _SymbolFormatter);
				t.Append("(");
				foreach (var op in om.Parameters) {
					var mp = mps.FirstOrDefault(p => p.Name == op.Name);
					if (op.Ordinal == 0) {
						if (ore == false && om.IsExtensionMethod) {
							t.Append("this ", _SymbolFormatter.Keyword);
						}
					}
					else {
						t.Append(", ");
					}
					if (mp != null) {
						if (mp.RefKind != op.RefKind
							|| CodeAnalysisHelper.AreEqual(mp.Type, op.Type) == false
							|| mp.IsParams != op.IsParams
							|| mp.IsOptional != op.IsOptional
							|| mp.HasExplicitDefaultValue != op.HasExplicitDefaultValue) {
							mp = null;
						}
					}
					t.AddSymbolDisplayParts(op.ToDisplayParts(WpfHelper.InTypeOverloadDisplayFormat), mp == null ? _SymbolFormatter : SymbolFormatter.SemiTransparent, -1);
				}
				t.Append(")");
				overloadInfo.Append(new ThemedTipParagraph(overload.GetImageId(), t));
			}
			if (overloadInfo.Children.Count > 1) {
				qiContent.Add(overloadInfo);
			}
		}

		static void ShowMethodTypeArguments(QiContainer qiContent, IMethodSymbol method) {
			var info = new ThemedTipDocument();
			var l = method.TypeArguments.Length;
			var content = new ThemedTipText("Type argument:", true);
			info.Append(new ThemedTipParagraph(KnownImageIds.Template, content));
			for (int i = 0; i < l; i++) {
				ShowTypeParameterInfo(method.TypeParameters[i], method.TypeArguments[i], content.AppendLine());
			}
			qiContent.Add(info);
		}

		static void ShowNamespaceInfo(QiContainer qiContent, INamespaceSymbol nsSymbol) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NamespaceTypes) == false) {
				return;
			}
			var namespaces = nsSymbol.GetNamespaceMembers().ToImmutableArray().Sort(Comparer<INamespaceSymbol>.Create((x, y) => String.CompareOrdinal(x.Name, y.Name)));
			if (namespaces.Length > 0) {
				var info = new ThemedTipDocument().AppendTitle(KnownImageIds.Namespace, "Namespace:");
				foreach (var ns in namespaces) {
					info.Append(new ThemedTipParagraph(KnownImageIds.Namespace, new ThemedTipText().Append(ns.Name, _SymbolFormatter.Namespace)));
				}
				qiContent.Add(info);
			}

			var members = nsSymbol.GetTypeMembers().Sort(Comparer<INamedTypeSymbol>.Create((x, y) => String.Compare(x.Name, y.Name)));
			if (members.Length > 0) {
				var info = new StackPanel().Add(new ThemedTipText("Type:", true));
				foreach (var type in members) {
					var t = new ThemedTipText().SetGlyph(ThemeHelper.GetImage(type.GetImageId()));
					_SymbolFormatter.ShowSymbolDeclaration(type, t, true, true);
					t.AddSymbol(type, false, _SymbolFormatter);
					info.Add(t);
				}
				qiContent.Add(info.Scrollable());
			}
		}

		void ShowTypeInfo(QiContainer qiContent, SyntaxNode node, INamedTypeSymbol typeSymbol) {
			var options = Config.Instance.QuickInfoOptions;
			if (options.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation) && typeSymbol.TypeKind == TypeKind.Class) {
				ShowAnonymousTypeInfo(qiContent, typeSymbol);
			}
			if (options.MatchFlags(QuickInfoOptions.MethodOverload)) {
				node = node.GetObjectCreationNode();
				if (node != null) {
					var method = _SemanticModel.GetSymbolOrFirstCandidate(node) as IMethodSymbol;
					if (method != null) {
						ShowOverloadsInfo(qiContent, node, method);
					}
				}
			}
			if (options.MatchFlags(QuickInfoOptions.Declaration)
				&& (typeSymbol.DeclaredAccessibility != Accessibility.Public
					|| typeSymbol.IsStatic
					|| (typeSymbol.IsAbstract || typeSymbol.IsSealed) && typeSymbol.TypeKind == TypeKind.Class)
				) {
				ShowDeclarationModifier(qiContent, typeSymbol);
			}
			if (options.MatchFlags(QuickInfoOptions.BaseType)) {
				if (typeSymbol.TypeKind == TypeKind.Enum) {
					ShowEnumInfo(qiContent, typeSymbol, true);
				}
				else {
					ShowBaseType(qiContent, typeSymbol);
				}
			}
			if (options.MatchFlags(QuickInfoOptions.Interfaces)) {
				ShowInterfaces(qiContent, typeSymbol);
			}
			if (options.MatchFlags(QuickInfoOptions.InterfaceMembers)
				&& typeSymbol.TypeKind == TypeKind.Interface) {
				ShowInterfaceMembers(qiContent, typeSymbol);
			}
		}

		static void ShowConstInfo(QiContainer qiContent, ISymbol symbol, object value) {
			var sv = value as string;
			if (sv != null) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.String)) {
					qiContent.Add(ShowStringInfo(sv, true));
				}
			}
			else if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues)) {
				var s = ToolTipFactory.ShowNumericForms(value);
				if (s != null) {
					if (symbol != null) {
						ShowEnumInfo(qiContent, symbol.ContainingType, false);
					}
					qiContent.Add(s);
				}
			}
		}

		static void ShowInterfaceImplementation<TSymbol>(QiContainer qiContent, TSymbol symbol, IEnumerable<TSymbol> explicitImplementations)
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
			var typeParams = symbol.GetTypeParameters();
			foreach (var intf in interfaces) {
				foreach (var member in intf.GetMembers()) {
					if (member.Kind == symbol.Kind
						&& member.DeclaredAccessibility == Accessibility.Public
						&& member.Name == symbol.Name
						&& member.MatchSignature(symbol.Kind, returnType, parameters, typeParams)) {
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
					(info ?? (info = new ThemedTipDocument()))
						.AppendTitle(KnownImageIds.ImplementInterface, "Explicit implements:");
					foreach (var item in explicitIntfs) {
						info.Append(new ThemedTipParagraph(item.GetImageId(), ToUIText(item)));
					}
				}
			}
			if (info != null) {
				qiContent.Add(info);
			}
		}
		static void ShowInterfaceMembers(QiContainer qiContent, INamedTypeSymbol type) {
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
			var members = new List<ISymbol>(type.GetMembers().RemoveAll(m => (m as IMethodSymbol)?.AssociatedSymbol != null || m.IsImplicitlyDeclared));
			members.Sort(CodeAnalysisHelper.CompareByAccessibilityKindName);
			foreach (var member in members) {
				var t = new ThemedTipText();
				if (isInherit) {
					t.AddSymbol(type, false, _SymbolFormatter).Append(".");
				}
				t.AddSymbol(member, false, _SymbolFormatter);
				if (member.Kind == SymbolKind.Method) {
					t.AddParameters(((IMethodSymbol)member).Parameters, _SymbolFormatter);
				}
				doc.Append(new ThemedTipParagraph(member.GetImageId(), t));
			}
		}

		static void ShowExtensionMethod(QiContainer qiContent, IMethodSymbol method) {
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
				.AddSymbol(typeArgument, true, _SymbolFormatter);
			if (typeParameter.HasConstraint()) {
				text.Append(" (");
				_SymbolFormatter.ShowTypeConstaints(typeParameter, text);
				text.Append(")");
			}
		}

		static StackPanel ShowStringInfo(string sv, bool showText) {
			var s = new StackPanel()
				.Add(new StackPanel { Orientation = Orientation.Horizontal }.AddReadOnlyTextBox(sv.Length.ToString()).Add(new ThemedTipText("chars", true)))
			//.Add(new StackPanel().MakeHorizontal().AddReadOnlyNumericTextBox(System.Text.Encoding.UTF8.GetByteCount(sv).ToString()).AddText("UTF-8 bytes", true))
			//.Add(new StackPanel().MakeHorizontal().AddReadOnlyNumericTextBox(System.Text.Encoding.Default.GetByteCount(sv).ToString()).AddText("System bytes", true))
				.Add(new StackPanel { Orientation = Orientation.Horizontal }.AddReadOnlyTextBox(sv.GetHashCode().ToString()).Add(new ThemedTipText("Hash code", true)));
			if (showText) {
				s.Add(new StackPanel { Orientation = Orientation.Horizontal }.AddReadOnlyTextBox(sv, true).Add(new ThemedTipText("text", true)));
			}
			return s;
		}

		static void ShowBaseType(QiContainer qiContent, ITypeSymbol typeSymbol) {
			var baseType = typeSymbol.BaseType;
			if (baseType == null || baseType.IsCommonClass()) {
				return;
			}
			var classList = new ThemedTipText("Base type: ", true)
				.AddSymbol(baseType, null, _SymbolFormatter);
			var info = new ThemedTipDocument().Append(new ThemedTipParagraph(KnownImageIds.ParentChild, classList));
			while (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseTypeInheritence) && (baseType = baseType.BaseType) != null) {
				if (baseType.IsCommonClass() == false) {
					classList.Append(" - ").AddSymbol(baseType, null, _SymbolFormatter);
				}
			}
			qiContent.Add(info);
		}

		static void ShowEnumInfo(QiContainer qiContent, INamedTypeSymbol type, bool fromEnum) {
			if (!Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseType)) {
				return;
			}

			var t = type.EnumUnderlyingType;
			if (t == null) {
				return;
			}
			var content = new ThemedTipText("Enum underlying type: ", true).AddSymbol(t, true, _SymbolFormatter);
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
					return;
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
			if (type.GetAttributes().Any(a => a.AttributeClass.Name == nameof(FlagsAttribute) && a.AttributeClass.ContainingNamespace.ToDisplayString() == "System")) {
				var d = Convert.ToString(Convert.ToInt64(bits), 2);
				content.AppendLine().Append("All flags: ", true)
					.Append($"{d} ({ d.Length}" + (d.Length > 1 ? " bits)" : " bit)"));
			}
			qiContent.Add(s);
		}

		static void ShowInterfaces(QiContainer qiContent, ITypeSymbol type) {
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
				if (item != disposable) {
					info.Append(new ThemedTipParagraph(item.GetImageId(), ToUIText(item)));
				}
			}
			foreach (var item in inheritedInterfaces) {
				info.Append(new ThemedTipParagraph(item.GetImageId(), ToUIText(item).Append(" (inherited)")));
			}
			qiContent.Add(info);
		}

		static void ShowDeclarationModifier(QiContainer qiContent, ISymbol symbol) {
			qiContent.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(KnownImageIds.ControlAltDel, _SymbolFormatter.ShowSymbolDeclaration(symbol, new ThemedTipText(), true, false))));
		}

		void ShowParameterInfo(QiContainer qiContent, SyntaxNode node) {
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

		void ShowArgumentInfo(QiContainer qiContent, SyntaxNode argument) {
			var argList = argument.Parent;
			SeparatedSyntaxList<ArgumentSyntax> arguments;
			int argIndex, argCount;
			string argName;
			switch (argList.Kind()) {
				case SyntaxKind.ArgumentList:
					arguments = ((ArgumentListSyntax)argList).Arguments;
					argIndex = arguments.IndexOf(argument as ArgumentSyntax);
					argCount = arguments.Count;
					argName = ((ArgumentSyntax)argument).NameColon?.Name.ToString();
					break;
				//case SyntaxKind.BracketedArgumentList: arguments = (argList as BracketedArgumentListSyntax).Arguments; break;
				case SyntaxKind.AttributeArgumentList:
					var aa = ((AttributeArgumentListSyntax)argument.Parent).Arguments;
					argIndex = aa.IndexOf((AttributeArgumentSyntax)argument);
					argCount = aa.Count;
					argName = ((AttributeArgumentSyntax)argument).NameColon?.Name.ToString();
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
					else if (mp.Length > 0 && mp[mp.Length - 1].IsParams) {
						argIndex = mp.Length - 1;
						argName = (p = mp[argIndex]).Name;
					}
				}
				var doc = argName != null ? new XmlDoc(om.MethodKind == MethodKind.DelegateInvoke ? om.ContainingSymbol : om, _SemanticModel.Compilation) : null;
				var paramDoc = doc?.GetParameter(argName);
				var content = new ThemedTipText("Argument", true)
					.Append(" of ")
					.AddSymbol(om.ReturnType, om.MethodKind == MethodKind.Constructor ? "new" : null, _SymbolFormatter)
					.Append(" ")
					.AddSymbol(om.MethodKind != MethodKind.DelegateInvoke ? om : (ISymbol)om.ContainingType, true, _SymbolFormatter)
					.AddParameters(om.Parameters, _SymbolFormatter, argIndex);
				var info = new ThemedTipDocument().Append(new ThemedTipParagraph(KnownImageIds.Parameter, content));
				if (paramDoc != null) {
					content.Append("\n" + argName, true, false, _SymbolFormatter.Parameter).Append(": ");
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
					var invoke = ((INamedTypeSymbol)p.Type).DelegateInvokeMethod;
					info.Append(new ThemedTipParagraph(KnownImageIds.Delegate,
						new ThemedTipText("Delegate signature", true).Append(":").AppendLine()
							.AddSymbol(invoke.ReturnType, false, _SymbolFormatter)
							.Append(" ").Append(p.Name, true, false, _SymbolFormatter.Parameter)
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
				var methodName = ((InvocationExpressionSyntax)argList.Parent).Expression.ToString();
				if (methodName == "nameof" && argCount == 1) {
					return;
				}
				qiContent.Add(new ThemedTipText("Argument " + ++argIndex + " of ").Append(methodName, true));
			}
			else {
				qiContent.Add("Argument " + ++argIndex);
			}
		}

		static void ShowAnonymousTypeInfo(QiContainer container, ISymbol symbol) {
			ITypeSymbol t;
			List<ITypeSymbol> types = null;
			switch (symbol.Kind) {
				case SymbolKind.NamedType:
					if ((t = symbol as ITypeSymbol).IsAnonymousType) {
						Add(ref types, t);
					}
					break;
				case SymbolKind.Method:
					var m = symbol as IMethodSymbol;
					if (m.IsGenericMethod) {
						foreach (var item in m.TypeArguments) {
							if (item.IsAnonymousType) {
								Add(ref types, item as ITypeSymbol);
							}
						}
					}
					else if (m.MethodKind == MethodKind.Constructor) {
						symbol = m.ContainingSymbol;
						goto case SymbolKind.NamedType;
					}
					break;
				case SymbolKind.Property:
					if ((t = symbol.ContainingType).IsAnonymousType) {
						Add(ref types, t);
					}
					break;
				default: return;
			}
			if (types != null) {
				ShowAnonymousTypes(container, types, symbol);
			}

			void ShowAnonymousTypes(QiContainer c, List<ITypeSymbol> anonymousTypes, ISymbol currentSymbol) {
				const string AnonymousNumbers = "abcdefghijklmnopqrstuvwxyz";
				var d = new ThemedTipDocument().AppendTitle(KnownImageIds.UserDataType, "Anonymous type: ");
				for (var i = 0; i < anonymousTypes.Count; i++) {
					var type = anonymousTypes[i];
					var content = new ThemedTipText()
						.AddSymbol(type, "'" + AnonymousNumbers[i], _SymbolFormatter)
						.Append(" is { ");
					foreach (var m in type.GetMembers()) {
						if (m.Kind != SymbolKind.Property) {
							continue;
						}
						var pt = m.GetReturnType() as ITypeSymbol;
						string alias = null;
						if (pt != null && pt.IsAnonymousType) {
							Add(ref anonymousTypes, pt);
							alias = "'" + AnonymousNumbers[anonymousTypes.IndexOf(pt)];
						}
						content.AddSymbol(pt, alias, _SymbolFormatter)
							.Append(" ")
							.AddSymbol(m, m == currentSymbol, _SymbolFormatter)
							.Append(", ");
					}
					var run = content.Inlines.LastInline as System.Windows.Documents.Run;
					if (run.Text == ", ") {
						run.Text = " }";
					}
					else {
						run.Text += "}";
					}
					d.Append(new ThemedTipParagraph(content));
				}
				c.Overrider?.OverrideAnonymousTypeInfo(d);
				c.Insert(0, d);
			}
			void Add(ref List<ITypeSymbol> list, ITypeSymbol type) {
				if ((list ?? (list = new List<ITypeSymbol>())).Contains(type) == false) {
					list.Add(type);
				}
				if (type.ContainingType?.IsAnonymousType == true) {
					Add(ref list, type);
				}
			}
		}

		static TextBlock ToUIText(ISymbol symbol) {
			return new ThemedTipText().AddSymbolDisplayParts(symbol.ToDisplayParts(WpfHelper.QuickInfoSymbolDisplayFormat), _SymbolFormatter, -1);
		}

		public void Dispose() {
		}
	}

	sealed class QiContainer
	{
		readonly List<object> _List = new List<object>();
		public readonly IQuickInfoOverrider Overrider;

		public QiContainer(IQuickInfoOverrider overrider) {
			Overrider = overrider;
		}

		public int Count => _List.Count;

		public void Insert(int index, object item) {
			if (item != null) {
				_List.Insert(index, item);
			}
		}
		public void Add(object item) {
			if (item != null) {
				_List.Add(item);
			}
		}

		public StackPanel ToUI() {
			var s = new StackPanel();
			foreach (var item in _List) {
				if (item is UIElement u) {
					s.Children.Add(u);
				}
				else if (item is string t) {
					s.Children.Add(new ThemedTipText(t));
				}
			}
			return s;
		}
	}
}
