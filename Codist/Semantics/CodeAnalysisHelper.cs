using System;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
		#region Compatibility constants (for older VS versions)
		internal const SyntaxKind DotDotToken = (SyntaxKind)8222;
		internal const SyntaxKind QuestionQuestionEqualsToken = (SyntaxKind)8284;
		internal const SyntaxKind WithKeyword = (SyntaxKind)8442;
		internal const SyntaxKind InitKeyword = (SyntaxKind)8443;
		internal const SyntaxKind RecordKeyword = (SyntaxKind)8444;
		internal const SyntaxKind RequiredKeyword = (SyntaxKind)8447;
		internal const SyntaxKind ImplicitObjectCreationExpression = (SyntaxKind)8659;
		internal const SyntaxKind CollectionExpression = (SyntaxKind)9076;
		internal const SyntaxKind FileScopedNamespaceDeclaration = (SyntaxKind)8845;
		internal const SyntaxKind RecursivePattern = (SyntaxKind)9020;
		internal const SyntaxKind PropertyPatternClause = (SyntaxKind)9021;
		internal const SyntaxKind PositionalPatternClause = (SyntaxKind)9023;
		internal const SyntaxKind SwitchExpression = (SyntaxKind)9025;
		internal const SyntaxKind SwitchExpressionArm = (SyntaxKind)9026;
		internal const SyntaxKind ListPatternExpression = (SyntaxKind)9035;
		internal const SyntaxKind VarPattern = (SyntaxKind)9027;
		internal const SyntaxKind FunctionPointerCallingConvention = (SyntaxKind)9059;
		internal const SyntaxKind InitAccessorDeclaration = (SyntaxKind)9060;
		internal const SyntaxKind WithInitializerExpression = (SyntaxKind)9062;
		internal const SyntaxKind RecordDeclaration = (SyntaxKind)9063;
		internal const SyntaxKind RecordStructDeclaration = (SyntaxKind)9068;
		internal const SyntaxKind PrimaryConstructorBaseType = (SyntaxKind)9065;
		internal const SyntaxKind SingleLineRawStringLiteralToken = (SyntaxKind)8518;
		internal const SyntaxKind MultiLineRawStringLiteralToken = (SyntaxKind)8519;
		internal const SymbolKind FunctionPointerType = (SymbolKind)20;
		internal const TypeKind FunctionPointer = (TypeKind)13;
		internal const MethodKind FunctionPointerMethod = (MethodKind)18;
		#endregion

		public static Span GetLineSpan(this SyntaxNode node) {
			var s = node.SyntaxTree.GetLineSpan(node.Span);
			return Span.FromBounds(s.StartLinePosition.Line, s.EndLinePosition.Line);
		}

		public static ArgumentListSyntax GetImplicitObjectCreationArgumentList(this ExpressionSyntax syntax) {
			return NonPublicOrFutureAccessors.GetImplicitObjectCreationArgumentList(syntax);
		}

		public static int GetWarningLevel(int csErrorCode) {
			return NonPublicOrFutureAccessors.GetWarningLevel(csErrorCode);
		}

		// gets parameter list in primary constructor
		public static ParameterListSyntax GetParameterList(this TypeDeclarationSyntax typeDeclaration) {
			return NonPublicOrFutureAccessors.GetParameterList(typeDeclaration);
		}

		static partial class NonPublicOrFutureAccessors
		{
			public static readonly Func<SyntaxNode, NameSyntax> GetFileScopedNamespaceName = ReflectionHelper.CreateGetPropertyMethod<SyntaxNode, NameSyntax>("Name", typeof(NamespaceDeclarationSyntax).Assembly.GetType("Microsoft.CodeAnalysis.CSharp.Syntax.FileScopedNamespaceDeclarationSyntax"));

			public static readonly Func<ExpressionSyntax, ArgumentListSyntax> GetImplicitObjectCreationArgumentList = ReflectionHelper.CreateGetPropertyMethod<ExpressionSyntax, ArgumentListSyntax>("ArgumentList", typeof(ExpressionSyntax).Assembly.GetType("Microsoft.CodeAnalysis.CSharp.Syntax.BaseObjectCreationExpressionSyntax"));

			public static readonly Func<int, int> GetWarningLevel = ReflectionHelper.CallStaticFunc<int, int>(typeof(LanguageVersionFacts).Assembly.GetType("Microsoft.CodeAnalysis.CSharp.ErrorFacts")?.GetMethod("GetWarningLevel", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) ?? (Func<int, int>)((int _) => 3);

			public static readonly Func<TypeDeclarationSyntax, ParameterListSyntax> GetParameterList = ReflectionHelper.CreateGetPropertyMethod<TypeDeclarationSyntax, ParameterListSyntax>("ParameterList", typeof(TypeDeclarationSyntax));
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
}
