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
		readonly IClassificationType _localField;
		readonly IClassificationType _namespace;
		readonly IClassificationType _parameter;
		readonly IClassificationType _extensionMethod;
		readonly IClassificationType _method;
		readonly IClassificationType _event;
		readonly IClassificationType _property;
		readonly IClassificationType _field;
		readonly IClassificationType _constField;
		readonly IClassificationType _readonlyField;
		readonly IClassificationType _aliasNamespace;
		readonly IClassificationType _constructorMethod;
		readonly IClassificationType _declaration;
		readonly IClassificationType _nestedDeclaration;
		readonly IClassificationType _typeParameter;
		readonly IClassificationType _staticMember;
		readonly IClassificationType _overrideMember;
		readonly IClassificationType _virtualMember;
		readonly IClassificationType _abstractMember;
		readonly IClassificationType _sealed;
		readonly IClassificationType _externMethod;
		readonly IClassificationType _label;
		readonly IClassificationType _attributeNotation;
		readonly IClassificationType _controlFlowKeyword;
		readonly IClassificationType _className;
		readonly IClassificationType _structName;
		readonly IClassificationType _interfaceName;
		readonly IClassificationType _delegateName;
		readonly IClassificationType _enumName;
		readonly IClassificationType _declarationBrace;
		//readonly IClassificationType _methodBody;

		public IClassificationType LocalField => _localField;

		public IClassificationType Namespace => _namespace;

		public IClassificationType Parameter => _parameter;

		public IClassificationType ExtensionMethod => _extensionMethod;

		public IClassificationType Method => _method;

		public IClassificationType Event => _event;

		public IClassificationType Property => _property;

		public IClassificationType Field => _field;

		public IClassificationType ConstField => _constField;

		public IClassificationType ReadonlyField => _readonlyField;

		public IClassificationType AliasNamespace => _aliasNamespace;

		public IClassificationType ConstructorMethod => _constructorMethod;

		public IClassificationType Declaration => _declaration;

		public IClassificationType NestedDeclaration => _nestedDeclaration;

		public IClassificationType TypeParameter => _typeParameter;

		public IClassificationType StaticMember => _staticMember;

		public IClassificationType OverrideMember => _overrideMember;

		public IClassificationType VirtualMember => _virtualMember;

		public IClassificationType AbstractMember => _abstractMember;

		public IClassificationType SealedMember => _sealed;

		public IClassificationType ExternMethod => _externMethod;

		public IClassificationType Label => _label;

		public IClassificationType AttributeNotation => _attributeNotation;

		public IClassificationType ControlFlowKeyword => _controlFlowKeyword;

		public IClassificationType ClassName => _className;

		public IClassificationType StructName => _structName;

		public IClassificationType InterfaceName => _interfaceName;

		public IClassificationType DelegateName => _delegateName;

		public IClassificationType EnumName => _enumName;

		public IClassificationType DeclarationBrace => _declarationBrace;

		//public IClassificationType MethodBody => _methodBody;

		public CSharpClassifications(IClassificationTypeRegistryService registry) {
			_localField = registry.GetClassificationType(Constants.CSharpLocalFieldName);
			_namespace = registry.GetClassificationType(Constants.CSharpNamespaceName);
			_parameter = registry.GetClassificationType(Constants.CSharpParameterName);
			_extensionMethod = registry.GetClassificationType(Constants.CSharpExtensionMethodName);
			_externMethod = registry.GetClassificationType(Constants.CSharpExternMethodName);
			_method = registry.GetClassificationType(Constants.CSharpMethodName);
			_event = registry.GetClassificationType(Constants.CSharpEventName);
			_property = registry.GetClassificationType(Constants.CSharpPropertyName);
			_field = registry.GetClassificationType(Constants.CSharpFieldName);
			_constField = registry.GetClassificationType(Constants.CSharpConstFieldName);
			_readonlyField = registry.GetClassificationType(Constants.CSharpReadOnlyFieldName);
			_aliasNamespace = registry.GetClassificationType(Constants.CSharpAliasNamespaceName);
			_constructorMethod = registry.GetClassificationType(Constants.CSharpConstructorMethodName);
			_declaration = registry.GetClassificationType(Constants.CSharpDeclarationName);
			_nestedDeclaration = registry.GetClassificationType(Constants.CSharpNestedDeclarationName);
			_staticMember = registry.GetClassificationType(Constants.CSharpStaticMemberName);
			_overrideMember = registry.GetClassificationType(Constants.CSharpOverrideMemberName);
			_virtualMember = registry.GetClassificationType(Constants.CSharpVirtualMemberName);
			_abstractMember = registry.GetClassificationType(Constants.CSharpAbstractMemberName);
			_sealed = registry.GetClassificationType(Constants.CSharpSealedClassName);
			_typeParameter = registry.GetClassificationType(Constants.CSharpTypeParameterName);
			_label = registry.GetClassificationType(Constants.CSharpLabel);
			_attributeNotation = registry.GetClassificationType(Constants.CSharpAttributeNotation);
			_controlFlowKeyword = registry.GetClassificationType(Constants.CodeControlFlowKeyword);
			_className = registry.GetClassificationType(Constants.CodeClassName);
			_structName = registry.GetClassificationType(Constants.CodeStructName);
			_interfaceName = registry.GetClassificationType(Constants.CodeInterfaceName);
			_delegateName = registry.GetClassificationType(Constants.CodeDelegateName);
			_enumName = registry.GetClassificationType(Constants.CodeEnumName);
			_declarationBrace = registry.GetClassificationType(Constants.CSharpDeclarationBrace);
			//_methodBody = registry.GetClassificationType(Constants.CSharpMethodBody);
		}
	}
}
