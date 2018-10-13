using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;

namespace Codist
{
	static partial class CodeAnalysisHelper
	{
		const SyntaxKind LocalFunction = (SyntaxKind)8830;

		public static Document GetDocument(this Workspace workspace, SnapshotSpan span) {
			if (workspace == null) {
				throw new ArgumentNullException("workspace");
			}
			var solution = workspace.CurrentSolution;
			if (solution == null) {
				throw new InvalidOperationException("solution is null");
			}
			if (span.Snapshot == null) {
				throw new InvalidOperationException("snapshot is null");
			}
			var sourceText = span.Snapshot.AsText();
			if (sourceText == null) {
				throw new InvalidOperationException("sourceText is null");
			}
			var docId = workspace.GetDocumentIdInCurrentContext(sourceText.Container);
			if (docId == null) {
				throw new InvalidOperationException("docId is null");
			}
			return solution.ContainsDocument(docId)
				? solution.GetDocument(docId)
				: solution.WithDocumentText(docId, sourceText, PreservationMode.PreserveIdentity).GetDocument(docId);
		}

		public static IEnumerable<Document> GetRelatedDocuments(this Project project) {
			var projects = GetRelatedProjects(project);
			foreach (var proj in projects) {
				foreach (var doc in proj.Documents) {
					yield return doc;
				}
			}
		}

		/// <summary>
		/// Gets a collection containing <paramref name="project"/> itself, and projects referenced by <paramref name="project"/> or referencing <paramref name="project"/>.
		/// </summary>
		/// <param name="project">The project to be examined.</param>
		static HashSet<Project> GetRelatedProjects(this Project project) {
			var projects = new HashSet<Project>();
			GetRelatedProjects(project, projects);
			var id = project.Id;
			foreach (var proj in project.Solution.Projects) {
				if (projects.Contains(proj) == false
					&& proj.AllProjectReferences.Any(p => p.ProjectId == id)) {
					projects.Add(proj);
				}
			}

			return projects;
		}

		static void GetRelatedProjects(Project project, HashSet<Project> projects) {
			if (project == null) {
				return;
			}
			projects.Add(project);
			foreach (var pr in project.AllProjectReferences) {
				GetRelatedProjects(project.Solution.GetProject(pr.ProjectId), projects);
			}
		}

		public static ISymbol GetSymbolExt(this SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken = default) {
			return node.IsDeclaration() || node.Kind() == SyntaxKind.VariableDeclarator ? semanticModel.GetDeclaredSymbol(node, cancellationToken) :
					(node is AttributeArgumentSyntax
						? semanticModel.GetSymbolInfo((node as AttributeArgumentSyntax).Expression, cancellationToken).Symbol
						: null)
					?? (node is SimpleBaseTypeSyntax || node is TypeConstraintSyntax
						? semanticModel.GetSymbolInfo(node.FindNode(node.Span, false, true), cancellationToken).Symbol
						: null)
					?? (node is ArgumentListSyntax
						? semanticModel.GetSymbolInfo(node.Parent, cancellationToken).Symbol
						: null)
					?? (node.Parent is MemberAccessExpressionSyntax
						? semanticModel.GetSymbolInfo(node.Parent, cancellationToken).CandidateSymbols.FirstOrDefault()
						: null)
					?? (node.Parent is ArgumentSyntax
						? semanticModel.GetSymbolInfo((node.Parent as ArgumentSyntax).Expression, cancellationToken).CandidateSymbols.FirstOrDefault()
						: null)
					?? (node is AccessorDeclarationSyntax
						? semanticModel.GetDeclaredSymbol(node.Parent.Parent, cancellationToken)
						: null)
					?? (node is TypeParameterSyntax || node is ParameterSyntax ? semanticModel.GetDeclaredSymbol(node, cancellationToken) : null);
		}

		public static ISymbol GetSymbolOrFirstCandidate(this SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken = default) {
			var info = semanticModel.GetSymbolInfo(node, cancellationToken);
			return info.Symbol
				?? (info.CandidateSymbols.Length > 0 ? info.CandidateSymbols[0] : null);
		}

		public static void OpenFile(this EnvDTE.DTE dte, string file, int line, int column) {
			ThreadHelper.ThrowIfNotOnUIThread();
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
				case LocalFunction:
				//case SyntaxKind.VariableDeclarator:
					return true;
			}
			return false;
		}

