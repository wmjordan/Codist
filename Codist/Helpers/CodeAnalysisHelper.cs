using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;

namespace Codist
{
	static class CodeAnalysisHelper
	{
		public static Document GetDocument(this Workspace workspace, SnapshotSpan span) {
			var solution = workspace.CurrentSolution;
			var sourceText = span.Snapshot.AsText();
			var docId = workspace.GetDocumentIdInCurrentContext(sourceText.Container);
			return solution.ContainsDocument(docId)
				? solution.GetDocument(docId)
				: solution.WithDocumentText(docId, sourceText, PreservationMode.PreserveIdentity).GetDocument(docId);
		}

		public static ISymbol GetSymbolExt(this SemanticModel semanticModel, SyntaxNode node) {
			return node.IsDeclaration() || node.Kind() == SyntaxKind.VariableDeclarator ? semanticModel.GetDeclaredSymbol(node) :
					(node is AttributeArgumentSyntax
						? semanticModel.GetSymbolInfo((node as AttributeArgumentSyntax).Expression).Symbol
						: null)
					?? (node is SimpleBaseTypeSyntax || node is TypeConstraintSyntax
						? semanticModel.GetSymbolInfo(node.FindNode(node.Span, false, true)).Symbol
						: null)
					?? (node.Parent is MemberAccessExpressionSyntax
						? semanticModel.GetSymbolInfo(node.Parent).CandidateSymbols.FirstOrDefault()
						: null)
					?? (node.Parent is ArgumentSyntax
						? semanticModel.GetSymbolInfo((node.Parent as ArgumentSyntax).Expression).CandidateSymbols.FirstOrDefault()
						: null)
					?? (node is TypeParameterSyntax || node is ParameterSyntax ? semanticModel.GetDeclaredSymbol(node) : null);
		}

		public static void GoToSource(this ISymbol symbol) {
			if (symbol != null && symbol.DeclaringSyntaxReferences.Length > 0) {
				var loc = symbol.DeclaringSyntaxReferences[0];
				var path = loc.SyntaxTree?.FilePath;
				if (path == null) {
					return;
				}
				loc.GoToSource();
			}
		}

		public static void GoToSource(this SyntaxReference loc) {
			var pos = loc.SyntaxTree.GetLineSpan(loc.Span).StartLinePosition;
			CodistPackage.DTE.OpenFile(loc.SyntaxTree.FilePath, pos.Line + 1, pos.Character + 1);
		}

		static void OpenFile(this EnvDTE.DTE dte, string file, int line, int column) {
			if (String.IsNullOrEmpty(file)) {
				return;
			}
			file = System.IO.Path.GetFullPath(file);
			if (System.IO.File.Exists(file) == false) {
				return;
			}
			using (new NewDocumentStateScope(__VSNEWDOCUMENTSTATE.NDS_Provisional, VSConstants.NewDocumentStateReason.Navigation)) {
				dte.ItemOperations.OpenFile(file);
				((EnvDTE.TextSelection)dte.ActiveDocument.Selection).MoveToLineAndOffset(line, column);
			}
		}

		public static bool IsDeclaration(this SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.ClassDeclaration:
				case SyntaxKind.ConstructorDeclaration:
				case SyntaxKind.ConversionOperatorDeclaration:
				case SyntaxKind.DelegateDeclaration:
				case SyntaxKind.DestructorDeclaration:
				case SyntaxKind.EnumDeclaration:
				case SyntaxKind.EnumMemberDeclaration:
				case SyntaxKind.EventDeclaration:
				case SyntaxKind.EventFieldDeclaration:
				case SyntaxKind.FieldDeclaration:
				case SyntaxKind.IndexerDeclaration:
				case SyntaxKind.InterfaceDeclaration:
				case SyntaxKind.LocalDeclarationStatement:
				case SyntaxKind.MethodDeclaration:
				case SyntaxKind.NamespaceDeclaration:
				case SyntaxKind.OperatorDeclaration:
				case SyntaxKind.PropertyDeclaration:
				case SyntaxKind.StructDeclaration:
				case SyntaxKind.VariableDeclaration:
				//case SyntaxKind.VariableDeclarator:
					return true;
			}
			return false;
		}

