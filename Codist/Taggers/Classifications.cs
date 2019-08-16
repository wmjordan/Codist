using System;
using Microsoft.VisualStudio.Text.Classification;

namespace Codist.Taggers
{
	sealed class CSharpClassifications
	{
		public CSharpClassifications(IClassificationTypeRegistryService registry) {
			AbstractMember = registry.GetClassificationType(Constants.CSharpAbstractMemberName);
			AbstractionKeyword = registry.GetClassificationType(Constants.CodeAbstractionKeyword);
			AliasNamespace = registry.GetClassificationType(Constants.CSharpAliasNamespaceName);
			AttributeName = registry.GetClassificationType(Constants.CSharpAttributeName);
			AttributeNotation = registry.GetClassificationType(Constants.CSharpAttributeNotation);
			ClassName = registry.GetClassificationType(Constants.CodeClassName);
			ConstField = registry.GetClassificationType(Constants.CSharpConstFieldName);
			ConstructorMethod = registry.GetClassificationType(Constants.CSharpConstructorMethodName);
			Declaration = registry.GetClassificationType(Constants.CSharpDeclarationName);
			DeclarationBrace = registry.GetClassificationType(Constants.CSharpDeclarationBrace);
			DelegateName = registry.GetClassificationType(Constants.CodeDelegateName);
			EnumName = registry.GetClassificationType(Constants.CodeEnumName);
			Event = registry.GetClassificationType(Constants.CSharpEventName);
			ExtensionMethod = registry.GetClassificationType(Constants.CSharpExtensionMethodName);
			ExternMethod = registry.GetClassificationType(Constants.CSharpExternMethodName);
			Field = registry.GetClassificationType(Constants.CSharpFieldName);
			InterfaceName = registry.GetClassificationType(Constants.CodeInterfaceName);
			Label = registry.GetClassificationType(Constants.CSharpLabel);
			LocalVariable = registry.GetClassificationType(Constants.CSharpLocalVariableName);
			LocalDeclaration = registry.GetClassificationType(Constants.CSharpLocalDeclarationName);
			Method = registry.GetClassificationType(Constants.CSharpMethodName);
			Namespace = registry.GetClassificationType(Constants.CSharpNamespaceName);
			NestedDeclaration = registry.GetClassificationType(Constants.CSharpNestedDeclarationName);
			OverrideMember = registry.GetClassificationType(Constants.CSharpOverrideMemberName);
			Parameter = registry.GetClassificationType(Constants.CSharpParameterName);
			Property = registry.GetClassificationType(Constants.CSharpPropertyName);
			ReadonlyField = registry.GetClassificationType(Constants.CSharpReadOnlyFieldName);
			ResourceKeyword = registry.GetClassificationType(Constants.CSharpResourceKeyword);
			SealedMember = registry.GetClassificationType(Constants.CSharpSealedClassName);
			StaticMember = registry.GetClassificationType(Constants.CSharpStaticMemberName);
			StructName = registry.GetClassificationType(Constants.CodeStructName);
			TypeParameter = registry.GetClassificationType(Constants.CSharpTypeParameterName);
			VirtualMember = registry.GetClassificationType(Constants.CSharpVirtualMemberName);
			VolatileField = registry.GetClassificationType(Constants.CSharpVolatileFieldName);
			XmlDoc = registry.GetClassificationType(Constants.CSharpXmlDoc);
			UserSymbol = registry.GetClassificationType(Constants.CSharpUserSymbol);
			MetadataSymbol = registry.GetClassificationType(Constants.CSharpMetadataSymbol);
		}

		public IClassificationType AbstractMember { get; }

		public IClassificationType AbstractionKeyword { get; }

		public IClassificationType AliasNamespace { get; }

		public IClassificationType AttributeName { get; }

		public IClassificationType AttributeNotation { get; }

		public IClassificationType ClassName { get; }

		public IClassificationType ConstField { get; }

