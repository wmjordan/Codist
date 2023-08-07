using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Taggers
{
	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpControlFlowKeyword)]
	[Name(Constants.CSharpControlFlowKeyword)]
	[UserVisible(false)]
	[Order(After = Constants.CodeKeyword)]
	[Order(After = Constants.CodeKeywordControl)]
	[Order(Before = Constants.CodeBold)]
	sealed class ControlFlowKeywordFormat : ClassificationFormatDefinition
	{
		public ControlFlowKeywordFormat() {
			DisplayName = Constants.CSharpControlFlowKeyword;
			IsBold = true;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpAbstractionKeyword)]
	[Name(Constants.CSharpAbstractionKeyword)]
	[UserVisible(false)]
	[Order(After = Constants.CodeKeyword)]
	sealed class AbstractionKeywordFormat : ClassificationFormatDefinition
	{
		public AbstractionKeywordFormat() {
			DisplayName = Constants.CSharpAbstractionKeyword;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpBranchingKeyword)]
	[Name(Constants.CSharpBranchingKeyword)]
	[UserVisible(false)]
	[Order(After = Constants.CodeKeyword)]
	[Order(After = Constants.CodeKeywordControl)]
	[Order(Before = Constants.CodeBold)]
	sealed class BranchingKeywordFormat : ClassificationFormatDefinition
	{
		public BranchingKeywordFormat() {
			DisplayName = Constants.CSharpBranchingKeyword;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpLoopKeyword)]
	[Name(Constants.CSharpLoopKeyword)]
	[UserVisible(false)]
	[Order(After = Constants.CodeKeyword)]
	[Order(After = Constants.CodeKeywordControl)]
	[Order(Before = Constants.CodeBold)]
	sealed class LoopKeywordFormat : ClassificationFormatDefinition
	{
		public LoopKeywordFormat() {
			DisplayName = Constants.CSharpLoopKeyword;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpTypeCastKeyword)]
	[Name(Constants.CSharpTypeCastKeyword)]
	[UserVisible(false)]
	[Order(After = Constants.CodeKeyword)]
	[Order(Before = Constants.CodeBold)]
	sealed class TypeCastKeywordFormat : ClassificationFormatDefinition
	{
		public TypeCastKeywordFormat() {
			DisplayName = Constants.CSharpTypeCastKeyword;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpResourceKeyword)]
	[Name(Constants.CSharpResourceKeyword)]
	[UserVisible(false)]
	[Order(After = Constants.CodeKeyword)]
	[Order(After = Constants.CSharpBranchingKeyword)]
	[Order(Before = Constants.CodeBold)]
	sealed class ResourceKeywordFormat : ClassificationFormatDefinition
	{
		public ResourceKeywordFormat() {
			DisplayName = Constants.CSharpResourceKeyword;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CodeBold)]
	[Name(Constants.CodeBold)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	sealed class BoldFormat : ClassificationFormatDefinition
	{
		public BoldFormat() {
			DisplayName = Constants.CodeBold;
			IsBold = true;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpLocalVariableName)]
	[Name(Constants.CSharpLocalVariableName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeLocalName)]
	[Order(Before = Constants.CSharpUserSymbol)]
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
	[Order(After = Constants.CSharpReadOnlyFieldName)]
	[Order(After = Constants.CSharpStaticMemberName)]
	[Order(After = Constants.CodeConstantName)]
	[Order(Before = Constants.CSharpUserSymbol)]
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
	[Order(Before = Constants.CSharpUserSymbol)]
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
	[Order(Before = Constants.CSharpUserSymbol)]
	sealed class VolatileFieldFormat : ClassificationFormatDefinition
	{
		public VolatileFieldFormat() {
			DisplayName = Constants.NameOfMe + ": volatile field";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpEnumFieldName)]
	[Name(Constants.CSharpEnumFieldName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeFieldName)]
	[Order(After = Constants.CSharpStaticMemberName)]
	[Order(After = Constants.CSharpConstFieldName)]
	[Order(After = Constants.CodeStaticSymbol)]
	[Order(Before = Constants.CSharpUserSymbol)]
	sealed class EnumFieldFormat : ClassificationFormatDefinition
	{
		public EnumFieldFormat() {
			DisplayName = Constants.NameOfMe + ": enum field";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpParameterName)]
	[Name(Constants.CSharpParameterName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeParameterName)]
	[Order(Before = Constants.CSharpUserSymbol)]
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
	[Order(Before = Constants.CSharpUserSymbol)]
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
	[Order(Before = Constants.CSharpUserSymbol)]
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
	[Order(After = Constants.CSharpMethodName)]
	[Order(After = Constants.CSharpStaticMemberName)]
	[Order(After = Constants.CodeStaticSymbol)]
	[Order(After = Constants.CodeExtensionMethodName)]
	[Order(Before = Constants.CSharpUserSymbol)]
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
	[Order(After = Constants.CSharpStaticMemberName)]
	[Order(After = Constants.CodeStaticSymbol)]
	[Order(Before = Constants.CSharpUserSymbol)]
	sealed class ExternMethodFormat : ClassificationFormatDefinition
	{
		public ExternMethodFormat() {
			DisplayName = Constants.NameOfMe + ": extern method";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpLocalFunctionDeclarationName)]
	[Name(Constants.CSharpLocalFunctionDeclarationName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeMethodName)]
	[Order(Before = Constants.CSharpUserSymbol)]
	sealed class LocalFunctionDeclarationFormat : ClassificationFormatDefinition
	{
		public LocalFunctionDeclarationFormat() {
			DisplayName = Constants.NameOfMe + ": local function declaration";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpMethodName)]
	[Name(Constants.CSharpMethodName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeMethodName)]
	[Order(Before = Constants.CSharpUserSymbol)]
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
	[Order(After = Constants.CodeEventName)]
	[Order(Before = Constants.CSharpUserSymbol)]
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
	[Order(Before = Constants.CSharpUserSymbol)]
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
	[Order(Before = Constants.CSharpUserSymbol)]
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
	[Order(Before = Constants.CSharpUserSymbol)]
	sealed class AliasNamespaceFormat : ClassificationFormatDefinition
	{
		public AliasNamespaceFormat() {
			DisplayName = Constants.NameOfMe + ": alias namespace";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpReadOnlyStructName)]
	[Name(Constants.CSharpReadOnlyStructName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeStructName)]
	[Order(After = Constants.CodeRecordStructName)]
	[Order(Before = Constants.CSharpUserSymbol)]
	sealed class ReadOnlyStructFormat : ClassificationFormatDefinition
	{
		public ReadOnlyStructFormat() {
			DisplayName = Constants.NameOfMe + ": read-only struct";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpRefStructName)]
	[Name(Constants.CSharpRefStructName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeStructName)]
	[Order(Before = Constants.CSharpUserSymbol)]
	sealed class RefStructFormat : ClassificationFormatDefinition
	{
		public RefStructFormat() {
			DisplayName = Constants.NameOfMe + ": ref struct";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpConstructorMethodName)]
	[Name(Constants.CSharpConstructorMethodName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeMethodName)]
	[Order(Before = Constants.CSharpUserSymbol)]
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
	[ClassificationType(ClassificationTypeNames = Constants.CSharpMemberDeclarationName)]
	[Name(Constants.CSharpMemberDeclarationName)]
	[UserVisible(false)]
	[Order(After = Constants.CSharpDeclarationName)]
	sealed class MemberDeclarationFormat : ClassificationFormatDefinition
	{
		public MemberDeclarationFormat() {
			DisplayName = Constants.NameOfMe + ": member declaration";
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
	[Order(Before = Constants.CSharpUserSymbol)]
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
	[Order(Before = Constants.CSharpUserSymbol)]
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
	[Order(Before = Constants.CSharpUserSymbol)]
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
	[Order(Before = Constants.CSharpUserSymbol)]
	sealed class VirtualMemberFormat : ClassificationFormatDefinition
	{
		public VirtualMemberFormat() {
			DisplayName = Constants.NameOfMe + ": virtual member";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpSealedMemberName)]
	[Name(Constants.CSharpSealedMemberName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(Before = Constants.CodeClassName)]
	[Order(Before = Constants.CodeRecordClassName)]
	[Order(Before = Constants.CodeMethodName)]
	[Order(Before = Constants.CodePropertyName)]
	[Order(Before = Constants.CodeEventName)]
	[Order(Before = Constants.CSharpUserSymbol)]
	sealed class SealedMemberFormat : ClassificationFormatDefinition
	{
		public SealedMemberFormat() {
			DisplayName = Constants.NameOfMe + ": sealed class or member";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpPrivateMemberName)]
	[Name(Constants.CSharpPrivateMemberName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(Before = Constants.CodeClassName)]
	[Order(Before = Constants.CodeRecordClassName)]
	[Order(Before = Constants.CodeMethodName)]
	[Order(Before = Constants.CodePropertyName)]
	[Order(Before = Constants.CodeEventName)]
	[Order(Before = Constants.CSharpUserSymbol)]
	sealed class PrivateMemberFormat : ClassificationFormatDefinition
	{
		public PrivateMemberFormat() {
			DisplayName = Constants.NameOfMe + ": private type or member";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpNestedTypeName)]
	[Name(Constants.CSharpNestedTypeName)]
	[UserVisible(false)]
	[Order(After = Constants.CodeIdentifier)]
	[Order(After = Constants.CodeClassName)]
	[Order(After = Constants.CodeStructName)]
	[Order(After = Constants.CodeInterfaceName)]
	[Order(After = Constants.CodeEnumName)]
	[Order(After = Constants.CodeRecordClassName)]
	[Order(After = Constants.CodeRecordStructName)]
	[Order(Before = Constants.CSharpUserSymbol)]
	sealed class NestedTypeFormat : ClassificationFormatDefinition
	{
		public NestedTypeFormat() {
			DisplayName = Constants.NameOfMe + ": nested type";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CSharpVariableCapturedExpression)]
	[Name(Constants.CSharpVariableCapturedExpression)]
	[UserVisible(false)]
	[Order(After = Constants.CodeOperator)]
	[Order(After = Constants.CSharpLocalFunctionDeclarationName)]
	[Order(Before = Constants.CSharpUserSymbol)]
	sealed class VariableCapturedExpressionFormat : ClassificationFormatDefinition
	{
		public VariableCapturedExpressionFormat() {
			DisplayName = Constants.NameOfMe + ": variable captured";
			IsBold = true;
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
	[Order(Before = Constants.CSharpUserSymbol)]
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
	[Order(After = Constants.CSharpUserSymbol)]
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