using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using CLR;
using Codist.SyntaxHighlight;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
{
	sealed partial class CSharpTagger : ITagger<IClassificationTag>, IDisposable
	{
		delegate void TaggerDelegate(in SyntaxToken token, Context ctx);
		static readonly CSharpClassifications __Classifications = CSharpClassifications.Instance;
		static readonly GeneralClassifications __GeneralClassifications = GeneralClassifications.Instance;
		static readonly UrlClassifications __UrlClassifications = UrlClassifications.Instance;

		ConcurrentQueue<SnapshotSpan> _PendingSpans = new ConcurrentQueue<SnapshotSpan>();
		CancellationTokenSource _RenderBreaker;
		int _Ref;
		ITextBufferParser _Parser;

		public CSharpTagger(CSharpParser parser, ITextBuffer buffer) {
			_Parser = parser.GetParser(buffer);
			_Parser.StateUpdated += HandleParseResult;
			Ref();
		}

		public bool Disabled { get; set; }
		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		void HandleParseResult(object sender, EventArgs<SemanticState> result) {
			var pendingSpans = _PendingSpans;
			if (pendingSpans != null) {
				var snapshot = result.Data.Snapshot;
				if (snapshot != null) {
					while (pendingSpans.TryDequeue(out var span)) {
						if (snapshot == span.Snapshot) {
							$"Refresh span {span}".Log(LogCategory.SyntaxHighlight);
							TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
						}
					}
				}
			}
		}

		public IEnumerable<ITagSpan<IClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
			var p = _Parser;
			if (p == null || Disabled) {
				return Enumerable.Empty<ITagSpan<IClassificationTag>>();
			}
			if (p.TryGetSemanticState(spans[0].Snapshot, out var r)) {
				return Logic.GetTags(spans, r, SyncHelper.CancelAndRetainToken(ref _RenderBreaker));
			}
			foreach (var item in spans) {
				$"Enqueue span {item}".Log(LogCategory.SyntaxHighlight);
				_PendingSpans.Enqueue(item);
			}
			return r == null
				? Enumerable.Empty<ITagSpan<IClassificationTag>>()
				: UseOldResult(spans, spans[0].Snapshot, r, SyncHelper.CancelAndRetainToken(ref _RenderBreaker));
		}

		static IEnumerable<ITagSpan<IClassificationTag>> UseOldResult(NormalizedSnapshotSpanCollection spans, ITextSnapshot snapshot, SemanticState result, CancellationToken cancellationToken) {
			foreach (var tagSpan in Logic.GetTags(MapToOldSpans(spans, result.Snapshot), result, cancellationToken)) {
				yield return new TagSpan<IClassificationTag>(tagSpan.Span.TranslateTo(snapshot, SpanTrackingMode.EdgeInclusive), tagSpan.Tag);
			}
		}

		static IEnumerable<SnapshotSpan> MapToOldSpans(NormalizedSnapshotSpanCollection spans, ITextSnapshot last) {
			foreach (var item in spans) {
				yield return item.TranslateTo(last, SpanTrackingMode.EdgeInclusive);
			}
		}

		public void Ref() {
			++_Ref;
		}

		public void Dispose() {
			if (_Ref-- > 0) {
				return;
			}
			_PendingSpans = null;
			_RenderBreaker.CancelAndDispose();
			ITextBufferParser t = _Parser;
			if (t != null) {
				t.Dispose();
				_Parser = null;
			}
		}

		sealed class Context
		{
			public readonly SemanticModel semanticModel;
			public readonly CompilationUnitSyntax compilationUnit;
			public readonly CancellationToken cancellationToken;
			public TagHolder Tags;

			public Context(SemanticModel semanticModel, CompilationUnitSyntax compilationUnit, TagHolder tags, CancellationToken cancellationToken) {
				this.semanticModel = semanticModel;
				this.compilationUnit = compilationUnit;
				this.cancellationToken = cancellationToken;
				Tags = tags;
			}
		}

		static class Logic
		{
			#region cache tag results for reusing among subsequent calls for the same spans
			static readonly WeakReference<IEnumerable<SnapshotSpan>> __LastSpans = new WeakReference<IEnumerable<SnapshotSpan>>(null);
			static TagHolder __LastTags;
			static WeakReference<SyntaxNode> __RecentXmlDoc;
			#endregion

			public static IEnumerable<ITagSpan<IClassificationTag>> GetTags(IEnumerable<SnapshotSpan> spans, SemanticState semantic, CancellationToken cancellationToken) {
				try {
					if (__LastSpans.TryGetTarget(out var lastSpans) && lastSpans == spans) {
						return __LastTags;
					}
					__LastSpans.SetTarget(spans);
					return __LastTags = GetTagsInternal(spans, semantic, cancellationToken);
				}
				catch (OperationCanceledException) {
					__LastSpans.SetTarget(null);
					return Enumerable.Empty<ITagSpan<IClassificationTag>>();
				}
			}

			static TagHolder GetTagsInternal(IEnumerable<SnapshotSpan> spans, SemanticState semantic, CancellationToken cancellationToken) {
				var semanticModel = semantic.Model;
				var compilationUnit = semantic.GetCompilationUnit(cancellationToken);
				var l = semanticModel.SyntaxTree.Length;
				var tags = new TagHolder(semantic.Snapshot);
				var ctx = new Context(semanticModel, compilationUnit, tags, cancellationToken);
				var prober = AttributeListProber.Instance;
				SyntaxNode attrs;
				foreach (var span in spans) {
					if (span.End > l || cancellationToken.IsCancellationRequested) {
						return tags;
					}
					var textSpan = new TextSpan(span.Start.Position, span.Length);
					foreach (var token in compilationUnit.DescendantTokens(textSpan, FormatStore.TagAttributeAnnotation ? prober.Probe : null)) {
						if (textSpan.Contains(token.SpanStart) == false) {
							if (token.HasLeadingTrivia) {
								TagXmlDocTokens(token.LeadingTrivia, textSpan, ctx);
							}
							if (FormatStore.TagUrl
								&& token.IsAnyKind(CodeAnalysisHelper.MultiLineRawStringLiteralToken, CodeAnalysisHelper.InterpolatedMultiLineRawStringStartToken)) {
								var s = span.Intersection(token.Span.ToSpan());
								if (s.HasValue) {
									TokenTaggers.TagUrlParts(ctx, s.Value.GetText(), 0, 0, s.Value.Start, StringKind.Raw);
								}
							}
							continue;
						}
						TokenTaggers.Tag(in token, ctx);
					}
					if (FormatStore.TagAttributeAnnotation
						&& (attrs = prober.FetchTarget()) != null
						&& attrs.Span.Contains(textSpan)) {
						tags.Add(span.TrimWhitespace(), __Classifications.AttributeNotation);
					}
				}
				return tags;
			}

			static void TagXmlDocTokens(SyntaxTriviaList leadingTrivia, TextSpan textSpan, Context ctx) {
				foreach (var trivia in leadingTrivia) {
					if (trivia.HasStructure == false) {
						continue;
					}
					if (__RecentXmlDoc?.TryGetTarget(out SyntaxNode docNode) != true
						|| docNode.SyntaxTree != trivia.SyntaxTree
						|| !docNode.FullSpan.Contains(trivia.FullSpan)) {
						__RecentXmlDoc = new WeakReference<SyntaxNode>(docNode = trivia.GetStructure());
					}
					if (docNode.IsAnyKind(SyntaxKind.SingleLineDocumentationCommentTrivia, SyntaxKind.MultiLineDocumentationCommentTrivia)) {
						ctx.Tags.Add(docNode.FullSpan, __Classifications.XmlDoc);
					}
					foreach (var token in docNode.DescendantTokens()) {
						if (textSpan.Contains(token.FullSpan) == false) {
							continue;
						}
						if (token.IsKind(SyntaxKind.IdentifierToken)) {
							if (token.Parent.IsAnyKind(SyntaxKind.IdentifierName, SyntaxKind.GenericName)) {
								TokenTaggers.TagIdentifier(in token, ctx);
							}
						}
						else if (token.IsKind(SyntaxKind.XmlTextLiteralToken)) {
							TokenTaggers.Tag(in token, ctx);
						}
					}
				}
			}
		}

		static class TokenTaggers {
			static readonly Dictionary<SyntaxKind, TaggerDelegate> __TokenTaggers = new Dictionary<SyntaxKind, TaggerDelegate> {
				{ SyntaxKind.AbstractKeyword, TagAbstractionKeyword },
				{ SyntaxKind.SealedKeyword, TagAbstractionKeyword },
				{ SyntaxKind.OverrideKeyword, TagAbstractionKeyword },
				{ SyntaxKind.VirtualKeyword, TagAbstractionKeyword },
				{ SyntaxKind.ProtectedKeyword, TagAbstractionKeyword },
				{ SyntaxKind.NewKeyword, TagAbstractionKeyword },
				{ SyntaxKind.ThisKeyword, TagThisKeyword },
				{ SyntaxKind.ExplicitKeyword, TagTypeCastDeclaration },
				{ SyntaxKind.ImplicitKeyword, TagTypeCastDeclaration },
				{ CodeAnalysisHelper.RecordKeyword, TagRecordDeclaration },
				{ CodeAnalysisHelper.ExtensionKeyword, TagExtensionDeclaration },
				{ SyntaxKind.BreakKeyword, TagBreakKeyword },
				{ SyntaxKind.AwaitKeyword, TagAwaitKeyword },
				{ SyntaxKind.GotoKeyword, TagControlFlowKeyword },
				{ SyntaxKind.ReturnKeyword, TagControlFlowKeyword },
				{ SyntaxKind.ContinueKeyword, TagControlFlowKeyword },
				{ SyntaxKind.ThrowKeyword, TagControlFlowKeyword },
				{ SyntaxKind.YieldKeyword, TagControlFlowKeyword },
				{ SyntaxKind.IfKeyword, TagBranchingKeyword },
				{ SyntaxKind.ElseKeyword, TagBranchingKeyword },
				{ SyntaxKind.SwitchKeyword, TagBranchingKeyword },
				{ SyntaxKind.CaseKeyword, TagBranchingKeyword },
				{ SyntaxKind.WhenKeyword, TagBranchingKeyword },
				{ SyntaxKind.DefaultKeyword, TagDefaultKeyword },
				{ SyntaxKind.WhileKeyword, TagLoopKeyword },
				{ SyntaxKind.ForKeyword, TagLoopKeyword },
				{ SyntaxKind.ForEachKeyword, TagLoopKeyword },
				{ SyntaxKind.DoKeyword, TagLoopKeyword },
				{ SyntaxKind.SelectKeyword, TagLoopKeyword },
				{ SyntaxKind.FromKeyword, TagLoopKeyword },
				{ SyntaxKind.UnsafeKeyword, TagResourceKeyword },
				{ SyntaxKind.FixedKeyword, TagResourceKeyword },
				{ SyntaxKind.LockKeyword, TagResourceKeyword },
				{ SyntaxKind.TryKeyword, TagResourceKeyword },
				{ SyntaxKind.CatchKeyword, TagResourceKeyword },
				{ SyntaxKind.FinallyKeyword, TagResourceKeyword },
				{ SyntaxKind.StackAllocKeyword, TagResourceKeyword },
				{ CodeAnalysisHelper.ManagedKeyword, TagResourceKeyword },
				{ CodeAnalysisHelper.UnmanagedKeyword, TagResourceKeyword },
				{ SyntaxKind.TildeToken, TagResourceKeyword },
				{ SyntaxKind.UsingKeyword, TagUsingKeyword },
				{ SyntaxKind.IsKeyword, TagTypeCastKeyword },
				{ SyntaxKind.AsKeyword, TagTypeCastKeyword },
				{ SyntaxKind.CheckedKeyword, TagTypeCastKeyword },
				{ SyntaxKind.UncheckedKeyword, TagTypeCastKeyword },
				{ SyntaxKind.RefKeyword, TagTypeCastKeyword },
				{ SyntaxKind.InKeyword, TagInKeyword },
				{ SyntaxKind.OutKeyword, TagInTypeCastKeyword },
				{ SyntaxKind.ReadOnlyKeyword, TagInTypeCastKeyword },
				{ SyntaxKind.OpenBraceToken, TagSemanticBrace },
				{ SyntaxKind.CloseBraceToken, TagSemanticBrace },
				{ SyntaxKind.OpenParenToken, TagSemanticParenthesis },
				{ SyntaxKind.CloseParenToken, TagSemanticParenthesis },
				{ SyntaxKind.OpenBracketToken, TagSemanticBracket },
				{ SyntaxKind.CloseBracketToken, TagSemanticBracket },
				{ SyntaxKind.EqualsGreaterThanToken, TagEqualsGreaterThenToken },
				{ SyntaxKind.EqualsToken, TagEqualsToken },
				{ SyntaxKind.PlusToken, TagOperatorToken },
				{ SyntaxKind.MinusToken, TagOperatorToken },
				{ SyntaxKind.AsteriskToken, TagOperatorToken },
				{ SyntaxKind.SlashToken, TagOperatorToken },
				{ SyntaxKind.ExclamationToken, TagOperatorToken },
				{ SyntaxKind.PercentToken, TagOperatorToken },
				{ SyntaxKind.AmpersandToken, TagOperatorToken },
				{ SyntaxKind.BarToken, TagOperatorToken },
				{ SyntaxKind.CaretToken, TagOperatorToken },
				{ SyntaxKind.LessThanToken, TagOperatorToken },
				{ SyntaxKind.LessThanLessThanToken, TagOperatorToken },
				{ SyntaxKind.GreaterThanToken, TagOperatorToken },
				{ SyntaxKind.GreaterThanGreaterThanToken, TagOperatorToken },
				{ CodeAnalysisHelper.GreaterThanGreaterThanGreaterThanToken, TagOperatorToken },
				{ SyntaxKind.EqualsEqualsToken, TagOperatorToken },
				{ SyntaxKind.LessThanEqualsToken, TagOperatorToken },
				{ SyntaxKind.GreaterThanEqualsToken, TagOperatorToken },
				{ SyntaxKind.ExclamationEqualsToken, TagOperatorToken },
				{ SyntaxKind.PlusPlusToken, TagOperatorToken },
				{ SyntaxKind.MinusMinusToken, TagOperatorToken },
				{ SyntaxKind.StringLiteralToken, TagStringToken },
				{ CodeAnalysisHelper.SingleLineRawStringLiteralToken, TagStringToken },
				{ CodeAnalysisHelper.MultiLineRawStringLiteralToken, TagStringToken },
				{ CodeAnalysisHelper.Utf8StringLiteralToken, TagStringToken },
				{ SyntaxKind.XmlTextLiteralToken, TagStringToken },
				{ SyntaxKind.InterpolatedStringStartToken, TagInterpolatedStringStartToken },
				{ SyntaxKind.InterpolatedVerbatimStringStartToken, TagInterpolatedStringStartToken },
				{ CodeAnalysisHelper.InterpolatedSingleLineRawStringStartToken, TagInterpolatedStringStartToken },
				{ CodeAnalysisHelper.InterpolatedMultiLineRawStringStartToken, TagInterpolatedStringStartToken },
				{ SyntaxKind.IdentifierToken, TagIdentifier },
			};

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void Tag(in SyntaxToken token, Context ctx) {
				if (__TokenTaggers.TryGetValue(token.Kind(), out var tagAction)) {
					tagAction(token, ctx);
				}
			}

			#region Punctuation and operator
			static void TagOperatorToken(in SyntaxToken token, Context ctx) {
				if (token.Parent.IsKind(SyntaxKind.OperatorDeclaration)) {
					ctx.Tags.Add(token.Span, __Classifications.Method, __Classifications.NestedDeclaration);
				}
			}

			static void TagEqualsToken(in SyntaxToken token, Context ctx) {
				if (!HighlightOptions.SemanticPunctuation) {
					return;
				}
				// note: DON'T use GetConversion, which gives nothing useful
				var node = token.Parent;
				if (node.IsKind(SyntaxKind.SimpleAssignmentExpression)) {
					var a = (AssignmentExpressionSyntax)node;
					if ((node = a.Right).IsAnyKind(SyntaxKind.NullLiteralExpression, SyntaxKind.DefaultLiteralExpression)
						|| ctx.semanticModel.GetTypeInfo(a.Left, ctx.cancellationToken).Type.OriginallyEquals(ctx.semanticModel.GetTypeInfo(node, ctx.cancellationToken).Type)) {
						return;
					}
				}
				else if (node.IsKind(SyntaxKind.EqualsValueClause)) {
					if (node.Parent.IsKind(SyntaxKind.Parameter)) {
						return;
					}
					var expressionType = ctx.semanticModel.GetTypeInfo(((EqualsValueClauseSyntax)node).Value, ctx.cancellationToken).Type;
					if (expressionType == null || MatchDeclaredSymbolType(ctx.semanticModel, node.Parent, expressionType, ctx.cancellationToken)) {
						return;
					}
				}
				else {
					return;
				}
				ctx.Tags.Add(token.Span,
					HighlightOptions.StyleSemanticPunctuation ? __GeneralClassifications.TypeCastKeyword : null,
					HighlightOptions.BoldSemanticPunctuationTag);
			}

			static bool MatchDeclaredSymbolType(SemanticModel semanticModel, SyntaxNode node, ITypeSymbol expressionType, CancellationToken cancellationToken) {
				switch (node.Kind()) {
					case SyntaxKind.VariableDeclarator:
						return expressionType.OriginallyEquals(semanticModel.GetDeclaredSymbol((VariableDeclaratorSyntax)node, cancellationToken).GetReturnType());
					case SyntaxKind.PropertyDeclaration:
					case SyntaxKind.EventDeclaration:
					case SyntaxKind.EventFieldDeclaration:
					case SyntaxKind.FieldDeclaration:
						return expressionType.OriginallyEquals(semanticModel.GetDeclaredSymbol((MemberDeclarationSyntax)node, cancellationToken).GetReturnType());
					case SyntaxKind.EnumMemberDeclaration:
						return GetEnumUnderlyingType(expressionType).OriginallyEquals(GetEnumUnderlyingType(semanticModel.GetDeclaredSymbol((MemberDeclarationSyntax)node, cancellationToken).GetReturnType()));
					default: return false;
				}
			}

			static ITypeSymbol GetEnumUnderlyingType(ITypeSymbol type) {
				return type is INamedTypeSymbol nt && nt.TypeKind == TypeKind.Enum
					? nt.EnumUnderlyingType
					: type;
			}

			static void TagEqualsGreaterThenToken(in SyntaxToken token, Context ctx) {
				var node = token.Parent;
				var kind = node.Kind();
				ClassificationTag tag;
				if (kind == SyntaxKind.ArrowExpressionClause) {
					tag = ClassifySemanticPunctuation(node.Parent);
				}
				else if (kind == SyntaxKind.SimpleLambdaExpression || kind == SyntaxKind.ParenthesizedLambdaExpression) {
					if (HighlightOptions.CapturingLambda && node is LambdaExpressionSyntax
						&& node.AncestorsAndSelf()
							.FirstOrDefault(i => i is StatementSyntax || i is ExpressionSyntax && i.IsKind(SyntaxKind.IdentifierName) == false)
							?.HasCapturedVariable(ctx.semanticModel) == true) {
						tag = __Classifications.VariableCapturedExpression;
					}
					else {
						tag = null;
					}
				}
				else if (kind == CodeAnalysisHelper.SwitchExpressionArm) {
					tag = __GeneralClassifications.BranchingKeyword;
				}
				else {
					return;
				}
				if (tag != null) {
					ctx.Tags.Add(token.Span,
						HighlightOptions.StyleSemanticPunctuation ? tag : null,
						HighlightOptions.BoldSemanticPunctuationTag);
				}
			}

			static void TagSemanticBrace(in SyntaxToken token, Context ctx) {
				if (!HighlightOptions.SemanticPunctuation) {
					return;
				}
				var node = token.Parent;
				if (node.IsAnyKind(SyntaxKind.Block, SyntaxKind.AccessorList)) {
					node = node.Parent;
				}
				var tag = ClassifySemanticPunctuation(node);
				if (tag == null) {
					if (node.IsKind(SyntaxKind.Interpolation)) {
						TagTypeCastedInterpolationExpression(token, ctx, node);
					}
					else if (node.IsKind(CodeAnalysisHelper.PropertyPatternClause)) {
						ctx.Tags.Add(token, __GeneralClassifications.BranchingKeyword);
					}
					return;
				}
				if (HighlightOptions.StyleSemanticPunctuation) {
					ctx.Tags.Add(token.Span,
						tag,
						node is MemberDeclarationSyntax || node.IsKind(SyntaxKind.NamespaceDeclaration)
							? __Classifications.DeclarationBrace
							: null,
						HighlightOptions.BoldSemanticPunctuationTag);
				}
				else if (HighlightOptions.BoldSemanticPunctuation) {
					ctx.Tags.Add(token, HighlightOptions.BoldSemanticPunctuationTag);
				}
			}

			static void TagTypeCastedInterpolationExpression(SyntaxToken token, Context ctx, SyntaxNode node) {
				var type = ctx.semanticModel.GetTypeInfo(((InterpolationSyntax)node).Expression, ctx.cancellationToken).Type;
				if (type != null && type.SpecialType != SpecialType.System_String) {
					ctx.Tags.Add(token.Span, __GeneralClassifications.TypeCastKeyword, HighlightOptions.BoldSemanticPunctuationTag);
				}
			}

			static void TagSemanticParenthesis(in SyntaxToken token, Context ctx) {
				if (HighlightOptions.SemanticPunctuation) {
					var tag = ClassifyParenthesis(ctx.semanticModel, token.Parent, ctx.cancellationToken);
					if (tag != null) {
						ctx.Tags.Add(token.Span,
							HighlightOptions.StyleSemanticPunctuation ? tag : null,
							HighlightOptions.BoldSemanticPunctuationTag);
					}
				}
			}

			static void TagSemanticBracket(in SyntaxToken token, Context ctx) {
				if (HighlightOptions.SemanticPunctuation) {
					var tag = ClassifyBracket(ctx.semanticModel, token.Parent, ctx.cancellationToken);
					if (tag != null) {
						ctx.Tags.Add(token.Span,
							HighlightOptions.StyleSemanticPunctuation ? tag : null,
							HighlightOptions.BoldSemanticPunctuationTag);
						return;
					}
				}
				if (FormatStore.TagAttributeAnnotation && token.Parent.IsKind(SyntaxKind.AttributeList)) {
					ctx.Tags.Add(token.Parent.Span, __Classifications.AttributeNotation);
				}
			}

			static ClassificationTag ClassifySemanticPunctuation(SyntaxNode node) {
				switch (node.Kind()) {
					case SyntaxKind.MethodDeclaration:
					case SyntaxKind.AnonymousMethodExpression:
					case SyntaxKind.SimpleLambdaExpression:
					case SyntaxKind.ParenthesizedLambdaExpression:
					case SyntaxKind.ConversionOperatorDeclaration:
					case SyntaxKind.OperatorDeclaration:
					case SyntaxKind.LocalFunctionStatement:
						return __Classifications.Method;
					case SyntaxKind.InvocationExpression:
						return ((((InvocationExpressionSyntax)node).Expression as IdentifierNameSyntax)?.Identifier.ValueText == "nameof") ? null : __Classifications.Method;
					case SyntaxKind.ConstructorDeclaration:
					case SyntaxKind.BaseConstructorInitializer:
					case SyntaxKind.AnonymousObjectCreationExpression:
					case SyntaxKind.ObjectInitializerExpression:
					case SyntaxKind.ObjectCreationExpression:
					case SyntaxKind.ComplexElementInitializerExpression:
					case SyntaxKind.CollectionInitializerExpression:
					case SyntaxKind.ArrayInitializerExpression:
					case SyntaxKind.ThisConstructorInitializer:
					case SyntaxKind.DestructorDeclaration:
					case CodeAnalysisHelper.ImplicitObjectCreationExpression:
					case CodeAnalysisHelper.WithInitializerExpression:
					case CodeAnalysisHelper.PrimaryConstructorBaseType:
						return __Classifications.ConstructorMethod;
					case SyntaxKind.IndexerDeclaration:
					case SyntaxKind.PropertyDeclaration:
					case SyntaxKind.GetAccessorDeclaration:
					case SyntaxKind.SetAccessorDeclaration:
						return __Classifications.Property;
					case SyntaxKind.ClassDeclaration:
					case CodeAnalysisHelper.RecordDeclaration:
					case CodeAnalysisHelper.ExtensionDeclaration:
						return __Classifications.ClassName;
					case SyntaxKind.InterfaceDeclaration:
						return __Classifications.InterfaceName;
					case SyntaxKind.EnumDeclaration:
						return __Classifications.EnumName;
					case CodeAnalysisHelper.RecordStructDeclaration:
					case SyntaxKind.StructDeclaration:
						return __Classifications.StructName;
					case SyntaxKind.Attribute:
						return __Classifications.AttributeName;
					case SyntaxKind.EventDeclaration:
					case SyntaxKind.AddAccessorDeclaration:
					case SyntaxKind.RemoveAccessorDeclaration:
						return __Classifications.Event;
					case SyntaxKind.DelegateDeclaration:
						return __Classifications.DelegateName;
					case SyntaxKind.NamespaceDeclaration:
						return __Classifications.Namespace;
					case SyntaxKind.IfStatement:
					case SyntaxKind.ElseClause:
					case SyntaxKind.SwitchStatement:
					case SyntaxKind.SwitchSection:
					case CodeAnalysisHelper.SwitchExpression:
					case CodeAnalysisHelper.RecursivePattern:
						return __GeneralClassifications.BranchingKeyword;
					case SyntaxKind.ForStatement:
					case SyntaxKind.ForEachStatement:
					case SyntaxKind.ForEachVariableStatement:
					case SyntaxKind.WhileStatement:
					case SyntaxKind.DoStatement:
						return __GeneralClassifications.LoopKeyword;
					case SyntaxKind.UsingStatement:
					case SyntaxKind.LockStatement:
					case SyntaxKind.FixedStatement:
					case SyntaxKind.UnsafeStatement:
					case SyntaxKind.TryStatement:
					case SyntaxKind.CatchClause:
					case SyntaxKind.CatchFilterClause:
					case SyntaxKind.FinallyClause:
						return __GeneralClassifications.ResourceKeyword;
					case SyntaxKind.CheckedStatement:
					case SyntaxKind.UncheckedStatement:
						return __GeneralClassifications.TypeCastKeyword;
				}
				return null;
			}

			static ClassificationTag ClassifyParenthesis(SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken) {
				switch (node.Kind()) {
					case SyntaxKind.CastExpression:
						return semanticModel.GetSymbolInfo(((CastExpressionSyntax)node).Type, cancellationToken).Symbol != null
							? __GeneralClassifications.TypeCastKeyword
							: null;
					case SyntaxKind.ParenthesizedExpression:
						return node.ChildNodes().FirstOrDefault().IsKind(SyntaxKind.AsExpression)
							&& semanticModel.GetSymbolInfo(((BinaryExpressionSyntax)node.ChildNodes().First()).Right, cancellationToken).Symbol != null
							? __GeneralClassifications.TypeCastKeyword
							: null;
					case SyntaxKind.SwitchStatement:
					case SyntaxKind.SwitchSection:
					case SyntaxKind.IfStatement:
					case SyntaxKind.ElseClause:
					case CodeAnalysisHelper.PositionalPatternClause:
						return __GeneralClassifications.BranchingKeyword;
					case SyntaxKind.ForStatement:
					case SyntaxKind.ForEachStatement:
					case SyntaxKind.ForEachVariableStatement:
					case SyntaxKind.WhileStatement:
					case SyntaxKind.DoStatement:
						return __GeneralClassifications.LoopKeyword;
					case SyntaxKind.UsingStatement:
					case SyntaxKind.FixedStatement:
					case SyntaxKind.LockStatement:
					case SyntaxKind.UnsafeStatement:
					case SyntaxKind.TryStatement:
					case SyntaxKind.CatchDeclaration:
					case SyntaxKind.CatchClause:
					case SyntaxKind.CatchFilterClause:
					case SyntaxKind.FinallyClause:
						return __GeneralClassifications.ResourceKeyword;
					case SyntaxKind.ParenthesizedVariableDesignation:
						return node.Parent.IsKind(CodeAnalysisHelper.VarPattern)
							? __GeneralClassifications.BranchingKeyword
							: __Classifications.ConstructorMethod;
					case SyntaxKind.TupleExpression:
					case SyntaxKind.TupleType:
						return __Classifications.ConstructorMethod;
					case SyntaxKind.CheckedExpression:
					case SyntaxKind.UncheckedExpression:
					case SyntaxKind.MakeRefExpression:
					case SyntaxKind.RefValueExpression:
						return __GeneralClassifications.TypeCastKeyword;
				}
				node = (node as BaseArgumentListSyntax
				   ?? node as BaseParameterListSyntax
				   ?? (CSharpSyntaxNode)(node as CastExpressionSyntax)
				   )?.Parent;
				return node != null
					? ClassifySemanticPunctuation(node)
					: null;
			}

			static ClassificationTag ClassifyBracket(SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken) {
				if (node.IsKind(SyntaxKind.Argument)) {
					node = ((ArgumentSyntax)node).Expression;
				}
				switch (node.Kind()) {
					case SyntaxKind.BracketedArgumentList:
						return ClassifyBracketedArgumentList(semanticModel, node, cancellationToken);
					case SyntaxKind.BracketedParameterList:
						return node.Parent.IsKind(SyntaxKind.IndexerDeclaration)
							? __Classifications.Property
							: null;
					case SyntaxKind.ArrayRankSpecifier:
						return node.Parent.Parent.IsAnyKind(SyntaxKind.ArrayCreationExpression, SyntaxKind.StackAllocArrayCreationExpression, SyntaxKind.ImplicitStackAllocArrayCreationExpression)
							? __Classifications.ConstructorMethod
							: null;
					case SyntaxKind.ImplicitStackAllocArrayCreationExpression:
					case SyntaxKind.ImplicitArrayCreationExpression:
					case CodeAnalysisHelper.CollectionExpression:
					case SyntaxKind.AttributeArgument:
						return __Classifications.ConstructorMethod;
					case CodeAnalysisHelper.ListPatternExpression:
						return __GeneralClassifications.BranchingKeyword;
					default:
						return null;
				}

				ClassificationTag ClassifyBracketedArgumentList(SemanticModel sm, SyntaxNode n, CancellationToken ct) {
					if ((n = n.Parent).IsKind(SyntaxKind.ElementAccessExpression)) {
						n = ((ElementAccessExpressionSyntax)n).Expression;
						var symbol = sm.GetSymbolInfo(n, ct).Symbol;
						if (symbol != null && symbol.Kind == SymbolKind.Property) {
							return __Classifications.Property;
						}
						var type = sm.GetTypeInfo(n, ct).Type;
						if (type != null) {
							return type.TypeKind.CeqAny(TypeKind.Struct, TypeKind.Pointer)
								? __Classifications.StructName
								: __Classifications.ClassName;
						}
						return null;
					}
					return n.IsKind(SyntaxKind.VariableDeclarator) ? __Classifications.ConstructorMethod : null;
				}
			}
			#endregion

			#region Others
			static void TagInKeyword(in SyntaxToken token, Context ctx) {
				if (token.Parent.IsAnyKind(SyntaxKind.ForEachStatement, SyntaxKind.ForEachVariableStatement)) {
					var f = (CommonForEachStatementSyntax)token.Parent;
					var info = ctx.semanticModel.GetForEachStatementInfo(f);
					if (info.ElementConversion.Exists && info.ElementConversion.IsIdentity == false) {
						ctx.Tags.Add(token, __GeneralClassifications.TypeCastKeyword);
					}
					else {
						TagLoopKeyword(in token, ctx);
					}
				}
				else {
					TagInTypeCastKeyword(in token, ctx);
				}
			}

			static void TagInTypeCastKeyword(in SyntaxToken token, Context ctx) {
				if (token.Parent.IsAnyKind(SyntaxKind.Parameter, SyntaxKind.Argument, SyntaxKind.CrefParameter)) {
					TagTypeCastKeyword(in token, ctx);
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			static void TagTypeCastKeyword(in SyntaxToken token, Context ctx) {
				ctx.Tags.Add(token, __GeneralClassifications.TypeCastKeyword);
			}

			static void TagUsingKeyword(in SyntaxToken token, Context ctx) {
				if (token.Parent.IsKind(SyntaxKind.UsingDirective)) {
					return;
				}
				TagResourceKeyword(in token, ctx);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			static void TagResourceKeyword(in SyntaxToken token, Context ctx) {
				ctx.Tags.Add(token, __GeneralClassifications.ResourceKeyword);
			}

			static void TagInterpolatedStringStartToken(in SyntaxToken token, Context ctx) {
				if (FormatStore.TagUrl == false) {
					return;
				}
				var node = token.Parent as InterpolatedStringExpressionSyntax;
				if (node.Span.Length < 3) {
					return;
				}
				var leading = node.StringStartToken.Span.Length;
				var trailing = node.StringEndToken.Span.Length;
				var kind = trailing > 2
					? StringKind.InterpolatedRaw
					: node.StringStartToken.Text.Contains('@')
						? StringKind.InterpolatedVerbatim
						: StringKind.Interpolated;
				TagUrlParts(ctx, node.ToString(), leading, trailing, token.SpanStart, kind);
			}

			static void TagStringToken(in SyntaxToken token, Context ctx) {
				if (FormatStore.TagUrl == false) {
					return;
				}
				var url = token.Text;
				if (url.Length < 5) {
					return;
				}
				int leading, trailing;
				StringKind kind;
				switch (token.Kind()) {
					case SyntaxKind.StringLiteralToken:
						leading = url[0] == '@' ? 2 : 1; // @" or "
						trailing = 1;
						kind = leading == 2 ? StringKind.Verbatim : StringKind.Default;
						break;
					case CodeAnalysisHelper.SingleLineRawStringLiteralToken:
					case CodeAnalysisHelper.MultiLineRawStringLiteralToken:
						GetRawStringQuoteLength(url, out leading);
						trailing = leading;
						kind = StringKind.Raw;
						break;
					case CodeAnalysisHelper.Utf8StringLiteralToken:
						leading = url[0] == '@' ? 2 : 1; // @" or "
						trailing = 3; // "u8
						kind = leading == 2 ? StringKind.Verbatim : StringKind.Default;
						break;
					default:
						leading = trailing = 0;
						kind = StringKind.Default;
						break;
				}
				TagUrlParts(ctx, url, leading, trailing, token.SpanStart, kind);
			}

			internal static void TagUrlParts(Context ctx, string url, int leading, int trailing, int offset, StringKind kind) {
				int length = url.Length - leading - trailing;
				if (length < 5) {
					return;
				}
				while ((leading = TryParseUrl(url, leading, length, kind, (p, s, l) => {
					ClassificationTag tag;
					switch (p) {
						case UrlPart.FullUrl: tag = __UrlClassifications.Url; break;
						case UrlPart.Scheme: tag = __UrlClassifications.Scheme; break;
						case UrlPart.Credential: tag = __UrlClassifications.Credential; break;
						case UrlPart.Host: tag = __UrlClassifications.Host; break;
						case UrlPart.Port: tag = __GeneralClassifications.Number; break;
						case UrlPart.FileName: tag = __UrlClassifications.File; break;
						case UrlPart.QueryQuestionMark: tag = __UrlClassifications.Punctuation; break;
						case UrlPart.QueryName: tag = __UrlClassifications.QueryName; break;
						case UrlPart.QueryValue: tag = __UrlClassifications.QueryValue; break;
						case UrlPart.Fragment: tag = __UrlClassifications.Fragment; break;
						default: return;
					}
					ctx.Tags.Add(new TextSpan(offset + s, l), tag);
				})) != -1) {
					if ((length = url.Length - leading - trailing) < 4) {
						return;
					}
				}
			}

			static void GetRawStringQuoteLength(string text, out int quoteLength) {
				int l;
				if ((l = text.Length) < 7 || text[l - 1] != '"') {
					quoteLength = -1;
					return;
				}
				var s = text.AsSpan(3, text.Length - 6);
				var end = s.Length - 1;
				int i = 0;
				while (s[i] == '"' && s[end - i] == '"') {
					++i;
				}
				quoteLength = 3 + i;
			}

			static bool IsHex(char c) {
				return c.IsBetween('0', '9') || c.IsBetween('A', 'F') || c.IsBetween('a', 'f');
			}
			#endregion

			#region Control flow and branching
			static void TagDefaultKeyword(in SyntaxToken token, Context ctx) {
				if (token.Parent.IsKind(SyntaxKind.DefaultSwitchLabel)) {
					TagBranchingKeyword(in token, ctx);
				}
			}

			static void TagAwaitKeyword(in SyntaxToken token, Context ctx) {
				if (token.Parent.IsAnyKind(SyntaxKind.AwaitExpression, SyntaxKind.ForEachStatement)) {
					TagControlFlowKeyword(in token, ctx);
				}
			}

			static void TagBreakKeyword(in SyntaxToken token, Context ctx) {
				if (token.Parent.Parent.IsKind(SyntaxKind.SwitchSection) == false) {
					TagControlFlowKeyword(in token, ctx);
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			static void TagControlFlowKeyword(in SyntaxToken token, Context ctx) {
				ctx.Tags.Add(token, __GeneralClassifications.ControlFlowKeyword);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			static void TagBranchingKeyword(in SyntaxToken token, Context ctx) {
				ctx.Tags.Add(token, __GeneralClassifications.BranchingKeyword);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			static void TagLoopKeyword(in SyntaxToken token, Context ctx) {
				ctx.Tags.Add(token, __GeneralClassifications.LoopKeyword);
			}
			#endregion

			#region Declaration
			static void TagRecordDeclaration(in SyntaxToken token, Context ctx) {
				if (token.Parent is TypeDeclarationSyntax t) {
					ctx.Tags.Add(t.Identifier.Span,
						t.IsKind(CodeAnalysisHelper.RecordDeclaration) ? __Classifications.ClassName : __Classifications.StructName,
						__Classifications.Declaration);
				}
			}

			static void TagExtensionDeclaration(in SyntaxToken token, Context ctx) {
				ctx.Tags.Add(token.Span, __Classifications.ClassName, __Classifications.NestedDeclaration);
			}

			static void TagTypeCastDeclaration(in SyntaxToken token, Context ctx) {
				if (token.Parent is ConversionOperatorDeclarationSyntax c) {
					ctx.Tags.Add(c.Type.Span, __Classifications.NestedDeclaration);
				}
				ctx.Tags.Add(token, __GeneralClassifications.TypeCastKeyword);
			}

			static void TagAbstractionKeyword(in SyntaxToken token, Context ctx) {
				if (token.Parent.Kind().IsDeclaration()) {
					ctx.Tags.Add(token, __GeneralClassifications.AbstractionKeyword);
				}
			}

			static void TagThisKeyword(in SyntaxToken token, Context ctx) {
				if (token.Parent.Kind().IsDeclaration()) {
					ctx.Tags.Add(token, __Classifications.Declaration);
				}
			}
			#endregion

			#region Identifier
			internal static void TagIdentifier(in SyntaxToken token, Context ctx) {
				var itemSpan = ctx.Tags.MakeSnapshotSpan(token.Span);
				var node = token.Parent;
				var cancellationToken = ctx.cancellationToken;
				var semanticModel = ctx.semanticModel;
				var tags = ctx.Tags;
				var symbol = semanticModel.GetSymbolOrFirstCandidate(node, cancellationToken);
				IMethodSymbol method;
				if (symbol is null) {
					symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken);
					if (symbol is null) {
						symbol = FindSymbolOrSymbolCandidateForNode(node, semanticModel, cancellationToken);
						if (symbol is null) {
							if (node.Parent.IsKind(SyntaxKind.TypeConstraint) && token.Text == "unmanaged") {
								// the "unmanaged" constraint is not classified as a keyword by Roslyn
								tags.Add(itemSpan, __GeneralClassifications.ResourceKeyword);
							}
							return;
						}
					}
					else if (ClassifyDeclarationSymbol(itemSpan, node, semanticModel, tags, ref symbol, cancellationToken) == false) {
						return;
					}
				}
				switch (symbol.Kind) {
					case SymbolKind.ArrayType:
					case SymbolKind.DynamicType:
						if (HighlightOptions.StyleVarAsType && token.IsVarKeyword()) {
							tags.Add(itemSpan, __Classifications.ClassName);
						}
						return;
					case SymbolKind.Alias:
					case SymbolKind.Assembly:
					case SymbolKind.ErrorType:
					case SymbolKind.NetModule:
					case SymbolKind.PointerType:
					case SymbolKind.RangeVariable:
					case SymbolKind.Preprocessing:
						//case SymbolKind.Discard:
						return;

					case SymbolKind.Label:
						tags.Add(itemSpan, __Classifications.Label);
						return;

					case SymbolKind.TypeParameter:
						tags.Add(itemSpan, __Classifications.TypeParameter);
						return;

					case SymbolKind.Field:
						var f = symbol as IFieldSymbol;
						if (f.IsConst) {
							tags.Add(itemSpan, f.ContainingType.TypeKind == TypeKind.Enum
								? __Classifications.EnumField
								: __Classifications.ConstField);
						}
						else {
							tags.Add(itemSpan, f.IsReadOnly ? __Classifications.ReadOnlyField
								: f.IsVolatile ? __Classifications.VolatileField
								: __Classifications.Field);
						}
						break;

					case SymbolKind.Property:
						tags.Add(itemSpan, __Classifications.Property);
						if (symbol.IsExtensionMember()) {
							tags.Add(itemSpan, __Classifications.ExtensionMember);
						}
						break;

					case SymbolKind.Event:
						tags.Add(itemSpan, __Classifications.Event);
						break;

					case SymbolKind.Local:
						tags.Add(itemSpan, ((ILocalSymbol)symbol).IsConst
							? __Classifications.ConstField
							: __Classifications.LocalVariable);
						return;

					case SymbolKind.Namespace:
						tags.Add(itemSpan, __Classifications.Namespace);
						return;

					case SymbolKind.Parameter:
						tags.Add(itemSpan, __Classifications.Parameter);
						if ((method = symbol.ContainingSymbol as IMethodSymbol) != null) {
							if (method.IsPrimaryConstructor()) {
								tags.Add(itemSpan, __Classifications.PrimaryConstructorParameter);
							}
							else if (method.MethodKind.CeqAny(MethodKind.LambdaMethod, MethodKind.LocalFunction)) {
								tags.Add(itemSpan, __Classifications.LocalFunctionParameter);
							}
						}
						break;

					case SymbolKind.Method:
						method = (IMethodSymbol)symbol;
						switch (method.MethodKind) {
							case MethodKind.Constructor:
								tags.Add(itemSpan,
									node is AttributeSyntax || node.Parent is AttributeSyntax || node.Parent?.Parent is AttributeSyntax
										? __Classifications.AttributeName
										: HighlightOptions.StyleConstructorAsType
										? (method.ContainingType.TypeKind == TypeKind.Struct ? __Classifications.StructName : __Classifications.ClassName)
										: __Classifications.ConstructorMethod);
								break;
							case MethodKind.Destructor:
							case MethodKind.StaticConstructor:
								tags.Add(itemSpan, HighlightOptions.StyleConstructorAsType
										? (method.ContainingType.TypeKind == TypeKind.Struct
											? __Classifications.StructName
											: __Classifications.ClassName)
										: __Classifications.ConstructorMethod);
								break;
							default:
								tags.Add(itemSpan, method.IsExtensionMethod ? __Classifications.ExtensionMethod
									: method.IsExtern ? __Classifications.ExternMethod
									: __Classifications.Method);
								if (method.MethodKind == MethodKind.Ordinary && method.IsExtensionMember()) {
									tags.Add(itemSpan, __Classifications.ExtensionMember);
								}
								break;
						}
						break;

					case SymbolKind.NamedType:
						if (symbol.ContainingType?.Kind == SymbolKind.NamedType) {
							tags.Add(itemSpan, __Classifications.NestedType);
						}
						if (token.IsVarKeyword()) {
							if (!HighlightOptions.StyleVarAsType) {
								return;
							}
							tags.Add(itemSpan, GetTypeKindClassificationTag(((ITypeSymbol)symbol).TypeKind));
						}
						break;

					default:
						return;
				}

				ClassifyCommonSymbol(itemSpan, in tags, symbol);
			}

			static bool ClassifyDeclarationSymbol(SnapshotSpan itemSpan, SyntaxNode node, SemanticModel semanticModel, in TagHolder tags, ref ISymbol symbol, CancellationToken cancellationToken) {
				switch (symbol.Kind) {
					case SymbolKind.NamedType:
						tags.Add(itemSpan, symbol.ContainingType != null
							? __Classifications.NestedDeclaration
							: __Classifications.Declaration);
						break;
					case SymbolKind.Event:
						if (HighlightOptions.NonPrivateField
							&& symbol.DeclaredAccessibility >= Accessibility.ProtectedAndInternal) {
							tags.Add(itemSpan, __Classifications.NestedDeclaration);
						}
						break;
					case SymbolKind.Method:
						if (((IMethodSymbol)symbol).MethodKind == MethodKind.LocalFunction) {
							tags.Add(itemSpan, HighlightOptions.LocalFunctionDeclaration ? __Classifications.NestedDeclaration : __Classifications.LocalFunctionDeclaration);
							if (node.HasCapturedVariable(semanticModel)) {
								tags.Add(itemSpan, CSharpClassifications.Instance.VariableCapturedExpression);
							}
						}
						else {
							tags.Add(itemSpan, __Classifications.NestedDeclaration);
						}
						break;
					case SymbolKind.Property:
						if (symbol.ContainingType.IsAnonymousType == false) {
							tags.Add(itemSpan, __Classifications.NestedDeclaration);
						}
						break;
					case SymbolKind.Field:
						if (node.IsKind(SyntaxKind.TupleElement)) {
							if (((TupleElementSyntax)node).Identifier.IsKind(SyntaxKind.None)) {
								symbol = semanticModel.GetTypeInfo(((TupleElementSyntax)node).Type, cancellationToken).Type;
								if (symbol is null) {
									return false;
								}
							}
						}
						else if (HighlightOptions.NonPrivateField
							&& symbol.DeclaredAccessibility >= Accessibility.ProtectedAndInternal
							&& symbol.ContainingType.TypeKind != TypeKind.Enum) {
							tags.Add(itemSpan, __Classifications.NestedDeclaration);
						}
						break;
					case SymbolKind.Local:
						tags.Add(itemSpan, __Classifications.LocalDeclaration);
						break;
					case SymbolKind.RangeVariable:
						tags.Add(itemSpan, __Classifications.LocalDeclaration);
						return false;
				}

				return true;
			}

			static ClassificationTag GetTypeKindClassificationTag(TypeKind typeKind) {
				switch (typeKind) {
					case TypeKind.Array:
					case TypeKind.Class:
					case TypeKind.Dynamic:
						return __Classifications.ClassName;
					case TypeKind.Delegate: return __Classifications.DelegateName;
					case TypeKind.Enum: return __Classifications.EnumName;
					case TypeKind.Interface: return __Classifications.InterfaceName;
					case TypeKind.Pointer:
					case TypeKind.Struct: return __Classifications.StructName;
					case TypeKind.TypeParameter: return __Classifications.TypeParameter;
					default: return __GeneralClassifications.Identifier;
				}
			}

			static void ClassifyCommonSymbol(SnapshotSpan itemSpan, in TagHolder tags, ISymbol symbol) {
				if (SymbolMarkManager.HasBookmark) {
					var markerStyle = SymbolMarkManager.GetSymbolMarkerStyle(symbol);
					if (markerStyle != null) {
						tags.Add(itemSpan, markerStyle);
					}
				}

				if (FormatStore.IdentifySymbolSource
					&& symbol.IsMemberOrType()
					&& symbol.ContainingAssembly != null) {
					tags.Add(itemSpan, symbol.ContainingAssembly.GetSourceType() == AssemblySource.Metadata
						? __Classifications.MetadataSymbol
						: __Classifications.UserSymbol);
				}

				if (symbol.DeclaredAccessibility == Accessibility.Private) {
					switch (symbol.Kind) {
						case SymbolKind.Property:
						case SymbolKind.Method:
						case SymbolKind.Field:
						case SymbolKind.NamedType:
						case SymbolKind.Event:
							tags.Add(itemSpan, __Classifications.PrivateMember);
							break;
					}
				}
				if (symbol.IsStatic) {
					if (symbol.Kind != SymbolKind.Namespace) {
						tags.Add(itemSpan, __Classifications.StaticMember);
						if (symbol.IsAbstract && symbol.ContainingType?.TypeKind == TypeKind.Interface) {
							tags.Add(itemSpan, __Classifications.AbstractMember);
						}
					}
				}
				else if (symbol.IsSealed) {
					ITypeSymbol type;
					if (symbol.Kind == SymbolKind.NamedType) {
						if ((type = (ITypeSymbol)symbol).TypeKind == TypeKind.Struct) {
							if (type.IsReadOnly()) {
								tags.Add(itemSpan, __Classifications.ReadOnlyStruct);
							}
							if (type.IsRefLike()) {
								tags.Add(itemSpan, __Classifications.RefStruct);
							}
							return;
						}
						if (type.TypeKind == TypeKind.Enum) {
							return;
						}
					}
					tags.Add(itemSpan, __Classifications.SealedMember);
				}
				else if (symbol.IsOverride) {
					tags.Add(itemSpan, __Classifications.OverrideMember);
				}
				else if (symbol.IsVirtual) {
					tags.Add(itemSpan, __Classifications.VirtualMember);
				}
				else if (symbol.IsAbstract && symbol.Kind != SymbolKind.NamedType) {
					tags.Add(itemSpan, __Classifications.AbstractMember);
				}
			}

			static ISymbol FindSymbolOrSymbolCandidateForNode(SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken) {
				SyntaxNode parent = node.Parent;
				return parent.IsKind(SyntaxKind.SimpleMemberAccessExpression) ? semanticModel.GetSymbolInfo(parent, cancellationToken).CandidateSymbols.FirstOrDefault()
					: parent.IsKind(SyntaxKind.Argument) ? semanticModel.GetSymbolInfo(((ArgumentSyntax)parent).Expression, cancellationToken).CandidateSymbols.FirstOrDefault()
					: node.IsKind(SyntaxKind.SimpleBaseType) ? semanticModel.GetTypeInfo(((SimpleBaseTypeSyntax)node).Type, cancellationToken).Type
					: node.IsKind(SyntaxKind.TypeConstraint) ? semanticModel.GetTypeInfo(((TypeConstraintSyntax)node).Type, cancellationToken).Type
					: node.IsKind(SyntaxKind.ExpressionStatement) ? semanticModel.GetSymbolInfo(((ExpressionStatementSyntax)node).Expression, cancellationToken).CandidateSymbols.FirstOrDefault()
					: node.IsKind(SyntaxKind.IdentifierName) && parent.Parent.IsKind(SyntaxKind.UsingDirective) ? semanticModel.GetDeclaredSymbol(parent.Parent, cancellationToken)?.GetAliasTarget()
					: semanticModel.GetSymbolInfo(node, cancellationToken).CandidateSymbols.FirstOrDefault();
			}
			#endregion
		}

		readonly struct TagHolder : IEnumerable<TagSpan<IClassificationTag>>
		{
			readonly ITextSnapshot _TextSnapshot;
			readonly Chain<TagSpan<IClassificationTag>> _Chain;

			public TagHolder(ITextSnapshot textSnapshot) : this() {
				_TextSnapshot = textSnapshot;
				_Chain = new Chain<TagSpan<IClassificationTag>>();
			}

			public SnapshotSpan MakeSnapshotSpan(TextSpan span) {
				return new SnapshotSpan(_TextSnapshot, span.Start, span.Length);
			}

			public void Add(in SyntaxToken token, ClassificationTag tag) {
				if (tag != null) {
					var span = token.Span;
					_Chain.Add(new TagSpan<IClassificationTag>(new SnapshotSpan(_TextSnapshot, span.Start, span.Length), tag));
				}
			}

			public void Add(TextSpan span, ClassificationTag tag) {
				if (tag != null) {
					_Chain.Add(new TagSpan<IClassificationTag>(new SnapshotSpan(_TextSnapshot, span.Start, span.Length), tag));
				}
			}

			public void Add(SnapshotSpan span, ClassificationTag tag) {
				if (tag != null) {
					_Chain.Add(new TagSpan<IClassificationTag>(span, tag));
				}
			}

			public void Add(TextSpan span, params ClassificationTag[] tags) {
				var ss = new SnapshotSpan(_TextSnapshot, span.Start, span.Length);
				foreach (var tag in tags) {
					if (tag != null) {
						_Chain.Add(new TagSpan<IClassificationTag>(ss, tag));
					}
				}
			}

			public void Add(TextSpan span, IEnumerable<ClassificationTag> tags) {
				var ss = new SnapshotSpan(_TextSnapshot, span.Start, span.Length);
				foreach (var tag in tags) {
					if (tag != null) {
						_Chain.Add(new TagSpan<IClassificationTag>(ss, tag));
					}
				}
			}

			public IEnumerator<TagSpan<IClassificationTag>> GetEnumerator() {
				return _Chain.GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator() {
				return ((IEnumerable)_Chain).GetEnumerator();
			}
		}

		sealed class AttributeListProber
		{
			[ThreadStatic]
			static AttributeListProber __Instance;
			public static AttributeListProber Instance => __Instance == null ? (__Instance = new AttributeListProber()) : __Instance;

			private AttributeListProber() {}

			SyntaxNode _Target;
			public bool Probe(SyntaxNode node) {
				if (node.RawKind == (int)SyntaxKind.AttributeList) {
					_Target = node;
				}
				return true;
			}
			public SyntaxNode FetchTarget() {
				if (_Target != null) {
					var target = _Target;
					_Target = null;
					return target;
				}
				return null;
			}
		}

		static class HighlightOptions
		{
			// use fields to cache option flags
			public static bool SemanticPunctuation,
				StyleSemanticPunctuation,
				BoldSemanticPunctuation,
				LocalFunctionDeclaration,
				NonPrivateField,
				StyleConstructorAsType,
				StyleVarAsType,
				CapturingLambda;
			public static ClassificationTag BoldSemanticPunctuationTag;

			static HighlightOptions() {
				Config.RegisterUpdateHandler(UpdateCSharpHighlighterConfig);
				UpdateCSharpHighlighterConfig(new ConfigUpdatedEventArgs(Config.Instance, Features.SyntaxHighlight));
			}

			static void UpdateCSharpHighlighterConfig(ConfigUpdatedEventArgs e) {
				if (e.UpdatedFeature.MatchFlags(Features.SyntaxHighlight) == false) {
					return;
				}
				var o = e.Config.SpecialHighlightOptions;
				SemanticPunctuation = o.HasAnyFlag(SpecialHighlightOptions.SemanticPunctuation | SpecialHighlightOptions.BoldSemanticPunctuation);
				StyleSemanticPunctuation = o.MatchFlags(SpecialHighlightOptions.SemanticPunctuation);
				CapturingLambda = o.MatchFlags(SpecialHighlightOptions.CapturingLambdaExpression);
				var bold = BoldSemanticPunctuation = o.MatchFlags(SpecialHighlightOptions.BoldSemanticPunctuation);
				BoldSemanticPunctuationTag = bold ? __GeneralClassifications.Bold : null;
				LocalFunctionDeclaration = o.MatchFlags(SpecialHighlightOptions.LocalFunctionDeclaration);
				NonPrivateField = o.MatchFlags(SpecialHighlightOptions.NonPrivateField);
				StyleConstructorAsType = o.MatchFlags(SpecialHighlightOptions.UseTypeStyleOnConstructor);
				StyleVarAsType = o.MatchFlags(SpecialHighlightOptions.UseTypeStyleOnVarKeyword);
			}
		}
	}
}