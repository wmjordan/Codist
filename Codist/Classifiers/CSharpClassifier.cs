using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using AppHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Classifiers
{
	[Export(typeof(IClassifierProvider))]
	[ContentType(Constants.CodeTypes.CSharp)]
	sealed class CSharpClassifierProvider : IClassifierProvider
	{
		public IClassifier GetClassifier(ITextBuffer textBuffer) {
			return Config.Instance.Features.MatchFlags(Features.SyntaxHighlight)
				? textBuffer.Properties.GetOrCreateSingletonProperty(() => new CSharpClassifier())
				: null;
		}
	}

	/// <summary>A classifier for C# code syntax highlight.</summary>
	sealed class CSharpClassifier : IClassifier
	{
		static readonly CSharpClassifications _Classifications = new CSharpClassifications(ServicesHelper.Instance.ClassificationTypeRegistry);
		static readonly GeneralClassifications _GeneralClassifications = new GeneralClassifications(ServicesHelper.Instance.ClassificationTypeRegistry);

		public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

		public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
			var snapshot = span.Snapshot;
			var workspace = snapshot.TextBuffer.GetWorkspace();
			if (workspace == null) {
				return Array.Empty<ClassificationSpan>();
			}
			var result = new List<ClassificationSpan>(16);
			var semanticModel = SyncHelper.RunSync(() => workspace.GetDocument(span).GetSemanticModelAsync());

			var textSpan = new TextSpan(span.Start.Position, span.Length);
			var unitCompilation = semanticModel.SyntaxTree.GetCompilationUnitRoot();
			var classifiedSpans = Classifier.GetClassifiedSpans(semanticModel, textSpan, workspace);
			var lastTriviaSpan = default(TextSpan);
			SyntaxNode node;
			GetAttributeNotationSpan(snapshot, result, textSpan, unitCompilation);

			foreach (var item in classifiedSpans) {
				var ct = item.ClassificationType;
				switch (ct) {
					case "keyword":
					case Constants.CodeKeywordControl: {
						node = unitCompilation.FindNode(item.TextSpan, true, true);
						if (node is MemberDeclarationSyntax) {
							var token = unitCompilation.FindToken(item.TextSpan.Start);
							switch (token.Kind()) {
								case SyntaxKind.SealedKeyword:
								case SyntaxKind.OverrideKeyword:
								case SyntaxKind.AbstractKeyword:
								case SyntaxKind.VirtualKeyword:
								case SyntaxKind.NewKeyword:
									result.Add(CreateClassificationSpan(snapshot, item.TextSpan, _Classifications.AbstractionKeyword));
									continue;
							}
							continue;
						}
						switch (node.Kind()) {
							case SyntaxKind.BreakStatement:
								if (node.Parent is SwitchSectionSyntax) {
									continue;
								}
								goto case SyntaxKind.ReturnStatement;
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
							case SyntaxKind.ThrowExpression:
								result.Add(CreateClassificationSpan(snapshot, item.TextSpan, _GeneralClassifications.ControlFlowKeyword));
								continue;
							case SyntaxKind.IfStatement:
							case SyntaxKind.ElseClause:
							case SyntaxKind.SwitchStatement:
							case SyntaxKind.CaseSwitchLabel:
							case SyntaxKind.DefaultSwitchLabel:
								result.Add(CreateClassificationSpan(snapshot, item.TextSpan, _GeneralClassifications.BranchingKeyword));
								continue;
							case SyntaxKind.ForStatement:
							case SyntaxKind.ForEachStatement:
							case SyntaxKind.ForEachVariableStatement:
							case SyntaxKind.WhileStatement:
							case SyntaxKind.DoStatement:
							case SyntaxKind.SelectClause:
								result.Add(CreateClassificationSpan(snapshot, item.TextSpan, _GeneralClassifications.LoopKeyword));
								continue;
							case SyntaxKind.UsingStatement:
							case SyntaxKind.FixedStatement:
							case SyntaxKind.LockStatement:
							case SyntaxKind.UnsafeStatement:
							case SyntaxKind.TryStatement:
							case SyntaxKind.CatchClause:
							case SyntaxKind.CatchFilterClause:
							case SyntaxKind.FinallyClause:
								result.Add(CreateClassificationSpan(snapshot, item.TextSpan, _Classifications.ResourceKeyword));
								continue;
							case SyntaxKind.IsExpression:
							case SyntaxKind.IsPatternExpression:
							case SyntaxKind.AsExpression:
							case SyntaxKind.RefExpression:
							case SyntaxKind.RefType:
								result.Add(CreateClassificationSpan(snapshot, item.TextSpan, _GeneralClassifications.TypeCastKeyword));
								break;
							case SyntaxKind.Argument:
							case SyntaxKind.Parameter:
							case SyntaxKind.CrefParameter:
								var token = unitCompilation.FindToken(item.TextSpan.Start, true);
								switch (token.Kind()) {
									case SyntaxKind.InKeyword:
									case SyntaxKind.OutKeyword:
									case SyntaxKind.RefKeyword:
										result.Add(CreateClassificationSpan(snapshot, item.TextSpan, _GeneralClassifications.TypeCastKeyword));
										continue;
								}
								break;
						}
						continue;
					}
					case "operator":
					case Constants.CodeOverloadedOperator: {
						node = unitCompilation.FindNode(item.TextSpan);
						var opMethod = semanticModel.GetSymbol(node.IsKind(SyntaxKind.Argument) ? ((ArgumentSyntax)node).Expression : node) as IMethodSymbol;
						if (opMethod?.MethodKind == MethodKind.UserDefinedOperator) {
							result.Add(CreateClassificationSpan(snapshot, item.TextSpan, _Classifications.OverrideMember));
						}
						continue;
					}
					case Constants.CodePunctuation:
						if (item.TextSpan.Length == 1) {
							ClassifyPunctuation(item.TextSpan, snapshot, result, semanticModel, unitCompilation);
						}
						continue;
					default:
						if (ct == Constants.XmlDocDelimiter) {
							if (lastTriviaSpan.Contains(item.TextSpan)) {
								continue;
							}
							var trivia = unitCompilation.FindTrivia(item.TextSpan.Start);
							switch (trivia.Kind()) {
								case SyntaxKind.SingleLineDocumentationCommentTrivia:
								case SyntaxKind.MultiLineDocumentationCommentTrivia:
								case SyntaxKind.DocumentationCommentExteriorTrivia:
									lastTriviaSpan = trivia.FullSpan;
									result.Add(CreateClassificationSpan(snapshot, lastTriviaSpan, _Classifications.XmlDoc));
									continue;
							}
						}
						else if (ct == Constants.CodeIdentifier
							|| ct == Constants.CodeStaticSymbol
							|| ct.EndsWith("name", StringComparison.Ordinal)) {
							var itemSpan = item.TextSpan;
							node = unitCompilation.FindNode(itemSpan, true);
							foreach (var type in GetClassificationType(node, semanticModel)) {
								result.Add(CreateClassificationSpan(snapshot, itemSpan, type));
							}
						}
						break;
				}
			}
			return result;
		}

		static void GetAttributeNotationSpan(ITextSnapshot snapshot, List<ClassificationSpan> result, TextSpan textSpan, CompilationUnitSyntax unitCompilation) {
			var spanNode = unitCompilation.FindNode(textSpan, true, false);
			if (spanNode.HasLeadingTrivia && spanNode.GetLeadingTrivia().FullSpan.Contains(textSpan)) {
				return;
			}
			switch (spanNode.Kind()) {
				case SyntaxKind.AttributeArgument:
				case SyntaxKind.AttributeList:
				case SyntaxKind.AttributeArgumentList:
					result.Add(CreateClassificationSpan(snapshot, textSpan, _Classifications.AttributeNotation));
					return;
			}
		}

		static void ClassifyPunctuation(TextSpan itemSpan, ITextSnapshot snapshot, List<ClassificationSpan> result, SemanticModel semanticModel, CompilationUnitSyntax unitCompilation) {
			if (Config.Instance.SpecialHighlightOptions.HasAnyFlag(SpecialHighlightOptions.AllBraces) == false) {
				return;
			}
			var s = snapshot.GetText(itemSpan.Start, itemSpan.Length)[0];
			if (s == '{' || s == '}') {
				var node = unitCompilation.FindNode(itemSpan, true, true);
				if (node is BaseTypeDeclarationSyntax == false
					&& node is ExpressionSyntax == false
					&& node is NamespaceDeclarationSyntax == false
					&& node.Kind() != SyntaxKind.SwitchStatement && (node = node.Parent) == null) {
					return;
				}
				var type = ClassifySyntaxNode(node);
				if (type != null) {
					if (Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.SpecialPunctuation)) {
						result.Add(CreateClassificationSpan(snapshot, itemSpan, _GeneralClassifications.SpecialPunctuation));
					}
					if (type == _GeneralClassifications.BranchingKeyword) {
						if (Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.BranchBrace)) {
							result.Add(CreateClassificationSpan(snapshot, itemSpan, type));
						}
						return;
					}
					if (type == _GeneralClassifications.LoopKeyword) {
						if (Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.LoopBrace)) {
							result.Add(CreateClassificationSpan(snapshot, itemSpan, type));
						}
						return;
					}
					if (type == _Classifications.ResourceKeyword) {
						if (Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.ResourceBrace)) {
							result.Add(CreateClassificationSpan(snapshot, itemSpan, type));
						}
						return;
					}
					if (node is ExpressionSyntax == false) {
						result.Add(CreateClassificationSpan(snapshot, itemSpan, _Classifications.DeclarationBrace));
					}
					if (Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.DeclarationBrace)) {
						result.Add(CreateClassificationSpan(snapshot, itemSpan, type));
					}
				}
			}
			else if ((s == '(' || s == ')') && Config.Instance.SpecialHighlightOptions.HasAnyFlag(SpecialHighlightOptions.AllParentheses)) {
				var node = unitCompilation.FindNode(itemSpan, true, true);
				switch (node.Kind()) {
					case SyntaxKind.CastExpression:
						if (Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.CastBrace) == false) {
							return;
						}
						var symbol = semanticModel.GetSymbolInfo(((CastExpressionSyntax)node).Type).Symbol;
						if (symbol == null) {
							return;
						}
						var type = GetClassificationType(symbol);
						if (type != null) {
							if (Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.SpecialPunctuation)) {
								result.Add(CreateClassificationSpan(snapshot, itemSpan, _GeneralClassifications.SpecialPunctuation));
							}
							result.Add(CreateClassificationSpan(snapshot, itemSpan, type));
							return;
						}
						break;
					case SyntaxKind.ParenthesizedExpression:
						if (Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.CastBrace) == false) {
							return;
						}
						if (node.ChildNodes().FirstOrDefault().IsKind(SyntaxKind.AsExpression)) {
							symbol = semanticModel.GetSymbolInfo(((BinaryExpressionSyntax)node.ChildNodes().First()).Right).Symbol;
							if (symbol == null) {
								return;
							}
							type = GetClassificationType(symbol);
							if (type != null) {
								if (Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.SpecialPunctuation)) {
									result.Add(CreateClassificationSpan(snapshot, itemSpan, _GeneralClassifications.SpecialPunctuation));
								}
								result.Add(CreateClassificationSpan(snapshot, itemSpan, type));
								return;
							}
						}
						break;
					case SyntaxKind.SwitchStatement:
					case SyntaxKind.SwitchSection:
					case SyntaxKind.IfStatement:
					case SyntaxKind.ElseClause:
						MarkClassificationTypeForBrace(itemSpan, snapshot, result, _GeneralClassifications.BranchingKeyword, SpecialHighlightOptions.BranchBrace);
						return;
					case SyntaxKind.ForStatement:
					case SyntaxKind.ForEachStatement:
					case SyntaxKind.ForEachVariableStatement:
					case SyntaxKind.WhileStatement:
					case SyntaxKind.DoStatement:
						MarkClassificationTypeForBrace(itemSpan, snapshot, result, _GeneralClassifications.LoopKeyword, SpecialHighlightOptions.LoopBrace);
						return;
					case SyntaxKind.UsingStatement:
					case SyntaxKind.FixedStatement:
					case SyntaxKind.LockStatement:
					case SyntaxKind.UnsafeStatement:
					case SyntaxKind.TryStatement:
					case SyntaxKind.CatchDeclaration:
					case SyntaxKind.CatchClause:
					case SyntaxKind.CatchFilterClause:
					case SyntaxKind.FinallyClause:
						MarkClassificationTypeForBrace(itemSpan, snapshot, result, _Classifications.ResourceKeyword, SpecialHighlightOptions.ResourceBrace);
						return;
					case SyntaxKind.TupleExpression:
						MarkClassificationTypeForBrace(itemSpan, snapshot, result, _Classifications.ConstructorMethod, SpecialHighlightOptions.ParameterBrace);
						return;
				}
				if (Config.Instance.SpecialHighlightOptions.HasAnyFlag(SpecialHighlightOptions.SpecialPunctuation | SpecialHighlightOptions.ParameterBrace)) {
					node = (node as BaseArgumentListSyntax
					   ?? node as BaseParameterListSyntax
					   ?? (CSharpSyntaxNode)(node as CastExpressionSyntax)
					   )?.Parent;
					if (node != null) {
						var type = ClassifySyntaxNode(node);
						if (type != null) {
							MarkClassificationTypeForBrace(itemSpan, snapshot, result, type, SpecialHighlightOptions.ParameterBrace);
						}
					}
				}
			}
			else if (s == '[' || s == ']') {
				// highlight attribute annotation
				var node = unitCompilation.FindNode(itemSpan, true, false);
				if (node.IsKind(SyntaxKind.AttributeList)) {
					result.Add(CreateClassificationSpan(snapshot, node.Span, _Classifications.AttributeNotation));
				}
			}

			IClassificationType GetClassificationType(ISymbol symbol) {
				switch (symbol.Kind) {
					case SymbolKind.NamedType:
						switch (((INamedTypeSymbol)symbol).TypeKind) {
							case TypeKind.Class: return _Classifications.ClassName;
							case TypeKind.Interface: return _Classifications.InterfaceName;
							case TypeKind.Struct: return _Classifications.StructName;
							case TypeKind.Delegate: return _Classifications.DelegateName;
							case TypeKind.Enum: return _Classifications.EnumName;
						}
						break;
					case SymbolKind.ArrayType: return _Classifications.ClassName;
					case SymbolKind.Event: return _Classifications.Event;
					case SymbolKind.PointerType: return _Classifications.StructName;
					case SymbolKind.TypeParameter:
						var p = (ITypeParameterSymbol)symbol;
						foreach (var c in p.ConstraintTypes) {
							switch (c.SpecialType) {
								case SpecialType.System_Enum: return _Classifications.EnumName;
								case SpecialType.System_Delegate: return _Classifications.DelegateName;
							}
							return _Classifications.ClassName;
						}
						if (p.HasReferenceTypeConstraint) {
							return _Classifications.ClassName;
						}
						if (p.HasValueTypeConstraint || p.HasUnmanagedTypeConstraint) {
							return _Classifications.StructName;
						}
						return _Classifications.TypeParameter;
				}
				return null;
			}
		}

		static void MarkClassificationTypeForBrace(TextSpan itemSpan, ITextSnapshot snapshot, List<ClassificationSpan> result, IClassificationType type, SpecialHighlightOptions options) {
			if (Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.SpecialPunctuation)) {
				result.Add(CreateClassificationSpan(snapshot, itemSpan, _GeneralClassifications.SpecialPunctuation));
			}
			if (Config.Instance.SpecialHighlightOptions.MatchFlags(options)) {
				result.Add(CreateClassificationSpan(snapshot, itemSpan, type));
			}
		}

		static IClassificationType ClassifySyntaxNode(SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.MethodDeclaration:
				case SyntaxKind.AnonymousMethodExpression:
				case SyntaxKind.SimpleLambdaExpression:
				case SyntaxKind.ParenthesizedLambdaExpression:
				case SyntaxKind.LocalFunctionStatement:
					return _Classifications.Method;
				case SyntaxKind.InvocationExpression:
					return ((((InvocationExpressionSyntax)node).Expression as IdentifierNameSyntax)?.Identifier.ValueText == "nameof") ? null : _Classifications.Method;
				case SyntaxKind.ConstructorDeclaration:
				case SyntaxKind.AnonymousObjectCreationExpression:
				case SyntaxKind.ObjectInitializerExpression:
				case SyntaxKind.ObjectCreationExpression:
				case SyntaxKind.ComplexElementInitializerExpression:
				case SyntaxKind.CollectionInitializerExpression:
				case SyntaxKind.ArrayInitializerExpression:
				case SyntaxKind.ThisConstructorInitializer:
					return _Classifications.ConstructorMethod;
				case SyntaxKind.IndexerDeclaration:
				case SyntaxKind.PropertyDeclaration: return _Classifications.Property;
				case SyntaxKind.ClassDeclaration: return _Classifications.ClassName;
				case SyntaxKind.InterfaceDeclaration: return _Classifications.InterfaceName;
				case SyntaxKind.EnumDeclaration: return _Classifications.EnumName;
				case SyntaxKind.StructDeclaration: return _Classifications.StructName;
				case SyntaxKind.Attribute: return _Classifications.AttributeName;
				case SyntaxKind.EventDeclaration: return _Classifications.Event;
				case SyntaxKind.NamespaceDeclaration:
					return _Classifications.Namespace;
				case SyntaxKind.IfStatement:
				case SyntaxKind.ElseClause:
				case SyntaxKind.SwitchStatement:
				case SyntaxKind.SwitchSection:
					return _GeneralClassifications.BranchingKeyword;
				case SyntaxKind.ForStatement:
				case SyntaxKind.ForEachStatement:
				case SyntaxKind.ForEachVariableStatement:
				case SyntaxKind.WhileStatement:
				case SyntaxKind.DoStatement:
					return _GeneralClassifications.LoopKeyword;
				case SyntaxKind.UsingStatement:
				case SyntaxKind.LockStatement:
				case SyntaxKind.FixedStatement:
				case SyntaxKind.UnsafeStatement:
				case SyntaxKind.TryStatement:
				case SyntaxKind.CatchClause:
				case SyntaxKind.CatchFilterClause:
				case SyntaxKind.FinallyClause:
					return _Classifications.ResourceKeyword;
			}
			return null;
		}

		static IEnumerable<IClassificationType> GetClassificationType(SyntaxNode node, SemanticModel semanticModel) {
			node = node.Kind() == SyntaxKind.Argument ? ((ArgumentSyntax)node).Expression : node;
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
							yield return _Classifications.NestedDeclaration;
							break;
						case SymbolKind.Method:
							if (Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.LocalFunctionDeclaration)
								|| ((IMethodSymbol)symbol).MethodKind != MethodKind.LocalFunction) {
								yield return _Classifications.NestedDeclaration;
							}
							break;
						case SymbolKind.Property:
							if (symbol.ContainingType.IsAnonymousType == false) {
								yield return _Classifications.NestedDeclaration;
							}
							break;
						case SymbolKind.Field:
							if (node.IsKind(SyntaxKind.TupleElement) && ((TupleElementSyntax)node).Identifier.IsKind(SyntaxKind.None)) {
								symbol = semanticModel.GetTypeInfo(((TupleElementSyntax)node).Type).Type;
							}
							break;
						case SymbolKind.Local:
							yield return _Classifications.LocalDeclaration;
							break;
					}
				}
				else {
					// NOTE: handle alias in using directive
					if ((node.Parent as NameEqualsSyntax)?.Parent is UsingDirectiveSyntax) {
						yield return _Classifications.AliasNamespace;
					}
					else if (node is AttributeArgumentSyntax) {
						symbol = semanticModel.GetSymbolInfo(((AttributeArgumentSyntax)node).Expression).Symbol;
						if (symbol != null && symbol.Kind == SymbolKind.Field && (symbol as IFieldSymbol)?.IsConst == true) {
							yield return _Classifications.ConstField;
							yield return _Classifications.StaticMember;
						}
					}
					symbol = node.Parent is MemberAccessExpressionSyntax ? semanticModel.GetSymbolInfo(node.Parent).CandidateSymbols.FirstOrDefault()
						: node.Parent.IsKind(SyntaxKind.Argument) ? semanticModel.GetSymbolInfo(((ArgumentSyntax)node.Parent).Expression).CandidateSymbols.FirstOrDefault()
						: node.IsKind(SyntaxKind.SimpleBaseType) ? semanticModel.GetTypeInfo(((SimpleBaseTypeSyntax)node).Type).Type
						: node.IsKind(SyntaxKind.TypeConstraint) ? semanticModel.GetTypeInfo(((TypeConstraintSyntax)node).Type).Type
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
					var f = symbol as IFieldSymbol;
					yield return f.IsConst ? _Classifications.ConstField
						: f.IsReadOnly ? _Classifications.ReadonlyField
						: f.IsVolatile ? _Classifications.VolatileField
						: _Classifications.Field;
					break;

				case SymbolKind.Property:
					yield return _Classifications.Property;
					break;

				case SymbolKind.Event:
					yield return _Classifications.Event;
					break;

				case SymbolKind.Local:
					var localSymbol = symbol as ILocalSymbol;
					yield return localSymbol.IsConst ? _Classifications.ConstField : _Classifications.LocalVariable;
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
							yield return methodSymbol.IsExtensionMethod ? _Classifications.ExtensionMethod
								: methodSymbol.IsExtern ? _Classifications.ExternMethod
								: _Classifications.Method;
							break;
					}
					break;

				case SymbolKind.NamedType:
					break;

				default:
					yield break;
			}

			if (SymbolMarkManager.HasBookmark) {
				var markerStyle = SymbolMarkManager.GetSymbolMarkerStyle(symbol);
				if (markerStyle != null) {
					yield return markerStyle;
				}
			}

			if (SyntaxHighlight.FormatStore.IdentifySymbolSource && symbol.IsMemberOrType() && symbol.ContainingAssembly != null) {
				yield return symbol.ContainingAssembly.GetSourceType() == AssemblySource.Metadata
					? _Classifications.MetadataSymbol
					: _Classifications.UserSymbol;
			}

			if (symbol.IsStatic) {
				if (symbol.Kind != SymbolKind.Namespace) {
					yield return _Classifications.StaticMember;
				}
			}
			else if (symbol.IsSealed) {
				if (symbol.Kind == SymbolKind.NamedType && ((ITypeSymbol)symbol).TypeKind != TypeKind.Class) {
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

		static ClassificationSpan CreateClassificationSpan(ITextSnapshot snapshotSpan, TextSpan span, IClassificationType type) {
			return new ClassificationSpan(new SnapshotSpan(snapshotSpan, span.Start, span.Length), type);
		}
	}
}