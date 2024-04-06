using System;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
{
	/// <summary>
	/// The <see cref="ClassificationTag"/> for Markdown title
	/// </summary>
	sealed class MarkdownTitleTag : ClassificationTag
	{
		public MarkdownTitleTag(IClassificationType classificationType, int level) : base(classificationType) {
			TitleLevel = level;
		}

		public readonly int TitleLevel;
	}
}
