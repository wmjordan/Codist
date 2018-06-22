using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using AppHelpers;

namespace Codist.Classifiers
{
	/// <summary>A classifier for C# code syntax highlight.</summary>
	sealed class CSharpClassifier : IClassifier
	{
		static CSharpClassifications _Classifications;
		static GeneralClassifications _GeneralClassifications;

		readonly ITextBuffer _TextBuffer;
		readonly IClassificationTypeRegistryService _TypeRegistryService;
		readonly ITextDocumentFactoryService _TextDocumentFactoryService;

		SemanticModel _SemanticModel;

		/// <summary>
		/// Initializes a new instance of the <see cref="CSharpClassifier"/> class.
		/// </summary>
		internal CSharpClassifier(
			IClassificationTypeRegistryService registry,
			ITextDocumentFactoryService textDocumentFactoryService,
			ITextBuffer buffer) {
			if (_Classifications == null) {
				_Classifications = new CSharpClassifications(registry);
			}
			if (_GeneralClassifications == null) {
				_GeneralClassifications = new GeneralClassifications(registry);
			}
			_TextDocumentFactoryService = textDocumentFactoryService;
			_TextBuffer = buffer;
			_TypeRegistryService = registry;
			_TextBuffer.Changed += OnTextBufferChanged;
			_TextDocumentFactoryService.TextDocumentDisposed += OnTextDocumentDisposed;
		}

		/// <summary>
		/// An event that occurs when the classification of a span of text has changed.
		/// </summary>
		/// <remarks>
		/// This event gets raised if a non-text change would affect the classification in some way,
		/// for example typing /* would cause the classification to change in C# without directly
		/// affecting the span.
		/// </remarks>
		public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

		/// <summary>
		/// Gets all the <see cref="ClassificationSpan"/> objects that intersect with the given range
		/// of text.
		/// </summary>
		/// <remarks>
		/// This method scans the given SnapshotSpan for potential matches for this classification.
		/// In this instance, it classifies everything and returns each span as a new ClassificationSpan.
		/// </remarks>
		/// <param name="span">The span currently being classified.</param>
		/// <returns>
		/// A list of ClassificationSpans that represent spans identified to be of this classification.
		/// </returns>
		public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
			var snapshot = span.Snapshot;
			var workspace = snapshot.TextBuffer.GetWorkspace();
			if (workspace == null) {
				return Array.Empty<ClassificationSpan>();
			}
			var result = new List<ClassificationSpan>(16);
			var semanticModel = _SemanticModel ?? (_SemanticModel = workspace.GetDocument(span).GetSemanticModelAsync().Result);