		public static bool IsTypeDeclaration(this SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.ClassDeclaration:
				case SyntaxKind.DelegateDeclaration:
				case SyntaxKind.EnumDeclaration:
				case SyntaxKind.EventDeclaration:
				case SyntaxKind.InterfaceDeclaration:
				case SyntaxKind.StructDeclaration:
					return true;
			}
			return false;
		}

		public static bool IsMemberDeclaration(this SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.FieldDeclaration:
				case SyntaxKind.MethodDeclaration:
				case SyntaxKind.ConstructorDeclaration:
				case SyntaxKind.DestructorDeclaration:
				case SyntaxKind.EventFieldDeclaration:
				case SyntaxKind.PropertyDeclaration:
				case SyntaxKind.IndexerDeclaration:
				case SyntaxKind.OperatorDeclaration:
				case SyntaxKind.ConversionOperatorDeclaration:
				case SyntaxKind.EnumMemberDeclaration:
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
				case SyntaxKind.UncheckedStatement:
				case SyntaxKind.CheckedStatement:
				case SyntaxKind.ReturnStatement:
				case SyntaxKind.ExpressionStatement:
				case SyntaxKind.XmlElement:
				case SyntaxKind.XmlEmptyElement:
				case SyntaxKind.XmlComment:
					return true;
			}
			return false;
		}

		public static int GetImageId(this SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.ClassDeclaration: return GetClassIcon((ClassDeclarationSyntax)node);
				case SyntaxKind.EnumDeclaration: return GetEnumIcon((EnumDeclarationSyntax)node);
				case SyntaxKind.StructDeclaration: return GetStructIcon((StructDeclarationSyntax)node);
				case SyntaxKind.InterfaceDeclaration: return GetInterfaceIcon((InterfaceDeclarationSyntax)node);
				case SyntaxKind.ConstructorDeclaration: return KnownImageIds.NewItem;
				case SyntaxKind.MethodDeclaration: return GetMethodIcon((MethodDeclarationSyntax)node);
				case SyntaxKind.PropertyDeclaration:
				case SyntaxKind.IndexerDeclaration: return GetPropertyIcon((BasePropertyDeclarationSyntax)node);
				case SyntaxKind.OperatorDeclaration: return GetOperatorIcon((OperatorDeclarationSyntax)node);
				case SyntaxKind.ConversionOperatorDeclaration: return KnownImageIds.ConvertPartition;
				case SyntaxKind.FieldDeclaration: return GetFieldIcon((FieldDeclarationSyntax)node);
				case SyntaxKind.EnumMemberDeclaration: return KnownImageIds.EnumerationItemPublic;
				case SyntaxKind.VariableDeclarator: return node.Parent.Parent.GetImageId();
				case SyntaxKind.VariableDeclaration:
				case SyntaxKind.LocalDeclarationStatement: return KnownImageIds.LocalVariable;
				case SyntaxKind.NamespaceDeclaration: return KnownImageIds.Namespace;
				case SyntaxKind.ArgumentList:
				case SyntaxKind.AttributeArgumentList: return KnownImageIds.Parameter;
				case SyntaxKind.DoStatement: return KnownImageIds.DoWhile;
				case SyntaxKind.FixedStatement: return KnownImageIds.Pin;
				case SyntaxKind.ForEachStatement: return KnownImageIds.ForEach;
				case SyntaxKind.ForStatement: return KnownImageIds.ForEachLoop;
				case SyntaxKind.IfStatement: return KnownImageIds.If;
				case SyntaxKind.LockStatement: return KnownImageIds.Lock;
				case SyntaxKind.SwitchStatement: return KnownImageIds.FlowSwitch;
				case SyntaxKind.SwitchSection: return KnownImageIds.FlowDecision;
				case SyntaxKind.TryStatement: return KnownImageIds.TryCatch;
				case SyntaxKind.UsingStatement: return KnownImageIds.TransactedReceiveScope;
				case SyntaxKind.WhileStatement: return KnownImageIds.While;
				case SyntaxKind.ParameterList: return KnownImageIds.Parameter;
				case SyntaxKind.ParenthesizedExpression: return KnownImageIds.NamedSet;
				case SyntaxKind.ParenthesizedLambdaExpression:
				case SyntaxKind.SimpleLambdaExpression: return KnownImageIds.PartitionFunction;
				case SyntaxKind.DelegateDeclaration: return GetDelegateIcon((DelegateDeclarationSyntax)node);
				case SyntaxKind.EventDeclaration: return GetEventIcon((BasePropertyDeclarationSyntax)node);
				case SyntaxKind.EventFieldDeclaration: return GetEventFieldIcon((EventFieldDeclarationSyntax)node);
				case SyntaxKind.UnsafeStatement: return KnownImageIds.HotSpot;
				case SyntaxKind.XmlElement:
				case SyntaxKind.XmlEmptyElement: return KnownImageIds.XMLElement;
				case SyntaxKind.XmlComment: return KnownImageIds.XMLCommentTag;
				case SyntaxKind.DestructorDeclaration: return KnownImageIds.DeleteListItem;
				case SyntaxKind.UncheckedStatement: return KnownImageIds.CheckBoxUnchecked;
				case SyntaxKind.CheckedStatement: return KnownImageIds.CheckBoxChecked;
				case SyntaxKind.ReturnStatement: return KnownImageIds.Return;
				case SyntaxKind.ExpressionStatement: return KnownImageIds.Action;
				case LocalFunction: return KnownImageIds.MethodSnippet;
			}
			return KnownImageIds.UnknownMember;
			int GetClassIcon(ClassDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Text) {
						case "public": return KnownImageIds.ClassPublic;
						case "protected": return KnownImageIds.ClassProtected;
						case "internal": return KnownImageIds.ClassInternal;
						case "private": return KnownImageIds.ClassPrivate;
					}
				}
				return syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.ClassInternal : KnownImageIds.ClassPrivate;
			}
			int GetStructIcon(StructDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Text) {
						case "public": return KnownImageIds.StructurePublic;
						case "protected": return KnownImageIds.StructureProtected;
						case "internal": return KnownImageIds.StructureInternal;
						case "private": return KnownImageIds.StructurePrivate;
					}
				}
				return syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.StructureInternal : KnownImageIds.StructurePrivate;
			}
			int GetEnumIcon(EnumDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Text) {
						case "public": return KnownImageIds.EnumerationPublic;
						case "internal": return KnownImageIds.EnumerationInternal;
						case "private": return KnownImageIds.EnumerationPrivate;
					}
				}
				return syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.EnumerationInternal : KnownImageIds.EnumerationPrivate;
			}
			int GetInterfaceIcon(InterfaceDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Text) {
						case "public": return KnownImageIds.InterfacePublic;
						case "internal": return KnownImageIds.InterfaceInternal;
						case "private": return KnownImageIds.InterfacePrivate;
					}
				}
				return syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.InterfaceInternal : KnownImageIds.InterfacePrivate;
			}
			int GetEventIcon(BasePropertyDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Text) {
						case "public": return KnownImageIds.EventPublic;
						case "internal": return KnownImageIds.EventInternal;
						case "protected": return KnownImageIds.EventProtected;
						case "private": return KnownImageIds.EventPrivate;
					}
				}
				return syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.EventInternal : KnownImageIds.EventPrivate;
			}
			int GetEventFieldIcon(EventFieldDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Text) {
						case "public": return KnownImageIds.EventPublic;
						case "internal": return KnownImageIds.EventInternal;
						case "protected": return KnownImageIds.EventProtected;
					}
				}
				return KnownImageIds.EventPrivate;
			}
			int GetDelegateIcon(DelegateDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Text) {
						case "public": return KnownImageIds.DelegatePublic;
						case "internal": return KnownImageIds.DelegateInternal;
						case "protected": return KnownImageIds.DelegateProtected;
						case "private": return KnownImageIds.DelegatePrivate;
					}
				}
				return syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.DelegateInternal : KnownImageIds.DelegatePrivate;
			}
			int GetFieldIcon(FieldDeclarationSyntax syntax) {
				bool isConst = false;
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Text) {
						case "const": isConst = true; break;
						case "public": return isConst ? KnownImageIds.ConstantPublic : KnownImageIds.FieldPublic;
						case "internal": return isConst ? KnownImageIds.ConstantInternal : KnownImageIds.FieldInternal;
						case "protected": return isConst ? KnownImageIds.ConstantProtected : KnownImageIds.FieldProtected;
					}
				}
				return isConst ? KnownImageIds.ConstantPrivate : KnownImageIds.FieldPrivate;
			}
			int GetMethodIcon(MethodDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Text) {
						case "public": return KnownImageIds.MethodPublic;
						case "internal": return KnownImageIds.MethodInternal;
						case "protected": return KnownImageIds.MethodProtected;
					}
				}
				return KnownImageIds.MethodPrivate;
			}
			int GetPropertyIcon(BasePropertyDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Text) {
						case "public": return KnownImageIds.PropertyPublic;
						case "internal": return KnownImageIds.PropertyInternal;
						case "protected": return KnownImageIds.PropertyProtected;
					}
				}
				return KnownImageIds.PropertyPrivate;
			}
			int GetOperatorIcon(OperatorDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Text) {
						case "public": return KnownImageIds.OperatorPublic;
						case "internal": return KnownImageIds.OperatorInternal;
						case "protected": return KnownImageIds.OperatorProtected;
					}
				}
				return KnownImageIds.OperatorPrivate;
			}
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
				case SyntaxKind.DelegateDeclaration: return "delegate";
				case SyntaxKind.ArgumentList:
				case SyntaxKind.AttributeArgumentList: return "argument list";
				case SyntaxKind.DoStatement: return "do loop";
				case SyntaxKind.FixedStatement: return "fixed";
				case SyntaxKind.ForEachStatement: return "foreach";
				case SyntaxKind.ForStatement: return "for";
				case SyntaxKind.IfStatement: return "if";
				case SyntaxKind.LocalDeclarationStatement: return "local";
				case SyntaxKind.LockStatement: return "lock";
				case SyntaxKind.SwitchStatement: return "switch";
				case SyntaxKind.SwitchSection: return "switch section";
				case SyntaxKind.TryStatement: return "try catch";
				case SyntaxKind.UsingStatement: return "using";
				case SyntaxKind.WhileStatement: return "while";
				case SyntaxKind.ParameterList: return "parameter list";
				case SyntaxKind.ParenthesizedExpression:
				case SyntaxKind.ParenthesizedLambdaExpression:
				case SyntaxKind.SimpleLambdaExpression: return "expression";
				case SyntaxKind.UnsafeStatement: return "unsafe";
				case SyntaxKind.VariableDeclarator: return "variable";
				case SyntaxKind.UncheckedStatement: return "unchecked";
				case SyntaxKind.CheckedStatement: return "checked";
				case SyntaxKind.ReturnStatement: return "return";
				case SyntaxKind.ExpressionStatement: return "expression";
				case SyntaxKind.XmlElement:
				case SyntaxKind.XmlEmptyElement: return "xml element";
				case SyntaxKind.XmlComment: return "xml comment";
				case LocalFunction: return "local function";
			}
			return null;
		}

		/// <summary>Returns the object creation syntax node from an named type identifier node.</summary>
		/// <param name="node"></param>
		/// <returns>Returns the constructor node if <paramref name="node"/>'s parent is <see cref="SyntaxKind.ObjectCreationExpression"/>, otherwise, <see langword="null"/> is returned.</returns>
		public static SyntaxNode GetObjectCreationNode(this SyntaxNode node) {
			node = node.Parent;
			if (node == null) {
				return null;
			}
			var kind = node.Kind();
			if (kind == SyntaxKind.ObjectCreationExpression) {
				return node;
			}
			if (kind == SyntaxKind.QualifiedName) {
				node = node.Parent;
				if (node.IsKind(SyntaxKind.ObjectCreationExpression)) {
					return node;
				}
			}
			return null;
		}

		public static string GetDeclarationSignature(this SyntaxNode node, int position = 0) {
			switch (node.Kind()) {
				case SyntaxKind.ClassDeclaration: return GetClassSignature(node as ClassDeclarationSyntax);
				case SyntaxKind.StructDeclaration: return GetStructSignature(node as StructDeclarationSyntax);
				case SyntaxKind.InterfaceDeclaration: return GetInterfaceSignature(node as InterfaceDeclarationSyntax);
				case SyntaxKind.EnumDeclaration: return (node as EnumDeclarationSyntax).Identifier.Text;
				case SyntaxKind.MethodDeclaration: return GetMethodSignature(node as MethodDeclarationSyntax);
				case SyntaxKind.ArgumentList: return GetArgumentListSignature(node as ArgumentListSyntax);
				case SyntaxKind.ConstructorDeclaration: return (node as ConstructorDeclarationSyntax).Identifier.Text;
				case SyntaxKind.ConversionOperatorDeclaration: return (node as ConversionOperatorDeclarationSyntax).OperatorKeyword.Text;
				case SyntaxKind.DelegateDeclaration: return GetDelegateSignature(node as DelegateDeclarationSyntax);
				case SyntaxKind.EventDeclaration: return (node as EventDeclarationSyntax).Identifier.Text;
				case SyntaxKind.EventFieldDeclaration: return GetVariableSignature((node as EventFieldDeclarationSyntax).Declaration, position);
				case SyntaxKind.FieldDeclaration: return GetVariableSignature((node as FieldDeclarationSyntax).Declaration, position);
				case SyntaxKind.DestructorDeclaration: return (node as DestructorDeclarationSyntax).Identifier.Text;
				case SyntaxKind.IndexerDeclaration: return "Indexer";
				case SyntaxKind.OperatorDeclaration: return (node as OperatorDeclarationSyntax).OperatorKeyword.Text;
				case SyntaxKind.PropertyDeclaration: return (node as PropertyDeclarationSyntax).Identifier.Text;
				case SyntaxKind.EnumMemberDeclaration: return (node as EnumMemberDeclarationSyntax).Identifier.Text;
				case SyntaxKind.SimpleLambdaExpression: return "(" + (node as SimpleLambdaExpressionSyntax).Parameter.ToString() + ")";
				case SyntaxKind.ParenthesizedLambdaExpression: return (node as ParenthesizedLambdaExpressionSyntax).ParameterList.ToString();
				case SyntaxKind.NamespaceDeclaration: return GetNamespaceSignature(node as NamespaceDeclarationSyntax);
				case SyntaxKind.VariableDeclarator: return (node as VariableDeclaratorSyntax).Identifier.Text;
				case SyntaxKind.LocalDeclarationStatement: return GetVariableSignature((node as LocalDeclarationStatementSyntax).Declaration, position);
				case SyntaxKind.VariableDeclaration: return GetVariableSignature(node as VariableDeclarationSyntax, position);
				case SyntaxKind.ForEachStatement: return (node as ForEachStatementSyntax).Identifier.Text;
				case SyntaxKind.ForStatement: return GetVariableSignature((node as ForStatementSyntax).Declaration, position);
				case SyntaxKind.IfStatement: return (node as IfStatementSyntax).Condition.GetFirstIdentifier()?.Identifier.Text;
				case SyntaxKind.SwitchSection: return GetSwitchSignature(node as SwitchSectionSyntax);
				case SyntaxKind.SwitchStatement: return (node as SwitchStatementSyntax).Expression.GetLastIdentifier()?.Identifier.Text;
				case SyntaxKind.WhileStatement: return (node as WhileStatementSyntax).Condition.GetFirstIdentifier()?.Identifier.Text;
				case SyntaxKind.UsingStatement: return GetUsingSignature(node as UsingStatementSyntax);
				case SyntaxKind.LockStatement: return ((node as LockStatementSyntax).Expression as IdentifierNameSyntax)?.ToString();
				case SyntaxKind.DoStatement: return (node as DoStatementSyntax).Condition.GetFirstIdentifier()?.Identifier.Text;
				case SyntaxKind.TryStatement: return (node as TryStatementSyntax).Catches.FirstOrDefault()?.Declaration?.Type.ToString();
				case SyntaxKind.UncheckedStatement:
				case SyntaxKind.CheckedStatement: return (node as CheckedExpressionSyntax).Expression.GetFirstIdentifier()?.Identifier.Text;
				case SyntaxKind.ReturnStatement: return (node as ReturnStatementSyntax).Expression.GetFirstIdentifier()?.Identifier.Text;
				case SyntaxKind.ExpressionStatement: return (node as ExpressionStatementSyntax).Expression.GetFirstIdentifier()?.Identifier.Text;
			}
			return null;
			string GetNamespaceSignature(NamespaceDeclarationSyntax syntax) {
				var name = (syntax as NamespaceDeclarationSyntax).Name;
				return (name as IdentifierNameSyntax)?.Identifier.Text ?? "..." + (name as QualifiedNameSyntax)?.Right.Identifier.Text;
			}
			string GetClassSignature(ClassDeclarationSyntax syntax) => GetGenericSignature(syntax.Identifier.Text, syntax.Arity);
			string GetStructSignature(StructDeclarationSyntax syntax) => GetGenericSignature(syntax.Identifier.Text, syntax.Arity);
			string GetInterfaceSignature(InterfaceDeclarationSyntax syntax) => GetGenericSignature(syntax.Identifier.Text, syntax.Arity);
			string GetMethodSignature(MethodDeclarationSyntax syntax) => GetGenericSignature(syntax.Identifier.Text, syntax.Arity);
			string GetDelegateSignature(DelegateDeclarationSyntax syntax) => GetGenericSignature(syntax.Identifier.Text, syntax.Arity);
			string GetArgumentListSignature(ArgumentListSyntax syntax) {
				var exp = (node as ArgumentListSyntax).Parent;
				exp = ((exp as InvocationExpressionSyntax)?.Expression as MemberAccessExpressionSyntax)?.Name
					?? ((exp as InvocationExpressionSyntax)?.Expression as IdentifierNameSyntax)
					?? (exp as ObjectCreationExpressionSyntax)?.Type;
				return (exp as IdentifierNameSyntax)?.ToString() ?? (exp as QualifiedNameSyntax)?.Right.ToString() ?? exp?.ToString();
			}
			string GetSwitchSignature(SwitchSectionSyntax syntax) {
				var label = (syntax as SwitchSectionSyntax).Labels.LastOrDefault();
				return label is DefaultSwitchLabelSyntax ? "default"
					: (label as CaseSwitchLabelSyntax)?.Value.ToString();
			}
			string GetUsingSignature(UsingStatementSyntax syntax) {
				return syntax.Declaration?.Variables.FirstOrDefault()?.Identifier.Text
					?? syntax.GetFirstIdentifier()?.Identifier.Text;
			}
			string GetGenericSignature(string name, int arity) {
				return arity > 0 ? name + "<" + new string(',', arity - 1) + ">" : name;
			}
			string GetVariableSignature(VariableDeclarationSyntax syntax, int pos) {
				if (syntax == null) {
					return String.Empty;
				}
				var vars = syntax.Variables;
				if (vars.Count == 0) {
					return String.Empty;
				}
				if (pos > 0) {
					foreach (var item in vars) {
						if (item.FullSpan.Contains(pos)) {
							return item.Identifier.Text;
						}
					}
				}
				return vars.Count > 1 ? vars[0].Identifier.Text + "..." : vars[0].Identifier.Text;
			}
		}

		public static IEnumerable<SyntaxNode> GetDecendantDeclarations(this SyntaxNode root) {
			foreach (var child in root.ChildNodes()) {
				switch (child.Kind()) {
					case SyntaxKind.CompilationUnit:
					case SyntaxKind.NamespaceDeclaration:
						foreach (var item in child.GetDecendantDeclarations()) {
							yield return item;
						}
						break;
					case SyntaxKind.ClassDeclaration:
					case SyntaxKind.DelegateDeclaration:
					case SyntaxKind.EnumDeclaration:
					case SyntaxKind.EventDeclaration:
					case SyntaxKind.InterfaceDeclaration:
					case SyntaxKind.StructDeclaration:
						yield return child;
						goto case SyntaxKind.CompilationUnit;
					case SyntaxKind.MethodDeclaration:
					case SyntaxKind.ConstructorDeclaration:
					case SyntaxKind.DestructorDeclaration:
					case SyntaxKind.PropertyDeclaration:
					case SyntaxKind.IndexerDeclaration:
					case SyntaxKind.OperatorDeclaration:
					case SyntaxKind.ConversionOperatorDeclaration:
					case SyntaxKind.EnumMemberDeclaration:
						yield return child;
						break;
					case SyntaxKind.FieldDeclaration:
						foreach (var field in (child as FieldDeclarationSyntax).Declaration.Variables) {
							yield return field;
						}
						break;
					case SyntaxKind.EventFieldDeclaration:
						foreach (var ev in (child as EventFieldDeclarationSyntax).Declaration.Variables) {
							yield return ev;
						}
						break;
				}
			}
		}

		public static IdentifierNameSyntax GetFirstIdentifier(this SyntaxNode node) {
			return node.DescendantNodes().FirstOrDefault(i => i.IsKind(SyntaxKind.IdentifierName)) as IdentifierNameSyntax;
		}
		public static IdentifierNameSyntax GetLastIdentifier(this SyntaxNode node) {
			return node.DescendantNodes().LastOrDefault(i => i.IsKind(SyntaxKind.IdentifierName)) as IdentifierNameSyntax;
		}

		public static bool IsLineComment(this SyntaxTrivia trivia) {
			switch (trivia.Kind()) {
				case SyntaxKind.MultiLineCommentTrivia:
				case SyntaxKind.SingleLineCommentTrivia:
					return true;
			}
			return false;
		}

		public static bool IsType(this CodeMemberType type) {
			return type > CodeMemberType.Root && type < CodeMemberType.Member;
		}

		public static bool IsMember(this CodeMemberType type) {
			return type > CodeMemberType.Member && type < CodeMemberType.Other;
		}
	}
}
