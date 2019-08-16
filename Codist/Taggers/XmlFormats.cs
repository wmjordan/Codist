using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Taggers
{
	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.XmlName)]
	[Name(Constants.XmlName)]
	[UserVisible(false)]
	[Order(Before = Priority.Default)]
	sealed class XmlNameFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.XmlAttributeName)]
	[Name(Constants.XmlAttributeName)]
	[UserVisible(false)]
	[Order(Before = Priority.Default)]
	sealed class XmlAttributeNameFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.XmlAttributeQuotes)]
	[Name(Constants.XmlAttributeQuotes)]
	[UserVisible(false)]
	[Order(Before = Priority.Default)]
	sealed class XmlAttributeQuotesFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.XmlAttributeValue)]
	[Name(Constants.XmlAttributeValue)]
	[UserVisible(false)]
	[Order(Before = Priority.Default)]
	sealed class XmlAttributeValueFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.XmlCData)]
	[Name(Constants.XmlCData)]
	[UserVisible(false)]
	[Order(Before = Priority.Default)]
	sealed class XmlCDataFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.XmlComment)]
	[Name(Constants.XmlComment)]
	[UserVisible(false)]
	[Order(Before = Priority.Default)]
	sealed class XmlCommentFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.XmlDelimiter)]
	[Name(Constants.XmlDelimiter)]
	[UserVisible(false)]
	[Order(Before = Priority.Default)]
	sealed class XmlDelimiterFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.XmlProcessingInstruction)]
	[Name(Constants.XmlProcessingInstruction)]
	[UserVisible(false)]
	[Order(Before = Priority.Default)]
	sealed class XmlProcessingInstructionFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.XmlText)]
	[Name(Constants.XmlText)]
	[UserVisible(false)]
	[Order(Before = Priority.Default)]
	sealed class XmlTextFormat : ClassificationFormatDefinition
	{
	}
}
