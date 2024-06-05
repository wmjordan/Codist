using System;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
{
	abstract class MarkdownTag : ClassificationTag
	{
		protected MarkdownTag(IClassificationType classificationType) : base(classificationType) {
		}

		public virtual bool IsContainerBlock => false;
		public virtual bool ContinueToNextLine => false;
		public abstract BlockType BlockType { get; }
	}

	sealed class MarkdownClassificationTypes
	{
		static readonly MarkdownClassificationTypes Default = new MarkdownClassificationTypes(ServicesHelper.Instance.ClassificationTypeRegistry);
		static readonly MarkdownClassificationTypes Dummy = new MarkdownClassificationTypes(null);

		internal readonly IClassificationType[] Headings;
		internal readonly IClassificationType Quotation, OrderedList, UnorderedList, CodeBlock, FencedCodeBlock, ThematicBreak;

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
				Quotation = types.GetClassificationType(Constants.MarkdownQuotation);
				OrderedList = types.GetClassificationType(Constants.MarkdownOrderedList);
				UnorderedList = types.GetClassificationType(Constants.MarkdownUnorderedList);
				CodeBlock = types.GetClassificationType(Constants.MarkdownCodeBlock);
				FencedCodeBlock = types.GetClassificationType(Constants.MarkdownFencedCodeBlock);
				ThematicBreak = types.GetClassificationType(Constants.MarkdownThematicBreak);
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
				Quotation = OrderedList = UnorderedList = CodeBlock = FencedCodeBlock = ThematicBreak = dummy;
			}
		}
	}

	abstract class MarkdownContainerTag : MarkdownTag
	{
		protected MarkdownContainerTag(IClassificationType classificationType, int leading) : base(classificationType) {
			Leading = leading;
		}

		public override bool IsContainerBlock => true;
		public override bool ContinueToNextLine => true;
		public MarkdownTag Child { get; set; }
		public int Leading { get; }
	}

	sealed class MarkdownBlockQuoteTag : MarkdownContainerTag
	{
		readonly bool _ContinueToNextLine;
		public MarkdownBlockQuoteTag(IClassificationType classificationType, int leading, bool continueToNext) : base(classificationType, leading) {
			_ContinueToNextLine = continueToNext;
		}

		public override BlockType BlockType => BlockType.Quotation;
		public override bool ContinueToNextLine => _ContinueToNextLine;
	}

	sealed class MarkdownOrderedListItemTag : MarkdownContainerTag
	{
		public MarkdownOrderedListItemTag(IClassificationType classificationType, int leading) : base(classificationType, leading) {
		}

		public override BlockType BlockType => BlockType.OrderedList;
	}

	sealed class MarkdownUnorderedListItemTag : MarkdownContainerTag
	{
		public MarkdownUnorderedListItemTag(IClassificationType classificationType, int leading) : base(classificationType, leading) {
		}

		public override BlockType BlockType => BlockType.UnorderedList;
	}

	/// <summary>
	/// The <see cref="ClassificationTag"/> for Markdown title
	/// </summary>
	sealed class MarkdownHeadingTag : MarkdownTag
	{
		public MarkdownHeadingTag(IClassificationType classificationType, int level) : base(classificationType) {
			Level = level;
		}

		public readonly int Level;
		public override BlockType BlockType => BlockType.Heading;
	}

	sealed class MarkdownThematicBreakTag : MarkdownTag
	{
		public MarkdownThematicBreakTag(IClassificationType classificationType) : base(classificationType) {
		}

		public override BlockType BlockType => BlockType.ThematicBreak;
	}

	/// <summary>
	/// The <see cref="ClassificationTag"/> for Markdown leading fence
	/// </summary>
	sealed class MarkdownFenceTag : MarkdownTag
	{
		public readonly char FenceCharacter;
		public readonly int FenceLength;

		public MarkdownFenceTag(IClassificationType classificationType, char fenceCharacter, int fenceLength) : base(classificationType) {
			FenceCharacter = fenceCharacter;
			FenceLength = fenceLength;
		}

		public override BlockType BlockType => BlockType.FencedBlock;
		public override bool ContinueToNextLine => true;
	}

	sealed class MarkdownHtmlBlockTag : MarkdownTag
	{
		public readonly MarkupHtmlType Type;

		public MarkdownHtmlBlockTag(IClassificationType classificationType, MarkupHtmlType type) : base(classificationType) {
			Type = type;
		}

		public override BlockType BlockType => BlockType.HtmlBlock;
		public override bool ContinueToNextLine => true;
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
