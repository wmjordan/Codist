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

namespace Codist.Classifiers
{
	/// <summary>
	/// Classifier that classifies all text as an instance of the "EditorClassifier" classification type.
	/// </summary>
	internal sealed class CodeClassifier : IClassifier
	{
		readonly IClassificationType _localFieldType;
		readonly IClassificationType _namespaceType;
		readonly IClassificationType _parameterType;
		readonly IClassificationType _extensionMethodType;
		readonly IClassificationType _methodType;
		readonly IClassificationType _eventType;
		readonly IClassificationType _propertyType;
		readonly IClassificationType _fieldType;
		readonly IClassificationType _constFieldType;
		readonly IClassificationType _readonlyFieldType;
		readonly IClassificationType _aliasNamespaceType;
		readonly IClassificationType _constructorMethodType;
		readonly IClassificationType _declarationType;
		readonly IClassificationType _nestedDeclarationType;
		readonly IClassificationType _typeParameterType;
		readonly IClassificationType _staticMemberType;
		readonly IClassificationType _overrideMemberType;
		readonly IClassificationType _virtualMemberType;
		readonly IClassificationType _abstractMemberType;
		readonly IClassificationType _sealedType;
		readonly IClassificationType _externMethodType;
		readonly IClassificationType _labelType;
		readonly IClassificationType _attributeNotationType;

		readonly IClassificationType _returnKeywordType;
		readonly ITextBuffer _textBuffer;
		readonly ITextDocumentFactoryService _textDocumentFactoryService;

		SemanticModel _semanticModel;

		/// <summary>
		/// Initializes a new instance of the <see cref="CodeClassifier"/> class.
		/// </summary>
		/// <param name="registry"></param>
		/// <param name="textDocumentFactoryService"></param>
		/// <param name="buffer"></param>
		internal CodeClassifier(
			IClassificationTypeRegistryService registry,
			ITextDocumentFactoryService textDocumentFactoryService,
			ITextBuffer buffer) {
			_localFieldType = registry.GetClassificationType(Constants.CSharpLocalFieldName);
			_namespaceType = registry.GetClassificationType(Constants.CSharpNamespaceName);
			_parameterType = registry.GetClassificationType(Constants.CSharpParameterName);
			_extensionMethodType = registry.GetClassificationType(Constants.CSharpExtensionMethodName);
			_externMethodType = registry.GetClassificationType(Constants.CSharpExternMethodName);
			_methodType = registry.GetClassificationType(Constants.CSharpMethodName);
			_eventType = registry.GetClassificationType(Constants.CSharpEventName);
			_propertyType = registry.GetClassificationType(Constants.CSharpPropertyName);
			_fieldType = registry.GetClassificationType(Constants.CSharpFieldName);
			_constFieldType = registry.GetClassificationType(Constants.CSharpConstFieldName);
			_readonlyFieldType = registry.GetClassificationType(Constants.CSharpReadOnlyFieldName);
			_aliasNamespaceType = registry.GetClassificationType(Constants.CSharpAliasNamespaceName);
			_constructorMethodType = registry.GetClassificationType(Constants.CSharpConstructorMethodName);
			_declarationType = registry.GetClassificationType(Constants.CSharpDeclarationName);
			_nestedDeclarationType = registry.GetClassificationType(Constants.CSharpNestedDeclarationName);
			_staticMemberType = registry.GetClassificationType(Constants.CSharpStaticMemberName);
			_overrideMemberType = registry.GetClassificationType(Constants.CSharpOverrideMemberName);
			_virtualMemberType = registry.GetClassificationType(Constants.CSharpVirtualMemberName);
			_abstractMemberType = registry.GetClassificationType(Constants.CSharpAbstractMemberName);
			_sealedType = registry.GetClassificationType(Constants.CSharpSealedClassName);
			_typeParameterType = registry.GetClassificationType(Constants.CSharpTypeParameterName);
			_labelType = registry.GetClassificationType(Constants.CSharpLabel);
			_attributeNotationType = registry.GetClassificationType(Constants.CSharpAttributeNotation);

			_returnKeywordType = registry.GetClassificationType(Constants.CodeReturnKeyword);
			_textDocumentFactoryService = textDocumentFactoryService;
			_textBuffer = buffer;

			_textBuffer.Changed += OnTextBufferChanged;
			_textDocumentFactoryService.TextDocumentDisposed += OnTextDocumentDisposed;
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

			// NOTE: Workspace can be null for "Using directive is unnecessary". Also workspace can
			// be null when solution/project failed to load and VS gave some reasons of it or when
			// try to open a file doesn't contained in the current solution
			var workspace = span.Snapshot.TextBuffer.GetWorkspace();
			if (workspace == null) {
				// TODO: Add supporting a files that doesn't included to the current solution
				return new ClassificationSpan[0];
			}
			var result = new List<ClassificationSpan>();
			var semanticModel = _semanticModel ?? (_semanticModel = GetDocument(workspace, span).GetSemanticModelAsync().Result);

			var textSpan = new TextSpan(span.Start.Position, span.Length);
			var unitCompilation = semanticModel.SyntaxTree.GetCompilationUnitRoot();
			var classifiedSpans = Classifier.GetClassifiedSpans(semanticModel, textSpan, workspace)
				.Where(item => {
					var ct = item.ClassificationType;
					if (ct == "keyword") {
						// highlights: return, yield return, yield break, throw and continue
						var node = unitCompilation.FindNode(item.TextSpan);
						switch (node.Kind()) {
							case SyntaxKind.BreakStatement:
								if (node.Parent is SwitchSectionSyntax == false) {
									goto case SyntaxKind.ReturnStatement;
								}
								break;
							case SyntaxKind.ReturnKeyword:
							case SyntaxKind.GotoCaseStatement:
							case SyntaxKind.GotoDefaultStatement:
							case SyntaxKind.ContinueStatement:
							case SyntaxKind.ReturnStatement:
							case SyntaxKind.YieldReturnStatement:
							case SyntaxKind.YieldBreakStatement:
							case SyntaxKind.ThrowStatement:
								result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, _returnKeywordType));
								return false;
						}
					}
					//System.Diagnostics.Debug.WriteLine(ct);
					return ct == "identifier"
						|| ct == "class name"
						|| ct == "struct name"
						|| ct == "interface name"
						|| ct == "enum name"
						|| ct == "type parameter name"
						|| ct == "delegate name";
				});

