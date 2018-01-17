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
		[BaseDefinition(Constants.CodeKeyword)]
		[Name(Constants.CodeControlFlowKeyword)]
		static ClassificationTypeDefinition ControlFlowKeyword;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpNamespaceName)]
		private static ClassificationTypeDefinition Namespace;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpParameterName)]
		private static ClassificationTypeDefinition Parameter;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpExtensionMethodName)]
		private static ClassificationTypeDefinition ExtensionMethod;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpExternMethodName)]
		private static ClassificationTypeDefinition ExternMethod;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpMethodName)]
		private static ClassificationTypeDefinition Method;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpEventName)]
		private static ClassificationTypeDefinition Event;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpPropertyName)]
		private static ClassificationTypeDefinition Property;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpFieldName)]
		private static ClassificationTypeDefinition Field;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpLocalFieldName)]
		private static ClassificationTypeDefinition LocalField;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpConstFieldName)]
		private static ClassificationTypeDefinition ConstField;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpReadOnlyFieldName)]
		private static ClassificationTypeDefinition ReadOnlyField;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpAliasNamespaceName)]
		private static ClassificationTypeDefinition AliasNamespace;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpConstructorMethodName)]
		private static ClassificationTypeDefinition ConstructorMethod;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpDeclarationName)]
		private static ClassificationTypeDefinition Declaration;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpNestedDeclarationName)]
		private static ClassificationTypeDefinition NestedDeclaration;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpStaticMemberName)]
		private static ClassificationTypeDefinition StaticMember;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpOverrideMemberName)]
		private static ClassificationTypeDefinition OverrideMember;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpAbstractMemberName)]
		private static ClassificationTypeDefinition AbstractMember;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpVirtualMemberName)]
		private static ClassificationTypeDefinition VirtualMember;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpSealedClassName)]
		private static ClassificationTypeDefinition SealedClass;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpTypeParameterName)]
		private static ClassificationTypeDefinition TypeParameter;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpLabel)]
		private static ClassificationTypeDefinition Label;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeFormalLanguage)]
		[Name(Constants.CSharpAttributeNotation)]
		private static ClassificationTypeDefinition AttributeNotation;
#pragma warning restore 169
	}
}