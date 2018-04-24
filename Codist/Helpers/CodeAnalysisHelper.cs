using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;

namespace Codist
{
	static class CodeAnalysisHelper
	{
		public static bool AnyTextChanges(ITextVersion oldVersion, ITextVersion currentVersion) {
			while (oldVersion != currentVersion) {
				if (oldVersion.Changes.Count > 0) {
					return true;
				}

				oldVersion = oldVersion.Next;
			}

			return false;
		}

		public static Document GetDocument(this Workspace workspace, SnapshotSpan span) {
			var solution = workspace.CurrentSolution;
			var sourceText = span.Snapshot.AsText();
			var docId = workspace.GetDocumentIdInCurrentContext(sourceText.Container);
			return solution.ContainsDocument(docId)
				? solution.GetDocument(docId)
				: solution.WithDocumentText(docId, sourceText, PreservationMode.PreserveIdentity).GetDocument(docId);
		}

		public static void GoToSymbol(this ISymbol symbol) {
			if (symbol != null && symbol.DeclaringSyntaxReferences.Length > 0) {
				var loc = symbol.DeclaringSyntaxReferences[0];
				var path = loc.SyntaxTree?.FilePath;
				if (path == null) {
					return;
				}
				var pos = loc.SyntaxTree.GetLineSpan(loc.Span);
				var openDoc = ServiceProvider.GlobalProvider.GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
				openDoc.OpenFile(path, pos.StartLinePosition.Line + 1, pos.StartLinePosition.Character + 1);
			}
		}

		public static void OpenFile(this EnvDTE.DTE dte, string file, int line, int column) {
			if (file == null) {
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
				case SyntaxKind.VariableDeclarator:
					return true;
			}
			return false;
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
		public static bool IsType(this CodeMemberType type) {
			return type > CodeMemberType.Root && type < CodeMemberType.Member;
		}
		public static bool IsMember(this CodeMemberType type) {
			return type > CodeMemberType.Member && type < CodeMemberType.Other;
		}
	}
}
