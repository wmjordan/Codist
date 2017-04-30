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
		[Name(Constants.ExitKeyword)]
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
#pragma warning restore 649
	}
}
