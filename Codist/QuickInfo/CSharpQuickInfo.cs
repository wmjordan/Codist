using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AppHelpers;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using R = Codist.Properties.Resources;

namespace Codist.QuickInfo
{
	sealed class CSharpQuickInfo : IAsyncQuickInfoSource
	{
		internal const string Name = nameof(CSharpQuickInfo);

		static readonly SymbolFormatter _SymbolFormatter = SymbolFormatter.Instance;

		readonly bool _IsVsProject;
		ITextBuffer _TextBuffer;
		bool _isCandidate;
		int _Ref;

		public CSharpQuickInfo(ITextBuffer subjectBuffer) {
			ThreadHelper.ThrowIfNotOnUIThread();
			_TextBuffer = subjectBuffer;
			_IsVsProject = TextEditorHelper.IsVsixProject();
		}

		public CSharpQuickInfo Reference() {
			++_Ref;
			return this;
		}

		public async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
			if (QuickInfoOverrider.CheckCtrlSuppression()) {
				return null;
			}
			// Map the trigger point down to our buffer.
			// It is weird that the session.TextView.TextBuffer != _TextBuffer and we can't get a Workspace from the former one
			var buffer = _TextBuffer;
			return buffer == null ? null : await InternalGetQuickInfoItemAsync(session, buffer, cancellationToken).ConfigureAwait(false);
		}

