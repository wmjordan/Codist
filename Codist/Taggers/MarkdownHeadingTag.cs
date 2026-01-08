using System;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
{
	abstract class MarkdownTag(IClassificationType classificationType) : ClassificationTag(classificationType)
	{
		public virtual bool IsContainerBlock => false;
		public virtual bool IsRawContentBlock => false;
		public virtual bool ContinueToNextLine => false;
		public abstract BlockType BlockType { get; }
	}

	sealed class MarkdownClassificationTypes
	{
		internal static readonly MarkdownClassificationTypes Default = new MarkdownClassificationTypes(ServicesHelper.Instance.ClassificationTypeRegistry);
		internal static readonly MarkdownClassificationTypes Dummy = new MarkdownClassificationTypes(null);

		internal readonly IClassificationType[] Headings;
		internal readonly IClassificationType Comment, Quotation, OrderedList, UnorderedList, CodeBlock, FencedCodeBlock, HtmlCodeBlock, ThematicBreak;

		public MarkdownClassificationTypes(IClassificationTypeRegistryService types) {
			if (types != null) {
				Headings = new[] {
					null,
					types.GetClassificationType(Constants.MarkdownHeading1),
					types.GetClassificationType(Constants.MarkdownHeading2),
					types.GetClassificationType(Constants.MarkdownHeading3),
					types.GetClassificationType(Constants.MarkdownHeading4),
					types.GetClassificationType(Constants.MarkdownHeading5),
					types.GetClassificationType(Constants.MarkdownHeading6),
				};
				Comment = types.GetClassificationType(Constants.CodeComment);
				Quotation = types.GetClassificationType(Constants.MarkdownQuotation);
				OrderedList = types.GetClassificationType(Constants.MarkdownOrderedList);
				UnorderedList = types.GetClassificationType(Constants.MarkdownUnorderedList);
				CodeBlock = types.GetClassificationType(Constants.MarkdownCodeBlock);
				FencedCodeBlock = types.GetClassificationType(Constants.MarkdownFencedCodeBlock);
				ThematicBreak = types.GetClassificationType(Constants.MarkdownThematicBreak);
				HtmlCodeBlock = types.GetClassificationType(Constants.MarkdownHtmlCodeBlock);
			}
			else {
				var dummy = TextEditorHelper.CreateClassificationCategory(Constants.CodeText);
				Headings = new[] {
					null,
					dummy,
					dummy,
					dummy,
					dummy,
					dummy,
					dummy,
				};
				Quotation = OrderedList = UnorderedList = CodeBlock = FencedCodeBlock = ThematicBreak = HtmlCodeBlock = dummy;
			}
		}
	}

	abstract class MarkdownContainerTag(IClassificationType classificationType, int leading) : MarkdownTag(classificationType)
	{
		public override bool IsContainerBlock => true;
		public override bool ContinueToNextLine => true;
		public MarkdownTag Child { get; set; }
		public int Leading { get; } = leading;
	}

	sealed class MarkdownBlockQuoteTag(IClassificationType classificationType, int leading, bool continueToNext) : MarkdownContainerTag(classificationType, leading)
	{
		readonly bool _ContinueToNextLine = continueToNext;

		public override BlockType BlockType => BlockType.Quotation;
		public override bool ContinueToNextLine => _ContinueToNextLine;
	}

	sealed class MarkdownOrderedListItemTag(IClassificationType classificationType, int leading) : MarkdownContainerTag(classificationType, leading)
	{
		public override BlockType BlockType => BlockType.OrderedList;
	}

	sealed class MarkdownUnorderedListItemTag(IClassificationType classificationType, int leading) : MarkdownContainerTag(classificationType, leading)
	{
		public override BlockType BlockType => BlockType.UnorderedList;
	}

	/// <summary>
	/// The <see cref="ClassificationTag"/> for Markdown title
	/// </summary>
	sealed class MarkdownHeadingTag(IClassificationType classificationType, int level) : MarkdownTag(classificationType)
	{
		public readonly int Level = level;
		public override BlockType BlockType => BlockType.Heading;
	}

	sealed class MarkdownThematicBreakTag(IClassificationType classificationType) : MarkdownTag(classificationType)
	{
		public override BlockType BlockType => BlockType.ThematicBreak;
	}

	/// <summary>
	/// The <see cref="ClassificationTag"/> for Markdown leading fence
	/// </summary>
	sealed class MarkdownFenceTag(IClassificationType classificationType, char fenceCharacter, int fenceLength) : MarkdownTag(classificationType)
	{
		public readonly char FenceCharacter = fenceCharacter;
		public readonly int FenceLength = fenceLength;

		public override BlockType BlockType => BlockType.FencedBlock;
		public override bool ContinueToNextLine => true;
        public override bool IsRawContentBlock => true;
	}
	sealed class MarkdownFenceEndTag(IClassificationType classificationType) : MarkdownTag(classificationType)
	{
		public override BlockType BlockType => BlockType.FencedBlock;
		public override bool ContinueToNextLine => false;
	}

	sealed class MarkdownHtmlBlockTag(IClassificationType classificationType, MarkupHtmlType type) : MarkdownTag(classificationType)
	{
		public readonly MarkupHtmlType Type = type;

		public override BlockType BlockType => BlockType.HtmlBlock;
		public override bool ContinueToNextLine => true;
		public override bool IsRawContentBlock => true;
	}

	sealed class MarkdownCodeBlockTag(IClassificationType classificationType) : MarkdownTag(classificationType)
	{
		public override BlockType BlockType => BlockType.IndentedCodeBlock;
		public override bool IsRawContentBlock => true;
	}

	enum MarkupHtmlType
	{
		General,
		HtmlScriptTag,
		HtmlComment,
		ProcessingInstruction,
		CData,
		DocType
	}

	enum BlockType : byte
	{
		Default,
		LeafBlocks, // used when comparing block types
		ThematicBreak = LeafBlocks,
		Heading,
		IndentedCodeBlock,
		FencedBlock,
		HtmlBlock,
		ContainerBlocks, // used when comparing block types
		Quotation = ContainerBlocks,
		UnorderedList,
		OrderedList,
	}

}
