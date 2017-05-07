using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Classifiers
{
	public static class ClassificationDefinitions
    {
#pragma warning disable 649
		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Keyword")]
		[Name(Constants.ReturnKeyword)]
		internal static ClassificationTypeDefinition ReturnKeywordClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Keyword")]
		[Name(Constants.CodeExitKeyword)]
		internal static ClassificationTypeDefinition ThrowKeywordClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
        [BaseDefinition("Comment")]
        [Name(Constants.EmphasisComment)]
        internal static ClassificationTypeDefinition EmphasisCommentClassificationType;

        [Export(typeof(ClassificationTypeDefinition))]
        [BaseDefinition("Comment")]
        [Name(Constants.QuestionComment)]
		internal static ClassificationTypeDefinition QuestionCommentClassificationType;

        [Export(typeof(ClassificationTypeDefinition))]
        [BaseDefinition("Comment")]
        [Name(Constants.ExclaimationComment)]
		internal static ClassificationTypeDefinition ExclaimationCommentClassificationType;

        [Export(typeof(ClassificationTypeDefinition))]
        [BaseDefinition("Comment")]
        [Name(Constants.DeletionComment)]
        internal static ClassificationTypeDefinition DeletionCommentClassificationType;

        [Export(typeof(ClassificationTypeDefinition))]
        [BaseDefinition("Comment")]
        [Name(Constants.TodoComment)]
        internal static ClassificationTypeDefinition TaskCommentClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Comment")]
		[Name(Constants.NoteComment)]
		internal static ClassificationTypeDefinition NoteCommentClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Comment")]
		[Name(Constants.HackComment)]
		internal static ClassificationTypeDefinition HackCommentClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Comment")]
		[Name(Constants.Heading1Comment)]
		internal static ClassificationTypeDefinition Heading1CommentClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Comment")]
		[Name(Constants.Heading2Comment)]
		internal static ClassificationTypeDefinition Heading2CommentClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Comment")]
		[Name(Constants.Heading3Comment)]
		internal static ClassificationTypeDefinition Heading3CommentClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Comment")]
		[Name(Constants.Heading4Comment)]
		internal static ClassificationTypeDefinition Heading4CommentClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Comment")]
		[Name(Constants.Heading5Comment)]
		internal static ClassificationTypeDefinition Heading5CommentClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Comment")]
		[Name(Constants.Heading6Comment)]
		internal static ClassificationTypeDefinition Heading6CommentClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Comment")]
		[Name(Constants.Task1Comment)]
		internal static ClassificationTypeDefinition Task1CommentClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Comment")]
		[Name(Constants.Task2Comment)]
		internal static ClassificationTypeDefinition Task2CommentClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Comment")]
		[Name(Constants.Task3Comment)]
		internal static ClassificationTypeDefinition Task3CommentClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Comment")]
		[Name(Constants.Task4Comment)]
		internal static ClassificationTypeDefinition Task4CommentClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Comment")]
		[Name(Constants.Task5Comment)]
		internal static ClassificationTypeDefinition Task5CommentClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Comment")]
		[Name(Constants.Task6Comment)]
		internal static ClassificationTypeDefinition Task6CommentClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Comment")]
		[Name(Constants.Task7Comment)]
		internal static ClassificationTypeDefinition Task7CommentClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Comment")]
		[Name(Constants.Task8Comment)]
		internal static ClassificationTypeDefinition Task8CommentClassificationType;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition("Comment")]
		[Name(Constants.Task9Comment)]
		internal static ClassificationTypeDefinition Task9CommentClassificationType;
#pragma warning restore 649
	}
}