		async Task<QuickInfoItem> InternalGetQuickInfoItemAsync(IAsyncQuickInfoSession session, ITextBuffer buffer, CancellationToken cancellationToken) {
			ISymbol symbol;
			SyntaxNode node;
			ImmutableArray<ISymbol> candidates;
			SyntaxToken token;
			var qiWrapper = Config.Instance.QuickInfoOptions.HasAnyFlag(QuickInfoOptions.QuickInfoOverride)
				? QuickInfoOverrider.CreateOverrider(session)
				: null;
			var qiContent = new QiContainer(qiWrapper);
			var currentSnapshot = buffer.CurrentSnapshot;
			var subjectTriggerPoint = session.GetTriggerPoint(currentSnapshot).GetValueOrDefault();
			if (subjectTriggerPoint.Snapshot == null) {
				return null;
			}

			var doc = currentSnapshot.GetOpenDocumentInCurrentContextWithChanges();
			if (doc == null) {
				return null;
			}
			var semanticModel = await doc.GetSemanticModelAsync(cancellationToken);
			if (semanticModel == null) {
				return null;
			}
			if (qiWrapper != null) {
				qiWrapper.OverrideBuiltInXmlDoc = Config.Instance.QuickInfoOptions.HasAnyFlag(QuickInfoOptions.DocumentationOverride);
			}
			var unitCompilation = semanticModel.SyntaxTree.GetCompilationUnitRoot(cancellationToken);

			//look for occurrences of our QuickInfo words in the span
			token = unitCompilation.FindToken(subjectTriggerPoint, true);
			var skipTriggerPointCheck = false;
			symbol = null;
			switch (token.Kind()) {
				case SyntaxKind.WhitespaceTrivia:
				case SyntaxKind.SingleLineCommentTrivia:
				case SyntaxKind.MultiLineCommentTrivia:
					return null;
				case SyntaxKind.OpenBraceToken:
				case SyntaxKind.CloseBraceToken:
					if (qiWrapper != null) {
						qiWrapper.OverrideBuiltInXmlDoc = false;
					}
					break;
				case SyntaxKind.ThisKeyword: // convert to type below
				case SyntaxKind.BaseKeyword:
				case SyntaxKind.OverrideKeyword:
					break;
				case SyntaxKind.TrueKeyword:
				case SyntaxKind.FalseKeyword:
				case SyntaxKind.IsKeyword:
				case SyntaxKind.AmpersandAmpersandToken:
				case SyntaxKind.BarBarToken:
					symbol = semanticModel.GetSystemTypeSymbol(nameof(Boolean));
					break;
				case SyntaxKind.EqualsGreaterThanToken:
					if ((node = unitCompilation.FindNode(token.Span)).IsKind(CodeAnalysisHelper.SwitchExpressionArm) && node.Parent.IsKind(CodeAnalysisHelper.SwitchExpression)) {
						symbol = semanticModel.GetTypeInfo(node.Parent).ConvertedType;
					}
					break;
				case SyntaxKind.EqualsToken:
					symbol = semanticModel.GetTypeInfo(unitCompilation.FindNode(token.GetPreviousToken().Span)).ConvertedType;
					break;
				case SyntaxKind.NullKeyword:
				case SyntaxKind.NewKeyword:
				case SyntaxKind.DefaultKeyword:
				case SyntaxKind.SwitchKeyword:
				case SyntaxKind.QuestionToken:
				case SyntaxKind.QuestionQuestionToken:
				case CodeAnalysisHelper.QuestionQuestionEqualsToken:
				case SyntaxKind.UnderscoreToken:
				case SyntaxKind.WhereKeyword:
				case SyntaxKind.OrderByKeyword:
					symbol = semanticModel.GetTypeInfo(unitCompilation.FindNode(token.Span)).ConvertedType;
					if (symbol == null) {
						if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter)) {
							break;
						}
						return null;
					}
					break;
				case SyntaxKind.AsKeyword:
					var asType = (unitCompilation.FindNode(token.Span) as BinaryExpressionSyntax)?.GetLastIdentifier();
					if (asType != null) {
						token = asType.Identifier;
						skipTriggerPointCheck = true;
					}
					break;
				case SyntaxKind.ReturnKeyword:
					var tb = ShowReturnInfo(unitCompilation.FindNode(token.Span) as ReturnStatementSyntax, semanticModel, cancellationToken);
					return tb != null ? CreateQuickInfoItem(session, token, tb) : null;
				case SyntaxKind.AwaitKeyword:
					node = (unitCompilation.FindNode(token.Span, false, true) as AwaitExpressionSyntax)?.Expression;
					goto PROCESS;
				case SyntaxKind.DotToken:
					token = token.GetNextToken();
					skipTriggerPointCheck = true;
					break;
				case SyntaxKind.OpenParenToken:
				case SyntaxKind.CloseParenToken:
				case SyntaxKind.CommaToken:
				case SyntaxKind.ColonToken:
				case SyntaxKind.SemicolonToken:
				case SyntaxKind.OpenBracketToken:
				case SyntaxKind.CloseBracketToken:
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
					qiContent.Add(new ThemedTipText(R.T_EndOfRegion)
						.SetGlyph(ThemeHelper.GetImage(IconIds.Region))
						.Append((unitCompilation.FindNode(token.Span, true, false) as EndRegionDirectiveTriviaSyntax).GetRegion()?.GetDeclarationSignature(), true)
						);
					return CreateQuickInfoItem(session, token, qiContent.ToUI());
				case SyntaxKind.VoidKeyword:
					return null;
				case SyntaxKind.TypeOfKeyword:
					symbol = semanticModel.GetSystemTypeSymbol(nameof(Type));
					break;
				case SyntaxKind.StackAllocKeyword:
					symbol = semanticModel.GetTypeInfo(unitCompilation.FindNode(token.Span), cancellationToken).Type;
					break;
				case CodeAnalysisHelper.DotDotToken:
					symbol = semanticModel.GetSystemTypeSymbol(nameof(Int32));
					break;
				default:
					if (token.Kind().IsPredefinedSystemType()) {
						symbol = semanticModel.GetSystemTypeSymbol(token.Kind());
						break;
					}
					if (token.Span.Contains(subjectTriggerPoint, true) == false
						|| token.IsReservedKeyword()) {
						node = unitCompilation.FindNode(token.Span, false, false);
						if (node is StatementSyntax) {
							ShowBlockInfo(qiContent, currentSnapshot, node, semanticModel);
						}
						if (qiContent.Count > 0) {
							return CreateQuickInfoItem(session, token, qiContent.ToUI());
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

			ObjectCreationExpressionSyntax ctor;
		PROCESS:
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter)) {
				ShowParameterInfo(qiContent, node, semanticModel);
			}
			if (symbol == null) {
				symbol = token.IsKind(SyntaxKind.CloseBraceToken) ? null
				: GetSymbol(semanticModel, node, ref candidates, cancellationToken);
			}
			if (token.IsKind(SyntaxKind.AwaitKeyword)
				&& symbol != null && symbol.Kind == SymbolKind.Method) {
				symbol = (symbol.GetReturnType() as INamedTypeSymbol).TypeArguments.FirstOrDefault();
			}
			if (_isCandidate = candidates.IsDefaultOrEmpty == false) {
				ShowCandidateInfo(qiContent, candidates);
			}
			if (symbol == null) {
				switch (token.Kind()) {
					case SyntaxKind.StringLiteralToken:
					case SyntaxKind.InterpolatedStringStartToken:
					case SyntaxKind.InterpolatedStringEndToken:
					case SyntaxKind.InterpolatedVerbatimStringStartToken:
					case SyntaxKind.InterpolatedStringToken:
					case SyntaxKind.InterpolatedStringTextToken:
					case SyntaxKind.NameOfKeyword:
						symbol = semanticModel.GetSystemTypeSymbol(nameof(String));
						break;
					case SyntaxKind.CharacterLiteralToken:
						symbol = semanticModel.GetSystemTypeSymbol(nameof(Char));
						if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues)
						&& token.Span.Length >= 8) {
							qiContent.Add(new ThemedTipText(token.ValueText) { FontSize = ThemeHelper.ToolTipFontSize * 2 });
						}
						else if (node.IsKind(SyntaxKind.Block) || node.IsKind(SyntaxKind.SwitchStatement)) {
							ShowBlockInfo(qiContent, currentSnapshot, node, semanticModel);
						}
						break;
					case SyntaxKind.NumericLiteralToken:
						symbol = semanticModel.GetSystemTypeSymbol(token.Value.GetType().Name);
						break;
					default:
						if (node.IsKind(SyntaxKind.Block) || node.IsKind(SyntaxKind.SwitchStatement)) {
							ShowBlockInfo(qiContent, currentSnapshot, node, semanticModel);
						}
						break;
				}
				ShowMiscInfo(qiContent, node);
				if (symbol == null) {
					goto RETURN;
				}
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation)) {
				qiContent.Add(await ShowAvailabilityAsync(doc, token, cancellationToken).ConfigureAwait(false));
				ctor = node.Parent as ObjectCreationExpressionSyntax;
				OverrideDocumentation(node, qiWrapper,
					ctor?.Type == node ? semanticModel.GetSymbolInfo(ctor, cancellationToken).Symbol ?? symbol
						//: node.Parent.IsKind(SyntaxKind.Attribute) ? symbol.ContainingType
						: symbol,
					semanticModel);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Attributes)) {
				ShowAttributesInfo(qiContent, symbol);
			}
			ShowSymbolInfo(session, qiContent, node, symbol, semanticModel);
		RETURN:
			ctor = node.Parent as ObjectCreationExpressionSyntax;
			if (ctor != null && ctor.Type == node) {
				symbol = semanticModel.GetSymbolOrFirstCandidate(ctor, cancellationToken) ?? symbol;
				if (symbol == null) {
					return null;
				}
				if (symbol.IsImplicitlyDeclared) {
					symbol = symbol.ContainingType;
				}
			}
			if (qiWrapper != null) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Diagnostics)) {
					qiWrapper.SetDiagnostics(semanticModel.GetDiagnostics(token.Span, cancellationToken));
				}
				qiWrapper.ApplyClickAndGo(symbol, buffer, session);
			}
			return CreateQuickInfoItem(session, (qiContent.Count > 0 || symbol != null && Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle)) && session.TextView.TextSnapshot == currentSnapshot
				? token
				: (SyntaxToken?)null, qiContent.ToUI());
		}

		static QuickInfoItem CreateQuickInfoItem(IAsyncQuickInfoSession session, SyntaxToken? token, object item) {
			session.KeepViewPosition();
			return new QuickInfoItem(token?.Span.CreateSnapshotSpan(session.TextView.TextSnapshot).ToTrackingSpan(), item);
		}

		static async Task<ThemedTipDocument> ShowAvailabilityAsync(Document doc, SyntaxToken token, CancellationToken cancellationToken) {
			ThemedTipDocument r = null;
			var solution = doc.Project.Solution;
			if (solution.ProjectIds.Count > 0) {
				var linkedDocuments = doc.GetLinkedDocumentIds();
				if (linkedDocuments.Length > 0) {
					ImmutableArray<ISymbol> candidates;
					foreach (var id in linkedDocuments) {
						var d = solution.GetDocument(id);
						var sm = await d.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
						if (sm.IsCSharp() == false) {
							continue;
						}
						if (GetSymbol(sm, sm.SyntaxTree.GetCompilationUnitRoot(cancellationToken).FindNode(token.Span, true, true), ref candidates, cancellationToken) == null) {
							if (r == null) {
								r = new ThemedTipDocument().AppendTitle(IconIds.UnavailableSymbol, R.T_SymbolUnavailableIn);
							}
							r.Append(new ThemedTipParagraph(IconIds.Project, new ThemedTipText(d.Project.Name)));
						}
					}
				}
			}
			return r;
		}

		static ISymbol GetSymbol(SemanticModel semanticModel, SyntaxNode node, ref ImmutableArray<ISymbol> candidates, CancellationToken cancellationToken) {
			if (node.IsKind(SyntaxKind.BaseExpression)
				|| node.IsKind(SyntaxKind.DefaultLiteralExpression)) {
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
			SyntaxKind kind;
			return symbolInfo.Symbol
				?? ((kind = node.Kind()).IsDeclaration()
						|| kind == SyntaxKind.VariableDeclarator
						|| kind == SyntaxKind.SingleVariableDesignation && (node.Parent.IsKind(SyntaxKind.DeclarationExpression)
							|| node.Parent.IsKind(SyntaxKind.DeclarationPattern)
							|| node.Parent.IsKind(SyntaxKind.ParenthesizedVariableDesignation))
					? semanticModel.GetDeclaredSymbol(node, cancellationToken)
					// : kind == SyntaxKind.ArrowExpressionClause
					// ? semanticModel.GetDeclaredSymbol(node.Parent, cancellationToken)
					: kind == SyntaxKind.IdentifierName && node.Parent.IsKind(SyntaxKind.NameEquals) && (node = node.Parent.Parent) != null && node.IsKind(SyntaxKind.UsingDirective)
					? semanticModel.GetDeclaredSymbol(node, cancellationToken)?.GetAliasTarget()
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

		static ThemedTipDocument OverrideDocumentation(SyntaxNode node, IQuickInfoOverrider qiWrapper, ISymbol symbol, SemanticModel semanticModel) {
			if (symbol == null) {
				return null;
			}
			if (symbol.Kind == SymbolKind.Method && (symbol as IMethodSymbol)?.IsAccessor() == true) {
				// hack: symbol could be Microsoft.CodeAnalysis.CSharp.Symbols.SourceMemberFieldSymbolFromDeclarator which is not IMethodSymbol
				symbol = symbol.ContainingSymbol;
			}
			symbol = symbol.GetAliasTarget();
			var compilation = semanticModel.Compilation;
			var doc = new XmlDoc(symbol, compilation);
			var docRenderer = new XmlDocRenderer(compilation, SymbolFormatter.Instance);
			var tip = docRenderer.RenderXmlDoc(symbol, doc);

			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ExceptionDoc)
				&& (symbol.Kind == SymbolKind.Method || symbol.Kind == SymbolKind.Property || symbol.Kind == SymbolKind.Event)) {
				var exceptions = doc.Exceptions ?? doc.ExplicitInheritDoc?.Exceptions ?? doc.InheritedXmlDocs.FirstOrDefault(i => i.Exceptions != null)?.Exceptions;
				if (exceptions != null) {
					var p = new ThemedTipParagraph(IconIds.ExceptionXmlDoc, new ThemedTipText(R.T_Exception, true)
					   .Append(exceptions == doc.Exceptions ? ": " : (R.T_Inherited + ": ")));
					foreach (var ex in exceptions) {
						var et = ex.Attribute("cref");
						if (et != null) {
							docRenderer.RenderXmlDocSymbol(et.Value, p.Content.AppendLine().Inlines, SymbolKind.NamedType);
							p.Content.Inlines.LastInline.FontWeight = FontWeights.Bold;
							docRenderer.Render(ex, p.Content.Append(": ").Inlines);
						}
					}
					qiWrapper.OverrideException(new ThemedTipDocument().Append(p));
				}
			}

			// show type XML Doc for constructors
			if ((symbol as IMethodSymbol)?.MethodKind == MethodKind.Constructor) {
				symbol = symbol.ContainingType;
				var summary = new XmlDoc(symbol, compilation)
					.GetDescription(symbol);
				if (summary != null) {
					tip.Append(new ThemedTipParagraph(IconIds.ReferencedXmlDoc, new ThemedTipText(R.T_DocumentationFrom).AddSymbol(symbol.OriginalDefinition, true, SymbolFormatter.Instance).Append(":")));
					new XmlDocRenderer(compilation, SymbolFormatter.Instance)
						.Render(summary, tip, false);
				}
			}

			ShowCapturedVariables(node, symbol, semanticModel, tip);

			if (tip.ParagraphCount > 0) {
				qiWrapper.OverrideDocumentation(tip);
			}
			return tip;
		}

		static void ShowCapturedVariables(SyntaxNode node, ISymbol symbol, SemanticModel semanticModel, ThemedTipDocument tip) {
			if (node is LambdaExpressionSyntax
				|| (symbol as IMethodSymbol)?.MethodKind == MethodKind.LocalFunction) {
				var ss = node is LambdaExpressionSyntax
					? node.AncestorsAndSelf().FirstOrDefault(i => i is StatementSyntax || i is ExpressionSyntax && i.Kind() != SyntaxKind.IdentifierName)
					: symbol.GetSyntaxNode();
				if (ss != null) {
					var df = semanticModel.AnalyzeDataFlow(ss);
					var captured = df.ReadInside.RemoveAll(i => df.VariablesDeclared.Contains(i) || (i as ILocalSymbol)?.IsConst == true);
					if (captured.Length > 0) {
						var p = new ThemedTipParagraph(IconIds.ReadVariables, new ThemedTipText().Append(R.T_CapturedVariables, true));
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
			var info = new ThemedTipDocument().AppendTitle(IconIds.SymbolCandidate, R.T_Maybe);
			foreach (var item in candidates) {
				info.Append(new ThemedTipParagraph(item.GetImageId(), ToUIText(item.OriginalDefinition)));
			}
			qiContent.Add(info);
		}

		void ShowSymbolInfo(IAsyncQuickInfoSession session, QiContainer qiContent, SyntaxNode node, ISymbol symbol, SemanticModel semanticModel) {
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
					ShowMethodInfo(qiContent, node, m, semanticModel);
					if (node.Parent.IsKind(SyntaxKind.Attribute)
						|| node.Parent.Parent.IsKind(SyntaxKind.Attribute) // qualified attribute annotation
						) {
						if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Attributes)) {
							ShowAttributesInfo(qiContent, symbol.ContainingType);
						}
						ShowTypeInfo(qiContent, node.Parent, symbol.ContainingType, semanticModel);
					}
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color)
						&& m.ContainingType?.Name == "Color"
						&& session.Mark(nameof(ColorQuickInfoUI))) {
						qiContent.Add(ColorQuickInfoUI.PreviewColorMethodInvocation(semanticModel, node, symbol as IMethodSymbol));
					}
					if (m.MethodKind == MethodKind.BuiltinOperator && node is ExpressionSyntax) {
						var value = semanticModel.GetConstantValue(node);
						if (value.HasValue) {
							ShowConstInfo(qiContent, null, value.Value);
						}
					}
					break;
				case SymbolKind.NamedType:
					ShowTypeInfo(qiContent, node, symbol as INamedTypeSymbol, semanticModel);
					break;
				case SymbolKind.Property:
					ShowPropertyInfo(qiContent, symbol as IPropertySymbol);
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color)
						&& session.Mark(nameof(ColorQuickInfoUI))) {
						qiContent.Add(ColorQuickInfoUI.PreviewColorProperty(symbol as IPropertySymbol, _IsVsProject));
					}
					break;
				case SymbolKind.Namespace:
					ShowNamespaceInfo(qiContent, symbol as INamespaceSymbol);
					break;
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.SymbolLocation)) {
				ShowSymbolLocationInfo(qiContent, symbol);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
				&& (node.Parent.IsKind(SyntaxKind.Argument) == false || Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter) == false) /*the signature has already been displayed there*/) {
				var st = symbol.GetReturnType();
				if (st != null && st.TypeKind == TypeKind.Delegate) {
					var invoke = ((INamedTypeSymbol)st).DelegateInvokeMethod;
					qiContent.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(IconIds.Delegate,
						new ThemedTipText(R.T_DelegateSignature, true).Append(": ")
							.AddSymbol(invoke.ReturnType, false, _SymbolFormatter)
							.Append(" ")
							.AddParameters(invoke.Parameters, _SymbolFormatter)
						)));
				}
			}

		}

		static void ShowSymbolLocationInfo(QiContainer qiContent, ISymbol symbol) {
			string asmName = symbol.GetAssemblyModuleName();
			if (asmName != null) {
				var item = new ThemedTipDocument()
					.AppendParagraph(IconIds.Module, new ThemedTipText(R.T_Assembly, true).Append(asmName));
				switch (symbol.Kind) {
					case SymbolKind.Field:
					case SymbolKind.Property:
					case SymbolKind.Event:
					case SymbolKind.Method:
					case SymbolKind.NamedType:
						var ns = symbol.ContainingNamespace;
						if (ns != null) {
							var t = new ThemedTipText(R.T_Namespace, true);
							_SymbolFormatter.ShowContainingNamespace(symbol, t);
							item.AppendParagraph(IconIds.Namespace, t);
						}
						break;
				}
				qiContent.Add(item);
			}
		}

		static void ShowBlockInfo(QiContainer qiContent, ITextSnapshot textSnapshot, SyntaxNode node, SemanticModel semanticModel) {
			var lines = textSnapshot.GetLineSpan(node.Span).Length + 1;
			if (lines > 100) {
				qiContent.Add(new ThemedTipText(lines + R.T_Lines, true));
			}
			else if (lines > 1) {
				qiContent.Add(lines + R.T_Lines);
			}
			var df = semanticModel.AnalyzeDataFlow(node);
			var vd = df.VariablesDeclared;
			if (vd.IsEmpty == false) {
				var p = new ThemedTipText(R.T_DeclaredVariable, true).Append(vd.Length).AppendLine();
				var s = false;
				foreach (var item in vd) {
					if (s) {
						p.Append(", ");
					}
					p.AddSymbol(item, false, _SymbolFormatter.Local);
					s = true;
				}
				qiContent.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(IconIds.LocalVariable, p)));
			}
			vd = df.DataFlowsIn;
			if (vd.IsEmpty == false) {
				var p = new ThemedTipText(R.T_ReadVariable, true).Append(vd.Length).AppendLine();
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
				qiContent.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(IconIds.ReadVariables, p)));
			}
		}
		static void ShowMiscInfo(QiContainer qiContent, SyntaxNode node) {
			Grid infoBox = null;
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
				else if (s == 1) {
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

		static ThemedTipText ShowReturnInfo(ReturnStatementSyntax returns, SemanticModel semanticModel, CancellationToken cancellationToken) {
			if (returns == null) {
				return null;
			}
			SyntaxNode node = returns;
			var method = returns.Expression != null
				? semanticModel.GetSymbolInfo(returns.Expression, cancellationToken).Symbol as IMethodSymbol
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
				var symbol = semanticModel.GetSymbolInfo(node, cancellationToken).Symbol ?? semanticModel.GetDeclaredSymbol(node, cancellationToken);
				var t = new ThemedTipText();
				t.SetGlyph(ThemeHelper.GetImage(IconIds.ReturnValue));
				if (method != null) {
					if (method.MethodKind == MethodKind.AnonymousFunction) {
						t.Append(R.T_ReturnAnonymousFunction);
					}
					else {
						t.Append(R.T_Return)
							.AddSymbol(method.GetReturnType(), false, _SymbolFormatter)
							.Append(R.T_ReturnFor);
					}
				}
				else {
					t.Append(R.T_Return)
						.AddSymbol(symbol?.GetReturnType(), false, _SymbolFormatter)
						.Append(R.T_ReturnFor);
				}
				if (symbol != null) {
					t.AddSymbol(symbol, node is LambdaExpressionSyntax ? R.T_LambdaExpression + name : null, _SymbolFormatter);
				}
				else {
					t.Append(name);
				}
				return t;
			}
			return null;
		}

		static void ShowAttributesInfo(QiContainer qiContent, ISymbol symbol) {
			// todo: show inherited attributes
			var p = ListAttributes(null, symbol.GetAttributes(), 0);
			if (symbol.Kind == SymbolKind.Method) {
				p = ListAttributes(p, ((IMethodSymbol)symbol).GetReturnTypeAttributes(), 1);
			}
			else if (symbol.Kind == SymbolKind.Property) {
				p = ListAttributes(p, ((IPropertySymbol)symbol).GetPropertyBackingField()?.GetAttributes() ?? ImmutableArray<AttributeData>.Empty, 2);
			}
			if (p != null) {
				qiContent.Add(new ThemedTipDocument().Append(p));
			}

			ThemedTipParagraph ListAttributes(ThemedTipParagraph paragraph, ImmutableArray<AttributeData> attributes, byte attrType) {
				if (attributes.Length > 0) {
					foreach (var item in attributes) {
						if (item.AttributeClass.IsAccessible(true)) {
							if (paragraph == null) {
								paragraph = new ThemedTipParagraph(IconIds.Attribute, new ThemedTipText().Append(R.T_Attribute, true));
							}
							_SymbolFormatter.Format(paragraph.Content.AppendLine().Inlines, item, attrType);
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
				&& Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle) == false
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
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle) == false
					&& (ev.DeclaredAccessibility != Accessibility.Public || ev.IsAbstract || ev.IsStatic || ev.IsOverride || ev.IsVirtual)
					&& ev.ContainingType?.TypeKind != TypeKind.Interface) {
					ShowDeclarationModifier(qiContent, ev);
				}
				var invoke = ev.Type.GetMembers("Invoke").FirstOrDefault() as IMethodSymbol;
				if (invoke != null && invoke.Parameters.Length == 2) {
					qiContent.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(IconIds.Event,
						new ThemedTipText(R.T_EventSignature, true).AddParameters(invoke.Parameters, _SymbolFormatter)
						)));
				}
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfaceImplementations)) {
				ShowInterfaceImplementation(qiContent, ev, ev.ExplicitInterfaceImplementations);
			}
		}

		void ShowFieldInfo(QiContainer qiContent, IFieldSymbol field) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
				&& Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle) == false
				&& (field.DeclaredAccessibility != Accessibility.Public || field.IsReadOnly || field.IsVolatile || field.IsStatic)
				&& field.ContainingType.TypeKind != TypeKind.Enum) {
				ShowDeclarationModifier(qiContent, field);
			}
			if (field.HasConstantValue) {
				if (_IsVsProject && field.ConstantValue is int) {
					ShowKnownImageId(qiContent, field, (int)field.ConstantValue);
				}
				ShowConstInfo(qiContent, field, field.ConstantValue);
			}
			else if (field.IsReadOnly && field.IsStatic && field.ContainingType.Name == nameof(System.Reflection.Emit.OpCodes)) {
				qiContent.ShowOpCodeInfo(field);
			}

			void ShowKnownImageId(QiContainer qc, IFieldSymbol f, int fieldValue) {
				var t = f.ContainingType;
				if (t.MatchTypeName(nameof(Microsoft.VisualStudio.Imaging.KnownImageIds), "Imaging", "VisualStudio", "Microsoft")
					|| t.MatchTypeName(nameof(IconIds), nameof(Codist))) {
					qc.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(fieldValue, new ThemedTipText(field.Name))));
				}
			}
		}

		void ShowMethodInfo(QiContainer qiContent, SyntaxNode node, IMethodSymbol method, SemanticModel semanticModel) {
			var options = Config.Instance.QuickInfoOptions;
			if (options.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation)) {
				ShowAnonymousTypeInfo(qiContent, method);
			}
			if (options.MatchFlags(QuickInfoOptions.Declaration)
				&& Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle) == false
				&& method.ContainingType?.TypeKind != TypeKind.Interface
				&& (method.DeclaredAccessibility != Accessibility.Public || method.IsAbstract || method.IsStatic || method.IsVirtual || method.IsOverride || method.IsExtern || method.IsSealed)) {
				ShowDeclarationModifier(qiContent, method);
			}
			if (options.MatchFlags(QuickInfoOptions.TypeParameters)
				&& options.MatchFlags(QuickInfoOptions.AlternativeStyle) == false
				&& method.IsGenericMethod
				&& method.TypeArguments.Length > 0
				&& method.TypeParameters[0] != method.TypeArguments[0]) {
				ShowTypeArguments(qiContent, method.TypeArguments, method.TypeParameters);
			}
			if (options.MatchFlags(QuickInfoOptions.InterfaceImplementations)) {
				ShowInterfaceImplementation(qiContent, method, method.ExplicitInterfaceImplementations);
			}
			if (options.MatchFlags(QuickInfoOptions.SymbolLocation)
				&& method.IsExtensionMethod
				&& options.MatchFlags(QuickInfoOptions.AlternativeStyle) == false) {
				ShowExtensionMethod(qiContent, method);
			}
			if (options.MatchFlags(QuickInfoOptions.MethodOverload)) {
				ShowOverloadsInfo(qiContent, node, method, semanticModel);
			}
		}

		void ShowOverloadsInfo(QiContainer qiContent, SyntaxNode node, IMethodSymbol method, SemanticModel semanticModel) {
			if (_isCandidate) {
				return;
			}
			var overloads = node.Kind() == SyntaxKind.MethodDeclaration || node.Kind() == SyntaxKind.ConstructorDeclaration
				? method.ContainingType.GetMembers(method.Name)
				: semanticModel.GetMemberGroup(node);
			if (overloads.Length < 2) {
				return;
			}
			var re = method.MethodKind == MethodKind.ReducedExtension;
			method = method.OriginalDefinition;
			if (re) {
				method = method.ReducedFrom;
			}
			var mst = method.IsStatic;
			var mmod = method.GetSpecialMethodModifier();
			var rt = method.ReturnType;
			var mps = method.Parameters;
			var ct = method.ContainingType;
			var overloadInfo = new ThemedTipDocument().AppendTitle(IconIds.MethodOverloads, R.T_MethodOverload);
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
				var st = om.IsStatic;
				if (st) {
					t.Append("static ".Render((st == mst ? SymbolFormatter.SemiTransparent : SymbolFormatter.Instance).Keyword));
				}
				var mod = om.GetSpecialMethodModifier();
				if (mod != null) {
					t.Append(mod.Render((mod == mmod ? SymbolFormatter.SemiTransparent : SymbolFormatter.Instance).Keyword));
				}
				if (om.MethodKind != MethodKind.Constructor) {
					t.AddSymbol(om.ReturnType, false, CodeAnalysisHelper.AreEqual(om.ReturnType, rt, false) ? SymbolFormatter.SemiTransparent : _SymbolFormatter).Append(" ");
				}
				if (ore) {
					t.AddSymbol(om.ReceiverType, "this", (om.ContainingType != ct ? _SymbolFormatter : SymbolFormatter.SemiTransparent).Keyword).Append(".", SymbolFormatter.SemiTransparent.PlainText);
				}
				else if (om.ContainingType != ct) {
					t.AddSymbol(om.ContainingType, false, _SymbolFormatter).Append(".", SymbolFormatter.SemiTransparent.PlainText);
				}
				t.AddSymbol(om, true, SymbolFormatter.SemiTransparent);
				t.Append("(", SymbolFormatter.SemiTransparent.PlainText);
				foreach (var op in om.Parameters) {
					var mp = mps.FirstOrDefault(p => p.Name == op.Name);
					if (op.Ordinal == 0) {
						if (ore == false && om.IsExtensionMethod) {
							t.Append("this ", _SymbolFormatter.Keyword);
						}
					}
					else {
						t.Append(", ", SymbolFormatter.SemiTransparent.PlainText);
					}
					if (mp != null) {
						if (mp.RefKind != op.RefKind
							|| CodeAnalysisHelper.AreEqual(mp.Type, op.Type, false) == false
							|| mp.IsParams != op.IsParams
							|| mp.IsOptional != op.IsOptional
							|| mp.HasExplicitDefaultValue != op.HasExplicitDefaultValue) {
							mp = null;
						}
					}
					t.AddSymbolDisplayParts(op.ToDisplayParts(CodeAnalysisHelper.InTypeOverloadDisplayFormat), mp == null ? _SymbolFormatter : SymbolFormatter.SemiTransparent, -1);
				}
				t.Append(")", SymbolFormatter.SemiTransparent.PlainText);
				overloadInfo.Append(new ThemedTipParagraph(overload.GetImageId(), t));
			}
			if (overloadInfo.ParagraphCount > 1) {
				qiContent.Add(overloadInfo);
			}
		}

		static void ShowTypeArguments(QiContainer qiContent, ImmutableArray<ITypeSymbol> args, ImmutableArray<ITypeParameterSymbol> typeParams) {
			var info = new ThemedTipDocument();
			var l = args.Length;
			var content = new ThemedTipText(R.T_TypeArgument, true);
			info.Append(new ThemedTipParagraph(IconIds.GenericDefinition, content));
			for (int i = 0; i < l; i++) {
				_SymbolFormatter.ShowTypeArgumentInfo(typeParams[i], args[i], content.AppendLine());
			}
			qiContent.Add(info);
		}

		static void ShowNamespaceInfo(QiContainer qiContent, INamespaceSymbol nsSymbol) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NamespaceTypes) == false) {
				return;
			}
			var namespaces = nsSymbol.GetNamespaceMembers().ToImmutableArray().Sort(Comparer<INamespaceSymbol>.Create((x, y) => String.CompareOrdinal(x.Name, y.Name)));
			if (namespaces.Length > 0) {
				var info = new ThemedTipDocument().AppendTitle(IconIds.Namespace, R.T_Namespace);
				foreach (var ns in namespaces) {
					info.Append(new ThemedTipParagraph(IconIds.Namespace, new ThemedTipText().Append(ns.Name, _SymbolFormatter.Namespace)));
				}
				qiContent.Add(info);
			}

			var members = nsSymbol.GetTypeMembers().Sort(Comparer<INamedTypeSymbol>.Create((x, y) => String.Compare(x.Name, y.Name)));
			if (members.Length > 0) {
				var info = new StackPanel().Add(new ThemedTipText(R.T_Type, true));
				foreach (var type in members) {
					var t = new ThemedTipText().SetGlyph(ThemeHelper.GetImage(type.GetImageId()));
					_SymbolFormatter.ShowSymbolDeclaration(type, t, true, true);
					t.AddSymbol(type, false, _SymbolFormatter);
					info.Add(t);
				}
				qiContent.Add(info.Scrollable());
			}
		}

		void ShowTypeInfo(QiContainer qiContent, SyntaxNode node, INamedTypeSymbol typeSymbol, SemanticModel semanticModel) {
			var options = Config.Instance.QuickInfoOptions;
			if (options.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation) && typeSymbol.TypeKind == TypeKind.Class) {
				ShowAnonymousTypeInfo(qiContent, typeSymbol);
			}
			if (options.MatchFlags(QuickInfoOptions.TypeParameters)
				&& options.MatchFlags(QuickInfoOptions.AlternativeStyle) == false
				&& typeSymbol.IsGenericType
				&& typeSymbol.TypeArguments.Length > 0
				&& typeSymbol.TypeParameters[0] != typeSymbol.TypeArguments[0]) {
				ShowTypeArguments(qiContent, typeSymbol.TypeArguments, typeSymbol.TypeParameters);
			}
			if (options.MatchFlags(QuickInfoOptions.MethodOverload)) {
				node = node.GetObjectCreationNode();
				if (node != null) {
					var method = semanticModel.GetSymbolOrFirstCandidate(node) as IMethodSymbol;
					if (method != null) {
						ShowOverloadsInfo(qiContent, node, method, semanticModel);
					}
				}
			}
			if (options.MatchFlags(QuickInfoOptions.Declaration)
				&& Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle) == false
				&& (typeSymbol.DeclaredAccessibility != Accessibility.Public
					|| typeSymbol.IsStatic
					|| typeSymbol.IsReadOnly()
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
			if (value is string sv) {
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
			var implementedIntfs = ImmutableArray.CreateBuilder<ITypeSymbol>(3);
			ThemedTipDocument info = null;
			var returnType = symbol.GetReturnType();
			var parameters = symbol.GetParameters();
			var typeParams = symbol.GetTypeParameters();
			foreach (var intf in interfaces) {
				foreach (var member in intf.GetMembers(symbol.Name)) {
					if (member.Kind == symbol.Kind
						&& member.DeclaredAccessibility == Accessibility.Public
						&& member.IsStatic == false
						&& member.MatchSignature(symbol.Kind, returnType, parameters, typeParams)) {
						implementedIntfs.Add(intf);
					}
				}
			}
			if (implementedIntfs.Count > 0) {
				info = new ThemedTipDocument().AppendTitle(IconIds.InterfaceImplementation, R.T_Implements);
				foreach (var item in implementedIntfs) {
					info.Append(new ThemedTipParagraph(item.GetImageId(), ToUIText(item)));
				}
			}
			if (explicitImplementations != null) {
				implementedIntfs.Clear();
				implementedIntfs.AddRange(explicitImplementations.Select(i => i.ContainingType));
				if (implementedIntfs.Count > 0) {
					(info ?? (info = new ThemedTipDocument()))
						.AppendTitle(IconIds.InterfaceImplementation, R.T_ExplicitImplements);
					foreach (var item in implementedIntfs) {
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
			doc.AppendTitle(IconIds.ListMembers, R.T_Member);
			ShowMembers(type, doc, false);
			foreach (var item in type.AllInterfaces) {
				ShowMembers(item, doc, true);
			}
			if (doc.ParagraphCount > 1) {
				qiContent.Add(doc);
			}
		}

		static void ShowMembers(INamedTypeSymbol type, ThemedTipDocument doc, bool isInherit) {
			var members = ImmutableArray.CreateBuilder<ISymbol>();
			members.AddRange(type.FindMembers());
			members.Sort(CodeAnalysisHelper.CompareByAccessibilityKindName);
			var isInterface = type.TypeKind == TypeKind.Interface;
			foreach (var member in members) {
				var t = new ThemedTipText();
				if (isInherit) {
					t.AddSymbol(type, false, SymbolFormatter.SemiTransparent).Append(".");
				}
				t.AddSymbol(member, false, _SymbolFormatter);
				if (member.Kind == SymbolKind.Method) {
					t.AddParameters(((IMethodSymbol)member).Parameters, _SymbolFormatter);
					if (isInterface && member.IsStatic == false && member.IsAbstract == false) {
						t.Append(" ").AddImage(IconIds.DefaultInterfaceImplementation);
					}
				}
				if (member.IsStatic) {
					t.Append(" ").AddImage(IconIds.StaticMember);
				}
				doc.Append(new ThemedTipParagraph(member.GetImageId(), t));
			}
		}

		static void ShowExtensionMethod(QiContainer qiContent, IMethodSymbol method) {
			var info = new ThemedTipDocument()
				.AppendParagraph(IconIds.ExtensionMethod, new ThemedTipText(R.T_ExtendedBy, true).AddSymbolDisplayParts(method.ContainingType.ToDisplayParts(), _SymbolFormatter, -1));
			var extType = method.MethodKind == MethodKind.ReducedExtension ? method.ReceiverType : method.GetParameters()[0].Type;
			if (extType != null) {
				info.AppendParagraph(extType.GetImageId(), new ThemedTipText(R.T_Extending, true).AddSymbol(extType, true, _SymbolFormatter));
			}
			qiContent.Add(info);
		}

		static Grid ShowStringInfo(string sv, bool showText) {
			var g = new Grid {
				HorizontalAlignment = HorizontalAlignment.Left,
				RowDefinitions = {
					new RowDefinition(), new RowDefinition()
				},
				ColumnDefinitions = {
					new ColumnDefinition(), new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }
				},
				Children = {
					new ThemedTipText(R.T_Chars, true) { Margin = WpfHelper.GlyphMargin, TextAlignment = TextAlignment.Right },
					new ThemedTipText(R.T_HashCode, true) { Margin = WpfHelper.GlyphMargin, TextAlignment = TextAlignment.Right }.SetValue(Grid.SetRow, 1),
					new ThemedTipText(sv.Length.ToString()) { Background = ThemeHelper.TextBoxBackgroundBrush.Alpha(0.5), Foreground = ThemeHelper.TextBoxBrush, Padding = WpfHelper.SmallHorizontalMargin }.WrapBorder(ThemeHelper.TextBoxBorderBrush, WpfHelper.TinyMargin).SetValue(Grid.SetColumn, 1),
					new ThemedTipText(sv.GetHashCode().ToString()) { Background = ThemeHelper.TextBoxBackgroundBrush.Alpha(0.5), Foreground = ThemeHelper.TextBoxBrush, Padding = WpfHelper.SmallHorizontalMargin }.WrapBorder(ThemeHelper.TextBoxBorderBrush, WpfHelper.TinyMargin).SetValue(Grid.SetRow, 1).SetValue(Grid.SetColumn, 1),
				}
			};
			if (showText) {
				g.RowDefinitions.Add(new RowDefinition());
				g.Children.Add(new ThemedTipText(R.T_Text, true) { Margin = WpfHelper.GlyphMargin, TextAlignment = TextAlignment.Right }.SetValue(Grid.SetRow, 2));
				g.Children.Add(new ThemedTipText(sv) { Background = ThemeHelper.TextBoxBackgroundBrush.Alpha(0.5), Foreground = ThemeHelper.TextBoxBrush, Padding = WpfHelper.SmallHorizontalMargin }.WrapBorder(ThemeHelper.TextBoxBorderBrush, WpfHelper.TinyMargin).SetValue(Grid.SetRow, 2).SetValue(Grid.SetColumn, 1));
			}
			return g;
		}

		static void ShowBaseType(QiContainer qiContent, ITypeSymbol typeSymbol) {
			var baseType = typeSymbol.BaseType;
			if (baseType == null || baseType.IsCommonClass()) {
				return;
			}
			var classList = new ThemedTipText(R.T_BaseType, true)
				.AddSymbol(baseType, null, _SymbolFormatter);
			var info = new ThemedTipDocument().Append(new ThemedTipParagraph(IconIds.BaseTypes, classList));
			while (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseTypeInheritence) && (baseType = baseType.BaseType) != null) {
				if (baseType.IsCommonClass() == false) {
					classList.Inlines.Add(new ThemedTipText(" - ") { TextWrapping = TextWrapping.Wrap }.AddSymbol(baseType, null, _SymbolFormatter));
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
			var content = new ThemedTipText(R.T_EnumUnderlyingType, true).AddSymbol(t, true, _SymbolFormatter);
			var s = new ThemedTipDocument()
				.Append(new ThemedTipParagraph(IconIds.Enum, content));
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
			content.AppendLine().Append(R.T_EnumFieldCount, true).Append(c.ToString())
				.AppendLine().Append(R.T_EnumMin, true)
					.Append(min.ToString() + "(")
					.Append(minName.Name, _SymbolFormatter.Enum)
					.Append(")")
				.AppendLine().Append(R.T_EnumMax, true)
					.Append(max.ToString() + "(")
					.Append(maxName.Name, _SymbolFormatter.Enum)
					.Append(")");
			if (type.GetAttributes().Any(a => a.AttributeClass.MatchTypeName(nameof(FlagsAttribute), "System"))) {
				var d = Convert.ToString(Convert.ToInt64(bits), 2);
				content.AppendLine().Append(R.T_EnumAllFlags, true)
					.Append($"{d} ({ d.Length}" + (d.Length > 1 ? " bits)" : " bit)"));
			}
			qiContent.Add(s);
		}

		static void ShowInterfaces(QiContainer qiContent, ITypeSymbol type) {
			var showAll = Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfacesInheritence);
			type = type.OriginalDefinition;
			var interfaces = type.Interfaces;
			if (interfaces.Length == 0 && showAll == false) {
				return;
			}
			var declaredInterfaces = ImmutableArray.CreateBuilder<INamedTypeSymbol>(interfaces.Length);
			var inheritedInterfaces = ImmutableArray.CreateBuilder<INamedTypeSymbol>(5);
			INamedTypeSymbol disposable = null;
			foreach (var item in interfaces) {
				if (item.IsDisposable()) {
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
				if (item.IsDisposable()) {
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
			var info = new ThemedTipDocument().AppendTitle(IconIds.Interface, R.T_Interface);
			if (disposable != null) {
				var t = ToUIText(disposable);
				if (interfaces.Contains(disposable) == false) {
					t.Append(R.T_Inherited);
				}
				info.Append(new ThemedTipParagraph(IconIds.Disposable, t));
			}
			foreach (var item in declaredInterfaces) {
				if (item != disposable) {
					info.Append(new ThemedTipParagraph(item.GetImageId(), ToUIText(item)));
				}
			}
			foreach (var item in inheritedInterfaces) {
				info.Append(new ThemedTipParagraph(item.GetImageId(), ToUIText(item).Append(R.T_Inherited)));
			}
			qiContent.Add(info);
		}

		static void ShowDeclarationModifier(QiContainer qiContent, ISymbol symbol) {
			qiContent.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(IconIds.DeclarationModifier, _SymbolFormatter.ShowSymbolDeclaration(symbol, new ThemedTipText(), true, false))));
		}

		static void ShowParameterInfo(QiContainer qiContent, SyntaxNode node, SemanticModel semanticModel) {
			var argument = node;
			if (node.Kind() == SyntaxKind.NullLiteralExpression) {
				argument = node.Parent;
			}
			int depth = 0;
			do {
				var n = argument as ArgumentSyntax ?? (SyntaxNode)(argument as AttributeArgumentSyntax);
				if (n != null) {
					ShowArgumentInfo(qiContent, n, semanticModel);
					return;
				}
			} while ((argument = argument.Parent) != null && ++depth < 4);
		}

		static void ShowArgumentInfo(QiContainer qiContent, SyntaxNode argument, SemanticModel semanticModel) {
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
			var symbol = semanticModel.GetSymbolInfo(argList.Parent);
			if (symbol.Symbol != null) {
				IMethodSymbol m;
				switch (symbol.Symbol.Kind) {
					case SymbolKind.Method: m = symbol.Symbol as IMethodSymbol; break;
					case CodeAnalysisHelper.FunctionPointerType: m = (symbol.Symbol as ITypeSymbol).GetFunctionPointerTypeSignature(); break;
					default: m = null; break;
				}
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
				var doc = argName != null ? new XmlDoc(om.MethodKind == MethodKind.DelegateInvoke ? om.ContainingSymbol : om, semanticModel.Compilation) : null;
				var paramDoc = doc?.GetParameter(argName);
				var content = new ThemedTipText(R.T_Argument, true)
					.Append(R.T_ArgumentOf)
					.AddSymbol(om.ReturnType, om.MethodKind == MethodKind.Constructor ? "new" : null, _SymbolFormatter)
					.Append(" ")
					.AddSymbol(om.MethodKind != MethodKind.DelegateInvoke ? om : (ISymbol)om.ContainingType, true, _SymbolFormatter)
					.AddParameters(om.Parameters, _SymbolFormatter, argIndex);
				var info = new ThemedTipDocument().Append(new ThemedTipParagraph(IconIds.Argument, content));
				if (paramDoc != null) {
					content.Append("\n" + argName, true, false, _SymbolFormatter.Parameter).Append(": ");
					new XmlDocRenderer(semanticModel.Compilation, _SymbolFormatter).Render(paramDoc, content.Inlines);
				}
				if (m.IsGenericMethod) {
					for (int i = 0; i < m.TypeArguments.Length; i++) {
						content.Append("\n");
						_SymbolFormatter.ShowTypeArgumentInfo(m.TypeParameters[i], m.TypeArguments[i], content);
						var typeParamDoc = doc.GetTypeParameter(m.TypeParameters[i].Name);
						if (typeParamDoc != null) {
							content.Append(": ");
							new XmlDocRenderer(semanticModel.Compilation, _SymbolFormatter).Render(typeParamDoc, content.Inlines);
						}
					}
				}
				if (p != null && p.Type.TypeKind == TypeKind.Delegate) {
					var invoke = ((INamedTypeSymbol)p.Type).DelegateInvokeMethod;
					info.Append(new ThemedTipParagraph(IconIds.Delegate,
						new ThemedTipText(R.T_DelegateSignature, true).Append(": ")
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
							IconIds.Attribute,
							new ThemedTipText().Append(R.T_AttributeOf).Append(p.Name, true, false, _SymbolFormatter.Parameter).Append(":")
						);
						foreach (var attr in attrs) {
							_SymbolFormatter.Format(para.Content.AppendLine().Inlines, attr, 0);
						}
						info.Append(para);
					}
				}
				qiContent.Add(info);
			}
			else if (symbol.CandidateSymbols.Length > 0) {
				var info = new ThemedTipDocument();
				info.Append(new ThemedTipParagraph(IconIds.ParameterCandidate, new ThemedTipText(R.T_MaybeArgument, true).Append(R.T_MaybeArgumentOf)));
				foreach (var candidate in symbol.CandidateSymbols) {
					info.Append(
						new ThemedTipParagraph(
							candidate.GetImageId(),
							new ThemedTipText().AddSymbolDisplayParts(
								candidate.ToDisplayParts(CodeAnalysisHelper.QuickInfoSymbolDisplayFormat),
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
				qiContent.Add(new ThemedTipText(R.T_ArgumentNOf.Replace("<N>", (++argIndex).ToString())).Append(methodName, true));
			}
			else {
				qiContent.Add(R.T_ArgumentN.Replace("<N>", (++argIndex).ToString()));
			}
		}

		static void ShowAnonymousTypeInfo(QiContainer container, ISymbol symbol) {
			ITypeSymbol t;
			ImmutableArray<ITypeSymbol>.Builder types = null;
			switch (symbol.Kind) {
				case SymbolKind.NamedType:
					if ((t = symbol as ITypeSymbol).IsAnonymousType
						&& Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle) == false) {
						Add(ref types, t);
					}
					break;
				case SymbolKind.Method:
					var m = symbol as IMethodSymbol;
					if (m.IsGenericMethod) {
						foreach (var item in m.TypeArguments) {
							if (item.IsAnonymousType) {
								Add(ref types, item);
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

			void ShowAnonymousTypes(QiContainer c, ImmutableArray<ITypeSymbol>.Builder anonymousTypes, ISymbol currentSymbol) {
				const string AnonymousNumbers = "abcdefghijklmnopqrstuvwxyz";
				var d = new ThemedTipDocument().AppendTitle(IconIds.AnonymousType, R.T_AnonymousType);
				for (var i = 0; i < anonymousTypes.Count; i++) {
					var type = anonymousTypes[i];
					var content = new ThemedTipText()
						.AddSymbol(type, "'" + AnonymousNumbers[i], _SymbolFormatter)
						.Append(" is { ");
					foreach (var m in type.GetMembers()) {
						if (m.Kind != SymbolKind.Property) {
							continue;
						}
						var pt = m.GetReturnType();
						string alias = null;
						if (pt?.IsAnonymousType == true) {
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
			void Add(ref ImmutableArray<ITypeSymbol>.Builder list, ITypeSymbol type) {
				if ((list ?? (list = ImmutableArray.CreateBuilder<ITypeSymbol>())).Contains(type) == false) {
					list.Add(type);
				}
				if (type.ContainingType?.IsAnonymousType == true) {
					Add(ref list, type);
				}
			}
		}

		static TextBlock ToUIText(ISymbol symbol) {
			return new ThemedTipText().AddSymbolDisplayParts(symbol.ToDisplayParts(CodeAnalysisHelper.QuickInfoSymbolDisplayFormat), _SymbolFormatter, -1);
		}

		public void Dispose() {
			if (--_Ref == 0 && _TextBuffer != null) {
				_TextBuffer.Properties.RemoveProperty(typeof(CSharpQuickInfo));
				_TextBuffer = null;
			}
		}
	}

	sealed class QiContainer
	{
		ImmutableArray<object>.Builder _List = ImmutableArray.CreateBuilder<object>();
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
