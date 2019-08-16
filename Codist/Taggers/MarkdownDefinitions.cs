using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Taggers
{
	/// <summary>Classification type definition export for markdown highlighter.</summary>
	static class MarkdownDefinitions
	{
#pragma warning disable 169
		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.MarkdownHeading1)]
		static ClassificationTypeDefinition MarkdownHeading1;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.MarkdownHeading2)]
		static ClassificationTypeDefinition MarkdownHeading2;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.MarkdownHeading3)]
		static ClassificationTypeDefinition MarkdownHeading3;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.MarkdownHeading4)]
		static ClassificationTypeDefinition MarkdownHeading4;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.MarkdownHeading5)]
		static ClassificationTypeDefinition MarkdownHeading5;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.MarkdownHeading6)]
		static ClassificationTypeDefinition MarkdownHeading6;
#pragma warning restore 169
	}
}
