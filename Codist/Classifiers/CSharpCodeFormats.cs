using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Classifiers
{
	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CodeControlFlowKeyword)]
	[Name(Constants.CodeControlFlowKeyword)]
	[UserVisible(false)]
	[Order(After = Constants.CodeKeyword)]
	[Order(After = Constants.CodeKeywordControl)]
	sealed class ControlFlowKeywordFormat : ClassificationFormatDefinition
	{
		public ControlFlowKeywordFormat() {
			DisplayName = Constants.CodeControlFlowKeyword;
			IsBold = true;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CodeAbstractionKeyword)]
	[Name(Constants.CodeAbstractionKeyword)]
	[UserVisible(false)]
	[Order(After = Constants.CodeKeyword)]
	sealed class AbstractionKeywordFormat : ClassificationFormatDefinition
	{
		public AbstractionKeywordFormat() {
			DisplayName = Constants.CodeAbstractionKeyword;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CodeBranchingKeyword)]
	[Name(Constants.CodeBranchingKeyword)]
	[UserVisible(false)]
	[Order(After = Constants.CodeKeyword)]
	[Order(After = Constants.CodeKeywordControl)]
	sealed class BranchingKeywordFormat : ClassificationFormatDefinition
	{
		public BranchingKeywordFormat() {
			DisplayName = Constants.CodeBranchingKeyword;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CodeLoopKeyword)]
	[Name(Constants.CodeLoopKeyword)]
	[UserVisible(false)]
	[Order(After = Constants.CodeKeyword)]
	sealed class LoopKeywordFormat : ClassificationFormatDefinition
	{
		public LoopKeywordFormat() {
			DisplayName = Constants.CodeLoopKeyword;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CodeTypeCastKeyword)]
	[Name(Constants.CodeTypeCastKeyword)]
	[UserVisible(false)]
	[Order(After = Constants.CodeKeyword)]
	sealed class TypeCastKeywordFormat : ClassificationFormatDefinition
	{
		public TypeCastKeywordFormat() {
			DisplayName = Constants.CodeTypeCastKeyword;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpResourceKeyword)]
	[Name(Constants.CSharpResourceKeyword)]
	[UserVisible(false)]
	[Order(After = Constants.CodeKeyword)]
	[Order(After = Constants.CodeBranchingKeyword)]
	sealed class ResourceKeywordFormat : ClassificationFormatDefinition
	{
		public ResourceKeywordFormat() {
			DisplayName = Constants.CSharpResourceKeyword;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CodeSpecialPuctuation)]
	[Name(Constants.CodeSpecialPuctuation)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	sealed class SpecialPuctuationFormat : ClassificationFormatDefinition
	{
		public SpecialPuctuationFormat() {
			DisplayName = Constants.CodeSpecialPuctuation;
			IsBold = true;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpLocalVariableName)]
	[Name(Constants.CSharpLocalVariableName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeLocalName)]
	sealed class LocalVariableFormat : ClassificationFormatDefinition
	{
		public LocalVariableFormat() {
			DisplayName = Constants.NameOfMe + ": local variable";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpConstFieldName)]
	[Name(Constants.CSharpConstFieldName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeStaticSymbol)]
	[Order(After = Constants.CodeConstantName)]
	sealed class ConstFieldFormat : ClassificationFormatDefinition
	{
		public ConstFieldFormat() {
			DisplayName = Constants.NameOfMe + ": const field";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpReadOnlyFieldName)]
	[Name(Constants.CSharpReadOnlyFieldName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeFieldName)]
	sealed class ReadOnlyFieldFormat : ClassificationFormatDefinition
	{
		public ReadOnlyFieldFormat() {
			DisplayName = Constants.NameOfMe + ": readonly field";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpVolatileFieldName)]
	[Name(Constants.CSharpVolatileFieldName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeFieldName)]
	sealed class VolatileFieldFormat : ClassificationFormatDefinition
	{
		public VolatileFieldFormat() {
			DisplayName = Constants.NameOfMe + ": volatile field";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpParameterName)]
	[Name(Constants.CSharpParameterName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeParameterName)]
	sealed class ParameterFormat : ClassificationFormatDefinition
	{
		public ParameterFormat() {
			DisplayName = Constants.NameOfMe + ": parameter";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpTypeParameterName)]
	[Name(Constants.CSharpTypeParameterName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeTypeParameterName)]
	sealed class TypeParameterFormat : ClassificationFormatDefinition
	{
		public TypeParameterFormat() {
			DisplayName = Constants.NameOfMe + ": type parameter";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpNamespaceName)]
	[Name(Constants.CSharpNamespaceName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeNamespaceName)]
	sealed class NamespaceFormat : ClassificationFormatDefinition
	{
		public NamespaceFormat() {
			DisplayName = Constants.NameOfMe + ": namespace";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpExtensionMethodName)]
	[Name(Constants.CSharpExtensionMethodName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeExtensionMethodName)]
	sealed class ExtensionMethodFormat : ClassificationFormatDefinition
	{
		public ExtensionMethodFormat() {
			DisplayName = Constants.NameOfMe + ": extension method";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpExternMethodName)]
	[Name(Constants.CSharpExternMethodName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeMethodName)]
	sealed class ExternMethodFormat : ClassificationFormatDefinition
	{
		public ExternMethodFormat() {
			DisplayName = Constants.NameOfMe + ": extern method";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpMethodName)]
	[Name(Constants.CSharpMethodName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeMethodName)]
	sealed class MethodFormat : ClassificationFormatDefinition
	{
		public MethodFormat() {
			DisplayName = Constants.NameOfMe + ": method";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpEventName)]
	[Name(Constants.CSharpEventName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	sealed class EventFormat : ClassificationFormatDefinition
	{
		public EventFormat() {
			DisplayName = Constants.NameOfMe + ": event";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpPropertyName)]
	[Name(Constants.CSharpPropertyName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodePropertyName)]
	sealed class PropertyFormat : ClassificationFormatDefinition
	{
		public PropertyFormat() {
			DisplayName = Constants.NameOfMe + ": property";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpFieldName)]
	[Name(Constants.CSharpFieldName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeFieldName)]
	sealed class FieldFormat : ClassificationFormatDefinition
	{
		public FieldFormat() {
			DisplayName = Constants.NameOfMe + ": field";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpAliasNamespaceName)]
	[Name(Constants.CSharpAliasNamespaceName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	sealed class AliasNamespaceFormat : ClassificationFormatDefinition
	{
		public AliasNamespaceFormat() {
			DisplayName = Constants.NameOfMe + ": alias namespace";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpConstructorMethodName)]
	[Name(Constants.CSharpConstructorMethodName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeMethodName)]
	sealed class ConstructorMethodFormat : ClassificationFormatDefinition
	{
		public ConstructorMethodFormat() {
			DisplayName = Constants.NameOfMe + ": constructor method";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpDeclarationName)]
	[Name(Constants.CSharpDeclarationName)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	sealed class DeclarationFormat : ClassificationFormatDefinition
	{
		public DeclarationFormat() {
			DisplayName = Constants.NameOfMe + ": declaration";
			IsBold = true;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpDeclarationBrace)]
	[Name(Constants.CSharpDeclarationBrace)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	sealed class DeclarationBraceFormat : ClassificationFormatDefinition
	{
		public DeclarationBraceFormat() {
			DisplayName = Constants.NameOfMe + ": declaration brace";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpNestedDeclarationName)]
	[Name(Constants.CSharpNestedDeclarationName)]
	[UserVisible(false)]
	[Order(After = Constants.CSharpDeclarationName)]
	sealed class NestedDeclarationFormat : ClassificationFormatDefinition
	{
		public NestedDeclarationFormat() {
			DisplayName = Constants.NameOfMe + ": nested declaration";
			IsBold = true;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpLocalDeclarationName)]
	[Name(Constants.CSharpLocalDeclarationName)]
	[UserVisible(false)]
	[Order(After = Constants.CSharpDeclarationName)]
	sealed class LocalDeclarationFormat : ClassificationFormatDefinition
	{
		public LocalDeclarationFormat() {
			DisplayName = Constants.NameOfMe + ": local declaration";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpStaticMemberName)]
	[Name(Constants.CSharpStaticMemberName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeStaticSymbol)]
	sealed class StaticMemberFormat : ClassificationFormatDefinition
	{
		public StaticMemberFormat() {
			DisplayName = Constants.NameOfMe + ": static member";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpOverrideMemberName)]
	[Name(Constants.CSharpOverrideMemberName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	sealed class OverrideMemberFormat : ClassificationFormatDefinition
	{
		public OverrideMemberFormat() {
			DisplayName = Constants.NameOfMe + ": override member";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpAbstractMemberName)]
	[Name(Constants.CSharpAbstractMemberName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	sealed class AbstractMemberFormat : ClassificationFormatDefinition
	{
		public AbstractMemberFormat() {
			DisplayName = Constants.NameOfMe + ": abstract member";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpVirtualMemberName)]
	[Name(Constants.CSharpVirtualMemberName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	sealed class VirtualMemberFormat : ClassificationFormatDefinition
	{
		public VirtualMemberFormat() {
			DisplayName = Constants.NameOfMe + ": virtual member";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpSealedClassName)]
	[Name(Constants.CSharpSealedClassName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	sealed class SealedClassFormat : ClassificationFormatDefinition
	{
		public SealedClassFormat() {
			DisplayName = Constants.NameOfMe + ": sealed class";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpLabel)]
	[Name(Constants.CSharpLabel)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeLabelName)]
	sealed class LabelFormat : ClassificationFormatDefinition
	{
		public LabelFormat() {
			DisplayName = Constants.NameOfMe + ": label";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpAttributeName)]
	[Name(Constants.CSharpAttributeName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeMethodName)]
	sealed class AttributeNameFormat : ClassificationFormatDefinition
	{
		public AttributeNameFormat() {
			DisplayName = Constants.NameOfMe + ": attribute name";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpAttributeNotation)]
	[Name(Constants.CSharpAttributeNotation)]
	[UserVisible(false)]
	[Order(Before = Priority.Low)]
	sealed class AttributeNotationFormat : ClassificationFormatDefinition
	{
		public AttributeNotationFormat() {
			DisplayName = Constants.NameOfMe + ": attribute notation";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpXmlDoc)]
	[Name(Constants.CSharpXmlDoc)]
	[UserVisible(false)]
	[Order(Before = Priority.Low)]
	sealed class XmlDocFormat : ClassificationFormatDefinition
	{
		public XmlDocFormat() {
			DisplayName = Constants.NameOfMe + ": xml doc";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpMetadataSymbol)]
	[Name(Constants.CSharpMetadataSymbol)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	sealed class MetadataSymbol : ClassificationFormatDefinition
	{
		public MetadataSymbol() {
			DisplayName = Constants.NameOfMe + ": metadata symbol";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpUserSymbol)]
	[Name(Constants.CSharpUserSymbol)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	sealed class UserSymbol : ClassificationFormatDefinition
	{
		public UserSymbol() {
			DisplayName = Constants.NameOfMe + ": user symbol";
		}
	}
}