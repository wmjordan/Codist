using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Taggers
{
	/// <summary>Classification type definition export for <see cref="CSharpParser"/>.</summary>
	static class CSharpClassificationDefinitions
	{
#pragma warning disable 169, IDE0044
		[Export]
		[BaseDefinition(Constants.CodeKeyword)]
		[Name(Constants.CSharpAbstractionKeyword)]
		static ClassificationTypeDefinition AbstractionKeyword;

		[Export]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpAbstractMemberName)]
		static ClassificationTypeDefinition AbstractMember;

		[Export]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpAliasNamespaceName)]
		static ClassificationTypeDefinition AliasNamespace;

		[Export]
		[BaseDefinition(Constants.CSharpAttributeNotation)]
		[BaseDefinition(Constants.CodeClassName)]
		[Name(Constants.CSharpAttributeName)]
		static ClassificationTypeDefinition AttributeName;

		[Export]
		[Name(Constants.CSharpAttributeNotation)]
		static ClassificationTypeDefinition AttributeNotation;

		[Export]
		[BaseDefinition(Constants.CodeKeyword)]
		[BaseDefinition(Constants.CodeKeywordControl)]
		[Name(Constants.CSharpBranchingKeyword)]
		static ClassificationTypeDefinition BranchingKeyword;

		[Export]
		[BaseDefinition(Constants.CSharpReadOnlyFieldName)]
		[BaseDefinition(Constants.CSharpStaticMemberName)]
		[BaseDefinition(Constants.CodeConstantName)]
		[Name(Constants.CSharpConstFieldName)]
		static ClassificationTypeDefinition ConstField;

		[Export]
		[BaseDefinition(Constants.CSharpMethodName)]
		[Name(Constants.CSharpConstructorMethodName)]
		static ClassificationTypeDefinition ConstructorMethod;

		[Export]
		[BaseDefinition(Constants.CodeKeyword)]
		[BaseDefinition(Constants.CodeKeywordControl)]
		[Name(Constants.CSharpControlFlowKeyword)]
		static ClassificationTypeDefinition ControlFlowKeyword;

		[Export]
		//[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpDeclarationName)]
		static ClassificationTypeDefinition Declaration;

		[Export]
		[BaseDefinition(Constants.CodePunctuation)]
		[Name(Constants.CSharpDeclarationBrace)]
		static ClassificationTypeDefinition DeclarationBrace;

		[Export]
		[BaseDefinition(Constants.CodeEnumMemberName)]
		[BaseDefinition(Constants.CSharpConstFieldName)]
		[Name(Constants.CSharpEnumFieldName)]
		static ClassificationTypeDefinition EnumField;

		[Export]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeEventName)]
		[Name(Constants.CSharpEventName)]
		static ClassificationTypeDefinition Event;

		[Export]
		[BaseDefinition(Constants.CSharpMethodName)]
		[BaseDefinition(Constants.CSharpStaticMemberName)]
		[BaseDefinition(Constants.CodeExtensionMethodName)]
		[Name(Constants.CSharpExtensionMethodName)]
		static ClassificationTypeDefinition ExtensionMethod;

		[Export]
		[BaseDefinition(Constants.CSharpMethodName)]
		[Name(Constants.CSharpExternMethodName)]
		static ClassificationTypeDefinition ExternMethod;

		[Export]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeFieldName)]
		[Name(Constants.CSharpFieldName)]
		static ClassificationTypeDefinition Field;

		[Export]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeLabelName)]
		[Name(Constants.CSharpLabel)]
		static ClassificationTypeDefinition Label;

		[Export]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CSharpMethodName)]
		[Name(Constants.CSharpLocalFunctionDeclarationName)]
		static ClassificationTypeDefinition LocalFunction;

		[Export]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeLocalName)]
		[Name(Constants.CSharpLocalVariableName)]
		static ClassificationTypeDefinition LocalVariable;

		[Export]
		[BaseDefinition(Constants.CodeKeyword)]
		[Name(Constants.CSharpLoopKeyword)]
		static ClassificationTypeDefinition LoopKeyword;

		[Export]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeMethodName)]
		[Name(Constants.CSharpMethodName)]
		static ClassificationTypeDefinition Method;

		[Export]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeNamespaceName)]
		[Name(Constants.CSharpNamespaceName)]
		static ClassificationTypeDefinition Namespace;

		[Export]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpNestedTypeName)]
		static ClassificationTypeDefinition NestedType;

		[Export]
		[BaseDefinition(Constants.CSharpDeclarationName)]
		[Name(Constants.CSharpMemberDeclarationName)]
		static ClassificationTypeDefinition MemberDeclaration;

		[Export]
		[BaseDefinition(Constants.CSharpLocalVariableName)]
		[Name(Constants.CSharpLocalDeclarationName)]
		static ClassificationTypeDefinition LocalDeclaration;

		[Export]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpOverrideMemberName)]
		static ClassificationTypeDefinition OverrideMember;

		[Export]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeParameterName)]
		[Name(Constants.CSharpParameterName)]
		static ClassificationTypeDefinition Parameter;

		[Export]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodePropertyName)]
		[Name(Constants.CSharpPropertyName)]
		static ClassificationTypeDefinition Property;

		[Export]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpPrivateMemberName)]
		static ClassificationTypeDefinition PrivateMember;

		[Export]
		[BaseDefinition(Constants.CSharpFieldName)]
		[Name(Constants.CSharpReadOnlyFieldName)]
		static ClassificationTypeDefinition ReadOnlyField;

		[Export]
		[BaseDefinition(Constants.CodeStructName)]
		[Name(Constants.CSharpReadOnlyStructName)]
		static ClassificationTypeDefinition ReadOnlyStruct;

		[Export]
		[BaseDefinition(Constants.CodeStructName)]
		[Name(Constants.CSharpRefStructName)]
		static ClassificationTypeDefinition RefStruct;

		[Export]
		[BaseDefinition(Constants.CodeKeyword)]
		[Name(Constants.CSharpResourceKeyword)]
		static ClassificationTypeDefinition ResourceKeyword;

		[Export]
		[Name(Constants.CSharpSealedMemberName)]
		static ClassificationTypeDefinition SealedClassOrMember;

		[Export]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[BaseDefinition(Constants.CodeStaticSymbol)]
		[Name(Constants.CSharpStaticMemberName)]
		static ClassificationTypeDefinition StaticMember;

		[Export]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpTypeParameterName)]
		static ClassificationTypeDefinition TypeParameter;

		[Export]
		[BaseDefinition(Constants.CodeKeyword)]
		[Name(Constants.CSharpTypeCastKeyword)]
		static ClassificationTypeDefinition TypeCastKeyword;

		[Export]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpVirtualMemberName)]
		static ClassificationTypeDefinition VirtualMember;

		[Export]
		[BaseDefinition(Constants.CodeOperator)]
		[Name(Constants.CSharpVariableCapturedExpression)]
		static ClassificationTypeDefinition VariableCapturedExpression;

		[Export]
		[BaseDefinition(Constants.CSharpFieldName)]
		[Name(Constants.CSharpVolatileFieldName)]
		static ClassificationTypeDefinition VolatileField;

		[Export]
		[Name(Constants.CSharpXmlDoc)]
		static ClassificationTypeDefinition XmlDoc;

		[Export]
		[Name(Constants.CodeBold)]
		static ClassificationTypeDefinition Bold;

		[Export]
		[Name(Constants.CSharpUserSymbol)]
		static ClassificationTypeDefinition UserSymbol;

		[Export]
		[Name(Constants.CSharpMetadataSymbol)]
		static ClassificationTypeDefinition MetadataSymbol;
#pragma warning restore 169
	}
}