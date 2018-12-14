using System;
using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Classifiers
{
	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Highlight1)]
	[Name(Constants.Highlight1)]
	[UserVisible(false)]
	[Order(After = Constants.CodeFormalLanguage)]
	sealed class Highlight1Format : ClassificationFormatDefinition
	{
		public Highlight1Format() {
			BackgroundBrush = Brushes.Salmon.Alpha(0.5);
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Highlight2)]
	[Name(Constants.Highlight2)]
	[UserVisible(false)]
	[Order(After = Constants.CodeFormalLanguage)]
	sealed class Highlight2Format : ClassificationFormatDefinition
	{
		public Highlight2Format() {
			BackgroundBrush = Brushes.Orange.Alpha(0.5);
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Highlight3)]
	[Name(Constants.Highlight3)]
	[UserVisible(false)]
	[Order(After = Constants.CodeFormalLanguage)]
	sealed class Highlight3Format : ClassificationFormatDefinition
	{
		public Highlight3Format() {
			BackgroundBrush = Brushes.Yellow.Alpha(0.5);
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Highlight4)]
	[Name(Constants.Highlight4)]
	[UserVisible(false)]
	[Order(After = Constants.CodeFormalLanguage)]
	sealed class Highlight4Format : ClassificationFormatDefinition
	{
		public Highlight4Format() {
			BackgroundBrush = Brushes.LawnGreen.Alpha(0.5);
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Highlight5)]
	[Name(Constants.Highlight5)]
	[UserVisible(false)]
	[Order(After = Constants.CodeFormalLanguage)]
	sealed class Highlight5Format : ClassificationFormatDefinition
	{
		public Highlight5Format() {
			BackgroundBrush = Brushes.Cyan.Alpha(0.5);
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Highlight6)]
	[Name(Constants.Highlight6)]
	[UserVisible(false)]
	[Order(After = Constants.CodeFormalLanguage)]
	sealed class Highlight6Format : ClassificationFormatDefinition
	{
		public Highlight6Format() {
			BackgroundBrush = Brushes.SkyBlue.Alpha(0.5);
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Highlight7)]
	[Name(Constants.Highlight7)]
	[UserVisible(false)]
	[Order(After = Constants.CodeFormalLanguage)]
	sealed class Highlight7Format : ClassificationFormatDefinition
	{
		public Highlight7Format() {
			BackgroundBrush = Brushes.Violet.Alpha(0.5);
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Highlight8)]
	[Name(Constants.Highlight8)]
	[UserVisible(false)]
	[Order(After = Constants.CodeFormalLanguage)]
	sealed class Highlight8Format : ClassificationFormatDefinition
	{
		public Highlight8Format() {
			BackgroundBrush = Brushes.DarkGray.Alpha(0.5);
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Highlight9)]
	[Name(Constants.Highlight9)]
	[UserVisible(false)]
	[Order(After = Constants.CodeFormalLanguage)]
	sealed class Highlight9Format : ClassificationFormatDefinition
	{
		public Highlight9Format() {
			BackgroundBrush = Brushes.AntiqueWhite.Alpha(0.5);
		}
	}
}