			foreach (var item in classifiedSpans) {
				var node = unitCompilation.FindNode(item.TextSpan, true);

				// NOTE: Some kind of nodes, for example ArgumentSyntax, should are handled with a
				// specific way
				node = node.Kind() == SyntaxKind.Argument ? (node as ArgumentSyntax).Expression : node;
				//System.Diagnostics.Debug.WriteLine(node.GetType().Name + node.Span.ToString());
				var symbol = semanticModel.GetSymbolInfo(node).Symbol;
				if (symbol == null) {
					symbol = semanticModel.GetDeclaredSymbol(node);
					if (symbol == null) {
						// NOTE: handle alias in using directive
						if ((node.Parent as NameEqualsSyntax)?.Parent is UsingDirectiveSyntax) {
							result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, _aliasNamespaceType));
						}

						// TODO: Log information about a node and semantic model, because semantic model
						// didn't retrive information from node in this case
						//_logger.ConditionalInfo("Nothing is found. Span start at {0} and end at {1}", span.Start.Position, span.End.Position);
						//_logger.ConditionalInfo("Node is {0} {1}", node.Kind(), node.RawKind);
						continue;
					}
					switch (symbol.Kind) {
						case SymbolKind.NamedType:
							result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, symbol.ContainingType != null ? _nestedDeclarationType : _declarationType));
							break;
						case SymbolKind.Event:
						case SymbolKind.Method:
							result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, _declarationType));
							break;
						case SymbolKind.Property:
							if (symbol.ContainingType.IsAnonymousType == false) {
								result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, _declarationType));
							}
							break;
					}
				}
				switch (symbol.Kind) {
					case SymbolKind.Alias:
					case SymbolKind.ArrayType:
					case SymbolKind.Assembly:
					case SymbolKind.DynamicType:
					case SymbolKind.ErrorType:
					case SymbolKind.NetModule:
					case SymbolKind.NamedType:
					case SymbolKind.PointerType:
					case SymbolKind.RangeVariable:
					case SymbolKind.Preprocessing:
						//case SymbolKind.Discard:
						//_logger.ConditionalInfo("Symbol kind={0} was on position [{1}..{2}]", symbol.Kind, item.TextSpan.Start, item.TextSpan.End);
						//_logger.ConditionalInfo("Text was: {0}", node.GetText().ToString());
						break;

					case SymbolKind.Label:
						result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, _labelType));
						break;

					case SymbolKind.TypeParameter:
						result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, _typeParameterType));
						break;

					case SymbolKind.Field:
						var fieldSymbol = (symbol as IFieldSymbol);
						result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, fieldSymbol.IsConst ? _constFieldType : fieldSymbol.IsReadOnly ? _readonlyFieldType : _fieldType));
						break;

					case SymbolKind.Property:
						result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, _propertyType));
						break;

					case SymbolKind.Event:
						result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, _eventType));
						break;

					case SymbolKind.Local:
						result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, _localFieldType));
						break;

					case SymbolKind.Namespace:
						result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, _namespaceType));
						break;

					case SymbolKind.Parameter:
						result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, _parameterType));
						break;

					case SymbolKind.Method:
						var methodSymbol = symbol as IMethodSymbol;
						switch (methodSymbol.MethodKind) {
							case MethodKind.Constructor:
								result.Add(CreateClassificationSpan(
									span.Snapshot,
									item.TextSpan,
									node is AttributeSyntax || node.Parent is AttributeSyntax || node.Parent?.Parent is AttributeSyntax ? _attributeNotationType : _constructorMethodType));
								break;
							case MethodKind.Destructor:
							case MethodKind.StaticConstructor:
								result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, _constructorMethodType));
								break;
							default:
								result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, methodSymbol.IsExtensionMethod ? _extensionMethodType : methodSymbol.IsExtern ? _externMethodType : _methodType));
								break;
						}
						break;

					default:
						break;
				}

				if (symbol.IsStatic) {
					if (symbol.Kind != SymbolKind.Namespace) {
						result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, _staticMemberType));
					}
				}
				else if (symbol.IsOverride) {
					result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, _overrideMemberType));
				}
				else if (symbol.IsVirtual) {
					result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, _virtualMemberType));
				}
				else if (symbol.IsAbstract) {
					result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, _abstractMemberType));
				}
				else if (symbol.IsSealed) {
					result.Add(CreateClassificationSpan(span.Snapshot, item.TextSpan, _sealedType));
				}
			}

			return result;
		}

		static Document GetDocument(Workspace workspace, SnapshotSpan span) {
			var solution = workspace.CurrentSolution;
			var sourceText = span.Snapshot.AsText();
			var docId = workspace.GetDocumentIdInCurrentContext(sourceText.Container);
			return solution.ContainsDocument(docId)
				? solution.GetDocument(docId)
				: solution.WithDocumentText(docId, sourceText, PreservationMode.PreserveIdentity).GetDocument(docId);
		}

		void OnTextBufferChanged(object sender, TextContentChangedEventArgs e) => _semanticModel = null;

		// TODO: it's not good idea subscribe on text document disposed. Try to subscribe on text
		// document closed.
		void OnTextDocumentDisposed(object sender, TextDocumentEventArgs e) {
			if (e.TextDocument.TextBuffer == _textBuffer) {
				_semanticModel = null;
				_textBuffer.Changed -= OnTextBufferChanged;
				_textDocumentFactoryService.TextDocumentDisposed -= OnTextDocumentDisposed;
			}
		}

		static ClassificationSpan CreateClassificationSpan(ITextSnapshot snapshot, TextSpan span, IClassificationType type) {
			return new ClassificationSpan(new SnapshotSpan(snapshot, span.Start, span.Length), type);
		}
	}
}