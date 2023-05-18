using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using AppHelpers;
using Codist.SyntaxHighlight;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
{
	sealed class CSharpTagger : ITagger<IClassificationTag>, IDisposable
	{
		static readonly CSharpClassifications __Classifications = CSharpClassifications.Instance;
		static readonly GeneralClassifications __GeneralClassifications = GeneralClassifications.Instance;

		ConcurrentQueue<SnapshotSpan> _PendingSpans = new ConcurrentQueue<SnapshotSpan>();
		CancellationTokenSource _RenderBreaker;

		ITextBufferParser _Parser;

		public CSharpTagger(CSharpParser parser, ITextBuffer buffer) {
			_Parser = parser.GetParser(buffer);
			_Parser.StateUpdated += HandleParseResult;
		}

		void HandleParseResult(object sender, EventArgs<SemanticState> result) {
			var pendingSpans = _PendingSpans;
			if (pendingSpans != null) {
				var snapshot = result.Data.Snapshot;
				if (snapshot != null) {
					while (pendingSpans.TryDequeue(out var span)) {
						if (snapshot == span.Snapshot) {
							Debug.WriteLine($"Refresh span {span}");
							TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
						}
					}
				}
			}
		}

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public IEnumerable<ITagSpan<IClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
			var p = _Parser;
			if (p == null) {
				return Enumerable.Empty<ITagSpan<IClassificationTag>>();
			}
			if (p.TryGetSemanticState(spans[0].Snapshot, out var r)) {
				return Tagger.GetTags(spans, r, SyncHelper.CancelAndRetainToken(ref _RenderBreaker));
			}
			foreach (var item in spans) {
				Debug.WriteLine($"Enqueue span {item}");
				_PendingSpans.Enqueue(item);
			}
			return r == null
				? Enumerable.Empty<ITagSpan<IClassificationTag>>()
				: UseOldResult(spans, spans[0].Snapshot, r, SyncHelper.CancelAndRetainToken(ref _RenderBreaker));
		}

		static IEnumerable<ITagSpan<IClassificationTag>> UseOldResult(NormalizedSnapshotSpanCollection spans, ITextSnapshot snapshot, SemanticState result, CancellationToken cancellationToken) {
			foreach (var tagSpan in Tagger.GetTags(MapToOldSpans(spans, result.Snapshot), result, cancellationToken)) {
				yield return new TagSpan<IClassificationTag>(tagSpan.Span.TranslateTo(snapshot, SpanTrackingMode.EdgeInclusive), tagSpan.Tag);
			}
		}

		static IEnumerable<SnapshotSpan> MapToOldSpans(NormalizedSnapshotSpanCollection spans, ITextSnapshot last) {
			foreach (var item in spans) {
				yield return item.TranslateTo(last, SpanTrackingMode.EdgeInclusive);
			}
		}

		public void Dispose() {
			_PendingSpans = null;
			SyncHelper.CancelAndDispose(ref _RenderBreaker, false);
			ITextBufferParser t = _Parser;
			if (t != null) {
				t.Dispose();
				_Parser = null;
			}
		}

		internal static class Tagger
		{
			public static IEnumerable<ITagSpan<IClassificationTag>> GetTags(IEnumerable<SnapshotSpan> spans, SemanticState result, CancellationToken cancellationToken) {
				try {
					return GetTagsInternal(spans, result, cancellationToken);
				}
				catch (OperationCanceledException) {
					return Enumerable.Empty<ITagSpan<IClassificationTag>>();
				}
			}
			static Chain<ITagSpan<IClassificationTag>> GetTagsInternal(IEnumerable<SnapshotSpan> spans, SemanticState result, CancellationToken cancellationToken) {
				var workspace = result.Workspace;
				var semanticModel = result.Model;
				var compilationUnit = result.GetCompilationUnit(cancellationToken);
				var snapshot = result.Snapshot;
				var l = semanticModel.SyntaxTree.Length;
				var tags = new Chain<ITagSpan<IClassificationTag>>();
				foreach (var span in spans) {
					if (span.End > l || cancellationToken.IsCancellationRequested) {
						return tags;
					}
					var textSpan = new TextSpan(span.Start.Position, span.Length);
					var classifiedSpans = Classifier.GetClassifiedSpans(semanticModel, textSpan, workspace, cancellationToken);
					var lastTriviaSpan = default(TextSpan);
					SyntaxNode node;
					TagSpan<IClassificationTag> tag = null;
					foreach (var item in classifiedSpans) {
						var ct = item.ClassificationType;
						switch (ct) {
							case "keyword":
							case Constants.CodeKeywordControl:
								node = compilationUnit.FindNode(item.TextSpan, true, true);
								if (node is MemberDeclarationSyntax || node is AccessorDeclarationSyntax) {
									tag = ClassifyDeclarationKeyword(item.TextSpan, snapshot, node, compilationUnit, out var tag2);
									if (tag2 != null) {
										tags.Add(tag2);
									}
								}
								else {
									tag = ClassifyKeyword(item.TextSpan, snapshot, node, compilationUnit);
								}
								break;
							case Constants.CodeOperator:
							case Constants.CodeOverloadedOperator:
								tag = ClassifyOperator(item.TextSpan, snapshot, semanticModel, compilationUnit, cancellationToken);
								break;
							case Constants.CodePunctuation:
								tag = ClassifyPunctuation(item.TextSpan, snapshot, semanticModel, compilationUnit, cancellationToken);
								break;
							case Constants.XmlDocDelimiter:
								tag = ClassifyXmlDoc(item.TextSpan, snapshot, compilationUnit, ref lastTriviaSpan);
								break;
							default:
								tag = null;
								break;
						}
						if (tag != null) {
							tags.Add(tag);
							continue;
						}
						if (ct == Constants.CodeIdentifier
							//|| ct == Constants.CodeStaticSymbol
							|| ct.EndsWith("name", StringComparison.Ordinal)) {
							textSpan = item.TextSpan;
							node = compilationUnit.FindNode(textSpan, true, true);
							foreach (var type in GetClassificationType(node, semanticModel, cancellationToken)) {
								tags.Add(CreateClassificationSpan(snapshot, textSpan, type));
							}
						}
					}
				}
				return tags;
			}

			static TagSpan<IClassificationTag> ClassifyDeclarationKeyword(TextSpan itemSpan, ITextSnapshot snapshot, SyntaxNode node, CompilationUnitSyntax unitCompilation, out TagSpan<IClassificationTag> secondaryTag) {
				secondaryTag = null;
				switch (unitCompilation.FindToken(itemSpan.Start).Kind()) {
					case SyntaxKind.SealedKeyword:
					case SyntaxKind.OverrideKeyword:
					case SyntaxKind.AbstractKeyword:
					case SyntaxKind.VirtualKeyword:
					case SyntaxKind.ProtectedKeyword:
					case SyntaxKind.NewKeyword:
						return CreateClassificationSpan(snapshot, itemSpan, __Classifications.AbstractionKeyword);
					case SyntaxKind.ThisKeyword:
						return CreateClassificationSpan(snapshot, itemSpan, __Classifications.Declaration);
					case SyntaxKind.UnsafeKeyword:
					case SyntaxKind.FixedKeyword:
						return CreateClassificationSpan(snapshot, itemSpan, __Classifications.ResourceKeyword);
					case SyntaxKind.ExplicitKeyword:
					case SyntaxKind.ImplicitKeyword:
						secondaryTag = CreateClassificationSpan(snapshot, ((ConversionOperatorDeclarationSyntax)node).Type.Span, __Classifications.NestedDeclaration);
						return CreateClassificationSpan(snapshot, itemSpan, __GeneralClassifications.TypeCastKeyword);
					case SyntaxKind.ReadOnlyKeyword:
						return CreateClassificationSpan(snapshot, itemSpan, __GeneralClassifications.TypeCastKeyword);
					case CodeAnalysisHelper.RecordKeyword: // workaround for missing classification type for record identifier
						itemSpan = ((TypeDeclarationSyntax)node).Identifier.Span;
						return CreateClassificationSpan(snapshot,
							itemSpan,
							node.IsKind(CodeAnalysisHelper.RecordDeclaration)
								? TransientTags.StructDeclaration
								: TransientTags.ClassDeclaration);
				}
				return null;
			}

			static TagSpan<IClassificationTag> ClassifyKeyword(TextSpan itemSpan, ITextSnapshot snapshot, SyntaxNode node, CompilationUnitSyntax unitCompilation) {
				switch (node.Kind()) {
					case SyntaxKind.BreakStatement:
						if (node.Parent is SwitchSectionSyntax) {
							return null;
						}
						goto case SyntaxKind.ReturnStatement;
					case SyntaxKind.AwaitExpression:
					case SyntaxKind.ReturnKeyword:
					case SyntaxKind.GotoCaseStatement:
					case SyntaxKind.GotoDefaultStatement:
					case SyntaxKind.GotoStatement:
					case SyntaxKind.ContinueStatement:
					case SyntaxKind.ReturnStatement:
					case SyntaxKind.YieldReturnStatement:
					case SyntaxKind.YieldBreakStatement:
					case SyntaxKind.ThrowStatement:
					case SyntaxKind.ThrowExpression:
						return CreateClassificationSpan(snapshot, itemSpan, __GeneralClassifications.ControlFlowKeyword);
					case SyntaxKind.IfStatement:
					case SyntaxKind.ElseClause:
					case SyntaxKind.SwitchStatement:
					case SyntaxKind.CaseSwitchLabel:
					case SyntaxKind.DefaultSwitchLabel:
					case CodeAnalysisHelper.SwitchExpression:
					case SyntaxKind.CasePatternSwitchLabel:
					case SyntaxKind.WhenClause:
						return CreateClassificationSpan(snapshot, itemSpan, __GeneralClassifications.BranchingKeyword);
					case SyntaxKind.ForStatement:
					case SyntaxKind.ForEachStatement:
					case SyntaxKind.ForEachVariableStatement:
					case SyntaxKind.WhileStatement:
					case SyntaxKind.DoStatement:
					case SyntaxKind.SelectClause:
					case SyntaxKind.FromClause:
						return CreateClassificationSpan(snapshot, itemSpan, __GeneralClassifications.LoopKeyword);
					case SyntaxKind.UsingStatement:
					case SyntaxKind.FixedStatement:
					case SyntaxKind.LockStatement:
					case SyntaxKind.UnsafeStatement:
					case SyntaxKind.TryStatement:
					case SyntaxKind.CatchClause:
					case SyntaxKind.CatchFilterClause:
					case SyntaxKind.FinallyClause:
					case SyntaxKind.StackAllocArrayCreationExpression:
					case SyntaxKind.ImplicitStackAllocArrayCreationExpression:
					case CodeAnalysisHelper.FunctionPointerCallingConvention:
						return CreateClassificationSpan(snapshot, itemSpan, __Classifications.ResourceKeyword);
					case SyntaxKind.LocalDeclarationStatement:
						if (unitCompilation.FindToken(itemSpan.Start, true).IsKind(SyntaxKind.UsingKeyword)) {
							goto case SyntaxKind.UsingStatement;
						}
						return null;
					case SyntaxKind.IsExpression:
					case SyntaxKind.IsPatternExpression:
					case SyntaxKind.AsExpression:
					case SyntaxKind.RefExpression:
					case SyntaxKind.RefType:
					case SyntaxKind.CheckedExpression:
					case SyntaxKind.CheckedStatement:
					case SyntaxKind.UncheckedExpression:
					case SyntaxKind.UncheckedStatement:
						return CreateClassificationSpan(snapshot, itemSpan, __GeneralClassifications.TypeCastKeyword);
					case SyntaxKind.Argument:
					case SyntaxKind.Parameter:
					case SyntaxKind.CrefParameter:
						switch (unitCompilation.FindToken(itemSpan.Start, true).Kind()) {
							case SyntaxKind.InKeyword:
							case SyntaxKind.OutKeyword:
							case SyntaxKind.RefKeyword:
								return CreateClassificationSpan(snapshot, itemSpan, __GeneralClassifications.TypeCastKeyword);
						}
						return null;
					case SyntaxKind.IdentifierName:
						if (node.Parent.IsKind(SyntaxKind.TypeConstraint)
							&& itemSpan.Length == 9
							&& node.ToString() == "unmanaged") {
							goto case SyntaxKind.UnsafeStatement;
						}
						return null;
				}
				return null;
			}
			static TagSpan<IClassificationTag> ClassifyOperator(TextSpan itemSpan, ITextSnapshot snapshot, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation, CancellationToken cancellationToken) {
				var node = unitCompilation.FindNode(itemSpan);
				if (node.RawKind == (int)SyntaxKind.DestructorDeclaration) {
					return CreateClassificationSpan(snapshot, itemSpan, TransientTags.DestructorDeclaration);
				}
				if (node.IsKind(SyntaxKind.ArrowExpressionClause)) {
					switch (node.Parent.Kind()) {
						case SyntaxKind.MethodDeclaration:
							return CreateClassificationSpan(snapshot, itemSpan, CSharpClassifications.Instance.Method);
						case SyntaxKind.PropertyDeclaration:
						case SyntaxKind.GetAccessorDeclaration:
						case SyntaxKind.SetAccessorDeclaration:
						case SyntaxKind.IndexerDeclaration:
							return CreateClassificationSpan(snapshot, itemSpan, CSharpClassifications.Instance.Property);
						case SyntaxKind.AddAccessorDeclaration:
						case SyntaxKind.RemoveAccessorDeclaration:
							return CreateClassificationSpan(snapshot, itemSpan, CSharpClassifications.Instance.Event);
						case SyntaxKind.ConstructorDeclaration:
						case SyntaxKind.DestructorDeclaration:
							return CreateClassificationSpan(snapshot, itemSpan, CSharpClassifications.Instance.ConstructorMethod);
					}
					return null;
				}
				if (semanticModel.GetSymbol(node.IsKind(SyntaxKind.Argument) ? ((ArgumentSyntax)node).Expression : node, cancellationToken) is IMethodSymbol opMethod) {
					if (opMethod.MethodKind == MethodKind.UserDefinedOperator) {
						return CreateClassificationSpan(snapshot,
							itemSpan,
							node.RawKind == (int)SyntaxKind.OperatorDeclaration
								? TransientTags.OverrideDeclaration
								: __Classifications.OverrideMember);
					}
					if (opMethod.MethodKind == MethodKind.LambdaMethod) {
						var l = ClassifyLambdaExpression(itemSpan, snapshot, semanticModel, unitCompilation);
						if (l != null) {
							return l;
						}
					}
				}
				return null;
			}

			static TagSpan<IClassificationTag> ClassifyPunctuation(TextSpan itemSpan, ITextSnapshot snapshot, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation, CancellationToken cancellationToken) {
				if ((HighlightOptions.AllBraces || HighlightOptions.AttributeAnnotation) && itemSpan.Length == 1) {
					switch (snapshot[itemSpan.Start]) {
						case '(':
						case ')':
							return HighlightOptions.AllParentheses ? ClassifyParentheses(itemSpan, snapshot, semanticModel, unitCompilation, cancellationToken) : null;
						case '{':
						case '}':
							return HighlightOptions.AllBraces ? ClassifyCurlyBraces(itemSpan, snapshot, unitCompilation) : null;
						case '[':
						case ']':
							return HighlightOptions.AttributeAnnotation ? ClassifyBrackets(itemSpan, snapshot, semanticModel, unitCompilation, cancellationToken) : null;
					}
				}
				return null;
			}

			static TagSpan<IClassificationTag> ClassifyCurlyBraces(TextSpan itemSpan, ITextSnapshot snapshot, CompilationUnitSyntax unitCompilation) {
				var node = unitCompilation.FindNode(itemSpan, true, true);
				if (node is BaseTypeDeclarationSyntax == false
					&& node is ExpressionSyntax == false
					&& node is NamespaceDeclarationSyntax == false
					&& node is AccessorDeclarationSyntax == false
					&& !node.IsKind(SyntaxKind.SwitchStatement)
					&& (node = node.Parent) == null) {
					return null;
				}
				var type = ClassifySyntaxNode(node,
					node is ExpressionSyntax || node is AccessorDeclarationSyntax
						? HighlightOptions.MemberBraceTags
						: HighlightOptions.MemberDeclarationBraceTags,
					HighlightOptions.KeywordBraceTags);
				return type != null
					? CreateClassificationSpan(snapshot, itemSpan, type)
					: null;
			}

			static TagSpan<IClassificationTag> ClassifyParentheses(TextSpan itemSpan, ITextSnapshot snapshot, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation, CancellationToken cancellationToken) {
				var node = unitCompilation.FindNode(itemSpan, true, true);
				switch (node.Kind()) {
					case SyntaxKind.CastExpression:
						return HighlightOptions.KeywordBraceTags.TypeCast != null
								&& semanticModel.GetSymbolInfo(((CastExpressionSyntax)node).Type, cancellationToken).Symbol != null
							? CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.KeywordBraceTags.TypeCast)
							: null;
					case SyntaxKind.ParenthesizedExpression:
						return (HighlightOptions.KeywordBraceTags.TypeCast != null
								&& node.ChildNodes().FirstOrDefault().IsKind(SyntaxKind.AsExpression)
								&& semanticModel.GetSymbolInfo(((BinaryExpressionSyntax)node.ChildNodes().First()).Right, cancellationToken).Symbol != null)
							? CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.KeywordBraceTags.TypeCast)
							: null;
					case SyntaxKind.SwitchStatement:
					case SyntaxKind.SwitchSection:
					case SyntaxKind.IfStatement:
					case SyntaxKind.ElseClause:
					case CodeAnalysisHelper.PositionalPatternClause:
						return CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.KeywordBraceTags.Branching);
					case SyntaxKind.ForStatement:
					case SyntaxKind.ForEachStatement:
					case SyntaxKind.ForEachVariableStatement:
					case SyntaxKind.WhileStatement:
					case SyntaxKind.DoStatement:
						return CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.KeywordBraceTags.Loop);
					case SyntaxKind.UsingStatement:
					case SyntaxKind.FixedStatement:
					case SyntaxKind.LockStatement:
					case SyntaxKind.UnsafeStatement:
					case SyntaxKind.TryStatement:
					case SyntaxKind.CatchDeclaration:
					case SyntaxKind.CatchClause:
					case SyntaxKind.CatchFilterClause:
					case SyntaxKind.FinallyClause:
						return CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.KeywordBraceTags.Resource);
					case SyntaxKind.ParenthesizedVariableDesignation:
						return CreateClassificationSpan(snapshot, itemSpan, node.Parent.IsKind(CodeAnalysisHelper.VarPattern)
							? HighlightOptions.KeywordBraceTags.Branching
							: HighlightOptions.MemberBraceTags.Constructor);
					case SyntaxKind.TupleExpression:
					case SyntaxKind.TupleType:
						return CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.MemberBraceTags.Constructor);
					case SyntaxKind.CheckedExpression:
					case SyntaxKind.UncheckedExpression:
						return CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.KeywordBraceTags.TypeCast);
				}
				if (HighlightOptions.MemberBraceTags.Constructor != null) {
					// SpecialHighlightOptions.ParameterBrace or SpecialHighlightOptions.SpecialPunctuation is ON
					node = (node as BaseArgumentListSyntax
					   ?? node as BaseParameterListSyntax
					   ?? (CSharpSyntaxNode)(node as CastExpressionSyntax)
					   )?.Parent;
					if (node != null) {
						var type = ClassifySyntaxNode(node, HighlightOptions.MemberBraceTags, HighlightOptions.KeywordBraceTags);
						if (type != null) {
							return CreateClassificationSpan(snapshot, itemSpan, type);
						}
					}
				}
				return null;
			}

			static TagSpan<IClassificationTag> ClassifyBrackets(TextSpan itemSpan, ITextSnapshot snapshot, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation, CancellationToken cancellationToken) {
				var node = unitCompilation.FindNode(itemSpan, true, false);
				if (node.IsKind(SyntaxKind.Argument)) {
					node = ((ArgumentSyntax)node).Expression;
				}
				switch (node.Kind()) {
					case SyntaxKind.BracketedArgumentList:
						return (node = node.Parent).IsKind(SyntaxKind.ElementAccessExpression)
							&& semanticModel.GetTypeInfo(((ElementAccessExpressionSyntax)node).Expression, cancellationToken).Type?.TypeKind != TypeKind.Array
							? CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.MemberBraceTags.Property)
							: node.IsKind(SyntaxKind.VariableDeclarator)
							? CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.MemberDeclarationBraceTags.Constructor)
							: null;
					case SyntaxKind.BracketedParameterList:
						return node.Parent.IsKind(SyntaxKind.IndexerDeclaration)
							? CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.MemberDeclarationBraceTags.Property)
							: null;
					case SyntaxKind.AttributeList:
						return CreateClassificationSpan(snapshot, node.Span, __Classifications.AttributeNotation);
					case SyntaxKind.ArrayRankSpecifier:
						return node.Parent.Parent.Kind().IsAny(SyntaxKind.ArrayCreationExpression, SyntaxKind.StackAllocArrayCreationExpression, SyntaxKind.ImplicitStackAllocArrayCreationExpression)
							? CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.MemberBraceTags.Constructor)
							: null;
					case SyntaxKind.ImplicitStackAllocArrayCreationExpression:
					case SyntaxKind.ImplicitArrayCreationExpression:
						return CreateClassificationSpan(snapshot, itemSpan, HighlightOptions.MemberBraceTags.Constructor);
				}
				return null;
			}

			static TagSpan<IClassificationTag> ClassifyLambdaExpression(TextSpan itemSpan, ITextSnapshot snapshot, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation) {
				if (HighlightOptions.CapturingLambda) {
					var node = unitCompilation.FindNode(itemSpan, true, true);
					if (node is LambdaExpressionSyntax) {
						var ss = node.AncestorsAndSelf().FirstOrDefault(i => i is StatementSyntax || i is ExpressionSyntax && i.IsKind(SyntaxKind.IdentifierName) == false);
						if (ss != null) {
							var df = semanticModel.AnalyzeDataFlow(ss);
							if (df.ReadInside.Any(i => (i as ILocalSymbol)?.IsConst != true && df.VariablesDeclared.Contains(i) == false)) {
								return CreateClassificationSpan(snapshot, itemSpan, TransientKeywordTagHolder.Bold.Resource);
							}
						}
					}
				}
				return null;
			}

			static TagSpan<IClassificationTag> ClassifyXmlDoc(TextSpan itemSpan, ITextSnapshot snapshot, CompilationUnitSyntax unitCompilation, ref TextSpan lastTriviaSpan) {
				if (lastTriviaSpan.Contains(itemSpan)) {
					return null;
				}
				var trivia = unitCompilation.FindTrivia(itemSpan.Start);
				switch (trivia.Kind()) {
					case SyntaxKind.SingleLineDocumentationCommentTrivia:
					case SyntaxKind.MultiLineDocumentationCommentTrivia:
					case SyntaxKind.DocumentationCommentExteriorTrivia:
						lastTriviaSpan = trivia.FullSpan;
						return CreateClassificationSpan(snapshot, lastTriviaSpan, __Classifications.XmlDoc);
				}
				return null;
			}

			static ClassificationTag ClassifySyntaxNode(SyntaxNode node, TransientMemberTagHolder tag, TransientKeywordTagHolder keyword) {
				switch (node.Kind()) {
					case SyntaxKind.MethodDeclaration:
					case SyntaxKind.AnonymousMethodExpression:
					case SyntaxKind.SimpleLambdaExpression:
					case SyntaxKind.ParenthesizedLambdaExpression:
					case SyntaxKind.ConversionOperatorDeclaration:
					case SyntaxKind.OperatorDeclaration:
					case SyntaxKind.LocalFunctionStatement:
						return tag.Method;
					case SyntaxKind.InvocationExpression:
						return ((((InvocationExpressionSyntax)node).Expression as IdentifierNameSyntax)?.Identifier.ValueText == "nameof") ? null : tag.Method;
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
						return tag.Constructor;
					case SyntaxKind.IndexerDeclaration:
					case SyntaxKind.PropertyDeclaration:
					case SyntaxKind.GetAccessorDeclaration:
					case SyntaxKind.SetAccessorDeclaration:
						return tag.Property;
					case SyntaxKind.ClassDeclaration:
					case CodeAnalysisHelper.RecordDeclaration:
						return tag.Class;
					case SyntaxKind.InterfaceDeclaration:
						return tag.Interface;
					case SyntaxKind.EnumDeclaration:
						return tag.Enum;
					case CodeAnalysisHelper.RecordStructDeclaration:
					case SyntaxKind.StructDeclaration:
						return tag.Struct;
					case SyntaxKind.Attribute:
						return __Classifications.AttributeName;
					case SyntaxKind.EventDeclaration:
					case SyntaxKind.AddAccessorDeclaration:
					case SyntaxKind.RemoveAccessorDeclaration:
						return tag.Event;
					case SyntaxKind.DelegateDeclaration:
						return tag.Delegate;
					case SyntaxKind.NamespaceDeclaration:
						return tag.Namespace;
					case SyntaxKind.IfStatement:
					case SyntaxKind.ElseClause:
					case SyntaxKind.SwitchStatement:
					case SyntaxKind.SwitchSection:
					case CodeAnalysisHelper.SwitchExpression:
					case CodeAnalysisHelper.RecursivePattern:
						return keyword.Branching;
					case SyntaxKind.ForStatement:
					case SyntaxKind.ForEachStatement:
					case SyntaxKind.ForEachVariableStatement:
					case SyntaxKind.WhileStatement:
					case SyntaxKind.DoStatement:
						return keyword.Loop;
					case SyntaxKind.UsingStatement:
					case SyntaxKind.LockStatement:
					case SyntaxKind.FixedStatement:
					case SyntaxKind.UnsafeStatement:
					case SyntaxKind.TryStatement:
					case SyntaxKind.CatchClause:
					case SyntaxKind.CatchFilterClause:
					case SyntaxKind.FinallyClause:
						return keyword.Resource;
					case SyntaxKind.CheckedStatement:
					case SyntaxKind.UncheckedStatement:
						return keyword.TypeCast;
				}
				return null;
			}

			static Chain<ClassificationTag> GetClassificationType(SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken) {
				var symbol = semanticModel.GetSymbolInfo(node, cancellationToken).Symbol;
				var tags = new Chain<ClassificationTag>();
				if (symbol is null) {
					symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken);
					if (symbol is null) {
						// NOTE: handle alias in using directive
						if ((node.Parent as NameEqualsSyntax)?.Parent is UsingDirectiveSyntax) {
							tags.Add(__Classifications.AliasNamespace);
						}
						else if (node is AttributeArgumentSyntax attributeArgument) {
							symbol = semanticModel.GetSymbolInfo(attributeArgument.Expression, cancellationToken).Symbol;
							if (symbol?.Kind == SymbolKind.Field && (symbol as IFieldSymbol)?.IsConst == true) {
								tags.Add(__Classifications.ConstField);
							}
						}
						symbol = FindSymbolOrSymbolCandidateForNode(node, semanticModel, cancellationToken);
						if (symbol is null) {
							goto EXIT;
						}
					}
					else {
						switch (symbol.Kind) {
							case SymbolKind.NamedType:
								tags.Add(symbol.ContainingType != null
									? __Classifications.NestedDeclaration
									: __Classifications.Declaration);
								break;
							case SymbolKind.Event:
								if (HighlightOptions.NonPrivateField
									&& symbol.DeclaredAccessibility >= Accessibility.ProtectedAndInternal) {
									tags.Add(__Classifications.NestedDeclaration);
								}
								break;
							case SymbolKind.Method:
								if (HighlightOptions.LocalFunctionDeclaration
									|| ((IMethodSymbol)symbol).MethodKind != MethodKind.LocalFunction) {
									tags.Add(__Classifications.NestedDeclaration);
								}
								if (((IMethodSymbol)symbol).MethodKind == MethodKind.LocalFunction) {
									tags.Add(__Classifications.LocalFunctionDeclaration);
								}
								break;
							case SymbolKind.Property:
								if (symbol.ContainingType.IsAnonymousType == false) {
									tags.Add(__Classifications.NestedDeclaration);
								}
								break;
							case SymbolKind.Field:
								if (node.IsKind(SyntaxKind.TupleElement)) {
									if (((TupleElementSyntax)node).Identifier.IsKind(SyntaxKind.None)) {
										symbol = semanticModel.GetTypeInfo(((TupleElementSyntax)node).Type, cancellationToken).Type;
										if (symbol is null) {
											goto EXIT;
										}
									}
								}
								else if (HighlightOptions.NonPrivateField
									&& symbol.DeclaredAccessibility >= Accessibility.ProtectedAndInternal
									&& symbol.ContainingType.TypeKind != TypeKind.Enum) {
									tags.Add(__Classifications.NestedDeclaration);
								}
								break;
							case SymbolKind.Local:
								tags.Add(__Classifications.LocalDeclaration);
								break;
							case SymbolKind.RangeVariable:
								return tags.Add(__Classifications.LocalDeclaration);
						}
					}
				}
				switch (symbol.Kind) {
					case SymbolKind.Alias:
					case SymbolKind.ArrayType:
					case SymbolKind.Assembly:
					case SymbolKind.DynamicType:
					case SymbolKind.ErrorType:
					case SymbolKind.NetModule:
					case SymbolKind.PointerType:
					case SymbolKind.RangeVariable:
					case SymbolKind.Preprocessing:
						//case SymbolKind.Discard:
						goto EXIT;

					case SymbolKind.Label:
						tags.Add(__Classifications.Label);
						goto EXIT;

					case SymbolKind.TypeParameter:
						tags.Add(__Classifications.TypeParameter);
						goto EXIT;

					case SymbolKind.Field:
						var f = symbol as IFieldSymbol;
						if (f.IsConst) {
							tags.Add(f.ContainingType.TypeKind == TypeKind.Enum
								? __Classifications.EnumField
								: __Classifications.ConstField);
						}
						else {
							tags.Add(f.IsReadOnly ? __Classifications.ReadOnlyField
								: f.IsVolatile ? __Classifications.VolatileField
								: __Classifications.Field);
						}
						break;

					case SymbolKind.Property:
						tags.Add(__Classifications.Property);
						break;

					case SymbolKind.Event:
						tags.Add(__Classifications.Event);
						break;

					case SymbolKind.Local:
						tags.Add(((ILocalSymbol)symbol).IsConst
							? __Classifications.ConstField
							: __Classifications.LocalVariable);
						break;

					case SymbolKind.Namespace:
						tags.Add(__Classifications.Namespace);
						goto EXIT;

					case SymbolKind.Parameter:
						tags.Add(__Classifications.Parameter);
						break;

					case SymbolKind.Method:
						var methodSymbol = symbol as IMethodSymbol;
						switch (methodSymbol.MethodKind) {
							case MethodKind.Constructor:
								tags.Add(
									node is AttributeSyntax || node.Parent is AttributeSyntax || node.Parent?.Parent is AttributeSyntax
										? __Classifications.AttributeName
										: HighlightOptions.StyleConstructorAsType
										? (methodSymbol.ContainingType.TypeKind == TypeKind.Struct ? __Classifications.StructName : __Classifications.ClassName)
										: __Classifications.ConstructorMethod);
								break;
							case MethodKind.Destructor:
							case MethodKind.StaticConstructor:
								tags.Add(HighlightOptions.StyleConstructorAsType
										? (methodSymbol.ContainingType.TypeKind == TypeKind.Struct
											? __Classifications.StructName
											: __Classifications.ClassName)
										: __Classifications.ConstructorMethod);
								break;
							default:
								tags.Add(methodSymbol.IsExtensionMethod ? __Classifications.ExtensionMethod
									: methodSymbol.IsExtern ? __Classifications.ExternMethod
									: __Classifications.Method);
								break;
						}
						break;

					case SymbolKind.NamedType:
						break;

					default:
						goto EXIT;
				}

				if (SymbolMarkManager.HasBookmark) {
					var markerStyle = SymbolMarkManager.GetSymbolMarkerStyle(symbol);
					if (markerStyle != null) {
						tags.Add(markerStyle);
					}
				}

				if (FormatStore.IdentifySymbolSource && symbol.IsMemberOrType() && symbol.ContainingAssembly != null) {
					tags.Add(symbol.ContainingAssembly.GetSourceType() == AssemblySource.Metadata
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
							tags.Add(__Classifications.PrivateMember);
							break;
					}
				}
				if (symbol.IsStatic) {
					if (symbol.Kind != SymbolKind.Namespace) {
						tags.Add(__Classifications.StaticMember);
						if (symbol.IsAbstract && symbol.ContainingType?.TypeKind == TypeKind.Interface) {
							tags.Add(__Classifications.AbstractMember);
						}
					}
				}
				else if (symbol.IsSealed) {
					ITypeSymbol type;
					if (symbol.Kind == SymbolKind.NamedType
						&& (type = (ITypeSymbol)symbol).TypeKind == TypeKind.Struct) {
						if (type.IsReadOnly()) {
							tags.Add(__Classifications.ReadOnlyStruct);
						}
						if (type.IsRefLike()) {
							tags.Add(__Classifications.RefStruct);
						}
						goto EXIT;
					}
					tags.Add(__Classifications.SealedMember);
				}
				else if (symbol.IsOverride) {
					tags.Add(__Classifications.OverrideMember);
				}
				else if (symbol.IsVirtual) {
					tags.Add(__Classifications.VirtualMember);
				}
				else if (symbol.IsAbstract) {
					tags.Add(__Classifications.AbstractMember);
				}
				EXIT:
				return tags;
			}

			static ISymbol FindSymbolOrSymbolCandidateForNode(SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken) {
				return node.Parent.IsKind(SyntaxKind.SimpleMemberAccessExpression) ? semanticModel.GetSymbolInfo(node.Parent, cancellationToken).CandidateSymbols.FirstOrDefault()
					: node.Parent.IsKind(SyntaxKind.Argument) ? semanticModel.GetSymbolInfo(((ArgumentSyntax)node.Parent).Expression, cancellationToken).CandidateSymbols.FirstOrDefault()
					: node.IsKind(SyntaxKind.SimpleBaseType) ? semanticModel.GetTypeInfo(((SimpleBaseTypeSyntax)node).Type, cancellationToken).Type
					: node.IsKind(SyntaxKind.TypeConstraint) ? semanticModel.GetTypeInfo(((TypeConstraintSyntax)node).Type, cancellationToken).Type
					: node.IsKind(SyntaxKind.ExpressionStatement) ? semanticModel.GetSymbolInfo(((ExpressionStatementSyntax)node).Expression, cancellationToken).CandidateSymbols.FirstOrDefault()
					: semanticModel.GetSymbolInfo(node, cancellationToken).CandidateSymbols.FirstOrDefault();
			}

			static TagSpan<IClassificationTag> CreateClassificationSpan(ITextSnapshot snapshotSpan, TextSpan span, ClassificationTag tag) {
				return tag != null ? new TagSpan<IClassificationTag>(new SnapshotSpan(snapshotSpan, span.Start, span.Length), tag) : null;
			}
		}

		static class TransientTags
		{
			public static readonly ClassificationTag ClassDeclaration = ClassificationTagHelper.CreateTransientClassificationTag(__Classifications.ClassName, __Classifications.Declaration);

			public static readonly ClassificationTag StructDeclaration = ClassificationTagHelper.CreateTransientClassificationTag(__Classifications.StructName, __Classifications.Declaration);

			public static readonly ClassificationTag ConstructorDeclaration = ClassificationTagHelper.CreateTransientClassificationTag(__Classifications.ConstructorMethod, __Classifications.NestedDeclaration);

			public static readonly ClassificationTag MethodDeclaration = ClassificationTagHelper.CreateTransientClassificationTag(__Classifications.Method, __Classifications.NestedDeclaration);

			public static readonly ClassificationTag EventDeclaration = ClassificationTagHelper.CreateTransientClassificationTag(__Classifications.Event, __Classifications.NestedDeclaration);

			public static readonly ClassificationTag PropertyDeclaration = ClassificationTagHelper.CreateTransientClassificationTag(__Classifications.Property, __Classifications.NestedDeclaration);

			public static readonly ClassificationTag DestructorDeclaration = ClassificationTagHelper.CreateTransientClassificationTag(__Classifications.ResourceKeyword, __Classifications.NestedDeclaration);

			public static readonly ClassificationTag OverrideDeclaration = ClassificationTagHelper.CreateTransientClassificationTag(__Classifications.OverrideMember, __Classifications.NestedDeclaration);
		}

		static class HighlightOptions
		{
			static readonly bool __Dummy = Init();
			public static TransientKeywordTagHolder KeywordBraceTags { get; private set; }
			public static TransientMemberTagHolder MemberDeclarationBraceTags { get; private set; }
			public static TransientMemberTagHolder MemberBraceTags { get; private set; }

			// use fields to cache option flags
			public static bool AllBraces, AllParentheses, LocalFunctionDeclaration, NonPrivateField, StyleConstructorAsType, CapturingLambda, AttributeAnnotation;

			static bool Init() {
				Config.RegisterUpdateHandler(UpdateCSharpHighlighterConfig);
				UpdateCSharpHighlighterConfig(new ConfigUpdatedEventArgs(Config.Instance, Features.SyntaxHighlight));
				return true;
			}

			static void UpdateCSharpHighlighterConfig(ConfigUpdatedEventArgs e) {
				if (e.UpdatedFeature.MatchFlags(Features.SyntaxHighlight) == false) {
					return;
				}
				var o = e.Config.SpecialHighlightOptions;
				AllBraces = o.HasAnyFlag(SpecialHighlightOptions.AllBraces);
				AllParentheses = o.HasAnyFlag(SpecialHighlightOptions.AllParentheses);
				CapturingLambda = o.MatchFlags(SpecialHighlightOptions.CapturingLambdaExpression);
				var sp = o.MatchFlags(SpecialHighlightOptions.SpecialPunctuation);
				if (sp) {
					KeywordBraceTags = TransientKeywordTagHolder.Bold.Clone();
					MemberBraceTags = TransientMemberTagHolder.BoldBraces.Clone();
					MemberDeclarationBraceTags = TransientMemberTagHolder.BoldDeclarationBraces.Clone();
				}
				else {
					KeywordBraceTags = TransientKeywordTagHolder.Default.Clone();
					MemberBraceTags = TransientMemberTagHolder.Default.Clone();
					MemberDeclarationBraceTags = TransientMemberTagHolder.DeclarationBraces.Clone();
				}
				if (o.MatchFlags(SpecialHighlightOptions.BranchBrace) == false) {
					KeywordBraceTags.Branching = sp ? ClassificationTagHelper.BoldTag : null;
				}
				if (o.MatchFlags(SpecialHighlightOptions.CastBrace) == false) {
					KeywordBraceTags.TypeCast = sp ? ClassificationTagHelper.BoldTag : null;
				}
				if (o.MatchFlags(SpecialHighlightOptions.LoopBrace) == false) {
					KeywordBraceTags.Loop = sp ? ClassificationTagHelper.BoldTag : null;
				}
				if (o.MatchFlags(SpecialHighlightOptions.ResourceBrace) == false) {
					KeywordBraceTags.Resource = sp ? ClassificationTagHelper.BoldTag : null;
				}
				if (o.MatchFlags(SpecialHighlightOptions.ParameterBrace) == false) {
					MemberBraceTags.Constructor = sp ? ClassificationTagHelper.BoldTag : null;
					MemberBraceTags.Method = sp ? ClassificationTagHelper.BoldTag : null;
				}
				if (o.MatchFlags(SpecialHighlightOptions.DeclarationBrace) == false) {
					MemberDeclarationBraceTags.Class
						= MemberDeclarationBraceTags.Constructor
						= MemberDeclarationBraceTags.Delegate
						= MemberDeclarationBraceTags.Enum
						= MemberDeclarationBraceTags.Event
						= MemberDeclarationBraceTags.Field
						= MemberDeclarationBraceTags.Interface
						= MemberDeclarationBraceTags.Method
						= MemberDeclarationBraceTags.Namespace
						= MemberDeclarationBraceTags.Property
						= MemberDeclarationBraceTags.Struct
						= sp ? ClassificationTagHelper.BoldDeclarationBraceTag : ClassificationTagHelper.DeclarationBraceTag;
				}
				LocalFunctionDeclaration = o.MatchFlags(SpecialHighlightOptions.LocalFunctionDeclaration);
				NonPrivateField = o.MatchFlags(SpecialHighlightOptions.NonPrivateField);
				StyleConstructorAsType = o.MatchFlags(SpecialHighlightOptions.UseTypeStyleOnConstructor);
				AttributeAnnotation = FormatStore.GetStyles().TryGetValue(Constants.CSharpAttributeNotation, out var s) && s.IsSet;
			}
		}
	}
}