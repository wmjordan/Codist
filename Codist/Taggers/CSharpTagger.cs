using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using CLR;
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

		public bool Disabled { get; set; }
		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public IEnumerable<ITagSpan<IClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
			var p = _Parser;
			if (p == null || Disabled) {
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

		static class Tagger
		{
			#region cache tag results for reusing among subsequent calls for the same spans
			static readonly WeakReference<IEnumerable<SnapshotSpan>> __LastSpans = new WeakReference<IEnumerable<SnapshotSpan>>(null);
			static TagHolder __LastTags;
			#endregion

			public static IEnumerable<ITagSpan<IClassificationTag>> GetTags(IEnumerable<SnapshotSpan> spans, SemanticState result, CancellationToken cancellationToken) {
				try {
					if (__LastSpans.TryGetTarget(out var lastSpans) && lastSpans == spans) {
						return __LastTags;
					}
					__LastSpans.SetTarget(spans);
					return __LastTags = GetTagsInternal(spans, result, cancellationToken);
				}
				catch (OperationCanceledException) {
					__LastSpans.SetTarget(null);
					return Enumerable.Empty<ITagSpan<IClassificationTag>>();
				}
			}
			static TagHolder GetTagsInternal(IEnumerable<SnapshotSpan> spans, SemanticState result, CancellationToken cancellationToken) {
				var workspace = result.Workspace;
				var semanticModel = result.Model;
				var compilationUnit = result.GetCompilationUnit(cancellationToken);
				var snapshot = result.Snapshot;
				var l = semanticModel.SyntaxTree.Length;
				var tags = new TagHolder(snapshot);
				foreach (var span in spans) {
					if (span.End > l || cancellationToken.IsCancellationRequested) {
						return tags;
					}
					var textSpan = new TextSpan(span.Start.Position, span.Length);
					var classifiedSpans = Classifier.GetClassifiedSpans(semanticModel, textSpan, workspace, cancellationToken);
					var lastTriviaSpan = default(TextSpan);
					SyntaxNode node;
					GetAttributeNotationSpan(textSpan, in tags, compilationUnit);

					foreach (var item in classifiedSpans) {
						var ct = item.ClassificationType;
						switch (ct) {
							case "keyword":
							case Constants.CodeKeywordControl:
								node = compilationUnit.FindNode(item.TextSpan, true, true);
								if (node is MemberDeclarationSyntax || node is AccessorDeclarationSyntax) {
									ClassifyDeclarationKeyword(item.TextSpan, in tags, node, compilationUnit);
									continue;
								}
								else {
									ClassifyKeyword(item.TextSpan, in tags, node, compilationUnit);
								}
								continue;
							case Constants.CodeOperator:
							case Constants.CodeOverloadedOperator:
								ClassifyOperator(item.TextSpan, in tags, semanticModel, compilationUnit, cancellationToken);
								continue;
							case Constants.CodePunctuation:
								ClassifyPunctuation(item.TextSpan, snapshot, in tags, semanticModel, compilationUnit, cancellationToken);
								continue;
							case Constants.XmlDocDelimiter:
								ClassifyXmlDoc(item.TextSpan, in tags, compilationUnit, ref lastTriviaSpan);
								continue;
						}
						if (ct == Constants.CodeIdentifier
							|| ct.EndsWith("name", StringComparison.Ordinal)) {
							textSpan = item.TextSpan;
							node = compilationUnit.FindNode(textSpan, true, true);
							ClassifyIdentifier(new SnapshotSpan(snapshot, textSpan.Start, textSpan.Length), in tags, node, semanticModel, cancellationToken);
						}
					}
				}
				return tags;
			}

			static void GetAttributeNotationSpan(TextSpan textSpan, in TagHolder tags, CompilationUnitSyntax unitCompilation) {
				var spanNode = unitCompilation.FindNode(textSpan, true, false);
				if (spanNode.HasLeadingTrivia && spanNode.GetLeadingTrivia().FullSpan.Contains(textSpan)) {
					return;
				}
				switch (spanNode.Kind()) {
					case SyntaxKind.AttributeArgument:
					case SyntaxKind.AttributeArgumentList:
						tags.Add(textSpan, __Classifications.AttributeNotation);
						return;
				}
			}

			static void ClassifyDeclarationKeyword(TextSpan itemSpan, in TagHolder tags, SyntaxNode node, CompilationUnitSyntax unitCompilation) {
				ClassificationTag tag;
				switch (unitCompilation.FindToken(itemSpan.Start).Kind()) {
					case SyntaxKind.SealedKeyword:
					case SyntaxKind.OverrideKeyword:
					case SyntaxKind.AbstractKeyword:
					case SyntaxKind.VirtualKeyword:
					case SyntaxKind.ProtectedKeyword:
					case SyntaxKind.NewKeyword:
						tag = __GeneralClassifications.AbstractionKeyword;
						break;
					case SyntaxKind.ThisKeyword:
						tag = __Classifications.Declaration;
						break;
					case SyntaxKind.UnsafeKeyword:
					case SyntaxKind.FixedKeyword:
						tag = __GeneralClassifications.ResourceKeyword;
						break;
					case SyntaxKind.ExplicitKeyword:
					case SyntaxKind.ImplicitKeyword:
						tags.Add(((ConversionOperatorDeclarationSyntax)node).Type.Span, __Classifications.NestedDeclaration);
						tag = __GeneralClassifications.TypeCastKeyword;
						break;
					case SyntaxKind.ReadOnlyKeyword:
						tag = __GeneralClassifications.TypeCastKeyword;
						break;
					case CodeAnalysisHelper.RecordKeyword: // workaround for missing classification type for record identifier
						tags.Add(((TypeDeclarationSyntax)node).Identifier.Span,
							node.IsKind(CodeAnalysisHelper.RecordDeclaration) ? __Classifications.ClassName : __Classifications.StructName,
							__Classifications.Declaration);
						return;
					default:
						return;
				}
				tags.Add(itemSpan, tag);
			}

			static void ClassifyKeyword(TextSpan itemSpan, in TagHolder tags, SyntaxNode node, CompilationUnitSyntax unitCompilation) {
				switch (node.Kind()) {
					case SyntaxKind.BreakStatement:
						if (node.Parent is SwitchSectionSyntax) {
							return;
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
						tags.Add(itemSpan, __GeneralClassifications.ControlFlowKeyword);
						return;
					case SyntaxKind.IfStatement:
					case SyntaxKind.ElseClause:
					case SyntaxKind.SwitchStatement:
					case SyntaxKind.CaseSwitchLabel:
					case SyntaxKind.DefaultSwitchLabel:
					case CodeAnalysisHelper.SwitchExpression:
					case SyntaxKind.CasePatternSwitchLabel:
					case SyntaxKind.WhenClause:
						tags.Add(itemSpan, __GeneralClassifications.BranchingKeyword);
						return;
					case SyntaxKind.ForStatement:
					case SyntaxKind.ForEachStatement:
					case SyntaxKind.ForEachVariableStatement:
					case SyntaxKind.WhileStatement:
					case SyntaxKind.DoStatement:
					case SyntaxKind.SelectClause:
					case SyntaxKind.FromClause:
						tags.Add(itemSpan, __GeneralClassifications.LoopKeyword);
						return;
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
						tags.Add(itemSpan, __GeneralClassifications.ResourceKeyword);
						return;
					case SyntaxKind.LocalDeclarationStatement:
						if (unitCompilation.FindToken(itemSpan.Start, true).IsKind(SyntaxKind.UsingKeyword)) {
							goto case SyntaxKind.UsingStatement;
						}
						return;
					case SyntaxKind.IsExpression:
					case SyntaxKind.IsPatternExpression:
					case SyntaxKind.AsExpression:
					case SyntaxKind.RefExpression:
					case SyntaxKind.RefType:
					case SyntaxKind.CheckedExpression:
					case SyntaxKind.CheckedStatement:
					case SyntaxKind.UncheckedExpression:
					case SyntaxKind.UncheckedStatement:
						tags.Add(itemSpan, __GeneralClassifications.TypeCastKeyword);
						return;
					case SyntaxKind.Argument:
					case SyntaxKind.Parameter:
					case SyntaxKind.CrefParameter:
						if (unitCompilation.FindToken(itemSpan.Start, true)
							.IsAnyKind(SyntaxKind.InKeyword, SyntaxKind.OutKeyword, SyntaxKind.RefKeyword)) {
							tags.Add(itemSpan, __GeneralClassifications.TypeCastKeyword);
						}
						return;
					case SyntaxKind.IdentifierName:
						if (node.Parent.IsKind(SyntaxKind.TypeConstraint)
							&& itemSpan.Length == 9
							&& node.ToString() == "unmanaged") {
							goto case SyntaxKind.UnsafeStatement;
						}
						return;
				}
			}
			static void ClassifyOperator(TextSpan itemSpan, in TagHolder tags, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation, CancellationToken cancellationToken) {
				var node = unitCompilation.FindNode(itemSpan);
				ClassificationTag tag;
				switch ((SyntaxKind)node.RawKind) {
					case SyntaxKind.DestructorDeclaration:
						tag = __GeneralClassifications.ResourceKeyword;
						goto CREATE_SPAN;
					case CodeAnalysisHelper.SwitchExpressionArm:
						tag = __GeneralClassifications.BranchingKeyword;
						goto CREATE_SPAN;
					case SyntaxKind.ArrowExpressionClause:
						switch (node.Parent.Kind()) {
							case SyntaxKind.MethodDeclaration:
							case SyntaxKind.LocalFunctionStatement:
							case SyntaxKind.OperatorDeclaration:
								tag = __Classifications.Method;
								goto CREATE_SPAN;
							case SyntaxKind.PropertyDeclaration:
							case SyntaxKind.GetAccessorDeclaration:
							case SyntaxKind.SetAccessorDeclaration:
							case SyntaxKind.IndexerDeclaration:
								tag = __Classifications.Property;
								goto CREATE_SPAN;
							case SyntaxKind.AddAccessorDeclaration:
							case SyntaxKind.RemoveAccessorDeclaration:
								tag = __Classifications.Event;
								goto CREATE_SPAN;
							case SyntaxKind.ConstructorDeclaration:
							case SyntaxKind.DestructorDeclaration:
								tag = __Classifications.ConstructorMethod;
								goto CREATE_SPAN;
						}
						return;
					case SyntaxKind.OperatorDeclaration:
						tags.Add(itemSpan, __Classifications.Method, __Classifications.NestedDeclaration);
						return;
				}
				if (semanticModel.GetSymbol(node.IsKind(SyntaxKind.Argument) ? ((ArgumentSyntax)node).Expression : node, cancellationToken) is IMethodSymbol opMethod
					&& opMethod.MethodKind == MethodKind.LambdaMethod) {
					ClassifyLambdaExpression(itemSpan, in tags, semanticModel, unitCompilation);
				}
				return;
				CREATE_SPAN:
				tags.Add(itemSpan,
					HighlightOptions.SemanticPunctuation ? tag : null,
					HighlightOptions.BoldSemanticPunctuationTag);
			}

			static void ClassifyPunctuation(TextSpan itemSpan, ITextSnapshot snapshot, in TagHolder tags, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation, CancellationToken cancellationToken) {
				if ((HighlightOptions.SemanticPunctuation || HighlightOptions.BoldSemanticPunctuation || HighlightOptions.AttributeAnnotation) && itemSpan.Length == 1) {
					switch (snapshot[itemSpan.Start]) {
						case '(':
						case ')':
							if (HighlightOptions.SemanticPunctuation || HighlightOptions.BoldSemanticPunctuation) {
								ClassifyParentheses(itemSpan, in tags, semanticModel, unitCompilation, cancellationToken);
							}
							return;
						case '{':
						case '}':
							if (HighlightOptions.SemanticPunctuation || HighlightOptions.BoldSemanticPunctuation) {
								ClassifyCurlyBraces(itemSpan, in tags, unitCompilation);
							}
							return;
						case '[':
						case ']':
							ClassifyBrackets(itemSpan, in tags, semanticModel, unitCompilation, cancellationToken);
							return;
					}
				}
			}

			static void ClassifyCurlyBraces(TextSpan itemSpan, in TagHolder tags, CompilationUnitSyntax unitCompilation) {
				var node = unitCompilation.FindNode(itemSpan, true, true);
				if (node is BaseTypeDeclarationSyntax == false
					&& node is ExpressionSyntax == false
					&& node is NamespaceDeclarationSyntax == false
					&& node is AccessorDeclarationSyntax == false
					&& !node.IsKind(SyntaxKind.SwitchStatement)
					&& (node = node.Parent) == null) {
					return;
				}
				var type = ClassifySemanticPunctuation(node);
				if (type != null) {
					if (HighlightOptions.SemanticPunctuation) {
						tags.Add(itemSpan,
							type,
							node is MemberDeclarationSyntax || node.IsKind(SyntaxKind.NamespaceDeclaration)
								? __Classifications.DeclarationBrace
								: null,
							HighlightOptions.BoldSemanticPunctuationTag);
					}
					else if (HighlightOptions.BoldSemanticPunctuationTag != null) {
						tags.Add(itemSpan, __GeneralClassifications.Bold);
					}
				}
			}

			static void ClassifyParentheses(TextSpan itemSpan, in TagHolder tags, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation, CancellationToken cancellationToken) {
				var node = unitCompilation.FindNode(itemSpan, true, true);
				ClassificationTag tag;
				switch (node.Kind()) {
					case SyntaxKind.CastExpression:
						if (semanticModel.GetSymbolInfo(((CastExpressionSyntax)node).Type, cancellationToken).Symbol != null) {
							tag = __GeneralClassifications.TypeCastKeyword;
							goto TAG;
						}
						return;
					case SyntaxKind.ParenthesizedExpression:
						if (node.ChildNodes().FirstOrDefault().IsKind(SyntaxKind.AsExpression)
							&& semanticModel.GetSymbolInfo(((BinaryExpressionSyntax)node.ChildNodes().First()).Right, cancellationToken).Symbol != null) {
							tag = __GeneralClassifications.TypeCastKeyword;
							goto TAG;
						}
						return;
					case SyntaxKind.SwitchStatement:
					case SyntaxKind.SwitchSection:
					case SyntaxKind.IfStatement:
					case SyntaxKind.ElseClause:
					case CodeAnalysisHelper.PositionalPatternClause:
						tag = __GeneralClassifications.BranchingKeyword;
						goto TAG;
					case SyntaxKind.ForStatement:
					case SyntaxKind.ForEachStatement:
					case SyntaxKind.ForEachVariableStatement:
					case SyntaxKind.WhileStatement:
					case SyntaxKind.DoStatement:
						tag = __GeneralClassifications.LoopKeyword;
						goto TAG;
					case SyntaxKind.UsingStatement:
					case SyntaxKind.FixedStatement:
					case SyntaxKind.LockStatement:
					case SyntaxKind.UnsafeStatement:
					case SyntaxKind.TryStatement:
					case SyntaxKind.CatchDeclaration:
					case SyntaxKind.CatchClause:
					case SyntaxKind.CatchFilterClause:
					case SyntaxKind.FinallyClause:
						tag = __GeneralClassifications.ResourceKeyword;
						goto TAG;
					case SyntaxKind.ParenthesizedVariableDesignation:
						tag = node.Parent.IsKind(CodeAnalysisHelper.VarPattern)
							? __GeneralClassifications.BranchingKeyword
							: __Classifications.ConstructorMethod;
						goto TAG;
					case SyntaxKind.TupleExpression:
					case SyntaxKind.TupleType:
						tag = __Classifications.ConstructorMethod;
						goto TAG;
					case SyntaxKind.CheckedExpression:
					case SyntaxKind.UncheckedExpression:
						tag = __GeneralClassifications.TypeCastKeyword;
						goto TAG;
				}
				node = (node as BaseArgumentListSyntax
				   ?? node as BaseParameterListSyntax
				   ?? (CSharpSyntaxNode)(node as CastExpressionSyntax)
				   )?.Parent;
				if (node != null) {
					tag = ClassifySemanticPunctuation(node);
					goto TAG;
				}
				return;
				TAG:
				if (tag != null) {
					tags.Add(itemSpan,
						HighlightOptions.SemanticPunctuation ? tag : null,
						HighlightOptions.BoldSemanticPunctuationTag);
				}
			}

			static void ClassifyBrackets(TextSpan itemSpan, in TagHolder tags, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation, CancellationToken cancellationToken) {
				var node = unitCompilation.FindNode(itemSpan, true, false);
				if (node.IsKind(SyntaxKind.Argument)) {
					node = ((ArgumentSyntax)node).Expression;
				}
				ClassificationTag tag;
				switch (node.Kind()) {
					case SyntaxKind.BracketedArgumentList:
						tag = (node = node.Parent).IsKind(SyntaxKind.ElementAccessExpression)
							&& semanticModel.GetTypeInfo(((ElementAccessExpressionSyntax)node).Expression, cancellationToken).Type?.TypeKind != TypeKind.Array
							? __Classifications.Property
							: node.IsKind(SyntaxKind.VariableDeclarator)
							? __Classifications.ConstructorMethod
							: null;
						break;
					case SyntaxKind.BracketedParameterList:
						tag = node.Parent.IsKind(SyntaxKind.IndexerDeclaration)
							? __Classifications.Property
							: null;
						break;
					case SyntaxKind.AttributeList:
						tags.Add(node.Span, __Classifications.AttributeNotation);
						return;
					case SyntaxKind.ArrayRankSpecifier:
						tag = node.Parent.Parent.IsAnyKind(SyntaxKind.ArrayCreationExpression, SyntaxKind.StackAllocArrayCreationExpression, SyntaxKind.ImplicitStackAllocArrayCreationExpression)
							? __Classifications.ConstructorMethod
							: null;
						break;
					case SyntaxKind.ImplicitStackAllocArrayCreationExpression:
					case SyntaxKind.ImplicitArrayCreationExpression:
					case CodeAnalysisHelper.CollectionExpression:
					case SyntaxKind.AttributeArgument:
						tag = __Classifications.ConstructorMethod;
						break;
					case CodeAnalysisHelper.ListPatternExpression:
						tag = __GeneralClassifications.BranchingKeyword;
						break;
					default: return;
				}

				if (tag != null) {
					tags.Add(itemSpan,
						HighlightOptions.SemanticPunctuation ? tag : null,
						HighlightOptions.BoldSemanticPunctuationTag);
				}
			}

			static void ClassifyLambdaExpression(TextSpan itemSpan, in TagHolder tags, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation) {
				if (HighlightOptions.CapturingLambda) {
					var node = unitCompilation.FindNode(itemSpan, true, true);
					if (node is LambdaExpressionSyntax
						&& node.AncestorsAndSelf()
							.FirstOrDefault(i => i is StatementSyntax || i is ExpressionSyntax && i.IsKind(SyntaxKind.IdentifierName) == false)
							?.HasCapturedVariable(semanticModel) == true) {
						tags.Add(itemSpan, __Classifications.VariableCapturedExpression);
					}
				}
			}

			static void ClassifyXmlDoc(TextSpan itemSpan, in TagHolder tags, CompilationUnitSyntax unitCompilation, ref TextSpan lastTriviaSpan) {
				if (lastTriviaSpan.Contains(itemSpan)) {
					return;
				}
				var trivia = unitCompilation.FindTrivia(itemSpan.Start);
				switch (trivia.Kind()) {
					case SyntaxKind.SingleLineDocumentationCommentTrivia:
					case SyntaxKind.MultiLineDocumentationCommentTrivia:
					case SyntaxKind.DocumentationCommentExteriorTrivia:
						lastTriviaSpan = trivia.FullSpan;
						tags.Add(lastTriviaSpan, __Classifications.XmlDoc);
						return;
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

			static void ClassifyIdentifier(SnapshotSpan itemSpan, in TagHolder tags, SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken) {
				var symbol = semanticModel.GetSymbolOrFirstCandidate(node, cancellationToken);
				if (symbol is null) {
					symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken);
					if (symbol is null) {
						// NOTE: handle alias in using directive
						if ((node.Parent as NameEqualsSyntax)?.Parent is UsingDirectiveSyntax) {
							tags.Add(itemSpan, __Classifications.AliasNamespace);
						}
						else if (node is AttributeArgumentSyntax attributeArgument) {
							symbol = semanticModel.GetSymbolOrFirstCandidate(attributeArgument.Expression, cancellationToken);
							if ((symbol as IFieldSymbol)?.IsConst == true) {
								tags.Add(itemSpan, __Classifications.ConstField);
							}
						}
						symbol = FindSymbolOrSymbolCandidateForNode(node, semanticModel, cancellationToken);
						if (symbol is null) {
							return;
						}
					}
					else {
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
											return;
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
								return;
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
						break;

					case SymbolKind.Event:
						tags.Add(itemSpan, __Classifications.Event);
						break;

					case SymbolKind.Local:
						tags.Add(itemSpan, ((ILocalSymbol)symbol).IsConst
							? __Classifications.ConstField
							: __Classifications.LocalVariable);
						break;

					case SymbolKind.Namespace:
						tags.Add(itemSpan, __Classifications.Namespace);
						return;

					case SymbolKind.Parameter:
						tags.Add(itemSpan, __Classifications.Parameter);
						break;

					case SymbolKind.Method:
						var methodSymbol = symbol as IMethodSymbol;
						switch (methodSymbol.MethodKind) {
							case MethodKind.Constructor:
								tags.Add(itemSpan,
									node is AttributeSyntax || node.Parent is AttributeSyntax || node.Parent?.Parent is AttributeSyntax
										? __Classifications.AttributeName
										: HighlightOptions.StyleConstructorAsType
										? (methodSymbol.ContainingType.TypeKind == TypeKind.Struct ? __Classifications.StructName : __Classifications.ClassName)
										: __Classifications.ConstructorMethod);
								break;
							case MethodKind.Destructor:
							case MethodKind.StaticConstructor:
								tags.Add(itemSpan, HighlightOptions.StyleConstructorAsType
										? (methodSymbol.ContainingType.TypeKind == TypeKind.Struct
											? __Classifications.StructName
											: __Classifications.ClassName)
										: __Classifications.ConstructorMethod);
								break;
							default:
								tags.Add(itemSpan, methodSymbol.IsExtensionMethod ? __Classifications.ExtensionMethod
									: methodSymbol.IsExtern ? __Classifications.ExternMethod
									: __Classifications.Method);
								break;
						}
						break;

					case SymbolKind.NamedType:
						if (symbol.ContainingType != null && symbol.Kind == SymbolKind.NamedType) {
							tags.Add(itemSpan, __Classifications.NestedType);
						}
						break;

					default:
						return;
				}

				if (SymbolMarkManager.HasBookmark) {
					var markerStyle = SymbolMarkManager.GetSymbolMarkerStyle(symbol);
					if (markerStyle != null) {
						tags.Add(itemSpan, markerStyle);
					}
				}

				if (FormatStore.IdentifySymbolSource && symbol.IsMemberOrType() && symbol.ContainingAssembly != null) {
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
					: semanticModel.GetSymbolInfo(node, cancellationToken).CandidateSymbols.FirstOrDefault();
			}
		}

		readonly struct TagHolder : IEnumerable<TagSpan<IClassificationTag>>
		{
			readonly ITextSnapshot _TextSnapshot;
			readonly Chain<TagSpan<IClassificationTag>> _Chain;

			public TagHolder(ITextSnapshot textSnapshot) : this() {
				_TextSnapshot = textSnapshot;
				_Chain = new Chain<TagSpan<IClassificationTag>>();
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

		static class HighlightOptions
		{
			// use fields to cache option flags
			public static bool SemanticPunctuation,
				BoldSemanticPunctuation,
				LocalFunctionDeclaration,
				NonPrivateField,
				StyleConstructorAsType,
				CapturingLambda,
				AttributeAnnotation;
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
				SemanticPunctuation = o.HasAnyFlag(SpecialHighlightOptions.SemanticPunctuation);
				CapturingLambda = o.MatchFlags(SpecialHighlightOptions.CapturingLambdaExpression);
				var bold = BoldSemanticPunctuation = o.MatchFlags(SpecialHighlightOptions.BoldSemanticPunctuation);
				BoldSemanticPunctuationTag = bold ? __GeneralClassifications.Bold : null;
				LocalFunctionDeclaration = o.MatchFlags(SpecialHighlightOptions.LocalFunctionDeclaration);
				NonPrivateField = o.MatchFlags(SpecialHighlightOptions.NonPrivateField);
				StyleConstructorAsType = o.MatchFlags(SpecialHighlightOptions.UseTypeStyleOnConstructor);
				AttributeAnnotation = FormatStore.GetStyles().TryGetValue(Constants.CSharpAttributeNotation, out var s) && s.IsSet;
			}
		}
	}
}