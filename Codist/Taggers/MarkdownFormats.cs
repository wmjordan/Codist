using System;
using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Taggers
{
	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.MarkdownHeading1)]
	[Name(Constants.MarkdownHeading1)]
	[UserVisible(false)]
	[Order(After = Constants.CodeFormalLanguage)]
	sealed class MarkdownHeading1Format : ClassificationFormatDefinition
	{
		public MarkdownHeading1Format() {
			FontRenderingSize = 28;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.MarkdownHeading2)]
	[Name(Constants.MarkdownHeading2)]
	[UserVisible(false)]
	[Order(After = Constants.CodeFormalLanguage)]
	sealed class MarkdownHeading2Format : ClassificationFormatDefinition
	{
		public MarkdownHeading2Format() {
			FontRenderingSize = 24;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.MarkdownHeading3)]
	[Name(Constants.MarkdownHeading3)]
	[UserVisible(false)]
	[Order(After = Constants.CodeFormalLanguage)]
	sealed class MarkdownHeading3Format : ClassificationFormatDefinition
	{
		public MarkdownHeading3Format() {
			FontRenderingSize = 20;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.MarkdownHeading4)]
	[Name(Constants.MarkdownHeading4)]
	[UserVisible(false)]
	[Order(After = Constants.CodeFormalLanguage)]
	sealed class MarkdownHeading4Format : ClassificationFormatDefinition
	{
		public MarkdownHeading4Format() {
			FontRenderingSize = 18;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.MarkdownHeading5)]
	[Name(Constants.MarkdownHeading5)]
	[UserVisible(false)]
	[Order(After = Constants.CodeFormalLanguage)]
	sealed class MarkdownHeading5Format : ClassificationFormatDefinition
	{
		public MarkdownHeading5Format() {
			FontRenderingSize = 16;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.MarkdownHeading6)]
	[Name(Constants.MarkdownHeading6)]
	[UserVisible(false)]
	[Order(After = Constants.CodeFormalLanguage)]
	sealed class MarkdownHeading6Format : ClassificationFormatDefinition
	{
		public MarkdownHeading6Format() {
			FontRenderingSize = 14;
		}
	}
}
