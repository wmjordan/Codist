using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Classifiers
{

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CodeAbstractionKeyword)]
	[Name(Constants.CodeAbstractionKeyword)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class AbstractKeywordFormat : ClassificationFormatDefinition
	{
		public AbstractKeywordFormat() {
			DisplayName = Constants.CodeAbstractionKeyword;
		}
	}
	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.CodeReturnKeyword)]
	[Name(Constants.CodeReturnKeyword)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class ReturnKeywordFormat : ClassificationFormatDefinition
	{
		public ReturnKeywordFormat() {
			DisplayName = Constants.CodeReturnKeyword;
			ForegroundColor = Constants.ReturnColor;
			IsBold = true;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.EmphasisComment)]
	[Name(Constants.EmphasisComment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class EmphasisCommentFormat : ClassificationFormatDefinition
	{
		public EmphasisCommentFormat() {
			DisplayName = Constants.EmphasisComment + " (//!)";
			ForegroundColor = Constants.CommentColor;
			IsBold = true;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.QuestionComment)]
	[Name(Constants.QuestionComment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class QuestionCommentFormat : ClassificationFormatDefinition
	{
		public QuestionCommentFormat() {
			DisplayName = Constants.QuestionComment + " (//?)";
			ForegroundColor = Constants.QuestionColor;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.ExclaimationComment)]
	[Name(Constants.ExclaimationComment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class ExclaimationCommentFormat : ClassificationFormatDefinition
	{
		public ExclaimationCommentFormat() {
			DisplayName = Constants.ExclaimationComment + " (//!?)";
			ForegroundColor = Constants.ExclaimationColor;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.DeletionComment)]
	[Name(Constants.DeletionComment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class DeletionCommentFormat : ClassificationFormatDefinition
	{
		public DeletionCommentFormat() {
			DisplayName = Constants.DeletionComment + " (//x)";
			ForegroundColor = Constants.DeletionColor;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.TodoComment)]
	[Name(Constants.TodoComment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class ToDoCommentFormat : ClassificationFormatDefinition
	{
		public ToDoCommentFormat() {
			DisplayName = Constants.TodoComment + " (//ToDo)";
			BackgroundColor = Constants.ToDoColor;
			ForegroundColor = Colors.LightYellow;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.NoteComment)]
	[Name(Constants.NoteComment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class NoteCommentFormat : ClassificationFormatDefinition
	{
		public NoteCommentFormat() {
			DisplayName = Constants.NoteComment + " (//Note)";
			BackgroundColor = Constants.NoteColor;
			ForegroundColor = Colors.LightYellow;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.HackComment)]
	[Name(Constants.HackComment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class HackCommentFormat : ClassificationFormatDefinition
	{
		public HackCommentFormat() {
			DisplayName = Constants.HackComment + " (//Hack)";
			BackgroundColor = Constants.HackColor;
			ForegroundColor = Colors.LightGreen;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Heading1Comment)]
	[Name(Constants.Heading1Comment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class Heading1CommentFormat : ClassificationFormatDefinition
	{
		public Heading1CommentFormat() {
			DisplayName = Constants.Heading1Comment + " (//+++)";
			ForegroundColor = Constants.CommentColor;
			IsBold = true;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Heading2Comment)]
	[Name(Constants.Heading2Comment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class Heading2CommentFormat : ClassificationFormatDefinition
	{
		public Heading2CommentFormat() {
			DisplayName = Constants.Heading2Comment + " (//++)";
			ForegroundColor = Constants.CommentColor;
			IsBold = true;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Heading3Comment)]
	[Name(Constants.Heading3Comment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class Heading3CommentFormat : ClassificationFormatDefinition
	{
		public Heading3CommentFormat() {
			DisplayName = Constants.Heading3Comment + " (//+)";
			ForegroundColor = Constants.CommentColor;
			IsBold = true;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Heading4Comment)]
	[Name(Constants.Heading4Comment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class Heading4CommentFormat : ClassificationFormatDefinition
	{
		public Heading4CommentFormat() {
			DisplayName = Constants.Heading4Comment + " (//-)";
			ForegroundColor = Constants.CommentColor;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Heading5Comment)]
	[Name(Constants.Heading5Comment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class Heading5CommentFormat : ClassificationFormatDefinition
	{
		public Heading5CommentFormat() {
			DisplayName = Constants.Heading5Comment + " (//--)";
			ForegroundColor = Constants.CommentColor;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Heading6Comment)]
	[Name(Constants.Heading6Comment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class Heading6CommentFormat : ClassificationFormatDefinition
	{
		public Heading6CommentFormat() {
			DisplayName = Constants.Heading6Comment + " (//---)";
			ForegroundColor = Constants.CommentColor;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Task1Comment)]
	[Name(Constants.Task1Comment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class Task1CommentFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Task2Comment)]
	[Name(Constants.Task2Comment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class Task2CommentFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Task3Comment)]
	[Name(Constants.Task3Comment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class Task3CommentFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Task4Comment)]
	[Name(Constants.Task4Comment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class Task4CommentFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Task5Comment)]
	[Name(Constants.Task5Comment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class Task5CommentFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Task6Comment)]
	[Name(Constants.Task6Comment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class Task6CommentFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Task7Comment)]
	[Name(Constants.Task7Comment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class Task7CommentFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Task8Comment)]
	[Name(Constants.Task8Comment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class Task8CommentFormat : ClassificationFormatDefinition
	{
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = Constants.Task9Comment)]
	[Name(Constants.Task9Comment)]
	[UserVisible(false)]
	[Order(After = Priority.High)]
	public sealed class Task9CommentFormat : ClassificationFormatDefinition
	{
	}

}
