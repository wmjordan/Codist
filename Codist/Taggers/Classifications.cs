using System;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
{
	sealed class CSharpClassifications
	{
		public static readonly CSharpClassifications Instance = new CSharpClassifications(ServicesHelper.Instance.ClassificationTypeRegistry);

		CSharpClassifications(IClassificationTypeRegistryService registry) {
			AbstractMember = registry.GetClassificationTag(Constants.CSharpAbstractMemberName);
			AbstractionKeyword = registry.GetClassificationTag(Constants.CSharpAbstractionKeyword);
			AliasNamespace = registry.GetClassificationTag(Constants.CSharpAliasNamespaceName);
			AttributeName = registry.GetClassificationTag(Constants.CSharpAttributeName);
			AttributeNotation = registry.GetClassificationTag(Constants.CSharpAttributeNotation);
			ClassName = registry.GetClassificationTag(Constants.CodeClassName);
			ConstField = registry.GetClassificationTag(Constants.CSharpConstFieldName);
			ConstructorMethod = registry.GetClassificationTag(Constants.CSharpConstructorMethodName);
			Declaration = registry.GetClassificationTag(Constants.CSharpDeclarationName);
			DeclarationBrace = registry.GetClassificationTag(Constants.CSharpDeclarationBrace);
			DelegateName = registry.GetClassificationTag(Constants.CodeDelegateName);
			EnumName = registry.GetClassificationTag(Constants.CodeEnumName);
			EnumField = registry.GetClassificationTag(Constants.CSharpEnumFieldName);
			Event = registry.GetClassificationTag(Constants.CSharpEventName);
			ExtensionMethod = registry.GetClassificationTag(Constants.CSharpExtensionMethodName);
			ExternMethod = registry.GetClassificationTag(Constants.CSharpExternMethodName);
			Field = registry.GetClassificationTag(Constants.CSharpFieldName);
			InterfaceName = registry.GetClassificationTag(Constants.CodeInterfaceName);
			Label = registry.GetClassificationTag(Constants.CSharpLabel);
			LocalVariable = registry.GetClassificationTag(Constants.CSharpLocalVariableName);
			LocalDeclaration = registry.GetClassificationTag(Constants.CSharpLocalDeclarationName);
			LocalFunctionDeclaration = registry.GetClassificationTag(Constants.CSharpLocalFunctionDeclarationName);
			Method = registry.GetClassificationTag(Constants.CSharpMethodName);
			Namespace = registry.GetClassificationTag(Constants.CSharpNamespaceName);
			NestedDeclaration = registry.GetClassificationTag(Constants.CSharpMemberDeclarationName);
			NestedType = registry.GetClassificationTag(Constants.CSharpNestedTypeName);
			OverrideMember = registry.GetClassificationTag(Constants.CSharpOverrideMemberName);
			Parameter = registry.GetClassificationTag(Constants.CSharpParameterName);
			Property = registry.GetClassificationTag(Constants.CSharpPropertyName);
			ReadOnlyField = registry.GetClassificationTag(Constants.CSharpReadOnlyFieldName);
			ReadOnlyStruct = registry.GetClassificationTag(Constants.CSharpReadOnlyStructName);
			RefStruct = registry.GetClassificationTag(Constants.CSharpRefStructName);
			ResourceKeyword = registry.GetClassificationTag(Constants.CSharpResourceKeyword);
			PrivateMember = registry.GetClassificationTag(Constants.CSharpPrivateMemberName);
			SealedMember = registry.GetClassificationTag(Constants.CSharpSealedMemberName);
			StaticMember = registry.GetClassificationTag(Constants.CSharpStaticMemberName);
			StructName = registry.GetClassificationTag(Constants.CodeStructName);
			TypeParameter = registry.GetClassificationTag(Constants.CSharpTypeParameterName);
			VariableCapturedExpression = registry.GetClassificationTag(Constants.CSharpVariableCapturedExpression);
			VirtualMember = registry.GetClassificationTag(Constants.CSharpVirtualMemberName);
			VolatileField = registry.GetClassificationTag(Constants.CSharpVolatileFieldName);
			XmlDoc = registry.GetClassificationTag(Constants.CSharpXmlDoc);
			UserSymbol = registry.GetClassificationTag(Constants.CSharpUserSymbol);
			MetadataSymbol = registry.GetClassificationTag(Constants.CSharpMetadataSymbol);
		}

		public ClassificationTag AbstractMember { get; }

		public ClassificationTag AbstractionKeyword { get; }

		public ClassificationTag AliasNamespace { get; }

		public ClassificationTag AttributeName { get; }

		public ClassificationTag AttributeNotation { get; }

		public ClassificationTag ClassName { get; }

		public ClassificationTag ConstField { get; }

		public ClassificationTag ConstructorMethod { get; }

		public ClassificationTag Declaration { get; }

		public ClassificationTag DeclarationBrace { get; }

		public ClassificationTag DelegateName { get; }

		public ClassificationTag EnumName { get; }

		public ClassificationTag EnumField { get; }

		public ClassificationTag Event { get; }

		public ClassificationTag ExtensionMethod { get; }

		public ClassificationTag ExternMethod { get; }

		public ClassificationTag Field { get; }

		public ClassificationTag InterfaceName { get; }

		public ClassificationTag Label { get; }

		public ClassificationTag LocalVariable { get; }

		public ClassificationTag LocalDeclaration { get; }

		public ClassificationTag LocalFunctionDeclaration { get; }

		public ClassificationTag Method { get; }

		public ClassificationTag MetadataSymbol { get; }

		public ClassificationTag Namespace { get; }

		public ClassificationTag NestedDeclaration { get; }

		public ClassificationTag NestedType { get; }

		public ClassificationTag OverrideMember { get; }

		public ClassificationTag Parameter { get; }

		public ClassificationTag PrivateMember { get; }

		public ClassificationTag Property { get; }

		public ClassificationTag ReadOnlyField { get; }

		public ClassificationTag ReadOnlyStruct { get; }

		public ClassificationTag RefStruct { get; }

		public ClassificationTag ResourceKeyword { get; }

		public ClassificationTag SealedMember { get; }

		public ClassificationTag StaticMember { get; }

		public ClassificationTag StructName { get; }

		public ClassificationTag TypeParameter { get; }

		public ClassificationTag UserSymbol { get; }

		public ClassificationTag VariableCapturedExpression { get; }

		public ClassificationTag VirtualMember { get; }

		public ClassificationTag VolatileField { get; }

		public ClassificationTag XmlDoc { get; }
	}

	sealed class GeneralClassifications
	{
		public static readonly GeneralClassifications Instance = new GeneralClassifications(ServicesHelper.Instance.ClassificationTypeRegistry);

		GeneralClassifications(IClassificationTypeRegistryService registry) {
			BranchingKeyword = registry.GetClassificationTag(Constants.CSharpBranchingKeyword);
			ControlFlowKeyword = registry.GetClassificationTag(Constants.CSharpControlFlowKeyword);
			Identifier = registry.GetClassificationTag(Constants.CodeIdentifier);
			LoopKeyword = registry.GetClassificationTag(Constants.CSharpLoopKeyword);
			TypeCastKeyword = registry.GetClassificationTag(Constants.CSharpTypeCastKeyword);
			Punctuation = registry.GetClassificationTag(Constants.CodePunctuation);
			Keyword = registry.GetClassificationTag(Constants.CodeKeyword);
			Bold = registry.GetClassificationTag(Constants.CodeBold);
		}

		public ClassificationTag BranchingKeyword { get; }
		public ClassificationTag ControlFlowKeyword { get; }
		public ClassificationTag Identifier { get; }
		public ClassificationTag LoopKeyword { get; }
		public ClassificationTag TypeCastKeyword { get; }
		public ClassificationTag Keyword { get; }
		public ClassificationTag Punctuation { get; }
		public ClassificationTag Bold { get; }
	}

	sealed class HighlightClassifications
	{
		public static readonly HighlightClassifications Instance = new HighlightClassifications(ServicesHelper.Instance.ClassificationTypeRegistry);

		HighlightClassifications(IClassificationTypeRegistryService registry) {
			Highlight1 = registry.GetClassificationTag(Constants.Highlight1);
			Highlight2 = registry.GetClassificationTag(Constants.Highlight2);
			Highlight3 = registry.GetClassificationTag(Constants.Highlight3);
			Highlight4 = registry.GetClassificationTag(Constants.Highlight4);
			Highlight5 = registry.GetClassificationTag(Constants.Highlight5);
			Highlight6 = registry.GetClassificationTag(Constants.Highlight6);
			Highlight7 = registry.GetClassificationTag(Constants.Highlight7);
			Highlight8 = registry.GetClassificationTag(Constants.Highlight8);
			Highlight9 = registry.GetClassificationTag(Constants.Highlight9);
		}
		public ClassificationTag Highlight1 { get; }
		public ClassificationTag Highlight2 { get; }
		public ClassificationTag Highlight3 { get; }
		public ClassificationTag Highlight4 { get; }
		public ClassificationTag Highlight5 { get; }
		public ClassificationTag Highlight6 { get; }
		public ClassificationTag Highlight7 { get; }
		public ClassificationTag Highlight8 { get; }
		public ClassificationTag Highlight9 { get; }
	}
}
