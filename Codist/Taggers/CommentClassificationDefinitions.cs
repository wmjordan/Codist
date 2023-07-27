using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Taggers
{
	/// <summary>Classification type definition export for code tagger.</summary>
	static class CommentClassificationDefinitions
	{
#pragma warning disable 169, IDE0044
		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.EmphasisComment)]
		static ClassificationTypeDefinition EmphasisComment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.QuestionComment)]
		static ClassificationTypeDefinition QuestionComment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.ExclamationComment)]
		static ClassificationTypeDefinition ExclamationComment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.DeletionComment)]
		static ClassificationTypeDefinition DeletionComment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.TodoComment)]
		static ClassificationTypeDefinition TodoComment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.NoteComment)]
		static ClassificationTypeDefinition NoteComment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.HackComment)]
		static ClassificationTypeDefinition HackComment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.UndoneComment)]
		static ClassificationTypeDefinition UndoneComment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Heading1Comment)]
		static ClassificationTypeDefinition Heading1Comment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Heading2Comment)]
		static ClassificationTypeDefinition Heading2Comment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Heading3Comment)]
		static ClassificationTypeDefinition Heading3Comment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Heading4Comment)]
		static ClassificationTypeDefinition Heading4Comment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Heading5Comment)]
		static ClassificationTypeDefinition Heading5Comment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Heading6Comment)]
		static ClassificationTypeDefinition Heading6Comment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Task1Comment)]
		static ClassificationTypeDefinition Task1Comment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Task2Comment)]
		static ClassificationTypeDefinition Task2Comment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Task3Comment)]
		static ClassificationTypeDefinition Task3Comment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Task4Comment)]
		static ClassificationTypeDefinition Task4Comment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Task5Comment)]
		static ClassificationTypeDefinition Task5Comment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Task6Comment)]
		static ClassificationTypeDefinition Task6Comment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Task7Comment)]
		static ClassificationTypeDefinition Task7Comment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Task8Comment)]
		static ClassificationTypeDefinition Task8Comment;

		[Export]
		[BaseDefinition(Constants.CodeComment)]
		[Name(Constants.Task9Comment)]
		static ClassificationTypeDefinition Task9Comment;
#pragma warning restore 169
	}
}