		public static bool IsSyntaxBlock(this SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.ArgumentList:
				case SyntaxKind.AttributeArgumentList:
				//case SyntaxKind.Block:
				case SyntaxKind.DoStatement:
				case SyntaxKind.FixedStatement:
				case SyntaxKind.ForEachStatement:
				case SyntaxKind.ForStatement:
				case SyntaxKind.IfStatement:
				case SyntaxKind.LockStatement:
				case SyntaxKind.SwitchStatement:
				case SyntaxKind.SwitchSection:
				case SyntaxKind.TryStatement:
				case SyntaxKind.UsingStatement:
				case SyntaxKind.WhileStatement:
				case SyntaxKind.ParameterList:
				case SyntaxKind.ParenthesizedExpression:
				case SyntaxKind.ParenthesizedLambdaExpression:
				case SyntaxKind.SimpleLambdaExpression:
				case SyntaxKind.UnsafeStatement:
				case SyntaxKind.XmlElement:
				case SyntaxKind.XmlEmptyElement:
				case SyntaxKind.XmlComment:
					return true;
			}
			return false;
		}
		public static ImageMoniker GetSyntaxMoniker(this SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.ClassDeclaration: return KnownMonikers.Class;
				case SyntaxKind.EnumDeclaration: return KnownMonikers.Enumeration;
				case SyntaxKind.StructDeclaration: return KnownMonikers.Structure;
				case SyntaxKind.InterfaceDeclaration: return KnownMonikers.Interface;
				case SyntaxKind.ConstructorDeclaration: return KnownMonikers.NewItem;
				case SyntaxKind.ConversionOperatorDeclaration: return KnownMonikers.ConvertPartition;
				case SyntaxKind.DestructorDeclaration: return KnownMonikers.DeleteListItem;
				case SyntaxKind.IndexerDeclaration: return KnownMonikers.ClusteredIndex;
				case SyntaxKind.MethodDeclaration: return KnownMonikers.Method;
				case SyntaxKind.OperatorDeclaration: return KnownMonikers.Operator;
				case SyntaxKind.PropertyDeclaration: return KnownMonikers.Property;
				case SyntaxKind.FieldDeclaration: return KnownMonikers.Field;
				case SyntaxKind.VariableDeclaration: return KnownMonikers.LocalVariable;
				case SyntaxKind.NamespaceDeclaration: return KnownMonikers.Namespace;
				case SyntaxKind.ArgumentList:
				case SyntaxKind.AttributeArgumentList: return KnownMonikers.Parameter;
				case SyntaxKind.DoStatement: return KnownMonikers.DoWhile;
				case SyntaxKind.FixedStatement: return KnownMonikers.Pin;
				case SyntaxKind.ForEachStatement:
				case SyntaxKind.ForStatement: return KnownMonikers.ForEachLoop;
				case SyntaxKind.IfStatement: return KnownMonikers.If;
				case SyntaxKind.LockStatement: return KnownMonikers.Lock;
				case SyntaxKind.SwitchStatement: return KnownMonikers.FlowSwitch;
				case SyntaxKind.SwitchSection: return KnownMonikers.FlowDecision;
				case SyntaxKind.TryStatement: return KnownMonikers.TryCatch;
				case SyntaxKind.UsingStatement: return KnownMonikers.Entry;
				case SyntaxKind.WhileStatement: return KnownMonikers.While;
				case SyntaxKind.ParameterList: return KnownMonikers.Parameter;
				case SyntaxKind.ParenthesizedExpression:
				case SyntaxKind.ParenthesizedLambdaExpression:
				case SyntaxKind.SimpleLambdaExpression: return KnownMonikers.NamedSet;
				case SyntaxKind.UnsafeStatement: return KnownMonikers.HotSpot;
				case SyntaxKind.XmlElement:
				case SyntaxKind.XmlEmptyElement: return KnownMonikers.XMLElement;
				case SyntaxKind.XmlComment: return KnownMonikers.XMLCommentTag;
			}
			return KnownMonikers.UnknownMember;
		}
		public static string GetSyntaxBrief(this SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.ClassDeclaration: return "class";
				case SyntaxKind.EnumDeclaration: return "enum";
				case SyntaxKind.StructDeclaration: return "struct";
				case SyntaxKind.InterfaceDeclaration: return "interface";
				case SyntaxKind.ConstructorDeclaration: return "constructor";
				case SyntaxKind.ConversionOperatorDeclaration: return "conversion operator";
				case SyntaxKind.DestructorDeclaration: return "destructor";
				case SyntaxKind.IndexerDeclaration: return "property";
				case SyntaxKind.MethodDeclaration: return "method";
				case SyntaxKind.OperatorDeclaration: return "operator";
				case SyntaxKind.PropertyDeclaration: return "property";
				case SyntaxKind.FieldDeclaration: return "field";
				case SyntaxKind.NamespaceDeclaration: return "namespace";
				case SyntaxKind.ArgumentList:
				case SyntaxKind.AttributeArgumentList: return "argument list";
				case SyntaxKind.DoStatement: return "do loop";
				case SyntaxKind.FixedStatement: return "fixed";
				case SyntaxKind.ForEachStatement: return "foreach loop";
				case SyntaxKind.ForStatement: return "for loop";
				case SyntaxKind.IfStatement: return "if statement";
				case SyntaxKind.LocalDeclarationStatement: return "local";
				case SyntaxKind.LockStatement: return "lock statement";
				case SyntaxKind.SwitchStatement: return "switch";
				case SyntaxKind.SwitchSection: return "switch section";
				case SyntaxKind.TryStatement: return "try catch";
				case SyntaxKind.UsingStatement: return "using statement";
				case SyntaxKind.WhileStatement: return "while loop";
				case SyntaxKind.ParameterList: return "parameter list";
				case SyntaxKind.ParenthesizedExpression:
				case SyntaxKind.ParenthesizedLambdaExpression:
				case SyntaxKind.SimpleLambdaExpression: return "expression";
				case SyntaxKind.UnsafeStatement: return "unsafe";
				case SyntaxKind.VariableDeclarator: return "variable";
				case SyntaxKind.XmlElement:
				case SyntaxKind.XmlEmptyElement: return "xml element";
				case SyntaxKind.XmlComment: return "xml comment";
			}
			return null;
		}

		public static string GetDeclarationSignature(this SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.ClassDeclaration:
				case SyntaxKind.EnumDeclaration:
				case SyntaxKind.StructDeclaration:
				case SyntaxKind.InterfaceDeclaration:
					return (node as BaseTypeDeclarationSyntax).Identifier.Text;
				case SyntaxKind.ConstructorDeclaration: return (node as ConstructorDeclarationSyntax).Identifier.Text;
				case SyntaxKind.ConversionOperatorDeclaration: return (node as ConversionOperatorDeclarationSyntax).OperatorKeyword.Text;
				case SyntaxKind.DestructorDeclaration: return (node as DestructorDeclarationSyntax).Identifier.Text;
				case SyntaxKind.IndexerDeclaration: return "Indexer";
				case SyntaxKind.MethodDeclaration: return (node as MethodDeclarationSyntax).Identifier.Text;
				case SyntaxKind.OperatorDeclaration: return (node as OperatorDeclarationSyntax).OperatorKeyword.Text;
				case SyntaxKind.PropertyDeclaration: return (node as PropertyDeclarationSyntax).Identifier.Text;
				case SyntaxKind.SimpleLambdaExpression: return "(" + (node as SimpleLambdaExpressionSyntax).Parameter.ToString() + ")";
				case SyntaxKind.ParenthesizedLambdaExpression: return (node as ParenthesizedLambdaExpressionSyntax).ParameterList.ToString();
				case SyntaxKind.NamespaceDeclaration: return ((node as NamespaceDeclarationSyntax).Name as IdentifierNameSyntax)?.Identifier.Text;
				case SyntaxKind.VariableDeclarator: return (node as VariableDeclaratorSyntax).Identifier.Text;
				case SyntaxKind.LocalDeclarationStatement:
				case SyntaxKind.VariableDeclaration: return "Variables";
			}
			return null;
		}

		public static bool IsLineComment(this SyntaxTrivia trivia) {
			switch (trivia.Kind()) {
				case SyntaxKind.MultiLineCommentTrivia:
				case SyntaxKind.SingleLineCommentTrivia:
					return true;
			}
			return false;
		}
		public static string GetAccessibility(this ISymbol symbol) {
			switch (symbol.DeclaredAccessibility) {
				case Accessibility.Public: return "public ";
				case Accessibility.Private: return "private ";
				case Accessibility.ProtectedAndInternal: return "protected internal ";
				case Accessibility.Protected: return "protected ";
				case Accessibility.Internal: return "internal ";
				case Accessibility.ProtectedOrInternal: return "protected or internal ";
				default: return String.Empty;
			}
		}

		public static bool IsMemberOrType(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Event:
				case SymbolKind.Field:
				case SymbolKind.Method:
				case SymbolKind.Property:
				case SymbolKind.NamedType:
					return true;
			}
			return false;
		}

		public static string GetTypeName(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Event: return "event";
				case SymbolKind.Field:
					return "field";
				case SymbolKind.Label: return "label";
				case SymbolKind.Local:
					return (symbol as ILocalSymbol).IsConst
						? "local const"
						: "local";
				case SymbolKind.Method:
					return (symbol as IMethodSymbol).IsExtensionMethod
						? "extension"
						: "method";
				case SymbolKind.NamedType:
					switch ((symbol as INamedTypeSymbol).TypeKind) {
						case TypeKind.Array: return "array";
						case TypeKind.Dynamic: return "dynamic";
						case TypeKind.Class: return "class";
						case TypeKind.Delegate: return "delegate";
						case TypeKind.Enum: return "enum";
						case TypeKind.Interface: return "interface";
						case TypeKind.Struct: return "struct";
						case TypeKind.TypeParameter: return "type parameter";
					}
					return "type";
				case SymbolKind.Namespace: return "namespace";
				case SymbolKind.Parameter: return "parameter";
				case SymbolKind.Property: return "property";
				case SymbolKind.TypeParameter: return "type parameter";
				default:
					return symbol.Kind.ToString();
			}
		}

		public static StandardGlyphGroup GetGlyphGroup(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Alias: return StandardGlyphGroup.GlyphForwardType;
				case SymbolKind.Assembly: return StandardGlyphGroup.GlyphAssembly;
				case SymbolKind.DynamicType: return StandardGlyphGroup.GlyphGroupType;
				case SymbolKind.ErrorType: return StandardGlyphGroup.GlyphGroupError;
				case SymbolKind.Event: return StandardGlyphGroup.GlyphGroupEvent;
				case SymbolKind.Field:
					return (symbol as IFieldSymbol).IsConst
						? StandardGlyphGroup.GlyphGroupConstant
						: StandardGlyphGroup.GlyphGroupField;
				case SymbolKind.Label: return StandardGlyphGroup.GlyphArrow;
				case SymbolKind.Local: return StandardGlyphGroup.GlyphGroupVariable;
				case SymbolKind.Method:
					return (symbol as IMethodSymbol).IsExtensionMethod
						? StandardGlyphGroup.GlyphExtensionMethod
						: StandardGlyphGroup.GlyphGroupMethod;
				case SymbolKind.NetModule: return StandardGlyphGroup.GlyphGroupModule;
				case SymbolKind.NamedType:
					switch ((symbol as INamedTypeSymbol).TypeKind) {
						case TypeKind.Unknown: return StandardGlyphGroup.GlyphGroupUnknown;
						case TypeKind.Array:
						case TypeKind.Dynamic:
						case TypeKind.Class:
							return StandardGlyphGroup.GlyphGroupClass;
						case TypeKind.Delegate: return StandardGlyphGroup.GlyphGroupDelegate;
						case TypeKind.Enum: return StandardGlyphGroup.GlyphGroupEnum;
						case TypeKind.Error: return StandardGlyphGroup.GlyphGroupError;
						case TypeKind.Interface: return StandardGlyphGroup.GlyphGroupInterface;
						case TypeKind.Module: return StandardGlyphGroup.GlyphGroupModule;
						case TypeKind.Pointer:
						case TypeKind.Struct: return StandardGlyphGroup.GlyphGroupStruct;
					}
					return StandardGlyphGroup.GlyphGroupType;
				case SymbolKind.Namespace: return StandardGlyphGroup.GlyphGroupNamespace;
				case SymbolKind.Parameter: return StandardGlyphGroup.GlyphGroupVariable;
				case SymbolKind.Property: return StandardGlyphGroup.GlyphGroupProperty;
				case SymbolKind.TypeParameter: return StandardGlyphGroup.GlyphGroupType;
				default: return StandardGlyphGroup.GlyphGroupUnknown;
			}
		}

		public static string GetAssemblyModuleName(this ISymbol symbol) {
			return symbol.ContainingAssembly?.Modules?.FirstOrDefault()?.Name
					?? symbol.ContainingAssembly?.Name;
		}

		public static StandardGlyphItem GetGlyphItem(this ISymbol symbol) {
			switch (symbol.DeclaredAccessibility) {
				case Accessibility.Private: return StandardGlyphItem.GlyphItemPrivate;
				case Accessibility.ProtectedAndInternal:
				case Accessibility.Protected: return StandardGlyphItem.GlyphItemProtected;
				case Accessibility.Internal: return StandardGlyphItem.GlyphItemInternal;
				case Accessibility.ProtectedOrInternal: return StandardGlyphItem.GlyphItemFriend;
				case Accessibility.Public: return StandardGlyphItem.GlyphItemPublic;
				default: return StandardGlyphItem.TotalGlyphItems;
			}
		}
		public static ITypeSymbol GetReturnType(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Field: return (symbol as IFieldSymbol).Type;
				case SymbolKind.Local: return (symbol as ILocalSymbol).Type;
				case SymbolKind.Method: return (symbol as IMethodSymbol).ReturnType;
				case SymbolKind.Parameter: return (symbol as IParameterSymbol).Type;
				case SymbolKind.Property: return (symbol as IPropertySymbol).Type;
			}
			return null;
		}
		public static ImmutableArray<IParameterSymbol> GetParameters(this ISymbol symbol) {
			switch (symbol.Kind) {
				case SymbolKind.Method: return (symbol as IMethodSymbol).Parameters;
				case SymbolKind.Event: return (symbol as IEventSymbol).AddMethod.Parameters;
				case SymbolKind.Property: return (symbol as IPropertySymbol).Parameters;
			}
			return default(ImmutableArray<IParameterSymbol>);
		}

		public static ImmutableArray<SyntaxReference> GetSourceLocations(this ISymbol symbol) {
			return symbol == null || symbol.DeclaringSyntaxReferences.Length == 0
				? ImmutableArray<SyntaxReference>.Empty
				: symbol.DeclaringSyntaxReferences.RemoveAll(i => i.SyntaxTree.FilePath == null);
		}

		/// <summary>
		/// Checks whether the given symbol has the given <paramref name="kind"/>, <paramref name="returnType"/> and <paramref name="parameters"/>.
		/// </summary>
		/// <param name="symbol">The symbol to be checked.</param>
		/// <param name="kind">The <see cref="SymbolKind"/> the symbol should have.</param>
		/// <param name="returnType">The type that the symbol should return.</param>
		/// <param name="parameters">The parameters the symbol should take.</param>
		public static bool MatchSignature(this ISymbol symbol, SymbolKind kind, ITypeSymbol returnType, ImmutableArray<IParameterSymbol> parameters) {
			if (symbol.Kind != kind
				|| returnType == null && symbol.GetReturnType() != null
				|| returnType != null && returnType.Equals(symbol.GetReturnType()) == false) {
				return false;
			}
			var method = kind == SymbolKind.Method ? symbol as IMethodSymbol
				: kind == SymbolKind.Event ? (symbol as IEventSymbol).RaiseMethod
				: null;
			if (method != null && parameters.IsDefault == false) {
				var memberParameters = method.Parameters;
				if (memberParameters.Length != parameters.Length) {
					return false;
				}
				for (int i = parameters.Length - 1; i >= 0; i--) {
					var pi = parameters[i];
					var mi = memberParameters[i];
					if (pi.Type.Equals(mi.Type) == false
						|| pi.RefKind != mi.RefKind) {
						return false;
					}
				}
			}
			return true;
		}
		public static bool IsAccessible(this ISymbol symbol) {
			return symbol.DeclaredAccessibility == Accessibility.Public || symbol.DeclaredAccessibility == Accessibility.NotApplicable || symbol.Locations.Any(l => l.IsInSource);
		}

		public static bool IsCommonClass(this ISymbol symbol) {
			if (symbol.Kind == SymbolKind.NamedType) {
				var name = symbol.Name;
				return name == "Object" || name == "ValueType" || name == "Enum" || name == "MulticastDelegate";
			}
			return false;
		}
		public static bool IsType(this CodeMemberType type) {
			return type > CodeMemberType.Root && type < CodeMemberType.Member;
		}
		public static bool IsMember(this CodeMemberType type) {
			return type > CodeMemberType.Member && type < CodeMemberType.Other;
		}
		public static XElement InheritDocumentation(this ISymbol symbol, out ISymbol baseMember) {
			return InheritDocumentation(symbol, symbol, out baseMember);
		}
		static XElement InheritDocumentation(ISymbol symbol, ISymbol querySymbol, out ISymbol baseMember) {
			var t = symbol.Kind == SymbolKind.NamedType ? symbol as INamedTypeSymbol : symbol.ContainingType;
			if (t == null
				// go to the base type if not querying interface
				|| t.TypeKind != TypeKind.Interface && (t = t.BaseType) == null
				) {
				baseMember = null;
				return null;
			}
			XElement doc;
			var member = t.GetMembers(querySymbol.Name).FirstOrDefault(i => i.MatchSignature(querySymbol.Kind, querySymbol.GetReturnType(), querySymbol.GetParameters()));
			if (member != null && (doc = member.GetXmlDoc().GetSummary()) != null) {
				baseMember = member;
				return doc;
			}
			if (t.TypeKind != TypeKind.Interface && (doc = InheritDocumentation(t, querySymbol, out baseMember)) != null) {
				return doc;
			}
			else if (symbol == querySymbol
				&& symbol.Kind != SymbolKind.NamedType
				&& (t = symbol.ContainingType) != null) {
				foreach (var item in t.Interfaces) {
					if ((doc = InheritDocumentation(item, querySymbol, out baseMember)) != null) {
						return doc;
					}
				}
				switch (symbol.Kind) {
					case SymbolKind.Method:
						foreach (var item in (symbol as IMethodSymbol).ExplicitInterfaceImplementations) {
							if ((doc = item.GetXmlDoc().GetSummary()) != null) {
								baseMember = item;
								return doc;
							}
						}
						break;
					case SymbolKind.Property:
						foreach (var item in (symbol as IPropertySymbol).ExplicitInterfaceImplementations) {
							if ((doc = item.GetXmlDoc().GetSummary()) != null) {
								baseMember = item;
								return doc;
							}
						}
						break;
					case SymbolKind.Event:
						foreach (var item in (symbol as IEventSymbol).ExplicitInterfaceImplementations) {
							if ((doc = item.GetXmlDoc().GetSummary()) != null) {
								baseMember = item;
								return doc;
							}
						}
						break;
				}
			}
			baseMember = null;
			return null;
		}


	}
}
