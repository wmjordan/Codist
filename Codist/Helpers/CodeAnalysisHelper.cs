using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text;

namespace Codist
{
	static partial class CodeAnalysisHelper
	{
		internal static readonly SymbolDisplayFormat QuickInfoSymbolDisplayFormat = new SymbolDisplayFormat(
			typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
			genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
			parameterOptions: SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeOptionalBrackets | SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeType,
			memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeContainingType,
			delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
			miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);
		internal static readonly SymbolDisplayFormat InTypeOverloadDisplayFormat = new SymbolDisplayFormat(
			typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
			genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
			parameterOptions: SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeOptionalBrackets | SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeType,
			memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType,
			delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
			miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);
		internal static readonly SymbolDisplayFormat MemberNameFormat = new SymbolDisplayFormat(
			typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
			parameterOptions: SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeOptionalBrackets | SymbolDisplayParameterOptions.IncludeType,
			genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
			miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);
		internal static readonly SymbolDisplayFormat TypeMemberNameFormat = new SymbolDisplayFormat(
			typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
			memberOptions: SymbolDisplayMemberOptions.IncludeContainingType,
			genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
			miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);
		internal static readonly SymbolDisplayFormat QualifiedTypeNameFormat = new SymbolDisplayFormat(
			typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
			memberOptions: SymbolDisplayMemberOptions.IncludeContainingType,
			genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
			miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);
		internal const SyntaxKind DotDotToken = (SyntaxKind)8222;
		internal const SyntaxKind QuestionQuestionEqualsToken = (SyntaxKind)8284;
		internal const SyntaxKind WithKeyword = (SyntaxKind)8442;
		internal const SyntaxKind InitKeyword = (SyntaxKind)8443;
		internal const SyntaxKind RecordKeyword = (SyntaxKind)8444;
		internal const SyntaxKind RequiredKeyword = (SyntaxKind)8447;
		internal const SyntaxKind ImplicitObjectCreationExpression = (SyntaxKind)8659;
		internal const SyntaxKind FileScopedNamespaceDeclaration = (SyntaxKind)8845;
		internal const SyntaxKind RecursivePattern = (SyntaxKind)9020;
		internal const SyntaxKind PositionalPatternClause = (SyntaxKind)9023;
		internal const SyntaxKind SwitchExpression = (SyntaxKind)9025;
		internal const SyntaxKind SwitchExpressionArm = (SyntaxKind)9026;
		internal const SyntaxKind VarPattern = (SyntaxKind)9027;
		internal const SyntaxKind FunctionPointerCallingConvention = (SyntaxKind)9059;
		internal const SyntaxKind InitAccessorDeclaration = (SyntaxKind)9060;
		internal const SyntaxKind WithInitializerExpression = (SyntaxKind)9062;
		internal const SyntaxKind RecordDeclaration = (SyntaxKind)9063;
		internal const SyntaxKind RecordStructDeclaration = (SyntaxKind)9068;
		internal const SyntaxKind SingleLineRawStringLiteralToken = (SyntaxKind)8518;
		internal const SyntaxKind MultiLineRawStringLiteralToken = (SyntaxKind)8519;
		internal const SymbolKind FunctionPointerType = (SymbolKind)20;
		internal const TypeKind FunctionPointer = (TypeKind)13;
		internal const MethodKind FunctionPointerMethod = (MethodKind)18;

