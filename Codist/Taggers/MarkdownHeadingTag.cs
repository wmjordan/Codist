using System;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
{
	abstract class MarkdownTag(IClassificationType classificationType) : ClassificationTag(classificationType)
	{
		public abstract BlockType BlockType { get; }
		public virtual bool IsContainerBlock => false;
		public virtual bool IsRawContentBlock => false;
		public virtual bool ContinueToNextLine => false;
		public virtual bool HasTerminator => false;
		public virtual bool MayContainEmptyLine => false;
		public virtual bool IsClosed => true;
		public virtual bool ValidateStart(ITextSnapshot snapshot, int position) {
			return true;
		}
		public virtual bool ValidateEnd(ITextSnapshot snapshot, int endPosition, bool autoReopen) {
			return true;
		}
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
				var dummy = ClassificationStyleHelper.CreateClassificationCategory(Constants.CodeText);
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

		public override bool ValidateStart(ITextSnapshot snapshot, int position) {
			return position < snapshot.Length && snapshot[position] == '>';
		}
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
		bool _IsClosed;
		public readonly char FenceCharacter = fenceCharacter;
		public readonly int FenceLength = fenceLength;

		public override BlockType BlockType => BlockType.FencedBlock;
		public override bool ContinueToNextLine => !IsClosed;
		public override bool IsRawContentBlock => true;
		public override bool MayContainEmptyLine => true;
		public override bool HasTerminator => true;
		public override bool IsClosed => _IsClosed;

		public override bool ValidateStart(ITextSnapshot snapshot, int position) {
			if (position < 0 || position >= snapshot.Length) return false;
			if (snapshot[position] != FenceCharacter) return false;

			int count = 1;
			int limit = Math.Min(position + FenceLength, snapshot.Length);
			for (int i = position + 1; i < limit; i++) {
				if (snapshot[i] == FenceCharacter) count++;
				else break;
			}
			return count >= FenceLength;
		}

		public override bool ValidateEnd(ITextSnapshot snapshot, int endPosition, bool autoReopen) {
			// 检查是否有足够的空间容纳至少 FenceLength 个字符
			if (endPosition - FenceLength < 0) {
				if (autoReopen) {
					_IsClosed = false;
				}
				return false;
			}

			int pos = endPosition - 1;

			// 1. 向前跳过行尾的空白字符（Markdown 允许闭合围栏后有空格）
			while (pos >= 0 && snapshot[pos].IsCodeWhitespaceOrNewLine()) {
				pos--;
			}

			// 2. 向前扫描闭合围栏字符
			int count = 0;
			while (pos >= 0 && snapshot[pos] == FenceCharacter && count < FenceLength) {
				count++;
				pos--;
			}

			if (count >= FenceLength) {
				// 3. 检查围栏前的缩进（Markdown 允许 0-3 个空格的缩进）
				int spaces = 0;
				while (pos >= 0 && snapshot[pos] == ' ') {
					spaces++;
					if (spaces > 3) break; // 超过3个空格，不符合规范
					pos--;
				}

				if (spaces <= 3) {
					return true; // 找到有效的闭合围栏
				}
			}

			// 4. 如果没有找到有效的闭合围栏，且允许自动重开，则标记为未闭合
			if (autoReopen) {
				_IsClosed = false;
			}
			return false;
		}

		public void Close() {
			_IsClosed = true;
		}
	}

	sealed class MarkdownHtmlBlockTag(IClassificationType classificationType, MarkupHtmlType type) : MarkdownTag(classificationType)
	{
		bool _IsClosed;

		public readonly MarkupHtmlType Type = type;

		public override BlockType BlockType => BlockType.HtmlBlock;
		public override bool ContinueToNextLine => Type != MarkupHtmlType.HtmlComment || !_IsClosed;
		public override bool IsRawContentBlock => true;
        public override bool MayContainEmptyLine => Type == MarkupHtmlType.HtmlComment;
		public override bool IsClosed => _IsClosed;

		public override bool ValidateStart(ITextSnapshot snapshot, int position) {
			if (position >= snapshot.Length || snapshot[position] != '<') return false;
			switch (Type) {
				case MarkupHtmlType.HtmlComment:
					return position + 3 < snapshot.Length
						&& snapshot[position + 1] == '!'
						&& snapshot[position + 2] == '-'
						&& snapshot[position + 3] == '-';
				case MarkupHtmlType.DocType:
					return position + 8 < snapshot.Length
						&& new SnapshotSpan(snapshot, position, 9).GetText().StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase);
				case MarkupHtmlType.ProcessingInstruction:
					return position + 1 < snapshot.Length && snapshot[position + 1] == '?';
				case MarkupHtmlType.CData:
					return position + 8 < snapshot.Length
						&& new SnapshotSpan(snapshot, position, 9).GetText().StartsWith("<![CDATA[", StringComparison.Ordinal);
				case MarkupHtmlType.General:
					return position + 1 < snapshot.Length && char.IsLetter(snapshot[position + 1]);
				default:
					return false;
			}
		}

		public override bool ValidateEnd(ITextSnapshot snapshot, int endPosition, bool autoReopen) {
			bool isValid = false;

			int pos = endPosition - 1;

			// 1. 向前跳过行尾的空白字符（Markdown 允许闭合围栏后有空格）
			while (pos >= 0 && snapshot[pos].IsCodeWhitespaceOrNewLine()) {
				pos--;
			}

			switch (Type) {
				case MarkupHtmlType.HtmlComment:
					// 验证 -->
					if (pos >= 3
						&& snapshot[pos - 2] == '-'
						&& snapshot[pos - 1] == '-'
						&& snapshot[pos] == '>') {
						isValid = true;
					}
					break;

				case MarkupHtmlType.CData:
					// 验证 ]]>
					if (pos >= 3
						&& snapshot[pos - 2] == ']'
						&& snapshot[pos - 1] == ']'
						&& snapshot[pos] == '>') {
						isValid = true;
					}
					break;

				case MarkupHtmlType.ProcessingInstruction:
					// 验证 ?>
					if (pos >= 2
						&& snapshot[pos - 1] == '?'
						&& snapshot[pos] == '>') {
						isValid = true;
					}
					break;

				case MarkupHtmlType.DocType:
					// 验证 >
					if (pos >= 1
						&& snapshot[pos] == '>') {
						isValid = true;
					}
					break;

				case MarkupHtmlType.General:
					// 对于一般性 HTML 块（如 <div>），结束标记是 </tag>
					// 我们检查是否有 > 字符作为结束标记的标志
					if (pos >= 1
						&& snapshot[pos] == '>') {
						isValid = true;
					}
					break;
			}

			if (!isValid && autoReopen) {
				_IsClosed = false;
			}
			return isValid;
		}

		public void Close() {
			_IsClosed = true;
		}
	}

	sealed class MarkdownCodeBlockTag(IClassificationType classificationType) : MarkdownTag(classificationType)
	{
		public override BlockType BlockType => BlockType.IndentedCodeBlock;
		public override bool IsRawContentBlock => true;

		public override bool ValidateStart(ITextSnapshot snapshot, int position) {
			if (position < 0 || position >= snapshot.Length) return false;
			char c = snapshot[position];
			if (c == '\t') return true;
			if (c == ' ') {
				int spaces = 1;
				int limit = Math.Min(position + 4, snapshot.Length);
				for (int i = position + 1; i < limit; i++) {
					if (snapshot[i] == ' ') spaces++;
					else break;
				}
				return spaces >= 4;
			}
			return false;
		}
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
