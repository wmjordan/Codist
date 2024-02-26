using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using CLR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Codist
{
	partial class CodeAnalysisHelper
	{
		#region Node info
		public static bool IsAnyKind(this SyntaxNode node, SyntaxKind kind1, SyntaxKind kind2) {
			return Op.Cast<int, SyntaxKind>(node.RawKind).CeqAny(kind1, kind2);
		}
		public static bool IsAnyKind(this SyntaxNode node, SyntaxKind kind1, SyntaxKind kind2, SyntaxKind kind3) {
			return Op.Cast<int, SyntaxKind>(node.RawKind).CeqAny(kind1, kind2, kind3);
		}
		public static bool IsAnyKind(this SyntaxNode node, SyntaxKind kind1, SyntaxKind kind2, SyntaxKind kind3, SyntaxKind kind4) {
			return Op.Cast<int, SyntaxKind>(node.RawKind).CeqAny(kind1, kind2, kind3, kind4);
		}
		public static bool IsAnyKind(this SyntaxToken token, SyntaxKind kind1, SyntaxKind kind2) {
			return Op.Cast<int, SyntaxKind>(token.RawKind).CeqAny(kind1, kind2);
		}
		public static bool IsAnyKind(this SyntaxToken token, SyntaxKind kind1, SyntaxKind kind2, SyntaxKind kind3) {
			return Op.Cast<int, SyntaxKind>(token.RawKind).CeqAny(kind1, kind2, kind3);
		}
		public static bool IsAnyKind(this SyntaxToken token, SyntaxKind kind1, SyntaxKind kind2, SyntaxKind kind3, SyntaxKind kind4) {
			return Op.Cast<int, SyntaxKind>(token.RawKind).CeqAny(kind1, kind2, kind3, kind4);
		}
		public static bool IsPredefinedSystemType(this SyntaxKind kind) {
			return kind >= SyntaxKind.BoolKeyword && kind <= SyntaxKind.ObjectKeyword;
		}
		public static bool IsDeclaration(this SyntaxKind kind) {
			switch (kind) {
				case SyntaxKind.ClassDeclaration:
				case RecordDeclaration:
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
				case FileScopedNamespaceDeclaration:
				case SyntaxKind.OperatorDeclaration:
				case SyntaxKind.PropertyDeclaration:
				case SyntaxKind.StructDeclaration:
				case RecordStructDeclaration:
				case SyntaxKind.VariableDeclaration:
				case SyntaxKind.LocalFunctionStatement:
				case SyntaxKind.SingleVariableDesignation:
					//case SyntaxKind.VariableDeclarator:
					return true;
			}
			return false;
		}

		static DeclarationCategory GetDeclarationCategory(this SyntaxKind kind) {
			switch (kind) {
				case SyntaxKind.ClassDeclaration:
				case RecordDeclaration:
				case SyntaxKind.DelegateDeclaration:
				case SyntaxKind.EnumDeclaration:
				case SyntaxKind.EventDeclaration:
				case SyntaxKind.InterfaceDeclaration:
				case SyntaxKind.StructDeclaration:
				case RecordStructDeclaration:
					return DeclarationCategory.Type;
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
					return DeclarationCategory.Member;
				case SyntaxKind.NamespaceDeclaration:
				case FileScopedNamespaceDeclaration:
					return DeclarationCategory.Namespace;
				case SyntaxKind.LocalDeclarationStatement:
				case SyntaxKind.VariableDeclaration:
				case SyntaxKind.LocalFunctionStatement:
				case SyntaxKind.VariableDeclarator:
					return DeclarationCategory.Local;
			}
			return DeclarationCategory.None;
		}

		public static bool IsTypeOrNamespaceDeclaration(this SyntaxKind kind) {
			switch (kind) {
				case SyntaxKind.ClassDeclaration:
				case SyntaxKind.DelegateDeclaration:
				case SyntaxKind.EnumDeclaration:
				case SyntaxKind.EventDeclaration:
				case SyntaxKind.InterfaceDeclaration:
				case SyntaxKind.StructDeclaration:
				case SyntaxKind.NamespaceDeclaration:
				case RecordDeclaration:
				case RecordStructDeclaration:
				case FileScopedNamespaceDeclaration:
					return true;
			}
			return false;
		}
		public static bool IsTypeDeclaration(this SyntaxKind kind) {
			switch (kind) {
				case SyntaxKind.ClassDeclaration:
				case SyntaxKind.DelegateDeclaration:
				case SyntaxKind.EnumDeclaration:
				case SyntaxKind.EventDeclaration:
				case SyntaxKind.InterfaceDeclaration:
				case SyntaxKind.StructDeclaration:
				case RecordDeclaration:
				case RecordStructDeclaration:
					return true;
			}
			return false;
		}
		public static bool IsNonDelegateTypeDeclaration(this SyntaxKind kind) {
			switch (kind) {
				case SyntaxKind.ClassDeclaration:
				case SyntaxKind.EnumDeclaration:
				case SyntaxKind.EventDeclaration:
				case SyntaxKind.InterfaceDeclaration:
				case SyntaxKind.StructDeclaration:
				case RecordDeclaration:
				case RecordStructDeclaration:
					return true;
			}
			return false;
		}
		public static bool IsNamespaceDeclaration(this SyntaxKind kind) {
			switch (kind) {
				case SyntaxKind.NamespaceDeclaration:
				case FileScopedNamespaceDeclaration:
					return true;
			}
			return false;
		}
		public static bool IsMemberDeclaration(this SyntaxKind kind) {
			switch (kind) {
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
		public static bool IsMethodDeclaration(this SyntaxKind kind) {
			switch (kind) {
				case SyntaxKind.MethodDeclaration:
				case SyntaxKind.ConstructorDeclaration:
				case SyntaxKind.LocalFunctionStatement:
				case SyntaxKind.SimpleLambdaExpression:
				case SyntaxKind.ParenthesizedLambdaExpression:
				case SyntaxKind.DestructorDeclaration:
				case SyntaxKind.OperatorDeclaration:
				case SyntaxKind.ConversionOperatorDeclaration:
					return true;
			}
			return false;
		}

		public static bool IsSyntaxBlock(this SyntaxKind kind) {
			switch (kind) {
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
				case SyntaxKind.YieldReturnStatement:
				case SyntaxKind.ExpressionStatement:
				case SyntaxKind.GotoStatement:
				case SyntaxKind.GotoCaseStatement:
				case SyntaxKind.GotoDefaultStatement:
				case SyntaxKind.XmlElement:
				case SyntaxKind.XmlEmptyElement:
				case SwitchExpression:
				case SyntaxKind.XmlComment:
					return true;
			}
			return false;
		}

		public static bool HasCapturedVariable(this SyntaxNode node, SemanticModel semanticModel) {
			return semanticModel.GetCapturedVariables(node).Length != 0;
		}

		public static string GetName(this NameSyntax name) {
			if (name == null) {
				return null;
			}
			switch (name.Kind()) {
				case SyntaxKind.IdentifierName:
				case SyntaxKind.GenericName: return ((SimpleNameSyntax)name).Identifier.Text;
				case SyntaxKind.QualifiedName: return ((QualifiedNameSyntax)name).Right.Identifier.Text;
				case SyntaxKind.AliasQualifiedName: return ((AliasQualifiedNameSyntax)name).Name.Identifier.Text;
			}
			return name.ToString();
		}

		public static string GetFullName(this NameSyntax name) {
			if (name is null) {
				return null;
			}
			LinkedList<string> nb = null;
			string t;
			do {
				switch (name.Kind()) {
					case SyntaxKind.IdentifierName:
					case SyntaxKind.GenericName:
						t = ((SimpleNameSyntax)name).Identifier.Text;
						if (nb == null) {
							return t;
						}
						nb.AddFirst(t);
						name = null;
						break;
					case SyntaxKind.QualifiedName:
						var qn = (QualifiedNameSyntax)name;
						(nb ?? (nb = new LinkedList<string>())).AddFirst(qn.Right.Identifier.Text);
						name = qn.Left;
						break;
					case SyntaxKind.AliasQualifiedName:
						var aqn = (AliasQualifiedNameSyntax)name;
						return $"{aqn.Alias.Identifier.Text}.{aqn.Name.Identifier.Text}";
				}
			} while (name != null);
			return nb != null
				? String.Join(".", nb)
				: null;
		}

		public static IdentifierNameSyntax GetFirstIdentifier(this SyntaxNode node) {
			return node.DescendantNodes().FirstOrDefault(i => i.IsKind(SyntaxKind.IdentifierName)) as IdentifierNameSyntax;
		}
		public static IdentifierNameSyntax GetLastIdentifier(this SyntaxNode node) {
			return node.DescendantNodes().LastOrDefault(i => i.IsKind(SyntaxKind.IdentifierName)) as IdentifierNameSyntax;
		}
		public static SyntaxToken GetIdentifierToken(this SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.ClassDeclaration:
				case SyntaxKind.StructDeclaration:
				case SyntaxKind.InterfaceDeclaration:
				case SyntaxKind.EnumDeclaration:
				case RecordDeclaration:
				case RecordStructDeclaration:
					return ((BaseTypeDeclarationSyntax)node).Identifier;
				case SyntaxKind.DelegateDeclaration: return ((DelegateDeclarationSyntax)node).Identifier;
				case SyntaxKind.MethodDeclaration: return ((MethodDeclarationSyntax)node).Identifier;
				case SyntaxKind.OperatorDeclaration: return ((OperatorDeclarationSyntax)node).OperatorToken;
				case SyntaxKind.ConversionOperatorDeclaration: return ((ConversionOperatorDeclarationSyntax)node).Type.GetFirstToken();
				case SyntaxKind.ConstructorDeclaration: return ((ConstructorDeclarationSyntax)node).Identifier;
				case SyntaxKind.DestructorDeclaration: return ((DestructorDeclarationSyntax)node).Identifier;
				case SyntaxKind.PropertyDeclaration: return ((PropertyDeclarationSyntax)node).Identifier;
				case SyntaxKind.IndexerDeclaration: return ((IndexerDeclarationSyntax)node).ThisKeyword;
				case SyntaxKind.EventDeclaration: return ((EventDeclarationSyntax)node).Identifier;
				case SyntaxKind.EnumMemberDeclaration: return ((EnumMemberDeclarationSyntax)node).Identifier;
				case SyntaxKind.VariableDeclarator: return ((VariableDeclaratorSyntax)node).Identifier;
				case SyntaxKind.LetClause: return ((LetClauseSyntax)node).Identifier;
				case SyntaxKind.JoinClause: return ((JoinClauseSyntax)node).Identifier;
				case SyntaxKind.JoinIntoClause: return ((JoinIntoClauseSyntax)node).Identifier;
			}
			return node.GetFirstToken();
		}

		public static string GetSyntaxBrief(this SyntaxKind kind) {
			switch (kind) {
				case SyntaxKind.ClassDeclaration: return "class";
				case RecordDeclaration: return "record";
				case SyntaxKind.EnumDeclaration: return "enum";
				case SyntaxKind.StructDeclaration: return "struct";
				case RecordStructDeclaration: return "record struct";
				case SyntaxKind.InterfaceDeclaration: return "interface";
				case SyntaxKind.ConstructorDeclaration: return "constructor";
				case SyntaxKind.ConversionOperatorDeclaration: return "conversion operator";
				case SyntaxKind.DestructorDeclaration: return "destructor";
				case SyntaxKind.IndexerDeclaration: return "property";
				case SyntaxKind.MethodDeclaration: return "method";
				case SyntaxKind.OperatorDeclaration: return "operator";
				case SyntaxKind.PropertyDeclaration: return "property";
				case SyntaxKind.FieldDeclaration: return "field";
				case SyntaxKind.NamespaceDeclaration:
				case FileScopedNamespaceDeclaration: return "namespace";
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
				case SyntaxKind.SwitchStatement:
				case SwitchExpression: return "switch";
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
				case SyntaxKind.GotoStatement:
				case SyntaxKind.GotoCaseStatement:
				case SyntaxKind.GotoDefaultStatement: return "goto";
				case SyntaxKind.XmlElement:
				case SyntaxKind.XmlEmptyElement: return "xml element";
				case SyntaxKind.XmlComment: return "xml comment";
				case SyntaxKind.LocalFunctionStatement: return "local function";
				case SyntaxKind.RegionDirectiveTrivia: return "region";
			}
			return null;
		}

		public static TypeSyntax GetMemberDeclarationType(this SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.MethodDeclaration: return ((MethodDeclarationSyntax)node).ReturnType;
				case SyntaxKind.DelegateDeclaration: return ((DelegateDeclarationSyntax)node).ReturnType;
				case SyntaxKind.OperatorDeclaration: return ((OperatorDeclarationSyntax)node).ReturnType;
				case SyntaxKind.ConversionOperatorDeclaration: return ((ConversionOperatorDeclarationSyntax)node).Type;
				case SyntaxKind.PropertyDeclaration:
				case SyntaxKind.IndexerDeclaration:
					return ((BasePropertyDeclarationSyntax)node).Type;
				case SyntaxKind.FieldDeclaration: return ((FieldDeclarationSyntax)node).Declaration.Type;
				case SyntaxKind.VariableDeclaration: return ((VariableDeclarationSyntax)node).Type;
				case SyntaxKind.VariableDeclarator: return ((VariableDeclarationSyntax)node.Parent).Type;
			}
			return null;
		}

		public static NameSyntax GetFileScopedNamespaceDeclarationName(this SyntaxNode node) {
			return node.IsKind(FileScopedNamespaceDeclaration) ? NonPublicOrFutureAccessors.GetFileScopedNamespaceName(node) : null;
		}

		public static bool IsTopmostIf(this IfStatementSyntax ifs) {
			return ifs?.Parent.IsKind(SyntaxKind.ElseClause) != true;
		}

		/// <summary>
		/// Returns whether a <see cref="SyntaxNode"/> spans multiple lines.
		/// </summary>
		public static bool IsMultiLine(this SyntaxNode node, bool includeTrivia) {
			var lines = node.SyntaxTree.GetText().Lines;
			var span = includeTrivia ? node.FullSpan : node.Span;
			return lines.GetLineFromPosition(span.Start).SpanIncludingLineBreak.Contains(span.End) == false;
		}
		/// <summary>
		/// Returns whether a <see cref="SyntaxTriviaList"/> spans multiple lines.
		/// </summary>
		public static bool IsMultiline(this SyntaxTriviaList triviaList) {
			foreach (var item in triviaList) {
				if (item.IsKind(SyntaxKind.EndOfLineTrivia)) {
					return true;
				}
			}
			return false;
		}

		public static bool IsAssignedToSameTarget(this StatementSyntax statement, StatementSyntax other) {
			return statement is ExpressionStatementSyntax e
				&& e.Expression is AssignmentExpressionSyntax a
				&& other is ExpressionStatementSyntax ee
				&& ee.Expression is AssignmentExpressionSyntax ea
				&& a.OperatorToken.IsKind(ea.OperatorToken.Kind())
				&& SyntaxFactory.AreEquivalent(a.Left, ea.Left);
		}

		public static SyntaxNodeOrToken GetFollowingNodeOrToken(this SyntaxNode node) {
			var foundCurrentNode = false;
			foreach (var item in node.Parent.ChildNodesAndTokens()) {
				if (foundCurrentNode) {
					return item;
				}
				if (item == node) {
					foundCurrentNode = true;
				}
			}
			return default;
		}

		public static string GetQualifiedSignature(this SyntaxNode node) {
			var s = new LinkedList<string>(); // signature parts
			DeclarationCategory c;
			while ((c = node.Kind().GetDeclarationCategory()) != DeclarationCategory.None) {
				if (c == DeclarationCategory.Namespace) {
					s.AddFirst((node.GetFileScopedNamespaceDeclarationName() ?? ((NamespaceDeclarationSyntax)node).Name).GetFullName());
				}
				else if (c == DeclarationCategory.Local) {
					if (node.Parent.IsKind(SyntaxKind.VariableDeclaration)) {
						s.AddFirst(node.GetDeclarationSignature());
						node = node.Parent.Parent;
						if (node.IsAnyKind(SyntaxKind.FieldDeclaration, SyntaxKind.EventFieldDeclaration) == false) {
							break;
						}
					}
					else {
						break;
					}
				}
				else {
					s.AddFirst(node.GetDeclarationSignature());
				}
				node = node.Parent;
			}
			return String.Join(".", s);
		}

		public static SyntaxNode UnqualifyExceptNamespace(this SyntaxNode node) {
			if (node.IsKind(SyntaxKind.QualifiedName)) {
				var n = node;
				while ((n = n.Parent).IsKind(SyntaxKind.QualifiedName)) {
				}
				return n.IsAnyKind(SyntaxKind.UsingDirective, SyntaxKind.NamespaceDeclaration) ? node : n;
			}
			return node;
		}
		#endregion

		#region Node signature

		public static bool MatchSignature(this MemberDeclarationSyntax node, SyntaxNode other) {
			var k1 = node.Kind();
			var k2 = other.Kind();
			if (k1 != k2) {
				return false;
			}
			switch (k1) {
				case SyntaxKind.NamespaceDeclaration:
					return ((NamespaceDeclarationSyntax)node).Name.ToString() == ((NamespaceDeclarationSyntax)other).Name.ToString();
				case FileScopedNamespaceDeclaration:
					return NonPublicOrFutureAccessors.GetFileScopedNamespaceName(node).ToString() == NonPublicOrFutureAccessors.GetFileScopedNamespaceName(other).ToString();
				case SyntaxKind.ClassDeclaration:
				case SyntaxKind.StructDeclaration:
				case SyntaxKind.InterfaceDeclaration:
				case RecordDeclaration:
				case RecordStructDeclaration:
					var t1 = (TypeDeclarationSyntax)node;
					var t2 = (TypeDeclarationSyntax)other;
					return t1.Arity == t2.Arity && t1.Identifier.Text == t2.Identifier.Text;
				case SyntaxKind.EnumDeclaration:
					return ((EnumDeclarationSyntax)node).Identifier.Text == ((EnumDeclarationSyntax)other).Identifier.Text;
				case SyntaxKind.ConstructorDeclaration:
					return ((ConstructorDeclarationSyntax)node).Identifier.Text == ((ConstructorDeclarationSyntax)other).Identifier.Text && MatchParameterList(((ConstructorDeclarationSyntax)node).ParameterList, ((ConstructorDeclarationSyntax)other).ParameterList);
				case SyntaxKind.DestructorDeclaration:
					return ((DestructorDeclarationSyntax)node).Identifier.Text == ((DestructorDeclarationSyntax)other).Identifier.Text;
				case SyntaxKind.MethodDeclaration:
					var m1 = (MethodDeclarationSyntax)node;
					var m2 = (MethodDeclarationSyntax)other;
					return m1.Arity == m2.Arity && m1.Identifier.Text == m2.Identifier.Text && MatchExplicitInterfaceSpecifier(m1.ExplicitInterfaceSpecifier, m2.ExplicitInterfaceSpecifier);
				case SyntaxKind.PropertyDeclaration:
					var p1 = (PropertyDeclarationSyntax)node;
					var p2 = (PropertyDeclarationSyntax)other;
					return p1.Identifier.Text == p2.Identifier.Text && MatchExplicitInterfaceSpecifier(p1.ExplicitInterfaceSpecifier, p2.ExplicitInterfaceSpecifier);
			}
			return false;
		}

		public static bool MatchAncestorDeclaration(this MemberDeclarationSyntax node, SyntaxNode other) {
			node = node.Parent as MemberDeclarationSyntax;
			other = other.Parent as MemberDeclarationSyntax;
			while (node != null && other != null) {
				if (MatchSignature(node, other) == false) {
					return false;
				}
				node = node.Parent as MemberDeclarationSyntax;
				other = other.Parent as MemberDeclarationSyntax;
			}
			return node == other; // both null
		}

		public static bool MatchExplicitInterfaceSpecifier(ExplicitInterfaceSpecifierSyntax x, ExplicitInterfaceSpecifierSyntax y) {
			return x == y
				|| x != null && y != null && x.Name.GetName() == y.Name.GetName();
		}

		static bool MatchParameterList(ParameterListSyntax x, ParameterListSyntax y) {
			var xp = x.Parameters;
			var yp = y.Parameters;
			if (xp.Count != yp.Count) {
				return false;
			}
			for (int i = xp.Count - 1; i >= 0; i--) {
				if (xp[i].Type.ToString() != yp[i].Type.ToString()) {
					return false;
				}
			}
			return true;
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

		public static SyntaxList<AttributeListSyntax> GetAttributes(this MemberDeclarationSyntax declaration, out bool canHaveAttributes) {
			canHaveAttributes = true;
			if (declaration is BaseTypeDeclarationSyntax t) {
				return t.AttributeLists;
			}
			if (declaration is BaseMethodDeclarationSyntax m) {
				return m.AttributeLists;
			}
			if (declaration is BaseFieldDeclarationSyntax f) {
				return f.AttributeLists;
			}
			if (declaration is BasePropertyDeclarationSyntax p) {
				return p.AttributeLists;
			}
			if (declaration is DelegateDeclarationSyntax d) {
				return d.AttributeLists;
			}
			canHaveAttributes = false;
			return default;
		}

		public static SyntaxTokenList GetModifiers(this MemberDeclarationSyntax declaration, out bool canHaveModifier) {
			canHaveModifier = true;
			if (declaration is BaseTypeDeclarationSyntax t) {
				return t.Modifiers;
			}
			if (declaration is BaseMethodDeclarationSyntax m) {
				return m.Modifiers;
			}
			if (declaration is BaseFieldDeclarationSyntax f) {
				return f.Modifiers;
			}
			if (declaration is BasePropertyDeclarationSyntax p) {
				return p.Modifiers;
			}
			if (declaration is DelegateDeclarationSyntax d) {
				return d.Modifiers;
			}
			canHaveModifier = false;
			return default;
		}


		public static string GetDeclarationSignature(this SyntaxNode node, int position = 0) {
			switch (node.Kind()) {
				case SyntaxKind.ClassDeclaration:
				case SyntaxKind.StructDeclaration:
				case SyntaxKind.InterfaceDeclaration:
				case RecordDeclaration:
				case RecordStructDeclaration:
					return GetTypeSignature((TypeDeclarationSyntax)node);
				case SyntaxKind.EnumDeclaration: return ((EnumDeclarationSyntax)node).Identifier.Text;
				case SyntaxKind.MethodDeclaration: return GetMethodSignature((MethodDeclarationSyntax)node);
				case SyntaxKind.ArgumentList: return GetArgumentListSignature((ArgumentListSyntax)node);
				case SyntaxKind.ConstructorDeclaration: return ((ConstructorDeclarationSyntax)node).Identifier.Text;
				case SyntaxKind.ConversionOperatorDeclaration: return GetConversionSignature((ConversionOperatorDeclarationSyntax)node);
				case SyntaxKind.DelegateDeclaration: return GetDelegateSignature((DelegateDeclarationSyntax)node);
				case SyntaxKind.EventDeclaration: return ((EventDeclarationSyntax)node).Identifier.Text;
				case SyntaxKind.EventFieldDeclaration: return GetVariableSignature(((EventFieldDeclarationSyntax)node).Declaration, position);
				case SyntaxKind.FieldDeclaration: return GetVariableSignature(((FieldDeclarationSyntax)node).Declaration, position);
				case SyntaxKind.DestructorDeclaration: return ((DestructorDeclarationSyntax)node).Identifier.Text;
				case SyntaxKind.IndexerDeclaration: return "this";
				case SyntaxKind.OperatorDeclaration: return ((OperatorDeclarationSyntax)node).OperatorToken.Text;
				case SyntaxKind.PropertyDeclaration: return ((PropertyDeclarationSyntax)node).Identifier.Text;
				case SyntaxKind.EnumMemberDeclaration: return ((EnumMemberDeclarationSyntax)node).Identifier.Text;
				case SyntaxKind.SimpleLambdaExpression: return "(" + ((SimpleLambdaExpressionSyntax)node).Parameter.ToString() + ")";
				case SyntaxKind.ParenthesizedLambdaExpression: return ((ParenthesizedLambdaExpressionSyntax)node).ParameterList.ToString();
				case SyntaxKind.LocalFunctionStatement: return ((LocalFunctionStatementSyntax)node).Identifier.Text;
				case SyntaxKind.NamespaceDeclaration: return (node as NamespaceDeclarationSyntax).Name.GetName();
				case FileScopedNamespaceDeclaration: return NonPublicOrFutureAccessors.GetFileScopedNamespaceName(node).GetName();
				case SyntaxKind.VariableDeclarator: return ((VariableDeclaratorSyntax)node).Identifier.Text;
				case SyntaxKind.LocalDeclarationStatement: return GetVariableSignature(((LocalDeclarationStatementSyntax)node).Declaration, position);
				case SyntaxKind.VariableDeclaration: return GetVariableSignature((VariableDeclarationSyntax)node, position);
				case SyntaxKind.ForEachStatement: return ((ForEachStatementSyntax)node).Identifier.Text;
				case SyntaxKind.ForStatement: return GetVariableSignature(((ForStatementSyntax)node).Declaration, position);
				case SyntaxKind.IfStatement: return ((IfStatementSyntax)node).Condition.GetExpressionSignature();
				case SyntaxKind.SwitchSection: return GetSwitchSignature((SwitchSectionSyntax)node);
				case SyntaxKind.SwitchStatement: return ((SwitchStatementSyntax)node).Expression.GetExpressionSignature();
				case SwitchExpression: return (node.ChildNodes().FirstOrDefault() as ExpressionSyntax).GetExpressionSignature();
				case SyntaxKind.WhileStatement: return ((WhileStatementSyntax)node).Condition.GetExpressionSignature();
				case SyntaxKind.UsingStatement: return GetUsingSignature((UsingStatementSyntax)node);
				case SyntaxKind.LockStatement: return ((LockStatementSyntax)node).Expression.GetExpressionSignature();
				case SyntaxKind.DoStatement: return ((DoStatementSyntax)node).Condition.GetExpressionSignature();
				case SyntaxKind.TryStatement: return ((TryStatementSyntax)node).Catches.FirstOrDefault()?.Declaration?.Type.ToString();
				case SyntaxKind.UncheckedStatement:
				case SyntaxKind.CheckedStatement: return ((CheckedStatementSyntax)node).Keyword.Text;
				case SyntaxKind.ReturnStatement: return ((ReturnStatementSyntax)node).Expression?.GetExpressionSignature();
				case SyntaxKind.ParenthesizedExpression: return ((ParenthesizedExpressionSyntax)node).Expression.GetExpressionSignature();
				case SyntaxKind.ExpressionStatement: return ((ExpressionStatementSyntax)node).Expression.GetExpressionSignature();
				case SyntaxKind.Attribute: return ((AttributeSyntax)node).Name.ToString();
				case SyntaxKind.AttributeArgumentList: return GetAttributeArgumentListSignature((AttributeArgumentListSyntax)node);
				case SyntaxKind.YieldReturnStatement:
					return ((YieldStatementSyntax)node).Expression?.GetExpressionSignature();
				case SyntaxKind.GotoStatement:
				case SyntaxKind.GotoCaseStatement:
					return ((GotoStatementSyntax)node).Expression?.GetExpressionSignature();
				case SyntaxKind.GotoDefaultStatement:
					return "(default)";
				case SyntaxKind.RegionDirectiveTrivia:
					return GetRegionSignature((RegionDirectiveTriviaSyntax)node);
				case SyntaxKind.EndRegionDirectiveTrivia:
					return GetEndRegionSignature((EndRegionDirectiveTriviaSyntax)node);
				case SyntaxKind.IfDirectiveTrivia:
					return GetIfSignature((IfDirectiveTriviaSyntax)node);
				case SyntaxKind.EndIfDirectiveTrivia:
					return GetEndIfSignature((EndIfDirectiveTriviaSyntax)node);
				case SyntaxKind.CaseSwitchLabel:
					return GetSwitchLabelSignature((CaseSwitchLabelSyntax)node);
				case SyntaxKind.DefaultSwitchLabel:
					return "default";
			}
			return null;

			string GetTypeSignature(TypeDeclarationSyntax syntax) => GetGenericSignature(syntax.Identifier.Text, syntax.Arity);
			string GetMethodSignature(MethodDeclarationSyntax syntax) => GetGenericSignature(syntax.Identifier.Text, syntax.Arity);
			string GetDelegateSignature(DelegateDeclarationSyntax syntax) => GetGenericSignature(syntax.Identifier.Text, syntax.Arity);
			string GetArgumentListSignature(ArgumentListSyntax syntax) {
				var exp = syntax.Parent;
				var ie = (exp as InvocationExpressionSyntax)?.Expression;
				if (ie != null) {
					return ((ie as MemberAccessExpressionSyntax)?.Name
						?? (ie as NameSyntax)
						?? (ie as MemberBindingExpressionSyntax)?.Name).GetName();
				}
				exp = (exp as ObjectCreationExpressionSyntax)?.Type;
				return (exp as NameSyntax)?.GetName() ?? exp?.ToString();
			}
			string GetAttributeArgumentListSignature(AttributeArgumentListSyntax syntax) {
				return (syntax.Parent as AttributeSyntax)?.Name.GetName();
			}
			string GetConversionSignature(ConversionOperatorDeclarationSyntax syntax) {
				return syntax.ImplicitOrExplicitKeyword.Text + " " + ((syntax.Type as NameSyntax)?.GetName() ?? syntax.Type.ToString());
			}
			string GetSwitchSignature(SwitchSectionSyntax syntax) {
				var label = syntax.Labels.LastOrDefault();
				return label is DefaultSwitchLabelSyntax ? "default"
					: (label as CaseSwitchLabelSyntax)?.Value.ToString();
			}
			string GetUsingSignature(UsingStatementSyntax syntax) {
				return syntax.Declaration?.Variables.FirstOrDefault()?.Identifier.Text
					?? syntax.GetFirstIdentifier()?.Identifier.Text;
			}
			string GetGenericSignature(string name, int arity) {
				return arity > 0 ? name + GetGenericAritySignature(arity) : name;
			}
			string GetGenericAritySignature(int arity) {
				switch (arity) {
					case 0:
					case 1: return "<>";
					case 2: return "<,>";
					case 3: return "<,,>";
					case 4: return "<,,,>";
					case 5: return "<,,,,>";
					case 6: return "<,,,,,>";
					case 7: return "<,,,,,,>";
					default: return "<" + new string(',', arity - 1) + ">";
				}
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
			string GetRegionSignature(RegionDirectiveTriviaSyntax syntax) {
				var e = syntax.EndOfDirectiveToken;
				return e.HasLeadingTrivia ? e.LeadingTrivia[0].ToString() : String.Empty;
			}
			string GetEndRegionSignature(EndRegionDirectiveTriviaSyntax syntax) {
				var e = syntax.GetPrecedingDirective<RegionDirectiveTriviaSyntax, EndRegionDirectiveTriviaSyntax>(SyntaxKind.RegionDirectiveTrivia, SyntaxKind.EndRegionDirectiveTrivia);
				return e != null ? GetRegionSignature(e) : String.Empty;
			}
			string GetIfSignature(IfDirectiveTriviaSyntax syntax) {
				return syntax.Condition?.ToString() ?? String.Empty;
			}
			string GetEndIfSignature(EndIfDirectiveTriviaSyntax syntax) {
				var e = syntax.GetPrecedingDirective<IfDirectiveTriviaSyntax, EndIfDirectiveTriviaSyntax>(SyntaxKind.IfDirectiveTrivia, SyntaxKind.EndIfDirectiveTrivia);
				return e != null ? GetIfSignature(e) : String.Empty;
			}
			string GetSwitchLabelSignature(CaseSwitchLabelSyntax syntax) {
				var e = syntax.Value;
				if (e.IsKind(SyntaxKind.SimpleMemberAccessExpression)) {
					return e.GetLastIdentifier().ToString();
				}
				return e.ToString();
			}
		}

		public static string GetParameterListSignature(this BaseParameterListSyntax parameters, bool useParamName) {
			if (parameters.Parameters.Count == 0) {
				return "()";
			}
			var isIndexer = parameters.Parent.IsKind(SyntaxKind.IndexerDeclaration);
			using (var r = ReusableStringBuilder.AcquireDefault(30)) {
				var sb = r.Resource;
				sb.Append(isIndexer ? '[' : '(');
				foreach (var item in parameters.Parameters) {
					if (sb.Length > 1) {
						sb.Append(',');
					}
					if (item.Default != null) {
						sb.Append('[');
					}
					foreach (var token in item.Modifiers) {
						switch (token.Kind()) {
							case SyntaxKind.OutKeyword: sb.Append("out "); break;
							case SyntaxKind.RefKeyword: sb.Append("ref "); break;
							case SyntaxKind.InKeyword: sb.Append("in "); break;
							case SyntaxKind.ParamsKeyword: sb.Append("params "); break;
						}
					}
					sb.Append(useParamName ? item.Identifier.Text : item.Type.ToString());
					if (item.Default != null) {
						sb.Append(']');
					}
				}
				sb.Append(isIndexer ? ']' : ')');
				return sb.ToString();
			}
		}

		public static string GetExpressionSignature(this ExpressionSyntax expression) {
			switch (expression.Kind()) {
				case SyntaxKind.SimpleAssignmentExpression:
				case SyntaxKind.AddAssignmentExpression:
				case SyntaxKind.AndAssignmentExpression:
				case SyntaxKind.DivideAssignmentExpression:
				case SyntaxKind.ExclusiveOrAssignmentExpression:
				case SyntaxKind.LeftShiftAssignmentExpression:
				case SyntaxKind.ModuloAssignmentExpression:
				case SyntaxKind.MultiplyAssignmentExpression:
				case SyntaxKind.OrAssignmentExpression:
				case SyntaxKind.RightShiftAssignmentExpression:
				case SyntaxKind.SubtractAssignmentExpression:
					return (expression as AssignmentExpressionSyntax).Left.GetExpressionSignature();
				case SyntaxKind.GreaterThanExpression:
				case SyntaxKind.GreaterThanOrEqualExpression:
				case SyntaxKind.LessThanExpression:
				case SyntaxKind.LessThanOrEqualExpression:
				case SyntaxKind.EqualsExpression:
				case SyntaxKind.NotEqualsExpression:
				case SyntaxKind.CoalesceExpression:
				case SyntaxKind.AsExpression:
				case SyntaxKind.IsExpression:
				case SyntaxKind.LogicalAndExpression:
				case SyntaxKind.LogicalOrExpression:
				case SyntaxKind.BitwiseAndExpression:
				case SyntaxKind.BitwiseOrExpression:
					return (expression as BinaryExpressionSyntax).Left.GetExpressionSignature();
				case SyntaxKind.PreIncrementExpression:
				case SyntaxKind.PreDecrementExpression:
				case SyntaxKind.LogicalNotExpression:
				case SyntaxKind.BitwiseNotExpression:
					return (expression as PrefixUnaryExpressionSyntax).Operand.ToString();
				case SyntaxKind.PostIncrementExpression:
				case SyntaxKind.PostDecrementExpression:
					return (expression as PostfixUnaryExpressionSyntax).Operand.ToString();
				case SyntaxKind.ObjectCreationExpression:
					return (expression as ObjectCreationExpressionSyntax).Type.GetExpressionSignature();
				case SyntaxKind.TypeOfExpression:
					return (expression as TypeOfExpressionSyntax).Type.GetExpressionSignature();
				case SyntaxKind.IdentifierName:
					return (expression as IdentifierNameSyntax).Identifier.Text;
				case SyntaxKind.QualifiedName:
					return (expression as QualifiedNameSyntax).Right.Identifier.Text;
				case SyntaxKind.AliasQualifiedName:
					return (expression as AliasQualifiedNameSyntax).Name.Identifier.Text;
				case SyntaxKind.SimpleMemberAccessExpression:
					return (expression as MemberAccessExpressionSyntax).Name.Identifier.Text;
				case SyntaxKind.PointerMemberAccessExpression:
					return ((MemberAccessExpressionSyntax)expression).Name.Identifier.Text;
				case SyntaxKind.MemberBindingExpression:
					return (expression as MemberBindingExpressionSyntax).Name.Identifier.Text;
				case SyntaxKind.CastExpression:
					return (expression as CastExpressionSyntax).Type.GetExpressionSignature();
				case SyntaxKind.FalseLiteralExpression:
					return "false";
				case SyntaxKind.TrueLiteralExpression:
					return "true";
				case SyntaxKind.NullLiteralExpression:
					return "null";
				case SyntaxKind.ThisExpression:
					return "this";
				case SyntaxKind.BaseExpression:
					return "base";
				case SyntaxKind.InvocationExpression:
					return (expression as InvocationExpressionSyntax).Expression.GetExpressionSignature();
				case SyntaxKind.ConditionalAccessExpression:
					return (expression as ConditionalAccessExpressionSyntax).WhenNotNull.GetExpressionSignature();
				case SyntaxKind.CharacterLiteralExpression:
				case SyntaxKind.NumericLiteralExpression:
				case SyntaxKind.StringLiteralExpression:
					return (expression as LiteralExpressionSyntax).Token.ValueText;
				case SyntaxKind.ConditionalExpression:
					return (expression as ConditionalExpressionSyntax).Condition.GetExpressionSignature() + "?:";
				default:
					return expression.GetFirstIdentifier()?.Identifier.Text;
			}
		}

		public static ExpressionSyntax GetHardCodedValue(this ImmutableArray<SyntaxReference> refs) {
			foreach (var item in refs) {
				var node = item.GetSyntax();
				ExpressionSyntax val;
				if (node is ParameterSyntax p) {
					if ((val = p.Default?.Value) != null) {
						return val;
					}
				}
				else if (node is VariableDeclaratorSyntax v) {
					if ((val = v.Initializer?.Value) != null) {
						return val;
					}
				}
				else if (node is EnumMemberDeclarationSyntax en) {
					if ((val = en.EqualsValue?.Value) != null) {
						return val;
					}
				}
			}
			return null;
		}

		public static ParameterSyntax FindParameter(this BaseMethodDeclarationSyntax node, string name) {
			return node?.ParameterList.Parameters.FirstOrDefault(p => p.Identifier.Text == name);
		}
		public static TypeParameterSyntax FindTypeParameter(this SyntaxNode node, string name) {
			var tp = (node is MethodDeclarationSyntax m && m.Arity > 0) ? m.TypeParameterList
				: node is TypeDeclarationSyntax t && t.Arity > 0 ? t.TypeParameterList
				: node is DelegateDeclarationSyntax d && d.Arity > 0 ? d.TypeParameterList
				: null;
			return tp?.Parameters.FirstOrDefault(p => p.Identifier.Text == name);
		}
		#endregion

		#region Directives
		public static bool IsRegionalDirective(this SyntaxKind kind) {
			switch (kind) {
				case SyntaxKind.IfDirectiveTrivia:
				case SyntaxKind.ElifDirectiveTrivia:
				case SyntaxKind.ElseDirectiveTrivia:
				case SyntaxKind.EndIfDirectiveTrivia:
				case SyntaxKind.RegionDirectiveTrivia:
				case SyntaxKind.EndRegionDirectiveTrivia:
					return true;
			}
			return false;
		}

		public static List<DirectiveTriviaSyntax> GetDirectives(this SyntaxNode node, Func<DirectiveTriviaSyntax, bool> predicate = null) {
			if (node.ContainsDirectives == false) {
				return null;
			}
			var directive = node.GetFirstDirective(predicate);
			if (directive == null) {
				return null;
			}
			var directives = new List<DirectiveTriviaSyntax>(4);
			var endOfNode = node.Span.End;
			do {
				if (directive.SpanStart > node.SpanStart) {
					directives.Add(directive);
				}
				directive = directive.GetNextDirective(predicate);
			} while (directive != null && directive.SpanStart < endOfNode);
			return directives;
		}

		public static TStartDirective GetPrecedingDirective<TStartDirective, TEndDirective>(this TEndDirective directive, SyntaxKind startSyntaxKind, SyntaxKind endSyntaxKind)
			where TStartDirective : DirectiveTriviaSyntax
			where TEndDirective : DirectiveTriviaSyntax {
			if (directive == null) {
				return null;
			}
			DirectiveTriviaSyntax d = directive;
			int c = -1;
			while ((d = d.GetPreviousDirective()) != null) {
				if (d.IsKind(endSyntaxKind)) {
					--c;
				}
				else if (d.IsKind(startSyntaxKind)) {
					++c;
					if (c == 0) {
						return d as TStartDirective;
					}
				}
			}
			return null;
		}
		public static RegionDirectiveTriviaSyntax GetRegion(this EndRegionDirectiveTriviaSyntax syntax) {
			return GetPrecedingDirective<RegionDirectiveTriviaSyntax, EndRegionDirectiveTriviaSyntax>(syntax, SyntaxKind.RegionDirectiveTrivia, SyntaxKind.EndRegionDirectiveTrivia);
		}
		public static IfDirectiveTriviaSyntax GetIf(this EndIfDirectiveTriviaSyntax syntax) {
			return GetPrecedingDirective<IfDirectiveTriviaSyntax, EndIfDirectiveTriviaSyntax>(syntax, SyntaxKind.IfDirectiveTrivia, SyntaxKind.EndIfDirectiveTrivia);
		}
		public static TEndDirective GetFollowingDirective<TStartDirective, TEndDirective>(this TStartDirective directive, SyntaxKind startSyntaxKind, SyntaxKind endSyntaxKind)
			where TStartDirective : DirectiveTriviaSyntax
			where TEndDirective : DirectiveTriviaSyntax {
			if (directive == null) {
				return null;
			}
			DirectiveTriviaSyntax d = directive;
			int c = 1;
			while ((d = d.GetNextDirective()) != null) {
				if (d.IsKind(endSyntaxKind)) {
					--c;
					if (c == 0) {
						return d as TEndDirective;
					}
				}
				else if (d.IsKind(startSyntaxKind)) {
					++c;
				}
			}
			return null;
		}
		public static EndRegionDirectiveTriviaSyntax GetEndRegion(this RegionDirectiveTriviaSyntax syntax) {
			return GetFollowingDirective<RegionDirectiveTriviaSyntax, EndRegionDirectiveTriviaSyntax>(syntax, SyntaxKind.RegionDirectiveTrivia, SyntaxKind.EndRegionDirectiveTrivia);
		}
		public static EndIfDirectiveTriviaSyntax GetEndIf(this IfDirectiveTriviaSyntax syntax) {
			return GetFollowingDirective<IfDirectiveTriviaSyntax, EndIfDirectiveTriviaSyntax>(syntax, SyntaxKind.IfDirectiveTrivia, SyntaxKind.EndIfDirectiveTrivia);
		}
		#endregion

		#region Relation
		public static SyntaxNode GetAncestorOrSelfDeclaration(this SyntaxNode node) {
			return node.AncestorsAndSelf().FirstOrDefault(n => n is MemberDeclarationSyntax || n is BaseTypeDeclarationSyntax);
		}

		/// <summary>Gets the first expression containing current node which is of type <typeparamref name="TExpression"/>.</summary>
		public static TExpression GetAncestorOrSelfExpression<TExpression>(this SyntaxNode node)
			where TExpression : ExpressionSyntax {
			if (node is TExpression r) {
				return r;
			}
			if (node is ExpressionSyntax) {
				var n = node;
				while ((n = n.Parent) is ExpressionSyntax) {
					if ((r = n as TExpression) != null) {
						return r;
					}
				}
			}
			return null;
		}

		/// <summary>Gets the first node containing current node which is of type <typeparamref name="TSyntaxNode"/> and not <see cref="ExpressionSyntax"/>.</summary>
		public static ExpressionSyntax GetLastAncestorExpressionNode(this SyntaxNode node) {
			if (node is ExpressionSyntax r) {
				while (node.Parent is ExpressionSyntax n) {
					node = r = n;
				}
				return r;
			}
			return null;
		}

		public static IEnumerable<SyntaxNode> GetDescendantDeclarations(this SyntaxNode root, CancellationToken cancellationToken = default) {
			foreach (var child in root.ChildNodes()) {
				cancellationToken.ThrowIfCancellationRequested();
				switch (child.Kind()) {
					case SyntaxKind.CompilationUnit:
					case SyntaxKind.NamespaceDeclaration:
						foreach (var item in child.GetDescendantDeclarations(cancellationToken)) {
							yield return item;
						}
						break;
					case SyntaxKind.ClassDeclaration:
					case SyntaxKind.DelegateDeclaration:
					case SyntaxKind.EnumDeclaration:
					case SyntaxKind.EventDeclaration:
					case SyntaxKind.InterfaceDeclaration:
					case SyntaxKind.StructDeclaration:
					case RecordDeclaration:
					case RecordStructDeclaration:
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
		#endregion

		#region Statements
		public static StatementSyntax GetStatement(this LabeledStatementSyntax labeledStatement) {
			var s = labeledStatement.Statement;
			while (s is LabeledStatementSyntax labeled) {
				s = labeled.Statement;
			}
			return s;
		}
		public static StatementSyntax GetSingleStatement(this StatementSyntax statement) {
			return statement is BlockSyntax b ? b.GetSingleStatement() : statement;
		}
		public static StatementSyntax GetSingleStatement(this BlockSyntax block) {
		START:
			var s = block.Statements;
			if (s.Count == 1) {
				var first = s[0];
				if (first.IsKind(SyntaxKind.Block)) {
					block = (BlockSyntax)first;
					goto START;
				}
				return first;
			}
			return null;
		}

		public static StatementSyntax GetContainingStatement(this SyntaxNode node) {
			var s = node.FirstAncestorOrSelf<StatementSyntax>();
			while (s.IsKind(SyntaxKind.Block)) {
				s = s.Parent.FirstAncestorOrSelf<StatementSyntax>();
			}
			return s;
		}
		public static SyntaxNode GetContainingStatementOrDeclaration(this SyntaxNode node) {
			var s = node.FirstAncestorOrSelf<SyntaxNode>(n => n is StatementSyntax || n.Kind().IsDeclaration());
			while (s.IsKind(SyntaxKind.Block)) {
				s = s.Parent.FirstAncestorOrSelf<SyntaxNode>(n => n is StatementSyntax || n.Kind().IsDeclaration());
			}
			return s;
		}
		public static SelectedSyntax<StatementSyntax> GetStatements(this SyntaxNode node, TextSpan span) {
			if (span.Length == 0) {
				goto NO_STATEMENT;
			}
			var statement = node.FindNode(new TextSpan(span.Start, 1)).FirstAncestorOrSelf<StatementSyntax>();
			StatementSyntax preceding = null, following = null;
			int spanEnd = span.End;
			List<StatementSyntax> selected = null;
			TextSpan nodeSpan;
			List<SyntaxNode> siblings = null;
			int i = -1;
			while (statement != null) {
				if (span.Contains(nodeSpan = statement.Span) == false) {
					goto NO_STATEMENT;
				}
				if (span.Start != nodeSpan.Start
					&& (statement.HasLeadingTrivia == false || span.Start != statement.GetLeadingTrivia().Span.Start)) {
					goto NO_STATEMENT;
				}
				if (statement.FullSpan.End <= spanEnd) {
					nodeSpan = statement.FullSpan;
					span = TextSpan.FromBounds(nodeSpan.End, spanEnd);
				}
				else if (spanEnd < statement.FullSpan.End && spanEnd >= nodeSpan.End) {
					span = default;
				}
				else {
					span = TextSpan.FromBounds(nodeSpan.End, spanEnd);
				}
				if (selected == null) {
					selected = new List<StatementSyntax>();
					siblings = statement.Parent.ChildNodes().ToList();
					i = siblings.IndexOf(statement);
				}
				selected.Add(statement);
				while (statement.Parent.IsKind(SyntaxKind.LabeledStatement)) {
					siblings = (statement = (StatementSyntax)statement.Parent).Parent.ChildNodes().ToList();
					i = siblings.IndexOf(statement);
				}
				if (preceding == null && i > 0) {
					preceding = siblings[i - 1] as StatementSyntax;
				}
				if (++i < siblings.Count) {
					statement = siblings[i] as StatementSyntax;
				}
				else {
					break;
				}
				if (span.IsEmpty) {
					break;
				}
			}
			if (siblings != null && i < siblings.Count) {
				following = siblings[i] as StatementSyntax;
			}
			if (span.Length == 0) {
				return new SelectedSyntax<StatementSyntax>(preceding, selected, following);
			}
		NO_STATEMENT:
			return default;
		}
		#endregion

		#region Trivias
		/// <summary>Gets full span for ordinary nodes, excluding leading directives; gets span for regions.</summary>
		public static Span GetSematicSpan(this SyntaxNode node, bool expandRegion) {
			int start, end;
			SyntaxTriviaList tl;
			SyntaxTrivia t;
			if (node.IsKind(SyntaxKind.RegionDirectiveTrivia)) {
				var region = node as RegionDirectiveTriviaSyntax;
				start = node.SpanStart;
				end = node.Span.End;
				if (start > 0) {
					t = region.SyntaxTree.GetCompilationUnitRoot().FindTrivia(start - 1, true);
					if (t.IsKind(SyntaxKind.WhitespaceTrivia)) {
						start = t.SpanStart;
					}
				}
				tl = (expandRegion ? (region.GetEndRegion() ?? (SyntaxNode)region) : region).GetTrailingTrivia();
				for (int i = tl.Count - 1; i >= 0; i--) {
					t = tl[i];
					if (t.IsKind(SyntaxKind.EndOfLineTrivia)) {
						end = t.Span.End;
						break;
					}
				}
				return new Span(start, end - start);
			}

			var span = node.FullSpan;
			if (node.ContainsDirectives == false) {
				return span.ToSpan();
			}

			start = span.Start;
			end = span.End;
			tl = node.GetLeadingTrivia();
			for (int i = tl.Count - 1; i >= 0; i--) {
				t = tl[i];
				if (t.IsDirective) {
					start = t.FullSpan.End;
					break;
				}
			}
			return new Span(start, end - start);
		}

		public static TSyntax WithWhitespaceFrom<TSyntax>(this TSyntax node, SyntaxNode from)
			where TSyntax : SyntaxNode {
			return node.WithLeadingTrivia(from.GetLeadingWhitespace()).WithTrailingTrivia(from.GetTrailingWhitespace());
		}

		public static SyntaxTriviaList GetNonDirectiveTrivia(this SyntaxTriviaList list) {
			int i;
			for (i = list.Count - 1; i >= 0; i--) {
				if (list[i].IsDirective == false) {
					continue;
				}
				break;
			}
			return i == 0 ? list : new SyntaxTriviaList(list.Skip(i));
		}

		public static SyntaxTriviaList GetNonDirectiveLeadingTrivia(this SyntaxNode node) {
			return node.HasLeadingTrivia == false
				? default
				: node.GetLeadingTrivia().GetNonDirectiveTrivia();
		}
		/// <summary>Gets leading whitespaces of <paramref name="node"/>, excluding directives, comments and whitespaces before them.</summary>
		public static SyntaxTriviaList GetLeadingWhitespace(this SyntaxNode node) {
			if (node == null || node.HasLeadingTrivia == false) {
				return SyntaxTriviaList.Empty;
			}
			var lt = node.GetLeadingTrivia();
			int first;
			for (int i = first = 0; i < lt.Count; i++) {
				var trivia = lt[i];
				if (trivia.IsKind(SyntaxKind.WhitespaceTrivia) == false) {
					first = i + 1;
				}
			}
			return first > 0 ? new SyntaxTriviaList(lt.Skip(first)) : lt;
		}
		public static SyntaxTriviaList GetTrailingWhitespace(this SyntaxNode node) {
			if (node == null || node.HasTrailingTrivia == false) {
				return SyntaxTriviaList.Empty;
			}
			var lt = node.GetTrailingTrivia();
			int first;
			for (int i = first = 0; i < lt.Count; i++) {
				var trivia = lt[i];
				if (trivia.IsKind(SyntaxKind.WhitespaceTrivia) == false) {
					first = i + 1;
					break;
				}
			}
			return first > 0 ? new SyntaxTriviaList(lt.Take(first)) : lt;
		}

		public static bool IsLineComment(this SyntaxTrivia trivia) {
			return trivia.Kind().CeqAny(SyntaxKind.MultiLineCommentTrivia, SyntaxKind.SingleLineCommentTrivia);
		}

		static readonly char[] __SplitLineChars = new char[] { '\r', '\n' };
		public static string GetCommentContent(this SyntaxTriviaList trivias, bool leading) {
			using (var rsb = ReusableStringBuilder.AcquireDefault(100)) {
				var sb = rsb.Resource;
				int hasLineBreak = 0;
				foreach (var item in trivias) {
					switch ((SyntaxKind)item.RawKind) {
						case SyntaxKind.SingleLineCommentTrivia:
							if (hasLineBreak != 0) {
								if (hasLineBreak == 1) {
									sb.AppendLine();
								}
								hasLineBreak = 0;
							}
							var t = item.ToString();
							if (t.Length > 2 && t[2] == ' ') {
								sb.Append(t, 3, t.Length - 3);
							}
							else {
								sb.Append(t, 2, t.Length - 2);
							}
							break;
						case SyntaxKind.MultiLineCommentTrivia:
							if (hasLineBreak != 0) {
								if (hasLineBreak == 1) {
									sb.AppendLine();
								}
								hasLineBreak = 0;
							}
							AppendMultilineComment(sb, item.ToString());
							break;
						case SyntaxKind.WhitespaceTrivia:
							continue;
						case SyntaxKind.EndOfLineTrivia:
							if (sb.Length != 0 && ++hasLineBreak > 1) {
								goto default;
							}
							break;
						default:
							if (leading) {
								sb.Clear();
								hasLineBreak = 0;
							}
							else {
								goto EXIT;
							}
							break;
					}
				}
			EXIT:
				return sb.Length != 0 ? sb.ToString() : null;
			}

			void AppendMultilineComment(System.Text.StringBuilder sb, string t) {
				int i = 2, // skip leading "/*"
					l = t.Length - 2; // drop trailing "*/"
				while (true) {
					var p = t.IndexOfAny(__SplitLineChars, i);
					if (i > 2) {
						sb.AppendLine();
					}
					while (i < l) {
						if (Char.IsWhiteSpace(t[i])) {
							++i;
						}
						else {
							break;
						}
					}
					if (p == -1) {
						sb.Append(t, i, l - i);
						break;
					}
					sb.Append(t, i, p - i);
					i = (t[p] == '\r' && p + 1 < l && t[p + 1] == '\n' ? 2 : 1)
						+ p;
				}
			}
		}
		#endregion
	}
}
