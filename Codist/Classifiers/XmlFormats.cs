using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Classifiers
{

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.XmlName)]
	[Name(Constants.XmlName)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	sealed class XmlNameFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.XmlAttributeName)]
	[Name(Constants.XmlAttributeName)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	sealed class XmlAttributeNameFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.XmlAttributeQuotes)]
	[Name(Constants.XmlAttributeQuotes)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	sealed class XmlAttributeQuotesFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.XmlAttributeValue)]
	[Name(Constants.XmlAttributeValue)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	sealed class XmlAttributeValueFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.XmlCData)]
	[Name(Constants.XmlCData)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	sealed class XmlCDataFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.XmlComment)]
	[Name(Constants.XmlComment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	sealed class XmlCommentFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.XmlDelimiter)]
	[Name(Constants.XmlDelimiter)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	sealed class XmlDelimiterFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.XmlProcessingInstruction)]
	[Name(Constants.XmlProcessingInstruction)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	sealed class XmlProcessingInstructionFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.XmlText)]
	[Name(Constants.XmlText)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	sealed class XmlTextFormat : ClassificationFormatDefinition
	{
	}
}
