using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Taggers
{
	/// <summary>Classification type definition export for symbol highlighter.</summary>
	static class HighlighterClassificationDefinitions
	{
#pragma warning disable 169, IDE0044
		[Export]
		[Name(Constants.Highlight1)]
		static ClassificationTypeDefinition Highlight1;

		[Export]
		[Name(Constants.Highlight2)]
		static ClassificationTypeDefinition Highlight2;

		[Export]
		[Name(Constants.Highlight3)]
		static ClassificationTypeDefinition Highlight3;

		[Export]
		[Name(Constants.Highlight4)]
		static ClassificationTypeDefinition Highlight4;

		[Export]
		[Name(Constants.Highlight5)]
		static ClassificationTypeDefinition Highlight5;

		[Export]
		[Name(Constants.Highlight6)]
		static ClassificationTypeDefinition Highlight6;

		[Export]
		[Name(Constants.Highlight7)]
		static ClassificationTypeDefinition Highlight7;

		[Export]
		[Name(Constants.Highlight8)]
		static ClassificationTypeDefinition Highlight8;

		[Export]
		[Name(Constants.Highlight9)]
		static ClassificationTypeDefinition Highlight9;
#pragma warning restore 169
	}
}
