using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CLR;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using R = Codist.Properties.Resources;
using Task = System.Threading.Tasks.Task;

namespace Codist.QuickInfo
{
	sealed partial class CSharpQuickInfo : SingletonQuickInfoSource
	{
		internal const string Name = nameof(CSharpQuickInfo);

		static readonly SymbolFormatter __SymbolFormatter = SymbolFormatter.Instance;

		SpecialProjectInfo _SpecialProject;
		bool _IsCandidate;

		protected override Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			// Map the trigger point down to our buffer.
			var buffer = session.GetSourceBuffer(out var triggerPoint);
			return buffer != null ? InternalGetQuickInfoItemAsync(session, buffer, triggerPoint, cancellationToken)
				: Task.FromResult<QuickInfoItem>(null);
		}

		async Task<QuickInfoItem> InternalGetQuickInfoItemAsync(IAsyncQuickInfoSession session, ITextBuffer textBuffer, SnapshotPoint triggerPoint, CancellationToken cancellationToken) {
			ISymbol symbol;
			SyntaxNode node;
			ImmutableArray<ISymbol> candidates;
			SyntaxToken token;
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			if (QuickInfoOverride.CheckCtrlSuppression()) {
				return null;
			}
			var ctx = SemanticContext.GetHovered();
			await ctx.UpdateAsync(textBuffer, cancellationToken);
			var semanticModel = ctx.SemanticModel;
			if (semanticModel == null) {
				return null;
			}
			var currentSnapshot = textBuffer.CurrentSnapshot;
			if (_SpecialProject == null) {
				_SpecialProject = new SpecialProjectInfo(semanticModel);
			}
			var unitCompilation = semanticModel.SyntaxTree.GetCompilationUnitRoot(cancellationToken);

			//look for occurrences of our QuickInfo words in the span
			token = unitCompilation.FindToken(triggerPoint, true);
			var skipTriggerPointCheck = false;
			var isConvertedType = false;
			symbol = null;
			// the Quick Info override
			var o = Config.Instance.QuickInfoOptions.HasAnyFlag(QuickInfoOptions.QuickInfoOverride)
				? QuickInfoOverride.CreateOverride(session)
				: null;
			var container = new InfoContainer();
			ObjectCreationExpressionSyntax ctor = null;
		ClassifyToken:
			switch (token.Kind()) {
				case SyntaxKind.WhitespaceTrivia:
				case SyntaxKind.SingleLineCommentTrivia:
				case SyntaxKind.MultiLineCommentTrivia:
					return null;
				case SyntaxKind.OpenBraceToken:
					if ((node = unitCompilation.FindNode(token.Span))
						.Kind().CeqAny(SyntaxKind.ArrayInitializerExpression,
							SyntaxKind.CollectionInitializerExpression,
							SyntaxKind.ComplexElementInitializerExpression,
							SyntaxKind.ObjectInitializerExpression,
							CodeAnalysisHelper.WithInitializerExpression)) {
						container.Add(new ThemedTipText()
							.SetGlyph(ThemeHelper.GetImage(IconIds.InstanceMember))
							.Append(R.T_ExpressionCount)
							.Append((node as InitializerExpressionSyntax).Expressions.Count.ToText(), true, false, __SymbolFormatter.Number));
					}
					else if (node.IsKind(SyntaxKind.Interpolation)) {
						symbol = semanticModel.Compilation.GetSpecialType(SpecialType.System_String);
						isConvertedType = true;
						goto PROCESS;
					}
					if (o != null) {
						o.OverrideBuiltInXmlDoc = false;
					}
					ShowBlockInfo(container, currentSnapshot, node, semanticModel);
					goto RETURN;
				case SyntaxKind.CloseBraceToken:
					if (o != null) {
						o.OverrideBuiltInXmlDoc = false;
					}
					if ((node = unitCompilation.FindNode(token.Span)).IsKind(SyntaxKind.Interpolation)) {
						goto case SyntaxKind.CommaToken;
					}
					ShowBlockInfo(container, currentSnapshot, node, semanticModel);
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
					symbol = semanticModel.GetTypeInfo(unitCompilation.FindNode(token.Span, false, true), cancellationToken).ConvertedType;
					isConvertedType = true;
					break;
				case SyntaxKind.EqualsToken:
					symbol = semanticModel.GetTypeInfo(unitCompilation.FindNode(token.GetPreviousToken().Span), cancellationToken).ConvertedType;
					isConvertedType = true;
					break;
				case SyntaxKind.NullKeyword:
				case SyntaxKind.DefaultKeyword:
				case SyntaxKind.SwitchKeyword:
				case SyntaxKind.QuestionToken:
				case SyntaxKind.ColonToken:
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
					var asType = (unitCompilation.FindNode(token.Span, false, true) as BinaryExpressionSyntax)?.GetLastIdentifier();
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
				case SyntaxKind.SemicolonToken:
					token = token.GetPreviousToken();
					skipTriggerPointCheck = true;
					goto ClassifyToken;
				case SyntaxKind.OpenBracketToken:
				case SyntaxKind.CloseBracketToken:
					if ((node = unitCompilation.FindNode(token.Span, false, true)).IsKind(SyntaxKind.BracketedArgumentList)
						&& node.Parent.IsKind(SyntaxKind.ElementAccessExpression)) {
						symbol = semanticModel.GetSymbolInfo((ElementAccessExpressionSyntax)node.Parent, cancellationToken).Symbol;
					}
					else if (node.IsKind(CodeAnalysisHelper.CollectionExpression)) {
						symbol = semanticModel.GetTypeInfo(node, cancellationToken).ConvertedType;
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
				case SyntaxKind.NewKeyword:
				case SyntaxKind.StackAllocKeyword:
					symbol = semanticModel.GetTypeInfo(unitCompilation.FindNode(token.Span, false, true), cancellationToken).Type;
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
					if (token.Span.Contains(triggerPoint, true) == false
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
				|| skipTriggerPointCheck == false && node.Span.Contains(triggerPoint.Position, true) == false) {
				return null;
			}
			node = node.UnqualifyExceptNamespace();
			switch (node.Kind()) {
				case SyntaxKind.Argument:
				case SyntaxKind.ArgumentList:
					LocateNodeInParameterList(ref node, ref token);
					break;
				case SyntaxKind.LetClause:
				case SyntaxKind.JoinClause:
				case SyntaxKind.JoinIntoClause:
					if (node.GetIdentifierToken().FullSpan == token.FullSpan) {
						symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken);
					}
					break;
				case SyntaxKind.SkippedTokensTrivia:
					return null;
			}

		PROCESS:
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter)) {
				ShowParameterInfo(container, node, semanticModel, cancellationToken);
			}
			if (symbol == null) {
				symbol = token.IsKind(SyntaxKind.CloseBraceToken) ? null
				: GetSymbol(semanticModel, node, ref candidates, cancellationToken);
			}
			if (_IsCandidate = candidates.IsDefaultOrEmpty == false) {
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
						else if (node.IsAnyKind(SyntaxKind.Block, SyntaxKind.SwitchStatement)) {
							ShowBlockInfo(container, currentSnapshot, node, semanticModel);
						}
						isConvertedType = true;
						break;
					case SyntaxKind.NumericLiteralToken:
						symbol = semanticModel.GetSystemTypeSymbol(token.Value.GetType().Name);
						isConvertedType = true;
						break;
					default:
						if (node.IsAnyKind(SyntaxKind.Block, SyntaxKind.SwitchStatement)) {
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
					container.Add(await ShowAvailabilityAsync(ctx.Document, token, cancellationToken).ConfigureAwait(false));
				}
				ctor = node.Parent.UnqualifyExceptNamespace() as ObjectCreationExpressionSyntax;
				OverrideDocumentation(node,
					o,
					ctor != null
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
			if (ctor == null) {
				ctor = node.Parent.UnqualifyExceptNamespace() as ObjectCreationExpressionSyntax;
			}
			if (ctor != null) {
				symbol = semanticModel.GetSymbolOrFirstCandidate(ctor, cancellationToken) ?? symbol;
				if (symbol == null) {
					return null;
				}
				if (symbol.IsImplicitlyDeclared) {
					symbol = symbol.ContainingType;
				}
			}
			o?.ApplyClickAndGo(symbol);
			if (container.ItemCount == 0) {
				if (symbol != null) {
					// place holder
					container.Add(new ContentPresenter() { Name = "SymbolPlaceHolder" });
				}
				return null;
			}
			return CreateQuickInfoItem(session, token, container.ToUI().Tag());
		}

		static QuickInfoItem CreateQuickInfoItem(IAsyncQuickInfoSession session, SyntaxToken? token, object item) {
			session.KeepViewPosition();
			return new QuickInfoItem(token?.Span.CreateSnapshotSpan(session.TextView.TextSnapshot).ToTrackingSpan(), item);
		}

		static Task<ThemedTipDocument> ShowAvailabilityAsync(Document doc, SyntaxToken token, CancellationToken cancellationToken) {
			var solution = doc.Project.Solution;
			ImmutableArray<DocumentId> linkedDocuments;
			return solution.ProjectIds.Count == 0 || (linkedDocuments = doc.GetLinkedDocumentIds()).Length == 0
				? Task.FromResult<ThemedTipDocument>(null)
				: ShowAvailabilityAsync(token, solution, linkedDocuments, cancellationToken);
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
			var kind = node.Kind();
			if (kind.CeqAny(SyntaxKind.BaseExpression, SyntaxKind.DefaultLiteralExpression, SyntaxKind.ImplicitStackAllocArrayCreationExpression)) {
				return semanticModel.GetTypeInfo(node, cancellationToken).ConvertedType;
			}
			if (kind.CeqAny(SyntaxKind.ThisExpression, CodeAnalysisHelper.VarPattern)) {
				return semanticModel.GetTypeInfo(node, cancellationToken).Type;
			}
			if (kind.CeqAny(SyntaxKind.TupleElement, SyntaxKind.ForEachStatement, SyntaxKind.FromClause, SyntaxKind.QueryContinuation)) {
				return semanticModel.GetDeclaredSymbol(node, cancellationToken);
			}
			if (node is QueryClauseSyntax q) {
				if (node is OrderByClauseSyntax o && o.Orderings.Count != 0) {
					return semanticModel.GetSymbolInfo(o.Orderings[0], cancellationToken).Symbol;
				}
				return semanticModel.GetQueryClauseInfo(q, cancellationToken).OperationInfo.Symbol;
			}
			var symbolInfo = semanticModel.GetSymbolInfo(node, cancellationToken);
			if (symbolInfo.CandidateReason != CandidateReason.None) {
				candidates = symbolInfo.CandidateSymbols;
				return symbolInfo.CandidateSymbols.FirstOrDefault();
			}
			return symbolInfo.Symbol
				?? (kind.IsDeclaration()
						|| kind == SyntaxKind.VariableDeclarator
						|| kind == SyntaxKind.SingleVariableDesignation
							&& node.Parent.IsAnyKind(SyntaxKind.DeclarationExpression, SyntaxKind.DeclarationPattern, SyntaxKind.ParenthesizedVariableDesignation)
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

		static ThemedTipDocument OverrideDocumentation(SyntaxNode node, IQuickInfoOverride qiWrapper, ISymbol symbol, SemanticModel semanticModel, CancellationToken cancellationToken) {
			if (symbol == null) {
				return null;
			}
			var ms = symbol as IMethodSymbol;
			if (symbol.Kind == SymbolKind.Method && ms?.IsAccessor() == true) {
				// hack: symbol could be Microsoft.CodeAnalysis.CSharp.Symbols.SourceMemberFieldSymbolFromDeclarator which is not IMethodSymbol
				symbol = ms.AssociatedSymbol;
			}
			symbol = symbol.GetAliasTarget();
			if (symbol is IFieldSymbol f && f.CorrespondingTupleField != null) {
				symbol = f.CorrespondingTupleField;
			}
			var compilation = semanticModel.Compilation;
			var doc = new XmlDoc(symbol, compilation);
			var docRenderer = new XmlDocRenderer(compilation, SymbolFormatter.Instance);
			var tip = docRenderer.RenderXmlDoc(symbol, doc);

			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ExceptionDoc)
				&& symbol.Kind.CeqAny(SymbolKind.Method, SymbolKind.Property, SymbolKind.Event)) {
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
						qiContent.Add(ColorQuickInfoUI.PreviewColorProperty(symbol as IPropertySymbol, _SpecialProject.MayBeVsProject));
					}
					break;
				case SymbolKind.Namespace:
					ShowNamespaceInfo(qiContent, symbol as INamespaceSymbol);
					break;
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
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.SymbolLocation)) {
				ShowSymbolLocationInfo(qiContent, semanticModel.Compilation, symbol);
			}
		}

		static void ShowSymbolLocationInfo(InfoContainer qiContent, Compilation compilation, ISymbol symbol) {
			var (p, f) = compilation.GetReferencedAssemblyPath(symbol as IAssemblySymbol ?? symbol.ContainingAssembly);
			if (String.IsNullOrEmpty(f)) {
				return;
			}
			var asmText = new ThemedTipText(R.T_Assembly, true);
			var item = new ThemedTipDocument { Name = "SymbolLocation" }
				.AppendParagraph(IconIds.Module, asmText);
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

		static void ShowMiscInfo(InfoContainer qiContent, SyntaxNode node) {
			Grid infoBox = null;
			var nodeKind = node.Kind();
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues)
				&& nodeKind.CeqAny(SyntaxKind.NumericLiteralExpression, SyntaxKind.CharacterLiteralExpression)) {
				infoBox = ToolTipHelper.ShowNumericRepresentations(node);
			}
			else if (nodeKind == SyntaxKind.SwitchStatement) {
				var s = ((SwitchStatementSyntax)node).Sections.Count;
				if (s > 1) {
					var cases = 0;
					foreach (var section in ((SwitchStatementSyntax)node).Sections) {
						cases += section.Labels.Count;
					}
					qiContent.Add(new ThemedTipText($"{s} switch sections, {cases} cases").SetGlyph(ThemeHelper.GetImage(IconIds.Switch)));
				}
				else if (s == 1) {
					s = ((SwitchStatementSyntax)node).Sections[0].Labels.Count;
					if (s > 1) {
						qiContent.Add(new ThemedTipText($"1 switch section, {s} cases").SetGlyph(ThemeHelper.GetImage(IconIds.Switch)));
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
					&& nodeKind.CeqAny(SyntaxKind.SimpleLambdaExpression, SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.LocalFunctionStatement) == false) {
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
				if (field.ConstantValue is int fc && _SpecialProject.MayBeVsProject) {
					ShowKnownImageId(qiContent, field, fc);
				}
				ShowConstInfo(qiContent, field, field.ConstantValue);
			}
			else if (field.IsReadOnly && field.IsStatic && field.Type.Name == "OpCode") {
				ShowOpCodeInfo(qiContent, field);
			}

			void ShowKnownImageId(InfoContainer qc, IFieldSymbol f, int fieldValue) {
				var t = f.ContainingType;
				if (t.MatchTypeName(nameof(Microsoft.VisualStudio.Imaging.KnownImageIds), "Imaging", "VisualStudio", "Microsoft")
					|| t.MatchTypeName(nameof(IconIds), nameof(Codist))) {
					qc.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(fieldValue, new ThemedTipText(f.Name))));
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
			if (options.MatchFlags(QuickInfoOptions.MethodOverload) && _IsCandidate == false) {
				ShowOverloadsInfo(qiContent, node, method, semanticModel, cancellationToken);
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
					&& semanticModel.GetSymbolOrFirstCandidate(node, cancellationToken) is IMethodSymbol method
					&& _IsCandidate == false) {
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
			if (typeSymbol.TypeKind == TypeKind.Enum && options.HasAnyFlag(QuickInfoOptions.BaseType | QuickInfoOptions.Enum)) {
				ShowEnumQuickInfo(qiContent, typeSymbol, options.MatchFlags(QuickInfoOptions.BaseType), options.MatchFlags(QuickInfoOptions.Enum));
			}
			else if (options.MatchFlags(QuickInfoOptions.BaseType)) {
				ShowBaseType(qiContent, typeSymbol);
			}
			if (options.MatchFlags(QuickInfoOptions.Interfaces)) {
				ShowInterfaces(qiContent, typeSymbol);
			}
			if (options.MatchFlags(QuickInfoOptions.InterfaceMembers)
				&& typeSymbol.TypeKind == TypeKind.Interface) {
				var declarationType = (node.Parent.UnqualifyExceptNamespace().Parent as BaseListSyntax)?.Parent;
				var declaredClass = declarationType?.Kind()
					.CeqAny(SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration, CodeAnalysisHelper.RecordDeclaration, CodeAnalysisHelper.RecordStructDeclaration) == true
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
				var s = ToolTipHelper.ShowNumericRepresentations(value);
				if (s != null) {
					if (symbol?.ContainingType?.TypeKind == TypeKind.Enum) {
						ShowEnumQuickInfo(qiContent, symbol.ContainingType, Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseType), false);
					}
					qiContent.Add(s);
				}
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
			if (baseType == null || baseType.IsCommonBaseType()) {
				return;
			}
			var classList = new ThemedTipText(R.T_BaseType, true)
				.AddSymbol(baseType, null, __SymbolFormatter);
			var info = new ThemedTipDocument().Append(new ThemedTipParagraph(IconIds.BaseTypes, classList));
			while ((baseType = baseType.BaseType) != null) {
				if (baseType.IsCommonBaseType() == false) {
					classList.Inlines.Add(new ThemedTipText(" - ") { TextWrapping = TextWrapping.Wrap }.AddSymbol(baseType, null, __SymbolFormatter));
				}
			}
			qiContent.Add(info);
		}

		static void ShowDeclarationModifier(InfoContainer qiContent, ISymbol symbol) {
			qiContent.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(IconIds.DeclarationModifier, __SymbolFormatter.ShowSymbolDeclaration(symbol, new ThemedTipText(), true, false))));
		}

		static TextBlock ToUIText(ISymbol symbol) {
			return new ThemedTipText().AddSymbolDisplayParts(symbol.ToDisplayParts(CodeAnalysisHelper.QuickInfoSymbolDisplayFormat), __SymbolFormatter, -1);
		}

		sealed class SpecialProjectInfo
		{
			readonly SemanticModel _Model;
			int _MayBeVsProject;

			public bool MayBeVsProject => _MayBeVsProject != 0
				? _MayBeVsProject == 1
				: (_MayBeVsProject = _Model.GetNamespaceSymbol("Microsoft", "VisualStudio", "PlatformUI") != null || _Model.GetTypeSymbol(nameof(VsColors), "Microsoft", "VisualStudio", "Shell") != null ? 1 : -1) == 1;

			public SpecialProjectInfo(SemanticModel model) {
				_Model = model;
			}
		}
	}
}
