using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Taggers
{
	/// <summary>Classification type definition export for symbol highlighter.</summary>
	static class HighlighterDefinitions
	{
#pragma warning disable 169
		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.Highlight1)]
		static ClassificationTypeDefinition Highlight1;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.Highlight2)]
		static ClassificationTypeDefinition Highlight2;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.Highlight3)]
		static ClassificationTypeDefinition Highlight3;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.Highlight4)]
		static ClassificationTypeDefinition Highlight4;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.Highlight5)]
		static ClassificationTypeDefinition Highlight5;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.Highlight6)]
		static ClassificationTypeDefinition Highlight6;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.Highlight7)]
		static ClassificationTypeDefinition Highlight7;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.Highlight8)]
		static ClassificationTypeDefinition Highlight8;

		[Export(typeof(ClassificationTypeDefinition))]
		[Name(Constants.Highlight9)]
		static ClassificationTypeDefinition Highlight9;
#pragma warning restore 169
	}
}
