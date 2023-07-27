using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Taggers
{
	/// <summary>Classification type definition export for <see cref="CSharpParser"/>.</summary>
	static class CSharpClassificationDefinition
	{
#pragma warning disable 169, IDE0044
		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeKeyword)]
		[Name(Constants.CSharpAbstractionKeyword)]
		static ClassificationTypeDefinition AbstractionKeyword;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpAbstractMemberName)]
		static ClassificationTypeDefinition AbstractMember;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpAliasNamespaceName)]
		static ClassificationTypeDefinition AliasNamespace;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CSharpAttributeNotation)]
		[BaseDefinition(Constants.CodeClassName)]
		[Name(Constants.CSharpAttributeName)]
		static ClassificationTypeDefinition AttributeName;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.CSharpAttributeNotation)]
		static ClassificationTypeDefinition AttributeNotation;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeKeyword)]
		[BaseDefinition(Constants.CodeKeywordControl)]
		[Name(Constants.CSharpBranchingKeyword)]
		static ClassificationTypeDefinition BranchingKeyword;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CSharpReadOnlyFieldName)]
		[BaseDefinition(Constants.CSharpStaticMemberName)]
		[BaseDefinition(Constants.CodeConstantName)]
		[Name(Constants.CSharpConstFieldName)]
		static ClassificationTypeDefinition ConstField;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CSharpMethodName)]
		[Name(Constants.CSharpConstructorMethodName)]
		static ClassificationTypeDefinition ConstructorMethod;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeKeyword)]
		[BaseDefinition(Constants.CodeKeywordControl)]
		[Name(Constants.CSharpControlFlowKeyword)]
		static ClassificationTypeDefinition ControlFlowKeyword;

		[Export(typeof(ClassificationTypeDefinition))]
		//[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpDeclarationName)]
		static ClassificationTypeDefinition Declaration;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodePunctuation)]
		[Name(Constants.CSharpDeclarationBrace)]
		static ClassificationTypeDefinition DeclarationBrace;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeEnumMemberName)]
		[BaseDefinition(Constants.CSharpConstFieldName)]
		[Name(Constants.CSharpEnumFieldName)]
		static ClassificationTypeDefinition EnumField;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeEventName)]
		[Name(Constants.CSharpEventName)]
		static ClassificationTypeDefinition Event;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CSharpMethodName)]
		[BaseDefinition(Constants.CSharpStaticMemberName)]
		[BaseDefinition(Constants.CodeExtensionMethodName)]
		[Name(Constants.CSharpExtensionMethodName)]
		static ClassificationTypeDefinition ExtensionMethod;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CSharpMethodName)]
		[Name(Constants.CSharpExternMethodName)]
		static ClassificationTypeDefinition ExternMethod;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeFieldName)]
		[Name(Constants.CSharpFieldName)]
		static ClassificationTypeDefinition Field;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeLabelName)]
		[Name(Constants.CSharpLabel)]
		static ClassificationTypeDefinition Label;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CSharpMethodName)]
		[Name(Constants.CSharpLocalFunctionDeclarationName)]
		static ClassificationTypeDefinition LocalFunction;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeLocalName)]
		[Name(Constants.CSharpLocalVariableName)]
		static ClassificationTypeDefinition LocalVariable;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeKeyword)]
		[Name(Constants.CSharpLoopKeyword)]
		static ClassificationTypeDefinition LoopKeyword;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeMethodName)]
		[Name(Constants.CSharpMethodName)]
		static ClassificationTypeDefinition Method;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeNamespaceName)]
		[Name(Constants.CSharpNamespaceName)]
		static ClassificationTypeDefinition Namespace;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpNestedTypeName)]
		static ClassificationTypeDefinition NestedType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CSharpDeclarationName)]
		[Name(Constants.CSharpMemberDeclarationName)]
		static ClassificationTypeDefinition MemberDeclaration;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CSharpLocalVariableName)]
		[Name(Constants.CSharpLocalDeclarationName)]
		static ClassificationTypeDefinition LocalDeclaration;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpOverrideMemberName)]
		static ClassificationTypeDefinition OverrideMember;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodeParameterName)]
		[Name(Constants.CSharpParameterName)]
		static ClassificationTypeDefinition Parameter;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[BaseDefinition(Constants.CodePropertyName)]
		[Name(Constants.CSharpPropertyName)]
		static ClassificationTypeDefinition Property;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpPrivateMemberName)]
		static ClassificationTypeDefinition PrivateMember;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CSharpFieldName)]
		[Name(Constants.CSharpReadOnlyFieldName)]
		static ClassificationTypeDefinition ReadOnlyField;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeStructName)]
		[Name(Constants.CSharpReadOnlyStructName)]
		static ClassificationTypeDefinition ReadOnlyStruct;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeStructName)]
		[Name(Constants.CSharpRefStructName)]
		static ClassificationTypeDefinition RefStruct;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeKeyword)]
		[Name(Constants.CSharpResourceKeyword)]
		static ClassificationTypeDefinition ResourceKeyword;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.CSharpSealedMemberName)]
		static ClassificationTypeDefinition SealedClassOrMember;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[BaseDefinition(Constants.CodeStaticSymbol)]
		[Name(Constants.CSharpStaticMemberName)]
		static ClassificationTypeDefinition StaticMember;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpTypeParameterName)]
		static ClassificationTypeDefinition TypeParameter;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeKeyword)]
		[Name(Constants.CSharpTypeCastKeyword)]
		static ClassificationTypeDefinition TypeCastKeyword;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpVirtualMemberName)]
		static ClassificationTypeDefinition VirtualMember;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeOperator)]
		[Name(Constants.CSharpVariableCapturedExpression)]
		static ClassificationTypeDefinition VariableCapturedExpression;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CSharpFieldName)]
		[Name(Constants.CSharpVolatileFieldName)]
		static ClassificationTypeDefinition VolatileField;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.CSharpXmlDoc)]
		static ClassificationTypeDefinition XmlDoc;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.CodeBold)]
		static ClassificationTypeDefinition Bold;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.CSharpUserSymbol)]
		static ClassificationTypeDefinition UserSymbol;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.CSharpMetadataSymbol)]
		static ClassificationTypeDefinition MetadataSymbol;
#pragma warning restore 169
	}
}