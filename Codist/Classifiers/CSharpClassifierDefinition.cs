using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Classifiers
{
	/// <summary>Classification type definition export for <see cref="CSharpClassifier"/>.</summary>
	static class CSharpClassifierDefinition
	{
#pragma warning disable 169
		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeKeyword)]
		[Name(Constants.CodeAbstractionKeyword)]
		static ClassificationTypeDefinition AbstractionKeyword;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpAbstractMemberName)]
		static ClassificationTypeDefinition AbstractMember;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpAliasNamespaceName)]
		static ClassificationTypeDefinition AliasNamespace;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpAttributeName)]
		static ClassificationTypeDefinition AttributeName;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpAttributeNotation)]
		static ClassificationTypeDefinition AttributeNotation;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeKeyword)]
		[Name(Constants.CodeBranchingKeyword)]
		static ClassificationTypeDefinition BranchingKeyword;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpConstFieldName)]
		static ClassificationTypeDefinition ConstField;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpConstructorMethodName)]
		static ClassificationTypeDefinition ConstructorMethod;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeKeyword)]
		[Name(Constants.CodeControlFlowKeyword)]
		static ClassificationTypeDefinition ControlFlowKeyword;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpDeclarationName)]
		static ClassificationTypeDefinition Declaration;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodePunctuation)]
		[Name(Constants.CSharpDeclarationBrace)]
		static ClassificationTypeDefinition DeclarationBrace;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpEventName)]
		static ClassificationTypeDefinition Event;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpExtensionMethodName)]
		static ClassificationTypeDefinition ExtensionMethod;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpExternMethodName)]
		static ClassificationTypeDefinition ExternMethod;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpFieldName)]
		static ClassificationTypeDefinition Field;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpLabel)]
		static ClassificationTypeDefinition Label;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpLocalVariableName)]
		static ClassificationTypeDefinition LocalVariable;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeKeyword)]
		[Name(Constants.CodeLoopKeyword)]
		static ClassificationTypeDefinition LoopKeyword;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpMethodName)]
		static ClassificationTypeDefinition Method;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpNamespaceName)]
		static ClassificationTypeDefinition Namespace;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpNestedDeclarationName)]
		static ClassificationTypeDefinition NestedDeclaration;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpOverrideMemberName)]
		static ClassificationTypeDefinition OverrideMember;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpParameterName)]
		static ClassificationTypeDefinition Parameter;
		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpPropertyName)]
		static ClassificationTypeDefinition Property;
		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpReadOnlyFieldName)]
		static ClassificationTypeDefinition ReadOnlyField;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeKeyword)]
		[Name(Constants.CSharpResourceKeyword)]
		static ClassificationTypeDefinition ResourceKeyword;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpSealedClassName)]
		static ClassificationTypeDefinition SealedClass;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpStaticMemberName)]
		static ClassificationTypeDefinition StaticMember;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpTypeParameterName)]
		static ClassificationTypeDefinition TypeParameter;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeIdentifier)]
		[Name(Constants.CSharpVirtualMemberName)]
		static ClassificationTypeDefinition VirtualMember;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpXmlDoc)]
		static ClassificationTypeDefinition XmlDoc;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.CodeSpecialPuctuation)]
		static ClassificationTypeDefinition SpecialPunctuation;
#pragma warning restore 169
	}
}