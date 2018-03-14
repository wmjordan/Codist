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
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpAbstractMemberName)]
		static ClassificationTypeDefinition AbstractMember;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpAliasNamespaceName)]
		static ClassificationTypeDefinition AliasNamespace;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpAttributeNotation)]
		static ClassificationTypeDefinition AttributeNotation;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpConstFieldName)]
		static ClassificationTypeDefinition ConstField;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpConstructorMethodName)]
		static ClassificationTypeDefinition ConstructorMethod;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeKeyword)]
		[Name(Constants.CodeControlFlowKeyword)]
		static ClassificationTypeDefinition ControlFlowKeyword;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpDeclarationName)]
		static ClassificationTypeDefinition Declaration;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpDeclarationBrace)]
		static ClassificationTypeDefinition DeclarationBrace;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpEventName)]
		static ClassificationTypeDefinition Event;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpExtensionMethodName)]
		static ClassificationTypeDefinition ExtensionMethod;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpExternMethodName)]
		static ClassificationTypeDefinition ExternMethod;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpFieldName)]
		static ClassificationTypeDefinition Field;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpLabel)]
		static ClassificationTypeDefinition Label;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpLocalFieldName)]
		static ClassificationTypeDefinition LocalField;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpMethodName)]
		static ClassificationTypeDefinition Method;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpMethodBody)]
		static ClassificationTypeDefinition MethodBody;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpNamespaceName)]
		static ClassificationTypeDefinition Namespace;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpNestedDeclarationName)]
		static ClassificationTypeDefinition NestedDeclaration;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpOverrideMemberName)]
		static ClassificationTypeDefinition OverrideMember;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpParameterName)]
		static ClassificationTypeDefinition Parameter;
		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpPropertyName)]
		static ClassificationTypeDefinition Property;
		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpReadOnlyFieldName)]
		static ClassificationTypeDefinition ReadOnlyField;
		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpSealedClassName)]
		static ClassificationTypeDefinition SealedClass;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpStaticMemberName)]
		static ClassificationTypeDefinition StaticMember;
		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpTypeParameterName)]
		static ClassificationTypeDefinition TypeParameter;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpVirtualMemberName)]
		static ClassificationTypeDefinition VirtualMember;
#pragma warning restore 169
	}
}