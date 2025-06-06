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

		protected override Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			// Map the trigger point down to our buffer.
			var buffer = session.GetSourceBuffer(out var triggerPoint);
			return buffer == null || triggerPoint >= buffer.CurrentSnapshot.Length
				? Task.FromResult<QuickInfoItem>(null)
				: InternalGetQuickInfoItemAsync(session, buffer, triggerPoint, cancellationToken);
		}

		sealed class Context
		{
			public readonly IAsyncQuickInfoSession session;
			public readonly ITextBuffer TextBuffer;
			public readonly SemanticContext semanticContext;
			public readonly SemanticModel semanticModel;
			public readonly CompilationUnitSyntax CompilationUnit;
			public readonly SnapshotPoint TriggerPoint;
			public readonly CancellationToken cancellationToken;

			InfoContainer _Container;
			QuickInfoItem _Result;

			public ISymbol symbol;
			public SyntaxNode node;
			public SyntaxToken token;
			public ImmutableArray<ISymbol> SymbolCandidates;
			public bool skipTriggerPointCheck;
			public bool isConvertedType;
			public bool keepBuiltInXmlDoc;
			public bool IsCandidate;
			public State State;
			public QuickInfoItem Result {
				get => _Result;
				set { _Result = value; State = State.DirectReturn; }
			}
			public InfoContainer Container => _Container ?? (_Container = new InfoContainer());

			public Context(IAsyncQuickInfoSession session, ITextBuffer textBuffer, SemanticContext semanticContext, SnapshotPoint triggerPoint, CancellationToken cancellationToken) {
				this.session = session;
				TextBuffer = textBuffer;
				this.semanticContext = semanticContext;
				this.semanticModel = semanticContext.SemanticModel;
				CompilationUnit = semanticModel.SyntaxTree.GetCompilationUnitRoot(cancellationToken);
				TriggerPoint = triggerPoint;
				this.cancellationToken = cancellationToken;
				//look for occurrences of our QuickInfo words in the span
				token = CompilationUnit.FindToken(triggerPoint, true);
			}

			public ITextSnapshot CurrentSnapshot => TextBuffer.CurrentSnapshot;

			public void UseTokenNode() {
				node = token.Parent;
			}

			public void SetSymbol(SymbolInfo symbolInfo) {
				symbolInfo.Symbol.SetNotDefault(ref symbol);
				if (symbolInfo.CandidateReason != CandidateReason.None) {
					SymbolCandidates = symbolInfo.CandidateSymbols;
					IsCandidate = true;
				}
			}

			public void SetSymbol(TypeInfo typeInfo) {
				if (typeInfo.ConvertedType.OriginallyEquals(typeInfo.Type) == false) {
					symbol = typeInfo.ConvertedType;
					isConvertedType = true;
				}
				else {
					symbol = typeInfo.Type;
				}
			}

			public QuickInfoItem CreateQuickInfoItem(object item) {
				session.KeepViewPosition();
				return new QuickInfoItem(token.Span.CreateSnapshotSpan(TextBuffer.CurrentSnapshot).ToTrackingSpan(), item);
			}
		}

		enum State
		{
			Undefined,
			PredefinedSymbol,
			Process,
			Return,
			AsType,
			DirectReturn,
			Unavailable,
			ReparseToken
		}

		async Task<QuickInfoItem> InternalGetQuickInfoItemAsync(IAsyncQuickInfoSession session, ITextBuffer textBuffer, SnapshotPoint triggerPoint, CancellationToken cancellationToken) {
			Action<Context> syntaxProcessor;
			if (session.TextView is Microsoft.VisualStudio.Text.Editor.IWpfTextView v == false) {
				return null;
			}
			var sc = SemanticContext.GetOrCreateSingletonInstance(v);
			if (await sc.UpdateAsync(textBuffer, triggerPoint, cancellationToken).ConfigureAwait(false) == false) {
				return null;
			}
			var semanticModel = sc.SemanticModel;
			if (semanticModel == null) {
				return null;
			}
			if (_SpecialProject == null) {
				_SpecialProject = new SpecialProjectInfo(semanticModel);
			}

			// the Quick Info override
			var o = Config.Instance.QuickInfoOptions.HasAnyFlag(QuickInfoOptions.QuickInfoOverride)
				? QuickInfoOverride.CreateOverride(session)
				: null;
			ObjectCreationExpressionSyntax ctor = null;
			var ctx = new Context(session, textBuffer, sc, triggerPoint, cancellationToken);
			if (ctx.token.Span.Contains(triggerPoint.Position, true) == false) {
				// skip when trigger point is on trivia
				return null;
			}
			#region Classify token
			do {
				ctx.State = State.Undefined;
				if (__TokenProcessors.TryGetValue((SyntaxKind)ctx.token.RawKind, out syntaxProcessor)) {
					syntaxProcessor(ctx);
				}
				else {
					ProcessToken(ctx);
				}
			} while (ctx.State >= State.ReparseToken);
			if (ctx.keepBuiltInXmlDoc && o != null) {
				o.OverrideBuiltInXmlDoc = false;
			}
			switch (ctx.State) {
				case State.Process: goto PROCESS;
				case State.PredefinedSymbol: ctx.UseTokenNode(); goto PROCESS;
				case State.Return: goto RETURN;
				case State.Unavailable: return null;
				case State.DirectReturn: return ctx.Result;
			}
			#endregion

			if (ResolveNode(ctx) == false) {
				return null;
			}

		PROCESS:
			if (ctx.node == null) {
				return null;
			}

			if (ctx.symbol == null) {
				ResolveSymbol(ctx);
			}
			if (__NodeProcessors.TryGetValue((SyntaxKind)ctx.node.RawKind, out syntaxProcessor)) {
				syntaxProcessor(ctx);
			}

		RETURN:
			if (ctx.symbol != null) {
				Chain<string> unavailableProjects = null;
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation)) {
					if (ctx.isConvertedType == false) {
						unavailableProjects = await SearchUnavailableProjectsAsync(sc.Document, ctx).ConfigureAwait(false);
					}
					ctor = ctx.node.Parent.UnqualifyExceptNamespace() as ObjectCreationExpressionSyntax;
					await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
					OverrideDocumentation(ctx.node,
						o,
						ctor != null
							? semanticModel.GetSymbolInfo(ctor, cancellationToken).Symbol ?? ctx.symbol
							: ctx.node.Parent.IsKind(CodeAnalysisHelper.PrimaryConstructorBaseType)
							? (ctx.symbol = semanticModel.GetSymbolInfo(ctx.node.Parent, cancellationToken).Symbol ?? ctx.symbol)
							: ctx.symbol,
						semanticModel,
						cancellationToken);
					if (ctx.symbol?.Kind == SymbolKind.RangeVariable) {
						ctx.Container.Add(new BlockItem(IconIds.LocalVariable, "Range Variable: ").Append(ctx.symbol.Name, true));
						semanticModel.GetTypeInfo(ctx.node, cancellationToken).Type.SetNotDefault(ref ctx.symbol);
					}
				}
				if (ctx.isConvertedType == false) {
					await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
					ShowSymbolInfo(ctx);
					if (unavailableProjects != null) {
						ShowUnavailableProjects(ctx, unavailableProjects);
					}
				}
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter)) {
				ShowArgumentInfo(ctx);
			}
			if (ctor == null) {
				ctor = ctx.node.Parent.UnqualifyExceptNamespace() as ObjectCreationExpressionSyntax;
			}
			if (ctor != null) {
				semanticModel.GetSymbolOrFirstCandidate(ctor, cancellationToken).SetNotDefault(ref ctx.symbol);
				if (ctx.symbol == null) {
					return null;
				}
				if (ctx.symbol.IsImplicitlyDeclared) {
					ctx.symbol = ctx.symbol.ContainingType;
				}
			}
			o?.ApplyClickAndGo(ctx.symbol);
			if (ctx.Container.ItemCount == 0 && ctx.isConvertedType == false) {
				if (ctx.symbol != null) {
					// place holder
					ctx.Container.Add(new ContentPresenter() { Name = "SymbolPlaceHolder" });
					return ctx.CreateQuickInfoItem(ctx.Container);
				}
				return null;
			}
			return ctx.CreateQuickInfoItem(ctx.Container);
		}

		static bool ResolveNode(Context context) {
			ref var node = ref context.node;
			node = context.CompilationUnit.FindNode(context.token.Span, true, true);
			if (node == null
				|| context.skipTriggerPointCheck == false
					&& node.Span.Contains(context.TriggerPoint.Position, true) == false) {
				return false;
			}
			node = node.UnqualifyExceptNamespace();
			return !node.IsKind(SyntaxKind.SkippedTokensTrivia);
		}

		static void ResolveSymbol(Context context) {
			context.symbol = GetSymbol(context.semanticModel, context.node, ref context.SymbolCandidates, context.cancellationToken);
			if (context.SymbolCandidates.IsDefaultOrEmpty == false) {
				context.IsCandidate = true;
				ShowCandidateInfo(context.Container, context.SymbolCandidates);
			}
		}

		static Task<Chain<string>> SearchUnavailableProjectsAsync(Document doc, Context ctx) {
			var solution = doc.Project.Solution;
			ImmutableArray<DocumentId> linkedDocuments;
			string docId;
			return solution.ProjectIds.Count < 2
				|| ctx.symbol.Kind.CeqAny(SymbolKind.Local, SymbolKind.Label, SymbolKind.ArrayType, SymbolKind.PointerType, SymbolKind.Preprocessing)
				|| (docId = ctx.symbol.GetDeclarationId()) is null
				|| (linkedDocuments = doc.GetLinkedDocumentIds()).Length == 0
				? Task.FromResult<Chain<string>>(null)
				: SearchUnavailableProjectsAsync(docId, solution, linkedDocuments, ctx.cancellationToken);
		}

		static async Task<Chain<string>> SearchUnavailableProjectsAsync(string docId, Solution solution, ImmutableArray<DocumentId> linkedDocuments, CancellationToken cancellationToken) {
			Chain<string> r = null;
			foreach (var id in linkedDocuments) {
				var d = solution.GetDocument(id);
				var compilation = await d.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
				if (compilation != null && DocumentationCommentId.GetFirstSymbolForDeclarationId(docId, compilation) != null) {
					continue;
				}
				if (r == null) {
					r = new Chain<string>();
				}
				r.Add(d.Project.Name);
			}
			return r;
		}

		static void ShowUnavailableProjects(Context context, Chain<string> unavailableProjects) {
			var doc = new GeneralInfoBlock(IconIds.UnavailableSymbol, R.T_SymbolUnavailableIn);
			foreach (var item in unavailableProjects) {
				doc.Add(new BlockItem(IconIds.Project, item));
			}
			context.Container.Add(doc);
		}

		static ISymbol GetSymbol(SemanticModel semanticModel, SyntaxNode node, ref ImmutableArray<ISymbol> candidates, CancellationToken cancellationToken) {
			var kind = node.Kind();
			if (kind.CeqAny(SyntaxKind.BaseExpression, SyntaxKind.DefaultLiteralExpression, SyntaxKind.ImplicitStackAllocArrayCreationExpression)) {
				return semanticModel.GetTypeInfo(node, cancellationToken).ConvertedType;
			}
			if (kind.CeqAny(SyntaxKind.ThisExpression, CodeAnalysisHelper.VarPattern, SyntaxKind.ImplicitArrayCreationExpression)) {
				return semanticModel.GetTypeInfo(node, cancellationToken).Type;
			}
			if (kind.CeqAny(SyntaxKind.TupleElement, SyntaxKind.ForEachStatement, SyntaxKind.FromClause, SyntaxKind.QueryContinuation, SyntaxKind.VariableDeclarator, SyntaxKind.CatchDeclaration)) {
				return semanticModel.GetDeclaredSymbol(node, cancellationToken);
			}
			if (node is QueryClauseSyntax q) {
				return node is OrderByClauseSyntax o && o.Orderings.Count != 0
					? semanticModel.GetSymbolInfo(o.Orderings[0], cancellationToken).Symbol
					: semanticModel.GetQueryClauseInfo(q, cancellationToken).OperationInfo.Symbol;
			}
			var symbolInfo = semanticModel.GetSymbolInfo(node, cancellationToken);
			if (symbolInfo.CandidateReason != CandidateReason.None) {
				return (candidates = symbolInfo.CandidateSymbols).FirstOrDefault();
			}
			return symbolInfo.Symbol
				?? (kind.IsDeclaration()
						|| kind == SyntaxKind.SingleVariableDesignation
							&& node.Parent.IsAnyKind(SyntaxKind.DeclarationExpression, SyntaxKind.DeclarationPattern, SyntaxKind.ParenthesizedVariableDesignation)
					? semanticModel.GetDeclaredSymbol(node, cancellationToken)
					: kind == SyntaxKind.IdentifierName && node.Parent.IsKind(SyntaxKind.NameEquals) && (node = node.Parent.Parent).IsKind(SyntaxKind.UsingDirective)
					? semanticModel.GetDeclaredSymbol(node, cancellationToken)?.GetAliasTarget()
					: semanticModel.GetSymbolExt(node, cancellationToken));
		}

		static void LocateNodeInParameterList(Context ctx) {
			ref var node = ref ctx.node;
			var token = ctx.token;
			if (node.IsKind(SyntaxKind.Argument)) {
				node = ((ArgumentSyntax)node).Expression;
				return;
			}
			if (node.IsKind(SyntaxKind.ArgumentList)) {
				var al = node as ArgumentListSyntax;
				if (al.OpenParenToken == token) {
					node = al.Arguments.FirstOrDefault() ?? node;
					return;
				}
				if (al.CloseParenToken == token) {
					node = al.Arguments.LastOrDefault() ?? node;
					return;
				}
				var tokenStart = token.SpanStart;
				foreach (var item in al.Arguments) {
					if (item.FullSpan.Contains(tokenStart, true)) {
						node = item;
						return;
					}
				}
			}
		}

		static void OverrideDocumentation(SyntaxNode node, IQuickInfoOverride qiWrapper, ISymbol symbol, SemanticModel semanticModel, CancellationToken cancellationToken) {
			if (symbol == null) {
				return;
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
		}

		static void ShowCandidateInfo(InfoContainer qiContent, ImmutableArray<ISymbol> candidates) {
			var info = new GeneralInfoBlock(IconIds.SymbolCandidate, R.T_Maybe);
			foreach (var item in candidates) {
				info.Add(new BlockItem(item.GetImageId()).AddSymbolDisplayParts(item.OriginalDefinition.ToDisplayParts(CodeAnalysisHelper.QuickInfoSymbolDisplayFormat), __SymbolFormatter));
			}
			qiContent.Add(info);
		}

		void ShowSymbolInfo(Context context) {
			var symbol = context.symbol;
			var container = context.Container;
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Attributes)) {
				ShowAttributesInfo(container, symbol);
			}
			switch (symbol.Kind) {
				case SymbolKind.Event:
					ShowEventInfo(container, (IEventSymbol)symbol);
					break;
				case SymbolKind.Field:
					ShowFieldInfo(container, (IFieldSymbol)symbol);
					break;
				case SymbolKind.Local:
					var loc = (ILocalSymbol)symbol;
					if (loc.HasConstantValue) {
						ShowConstInfo(container, symbol, loc.ConstantValue);
					}
					else if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.SymbolReassignment)) {
						ShowLocalAssignmentInfo(context, loc);
					}
					break;
				case SymbolKind.Method:
					var m = (IMethodSymbol)symbol;
					if (m.MethodKind == MethodKind.AnonymousFunction) {
						return;
					}
					ShowMethodInfo(context, m);
					break;
				case SymbolKind.NamedType:
					ShowTypeInfo(context, context.node, (INamedTypeSymbol)symbol);
					break;
				case SymbolKind.Property:
					ShowPropertyInfo(container, (IPropertySymbol)symbol);
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color)
						&& context.session.Mark(nameof(ColorQuickInfoUI))) {
						container.Add(ColorQuickInfoUI.PreviewColorProperty((IPropertySymbol)symbol, _SpecialProject.MayBeVsProject));
					}
					break;
				case SymbolKind.Parameter:
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.SymbolReassignment)
						&& symbol.ContainingSymbol.HasSource()) {
						ShowParameterInfo(context, (IParameterSymbol)symbol);
					}
					break;
				case SymbolKind.Namespace:
					ShowNamespaceInfo(container, (INamespaceSymbol)symbol);
					break;
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
				&& (context.node.Parent.IsKind(SyntaxKind.Argument) == false
					|| Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter) == false)
					/*the signature has already been displayed there*/) {
				var st = symbol.GetReturnType();
				if (st?.TypeKind == TypeKind.Delegate) {
					var invoke = ((INamedTypeSymbol)st).DelegateInvokeMethod;
					container.Add(
						new GeneralInfoBlock(
							new BlockItem(IconIds.Delegate, R.T_DelegateSignature, true)
								.Append(": ")
								.AddSymbol(invoke.ReturnType, false, __SymbolFormatter)
								.Append(" ")
								.AddParameters(invoke.Parameters)
						)
					);
				}
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.SymbolLocation)) {
				ShowSymbolLocationInfo(context);
			}
		}

		static void ShowSymbolLocationInfo(Context context) {
			var symbol = context.symbol;
			var (p, f) = context.semanticModel.Compilation.GetReferencedAssemblyPath(symbol as IAssemblySymbol ?? symbol.ContainingAssembly);
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
				var proj = symbol.GetSourceReferences().Select(r => context.semanticContext.GetProject(r.SyntaxTree)).FirstOrDefault(i => i != null);
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
			context.Container.Add(item);
		}

		static BlockItem ShowReturnInfo(ReturnStatementSyntax returns, SemanticModel semanticModel, CancellationToken cancellationToken) {
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
				var t = new BlockItem(IconIds.ReturnValue);
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
				qiContent.Add(new GeneralInfoBlock(p));
			}

			BlockItem ListAttributes(BlockItem paragraph, ImmutableArray<AttributeData> attributes, byte attrType) {
				if (attributes.Length > 0) {
					foreach (var item in attributes) {
						if (item.AttributeClass.IsAccessible(true)) {
							if (paragraph == null) {
								paragraph = new BlockItem(IconIds.Attribute, R.T_Attribute, true);
							}
							paragraph.AppendLine().Append(new AttributeDataSegment(item, attrType));
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
				&& (property.DeclaredAccessibility != Accessibility.Public
					|| property.IsAbstract
					|| property.IsStatic
					|| property.IsOverride
					|| property.IsVirtual)) {
				ShowDeclarationModifier(qiContent, property);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfaceImplementations)) {
				ShowInterfaceImplementation(qiContent, property, property.ExplicitInterfaceImplementations);
			}
		}

		static void ShowEventInfo(InfoContainer qiContent, IEventSymbol ev) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle) == false
					&& (ev.DeclaredAccessibility != Accessibility.Public
						|| ev.IsAbstract
						|| ev.IsStatic
						|| ev.IsOverride
						|| ev.IsVirtual)
					&& ev.ContainingType?.TypeKind != TypeKind.Interface) {
					ShowDeclarationModifier(qiContent, ev);
				}
				if (ev.Type.GetMembers("Invoke").FirstOrDefault() is IMethodSymbol invoke
					&& invoke.Parameters.Length == 2) {
					qiContent.Add(
						new GeneralInfoBlock(
							new BlockItem(IconIds.Event, R.T_EventSignature, true)
								.AddParameters(invoke.Parameters)
						)
					);
				}
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfaceImplementations)) {
				ShowInterfaceImplementation(qiContent, ev, ev.ExplicitInterfaceImplementations);
			}
		}

		void ShowFieldInfo(InfoContainer qiContent, IFieldSymbol field) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration)
				&& Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle) == false
				&& (field.DeclaredAccessibility != Accessibility.Public
					|| field.IsReadOnly
					|| field.IsVolatile
					|| field.IsStatic)
				&& field.ContainingType.TypeKind != TypeKind.Enum) {
				ShowDeclarationModifier(qiContent, field);
			}
			if (field.HasConstantValue) {
				if (field.ConstantValue is int fc
					&& field.DeclaredAccessibility == Accessibility.Public
					&& _SpecialProject.MayBeVsProject) {
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
					qc.Add(new GeneralInfoBlock(fieldValue, f.Name));
				}
			}
		}

		static void ShowMethodInfo(Context context, IMethodSymbol method) {
			var container = context.Container;
			var options = Config.Instance.QuickInfoOptions;
			var node = context.node;
			if (options.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation)) {
				ShowAnonymousTypeInfo(container, method);
			}
			if (options.MatchFlags(QuickInfoOptions.Declaration)
				&& Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle) == false
				&& method.ContainingType?.TypeKind != TypeKind.Interface
				&& (method.DeclaredAccessibility != Accessibility.Public
					|| method.IsAbstract
					|| method.IsStatic
					|| method.IsVirtual
					|| method.IsOverride
					|| method.IsExtern
					|| method.IsSealed)) {
				ShowDeclarationModifier(container, method);
			}
			if (options.MatchFlags(QuickInfoOptions.TypeParameters)
				&& options.MatchFlags(QuickInfoOptions.AlternativeStyle) == false
				&& method.IsGenericMethod
				&& method.TypeArguments.Length > 0
				&& method.TypeParameters[0].OriginallyEquals(method.TypeArguments[0])) {
				ShowTypeArguments(container, method.TypeArguments, method.TypeParameters);
			}
			if (options.MatchFlags(QuickInfoOptions.InterfaceImplementations)) {
				ShowInterfaceImplementation(container, method, method.ExplicitInterfaceImplementations);
			}
			if (options.MatchFlags(QuickInfoOptions.SymbolLocation)
				&& method.IsExtensionMethod
				&& options.MatchFlags(QuickInfoOptions.AlternativeStyle) == false) {
				ShowExtensionMethod(container, method);
			}
			if (options.MatchFlags(QuickInfoOptions.MethodOverload) && context.IsCandidate == false) {
				ShowOverloadsInfo(context, method);
			}
			if (node.Parent.IsKind(SyntaxKind.Attribute)
				|| node.Parent.Parent.IsKind(SyntaxKind.Attribute) // qualified attribute annotation
				) {
				if (options.MatchFlags(QuickInfoOptions.Attributes)) {
					ShowAttributesInfo(container, method.ContainingType);
				}
				ShowTypeInfo(context, node.Parent, method.ContainingType);
			}
			if (options.MatchFlags(QuickInfoOptions.Color)
				&& method.ContainingType?.Name == "Color"
				&& context.session.Mark(nameof(ColorQuickInfoUI))) {
				container.Add(ColorQuickInfoUI.PreviewColorMethodInvocation(context.semanticModel, node, method));
			}
			if (method.MethodKind == MethodKind.BuiltinOperator && node is ExpressionSyntax) {
				var value = context.semanticModel.GetConstantValue(node, context.cancellationToken);
				if (value.HasValue) {
					ShowConstInfo(container, null, value.Value);
				}
			}
		}

		static void ShowTypeArguments(InfoContainer qiContent, ImmutableArray<ITypeSymbol> args, ImmutableArray<ITypeParameterSymbol> typeParams) {
			var info = new GeneralInfoBlock(IconIds.GenericDefinition, R.T_TypeArgument);
			var l = args.Length;
			for (int i = 0; i < l; i++) {
				info.Add(new BlockItem().AddTypeParameterInfo(typeParams[i], args[i]));
			}
			qiContent.Add(info);
		}

		static void ShowNamespaceInfo(InfoContainer qiContent, INamespaceSymbol nsSymbol) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NamespaceTypes) == false) {
				return;
			}
			var namespaces = nsSymbol.GetNamespaceMembers()
				.ToImmutableArray()
				.Sort(Comparer<INamespaceSymbol>.Create((x, y) => String.CompareOrdinal(x.Name, y.Name)));
			if (namespaces.Length > 0) {
				var info = new GeneralInfoBlock(IconIds.Namespace, R.T_Namespace);
				foreach (var ns in namespaces) {
					info.Add(
						new BlockItem(IconIds.Namespace)
							.Append(ns.Name, __SymbolFormatter.Namespace)
					);
				}
				qiContent.Add(info);
			}

			var members = nsSymbol.GetTypeMembers().Sort(Comparer<INamedTypeSymbol>.Create((x, y) => String.Compare(x.Name, y.Name)));
			if (members.Length > 0) {
				var info = new GeneralInfoBlock(IconIds.Namespace, R.T_Type);
				foreach (var type in members) {
					info.Add(
						new BlockItem(type.GetImageId())
							.Append(new SymbolDeclarationSegment(type, true, true))
							.AddSymbol(type, false, __SymbolFormatter)
					);
				}
				qiContent.Add(info);
			}
		}

		static void ShowTypeInfo(Context context, SyntaxNode node, INamedTypeSymbol typeSymbol) {
			var qiContent = context.Container;
			var options = Config.Instance.QuickInfoOptions;
			if (options.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation) && typeSymbol.TypeKind == TypeKind.Class) {
				ShowAnonymousTypeInfo(qiContent, typeSymbol);
			}
			if (options.MatchFlags(QuickInfoOptions.TypeParameters)
				&& options.MatchFlags(QuickInfoOptions.AlternativeStyle) == false
				&& typeSymbol.IsGenericType
				&& typeSymbol.TypeArguments.Length > 0
				&& typeSymbol.TypeParameters[0].OriginallyEquals(typeSymbol.TypeArguments[0])) {
				ShowTypeArguments(qiContent, typeSymbol.TypeArguments, typeSymbol.TypeParameters);
			}
			if (typeSymbol.IsAnyKind(TypeKind.Class, TypeKind.Struct)
				&& options.MatchFlags(QuickInfoOptions.MethodOverload)) {
				if (node.IsAnyKind(SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration)
					&& ((TypeDeclarationSyntax)node).GetParameterList() != null) {
					// show overloads for primary constructors
					var ctors = typeSymbol.GetMembers(".ctor");
					if (ctors.Length > 1) {
						ShowOverloadsInfo(qiContent, ctors[0] as IMethodSymbol, ctors);
					}
				}
				else {
					node = node.GetObjectCreationNode();
					var semanticModel = context.semanticModel;
					if (node != null
						&& semanticModel.GetSymbolOrFirstCandidate(node, context.cancellationToken) is IMethodSymbol method
						&& context.IsCandidate == false) {
						ShowOverloadsInfo(context, method);
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
			if (typeSymbol.TypeKind == TypeKind.Enum
				&& options.HasAnyFlag(QuickInfoOptions.BaseType | QuickInfoOptions.Enum)) {
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
					? context.semanticModel.GetDeclaredSymbol(declarationType, context.cancellationToken) as INamedTypeSymbol
					: null;
				ShowInterfaceMembers(qiContent, typeSymbol, declaredClass);
			}
		}

		static void ShowConstInfo(InfoContainer qiContent, ISymbol symbol, object value) {
			if (value is string text) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.String)) {
					qiContent.Add(new StringInfoBlock(text, true));
				}
			}
			else if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues)) {
				if (value != null
					&& Type.GetTypeCode(value.GetType()).IsBetween(TypeCode.Char, TypeCode.Double)) {
					if (symbol?.ContainingType?.TypeKind == TypeKind.Enum) {
						ShowEnumQuickInfo(qiContent, symbol.ContainingType, Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseType), false);
					}
					qiContent.Add(new NumericInfoBlock(value, false));
				}
			}
		}

		static void ShowLocalAssignmentInfo(Context ctx, ILocalSymbol loc) {
			var locSpan = loc.DeclaringSyntaxReferences[0].Span;
			var node = ctx.CompilationUnit.FindNode(locSpan);
			if (IsVariableAssignedAfterDeclaration(loc, node, ctx.semanticModel)) {
				ctx.Container.Add(new BlockItem(IconIds.WrittenVariables, R.T_Reassigned));
			}
			else {
				ctx.Container.Add(new BlockItem(IconIds.ReadonlyVariable, R.T_NoReassignment));
			}
		}

		void ShowParameterInfo(Context ctx, IParameterSymbol parameter) {
			var reassigned = IsParameterAssignedAfterDeclaration(ctx, parameter);
			if (reassigned == true) {
				ctx.Container.Add(new BlockItem(IconIds.WrittenVariables, R.T_Reassigned.Replace("<S>", parameter.Name)));
			}
			else if (reassigned == false) {
				ctx.Container.Add(new BlockItem(IconIds.ReadonlyParameter, R.T_NoReassignment.Replace("<S>", parameter.Name)));
			}
		}

		static void ShowExtensionMethod(InfoContainer qiContent, IMethodSymbol method) {
			var info = new GeneralInfoBlock();
			info.Add(
				new BlockItem(IconIds.ExtensionMethod, R.T_ExtendedBy, true)
					.AddSymbolDisplayParts(method.ContainingType.ToDisplayParts(), __SymbolFormatter)
			);
			var extType = method.MethodKind == MethodKind.ReducedExtension
				? method.ReceiverType
				: method.GetParameters()[0].Type;
			if (extType != null) {
				info.Add(
					new BlockItem(extType.GetImageId(), R.T_Extending, true)
						.AddSymbol(extType, true, __SymbolFormatter)
				);
			}
			qiContent.Add(info);
		}

		static void ShowBaseType(InfoContainer qiContent, ITypeSymbol typeSymbol) {
			var baseType = typeSymbol.BaseType;
			if (baseType == null || baseType.IsCommonBaseType()) {
				return;
			}
			var classList = new BlockItem(IconIds.BaseTypes, R.T_BaseType, true)
				.AddSymbol(baseType, null, __SymbolFormatter);
			var info = new GeneralInfoBlock(classList);
			while ((baseType = baseType.BaseType) != null) {
				if (baseType.IsCommonBaseType() == false) {
					classList.Append(" - ").AddSymbol(baseType, null, __SymbolFormatter);
				}
			}
			qiContent.Add(info);
		}

		static void ShowDeclarationModifier(InfoContainer qiContent, ISymbol symbol) {
			var info = new GeneralInfoBlock();
			info.Add(
				new BlockItem(IconIds.DeclarationModifier)
					.Append(new SymbolDeclarationSegment(symbol, true, false))
			);
			qiContent.Add(info);
		}

		static TextBlock ToUIText(ISymbol symbol) {
			return new ThemedTipText().AddSymbolDisplayParts(symbol.ToDisplayParts(CodeAnalysisHelper.QuickInfoSymbolDisplayFormat), __SymbolFormatter, -1);
		}

		sealed class SpecialProjectInfo
		{
			static readonly string[] __PlatformUINamespace = new[] { "Microsoft", "VisualStudio", "PlatformUI" },
				__ShellNamespace = new[] { "Microsoft", "VisualStudio", "Shell" };
			readonly SemanticModel _Model;
			int _MayBeVsProject;

			public bool MayBeVsProject => _MayBeVsProject != 0
				? _MayBeVsProject == 1
				: (_MayBeVsProject = _Model.GetNamespaceSymbol(__PlatformUINamespace) != null || _Model.GetTypeSymbol(nameof(VsColors), __ShellNamespace) != null ? 1 : -1) == 1;

			public SpecialProjectInfo(SemanticModel model) {
				_Model = model;
			}
		}
	}
}
