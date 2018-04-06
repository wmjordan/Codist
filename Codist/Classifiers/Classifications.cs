using System;
using Microsoft.VisualStudio.Text.Classification;

namespace Codist.Classifiers
{
	sealed class GeneralClassifications
	{
		readonly IClassificationType _identifier;
		readonly IClassificationType _punctuation;
		readonly IClassificationType _keyword;

		public GeneralClassifications(IClassificationTypeRegistryService registry) {
			_identifier = registry.GetClassificationType(Constants.CodeIdentifier);
			_punctuation = registry.GetClassificationType(Constants.CodePunctuation);
			_keyword = registry.GetClassificationType(Constants.CodeKeyword);
		}

		public IClassificationType Identifier => _identifier;
		public IClassificationType Punctuation => _punctuation;
		public IClassificationType Keyword => _keyword;
	}

	sealed class CSharpClassifications
	{
		readonly IClassificationType _abstractMember;
		readonly IClassificationType _aliasNamespace;
		readonly IClassificationType _attributeNotation;
		readonly IClassificationType _className;
		readonly IClassificationType _constField;
		readonly IClassificationType _constructorMethod;
		readonly IClassificationType _controlFlowKeyword;
		readonly IClassificationType _declaration;
		readonly IClassificationType _declarationBrace;
		readonly IClassificationType _delegateName;
		readonly IClassificationType _enumName;
		readonly IClassificationType _event;
		readonly IClassificationType _extensionMethod;
		readonly IClassificationType _externMethod;
		readonly IClassificationType _field;
		readonly IClassificationType _interfaceName;
		readonly IClassificationType _label;
		readonly IClassificationType _localField;
		readonly IClassificationType _method;
		readonly IClassificationType _namespace;
		readonly IClassificationType _nestedDeclaration;
		readonly IClassificationType _overrideMember;
		readonly IClassificationType _parameter;
		readonly IClassificationType _property;
		readonly IClassificationType _readonlyField;
		readonly IClassificationType _sealed;
		readonly IClassificationType _staticMember;
		readonly IClassificationType _structName;
		readonly IClassificationType _typeParameter;
		readonly IClassificationType _virtualMember;

		public IClassificationType AbstractMember => _abstractMember;
		public IClassificationType AliasNamespace => _aliasNamespace;
		public IClassificationType AttributeNotation => _attributeNotation;
		public IClassificationType ClassName => _className;
		public IClassificationType ConstField => _constField;
		public IClassificationType ConstructorMethod => _constructorMethod;
		public IClassificationType ControlFlowKeyword => _controlFlowKeyword;
		public IClassificationType Declaration => _declaration;
		public IClassificationType DeclarationBrace => _declarationBrace;
		public IClassificationType DelegateName => _delegateName;
		public IClassificationType EnumName => _enumName;
		public IClassificationType Event => _event;
		public IClassificationType ExtensionMethod => _extensionMethod;
		public IClassificationType ExternMethod => _externMethod;
		public IClassificationType Field => _field;
		public IClassificationType InterfaceName => _interfaceName;
		public IClassificationType Label => _label;
		public IClassificationType LocalField => _localField;
		public IClassificationType Method => _method;
		public IClassificationType Namespace => _namespace;
		public IClassificationType NestedDeclaration => _nestedDeclaration;
		public IClassificationType OverrideMember => _overrideMember;
		public IClassificationType Parameter => _parameter;
		public IClassificationType Property => _property;
		public IClassificationType ReadonlyField => _readonlyField;
		public IClassificationType SealedMember => _sealed;
		public IClassificationType StaticMember => _staticMember;
		public IClassificationType StructName => _structName;
		public IClassificationType TypeParameter => _typeParameter;
		public IClassificationType VirtualMember => _virtualMember;

		public CSharpClassifications(IClassificationTypeRegistryService registry) {
			_abstractMember = registry.GetClassificationType(Constants.CSharpAbstractMemberName);
			_aliasNamespace = registry.GetClassificationType(Constants.CSharpAliasNamespaceName);
			_attributeNotation = registry.GetClassificationType(Constants.CSharpAttributeNotation);
			_className = registry.GetClassificationType(Constants.CodeClassName);
			_constField = registry.GetClassificationType(Constants.CSharpConstFieldName);
			_constructorMethod = registry.GetClassificationType(Constants.CSharpConstructorMethodName);
			_controlFlowKeyword = registry.GetClassificationType(Constants.CodeControlFlowKeyword);
			_declaration = registry.GetClassificationType(Constants.CSharpDeclarationName);
			_declarationBrace = registry.GetClassificationType(Constants.CSharpDeclarationBrace);
			_delegateName = registry.GetClassificationType(Constants.CodeDelegateName);
			_enumName = registry.GetClassificationType(Constants.CodeEnumName);
			_event = registry.GetClassificationType(Constants.CSharpEventName);
			_extensionMethod = registry.GetClassificationType(Constants.CSharpExtensionMethodName);
			_externMethod = registry.GetClassificationType(Constants.CSharpExternMethodName);
			_field = registry.GetClassificationType(Constants.CSharpFieldName);
			_interfaceName = registry.GetClassificationType(Constants.CodeInterfaceName);
			_label = registry.GetClassificationType(Constants.CSharpLabel);
			_localField = registry.GetClassificationType(Constants.CSharpLocalFieldName);
			_method = registry.GetClassificationType(Constants.CSharpMethodName);
			_namespace = registry.GetClassificationType(Constants.CSharpNamespaceName);
			_nestedDeclaration = registry.GetClassificationType(Constants.CSharpNestedDeclarationName);
			_overrideMember = registry.GetClassificationType(Constants.CSharpOverrideMemberName);
			_parameter = registry.GetClassificationType(Constants.CSharpParameterName);
			_property = registry.GetClassificationType(Constants.CSharpPropertyName);
			_readonlyField = registry.GetClassificationType(Constants.CSharpReadOnlyFieldName);
			_sealed = registry.GetClassificationType(Constants.CSharpSealedClassName);
			_staticMember = registry.GetClassificationType(Constants.CSharpStaticMemberName);
			_structName = registry.GetClassificationType(Constants.CodeStructName);
			_typeParameter = registry.GetClassificationType(Constants.CSharpTypeParameterName);
			_virtualMember = registry.GetClassificationType(Constants.CSharpVirtualMemberName);
		}
	}
}
