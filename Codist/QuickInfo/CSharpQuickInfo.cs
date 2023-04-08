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
using Microsoft.VisualStudio.Text.Projection;
using R = Codist.Properties.Resources;

namespace Codist.QuickInfo
{
	sealed class CSharpQuickInfo : IAsyncQuickInfoSource
	{
		internal const string Name = nameof(CSharpQuickInfo);

		static readonly SymbolFormatter __SymbolFormatter = SymbolFormatter.Instance;

		SpecialProjectInfo _SpecialProject;
		bool _isCandidate;

		public async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
			if (QuickInfoOverrider.CheckCtrlSuppression()) {
				return null;
			}
			// Map the trigger point down to our buffer.
			var buffer = session.TextView.TextBuffer;
			if (buffer is IProjectionBuffer projection) {
				foreach (var sb in projection.SourceBuffers) {
					if (session.GetTriggerPoint(sb) != null) {
						buffer = sb;
						break;
					}
				}
			}
			return buffer == null
				? null
				: await InternalGetQuickInfoItemAsync(session, buffer, cancellationToken).ConfigureAwait(false);
		}

		async Task<QuickInfoItem> InternalGetQuickInfoItemAsync(IAsyncQuickInfoSession session, ITextBuffer buffer, CancellationToken cancellationToken) {
			ISymbol symbol;
			SyntaxNode node;
			ImmutableArray<ISymbol> candidates;
			SyntaxToken token;
			var overrider = Config.Instance.QuickInfoOptions.HasAnyFlag(QuickInfoOptions.QuickInfoOverride)
				? QuickInfoOverrider.CreateOverrider(session)
				: null;
			var container = new InfoContainer(overrider);
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
			if (_SpecialProject == null) {
				_SpecialProject = new SpecialProjectInfo(semanticModel);
			}
			var unitCompilation = semanticModel.SyntaxTree.GetCompilationUnitRoot(cancellationToken);

			//look for occurrences of our QuickInfo words in the span
			token = unitCompilation.FindToken(subjectTriggerPoint, true);
			var skipTriggerPointCheck = false;
			var isConvertedType = false;
			symbol = null;
			ClassifyToken:
			switch (token.Kind()) {
				case SyntaxKind.WhitespaceTrivia:
				case SyntaxKind.SingleLineCommentTrivia:
				case SyntaxKind.MultiLineCommentTrivia:
					return null;
				case SyntaxKind.OpenBraceToken:
					if ((node = unitCompilation.FindNode(token.Span))
						.Kind().IsAny(SyntaxKind.ArrayInitializerExpression,
							SyntaxKind.CollectionInitializerExpression,
							SyntaxKind.ComplexElementInitializerExpression,
							SyntaxKind.ObjectInitializerExpression,
							CodeAnalysisHelper.WithInitializerExpression)) {
						container.Add(new ThemedTipText()
							.SetGlyph(ThemeHelper.GetImage(IconIds.InstanceMember))
							.Append(R.T_ExpressionCount)
							.Append((node as InitializerExpressionSyntax).Expressions.Count.ToText(), true, false, __SymbolFormatter.Number));
					}
					if (overrider != null) {
						overrider.OverrideBuiltInXmlDoc = false;
					}
					ShowBlockInfo(container, currentSnapshot, node, semanticModel);
					goto RETURN;
				case SyntaxKind.CloseBraceToken:
					if (overrider != null) {
						overrider.OverrideBuiltInXmlDoc = false;
					}
					ShowBlockInfo(container, currentSnapshot, node = unitCompilation.FindNode(token.Span), semanticModel);
					goto RETURN;
				case SyntaxKind.ThisKeyword: // convert to type below
				case SyntaxKind.BaseKeyword:
				case SyntaxKind.OverrideKeyword:
					break;
				case SyntaxKind.TrueKeyword:
				case SyntaxKind.FalseKeyword:
				case SyntaxKind.IsKeyword:
				case SyntaxKind.AmpersandAmpersandToken:
				case SyntaxKind.BarBarToken:
					symbol = semanticModel.Compilation.GetSpecialType(SpecialType.System_Boolean);
					isConvertedType = true;
					break;
				case SyntaxKind.EqualsGreaterThanToken:
					if ((node = unitCompilation.FindNode(token.Span)).IsKind(CodeAnalysisHelper.SwitchExpressionArm) && node.Parent.IsKind(CodeAnalysisHelper.SwitchExpression)) {
						symbol = semanticModel.GetTypeInfo(node.Parent, cancellationToken).ConvertedType;
						isConvertedType = true;
					}
					break;
				case SyntaxKind.ExclamationEqualsToken:
				case SyntaxKind.EqualsEqualsToken:
					symbol = semanticModel.GetTypeInfo(unitCompilation.FindNode(token.Span), cancellationToken).ConvertedType;
					isConvertedType = true;
					break;
				case SyntaxKind.EqualsToken:
					symbol = semanticModel.GetTypeInfo(unitCompilation.FindNode(token.GetPreviousToken().Span), cancellationToken).ConvertedType;
					isConvertedType = true;
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
				case CodeAnalysisHelper.WithKeyword:
					symbol = semanticModel.GetTypeInfo(unitCompilation.FindNode(token.Span, false, true), cancellationToken).ConvertedType;
					if (symbol == null) {
						if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter)) {
							break;
						}
						return null;
					}
					isConvertedType = true;
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
					node = unitCompilation.FindNode(token.Span, false, true) as AwaitExpressionSyntax;
					if (node != null) {
						symbol = semanticModel.GetTypeInfo(node, cancellationToken).Type;
					}
					goto PROCESS;
				case SyntaxKind.DotToken:
					token = token.GetNextToken();
					skipTriggerPointCheck = true;
					break;
				case SyntaxKind.OpenParenToken:
				case SyntaxKind.CloseParenToken:
					node = unitCompilation.FindNode(token.Span, false, true);
					if (node.IsKind(SyntaxKind.ArgumentList)) {
						node = node.Parent;
						goto PROCESS;
					}
					goto case SyntaxKind.CommaToken;
				case SyntaxKind.CommaToken:
				case SyntaxKind.ColonToken:
				case SyntaxKind.SemicolonToken:
					token = token.GetPreviousToken();
					skipTriggerPointCheck = true;
					goto ClassifyToken;
				case SyntaxKind.OpenBracketToken:
				case SyntaxKind.CloseBracketToken:
					if ((node = unitCompilation.FindNode(token.Span)).IsKind(SyntaxKind.BracketedArgumentList)
						&& node.Parent.IsKind(SyntaxKind.ElementAccessExpression)) {
						symbol = semanticModel.GetSymbolInfo((ElementAccessExpressionSyntax)node.Parent, cancellationToken).Symbol;
					}
					if (symbol == null) {
						goto case SyntaxKind.OpenParenToken;
					}
					break;
				case SyntaxKind.UsingKeyword:
					node = unitCompilation.FindNode(token.Span);
					symbol = semanticModel.GetDisposeMethodForUsingStatement(node, cancellationToken);
					goto PROCESS;
				case SyntaxKind.InKeyword:
					if ((node = unitCompilation.FindNode(token.Span)).IsKind(SyntaxKind.ForEachStatement)
						&& (symbol = semanticModel.GetForEachStatementInfo((CommonForEachStatementSyntax)node).GetEnumeratorMethod) != null) {
						goto PROCESS;
					}
					break;
				case SyntaxKind.LessThanToken:
				case SyntaxKind.GreaterThanToken:
					node = unitCompilation.FindNode(token.Span);
					if (node is BinaryExpressionSyntax) {
						goto PROCESS;
					}
					else {
						goto case SyntaxKind.OpenParenToken;
					}
				case SyntaxKind.HashToken:
					token = token.GetNextToken();
					if (token.IsKind(SyntaxKind.EndRegionKeyword)) {
						goto case SyntaxKind.EndRegionKeyword;
					}
					else if (token.IsKind(SyntaxKind.EndIfKeyword)) {
						goto case SyntaxKind.EndIfKeyword;
					}
					return null;
				case SyntaxKind.EndRegionKeyword:
					container.Add(new ThemedTipText(R.T_EndOfRegion)
						.SetGlyph(ThemeHelper.GetImage(IconIds.Region))
						.Append((unitCompilation.FindNode(token.Span, true) as EndRegionDirectiveTriviaSyntax).GetRegion()?.GetDeclarationSignature(), true)
						);
					return CreateQuickInfoItem(session, token, container.ToUI());
				case SyntaxKind.EndIfKeyword:
					container.Add(new ThemedTipText(R.T_EndOfIf)
						.SetGlyph(ThemeHelper.GetImage(IconIds.Region))
						.Append((unitCompilation.FindNode(token.Span, true) as EndIfDirectiveTriviaSyntax).GetIf()?.GetDeclarationSignature(), true)
						);
					return CreateQuickInfoItem(session, token, container.ToUI());
				case SyntaxKind.VoidKeyword:
					return null;
				case SyntaxKind.TypeOfKeyword:
					symbol = semanticModel.GetSystemTypeSymbol(nameof(Type));
					break;
				case SyntaxKind.StackAllocKeyword:
					symbol = semanticModel.GetTypeInfo(unitCompilation.FindNode(token.Span), cancellationToken).Type;
					break;
				case CodeAnalysisHelper.DotDotToken:
					symbol = semanticModel.Compilation.GetSpecialType(SpecialType.System_Int32);
					isConvertedType = true;
					break;
				default:
					if (token.Kind().IsPredefinedSystemType()) {
						symbol = semanticModel.GetSystemTypeSymbol(token.Kind());
						break;
					}
					if (token.Span.Contains(subjectTriggerPoint, true) == false
						|| token.IsReservedKeyword()) {
						node = unitCompilation.FindNode(token.Span);
						if (node is StatementSyntax) {
							ShowBlockInfo(container, currentSnapshot, node, semanticModel);
						}
						return container.ItemCount > 0
							? CreateQuickInfoItem(session, token, container.ToUI())
							: null;
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
				ShowParameterInfo(container, node, semanticModel, cancellationToken);
			}
			if (symbol == null) {
				symbol = token.IsKind(SyntaxKind.CloseBraceToken) ? null
				: GetSymbol(semanticModel, node, ref candidates, cancellationToken);
			}
			if (_isCandidate = candidates.IsDefaultOrEmpty == false) {
				ShowCandidateInfo(container, candidates);
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
					case CodeAnalysisHelper.SingleLineRawStringLiteralToken:
					case CodeAnalysisHelper.MultiLineRawStringLiteralToken:
						symbol = semanticModel.Compilation.GetSpecialType(SpecialType.System_String);
						isConvertedType = true;
						break;
					case SyntaxKind.CharacterLiteralToken:
						symbol = semanticModel.Compilation.GetSpecialType(SpecialType.System_Char);
						if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues)
						&& token.Span.Length >= 8) {
							container.Add(new ThemedTipText(token.ValueText) { FontSize = ThemeHelper.ToolTipFontSize * 2 });
						}
						else if (node.IsKind(SyntaxKind.Block) || node.IsKind(SyntaxKind.SwitchStatement)) {
							ShowBlockInfo(container, currentSnapshot, node, semanticModel);
						}
						isConvertedType = true;
						break;
					case SyntaxKind.NumericLiteralToken:
						symbol = semanticModel.GetSystemTypeSymbol(token.Value.GetType().Name);
						isConvertedType = true;
						break;
					default:
						if (node.IsKind(SyntaxKind.Block) || node.IsKind(SyntaxKind.SwitchStatement)) {
							ShowBlockInfo(container, currentSnapshot, node, semanticModel);
						}
						break;
				}
				ShowMiscInfo(container, node);
				if (symbol == null) {
					goto RETURN;
				}
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation)) {
				if (isConvertedType == false) {
					container.Add(await ShowAvailabilityAsync(doc, token, cancellationToken).ConfigureAwait(false));
				}
				ctor = node.Parent as ObjectCreationExpressionSyntax;
				OverrideDocumentation(node,
					overrider,
					ctor?.Type == node
						? semanticModel.GetSymbolInfo(ctor, cancellationToken).Symbol ?? symbol
						: symbol,
					semanticModel,
					cancellationToken);
			}
			if (isConvertedType == false) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Attributes)) {
					ShowAttributesInfo(container, symbol);
				}
				ShowSymbolInfo(session, container, node, symbol, semanticModel, cancellationToken);
			}
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
			overrider?.ApplyClickAndGo(symbol);
			return container.ItemCount == 0 ? null : CreateQuickInfoItem(session, token, container.ToUI().Tag());
		}

		static QuickInfoItem CreateQuickInfoItem(IAsyncQuickInfoSession session, SyntaxToken? token, object item) {
			session.KeepViewPosition();
			return new QuickInfoItem(token?.Span.CreateSnapshotSpan(session.TextView.TextSnapshot).ToTrackingSpan(), item);
		}

		static Task<ThemedTipDocument> ShowAvailabilityAsync(Document doc, SyntaxToken token, CancellationToken cancellationToken) {
			var solution = doc.Project.Solution;
			if (solution.ProjectIds.Count == 0) {
				return System.Threading.Tasks.Task.FromResult<ThemedTipDocument>(null);
			}
			var linkedDocuments = doc.GetLinkedDocumentIds();
			if (linkedDocuments.Length == 0) {
				return System.Threading.Tasks.Task.FromResult<ThemedTipDocument>(null);
			}
			return ShowAvailabilityAsync(token, solution, linkedDocuments, cancellationToken);
		}

		static async Task<ThemedTipDocument> ShowAvailabilityAsync(SyntaxToken token, Solution solution, ImmutableArray<DocumentId> linkedDocuments, CancellationToken cancellationToken) {
			ThemedTipDocument r = null;
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
			return r;
		}

		static ISymbol GetSymbol(SemanticModel semanticModel, SyntaxNode node, ref ImmutableArray<ISymbol> candidates, CancellationToken cancellationToken) {
			SyntaxKind kind = node.Kind();
			if (kind == SyntaxKind.BaseExpression
				|| kind == SyntaxKind.DefaultLiteralExpression
				|| kind == SyntaxKind.ImplicitStackAllocArrayCreationExpression) {
				return semanticModel.GetTypeInfo(node, cancellationToken).ConvertedType;
			}
			if (kind == SyntaxKind.ThisExpression) {
				return semanticModel.GetTypeInfo(node, cancellationToken).Type;
			}
			if (kind == SyntaxKind.TupleElement || kind == SyntaxKind.ForEachStatement) {
				return semanticModel.GetDeclaredSymbol(node, cancellationToken);
			}
			var symbolInfo = semanticModel.GetSymbolInfo(node, cancellationToken);
			if (symbolInfo.CandidateReason != CandidateReason.None) {
				candidates = symbolInfo.CandidateSymbols;
				return symbolInfo.CandidateSymbols.FirstOrDefault();
			}
			return symbolInfo.Symbol
				?? (kind.IsDeclaration()
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

		static ThemedTipDocument OverrideDocumentation(SyntaxNode node, IQuickInfoOverrider qiWrapper, ISymbol symbol, SemanticModel semanticModel, CancellationToken cancellationToken) {
			if (symbol == null) {
				return null;
			}
			var ms = symbol as IMethodSymbol;
			if (symbol.Kind == SymbolKind.Method && ms?.IsAccessor() == true) {
				// hack: symbol could be Microsoft.CodeAnalysis.CSharp.Symbols.SourceMemberFieldSymbolFromDeclarator which is not IMethodSymbol
				symbol = ms.AssociatedSymbol;
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
			if (ms?.MethodKind == MethodKind.Constructor) {
				symbol = symbol.ContainingType;
				var summary = new XmlDoc(symbol, compilation)
					.GetDescription(symbol);
				if (summary != null) {
					tip.Append(new ThemedTipParagraph(IconIds.ReferencedXmlDoc,
						new ThemedTipText()
							.AddSymbol(symbol.OriginalDefinition, true, SymbolFormatter.Instance)
							.Append(": ")
							.AddXmlDoc(summary, new XmlDocRenderer(compilation, SymbolFormatter.Instance)))
						);
				}
			}

			ShowCapturedVariables(node, symbol, semanticModel, tip, cancellationToken);

			if (tip.ParagraphCount > 0) {
				var i = tip.ParagraphCount;
				foreach (var p in tip.Paragraphs) {
					if (--i > 0) {
						p.Padding = WpfHelper.MiddleBottomMargin;
					}
				}
				qiWrapper.OverrideDocumentation(tip);
			}
			return tip;
		}

		static void ShowCapturedVariables(SyntaxNode node, ISymbol symbol, SemanticModel semanticModel, ThemedTipDocument tip, CancellationToken cancellationToken) {
			if (node is LambdaExpressionSyntax
				|| (symbol as IMethodSymbol)?.MethodKind == MethodKind.LocalFunction) {
				var ss = node is LambdaExpressionSyntax
					? node.AncestorsAndSelf().FirstOrDefault(i => i is StatementSyntax || i is ExpressionSyntax && i.IsKind(SyntaxKind.IdentifierName) == false)
					: symbol.GetSyntaxNode(cancellationToken);
				if (ss != null) {
					var df = semanticModel.AnalyzeDataFlow(ss);
					var captured = df.ReadInside.RemoveAll(i => df.VariablesDeclared.Contains(i) || (i as ILocalSymbol)?.IsConst == true);
					if (captured.Length > 0) {
						var p = new ThemedTipParagraph(IconIds.ReadVariables, new ThemedTipText().Append(R.T_CapturedVariables, true));
						int i = 0;
						foreach (var item in captured) {
							p.Content.Append(++i == 1 ? ": " : ", ").AddSymbol(item, false, __SymbolFormatter);
						}
						tip.Append(p);
					}
				}
			}
		}

		static void ShowCandidateInfo(InfoContainer qiContent, ImmutableArray<ISymbol> candidates) {
			var info = new ThemedTipDocument().AppendTitle(IconIds.SymbolCandidate, R.T_Maybe);
			foreach (var item in candidates) {
				info.Append(new ThemedTipParagraph(item.GetImageId(), ToUIText(item.OriginalDefinition)));
			}
			qiContent.Add(info);
		}

		void ShowSymbolInfo(IAsyncQuickInfoSession session, InfoContainer qiContent, SyntaxNode node, ISymbol symbol, SemanticModel semanticModel, CancellationToken cancellationToken) {
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
					ShowMethodInfo(qiContent, node, m, semanticModel, cancellationToken);
					if (node.Parent.IsKind(SyntaxKind.Attribute)
						|| node.Parent.Parent.IsKind(SyntaxKind.Attribute) // qualified attribute annotation
						) {
						if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Attributes)) {
							ShowAttributesInfo(qiContent, symbol.ContainingType);
						}
						ShowTypeInfo(qiContent, node.Parent, symbol.ContainingType, semanticModel, cancellationToken);
					}
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color)
						&& m.ContainingType?.Name == "Color"
						&& session.Mark(nameof(ColorQuickInfoUI))) {
						qiContent.Add(ColorQuickInfoUI.PreviewColorMethodInvocation(semanticModel, node, symbol as IMethodSymbol));
					}
					if (m.MethodKind == MethodKind.BuiltinOperator && node is ExpressionSyntax) {
						var value = semanticModel.GetConstantValue(node, cancellationToken);
						if (value.HasValue) {
							ShowConstInfo(qiContent, null, value.Value);
						}
					}
					break;
				case SymbolKind.NamedType:
					ShowTypeInfo(qiContent, node, symbol as INamedTypeSymbol, semanticModel, cancellationToken);
					break;
				case SymbolKind.Property:
					ShowPropertyInfo(qiContent, symbol as IPropertySymbol);
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color)
						&& session.Mark(nameof(ColorQuickInfoUI))) {
						qiContent.Add(ColorQuickInfoUI.PreviewColorProperty(symbol as IPropertySymbol, _SpecialProject.MaybeVsProject));
					}
					break;
				case SymbolKind.Namespace:
					ShowNamespaceInfo(qiContent, symbol as INamespaceSymbol);
					break;
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.SymbolLocation)) {
				ShowSymbolLocationInfo(qiContent, semanticModel.Compilation, symbol);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
				&& (node.Parent.IsKind(SyntaxKind.Argument) == false || Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter) == false) /*the signature has already been displayed there*/) {
				var st = symbol.GetReturnType();
				if (st?.TypeKind == TypeKind.Delegate) {
					var invoke = ((INamedTypeSymbol)st).DelegateInvokeMethod;
					qiContent.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(IconIds.Delegate,
						new ThemedTipText(R.T_DelegateSignature, true).Append(": ")
							.AddSymbol(invoke.ReturnType, false, __SymbolFormatter)
							.Append(" ")
							.AddParameters(invoke.Parameters, __SymbolFormatter)
						)));
				}
			}

		}

		static void ShowSymbolLocationInfo(InfoContainer qiContent, Compilation compilation, ISymbol symbol) {
			var (p, f) = compilation.GetReferencedAssemblyPath(symbol as IAssemblySymbol ?? symbol.ContainingAssembly);
			if (String.IsNullOrEmpty(f)) {
				return;
			}
			var asmText = new ThemedTipText(R.T_Assembly, true);
			var item = new ThemedTipDocument().AppendParagraph(IconIds.Module, asmText);
			if (p.Length > 0) {
				asmText.AppendFileLink(f, p);
			}
			else {
				var proj = symbol.GetSourceReferences().Select(r => SemanticContext.GetHovered().GetProject(r.SyntaxTree)).FirstOrDefault(i => i != null);
				if (proj?.OutputFilePath != null) {
					(p, f) = FileHelper.DeconstructPath(proj.OutputFilePath);
				}
				asmText.AppendFileLink(f, p);
			}
			switch (symbol.Kind) {
				case SymbolKind.Field:
				case SymbolKind.Property:
				case SymbolKind.Event:
				case SymbolKind.Method:
				case SymbolKind.NamedType:
					var ns = symbol.ContainingNamespace;
					if (ns != null) {
						var nsText = new ThemedTipText(R.T_Namespace, true);
						__SymbolFormatter.ShowContainingNamespace(symbol, nsText);
						item.AppendParagraph(IconIds.Namespace, nsText);
					}
					break;
			}
			qiContent.Add(item);
		}

		static void ShowBlockInfo(InfoContainer qiContent, ITextSnapshot textSnapshot, SyntaxNode node, SemanticModel semanticModel) {
			var lines = textSnapshot.GetLineSpan(node.Span).Length + 1;
			if (lines > 1) {
				qiContent.Add(
					(lines > 100 ? new ThemedTipText(lines + R.T_Lines, true) : new ThemedTipText(lines + R.T_Lines))
						.SetGlyph(ThemeHelper.GetImage(IconIds.LineOfCode))
					);
			}
			if ((node is StatementSyntax || node is ExpressionSyntax || node is ConstructorInitializerSyntax) == false) {
				return;
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
					p.AddSymbol(item, false, __SymbolFormatter.Local);
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
						p.AddSymbol(item.GetReturnType(), item.Name, __SymbolFormatter);
					}
					else {
						p.AddSymbol(item, false, __SymbolFormatter);
					}
					s = true;
				}
				qiContent.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(IconIds.ReadVariables, p)));
			}
		}
		static void ShowMiscInfo(InfoContainer qiContent, SyntaxNode node) {
			Grid infoBox = null;
			var nodeKind = node.Kind();
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues) && (nodeKind == SyntaxKind.NumericLiteralExpression || nodeKind == SyntaxKind.CharacterLiteralExpression)) {
				infoBox = ToolTipHelper.ShowNumericForms(node);
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
							.AddSymbol(method.GetReturnType(), false, __SymbolFormatter)
							.Append(R.T_ReturnFor);
					}
				}
				else {
					t.Append(R.T_Return)
						.AddSymbol(symbol?.GetReturnType(), false, __SymbolFormatter)
						.Append(R.T_ReturnFor);
				}
				if (symbol != null) {
					t.AddSymbol(symbol, node is LambdaExpressionSyntax ? R.T_LambdaExpression + name : null, __SymbolFormatter);
				}
				else {
					t.Append(name);
				}
				return t;
			}
			return null;
		}

		static void ShowAttributesInfo(InfoContainer qiContent, ISymbol symbol) {
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
							__SymbolFormatter.Format(paragraph.Content.AppendLine().Inlines, item, attrType);
						}
					}
				}
				return paragraph;
			}
		}

		static void ShowPropertyInfo(InfoContainer qiContent, IPropertySymbol property) {
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

		static void ShowEventInfo(InfoContainer qiContent, IEventSymbol ev) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle) == false
					&& (ev.DeclaredAccessibility != Accessibility.Public || ev.IsAbstract || ev.IsStatic || ev.IsOverride || ev.IsVirtual)
					&& ev.ContainingType?.TypeKind != TypeKind.Interface) {
					ShowDeclarationModifier(qiContent, ev);
				}
				if (ev.Type.GetMembers("Invoke").FirstOrDefault() is IMethodSymbol invoke
					&& invoke.Parameters.Length == 2) {
					qiContent.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(IconIds.Event,
						new ThemedTipText(R.T_EventSignature, true).AddParameters(invoke.Parameters, __SymbolFormatter)
						)));
				}
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfaceImplementations)) {
				ShowInterfaceImplementation(qiContent, ev, ev.ExplicitInterfaceImplementations);
			}
		}

		void ShowFieldInfo(InfoContainer qiContent, IFieldSymbol field) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
				&& Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle) == false
				&& (field.DeclaredAccessibility != Accessibility.Public || field.IsReadOnly || field.IsVolatile || field.IsStatic)
				&& field.ContainingType.TypeKind != TypeKind.Enum) {
				ShowDeclarationModifier(qiContent, field);
			}
			if (field.HasConstantValue) {
				if (_SpecialProject.MaybeVsProject && field.ConstantValue is int fc) {
					ShowKnownImageId(qiContent, field, fc);
				}
				ShowConstInfo(qiContent, field, field.ConstantValue);
			}
			else if (field.IsReadOnly && field.IsStatic && field.ContainingType.Name == nameof(System.Reflection.Emit.OpCodes)) {
				qiContent.ShowOpCodeInfo(field);
			}

			void ShowKnownImageId(InfoContainer qc, IFieldSymbol f, int fieldValue) {
				var t = f.ContainingType;
				if (t.MatchTypeName(nameof(Microsoft.VisualStudio.Imaging.KnownImageIds), "Imaging", "VisualStudio", "Microsoft")
					|| t.MatchTypeName(nameof(IconIds), nameof(Codist))) {
					qc.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(fieldValue, new ThemedTipText(field.Name))));
				}
			}
		}

		void ShowMethodInfo(InfoContainer qiContent, SyntaxNode node, IMethodSymbol method, SemanticModel semanticModel, CancellationToken cancellationToken) {
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
				ShowOverloadsInfo(qiContent, node, method, semanticModel, cancellationToken);
			}
		}

		void ShowOverloadsInfo(InfoContainer qiContent, SyntaxNode node, IMethodSymbol method, SemanticModel semanticModel, CancellationToken cancellationToken) {
			if (_isCandidate) {
				return;
			}
			var overloads = node.IsKind(SyntaxKind.MethodDeclaration) || node.IsKind(SyntaxKind.ConstructorDeclaration)
				? method.ContainingType.GetMembers(method.Name)
				: semanticModel.GetMemberGroup(node, cancellationToken);
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
					t.AddSymbol(om.ReturnType, false, CodeAnalysisHelper.AreEqual(om.ReturnType, rt, false) ? SymbolFormatter.SemiTransparent : __SymbolFormatter).Append(" ");
				}
				if (ore) {
					t.AddSymbol(om.ReceiverType, "this", (om.ContainingType != ct ? __SymbolFormatter : SymbolFormatter.SemiTransparent).Keyword).Append(".", SymbolFormatter.SemiTransparent.PlainText);
				}
				else if (om.ContainingType != ct) {
					t.AddSymbol(om.ContainingType, false, __SymbolFormatter).Append(".", SymbolFormatter.SemiTransparent.PlainText);
				}
				t.AddSymbol(om, true, SymbolFormatter.SemiTransparent);
				t.Append("(", SymbolFormatter.SemiTransparent.PlainText);
				foreach (var op in om.Parameters) {
					var mp = mps.FirstOrDefault(p => p.Name == op.Name);
					if (op.Ordinal == 0) {
						if (ore == false && om.IsExtensionMethod) {
							t.Append("this ", __SymbolFormatter.Keyword);
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
					t.AddSymbolDisplayParts(op.ToDisplayParts(CodeAnalysisHelper.InTypeOverloadDisplayFormat), mp == null ? __SymbolFormatter : SymbolFormatter.SemiTransparent, -1);
				}
				t.Append(")", SymbolFormatter.SemiTransparent.PlainText);
				overloadInfo.Append(new ThemedTipParagraph(overload.GetImageId(), t));
			}
			if (overloadInfo.ParagraphCount > 1) {
				qiContent.Add(overloadInfo);
			}
		}

		static void ShowTypeArguments(InfoContainer qiContent, ImmutableArray<ITypeSymbol> args, ImmutableArray<ITypeParameterSymbol> typeParams) {
			var info = new ThemedTipDocument();
			var l = args.Length;
			var content = new ThemedTipText(R.T_TypeArgument, true);
			info.Append(new ThemedTipParagraph(IconIds.GenericDefinition, content));
			for (int i = 0; i < l; i++) {
				__SymbolFormatter.ShowTypeArgumentInfo(typeParams[i], args[i], content.AppendLine());
			}
			qiContent.Add(info);
		}

		static void ShowNamespaceInfo(InfoContainer qiContent, INamespaceSymbol nsSymbol) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NamespaceTypes) == false) {
				return;
			}
			var namespaces = nsSymbol.GetNamespaceMembers().ToImmutableArray().Sort(Comparer<INamespaceSymbol>.Create((x, y) => String.CompareOrdinal(x.Name, y.Name)));
			if (namespaces.Length > 0) {
				var info = new ThemedTipDocument().AppendTitle(IconIds.Namespace, R.T_Namespace);
				foreach (var ns in namespaces) {
					info.Append(new ThemedTipParagraph(IconIds.Namespace, new ThemedTipText().Append(ns.Name, __SymbolFormatter.Namespace)));
				}
				qiContent.Add(info);
			}

			var members = nsSymbol.GetTypeMembers().Sort(Comparer<INamedTypeSymbol>.Create((x, y) => String.Compare(x.Name, y.Name)));
			if (members.Length > 0) {
				var info = new StackPanel().Add(new ThemedTipText(R.T_Type, true));
				foreach (var type in members) {
					var t = new ThemedTipText().SetGlyph(ThemeHelper.GetImage(type.GetImageId()));
					__SymbolFormatter.ShowSymbolDeclaration(type, t, true, true);
					t.AddSymbol(type, false, __SymbolFormatter);
					info.Add(t);
				}
				qiContent.Add(info.Scrollable());
			}
		}

		void ShowTypeInfo(InfoContainer qiContent, SyntaxNode node, INamedTypeSymbol typeSymbol, SemanticModel semanticModel, CancellationToken cancellationToken) {
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
			if (typeSymbol.TypeKind == TypeKind.Class
				&& options.MatchFlags(QuickInfoOptions.MethodOverload)) {
				node = node.GetObjectCreationNode();
				if (node != null
					&& semanticModel.GetSymbolOrFirstCandidate(node, cancellationToken) is IMethodSymbol method) {
					ShowOverloadsInfo(qiContent, node, method, semanticModel, cancellationToken);
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
			if (typeSymbol.TypeKind == TypeKind.Enum) {
				if (options.MatchFlags(QuickInfoOptions.Enum)) {
					ShowEnumInfo(qiContent, typeSymbol, true);
				}
				else if (options.MatchFlags(QuickInfoOptions.BaseType)) {
					ShowEnumInfo(qiContent, typeSymbol, false);
				}
			}
			else if (options.MatchFlags(QuickInfoOptions.BaseType)) {
				ShowBaseType(qiContent, typeSymbol);
			}
			if (options.MatchFlags(QuickInfoOptions.Interfaces)) {
				ShowInterfaces(qiContent, typeSymbol);
			}
			if (options.MatchFlags(QuickInfoOptions.InterfaceMembers)
				&& typeSymbol.TypeKind == TypeKind.Interface) {
				var declarationType = node.FirstAncestorOrSelf<BaseListSyntax>()?.Parent;
				INamedTypeSymbol declaredClass = declarationType?.Kind()
					.IsAny(SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration, CodeAnalysisHelper.RecordDeclaration, CodeAnalysisHelper.RecordStructDeclaration) == true
					? semanticModel.GetDeclaredSymbol(declarationType, cancellationToken) as INamedTypeSymbol
					: null;
				ShowInterfaceMembers(qiContent, typeSymbol, declaredClass);
			}
		}

		static void ShowConstInfo(InfoContainer qiContent, ISymbol symbol, object value) {
			if (value is string sv) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.String)) {
					qiContent.Add(ShowStringInfo(sv, true));
				}
			}
			else if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues)) {
				var s = ToolTipHelper.ShowNumericForms(value);
				if (s != null) {
					if (symbol != null) {
						ShowEnumInfo(qiContent, symbol.ContainingType, false);
					}
					qiContent.Add(s);
				}
			}
		}

		static void ShowInterfaceImplementation<TSymbol>(InfoContainer qiContent, TSymbol symbol, IEnumerable<TSymbol> explicitImplementations)
			where TSymbol : class, ISymbol {
			if (symbol.DeclaredAccessibility != Accessibility.Public && explicitImplementations.Any() == false) {
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
		static void ShowInterfaceMembers(InfoContainer qiContent, INamedTypeSymbol type, INamedTypeSymbol declaredClass) {
			var doc = new ThemedTipDocument();
			doc.AppendTitle(IconIds.ListMembers, declaredClass != null ? R.T_MemberImplementation : R.T_Member);
			ShowInterfaceMembers(type, declaredClass, doc, false);
			foreach (var item in type.AllInterfaces) {
				ShowInterfaceMembers(item, declaredClass, doc, true);
			}
			if (doc.ParagraphCount > 1) {
				qiContent.Add(doc);
			}
		}

		static void ShowInterfaceMembers(INamedTypeSymbol type, INamedTypeSymbol declaredClass, ThemedTipDocument doc, bool isInherit) {
			var members = ImmutableArray.CreateBuilder<ISymbol>();
			members.AddRange(type.FindMembers());
			members.Sort(CodeAnalysisHelper.CompareByAccessibilityKindName);
			var isInterface = type.TypeKind == TypeKind.Interface;
			foreach (var member in members) {
				var t = new ThemedTipText();
				if (isInherit) {
					t.AddSymbol(type, false, SymbolFormatter.SemiTransparent).Append(".");
				}
				if (declaredClass != null && member.IsAbstract) {
					var implementation = declaredClass.FindImplementationForInterfaceMember(member);
					if (implementation != null) {
						t.AddSymbol(implementation, false, __SymbolFormatter);
					}
					else {
						t.AddSymbol(member, false, __SymbolFormatter)
							.Append(ThemeHelper.GetImage(IconIds.MissingImplementation).WrapMargin(WpfHelper.SmallHorizontalMargin).SetOpacity(WpfHelper.DimmedOpacity));
					}
				}
				else {
					t.AddSymbol(member, false, __SymbolFormatter);
				}
				if (member.Kind == SymbolKind.Method) {
					t.AddParameters(((IMethodSymbol)member).Parameters, __SymbolFormatter);
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

		static void ShowExtensionMethod(InfoContainer qiContent, IMethodSymbol method) {
			var info = new ThemedTipDocument()
				.AppendParagraph(IconIds.ExtensionMethod, new ThemedTipText(R.T_ExtendedBy, true).AddSymbolDisplayParts(method.ContainingType.ToDisplayParts(), __SymbolFormatter, -1));
			var extType = method.MethodKind == MethodKind.ReducedExtension ? method.ReceiverType : method.GetParameters()[0].Type;
			if (extType != null) {
				info.AppendParagraph(extType.GetImageId(), new ThemedTipText(R.T_Extending, true).AddSymbol(extType, true, __SymbolFormatter));
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
				},
				Margin = WpfHelper.MiddleBottomMargin
			};
			if (showText) {
				g.RowDefinitions.Add(new RowDefinition());
				g.Children.Add(new ThemedTipText(R.T_Text, true) { Margin = WpfHelper.GlyphMargin, TextAlignment = TextAlignment.Right }.SetValue(Grid.SetRow, 2));
				g.Children.Add(new ThemedTipText(sv) {
					Background = ThemeHelper.TextBoxBackgroundBrush.Alpha(0.5),
					Foreground = ThemeHelper.TextBoxBrush,
					Padding = WpfHelper.SmallHorizontalMargin,
					FontFamily = ThemeHelper.CodeTextFont
				}.WrapBorder(ThemeHelper.TextBoxBorderBrush, WpfHelper.TinyMargin).SetValue(Grid.SetRow, 2).SetValue(Grid.SetColumn, 1));
			}
			return g;
		}

		static void ShowBaseType(InfoContainer qiContent, ITypeSymbol typeSymbol) {
			var baseType = typeSymbol.BaseType;
			if (baseType == null || baseType.IsCommonClass()) {
				return;
			}
			var classList = new ThemedTipText(R.T_BaseType, true)
				.AddSymbol(baseType, null, __SymbolFormatter);
			var info = new ThemedTipDocument().Append(new ThemedTipParagraph(IconIds.BaseTypes, classList));
			while ((baseType = baseType.BaseType) != null) {
				if (baseType.IsCommonClass() == false) {
					classList.Inlines.Add(new ThemedTipText(" - ") { TextWrapping = TextWrapping.Wrap }.AddSymbol(baseType, null, __SymbolFormatter));
				}
			}
			qiContent.Add(info);
		}

		static void ShowEnumInfo(InfoContainer qiContent, INamedTypeSymbol type, bool showMembers) {
			var t = type.EnumUnderlyingType;
			if (t == null) {
				return;
			}
			var content = new ThemedTipText(R.T_EnumUnderlyingType, true).AddSymbol(t, true, __SymbolFormatter);
			var s = new ThemedTipDocument()
				.Append(new ThemedTipParagraph(IconIds.Enum, content));
			if (showMembers == false) {
				qiContent.Add(s);
				return;
			}
			bool isFlags = type.GetAttributes().Any(a => a.AttributeClass.MatchTypeName(nameof(FlagsAttribute), "System"));
			var c = 0;
			object min = null, max = null, bits = null;
			IFieldSymbol minName = null, maxName = null;
			Grid g = null;
			foreach (var f in type.FindMembers().OfType<IFieldSymbol>().Where(i => i.ConstantValue != null)) {
				var v = f.ConstantValue;
				if (min == null) {
					min = max = bits = v;
					minName = maxName = f;
					g = new Grid {
						HorizontalAlignment = HorizontalAlignment.Left,
						ColumnDefinitions = {
							new ColumnDefinition(),
							new ColumnDefinition()
						},
						Margin = WpfHelper.MiddleBottomMargin
					};
					goto NEXT;
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
			NEXT:
				if (c < 64) {
					g.RowDefinitions.Add(new RowDefinition());
					var ft = new ThemedTipText {
						TextAlignment = TextAlignment.Right,
						Foreground = ThemeHelper.SystemGrayTextBrush,
						Margin = WpfHelper.SmallHorizontalMargin,
						FontFamily = ThemeHelper.CodeTextFont
					}.Append("= ", ThemeHelper.SystemGrayTextBrush);
					SymbolFormatter.Instance.ShowFieldConstantText(ft.Inlines, f, isFlags);
					g.Add(new TextBlock { Foreground = ThemeHelper.ToolTipTextBrush }
							.AddSymbol(f, false, __SymbolFormatter)
							.SetGlyph(ThemeHelper.GetImage(IconIds.EnumField))
							.SetValue(Grid.SetRow, c))
						.Add(ft
							.SetValue(Grid.SetRow, c)
							.SetValue(Grid.SetColumn, 1));
				}
				else if (c == 64) {
					g.RowDefinitions.Add(new RowDefinition());
					g.Add(new ThemedTipText(R.T_More).SetValue(Grid.SetRow, c).SetValue(Grid.SetColumnSpan, 2));
				}
				++c;
			}
			if (min == null) {
				return;
			}
			content.AppendLine().Append(R.T_EnumFieldCount, true).Append(c.ToString());
				//.AppendLine().Append(R.T_EnumMin, true)
				//			.Append(min.ToString() + "(")
				//			.AddSymbol(minName, false, _SymbolFormatter)
				//			.Append(")")
				//		.AppendLine().Append(R.T_EnumMax, true)
				//			.Append(max.ToString() + "(")
				//			.AddSymbol(maxName, false, _SymbolFormatter)
				//			.Append(")");
			if (isFlags) {
				var d = Convert.ToString(Convert.ToInt64(bits), 2);
				content.AppendLine().Append(R.T_BitCount, true)
					.Append(d.Length.ToText())
					.AppendLine()
					.Append(R.T_EnumAllFlags, true)
					.Append(d);
			}
			qiContent.Add(s);
			if (g != null) {
				qiContent.Add(g);
			}
		}

		static void ShowInterfaces(InfoContainer qiContent, ITypeSymbol type) {
			type = type.OriginalDefinition;
			var interfaces = type.Interfaces;
			var declaredInterfaces = ImmutableArray.CreateBuilder<INamedTypeSymbol>(interfaces.Length);
			var inheritedInterfaces = ImmutableArray.CreateBuilder<(INamedTypeSymbol intf, ITypeSymbol baseType)>(5);
			foreach (var item in interfaces) {
				if (item.DeclaredAccessibility == Accessibility.Public || item.Locations.Any(l => l.IsInSource)) {
					declaredInterfaces.Add(item);
				}
			}
			HashSet<ITypeSymbol> all;
			switch (type.TypeKind) {
				case TypeKind.Class:
					all = new HashSet<ITypeSymbol>(interfaces);
					while ((type = type.BaseType) != null) {
						FindInterfacesForType(type, true, type.Interfaces, inheritedInterfaces, all);
					}
					foreach (var item in interfaces) {
						FindInterfacesForType(item, true, item.Interfaces, inheritedInterfaces, all);
					}
					break;
				case TypeKind.Interface:
					all = new HashSet<ITypeSymbol>(interfaces);
					foreach (var item in interfaces) {
						FindInterfacesForType(item, false, item.Interfaces, inheritedInterfaces, all);
					}
					FindInterfacesForType(type, false, type.Interfaces, inheritedInterfaces, all);
					break;
				case TypeKind.Struct:
					all = new HashSet<ITypeSymbol>(interfaces);
					foreach (var item in interfaces) {
						FindInterfacesForType(item, true, item.Interfaces, inheritedInterfaces, all);
					}
					break;
			}
			if (declaredInterfaces.Count == 0 && inheritedInterfaces.Count == 0) {
				return;
			}
			var info = new ThemedTipDocument().AppendTitle(IconIds.Interface, R.T_Interface);
			foreach (var item in declaredInterfaces) {
				info.Append(new ThemedTipParagraph(item.IsDisposable() ? IconIds.Disposable : item.GetImageId(), ToUIText(item)));
			}
			foreach (var (intf, baseType) in inheritedInterfaces) {
				info.Append(new ThemedTipParagraph(
					intf.IsDisposable() ? IconIds.Disposable : intf.GetImageId(),
					ToUIText(intf)
						.Append(" : ", SymbolFormatter.SemiTransparent.PlainText)
						.Append(ThemeHelper.GetImage(baseType.GetImageId()).WrapMargin(WpfHelper.GlyphMargin).SetOpacity(SymbolFormatter.TransparentLevel))
						.AddSymbol(baseType, false, SymbolFormatter.SemiTransparent)));
			}
			qiContent.Add(info);
		}

		static void FindInterfacesForType(ITypeSymbol type, bool useType, ImmutableArray<INamedTypeSymbol> interfaces, ImmutableArray<(INamedTypeSymbol, ITypeSymbol)>.Builder inheritedInterfaces, HashSet<ITypeSymbol> all) {
			foreach (var item in interfaces) {
				if (all.Add(item) && IsAccessibleInterface(item)) {
					inheritedInterfaces.Add((item, type));
					FindInterfacesForType(useType ? type : item, useType, item.Interfaces, inheritedInterfaces, all);
				}
			}
		}

		static bool IsAccessibleInterface(INamedTypeSymbol type) {
			return type.DeclaredAccessibility == Accessibility.Public || type.Locations.Any(l => l.IsInSource);
		}

		static void ShowDeclarationModifier(InfoContainer qiContent, ISymbol symbol) {
			qiContent.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(IconIds.DeclarationModifier, __SymbolFormatter.ShowSymbolDeclaration(symbol, new ThemedTipText(), true, false))));
		}

		static void ShowParameterInfo(InfoContainer qiContent, SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken) {
			var argument = node;
			if (node.IsKind(SyntaxKind.NullLiteralExpression)) {
				argument = node.Parent;
			}
			int depth = 0;
			do {
				var n = argument as ArgumentSyntax ?? (SyntaxNode)(argument as AttributeArgumentSyntax);
				if (n != null) {
					ShowArgumentInfo(qiContent, n, semanticModel, cancellationToken);
					return;
				}
			} while ((argument = argument.Parent) != null && ++depth < 4);
		}

		static void ShowArgumentInfo(InfoContainer qiContent, SyntaxNode argument, SemanticModel semanticModel, CancellationToken cancellationToken) {
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
			var symbol = semanticModel.GetSymbolInfo(argList.Parent, cancellationToken);
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
					.AddSymbol(om.ReturnType, om.MethodKind == MethodKind.Constructor ? "new" : null, __SymbolFormatter)
					.Append(" ")
					.AddSymbol(om.MethodKind != MethodKind.DelegateInvoke ? om : (ISymbol)om.ContainingType, true, __SymbolFormatter)
					.AddParameters(om.Parameters, __SymbolFormatter, argIndex);
				var info = new ThemedTipDocument().Append(new ThemedTipParagraph(IconIds.Argument, content));
				if (paramDoc != null) {
					content.Append("\n" + argName, true, false, __SymbolFormatter.Parameter)
						.Append(": ")
						.AddXmlDoc(paramDoc, new XmlDocRenderer(semanticModel.Compilation, __SymbolFormatter));
				}
				if (m.IsGenericMethod) {
					for (int i = 0; i < m.TypeArguments.Length; i++) {
						content.Append("\n");
						__SymbolFormatter.ShowTypeArgumentInfo(m.TypeParameters[i], m.TypeArguments[i], content);
						var typeParamDoc = doc.GetTypeParameter(m.TypeParameters[i].Name);
						if (typeParamDoc != null) {
							content.Append(": ")
								.AddXmlDoc(typeParamDoc, new XmlDocRenderer(semanticModel.Compilation, __SymbolFormatter));
						}
					}
				}
				if (p?.Type.TypeKind == TypeKind.Delegate) {
					var invoke = ((INamedTypeSymbol)p.Type).DelegateInvokeMethod;
					info.Append(new ThemedTipParagraph(IconIds.Delegate,
						new ThemedTipText(R.T_DelegateSignature, true).Append(": ")
							.AddSymbol(invoke.ReturnType, false, __SymbolFormatter)
							.Append(" ").Append(p.Name, true, false, __SymbolFormatter.Parameter)
							.AddParameters(invoke.Parameters, __SymbolFormatter)
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
							new ThemedTipText().Append(R.T_AttributeOf).Append(p.Name, true, false, __SymbolFormatter.Parameter).Append(":")
						);
						foreach (var attr in attrs) {
							__SymbolFormatter.Format(para.Content.AppendLine().Inlines, attr, 0);
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
								__SymbolFormatter,
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

		static void ShowAnonymousTypeInfo(InfoContainer container, ISymbol symbol) {
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

			void ShowAnonymousTypes(InfoContainer c, ImmutableArray<ITypeSymbol>.Builder anonymousTypes, ISymbol currentSymbol) {
				const string AnonymousNumbers = "abcdefghijklmnopqrstuvwxyz";
				var d = new ThemedTipDocument().AppendTitle(IconIds.AnonymousType, R.T_AnonymousType);
				for (var i = 0; i < anonymousTypes.Count; i++) {
					var type = anonymousTypes[i];
					var content = new ThemedTipText()
						.AddSymbol(type, "'" + AnonymousNumbers[i], __SymbolFormatter)
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
						content.AddSymbol(pt, alias, __SymbolFormatter)
							.Append(" ")
							.AddSymbol(m, m == currentSymbol, __SymbolFormatter)
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
			return new ThemedTipText().AddSymbolDisplayParts(symbol.ToDisplayParts(CodeAnalysisHelper.QuickInfoSymbolDisplayFormat), __SymbolFormatter, -1);
		}

		void IDisposable.Dispose() { }

		sealed class SpecialProjectInfo
		{
			public readonly bool IsCodist;
			public readonly bool MaybeVsProject;

			public SpecialProjectInfo(SemanticModel model) {
				IsCodist = model.GetTypeSymbol(nameof(IconIds), nameof(Codist)) != null;
				MaybeVsProject = model.GetNamespaceSymbol("Microsoft", "VisualStudio", "PlatformUI") != null || model.GetTypeSymbol(nameof(VsColors), "Microsoft", "VisualStudio", "Shell") != null;
			}
		}
	}
}
