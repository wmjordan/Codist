using System;
using System.Collections.Generic;
using CLR;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
{
	sealed class MarkdownTagger : CachedTaggerBase
	{
		internal static readonly MarkdownHeadingTag[] HeaderClassificationTypes = InitHeaderClassificationTypes();
		internal static readonly MarkdownHeadingTag[] DummyHeaderTags = [
			null,
			new(ClassificationStyleHelper.CreateClassificationCategory(Constants.CodeText), 1),
			new(ClassificationStyleHelper.CreateClassificationCategory(Constants.CodeText), 2),
			new(ClassificationStyleHelper.CreateClassificationCategory(Constants.CodeText), 3),
			new(ClassificationStyleHelper.CreateClassificationCategory(Constants.CodeText), 4),
			new(ClassificationStyleHelper.CreateClassificationCategory(Constants.CodeText), 5),
			new(ClassificationStyleHelper.CreateClassificationCategory(Constants.CodeText), 6)
		]; // used when syntax highlight is disabled
		static readonly ClassificationTag __QuotationTag = new(MarkdownClassificationTypes.Default.Quotation);
		static readonly ClassificationTag __OrderedListTag = new(MarkdownClassificationTypes.Default.OrderedList);
		static readonly ClassificationTag __UnorderedListTag = new(MarkdownClassificationTypes.Default.UnorderedList);
		static readonly ClassificationTag __CodeBlockTag = new MarkdownCodeBlockTag(MarkdownClassificationTypes.Default.CodeBlock);
		static readonly IClassificationType __FencedCodeBlockType = MarkdownClassificationTypes.Default.FencedCodeBlock;
		static readonly ClassificationTag __ThematicBreakTag = new(MarkdownClassificationTypes.Default.ThematicBreak);
		const int WaitingEndTag = -1, InvalidTag = -2;

		readonly bool _FullParseAtFirstLoad;
		readonly Action<SnapshotSpan, ICollection<TaggedContentSpan>> _SyntaxParser;
		TaggedContentSpan _LastTaggedSpan; // shortcut field to quicken the processing of adjacent lines
		ITextView _TextView;
		ITextBuffer _TextBuffer;

		public MarkdownTagger(ITextView textView, ITextBuffer buffer, bool syntaxHighlightEnabled) : base(textView) {
			_SyntaxParser = syntaxHighlightEnabled ? (Action<SnapshotSpan, ICollection<TaggedContentSpan>>)ParseSyntax : ParseHeader;
			_TextView = textView;
			_TextBuffer = buffer;
			_FullParseAtFirstLoad = textView.Roles.Contains(PredefinedTextViewRoles.PreviewTextView) == false
				&& textView.Roles.Contains(PredefinedTextViewRoles.Document);
			buffer.ContentTypeChanged += Buffer_ContentTypeChanged;
			textView.Closed += TextView_Closed;
		}

		protected override bool DoFullParseAtFirstLoad => _FullParseAtFirstLoad;

		protected override void Parse(SnapshotSpan span, ICollection<TaggedContentSpan> results) {
			_SyntaxParser(RemoveTrailingNewLine(span), results);
		}

		static SnapshotSpan RemoveTrailingNewLine(SnapshotSpan span) {
			var shot = span.Snapshot;
			var l = span.Length;
			int end;
			if (l > 0 && shot[end = span.End.Position - 1] == '\n') {
				return new SnapshotSpan(shot,
					span.Start,
					--l > 0 && shot[end - 1] == '\r' ? --l : l);
			}
			return span;
		}

		// This minimal parser is used by MarkdownNaviBar when syntax highlight is disabled
		void ParseHeader(SnapshotSpan span, ICollection<TaggedContentSpan> results) {
			int l = span.Length, start = span.Start;
			if (l < 1 || span.Start.GetChar() != '#') {
				goto MISMATCH;
			}

			int level = 1, w = 0;
			var s = span.Snapshot;
			for (int i = 1, p = start + 1; i < l; i++, p++) {
				switch (s[p]) {
					case '#':
						++level;
						continue;
					case ' ':
					case '\t':
						goto TAGGED;
				}
				goto MISMATCH;
			}
		TAGGED:
			if (level < 7) {
				w += level;
				results.Add(new TaggedContentSpan(DummyHeaderTags[level], span, w, l - w));
				return;
			}

		MISMATCH:
			Result.ClearRange(start, l);
		}
		enum ParseResult
		{
			Success,       // 解析成功，完成当前行
			Retry,         // 状态更新（如 c 改变），需要重新进入 switch 判断
			Failure,       // 解析失败，回退到 TRY_MERGE（可能合并上一行）
			ForceMismatch  // 解析失败，强制回退到 MISMATCH（清除上一行状态，如遇到换行）
		}

		void ParseSyntax(SnapshotSpan span, ICollection<TaggedContentSpan> results) {
			int l = span.Length, start = span.Start, lineEnd = start + l;
			if (IsProcessingChanges) {
				RevalidateTaggedSpan(GetPrecedingTaggedSpan(span), span);
			}
			if (l < 1 && (_LastTaggedSpan?.Tag as MarkdownTag)?.MayContainEmptyLine != true) {
				_LastTaggedSpan = null;
				goto MISMATCH;
			}

			var s = span.Snapshot;
			var taggedSpan = GetPrecedingTaggedSpan(span);
			IClassificationTag lastTag;
			if (taggedSpan != null) {
				// --- 1. 验证起始标记 ---
				if (taggedSpan.Tag is MarkdownTag mt) {
					if (!mt.ValidateStart(s, taggedSpan.Start)) {
						Result.ClearRange(taggedSpan);
						//OnTagsChanged(new SnapshotSpanEventArgs(taggedSpan.Span));
						taggedSpan = null;
						_LastTaggedSpan = null;
					}
					// --- 2. 验证结束标记 (如果当前标记为已闭合状态) ---
					else if (mt.IsClosed) {
						// already closed, may reuse
						if (IsProcessingChanges && taggedSpan.Span.Contains(span) && span.End < taggedSpan.End && mt.ValidateEnd(s, span.End, true)) {
							// new end inserted in the middle of the block
							if (span.Start != taggedSpan.Start && span.End < taggedSpan.End) {
								// we can safely shrink the span since the cache sort by the start majorly
								var changedLength = taggedSpan.End - span.End;
								taggedSpan.ExtendTo(span.End);
								OnTagsChanged(new SnapshotSpanEventArgs(new SnapshotSpan(s, span.End, changedLength)));
							}
							goto REUSE;
						}
						if (mt.ValidateEnd(s, taggedSpan.End, true)
							&& (mt.IsRawContentBlock ? taggedSpan.Span.Contains(span) : taggedSpan.Span == span.Span) && (_LastTaggedSpan is null || !_LastTaggedSpan.Span.Contains(span) || (_LastTaggedSpan.Tag as MarkdownTag)?.IsClosed == true)) {
							goto REUSE;
						}
					}
					else {
						if (IsProcessingChanges && taggedSpan.Span.Contains(span) && span.End < taggedSpan.End && mt.ValidateEnd(s, span.End, true)) {
							// new end inserted in the middle of the unclosed block
							if (span.Start != taggedSpan.Start && span.End < taggedSpan.End) {
								// we can safely shrink the span since the cache sort by the start majorly
								var changedLength = taggedSpan.End - span.End;
								taggedSpan.ExtendTo(span.End);
								if (mt.BlockType == BlockType.HtmlBlock && ((MarkdownHtmlBlockTag)mt).Type != MarkupHtmlType.General) {
									((MarkdownHtmlBlockTag)mt).Close();
								}
							}
							goto REUSE;
						}
						// reuse existing tag
						if ((mt.IsRawContentBlock ? taggedSpan.Span.Contains(span) : taggedSpan.Span == span.Span) && (_LastTaggedSpan is null || _LastTaggedSpan == taggedSpan || !_LastTaggedSpan.Span.Contains(span) || (_LastTaggedSpan.Tag as MarkdownTag)?.IsClosed == false)) {
							goto REUSE;
						}
					}
				}

				lastTag = taggedSpan?.Tag;
			}
			else {
				lastTag = null;
			}
			var c = s[start];
			ParseResult result;
			int lineStart;

			do {
				lineStart = start;
				if (lastTag is MarkdownHtmlBlockTag h
					&& !h.IsClosed
					&& h.Type == MarkupHtmlType.HtmlComment
					&& IsNextTo(ref span, taggedSpan.Span)
					&& HandleHtml(ref start, lineEnd, s, new SnapshotSpan(s, lineStart, lineEnd - lineStart), results, taggedSpan) == ParseResult.Success) {
					return;
				}
				// 字符分发
				result = c switch {
					' ' => HandleSpaces(ref start, ref c, lineEnd, s, new SnapshotSpan(s, lineStart, lineEnd - lineStart), results, taggedSpan),
					'\t' => HandleIndentedBlock(ref start, lineEnd, s, new SnapshotSpan(s, lineStart, lineEnd - lineStart), results, lastTag),
					'#' => HandleHeader(ref start, lineEnd, s, new SnapshotSpan(s, lineStart, lineEnd - lineStart), results, lastTag),
					'>' => HandleQuote(ref start, ref c, lineEnd, s, new SnapshotSpan(s, lineStart, lineEnd - lineStart), results, lastTag),
					'0' or '1' or '2' or '3' or '4' or '5' or '6' or '7' or '8' or '9' => HandleOrderedList(ref start, lineEnd, s, new SnapshotSpan(s, lineStart, lineEnd - lineStart), results, lastTag),
					'-' or '*' or '_' => HandleThematicBreakOrList(ref start, lineEnd, s, new SnapshotSpan(s, lineStart, lineEnd - lineStart), results, lastTag, c),
					'+' => HandleUnorderedList(ref start, lineEnd, s, new SnapshotSpan(s, lineStart, lineEnd - lineStart), results, lastTag),
					'`' or '~' => HandleFence(ref start, lineEnd, s, new SnapshotSpan(s, lineStart, lineEnd - lineStart), results, taggedSpan),
					'<' => HandleHtml(ref start, lineEnd, s, new SnapshotSpan(s, lineStart, lineEnd - lineStart), results, taggedSpan),
					_ => ParseResult.Failure
				};

				switch (result) {
					case ParseResult.Success:
						return;
					case ParseResult.ForceMismatch:
						goto MISMATCH;
					case ParseResult.Failure:
						goto TRY_MERGE;
				}
			}
			while (start < lineEnd);

		TRY_MERGE:
			if (lastTag == null || (lastTag is MarkdownTag m && m.ContinueToNextLine == false)) {
				goto MISMATCH;
			}
			if (taggedSpan != null) {
				// 扩展长度至当前行结束
				taggedSpan.ExtendTo(lineEnd);

				// 将更新后的 Tag 加入 results 以便渲染
				results.Add(taggedSpan);
				_LastTaggedSpan = taggedSpan;
				return;
			}
			return;

		MISMATCH:
			Result.ClearRange(span.Start, l);
			return;

		REUSE:
			results.Add(taggedSpan);
			_LastTaggedSpan = taggedSpan;
		}

		void RevalidateTaggedSpan(TaggedContentSpan taggedSpan, SnapshotSpan changedSpan) {
			if (taggedSpan?.Tag is not MarkdownTag tag || !taggedSpan.Contains(changedSpan.End) && taggedSpan.End != changedSpan.End.Position) {
				return;
			}
			var s = taggedSpan.TextSnapshot;
			if (!tag.ValidateStart(s, taggedSpan.Start)) {
				Result.ClearRange(taggedSpan);
				return;
			}
			if (tag.IsClosed && !tag.ValidateEnd(s, taggedSpan.End, true)) {
				if (!tag.ContinueToNextLine) {
					Result.ClearRange(taggedSpan);
					return;
				}
				_LastTaggedSpan = taggedSpan;
			}
		}

		ParseResult HandleSpaces(ref int start, ref char c, int lineEnd, ITextSnapshot s, SnapshotSpan lineSpan, ICollection<TaggedContentSpan> results, TaggedContentSpan taggedSpan) {
			// n 初始化为 1，因为进入此函数时 c 已经是 ' '
			int n = 1;

			while (++start < lineEnd) {
				switch (c = s[start]) {
					case ' ':
						++n;
						if (n == 4) {
							// 1. 检查嵌套列表
							if (PeekForNestedList(ref start, lineEnd, s, ref c)) {
								return ParseResult.Retry;
							}
							// 2. 检查缩进 HTML (针对 4+ 空格)
							if (PeekForIndentedHtml(ref start, lineEnd, s, ref c)) {
								return ParseResult.Retry;
							}
							// 3. 否则是缩进代码块
							return HandleIndentedBlock(ref start, lineEnd, s, lineSpan, results, taggedSpan?.Tag);
						}
						continue;
					case '\t':
						return HandleIndentedBlock(ref start, lineEnd, s, lineSpan, results, taggedSpan?.Tag);
					case '\r':
					case '\n':
						return ParseResult.ForceMismatch;
					case '<':
						// less then 4 spaces, maybe HTML block
						var (htmlEnd, htmlType) = ParseHtmlBlock(s, start, lineEnd);
						if (htmlEnd != InvalidTag) {
							// 是有效的 HTML 块，委托给 HandleHtml 处理
							// 传入 lineSpan 可以确保高亮范围包含行首的空格缩进
							return HandleHtml(ref start, lineEnd, s, lineSpan, results, taggedSpan);
						}
						return ParseResult.Failure;
					default:
						return ParseResult.Failure;
				}
			}

			// 循环结束（行尾），且 n < 4，清除状态
			return ParseResult.ForceMismatch;
		}

		ParseResult HandleIndentedBlock(ref int start, int lineEnd, ITextSnapshot s, SnapshotSpan lineSpan, ICollection<TaggedContentSpan> results, IClassificationTag lastTag) {
			// 如果在原始块中，不创建新的代码块
			if (IsInRawContentBlock(lastTag)) {
				return ParseResult.Failure;
			}
			// 添加缩进代码块标记
			results.Add(_LastTaggedSpan = new TaggedContentSpan(__CodeBlockTag, s, lineSpan.Start, lineEnd - lineSpan.Start, start - lineSpan.Start, lineEnd - start));
			return ParseResult.Success;
		}

		ParseResult HandleHeader(ref int start, int lineEnd, ITextSnapshot s, SnapshotSpan lineSpan, ICollection<TaggedContentSpan> results, IClassificationTag lastTag) {
			if (IsInRawContentBlock(lastTag)) {
				return ParseResult.Failure;
			}

			// 修复：n 初始化为 1
			int n = 1;
			int lineStart = start;
			char c;
			while (++start < lineEnd) {
				if ((c = s[start]) == '#') {
					++n;
					if (n == 7) {
						return ParseResult.Failure;
					}
					continue;
				}
				if (c.IsCodeWhitespaceOrNewLine()) {
					++start;
					break;
				}
				return ParseResult.Failure;
			}

			// 防止越界
			if (n < 1 || n > 6) return ParseResult.Failure;

			results.Add(_LastTaggedSpan = new TaggedContentSpan(HeaderClassificationTypes[n], s, lineStart, lineEnd - lineStart, start - lineStart, lineEnd - start));
			return ParseResult.Success;
		}

		ParseResult HandleQuote(ref int start, ref char c, int lineEnd, ITextSnapshot s, SnapshotSpan lineSpan, ICollection<TaggedContentSpan> results, IClassificationTag lastTag) {
			if (IsInRawContentBlock(lastTag)) {
				return ParseResult.Failure;
			}

			int contenStart = SkipWhitespace(s, start, lineEnd);
			results.Add(_LastTaggedSpan = new TaggedContentSpan(new MarkdownBlockQuoteTag(__QuotationTag.ClassificationType, start - lineSpan.Start, contenStart != lineEnd), s, lineSpan.Start, lineEnd - lineSpan.Start, contenStart - lineSpan.Start, lineEnd - contenStart));

			if (++start < lineEnd) {
				c = s[start];
				return ParseResult.Retry;
			}
			return ParseResult.Success;
		}

		ParseResult HandleOrderedList(ref int start, int lineEnd, ITextSnapshot s, SnapshotSpan lineSpan, ICollection<TaggedContentSpan> results, IClassificationTag lastTag) {
			if (IsInRawContentBlock(lastTag)) {
				return ParseResult.Failure;
			}

			// 修复：n 初始化为 1
			int n = 1;
			char c = s[start];

			while (++start < lineEnd) {
				if ((c = s[start]).IsBetweenUn('0', '9')) {
					++n;
					if (n == 10) {
						return ParseResult.Failure;
					}
				}
				else if (c.CeqAny('.', ')')) {
					results.Add(_LastTaggedSpan = new TaggedContentSpan(__OrderedListTag, s, lineSpan.Start, lineEnd - lineSpan.Start, start - lineSpan.Start, lineEnd - start));
					return ParseResult.Success;
				}
				else {
					return ParseResult.Failure;
				}
			}
			return ParseResult.Failure;
		}

		ParseResult HandleThematicBreakOrList(ref int start, int lineEnd, ITextSnapshot s, SnapshotSpan lineSpan, ICollection<TaggedContentSpan> results, IClassificationTag lastTag, char c) {
			if (IsInRawContentBlock(lastTag)) {
				return ParseResult.Failure;
			}

			if (IsThematicBreak(s, start, lineEnd, c)) {
				results.Add(_LastTaggedSpan = new TaggedContentSpan(__ThematicBreakTag, s, lineSpan.Start, lineEnd - lineSpan.Start, start - lineSpan.Start, lineEnd - start));
				return ParseResult.Success;
			}

			if (c == '_') {
				return ParseResult.Failure;
			}

			// 委托给无序列表处理
			return HandleUnorderedList(ref start, lineEnd, s, lineSpan, results, lastTag);
		}

		ParseResult HandleUnorderedList(ref int start, int lineEnd, ITextSnapshot s, SnapshotSpan lineSpan, ICollection<TaggedContentSpan> results, IClassificationTag lastTag) {
			if (IsInRawContentBlock(lastTag)) {
				return ParseResult.Failure;
			}

			if (++start < lineEnd && s[start].IsCodeWhitespaceChar()) {
				results.Add(_LastTaggedSpan = new TaggedContentSpan(__UnorderedListTag, s, lineSpan.Start, lineEnd - lineSpan.Start, start - lineSpan.Start, lineEnd - start));
				return ParseResult.Success;
			}
			return ParseResult.Failure;
		}

		ParseResult HandleFence(ref int start, int lineEnd, ITextSnapshot s, SnapshotSpan lineSpan, ICollection<TaggedContentSpan> results, TaggedContentSpan taggedSpan) {
			char c = s[start];
			int n = GetFenceBlockSequence(s, start, lineEnd, c, out bool isOpen);
			if (n >= 3) {
				var lastTag = taggedSpan?.Tag;
				// 闭合 Fence 的处理
				if (lastTag is MarkdownFenceTag lastFence && !lastFence.IsClosed) {
					// 1. 扩展当前 Tag 的范围，使其覆盖当前的闭合行
					taggedSpan.ExtendTo(lineEnd);
					results.Add(taggedSpan);
					if (lastFence.FenceCharacter == c && !isOpen && n >= lastFence.FenceLength) {
						// 2. 标记 Tag 为已关闭，这样下一行解析时 ContinueToNextLine 将返回 false
						lastFence.Close();
					}
					_LastTaggedSpan = taggedSpan;
					return ParseResult.Success;
				}

				// 开启新 Fence 的处理
				if (lastTag is MarkdownHtmlBlockTag) {
					return ParseResult.Failure;
				}

				var newTag = new TaggedContentSpan(new MarkdownFenceTag(__FencedCodeBlockType, c, n), s, lineSpan.Start, lineEnd - lineSpan.Start, start - lineSpan.Start, lineEnd - start);
				results.Add(_LastTaggedSpan = newTag);
				return ParseResult.Success;
			}
			return ParseResult.Failure;
		}

		ParseResult HandleHtml(ref int start, int lineEnd, ITextSnapshot s, SnapshotSpan lineSpan, ICollection<TaggedContentSpan> results, TaggedContentSpan taggedSpan) {
			// 1. 检查是否在 HTML 块内部
			if (taggedSpan?.Tag is MarkdownFenceTag) {
				return ParseResult.Failure; // 在代码块中，HTML 块无效
			}

			// 2. 尝试关闭现有的 HTML 块 (类似 HandleFence 的逻辑)
			if (taggedSpan?.Tag is MarkdownHtmlBlockTag lastHtml && !lastHtml.IsClosed) {
				bool isClosing = false;
				// 简单扫描当前行是否包含结束标记
				// 注意：这里不需要完全解析，只需要找到闭合符号
				switch (lastHtml.Type) {
					case MarkupHtmlType.HtmlComment:
						// 查找 -->
						for (int i = start; i < lineEnd - 2; i++) {
							if (s[i] == '-' && s[i + 1] == '-' && s[i + 2] == '>') {
								isClosing = true;
								break;
							}
						}
						break;
					case MarkupHtmlType.ProcessingInstruction:
						// 查找 ?>
						for (int i = start; i < lineEnd - 1; i++) {
							if (s[i] == '?' && s[i + 1] == '>') {
								isClosing = true;
								break;
							}
						}
						break;
					case MarkupHtmlType.CData:
						// 查找 ]]>
						for (int i = start; i < lineEnd - 2; i++) {
							if (s[i] == ']' && s[i + 1] == ']' && s[i + 2] == '>') {
								isClosing = true;
								break;
							}
						}
						break;
						// DocType 和 General 通常在单行闭合，或者需要匹配 Tag 名（目前较难支持），
						// 暂时不处理 General 的动态关闭，依靠默认行为或编辑时的重新解析
				}

				if (isClosing) {
					lastHtml.Close();
				}
				// 找到结束标记：扩展范围，关闭 Tag
				Result.ClearRange(start, lineEnd - start);
				taggedSpan.ExtendTo(lineEnd);
				results.Add(taggedSpan);
				_LastTaggedSpan = taggedSpan;
				return ParseResult.Success;
			}

			// 3. 处理新 HTML 块的开始
			// 如果当前行已经在某个未关闭的 HTML 块内（且没有找到结束标记），则由 TRY_MERGE 处理扩展
			if (taggedSpan?.Tag is MarkdownHtmlBlockTag) {
				return ParseResult.Failure;
			}

			var (htmlEnd, htmlType) = ParseHtmlBlock(s, start, lineEnd);
			if (htmlEnd == InvalidTag) {
				return ParseResult.Failure;
			}

			var htmlTag = new MarkdownHtmlBlockTag(htmlType == MarkupHtmlType.HtmlComment ? MarkdownClassificationTypes.Default.Comment : MarkdownClassificationTypes.Default.HtmlCodeBlock, htmlType);
			taggedSpan = new TaggedContentSpan(htmlTag, s, lineSpan.Start, lineEnd - lineSpan.Start, start - lineSpan.Start, lineEnd - start);
			results.Add(taggedSpan);

			if (htmlEnd == WaitingEndTag) {
				_LastTaggedSpan = taggedSpan;
			}
			else {
				// 如果是新块且在一行内闭合，标记为已关闭
				htmlTag.Close();
				_LastTaggedSpan = null;
			}
			return ParseResult.Success;
		}


		static bool PeekForNestedList(ref int start, int lineEnd, ITextSnapshot s, ref char c) {
			int nextPos = start + 1;
			if (nextPos >= lineEnd) {
				return false;
			}
			char nextChar = s[nextPos];

			// unordered list
			if (nextChar.CeqAny('-', '+', '*')
				&& (nextPos + 1 >= lineEnd || s[nextPos + 1].IsCodeWhitespaceOrNewLine())) {
				// list confirmed
				c = nextChar;
				start = nextPos;
				return true;
			}

			// ordered list
			if (nextChar >= '0' && nextChar <= '9') {
				int tempPos = nextPos;
				while (tempPos < lineEnd && s[tempPos] >= '0' && s[tempPos] <= '9') {
					tempPos++;
				}
				if (tempPos < lineEnd && tempPos > nextPos) {
					char delimiter = s[tempPos];
					if (delimiter == '.' || delimiter == ')') {
						if (tempPos + 1 >= lineEnd || s[tempPos + 1].IsCodeWhitespaceOrNewLine()) {
							// list comfirmed
							c = nextChar;
							start = nextPos;
							return true;
						}
					}
				}
			}
			return false;
		}

		static bool PeekForIndentedHtml(ref int start, int lineEnd, ITextSnapshot s, ref char c) {
			int checkPos = start + 1;
			while (checkPos < lineEnd && s[checkPos] == ' ') {
				checkPos++;
			}

			if (checkPos < lineEnd && s[checkPos] == '<') {
				var (htmlEnd, htmlType) = ParseHtmlBlock(s, checkPos, lineEnd);

				if (htmlEnd != InvalidTag) {
					// go back and reparse
					c = s[checkPos];
					start = checkPos;
					return true;
				}
			}
			return false;
		}

		TaggedContentSpan GetPrecedingTaggedSpan(SnapshotSpan span) {
			var s = span.Snapshot;
			if (_LastTaggedSpan != null) {
				if (_LastTaggedSpan.Update(s)
					&& IsNextTo(ref span, _LastTaggedSpan.Span)
					&& _LastTaggedSpan.Tag is MarkdownHeadingTag == false) {
					return _LastTaggedSpan;
				}
			}
			if (Result.HasTag == false) {
				return null;
			}

			var spanStart = span.Start;
			var ts = Result.GetPrecedingTaggedSpan(spanStart, _ => true);
			if (ts == null || !ts.Update(s)) {
				return null;
			}
			if (ts.Start > 0) {
				var prev = Result.GetPrecedingTaggedSpan(ts.Span.Start - 1, _ => true);
				if (prev?.Tag is MarkdownTag pt && pt.BlockType.CeqAny(BlockType.FencedBlock, BlockType.HtmlBlock) && prev.Update(s)) {
					if (!pt.IsClosed
						|| pt.ValidateStart(s, prev.Start) && !pt.ValidateEnd(s, prev.End, IsProcessingChanges)) {
						return prev;
					}
				}
			}
			if (ts.Tag is MarkdownHeadingTag) {
				return null;
			}
			if (span.Start.Position.CeqAny(ts.Span.Start.Position, ts.Span.End.Position)
				|| IsNextTo(ref span, ts.Span)) {
				return ts;
			}
			var line = s.GetLineFromPosition(ts.Start);
			var lineCount = s.LineCount;
			int lineNum = line.LineNumber;
			var p = spanStart.Position;
			bool includeEmptyLine = ts.Tag is MarkdownTag mt && mt.MayContainEmptyLine;
			while (++lineNum < lineCount && line.Start.Position < p) {
				line = s.GetLineFromLineNumber(lineNum);
				if (line.Start.Position > p) {
					return null;
				}

				if (includeEmptyLine == false) {
					var contentStart = line.Extent.GetContentStart();
					if (contentStart < 0) {
						return null;
					}
				}
				if (line.Start.Position == p) {
					return ts;
				}
			}
			return null;
		}

		static bool IsInRawContentBlock(IClassificationTag tag) {
			return tag is MarkdownTag m && m.IsRawContentBlock && m.ContinueToNextLine;
		}

		static bool IsNextTo(ref SnapshotSpan next, SnapshotSpan prev) {
			var pp = prev.End.Position;
			var np = next.Start.Position;
			if (pp == np) {
				return true;
			}
			return (np - pp) switch {
				1 => prev.Snapshot[pp].IsNewLine(),
				2 => prev.Snapshot[pp] == '\r' && prev.Snapshot[pp + 1] == '\n',
				_ => false,
			};
		}

		static MarkdownHeadingTag[] InitHeaderClassificationTypes() {
			var r = ServicesHelper.Instance.ClassificationTypeRegistry;
			return [
				null,
				new(r.GetClassificationType(Constants.MarkdownHeading1), 1),
				new(r.GetClassificationType(Constants.MarkdownHeading2), 2),
				new(r.GetClassificationType(Constants.MarkdownHeading3), 3),
				new(r.GetClassificationType(Constants.MarkdownHeading4), 4),
				new(r.GetClassificationType(Constants.MarkdownHeading5), 5),
				new(r.GetClassificationType(Constants.MarkdownHeading6), 6)
			];
		}

		static bool IsThematicBreak(ITextSnapshot snapshot, int start, int end, char ch) {
			char c;
			int n = 1;
			while (++start < end) {
				if ((c = snapshot[start]) == ch) {
					++n;
					continue;
				}
				if (c.IsCodeWhitespaceOrNewLine()) {
					continue;
				}
				return false;
			}
			return n > 2;
		}

		static int SkipWhitespace(ITextSnapshot snapshot, int start, int end) {
			while (++start < end) {
				if (snapshot[start].IsCodeWhitespaceChar() == false) {
					return start;
				}
			}
			return start;
		}

		static int GetFenceBlockSequence(ITextSnapshot snapshot, int start, int end, char ch, out bool isOpen) {
			char c;
			int n = 1;
			isOpen = false;
			while (++start < end) {
				if ((c = snapshot[start]) == ch) {
					++n;
					continue;
				}
				if (c.IsCodeWhitespaceOrNewLine()) {
					if (n > 2) {
						continue;
					}
					return 0;
				}
				isOpen = true;
				break;
			}
			return n > 2 ? n : 0;
		}

		static (int endPos, MarkupHtmlType type) ParseHtmlBlock(ITextSnapshot s, int start, int end) {
			if (start + 1 >= end) {
				return (WaitingEndTag, MarkupHtmlType.General);
			}

			char c = s[start + 1];
			if (c == '!') {
				// match <!
				return MatchCommentCDataOrPI(s, start, end);
			}
			else if (c == '?') {
				// match <?
				return MatchPI(s, start, end);
			}

			// HTML block
			string tagName = ExtractTagName(s, start + 1, end);
			if (!string.IsNullOrEmpty(tagName) && IsBlockLevelTag(tagName)) {
				// match </tagName>
				int closeTagPos = FindCloseTag(s, start, end, tagName);
				if (closeTagPos > 0) {
					return (closeTagPos, MarkupHtmlType.General);
				}
				// self-closing tag or multiline tag
				int tagEnd = FindNext(s, start, end, '>');
				if (tagEnd > 0) {
					// check for self-closing tag
					while (tagEnd > start && s[tagEnd - 1].IsCodeWhitespaceChar()) {
						tagEnd--;
					}
					if (tagEnd > start && s[tagEnd - 1] == '/') {
						return (tagEnd + 1, MarkupHtmlType.General);
					}
					return (WaitingEndTag, MarkupHtmlType.General); // 多行标签：表示这是一个有效的块开始，但未闭合
				}
			}

			return (InvalidTag, MarkupHtmlType.General);
		}

		static (int endPos, MarkupHtmlType type) MatchCommentCDataOrPI(ITextSnapshot s, int start, int end) {
			if (start + 3 >= end) {
				return (InvalidTag, MarkupHtmlType.General);
			}
			var c = s[start + 2];
			if (c == '-') {
				// match <!--
				if (s[start + 3] == '-') {
					return MatchComment(s, start, end);
				}
			}
			else if (c == '[') {
				// match <!CDATA[
				if (start + 8 < end && new SnapshotSpan(s, start + 3, 6).IndexOf("CDATA[") == 0) {
					return MatchCDataSection(s, start, end);
				}
			}
			else if (c == 'D') {
				// match DOCTYPE
				if (start + 8 < end && new SnapshotSpan(s, start + 3, 6).IndexOf("OCTYPE") == 0) {
					return MatchDocType(s, start, end);
				}
			}
			else if (c == 'd') {
				// match doctype
				if (start + 8 < end && new SnapshotSpan(s, start + 3, 6).IndexOf("octype") == 0) {
					return MatchDocType(s, start, end);
				}
			}
			return (InvalidTag, MarkupHtmlType.General);
		}

		static (int endPos, MarkupHtmlType type) MatchComment(ITextSnapshot s, int start, int end) {
			// look for -->
			start = FindNext(s, start + 4, end, '-');
			while (start > 0 && start + 2 < end) {
				if (s[start + 1] == '-' && s[start + 2] == '>') {
					return (start + 3, MarkupHtmlType.HtmlComment);
				}
				start = FindNext(s, start + 1, end, '-');
			}
			return (WaitingEndTag, MarkupHtmlType.HtmlComment);
		}

		static (int endPos, MarkupHtmlType type) MatchPI(ITextSnapshot s, int start, int end) {
			int piEnd = FindNext(s, start + 2, end, '?');
			while (piEnd > 0 && piEnd + 1 < end) {
				if (s[piEnd + 1] == '>') {
					return (piEnd + 2, MarkupHtmlType.ProcessingInstruction);
				}
				piEnd = FindNext(s, piEnd + 1, end, '?');
			}
			return (WaitingEndTag, MarkupHtmlType.ProcessingInstruction);
		}

		static (int endPos, MarkupHtmlType type) MatchDocType(ITextSnapshot s, int start, int end) {
			int docTypeEnd = FindNext(s, start + 9, end, '>');
			if (docTypeEnd > 0) {
				return (docTypeEnd + 1, MarkupHtmlType.DocType);
			}
			return (WaitingEndTag, MarkupHtmlType.DocType);
		}

		static (int endPos, MarkupHtmlType type) MatchCDataSection(ITextSnapshot s, int start, int end) {
			start = FindNext(s, start + 9, end, ']');
			while (start > 0 && start + 2 < end) {
				if (s[start + 1] == ']' && s[start + 2] == '>') {
					return (start + 3, MarkupHtmlType.CData);
				}
				start = FindNext(s, start + 1, end, ']');
			}
			return (WaitingEndTag, MarkupHtmlType.CData);
		}

		static string ExtractTagName(ITextSnapshot s, int start, int end) {
			int pos = start;
			if (pos < end && s[pos] == '!') {
				pos++;
			}
			if (pos < end && s[pos] == '/') {
				pos++;
			}

			int nameStart = pos;
			while (pos < end && !s[pos].IsCodeWhitespaceOrNewLine() && s[pos] != '>' && s[pos] != '/' && s[pos] != '\t') {
				pos++;
			}

			if (pos > nameStart && pos < end) {
				return s.GetText(new SnapshotSpan(s, nameStart, pos - nameStart));
			}
			return string.Empty;
		}

		static bool IsBlockLevelTag(string tagName) {
			var lowerName = tagName.ToLowerInvariant();
			return lowerName == "div" || lowerName == "table" || lowerName == "pre" ||
				   lowerName == "p" || lowerName == "blockquote" || lowerName == "form" ||
				   lowerName == "h1" || lowerName == "h2" || lowerName == "h3" ||
				   lowerName == "h4" || lowerName == "h5" || lowerName == "h6" ||
				   lowerName == "ul" || lowerName == "ol" || lowerName == "li" ||
				   lowerName == "dl" || lowerName == "dt" || lowerName == "dd" ||
				   lowerName == "fieldset" || lowerName == "legend" || lowerName == "section" ||
				   lowerName == "article" || lowerName == "aside" || lowerName == "footer" ||
				   lowerName == "header" || lowerName == "nav" || lowerName == "main";
		}

		static int FindCloseTag(ITextSnapshot s, int start, int end, string tagName) {
			int pos = start + 1;
			string searchStr = "</" + tagName + ">";
			// 简单搜索，不考虑嵌套
			while (pos < end) {
				int found = new SnapshotSpan(s, pos, end - pos).IndexOf(searchStr, 0, true);
				if (found >= 0) {
					return pos + found + searchStr.Length;
				}
				break;
			}
			return WaitingEndTag;
		}

		static int FindNext(ITextSnapshot s, int start, int end, char ch) {
			for (int i = start; i < end; i++) {
				if (s[i] == ch) {
					return i;
				}
			}
			return -1;
		}

		void Buffer_ContentTypeChanged(object sender, ContentTypeChangedEventArgs e) {
			if (e.AfterContentType.LikeContentType(Constants.CodeTypes.Markdown) == false) {
				TextView_Closed(null, EventArgs.Empty);
			}
		}

		void TextView_Closed(object sender, EventArgs e) {
			var view = _TextView;
			if (view != null) {
				_TextView = null;
				view.Closed -= TextView_Closed;
				view.Properties.RemoveProperty(typeof(MarkdownTagger));

				_TextBuffer.ContentTypeChanged -= Buffer_ContentTypeChanged;
				_TextBuffer = null;
			}
		}
	}
}
