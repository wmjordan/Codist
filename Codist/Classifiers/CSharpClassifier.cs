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
	/// <summary>
	/// Classifier that classifies all text as an instance of the "EditorClassifier" classification type.
	/// </summary>
	internal sealed class CSharpClassifier : IClassifier
	{
		static CSharpClassifications _Classifications;

		readonly ITextBuffer _TextBuffer;
		readonly IClassificationTypeRegistryService _TypeRegistryService;
		readonly ITextDocumentFactoryService _TextDocumentFactoryService;

		SemanticModel _SemanticModel;

		/// <summary>
		/// Initializes a new instance of the <see cref="CSharpClassifier"/> class.
		/// </summary>
		/// <param name="registry"></param>
		/// <param name="textDocumentFactoryService"></param>
		/// <param name="buffer"></param>
		internal CSharpClassifier(
			IClassificationTypeRegistryService registry,
			ITextDocumentFactoryService textDocumentFactoryService,
			ITextBuffer buffer) {
			if (_Classifications == null) {
				_Classifications = new CSharpClassifications(registry);
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

			// NOTE: Workspace can be null for "Using directive is unnecessary". Also workspace can
			// be null when solution/project failed to load and VS gave some reasons of it or when
			// try to open a file doesn't contained in the current solution
			var snapshot = span.Snapshot;
			var workspace = snapshot.TextBuffer.GetWorkspace();
			if (workspace == null) {
				// TODO: Add supporting a files that doesn't included to the current solution
				return Array.Empty<ClassificationSpan>();
			}
			var result = new List<ClassificationSpan>(16);
			var semanticModel = _SemanticModel ?? (_SemanticModel = workspace.GetDocument(span).GetSemanticModelAsync().Result);

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
								return false;
							case SyntaxKind.ReturnKeyword:
							case SyntaxKind.GotoCaseStatement:
							case SyntaxKind.GotoDefaultStatement:
							case SyntaxKind.ContinueStatement:
							case SyntaxKind.ReturnStatement:
							case SyntaxKind.YieldReturnStatement:
							case SyntaxKind.YieldBreakStatement:
							case SyntaxKind.ThrowStatement:
								result.Add(CreateClassificationSpan(snapshot, item.TextSpan, _Classifications.ControlFlowKeyword));
								return false;
						}
						return false;
					}
					if (Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.XmlDocCode)
						&& ct == Constants.XmlDocCData) {
						ct = HighlightXmlDocCData(span, item, workspace, result, ct);
						return false;
					}
					if (ct == "punctuation" && item.TextSpan.Length == 1) {
						var s = snapshot.GetText(item.TextSpan.Start, item.TextSpan.Length)[0];
						if (s == '{' || s == '}') {
							var node = unitCompilation.FindNode(item.TextSpan, true, true);
							if (node is BaseTypeDeclarationSyntax == false
								&& (node = node.Parent) == null) {
								return false;
							}
							IClassificationType type = null;
							switch (node.Kind()) {
								case SyntaxKind.MethodDeclaration: type = _Classifications.Method; break;
								case SyntaxKind.ConstructorDeclaration: type = _Classifications.ConstructorMethod; break;
								case SyntaxKind.PropertyDeclaration: type = _Classifications.Property; break;
								case SyntaxKind.ClassDeclaration: type = _Classifications.ClassName; break;
								case SyntaxKind.InterfaceDeclaration: type = _Classifications.InterfaceName; break;
								case SyntaxKind.EnumDeclaration: type = _Classifications.EnumName; break;
								case SyntaxKind.StructDeclaration: type = _Classifications.StructName; break;
							}
							if (type != null) {
								if (Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.DeclarationBrace)) {
									result.Add(CreateClassificationSpan(snapshot, item.TextSpan, type));
								}
								result.Add(CreateClassificationSpan(snapshot, item.TextSpan, _Classifications.DeclarationBrace));
							}
						}
						return false;
					}

					return ct == Constants.CodeIdentifier
						|| ct == Constants.CodeClassName
						|| ct == Constants.CodeStructName
						|| ct == Constants.CodeInterfaceName
						|| ct == Constants.CodeEnumName
						|| ct == Constants.CodeTypeParameterName
						|| ct == Constants.CodeDelegateName;
				});

			foreach (var item in classifiedSpans) {
				var itemSpan = item.TextSpan;
				var node = unitCompilation.FindNode(itemSpan, true);

				foreach (var type in GetClassificationType(node, semanticModel)) {
					result.Add(CreateClassificationSpan(snapshot, itemSpan, type));
				}
			}

			return result;
		}

		private string HighlightXmlDocCData(SnapshotSpan span, ClassifiedSpan item, Workspace workspace, List<ClassificationSpan> result, string ct) {
			var snapshot = span.Snapshot;
			var start = item.TextSpan.Start;
			SyntaxNode root;
			var sourceText = snapshot.AsText();
			var docId = DocumentId.CreateNewId(workspace.GetDocumentIdInCurrentContext(sourceText.Container).ProjectId);
			//var solution = workspace.CurrentSolution.WithProjectCompilationOptions(docId.ProjectId, new CSharpCompilationOptions(OutputKind.ConsoleApplication, usings: new[] { "Codist" }));
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
			// NOTE: Some kind of nodes, for example ArgumentSyntax, should are handled with a
			// specific way
			node = node.Kind() == SyntaxKind.Argument ? (node as ArgumentSyntax).Expression : node;
			//System.Diagnostics.Debug.WriteLine(node.GetType().Name + node.Span.ToString());
			var symbol = semanticModel.GetSymbolInfo(node).Symbol;
			if (symbol == null) {
				symbol = semanticModel.GetDeclaredSymbol(node);
				if (symbol != null) {
					switch (symbol.Kind) {
						case SymbolKind.NamedType:
							yield return symbol.ContainingType != null ? _Classifications.NestedDeclaration : _Classifications.Declaration;
							break;
						case SymbolKind.Event:
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
				case SymbolKind.NamedType:
				case SymbolKind.PointerType:
				case SymbolKind.RangeVariable:
				case SymbolKind.Preprocessing:
					//case SymbolKind.Discard:
					break;

				case SymbolKind.Label:
					yield return _Classifications.Label;
					break;

				case SymbolKind.TypeParameter:
					yield return _Classifications.TypeParameter;
					break;

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
					break;

				case SymbolKind.Parameter:
					yield return _Classifications.Parameter;
					break;

				case SymbolKind.Method:
					var methodSymbol = symbol as IMethodSymbol;
					switch (methodSymbol.MethodKind) {
						case MethodKind.Constructor:
							yield return
								node is AttributeSyntax || node.Parent is AttributeSyntax || node.Parent?.Parent is AttributeSyntax ? _Classifications.AttributeNotation : _Classifications.ConstructorMethod;
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

				default:
					break;
			}

			if (symbol.IsStatic) {
				if (symbol.Kind != SymbolKind.Namespace) {
					yield return _Classifications.StaticMember;
				}
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
			else if (symbol.IsSealed) {
				yield return _Classifications.SealedMember;
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