using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Classifiers
{
	/// <summary>Classification type definition export for code tagger.</summary>
	static class CommentClassificationDefinitions
    {
#pragma warning disable 169
		[Export(typeof(ClassificationTypeDefinition))]
        [BaseDefinition(Constants.CodeComment)]
        [Name(Constants.EmphasisComment)]
        static ClassificationTypeDefinition EmphasisComment;

        [Export(typeof(ClassificationTypeDefinition))]
        [BaseDefinition(Constants.CodeComment)]
        [Name(Constants.QuestionComment)]
		static ClassificationTypeDefinition QuestionComment;

        [Export(typeof(ClassificationTypeDefinition))]
        [BaseDefinition(Constants.CodeComment)]
        [Name(Constants.ExclaimationComment)]
		static ClassificationTypeDefinition ExclaimationComment;

        [Export(typeof(ClassificationTypeDefinition))]
        [BaseDefinition(Constants.CodeComment)]
        [Name(Constants.DeletionComment)]
        static ClassificationTypeDefinition DeletionComment;

        [Export(typeof(ClassificationTypeDefinition))]
        [BaseDefinition(Constants.CodeComment)]
        [Name(Constants.TodoComment)]
        static ClassificationTypeDefinition TodoComment;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.NoteComment)]
		static ClassificationTypeDefinition NoteComment;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.HackComment)]
		static ClassificationTypeDefinition HackComment;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.UndoneComment)]
		static ClassificationTypeDefinition UndoneComment;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Heading1Comment)]
		static ClassificationTypeDefinition Heading1Comment;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Heading2Comment)]
		static ClassificationTypeDefinition Heading2Comment;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Heading3Comment)]
		static ClassificationTypeDefinition Heading3Comment;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Heading4Comment)]
		static ClassificationTypeDefinition Heading4Comment;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Heading5Comment)]
		static ClassificationTypeDefinition Heading5Comment;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Heading6Comment)]
		static ClassificationTypeDefinition Heading6Comment;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Task1Comment)]
		static ClassificationTypeDefinition Task1Comment;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Task2Comment)]
		static ClassificationTypeDefinition Task2Comment;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Task3Comment)]
		static ClassificationTypeDefinition Task3Comment;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Task4Comment)]
		static ClassificationTypeDefinition Task4Comment;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Task5Comment)]
		static ClassificationTypeDefinition Task5Comment;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Task6Comment)]
		static ClassificationTypeDefinition Task6Comment;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Task7Comment)]
		static ClassificationTypeDefinition Task7Comment;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Task8Comment)]
		static ClassificationTypeDefinition Task8Comment;

		[Export(typeof(ClassificationTypeDefinition))]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Task9Comment)]
		static ClassificationTypeDefinition Task9Comment;
#pragma warning restore 169
	}
}
