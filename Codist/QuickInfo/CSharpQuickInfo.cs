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
			return buffer == null ? Task.FromResult<QuickInfoItem>(null)
				: InternalGetQuickInfoItemAsync(session, buffer, triggerPoint, cancellationToken);
		}

		sealed class Context
		{
			public readonly IAsyncQuickInfoSession session;
			public readonly ITextBuffer TextBuffer;
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

			public Context(IAsyncQuickInfoSession session, ITextBuffer textBuffer, SemanticModel semanticModel, SnapshotPoint triggerPoint, CancellationToken cancellationToken) {
				this.session = session;
				TextBuffer = textBuffer;
				this.semanticModel = semanticModel;
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
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			if (QuickInfoOverride.CheckCtrlSuppression()
				|| session.TextView is Microsoft.VisualStudio.Text.Editor.IWpfTextView v == false) {
				return null;
			}
			var ctx = SemanticContext.GetOrCreateSingletonInstance(v);
			await ctx.UpdateAsync(textBuffer, triggerPoint, cancellationToken);
			var semanticModel = ctx.SemanticModel;
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
			var context = new Context(session, textBuffer, semanticModel, triggerPoint, cancellationToken);
			if (context.token.Span.Contains(triggerPoint.Position) == false) {
				// skip when trigger point is on trivia
				return null;
			}
			#region Classify token
			do {
				context.State = State.Undefined;
				if (__TokenProcessors.TryGetValue((SyntaxKind)context.token.RawKind, out syntaxProcessor)) {
					syntaxProcessor(context);
				}
				else {
					ProcessToken(context);
				}
			} while (context.State >= State.ReparseToken);
			if (context.keepBuiltInXmlDoc && o != null) {
				o.OverrideBuiltInXmlDoc = false;
			}
			switch (context.State) {
				case State.Process: goto PROCESS;
				case State.PredefinedSymbol: context.UseTokenNode(); goto PROCESS;
				case State.Return: goto RETURN;
				case State.Unavailable: return null;
				case State.DirectReturn: return context.Result;
			}
			#endregion

			if (ResolveNode(context) == false) {
				return null;
			}

		PROCESS:
			if (context.node == null) {
				return null;
			}

			if (context.symbol == null) {
				ResolveSymbol(context);
			}
			if (__NodeProcessors.TryGetValue((SyntaxKind)context.node.RawKind, out syntaxProcessor)) {
				syntaxProcessor(context);
			}
			if (context.symbol == null) {
				goto RETURN;
			}
			Chain<string> unavailableProjects = null;
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation)) {
				if (context.isConvertedType == false) {
					unavailableProjects = await SearchUnavailableProjectsAsync(ctx.Document, context).ConfigureAwait(false);
				}
				ctor = context.node.Parent.UnqualifyExceptNamespace() as ObjectCreationExpressionSyntax;
				await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
				OverrideDocumentation(context.node,
					o,
					ctor != null
						? semanticModel.GetSymbolInfo(ctor, cancellationToken).Symbol ?? context.symbol
						: context.node.Parent.IsKind(CodeAnalysisHelper.PrimaryConstructorBaseType)
						? (context.symbol = semanticModel.GetSymbolInfo(context.node.Parent, cancellationToken).Symbol ?? context.symbol)
						: context.symbol,
					semanticModel,
					cancellationToken);
				if (context.symbol?.Kind == SymbolKind.RangeVariable) {
					context.Container.Add(new ThemedTipText("Range Variable: ").SetGlyph(IconIds.LocalVariable).Append(context.symbol.Name, true));
					semanticModel.GetTypeInfo(context.node, cancellationToken).Type.SetNotDefault(ref context.symbol);
				}
			}
			if (context.isConvertedType == false) {
				await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
				ShowSymbolInfo(context);
				if (unavailableProjects != null) {
					ShowUnavailableProjects(context, unavailableProjects);
				}
			}
		RETURN:
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter)) {
				ShowArgumentInfo(context);
			}
			if (ctor == null) {
				ctor = context.node.Parent.UnqualifyExceptNamespace() as ObjectCreationExpressionSyntax;
			}
			if (ctor != null) {
				semanticModel.GetSymbolOrFirstCandidate(ctor, cancellationToken).SetNotDefault(ref context.symbol);
				if (context.symbol == null) {
					return null;
				}
				if (context.symbol.IsImplicitlyDeclared) {
					context.symbol = context.symbol.ContainingType;
				}
			}
			o?.ApplyClickAndGo(context.symbol);
			if (context.Container.ItemCount == 0 && context.isConvertedType == false) {
				if (context.symbol != null) {
					// place holder
					context.Container.Add(new ContentPresenter() { Name = "SymbolPlaceHolder" });
					return CreateQuickInfoItem(session, context.token, context.Container);
				}
				return null;
			}
			return CreateQuickInfoItem(session, context.token, context.Container);
		}

		static bool ResolveNode(Context context) {
			ref var node = ref context.node;
			node = context.CompilationUnit.FindNode(context.token.Span, true, true);
			if (node == null
				|| context.skipTriggerPointCheck == false && node.Span.Contains(context.TriggerPoint.Position, true) == false) {
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

		static QuickInfoItem CreateQuickInfoItem(IAsyncQuickInfoSession session, SyntaxToken? token, object item) {
			session.KeepViewPosition();
			return new QuickInfoItem(token?.Span.CreateSnapshotSpan(session.TextView.TextSnapshot).ToTrackingSpan(), item);
		}

		static Task<Chain<string>> SearchUnavailableProjectsAsync(Document doc, Context ctx) {
			var solution = doc.Project.Solution;
			ImmutableArray<DocumentId> linkedDocuments;
			string docId;
			return solution.ProjectIds.Count < 2
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
			var doc = new ThemedTipDocument().AppendTitle(IconIds.UnavailableSymbol, R.T_SymbolUnavailableIn);
			foreach (var item in unavailableProjects) {
				doc.Append(new ThemedTipParagraph(IconIds.Project, new ThemedTipText(item)));
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
			var info = new ThemedTipDocument().AppendTitle(IconIds.SymbolCandidate, R.T_Maybe);
			foreach (var item in candidates) {
				info.Append(new ThemedTipParagraph(item.GetImageId(), ToUIText(item.OriginalDefinition)));
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
						&& symbol.HasSource()) {
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
					container.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(IconIds.Delegate,
						new ThemedTipText(R.T_DelegateSignature, true).Append(": ")
							.AddSymbol(invoke.ReturnType, false, __SymbolFormatter)
							.Append(" ")
							.AddParameters(invoke.Parameters, __SymbolFormatter)
						)));
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
			context.Container.Add(item);
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
				t.SetGlyph(IconIds.ReturnValue);
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
					qc.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(fieldValue, new ThemedTipText(f.Name))));
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
				&& (method.DeclaredAccessibility != Accessibility.Public || method.IsAbstract || method.IsStatic || method.IsVirtual || method.IsOverride || method.IsExtern || method.IsSealed)) {
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
					var t = new ThemedTipText().SetGlyph(type.GetImageId());
					__SymbolFormatter.ShowSymbolDeclaration(type, t, true, true);
					t.AddSymbol(type, false, __SymbolFormatter);
					info.Add(t);
				}
				qiContent.Add(info.Scrollable());
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
					? context.semanticModel.GetDeclaredSymbol(declarationType, context.cancellationToken) as INamedTypeSymbol
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

		static void ShowLocalAssignmentInfo(Context ctx, ILocalSymbol loc) {
			var locSpan = loc.DeclaringSyntaxReferences[0].Span;
			var node = ctx.CompilationUnit.FindNode(locSpan);
			if (IsVariableAssignedAfterDeclaration(loc, node, ctx.semanticModel)) {
				ctx.Container.Add(new ThemedTipText(R.T_Reassigned).SetGlyph(IconIds.WrittenVariables));
			}
			else {
				ctx.Container.Add(new ThemedTipText(R.T_NoReassignment).SetGlyph(IconIds.ReadonlyVariable));
			}
		}

		void ShowParameterInfo(Context context, IParameterSymbol parameter) {
			var declaration = context.CompilationUnit.FindNode(parameter.ContainingSymbol.DeclaringSyntaxReferences[0].Span, false, true);
			if (declaration == null) {
				return;
			}
			DataFlowAnalysis analysis;
			BlockSyntax body = null;
			SyntaxNode expression = null;
			if (declaration is BaseMethodDeclarationSyntax m) {
				body = m.Body;
				expression = m.ExpressionBody;
			}
			else if (declaration is AccessorDeclarationSyntax a) {
				body = a.Body;
				expression = a.ExpressionBody;
			}
			else if (declaration is AnonymousFunctionExpressionSyntax af) {
				expression = af.Body;
			}
			else if (declaration is LocalFunctionStatementSyntax lf) {
				body = lf.Body;
				expression = lf.ExpressionBody;
			}

			analysis = body != null ? context.semanticModel.AnalyzeDataFlow(body)
				: expression != null ? context.semanticModel.AnalyzeDataFlow(expression is ArrowExpressionClauseSyntax ae ? ae.Expression : expression)
				: null;
			if (analysis?.WrittenInside.Contains(parameter) == true) {
				context.Container.Add(new ThemedTipText(R.T_Reassigned.Replace("<S>", parameter.Name)).SetGlyph(IconIds.WrittenVariables));
			}
			else {
				context.Container.Add(new ThemedTipText(R.T_NoReassignment.Replace("<S>", parameter.Name)).SetGlyph(IconIds.ReadonlyParameter));
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
