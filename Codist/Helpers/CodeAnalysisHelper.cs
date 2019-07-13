using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text;

namespace Codist
{
	static partial class CodeAnalysisHelper
	{
		#region Node info
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
				case SyntaxKind.LocalFunctionStatement:
					//case SyntaxKind.VariableDeclarator:
					return true;
			}
			return false;
		}

		public static bool IsTypeOrNamespaceDeclaration(this SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.ClassDeclaration:
				case SyntaxKind.DelegateDeclaration:
				case SyntaxKind.EnumDeclaration:
				case SyntaxKind.EventDeclaration:
				case SyntaxKind.InterfaceDeclaration:
				case SyntaxKind.StructDeclaration:
				case SyntaxKind.NamespaceDeclaration:
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
				case SyntaxKind.YieldReturnStatement:
				case SyntaxKind.ExpressionStatement:
				case SyntaxKind.GotoStatement:
				case SyntaxKind.GotoCaseStatement:
				case SyntaxKind.GotoDefaultStatement:
				case SyntaxKind.XmlElement:
				case SyntaxKind.XmlEmptyElement:
				case SyntaxKind.XmlComment:
					return true;
			}
			return false;
		}

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

		#endregion

		#region Node icon
		public static int GetImageId(this SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.ClassDeclaration: return GetClassIcon((ClassDeclarationSyntax)node);
				case SyntaxKind.EnumDeclaration: return GetEnumIcon((EnumDeclarationSyntax)node);
				case SyntaxKind.StructDeclaration: return GetStructIcon((StructDeclarationSyntax)node);
				case SyntaxKind.InterfaceDeclaration: return GetInterfaceIcon((InterfaceDeclarationSyntax)node);
				case SyntaxKind.MethodDeclaration: return GetMethodIcon((MethodDeclarationSyntax)node);
				case SyntaxKind.ConstructorDeclaration: return GetConstructorIcon((ConstructorDeclarationSyntax)node);
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
				case SyntaxKind.ExpressionStatement: return GetImageId(((ExpressionStatementSyntax)node).Expression);
				case SyntaxKind.Attribute: return KnownImageIds.FormPostBodyParameterNode;
				case SyntaxKind.YieldReturnStatement: return KnownImageIds.Yield;
				case SyntaxKind.GotoStatement:
				case SyntaxKind.GotoCaseStatement:
				case SyntaxKind.GotoDefaultStatement: return KnownImageIds.GoToSourceCode;
				case SyntaxKind.LocalFunctionStatement: return KnownImageIds.MethodSnippet;
				case SyntaxKind.RegionDirectiveTrivia: return KnownImageIds.Numeric;
				case SyntaxKind.EndRegionDirectiveTrivia: return KnownImageIds.ToolstripPanelBottom;
			}
			return KnownImageIds.UnknownMember;
			int GetClassIcon(ClassDeclarationSyntax syntax) {
				bool isPartial = false;
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Text) {
						case "public": return KnownImageIds.ClassPublic;
						case "protected": return KnownImageIds.ClassProtected;
						case "internal": return KnownImageIds.ClassInternal;
						case "private": return KnownImageIds.ClassPrivate;
						case "partial": isPartial = true; break;
					}
				}
				return isPartial ? KnownImageIds.ClassShortcut : syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.ClassInternal : KnownImageIds.ClassPrivate;
			}
			int GetStructIcon(StructDeclarationSyntax syntax) {
				bool isPartial = false;
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Text) {
						case "public": return KnownImageIds.StructurePublic;
						case "protected": return KnownImageIds.StructureProtected;
						case "internal": return KnownImageIds.StructureInternal;
						case "private": return KnownImageIds.StructurePrivate;
						case "partial": isPartial = true; break;
					}
				}
				return isPartial ? KnownImageIds.StructureShortcut : syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.StructureInternal : KnownImageIds.StructurePrivate;
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
				bool isPartial = false;
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Text) {
						case "public": return KnownImageIds.InterfacePublic;
						case "internal": return KnownImageIds.InterfaceInternal;
						case "private": return KnownImageIds.InterfacePrivate;
						case "partial": isPartial = true; break;
					}
				}
				return isPartial ? KnownImageIds.InterfaceSnippet : syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.InterfaceInternal : KnownImageIds.InterfacePrivate;
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
				var accessibility = Accessibility.Private;
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Text) {
						case "const": isConst = true; break;
						case "public": accessibility = Accessibility.Public; break;
						case "internal":
							if (accessibility != Accessibility.Protected) {
								accessibility = Accessibility.Internal;
							}
							break;
						case "protected": accessibility = Accessibility.Protected; break;
					}
				}
				switch (accessibility) {
					case Accessibility.Public: return isConst ? KnownImageIds.ConstantPublic : KnownImageIds.FieldPublic;
					case Accessibility.Internal: return isConst ? KnownImageIds.ConstantInternal : KnownImageIds.FieldInternal;
					case Accessibility.Protected: return isConst ? KnownImageIds.ConstantProtected : KnownImageIds.FieldProtected;
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
			int GetConstructorIcon(ConstructorDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Text) {
						case "public": return KnownImageIds.TypePublic;
						case "internal": return KnownImageIds.TypeInternal;
						case "protected": return KnownImageIds.TypeProtected;
						case "private": return KnownImageIds.TypePrivate;
					}
				}
				return KnownImageIds.TypePrivate;
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

		public static int GetImageId(this ExpressionSyntax node) {
			switch (node.Kind()) {
				case SyntaxKind.InvocationExpression: return KnownImageIds.InvokeMethod;
			}
			return node is AssignmentExpressionSyntax ? KnownImageIds.Assign
				: node is BinaryExpressionSyntax || node is PrefixUnaryExpressionSyntax || node is PostfixUnaryExpressionSyntax ? KnownImageIds.Operator
				: KnownImageIds.Action;
		} 
		#endregion

		public static bool MatchSignature(this MemberDeclarationSyntax node, SyntaxNode other) {
			var k1 = node.Kind();
			var k2 = other.Kind();
			if (k1 != k2) {
				return false;
			}
			switch (k1) {
				case SyntaxKind.NamespaceDeclaration: return ((NamespaceDeclarationSyntax)node).Name.ToString() == ((NamespaceDeclarationSyntax)other).Name.ToString();
				case SyntaxKind.ClassDeclaration:
				case SyntaxKind.StructDeclaration:
				case SyntaxKind.InterfaceDeclaration:
					var t1 = (TypeDeclarationSyntax)node;
					var t2 = (TypeDeclarationSyntax)other;
					return t1.Arity == t2.Arity && t1.Identifier.Text == t2.Identifier.Text;
				case SyntaxKind.EnumDeclaration: return ((EnumDeclarationSyntax)node).Identifier.Text == ((EnumDeclarationSyntax)other).Identifier.Text;
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
			if (x == y) {
				return true;
			}
			if (x == null || y == null) {
				return false;
			}
			return x.Name.GetName() == y.Name.GetName();
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
				case SyntaxKind.ClassDeclaration: return GetClassSignature((ClassDeclarationSyntax)node);
				case SyntaxKind.StructDeclaration: return GetStructSignature((StructDeclarationSyntax)node);
				case SyntaxKind.InterfaceDeclaration: return GetInterfaceSignature((InterfaceDeclarationSyntax)node);
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
				case SyntaxKind.IndexerDeclaration: return "Indexer";
				case SyntaxKind.OperatorDeclaration: return ((OperatorDeclarationSyntax)node).OperatorToken.Text;
				case SyntaxKind.PropertyDeclaration: return ((PropertyDeclarationSyntax)node).Identifier.Text;
				case SyntaxKind.EnumMemberDeclaration: return ((EnumMemberDeclarationSyntax)node).Identifier.Text;
				case SyntaxKind.SimpleLambdaExpression: return "(" + ((SimpleLambdaExpressionSyntax)node).Parameter.ToString() + ")";
				case SyntaxKind.ParenthesizedLambdaExpression: return ((ParenthesizedLambdaExpressionSyntax)node).ParameterList.ToString();
				case SyntaxKind.LocalFunctionStatement: return ((LocalFunctionStatementSyntax)node).Identifier.Text;
				case SyntaxKind.NamespaceDeclaration: return (node as NamespaceDeclarationSyntax).Name.GetName();
				case SyntaxKind.VariableDeclarator: return ((VariableDeclaratorSyntax)node).Identifier.Text;
				case SyntaxKind.LocalDeclarationStatement: return GetVariableSignature(((LocalDeclarationStatementSyntax)node).Declaration, position);
				case SyntaxKind.VariableDeclaration: return GetVariableSignature((VariableDeclarationSyntax)node, position);
				case SyntaxKind.ForEachStatement: return ((ForEachStatementSyntax)node).Identifier.Text;
				case SyntaxKind.ForStatement: return GetVariableSignature(((ForStatementSyntax)node).Declaration, position);
				case SyntaxKind.IfStatement: return ((IfStatementSyntax)node).Condition.GetExpressionSignature();
				case SyntaxKind.SwitchSection: return GetSwitchSignature((SwitchSectionSyntax)node);
				case SyntaxKind.SwitchStatement: return ((SwitchStatementSyntax)node).Expression.GetExpressionSignature();
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
				case SyntaxKind.YieldReturnStatement: return ((YieldStatementSyntax)node).Expression?.GetExpressionSignature();
				case SyntaxKind.GotoStatement:
				case SyntaxKind.GotoCaseStatement:
					return ((GotoStatementSyntax)node).Expression?.GetExpressionSignature();
				case SyntaxKind.GotoDefaultStatement:
					return "(default)";
				case SyntaxKind.RegionDirectiveTrivia:
					return GetRegionSignature((RegionDirectiveTriviaSyntax)node);
				case SyntaxKind.EndRegionDirectiveTrivia:
					return GetEndRegionSignature((EndRegionDirectiveTriviaSyntax)node);
			}
			return null;
			string GetClassSignature(ClassDeclarationSyntax syntax) => GetGenericSignature(syntax.Identifier.Text, syntax.Arity);
			string GetStructSignature(StructDeclarationSyntax syntax) => GetGenericSignature(syntax.Identifier.Text, syntax.Arity);
			string GetInterfaceSignature(InterfaceDeclarationSyntax syntax) => GetGenericSignature(syntax.Identifier.Text, syntax.Arity);
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
			string GetRegionSignature(RegionDirectiveTriviaSyntax syntax) {
				var e = syntax.EndOfDirectiveToken;
				return e.HasLeadingTrivia ? e.LeadingTrivia[0].ToString() : String.Empty;
			}
			string GetEndRegionSignature(EndRegionDirectiveTriviaSyntax syntax) {
				DirectiveTriviaSyntax region = syntax;
				int c = 1;
				while ((region = region.GetPreviousDirective()) != null) {
					if (region.IsKind(SyntaxKind.RegionDirectiveTrivia)) {
						--c;
						if (c == 0) {
							return GetRegionSignature(region as RegionDirectiveTriviaSyntax);
						}
					}
					else if (region.IsKind(SyntaxKind.EndRegionDirectiveTrivia)) {
						++c;
					}
				}
				return String.Empty;
			}
		}

		public static string GetParameterListSignature(this ParameterListSyntax parameters, bool useParamName) {
			if (parameters.Parameters.Count == 0) {
				return "()";
			}
			using (var r = Microsoft.VisualStudio.Utilities.ReusableStringBuilder.AcquireDefault(30)) {
				var sb = r.Resource;
				sb.Append('(');
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
				sb.Append(')');
				return sb.ToString();
			}
		}
		public static EndRegionDirectiveTriviaSyntax GetEndRegion(this RegionDirectiveTriviaSyntax syntax) {
			DirectiveTriviaSyntax region = syntax;
			int c = 1;
			while ((region = region.GetNextDirective()) != null) {
				if (region.IsKind(SyntaxKind.EndRegionDirectiveTrivia)) {
					--c;
					if (c == 0) {
						return region as EndRegionDirectiveTriviaSyntax;
					}
				}
				else if (region.IsKind(SyntaxKind.RegionDirectiveTrivia)) {
					++c;
				}
			}
			return null;
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
				case SyntaxKind.TypeOfExpression: return (expression as TypeOfExpressionSyntax).Type.GetExpressionSignature();
				case SyntaxKind.IdentifierName: return (expression as IdentifierNameSyntax).Identifier.Text;
				case SyntaxKind.QualifiedName: return (expression as QualifiedNameSyntax).Right.Identifier.Text;
				case SyntaxKind.AliasQualifiedName: return (expression as AliasQualifiedNameSyntax).Name.Identifier.Text;
				case SyntaxKind.SimpleMemberAccessExpression: return (expression as MemberAccessExpressionSyntax).Name.Identifier.Text;
				case SyntaxKind.PointerMemberAccessExpression:
				case SyntaxKind.MemberBindingExpression: return (expression as MemberBindingExpressionSyntax).Name.Identifier.Text;
				case SyntaxKind.CastExpression:
					return (expression as CastExpressionSyntax).Type.GetExpressionSignature();
				case SyntaxKind.FalseLiteralExpression: return "false";
				case SyntaxKind.TrueLiteralExpression: return "true";
				case SyntaxKind.NullLiteralExpression: return "null";
				case SyntaxKind.ThisExpression: return "this";
				case SyntaxKind.BaseExpression: return "base";
				case SyntaxKind.InvocationExpression: return (expression as InvocationExpressionSyntax).Expression.GetExpressionSignature();
				case SyntaxKind.ConditionalAccessExpression: return (expression as ConditionalAccessExpressionSyntax).WhenNotNull.GetExpressionSignature();
				case SyntaxKind.CharacterLiteralExpression:
				case SyntaxKind.NumericLiteralExpression:
				case SyntaxKind.StringLiteralExpression:
					return (expression as LiteralExpressionSyntax).Token.ValueText;
				case SyntaxKind.ConditionalExpression: return (expression as ConditionalExpressionSyntax).Condition.GetExpressionSignature() + "?:";
				default: return expression.GetFirstIdentifier()?.Identifier.Text;
			}
		}
		public static SyntaxNode GetAncestorOrSelfDeclaration(this SyntaxNode node) {
			return node.AncestorsAndSelf().FirstOrDefault(n => n is MemberDeclarationSyntax || n is BaseTypeDeclarationSyntax);
		}
		public static IEnumerable<SyntaxNode> GetDecendantDeclarations(this SyntaxNode root, CancellationToken cancellationToken = default) {
			foreach (var child in root.ChildNodes()) {
				cancellationToken.ThrowIfCancellationRequested();
				switch (child.Kind()) {
					case SyntaxKind.CompilationUnit:
					case SyntaxKind.NamespaceDeclaration:
						foreach (var item in child.GetDecendantDeclarations(cancellationToken)) {
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

		public static Span GetLineSpan(this SyntaxNode node) {
			var s = node.SyntaxTree.GetLineSpan(node.Span);
			return Span.FromBounds(s.StartLinePosition.Line, s.EndLinePosition.Line);
		}

		/// <summary>Gets full span for ordinary nodes, excluding leading directives; gets span for regions.</summary>
		public static Span GetSematicSpan(this SyntaxNode node, bool expandRegion) {
			int start, end;
			SyntaxTriviaList trivias;
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
				trivias = (expandRegion ? (region.GetEndRegion() ?? (SyntaxNode)region) : region).GetTrailingTrivia();
				for (int i = trivias.Count - 1; i >= 0; i--) {
					t = trivias[i];
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
			trivias = node.GetLeadingTrivia();
			for (int i = trivias.Count - 1; i >= 0; i--) {
				t = trivias[i];
				if (t.IsDirective) {
					start = t.FullSpan.End;
					break;
				}
			}
			return new Span(start, end - start);
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
					return (node as BaseTypeDeclarationSyntax).Identifier;
				case SyntaxKind.DelegateDeclaration: return (node as DelegateDeclarationSyntax).Identifier;
				case SyntaxKind.MethodDeclaration: return (node as MethodDeclarationSyntax).Identifier;
				case SyntaxKind.OperatorDeclaration: return (node as OperatorDeclarationSyntax).OperatorToken;
				case SyntaxKind.ConversionOperatorDeclaration: return (node as ConversionOperatorDeclarationSyntax).Type.GetFirstToken();
				case SyntaxKind.ConstructorDeclaration: return (node as ConstructorDeclarationSyntax).Identifier;
				case SyntaxKind.DestructorDeclaration: return (node as DestructorDeclarationSyntax).Identifier;
				case SyntaxKind.PropertyDeclaration: return (node as PropertyDeclarationSyntax).Identifier;
				case SyntaxKind.IndexerDeclaration: return (node as IndexerDeclarationSyntax).ThisKeyword;
				case SyntaxKind.EventDeclaration: return (node as EventDeclarationSyntax).Identifier;
				case SyntaxKind.EnumMemberDeclaration: return (node as EnumMemberDeclarationSyntax).Identifier;
				case SyntaxKind.VariableDeclarator: return (node as VariableDeclaratorSyntax).Identifier;
			}
			return node.GetFirstToken();
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
		public static List<DirectiveTriviaSyntax> GetDirectives(this SyntaxNode node, Func<DirectiveTriviaSyntax, bool> predicate = null) {
			if (node.ContainsDirectives == false) {
				return null;
			}
			var directive = node.GetFirstDirective(predicate);
			if (directive == null) {
				return null;
			}
			var directives = new List<DirectiveTriviaSyntax>(4);
			do {
				directives.Add(directive);
				directive = directive.GetNextDirective(predicate);
			} while (directive != null);
			return directives;
		}
		public static ParameterSyntax FindParameter(this BaseMethodDeclarationSyntax node, string name) {
			return node?.ParameterList.Parameters.FirstOrDefault(p => p.Identifier.Text == name);
		}
		public static TypeParameterSyntax FindTypeParameter(this SyntaxNode node, string name) {
			TypeParameterListSyntax tp;
			var m = node as MethodDeclarationSyntax;
			if (m != null && m.Arity > 0) {
				tp = m.TypeParameterList;
			}
			else {
				var t = node as TypeDeclarationSyntax;
				if (t != null && t.Arity > 0) {
					tp = t.TypeParameterList;
				}
				else {
					var d = node as DelegateDeclarationSyntax;
					if (d != null && d.Arity > 0) {
						tp = d.TypeParameterList;
					}
					else {
						tp = null;
					}
				}
			}
			return tp?.Parameters.FirstOrDefault(p => p.Identifier.Text == name);
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