		#region Node info
		public static bool IsAny(this SyntaxKind kind, SyntaxKind kind1, SyntaxKind kind2) {
			return kind == kind1 || kind == kind2;
		}
		public static bool IsAny(this SyntaxKind kind, SyntaxKind kind1, SyntaxKind kind2, SyntaxKind kind3) {
			return kind == kind1 || kind == kind2 || kind == kind3;
		}
		public static bool IsAny(this SyntaxKind kind, SyntaxKind kind1, SyntaxKind kind2, SyntaxKind kind3, SyntaxKind kind4) {
			return kind == kind1 || kind == kind2 || kind == kind3 || kind == kind4;
		}
		public static bool IsAny(this SyntaxKind kind, SyntaxKind kind1, SyntaxKind kind2, SyntaxKind kind3, SyntaxKind kind4, SyntaxKind kind5) {
			return kind == kind1 || kind == kind2 || kind == kind3 || kind == kind4 || kind == kind5;
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

		public static DeclarationCategory GetDeclarationCategory(this SyntaxKind kind) {
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

		public static SyntaxTriviaList GetNonDirectiveLeadingTrivia(this SyntaxNode node) {
			return node.HasLeadingTrivia == false
				? default
				: node.GetLeadingTrivia().GetNonDirectiveTrivia();
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
		#endregion

		#region Node icon
		public static int GetImageId(this SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.ClassDeclaration:
				case RecordDeclaration:
					return GetClassIcon((BaseTypeDeclarationSyntax)node);
				case SyntaxKind.EnumDeclaration: return GetEnumIcon((EnumDeclarationSyntax)node);
				case SyntaxKind.StructDeclaration:
				case RecordStructDeclaration:
					return GetStructIcon((BaseTypeDeclarationSyntax)node);
				case SyntaxKind.InterfaceDeclaration: return GetInterfaceIcon((InterfaceDeclarationSyntax)node);
				case SyntaxKind.MethodDeclaration: return GetMethodIcon((MethodDeclarationSyntax)node);
				case SyntaxKind.ConstructorDeclaration: return GetConstructorIcon((ConstructorDeclarationSyntax)node);
				case SyntaxKind.PropertyDeclaration: return GetPropertyIconExt((BasePropertyDeclarationSyntax)node);
				case SyntaxKind.IndexerDeclaration: return GetPropertyIcon((BasePropertyDeclarationSyntax)node);
				case SyntaxKind.OperatorDeclaration: return GetOperatorIcon((OperatorDeclarationSyntax)node);
				case SyntaxKind.ConversionOperatorDeclaration: return GetConversionIcon((ConversionOperatorDeclarationSyntax)node);
				case SyntaxKind.FieldDeclaration: return GetFieldIcon((FieldDeclarationSyntax)node);
				case SyntaxKind.EnumMemberDeclaration: return IconIds.EnumField;
				case SyntaxKind.VariableDeclarator: return node.Parent.Parent.GetImageId();
				case SyntaxKind.VariableDeclaration:
				case SyntaxKind.LocalDeclarationStatement: return IconIds.LocalVariable;
				case SyntaxKind.NamespaceDeclaration:
				case FileScopedNamespaceDeclaration: return IconIds.Namespace;
				case SyntaxKind.ArgumentList:
				case SyntaxKind.AttributeArgumentList: return IconIds.Argument;
				case SyntaxKind.DoStatement: return KnownImageIds.DoWhile;
				case SyntaxKind.FixedStatement: return KnownImageIds.Pin;
				case SyntaxKind.ForEachStatement: return KnownImageIds.ForEach;
				case SyntaxKind.ForStatement: return KnownImageIds.ForEachLoop;
				case SyntaxKind.IfStatement: return IconIds.If;
				case SyntaxKind.LockStatement: return KnownImageIds.Lock;
				case SyntaxKind.SwitchStatement:
				case SwitchExpression:
					return KnownImageIds.FlowSwitch;
				case SyntaxKind.SwitchSection: return KnownImageIds.FlowDecision;
				case SyntaxKind.TryStatement: return IconIds.TryCatch;
				case SyntaxKind.UsingStatement: return IconIds.Using;
				case SyntaxKind.WhileStatement: return KnownImageIds.While;
				case SyntaxKind.ParameterList: return KnownImageIds.Parameter;
				case SyntaxKind.ParenthesizedExpression: return IconIds.ParenthesizedExpression;
				case SyntaxKind.ParenthesizedLambdaExpression:
				case SyntaxKind.SimpleLambdaExpression: return IconIds.LambdaExpression;
				case SyntaxKind.DelegateDeclaration: return GetDelegateIcon((DelegateDeclarationSyntax)node);
				case SyntaxKind.EventDeclaration: return GetEventIcon((BasePropertyDeclarationSyntax)node);
				case SyntaxKind.EventFieldDeclaration: return GetEventFieldIcon((EventFieldDeclarationSyntax)node);
				case SyntaxKind.UnsafeStatement: return IconIds.Unsafe;
				case SyntaxKind.XmlElement:
				case SyntaxKind.XmlEmptyElement: return KnownImageIds.XMLElement;
				case SyntaxKind.XmlComment: return KnownImageIds.XMLCommentTag;
				case SyntaxKind.DestructorDeclaration: return IconIds.Destructor;
				case SyntaxKind.UncheckedStatement: return KnownImageIds.CheckBoxUnchecked;
				case SyntaxKind.CheckedStatement: return KnownImageIds.CheckBoxChecked;
				case SyntaxKind.ReturnStatement: return IconIds.Return;
				case SyntaxKind.ExpressionStatement: return GetImageId(((ExpressionStatementSyntax)node).Expression);
				case SyntaxKind.Attribute: return IconIds.Attribute;
				case SyntaxKind.YieldReturnStatement: return KnownImageIds.Yield;
				case SyntaxKind.GotoStatement:
				case SyntaxKind.GotoCaseStatement:
				case SyntaxKind.GotoDefaultStatement: return KnownImageIds.GoToSourceCode;
				case SyntaxKind.LocalFunctionStatement: return IconIds.LocalFunction;
				case SyntaxKind.RegionDirectiveTrivia: return IconIds.Region;
				case SyntaxKind.EndRegionDirectiveTrivia: return KnownImageIds.ToolstripPanelBottom;
			}
			return KnownImageIds.UnknownMember;

			int GetClassIcon(BaseTypeDeclarationSyntax syntax) {
				bool isPartial = false;
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Kind()) {
						case SyntaxKind.PublicKeyword: return KnownImageIds.ClassPublic;
						case SyntaxKind.ProtectedKeyword: return KnownImageIds.ClassProtected;
						case SyntaxKind.InternalKeyword: return KnownImageIds.ClassInternal;
						case SyntaxKind.PrivateKeyword: return KnownImageIds.ClassPrivate;
						case SyntaxKind.PartialKeyword: isPartial = true; break;
					}
				}
				return isPartial ? IconIds.PartialClass
					: syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.ClassInternal
					: KnownImageIds.ClassPrivate;
			}
			int GetStructIcon(BaseTypeDeclarationSyntax syntax) {
				bool isPartial = false;
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Kind()) {
						case SyntaxKind.PublicKeyword: return KnownImageIds.StructurePublic;
						case SyntaxKind.ProtectedKeyword: return KnownImageIds.StructureProtected;
						case SyntaxKind.InternalKeyword: return KnownImageIds.StructureInternal;
						case SyntaxKind.PrivateKeyword: return KnownImageIds.StructurePrivate;
						case SyntaxKind.PartialKeyword: isPartial = true; break;
					}
				}
				return isPartial ? IconIds.PartialStruct
					: syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.StructureInternal
					: KnownImageIds.StructurePrivate;
			}
			int GetEnumIcon(EnumDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Kind()) {
						case SyntaxKind.PublicKeyword: return KnownImageIds.EnumerationPublic;
						case SyntaxKind.InternalKeyword: return KnownImageIds.EnumerationInternal;
						case SyntaxKind.PrivateKeyword: return KnownImageIds.EnumerationPrivate;
					}
				}
				return syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.EnumerationInternal : KnownImageIds.EnumerationPrivate;
			}
			int GetInterfaceIcon(InterfaceDeclarationSyntax syntax) {
				bool isPartial = false;
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Kind()) {
						case SyntaxKind.PublicKeyword: return KnownImageIds.InterfacePublic;
						case SyntaxKind.InternalKeyword: return KnownImageIds.InterfaceInternal;
						case SyntaxKind.PrivateKeyword: return KnownImageIds.InterfacePrivate;
						case SyntaxKind.PartialKeyword: isPartial = true; break;
					}
				}
				return isPartial ? IconIds.PartialInterface
					: syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.InterfaceInternal
					: KnownImageIds.InterfacePrivate;
			}
			int GetEventIcon(BasePropertyDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Kind()) {
						case SyntaxKind.PublicKeyword: return KnownImageIds.EventPublic;
						case SyntaxKind.InternalKeyword: return KnownImageIds.EventInternal;
						case SyntaxKind.ProtectedKeyword: return KnownImageIds.EventProtected;
						case SyntaxKind.PrivateKeyword: return KnownImageIds.EventPrivate;
					}
				}
				return syntax.ExplicitInterfaceSpecifier != null ? IconIds.ExplicitInterfaceEvent
					: syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration) ? KnownImageIds.EventInternal
					: syntax.Parent.IsKind(SyntaxKind.InterfaceDeclaration) ? KnownImageIds.EventPublic
					: KnownImageIds.EventPrivate;
			}
			int GetEventFieldIcon(EventFieldDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Kind()) {
						case SyntaxKind.PublicKeyword: return KnownImageIds.EventPublic;
						case SyntaxKind.InternalKeyword: return KnownImageIds.EventInternal;
						case SyntaxKind.ProtectedKeyword: return KnownImageIds.EventProtected;
						case SyntaxKind.PrivateKeyword: return KnownImageIds.EventPrivate;
					}
				}
				return syntax.Parent.IsKind(SyntaxKind.InterfaceDeclaration)
					? KnownImageIds.EventPublic
					: KnownImageIds.EventPrivate;
			}
			int GetDelegateIcon(DelegateDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Kind()) {
						case SyntaxKind.PublicKeyword: return KnownImageIds.DelegatePublic;
						case SyntaxKind.InternalKeyword: return KnownImageIds.DelegateInternal;
						case SyntaxKind.ProtectedKeyword: return KnownImageIds.DelegateProtected;
						case SyntaxKind.PrivateKeyword: return KnownImageIds.DelegatePrivate;
					}
				}
				return syntax.Parent.IsKind(SyntaxKind.NamespaceDeclaration)
					? KnownImageIds.DelegateInternal
					: KnownImageIds.DelegatePrivate;
			}
			int GetFieldIcon(FieldDeclarationSyntax syntax) {
				bool isConst = false;
				var accessibility = Accessibility.NotApplicable;
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Kind()) {
						case SyntaxKind.ConstKeyword: isConst = true; break;
						case SyntaxKind.PublicKeyword: accessibility = Accessibility.Public; break;
						case SyntaxKind.InternalKeyword:
							if (accessibility != Accessibility.Protected) {
								accessibility = Accessibility.Internal;
							}
							break;
						case SyntaxKind.ProtectedKeyword: accessibility = Accessibility.Protected; break;
						case SyntaxKind.PrivateKeyword: accessibility = Accessibility.Private; break;
					}
				}
				switch (accessibility) {
					case Accessibility.Public: return isConst ? KnownImageIds.ConstantPublic : KnownImageIds.FieldPublic;
					case Accessibility.Internal: return isConst ? KnownImageIds.ConstantInternal : KnownImageIds.FieldInternal;
					case Accessibility.Protected: return isConst ? KnownImageIds.ConstantProtected : KnownImageIds.FieldProtected;
					case Accessibility.Private: return isConst ? KnownImageIds.ConstantPrivate : KnownImageIds.FieldPrivate;
				}
				return syntax.Parent.IsKind(SyntaxKind.InterfaceDeclaration)
					? isConst ? KnownImageIds.ConstantPublic : KnownImageIds.FieldPublic
					: isConst ? KnownImageIds.ConstantPrivate : KnownImageIds.FieldPrivate;
			}
			int GetMethodIcon(MethodDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Kind()) {
						case SyntaxKind.PublicKeyword: return KnownImageIds.MethodPublic;
						case SyntaxKind.InternalKeyword: return KnownImageIds.MethodInternal;
						case SyntaxKind.ProtectedKeyword: return KnownImageIds.MethodProtected;
						case SyntaxKind.PrivateKeyword: return KnownImageIds.MethodPrivate;
					}
				}
				return syntax.ExplicitInterfaceSpecifier != null ? IconIds.ExplicitInterfaceMethod
					: syntax.Parent.IsKind(SyntaxKind.InterfaceDeclaration) ? KnownImageIds.MethodPublic
					: KnownImageIds.MethodPrivate;
			}
			int GetConstructorIcon(ConstructorDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Kind()) {
						case SyntaxKind.PublicKeyword: return IconIds.PublicConstructor;
						case SyntaxKind.InternalKeyword: return IconIds.InternalConstructor;
						case SyntaxKind.ProtectedKeyword: return IconIds.ProtectedConstructor;
						case SyntaxKind.PrivateKeyword: return IconIds.PrivateConstructor;
					}
				}
				return IconIds.PrivateConstructor;
			}
			int GetPropertyIconExt(BasePropertyDeclarationSyntax syntax) {
				bool autoProperty = syntax.Modifiers.Any(i => i.IsKind(SyntaxKind.AbstractKeyword)) == false
					&& syntax.AccessorList?.Accessors.All(i => i.Body == null && i.ExpressionBody == null) == true;
				if (autoProperty) {
					return GetPropertyIcon(syntax);
				}
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Kind()) {
						case SyntaxKind.PublicKeyword: return IconIds.PublicPropertyMethod;
						case SyntaxKind.InternalKeyword: return IconIds.InternalPropertyMethod;
						case SyntaxKind.ProtectedKeyword: return IconIds.ProtectedPropertyMethod;
						case SyntaxKind.PrivateKeyword: return IconIds.PrivatePropertyMethod;
					}
				}
				return syntax.Parent.IsKind(SyntaxKind.InterfaceDeclaration)
					? IconIds.PublicPropertyMethod
					: IconIds.PrivatePropertyMethod;
			}
			int GetPropertyIcon(BasePropertyDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Kind()) {
						case SyntaxKind.PublicKeyword: return KnownImageIds.PropertyPublic;
						case SyntaxKind.InternalKeyword: return KnownImageIds.PropertyInternal;
						case SyntaxKind.ProtectedKeyword: return KnownImageIds.PropertyProtected;
						case SyntaxKind.PrivateKeyword: return KnownImageIds.PropertyPrivate;
					}
				}
				return syntax.ExplicitInterfaceSpecifier != null ? IconIds.ExplicitInterfaceProperty
					: syntax.Parent.IsKind(SyntaxKind.InterfaceDeclaration) ? KnownImageIds.PropertyPublic
					: KnownImageIds.PropertyPrivate;
			}
			int GetOperatorIcon(OperatorDeclarationSyntax syntax) {
				foreach (var modifier in syntax.Modifiers) {
					switch (modifier.Kind()) {
						case SyntaxKind.PublicKeyword: return KnownImageIds.OperatorPublic;
						case SyntaxKind.InternalKeyword: return KnownImageIds.OperatorInternal;
						case SyntaxKind.ProtectedKeyword: return KnownImageIds.OperatorProtected;
						case SyntaxKind.PrivateKeyword: return KnownImageIds.OperatorPrivate;
					}
				}
				return syntax.Parent.IsKind(SyntaxKind.InterfaceDeclaration)
					? KnownImageIds.OperatorPublic
					: KnownImageIds.OperatorPrivate;
			}

			int GetConversionIcon(ConversionOperatorDeclarationSyntax syntax) {
				return syntax.ImplicitOrExplicitKeyword.IsKind(SyntaxKind.ExplicitKeyword)
					? IconIds.ExplicitConversion
					: IconIds.ImplicitConversion;
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
				case FileScopedNamespaceDeclaration: return NonPublicOrFutureAccessors.GetFileScopedNamespaceName(node).ToString() == NonPublicOrFutureAccessors.GetFileScopedNamespaceName(other).ToString();
				case SyntaxKind.ClassDeclaration:
				case SyntaxKind.StructDeclaration:
				case SyntaxKind.InterfaceDeclaration:
				case RecordDeclaration:
				case RecordStructDeclaration:
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
				case SyntaxKind.IndexerDeclaration: return "Indexer";
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
				case SyntaxKind.IfDirectiveTrivia:
					return GetIfSignature((IfDirectiveTriviaSyntax)node);
				case SyntaxKind.EndIfDirectiveTrivia:
					return GetEndIfSignature((EndIfDirectiveTriviaSyntax)node);
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
					default: return "<" + new String(',', arity - 1) + ">";
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
				if (d.IsKind(SyntaxKind.EndRegionDirectiveTrivia)) {
					--c;
					if (c == 0) {
						return d as TEndDirective;
					}
				}
				else if (d.IsKind(SyntaxKind.RegionDirectiveTrivia)) {
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
					return ((MemberAccessExpressionSyntax)expression).Name.Identifier.Text;
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
		/// <summary>Gets the first expression containing current node which is of type <typeparamref name="TExpression"/>.</summary>
		public static TExpression GetAncestorOrSelfExpression<TExpression>(this SyntaxNode node)
			where TExpression : ExpressionSyntax {
			TExpression r;
			if ((r = node as TExpression) != null) {
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
			var r = node as ExpressionSyntax;
			if (r == null) {
				return null;
			}
			while (node.Parent is ExpressionSyntax n) {
				node = r = n;
			}
			return r;
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

		public static Span GetLineSpan(this SyntaxNode node) {
			var s = node.SyntaxTree.GetLineSpan(node.Span);
			return Span.FromBounds(s.StartLinePosition.Line, s.EndLinePosition.Line);
		}

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
		public static TSyntax WithWhitespaceFrom<TSyntax>(this TSyntax node, SyntaxNode from)
			where TSyntax : SyntaxNode {
			return node.WithLeadingTrivia(from.GetLeadingWhitespace()).WithTrailingTrivia(from.GetTrailingWhitespace());
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

		public static SyntaxNode UnqualifyExceptNamespace(this SyntaxNode node) {
			if (node.IsKind(SyntaxKind.QualifiedName)) {
				var n = node;
				while ((n = n.Parent).IsKind(SyntaxKind.QualifiedName)) {
				}
				switch (n.Kind()) {
					case SyntaxKind.UsingDirective:
					case SyntaxKind.NamespaceDeclaration:
						return node;
					default:
						return n;
				}
			}
			return node;
		}

		/// <summary>Navigates upward through ancestral axis and find out the first node reflecting the usage.</summary>
		public static SyntaxNode GetNodePurpose(this SyntaxNode node) {
			NameSyntax originName;
			if (node.IsKind(SyntaxKind.IdentifierName) || node.IsKind(SyntaxKind.GenericName)) {
				originName = node as NameSyntax;
				node = node.Parent;
			}
			else {
				originName = null;
			}
			var n = node;
			while (n.IsKind(SyntaxKind.QualifiedName)
				|| n.IsKind(SyntaxKind.SimpleMemberAccessExpression)
				|| n.IsKind(SyntaxKind.PointerMemberAccessExpression)) {
				if (n is MemberAccessExpressionSyntax ma && ma.Name != originName) {
					return node;
				}
				node = n;
				n = n.Parent;
			}
			return n;
		}

		public static ArgumentListSyntax GetImplicitObjectCreationArgumentList(this ExpressionSyntax syntax) {
			return NonPublicOrFutureAccessors.GetImplicitObjectCreationArgumentList(syntax);
		}

		public static int GetWarningLevel(int csErrorCode) {
			return NonPublicOrFutureAccessors.GetWarningLevel(csErrorCode);
		}

		static partial class NonPublicOrFutureAccessors
		{
			public static readonly Func<SyntaxNode, NameSyntax> GetFileScopedNamespaceName = ReflectionHelper.CreateGetPropertyMethod<SyntaxNode, NameSyntax>("Name", typeof(NamespaceDeclarationSyntax).Assembly.GetType("Microsoft.CodeAnalysis.CSharp.Syntax.FileScopedNamespaceDeclarationSyntax"));
			public static readonly Func<ExpressionSyntax, ArgumentListSyntax> GetImplicitObjectCreationArgumentList = ReflectionHelper.CreateGetPropertyMethod<ExpressionSyntax, ArgumentListSyntax>("ArgumentList", typeof(ExpressionSyntax).Assembly.GetType("Microsoft.CodeAnalysis.CSharp.Syntax.BaseObjectCreationExpressionSyntax"));
			public static readonly Func<int, int> GetWarningLevel = ReflectionHelper.CallStaticFunc<int, int>(typeof(LanguageVersionFacts).Assembly.GetType("Microsoft.CodeAnalysis.CSharp.ErrorFacts")?.GetMethod("GetWarningLevel", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) ?? (Func<int, int>)((int _) => 3);
		}
	}

	[Flags]
	enum DeclarationCategory
	{
		None,
		Type = 1,
		Namespace = 1 << 1,
		Member = 1 << 2,
		Local = 1 << 3
	}
}