		public IClassificationType ConstructorMethod { get; }

		public IClassificationType Declaration { get; }

		public IClassificationType DeclarationBrace { get; }

		public IClassificationType DelegateName { get; }

		public IClassificationType EnumName { get; }

		public IClassificationType Event { get; }

		public IClassificationType ExtensionMethod { get; }

		public IClassificationType ExternMethod { get; }

		public IClassificationType Field { get; }

		public IClassificationType InterfaceName { get; }

		public IClassificationType Label { get; }

		public IClassificationType LocalVariable { get; }

		public IClassificationType LocalDeclaration { get; }

		public IClassificationType Method { get; }

		public IClassificationType MetadataSymbol { get; }

		public IClassificationType Namespace { get; }

		public IClassificationType NestedDeclaration { get; }

		public IClassificationType OverrideMember { get; }

		public IClassificationType Parameter { get; }

		public IClassificationType Property { get; }

		public IClassificationType ReadonlyField { get; }

		public IClassificationType ResourceKeyword { get; }

		public IClassificationType SealedMember { get; }

		public IClassificationType StaticMember { get; }

		public IClassificationType StructName { get; }

		public IClassificationType TypeParameter { get; }

		public IClassificationType UserSymbol { get; }

		public IClassificationType VirtualMember { get; }

		public IClassificationType VolatileField { get; }

		public IClassificationType XmlDoc { get; }
	}

	sealed class GeneralClassifications
	{
		public GeneralClassifications(IClassificationTypeRegistryService registry) {
			BranchingKeyword = registry.GetClassificationType(Constants.CodeBranchingKeyword);
			ControlFlowKeyword = registry.GetClassificationType(Constants.CodeControlFlowKeyword);
			Identifier = registry.GetClassificationType(Constants.CodeIdentifier);
			LoopKeyword = registry.GetClassificationType(Constants.CodeLoopKeyword);
			TypeCastKeyword = registry.GetClassificationType(Constants.CodeTypeCastKeyword);
			Punctuation = registry.GetClassificationType(Constants.CodePunctuation);
			Keyword = registry.GetClassificationType(Constants.CodeKeyword);
			SpecialPunctuation = registry.GetClassificationType(Constants.CodeSpecialPunctuation);
		}

		public IClassificationType BranchingKeyword { get; }
		public IClassificationType ControlFlowKeyword { get; }
		public IClassificationType Identifier { get; }
		public IClassificationType LoopKeyword { get; }
		public IClassificationType TypeCastKeyword { get; }
		public IClassificationType Keyword { get; }
		public IClassificationType Punctuation { get; }
		public IClassificationType SpecialPunctuation { get; }
	}

	sealed class HighlightClassifications
	{
		public HighlightClassifications(IClassificationTypeRegistryService registry) {
			Highlight1 = registry.GetClassificationType(Constants.Highlight1);
			Highlight2 = registry.GetClassificationType(Constants.Highlight2);
			Highlight3 = registry.GetClassificationType(Constants.Highlight3);
			Highlight4 = registry.GetClassificationType(Constants.Highlight4);
			Highlight5 = registry.GetClassificationType(Constants.Highlight5);
			Highlight6 = registry.GetClassificationType(Constants.Highlight6);
			Highlight7 = registry.GetClassificationType(Constants.Highlight7);
			Highlight8 = registry.GetClassificationType(Constants.Highlight8);
			Highlight9 = registry.GetClassificationType(Constants.Highlight9);
		}
		public IClassificationType Highlight1 { get; }
		public IClassificationType Highlight2 { get; }
		public IClassificationType Highlight3 { get; }
		public IClassificationType Highlight4 { get; }
		public IClassificationType Highlight5 { get; }
		public IClassificationType Highlight6 { get; }
		public IClassificationType Highlight7 { get; }
		public IClassificationType Highlight8 { get; }
		public IClassificationType Highlight9 { get; }
	}
}