			var textSpan = new TextSpan(span.Start.Position, span.Length);
			var unitCompilation = semanticModel.SyntaxTree.GetCompilationUnitRoot();
			var classifiedSpans = Classifier.GetClassifiedSpans(semanticModel, textSpan, workspace);
			var lastTriviaSpan = default(TextSpan);
			var spanNode = unitCompilation.FindNode(textSpan);
			switch (spanNode.Kind()) {
				case SyntaxKind.AttributeList:
				case SyntaxKind.AttributeArgumentList:
					result.Add(CreateClassificationSpan(snapshot, textSpan, _Classifications.AttributeNotation));
					break;
			}
			foreach (var item in classifiedSpans) {
				var ct = item.ClassificationType;
				switch (ct) {
					case "keyword": {
							var node = unitCompilation.FindNode(item.TextSpan, true, true);
							if (node is MemberDeclarationSyntax) {
								var token = unitCompilation.FindToken(item.TextSpan.Start);
								if (token != null) {
									switch (token.Kind()) {
									case SyntaxKind.SealedKeyword:
									case SyntaxKind.OverrideKeyword:
									case SyntaxKind.AbstractKeyword:
									case SyntaxKind.VirtualKeyword:
									case SyntaxKind.NewKeyword:
										result.Add(CreateClassificationSpan(snapshot, item.TextSpan, _Classifications.AbstractionKeyword));
										continue;
									}
								}
								continue;
							}
							const SyntaxKind ThrowExpression = (SyntaxKind)9052;
							switch (node.Kind()) {
								case SyntaxKind.BreakStatement:
									if (node.Parent is SwitchSectionSyntax == false) {
										goto case SyntaxKind.ReturnStatement;
									}
									continue;
								// highlights: return, yield return, yield break, throw and continue
								case SyntaxKind.ReturnKeyword:
								case SyntaxKind.GotoCaseStatement:
								case SyntaxKind.GotoDefaultStatement:
								case SyntaxKind.GotoStatement:
								case SyntaxKind.ContinueStatement:
								case SyntaxKind.ReturnStatement:
								case SyntaxKind.YieldReturnStatement:
								case SyntaxKind.YieldBreakStatement:
								case SyntaxKind.ThrowStatement:
								case ThrowExpression:
									result.Add(CreateClassificationSpan(snapshot, item.TextSpan, _Classifications.ControlFlowKeyword));
									continue;
							}
						}
						continue;
					case Constants.XmlDocCData:
						if (Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.XmlDocCode)) {
							ct = HighlightXmlDocCData(span, item, workspace, result, ct);
						}
						continue;
					case Constants.CodePunctuation:
						if (item.TextSpan.Length == 1) {
							HighlightPunctuation(item, snapshot, result, semanticModel, unitCompilation);
						}
						continue;
					default:
						if (ct == Constants.XmlDocDelimiter) {
							if (lastTriviaSpan.Contains(item.TextSpan)) {
								continue;
							}
							var node = unitCompilation.FindTrivia(item.TextSpan.Start);
							if (node != null) {
								switch (node.Kind()) {
									case SyntaxKind.SingleLineDocumentationCommentTrivia:
									case SyntaxKind.MultiLineDocumentationCommentTrivia:
									case SyntaxKind.DocumentationCommentExteriorTrivia:
										lastTriviaSpan = node.FullSpan;
										result.Add(CreateClassificationSpan(snapshot, lastTriviaSpan, _Classifications.XmlDoc));
										continue;
								}
							}
						}
						else if (ct == Constants.CodeIdentifier
							|| ct.EndsWith("name", StringComparison.Ordinal))
						{
							var itemSpan = item.TextSpan;
							var node = unitCompilation.FindNode(itemSpan, true);
							foreach (var type in GetClassificationType(node, semanticModel)) {
								result.Add(CreateClassificationSpan(snapshot, itemSpan, type));
							}
						}
						break;
				}
			}
			return result;
		}

		static void HighlightPunctuation(ClassifiedSpan item, ITextSnapshot snapshot, List<ClassificationSpan> result, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation) {
			var s = snapshot.GetText(item.TextSpan.Start, item.TextSpan.Length)[0];
			if (s == '{' || s == '}') {
				var node = unitCompilation.FindNode(item.TextSpan, true, true);
				if (node is BaseTypeDeclarationSyntax == false
					&& node is ExpressionSyntax == false
					&& node is NamespaceDeclarationSyntax == false
					&& (node = node.Parent) == null) {
					return;
				}
				var type = ClassifySyntaxNode(node);
				if (type != null) {
					if (node is ExpressionSyntax == false) {
						result.Add(CreateClassificationSpan(snapshot, item.TextSpan, _Classifications.DeclarationBrace));
					}
					if (Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.DeclarationBrace)) {
						result.Add(CreateClassificationSpan(snapshot, item.TextSpan, type));
					}
				}
			}
			else if ((s == '(' || s == ')')
					&& Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.ParameterBrace)) {
				var node = unitCompilation.FindNode(item.TextSpan, true, true);
				if (node.Kind() == SyntaxKind.CastExpression) {
					var symbol = semanticModel.GetSymbolInfo((node as CastExpressionSyntax).Type).Symbol;
					if (symbol == null) {
						return;
					}
					IClassificationType type = null;
					switch (symbol.Kind) {
						case SymbolKind.NamedType:
							switch((symbol as INamedTypeSymbol).TypeKind) {
								case TypeKind.Class: type = _Classifications.ClassName; break;
								case TypeKind.Interface: type = _Classifications.InterfaceName; break;
								case TypeKind.Struct: type = _Classifications.StructName; break;
								case TypeKind.Delegate: type = _Classifications.DelegateName; break;
								case TypeKind.Enum: type = _Classifications.EnumName; break;
							}
							break;
						case SymbolKind.TypeParameter:
							type = _Classifications.TypeParameter; break;
					}
					if (type != null) {
						result.Add(CreateClassificationSpan(snapshot, item.TextSpan, type));
					}
				}
				node = (node as BaseArgumentListSyntax
					?? node as BaseParameterListSyntax
					?? (CSharpSyntaxNode)(node as CastExpressionSyntax)
					)?.Parent;
				if (node != null) {
					var type = ClassifySyntaxNode(node);
					if (type != null) {
						result.Add(CreateClassificationSpan(snapshot, item.TextSpan, type));
					}
				}
			}
			//else if (s == '[') {
			//	// highlight attribute annotation
			//	var node = unitCompilation.FindNode(item.TextSpan, true, true);
			//	if (node is AttributeListSyntax) {
			//		result.Add(CreateClassificationSpan(snapshot, node.Span, _Classifications.AttributeNotation));
			//	}
			//}
		}

		static IClassificationType ClassifySyntaxNode(SyntaxNode node) {
			IClassificationType type = null;
			switch (node.Kind()) {
				case SyntaxKind.MethodDeclaration:
				case SyntaxKind.AnonymousMethodExpression:
				case SyntaxKind.SimpleLambdaExpression:
				case SyntaxKind.ParenthesizedLambdaExpression:
				case SyntaxKind.InvocationExpression:
					type = _Classifications.Method;
					break;
				case SyntaxKind.ConstructorDeclaration:
				case SyntaxKind.AnonymousObjectCreationExpression:
				case SyntaxKind.ObjectInitializerExpression:
				case SyntaxKind.ObjectCreationExpression:
				case SyntaxKind.CollectionInitializerExpression:
				case SyntaxKind.ArrayInitializerExpression:
				case SyntaxKind.ThisConstructorInitializer:
					type = _Classifications.ConstructorMethod;
					break;
				case SyntaxKind.PropertyDeclaration: type = _Classifications.Property; break;
				case SyntaxKind.ClassDeclaration: type = _Classifications.ClassName; break;
				case SyntaxKind.InterfaceDeclaration: type = _Classifications.InterfaceName; break;
				case SyntaxKind.EnumDeclaration: type = _Classifications.EnumName; break;
				case SyntaxKind.StructDeclaration: type = _Classifications.StructName; break;
				case SyntaxKind.Attribute: type = _Classifications.AttributeName; break;
				case SyntaxKind.NamespaceDeclaration:
					type = _Classifications.Namespace;
					break;
					//case SyntaxKind.InterpolatedStringExpression: type = _Classifications.ConstField; break;
			}

			return type;
		}

		string HighlightXmlDocCData(SnapshotSpan span, ClassifiedSpan item, Workspace workspace, List<ClassificationSpan> result, string ct) {
			var snapshot = span.Snapshot;
			var start = item.TextSpan.Start;
			SyntaxNode root;
			var sourceText = snapshot.AsText();
			var docId = DocumentId.CreateNewId(workspace.GetDocumentIdInCurrentContext(sourceText.Container).ProjectId);
			var document = workspace.CurrentSolution
				.AddDocument(docId, "xmlDocCData.cs", snapshot.GetText(item.TextSpan.Start, item.TextSpan.Length))
				.WithDocumentSourceCodeKind(docId, SourceCodeKind.Script)
				.GetDocument(docId);
			var model = document.GetSemanticModelAsync().Result;
			var compilation = model.SyntaxTree.GetCompilationUnitRoot();
			if (document
				.GetSyntaxTreeAsync().Result
				.TryGetRoot(out root)) {
				foreach (var spanItem in Classifier.GetClassifiedSpans(model, new TextSpan(0, item.TextSpan.Length), workspace)) {
					ct = spanItem.ClassificationType;
					if (ct == Constants.CodeIdentifier
						|| ct == Constants.CodeClassName
						|| ct == Constants.CodeStructName
						|| ct == Constants.CodeInterfaceName
						|| ct == Constants.CodeEnumName
						|| ct == Constants.CodeTypeParameterName
						|| ct == Constants.CodeDelegateName) {

						var node = compilation.FindNode(spanItem.TextSpan, true);

						foreach (var type in GetClassificationType(node, model)) {
							result.Add(CreateClassificationSpan(snapshot, new TextSpan(start + spanItem.TextSpan.Start, spanItem.TextSpan.Length), type));
						}
					}
					else {
						result.Add(CreateClassificationSpan(snapshot, new TextSpan(start + spanItem.TextSpan.Start, spanItem.TextSpan.Length), _TypeRegistryService.GetClassificationType(ct)));
					}
				}
			}

			return ct;
		}

		static IEnumerable<IClassificationType> GetClassificationType(SyntaxNode node, SemanticModel semanticModel) {
			node = node.Kind() == SyntaxKind.Argument ? (node as ArgumentSyntax).Expression : node;
			//System.Diagnostics.Debug.WriteLine(node.GetType().Name + node.Span.ToString());
			var symbol = semanticModel.GetSymbolInfo(node).Symbol;
			if (symbol == null) {
				symbol = semanticModel.GetDeclaredSymbol(node);
				if (symbol != null) {
					switch (symbol.Kind) {
						case SymbolKind.NamedType:
						case SymbolKind.Event:
							yield return symbol.ContainingType != null ? _Classifications.NestedDeclaration : _Classifications.Declaration;
							break;
						case SymbolKind.Method:
							yield return _Classifications.Declaration;
							break;
						case SymbolKind.Property:
							if (symbol.ContainingType.IsAnonymousType == false) {
								yield return _Classifications.Declaration;
							}
							break;
					}
				}
				else {
					// NOTE: handle alias in using directive
					if ((node.Parent as NameEqualsSyntax)?.Parent is UsingDirectiveSyntax) {
						yield return _Classifications.AliasNamespace;
					}
					else if (node is AttributeArgumentSyntax) {
						symbol = semanticModel.GetSymbolInfo((node as AttributeArgumentSyntax).Expression).Symbol;
						if (symbol != null && symbol.Kind == SymbolKind.Field && (symbol as IFieldSymbol)?.IsConst == true) {
							yield return _Classifications.ConstField;
							yield return _Classifications.StaticMember;
						}
					}
					symbol = node.Parent is MemberAccessExpressionSyntax
						? semanticModel.GetSymbolInfo(node.Parent).CandidateSymbols.FirstOrDefault()
						: node.Parent is ArgumentSyntax
						? semanticModel.GetSymbolInfo((node.Parent as ArgumentSyntax).Expression).CandidateSymbols.FirstOrDefault()
						: null;
					if (symbol == null) {
						yield break;
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
					yield break;

				case SymbolKind.Label:
					yield return _Classifications.Label;
					yield break;

				case SymbolKind.TypeParameter:
					yield return _Classifications.TypeParameter;
					yield break;

				case SymbolKind.Field:
					var fieldSymbol = (symbol as IFieldSymbol);
					yield return fieldSymbol.IsConst ? _Classifications.ConstField : fieldSymbol.IsReadOnly ? _Classifications.ReadonlyField : _Classifications.Field;
					break;

				case SymbolKind.Property:
					yield return _Classifications.Property;
					break;

				case SymbolKind.Event:
					yield return _Classifications.Event;
					break;

				case SymbolKind.Local:
					var localSymbol = (symbol as ILocalSymbol);
					yield return localSymbol.IsConst ? _Classifications.ConstField : _Classifications.LocalField;
					break;

				case SymbolKind.Namespace:
					yield return _Classifications.Namespace;
					yield break;

				case SymbolKind.Parameter:
					yield return _Classifications.Parameter;
					break;

				case SymbolKind.Method:
					var methodSymbol = symbol as IMethodSymbol;
					switch (methodSymbol.MethodKind) {
						case MethodKind.Constructor:
							yield return
								node is AttributeSyntax || node.Parent is AttributeSyntax || node.Parent?.Parent is AttributeSyntax
									? _Classifications.AttributeName
									: _Classifications.ConstructorMethod;
							break;
						case MethodKind.Destructor:
						case MethodKind.StaticConstructor:
							yield return _Classifications.ConstructorMethod;
							break;
						default:
							yield return methodSymbol.IsExtensionMethod ? _Classifications.ExtensionMethod : methodSymbol.IsExtern ? _Classifications.ExternMethod : _Classifications.Method;
							break;
					}
					break;

				case SymbolKind.NamedType:
					break;

				default:
					yield break;
			}

			if (symbol.IsStatic) {
				if (symbol.Kind != SymbolKind.Namespace) {
					yield return _Classifications.StaticMember;
				}
			}
			else if (symbol.IsSealed) {
				if (symbol.Kind == SymbolKind.NamedType && (symbol as ITypeSymbol).TypeKind != TypeKind.Class) {
					yield break;
				}
				yield return _Classifications.SealedMember;
			}
			else if (symbol.IsOverride) {
				yield return _Classifications.OverrideMember;
			}
			else if (symbol.IsVirtual) {
				yield return _Classifications.VirtualMember;
			}
			else if (symbol.IsAbstract) {
				yield return _Classifications.AbstractMember;
			}
		}

		void OnTextBufferChanged(object sender, TextContentChangedEventArgs e) => _SemanticModel = null;

		// TODO: it's not good idea subscribe on text document disposed. Try to subscribe on text
		// document closed.
		void OnTextDocumentDisposed(object sender, TextDocumentEventArgs e) {
			if (e.TextDocument.TextBuffer == _TextBuffer) {
				_SemanticModel = null;
				_TextBuffer.Changed -= OnTextBufferChanged;
				_TextDocumentFactoryService.TextDocumentDisposed -= OnTextDocumentDisposed;
			}
		}

		static ClassificationSpan CreateClassificationSpan(ITextSnapshot snapshotSpan, TextSpan span, IClassificationType type) {
			return new ClassificationSpan(new SnapshotSpan(snapshotSpan, span.Start, span.Length), type);
		}
	}
}