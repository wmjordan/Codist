using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text.Classification;

namespace Codist.Classifiers
{
	sealed class CSharpClassifications
	{
		readonly IClassificationType _localFieldType;
		readonly IClassificationType _namespaceType;
		readonly IClassificationType _parameterType;
		readonly IClassificationType _extensionMethodType;
		readonly IClassificationType _methodType;
		readonly IClassificationType _eventType;
		readonly IClassificationType _propertyType;
		readonly IClassificationType _fieldType;
		readonly IClassificationType _constFieldType;
		readonly IClassificationType _readonlyFieldType;
		readonly IClassificationType _aliasNamespaceType;
		readonly IClassificationType _constructorMethodType;
		readonly IClassificationType _declarationType;
		readonly IClassificationType _nestedDeclarationType;
		readonly IClassificationType _typeParameterType;
		readonly IClassificationType _staticMemberType;
		readonly IClassificationType _overrideMemberType;
		readonly IClassificationType _virtualMemberType;
		readonly IClassificationType _abstractMemberType;
		readonly IClassificationType _sealedType;
		readonly IClassificationType _externMethodType;
		readonly IClassificationType _labelType;
		readonly IClassificationType _attributeNotationType;
		readonly IClassificationType _controlFlowKeywordType;
		readonly IClassificationType _classNameType;
		readonly IClassificationType _structNameType;
		readonly IClassificationType _interfaceNameType;
		readonly IClassificationType _delegateNameType;
		readonly IClassificationType _enumNameType;
		readonly IClassificationType _declarationBraceType;

		public IClassificationType LocalField => _localFieldType;

		public IClassificationType Namespace => _namespaceType;

		public IClassificationType Parameter => _parameterType;

		public IClassificationType ExtensionMethod => _extensionMethodType;

		public IClassificationType Method => _methodType;

		public IClassificationType Event => _eventType;

		public IClassificationType Property => _propertyType;

		public IClassificationType Field => _fieldType;

		public IClassificationType ConstField => _constFieldType;

		public IClassificationType ReadonlyField => _readonlyFieldType;

		public IClassificationType AliasNamespace => _aliasNamespaceType;

		public IClassificationType ConstructorMethod => _constructorMethodType;

		public IClassificationType Declaration => _declarationType;

		public IClassificationType NestedDeclaration => _nestedDeclarationType;

		public IClassificationType TypeParameter => _typeParameterType;

		public IClassificationType StaticMember => _staticMemberType;

		public IClassificationType OverrideMember => _overrideMemberType;

		public IClassificationType VirtualMember => _virtualMemberType;

		public IClassificationType AbstractMember => _abstractMemberType;

		public IClassificationType SealedMember => _sealedType;

		public IClassificationType ExternMethod => _externMethodType;

		public IClassificationType Label => _labelType;

		public IClassificationType AttributeNotation => _attributeNotationType;

		public IClassificationType ControlFlowKeyword => _controlFlowKeywordType;

		public IClassificationType ClassName => _classNameType;

		public IClassificationType StructName => _structNameType;

		public IClassificationType InterfaceName => _interfaceNameType;

		public IClassificationType DelegateName => _delegateNameType;

		public IClassificationType EnumName => _enumNameType;

		public IClassificationType DeclarationBrace => _declarationBraceType;

		public CSharpClassifications(IClassificationTypeRegistryService registry) {
			_localFieldType = registry.GetClassificationType(Constants.CSharpLocalFieldName);
			_namespaceType = registry.GetClassificationType(Constants.CSharpNamespaceName);
			_parameterType = registry.GetClassificationType(Constants.CSharpParameterName);
			_extensionMethodType = registry.GetClassificationType(Constants.CSharpExtensionMethodName);
			_externMethodType = registry.GetClassificationType(Constants.CSharpExternMethodName);
			_methodType = registry.GetClassificationType(Constants.CSharpMethodName);
			_eventType = registry.GetClassificationType(Constants.CSharpEventName);
			_propertyType = registry.GetClassificationType(Constants.CSharpPropertyName);
			_fieldType = registry.GetClassificationType(Constants.CSharpFieldName);
			_constFieldType = registry.GetClassificationType(Constants.CSharpConstFieldName);
			_readonlyFieldType = registry.GetClassificationType(Constants.CSharpReadOnlyFieldName);
			_aliasNamespaceType = registry.GetClassificationType(Constants.CSharpAliasNamespaceName);
			_constructorMethodType = registry.GetClassificationType(Constants.CSharpConstructorMethodName);
			_declarationType = registry.GetClassificationType(Constants.CSharpDeclarationName);
			_nestedDeclarationType = registry.GetClassificationType(Constants.CSharpNestedDeclarationName);
			_staticMemberType = registry.GetClassificationType(Constants.CSharpStaticMemberName);
			_overrideMemberType = registry.GetClassificationType(Constants.CSharpOverrideMemberName);
			_virtualMemberType = registry.GetClassificationType(Constants.CSharpVirtualMemberName);
			_abstractMemberType = registry.GetClassificationType(Constants.CSharpAbstractMemberName);
			_sealedType = registry.GetClassificationType(Constants.CSharpSealedClassName);
			_typeParameterType = registry.GetClassificationType(Constants.CSharpTypeParameterName);
			_labelType = registry.GetClassificationType(Constants.CSharpLabel);
			_attributeNotationType = registry.GetClassificationType(Constants.CSharpAttributeNotation);
			_controlFlowKeywordType = registry.GetClassificationType(Constants.CodeControlFlowKeyword);
			_classNameType = registry.GetClassificationType(Constants.CodeClassName);
			_structNameType = registry.GetClassificationType(Constants.CodeStructName);
			_interfaceNameType = registry.GetClassificationType(Constants.CodeInterfaceName);
			_delegateNameType = registry.GetClassificationType(Constants.CodeDelegateName);
			_enumNameType = registry.GetClassificationType(Constants.CodeEnumName);
			_declarationBraceType = registry.GetClassificationType(Constants.CSharpDeclarationBrace);
		}
	}
}
