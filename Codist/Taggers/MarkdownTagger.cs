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
		internal static readonly MarkdownHeadingTag[] DummyHeaderTags = new MarkdownHeadingTag[7] {
			null,
			new MarkdownHeadingTag(TextEditorHelper.CreateClassificationCategory(Constants.CodeText), 1),
			new MarkdownHeadingTag(TextEditorHelper.CreateClassificationCategory(Constants.CodeText), 2),
			new MarkdownHeadingTag(TextEditorHelper.CreateClassificationCategory(Constants.CodeText), 3),
			new MarkdownHeadingTag(TextEditorHelper.CreateClassificationCategory(Constants.CodeText), 4),
			new MarkdownHeadingTag(TextEditorHelper.CreateClassificationCategory(Constants.CodeText), 5),
			new MarkdownHeadingTag(TextEditorHelper.CreateClassificationCategory(Constants.CodeText), 6)
		}; // used when syntax highlight is disabled
		static readonly ClassificationTag __QuotationTag = new ClassificationTag(MarkdownClassificationTypes.Default.Quotation);
		static readonly ClassificationTag __OrderedListTag = new ClassificationTag(MarkdownClassificationTypes.Default.OrderedList);
		static readonly ClassificationTag __UnorderedListTag = new ClassificationTag(MarkdownClassificationTypes.Default.UnorderedList);
		static readonly ClassificationTag __CodeBlockTag = new MarkdownCodeBlockTag(MarkdownClassificationTypes.Default.CodeBlock);
		static readonly IClassificationType __FencedCodeBlockType = MarkdownClassificationTypes.Default.FencedCodeBlock;
		static readonly MarkdownFenceEndTag __EndOfFenceBlockTag = new MarkdownFenceEndTag(__FencedCodeBlockType);
		static readonly ClassificationTag __ThematicBreakTag = new ClassificationTag(MarkdownClassificationTypes.Default.ThematicBreak);
		const int WaitingEndTag = -1, InvalidTag = -2;

		readonly bool _FullParseAtFirstLoad;
		readonly Action<SnapshotSpan, ICollection<TaggedContentSpan>> _SyntaxParser;
		TaggedContentSpan _LastTaggedSpan;
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
			w += level;
			results.Add(new TaggedContentSpan(DummyHeaderTags[level], span, w, l - w));
			return;

		MISMATCH:
			Result.ClearRange(start, l);
		}

		// we assume that each span is a line in the editor
		void ParseSyntax(SnapshotSpan span, ICollection<TaggedContentSpan> results) {
			int l = span.Length, lineStart, start = span.Start, lineEnd = start + l, contenStart;
			if (l < 1) {
				_LastTaggedSpan = null;
				goto MISMATCH;
			}

			var s = span.Snapshot;
			IClassificationTag tag;
			var c = s[start];
			var lastTag = GetPrecedingTaggedSpan(span, span.Start, s)?.Tag;
		PARSE_LEADING_CHAR:
			lineStart = start;
			int n = 0;
			switch (c) {
				case '\t':
				INDENTED_CODE_BLOCK:
					if (IsInRawContentBlock(lastTag)) {
						goto TRY_MERGE;
					}
					n += 4;
					results.Add(_LastTaggedSpan = new TaggedContentSpan(__CodeBlockTag, s, lineStart, lineEnd - lineStart, start - lineStart, lineEnd - start));
					return;
				case ' ':
					++n;
					while (++start < lineEnd) {
						switch (c = s[start]) {
							case ' ':
								++n;
								if (n == 4) {
									if (PeekForNestedList(ref start, lineEnd, s, ref c)
										|| PeekForIndentedHtml(ref start, lineEnd, s, ref c)) {
										goto PARSE_LEADING_CHAR;
									}
									goto INDENTED_CODE_BLOCK;
								}
								continue;
							case '\t':
								goto INDENTED_CODE_BLOCK;
							case '\r':
							case '\n':
								goto MISMATCH;
							default:
								goto PARSE_LEADING_CHAR;
						}
					}
					goto MISMATCH;
				case '#':
					if (IsInRawContentBlock(lastTag)) {
						goto TRY_MERGE;
					}
					++n;
					while (++start < lineEnd) {
						if ((c = s[start]) == '#') {
							++n;
							if (n == 7) {
								goto TRY_MERGE;
							}
							continue;
						}
						if (c.IsCodeWhitespaceOrNewLine()) {
							++start;
							break;
						}
						goto TRY_MERGE;
					}
					// todo: trim trailing header indicators and whitespaces
					results.Add(_LastTaggedSpan = new TaggedContentSpan(HeaderClassificationTypes[n], s, lineStart, lineEnd - lineStart, start - lineStart, lineEnd - start));
					return;
				case '>':
					if (IsInRawContentBlock(lastTag)) {
						goto TRY_MERGE;
					}
					contenStart = SkipWhitespace(s, start, lineEnd);
					results.Add(_LastTaggedSpan = new TaggedContentSpan(new MarkdownBlockQuoteTag(__QuotationTag.ClassificationType, start - lineStart, contenStart != lineEnd), s, lineStart, lineEnd - lineStart, contenStart - lineStart, lineEnd - contenStart));
					if (++start < lineEnd) {
						c = s[start];
						goto PARSE_LEADING_CHAR;
					}
					return;
				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
					if (IsInRawContentBlock(lastTag)) {
						goto TRY_MERGE;
					}
					++n;
					while (++start < lineEnd) {
						if ((c = s[start]).IsBetweenUn('0', '9')) {
							++n;
							if (n == 10) {
								goto TRY_MERGE;
							}
						}
						else if (c.CeqAny('.', ')')) {
							results.Add(_LastTaggedSpan = new TaggedContentSpan(__OrderedListTag, s, lineStart, lineEnd - lineStart, start - lineStart, lineEnd - start));
							goto PARSE_LEADING_CHAR;
						}
						else {
							goto TRY_MERGE;
						}
					}
					goto TRY_MERGE;
				case '_': // candidate of thematic break
				case '-': // candidate of bullet, thematic break
				case '*': // candidate of bullet, thematic break
					if (IsInRawContentBlock(lastTag)) {
						goto TRY_MERGE;
					}
					if (IsThematicBreak(s, start, lineEnd, c)) {
						results.Add(_LastTaggedSpan = new TaggedContentSpan(__ThematicBreakTag, s, lineStart, lineEnd - lineStart, start - lineStart, lineEnd - start));
						return;
					}

					if (c == '_') {
						goto TRY_MERGE;
					}
					goto case '+';
				case '+': // candidate of bullet
					if (IsInRawContentBlock(lastTag)) {
						goto TRY_MERGE;
					}
					if (++start < lineEnd) {
						if ((c = s[start]).IsCodeWhitespaceChar()) {
							results.Add(_LastTaggedSpan = new TaggedContentSpan(__UnorderedListTag, s, lineStart, lineEnd - lineStart, start - lineStart, lineEnd - start));
							goto PARSE_LEADING_CHAR;
						}
						goto TRY_MERGE;
					}
					else {
						return;
					}
				case '`':
				case '~':
					n = GetFenceBlockSequence(s, start, lineEnd, c, out bool isOpen);
					if (n >= 3) {
						// Check if we are closing an existing fence block
						if (lastTag is MarkdownFenceTag lastFence
							&& lastFence.FenceCharacter == c) {
							if (!isOpen && n >= lastFence.FenceLength) {
								results.Add(_LastTaggedSpan = new TaggedContentSpan(__EndOfFenceBlockTag, s, lineStart, lineEnd - lineStart, start - lineStart, lineEnd - start));
								return;
							}
							goto TRY_MERGE;
						}
						if (IsInRawContentBlock(lastTag)) {
							goto TRY_MERGE;
						}

						// It is an opening fence (or an invalid closing fence treated as content)
						results.Add(_LastTaggedSpan = new TaggedContentSpan(new MarkdownFenceTag(__FencedCodeBlockType, c, n), s, lineStart, lineEnd - lineStart, start - lineStart, lineEnd - start));
						return;
					}
					break;
				case '<':  // HTML block
					if (IsInRawContentBlock(lastTag)) {
						goto TRY_MERGE;
					}
					var (htmlEnd, htmlType) = ParseHtmlBlock(s, start, lineEnd);
					if (htmlEnd == InvalidTag) {
						break;
					}

					var htmlTag = new MarkdownHtmlBlockTag(htmlType == MarkupHtmlType.HtmlComment ? MarkdownClassificationTypes.Default.Comment : MarkdownClassificationTypes.Default.HtmlCodeBlock, htmlType);

					results.Add(new TaggedContentSpan(htmlTag, s, lineStart, lineEnd - lineStart, start - lineStart, lineEnd - start));

					if (htmlEnd == WaitingEndTag) {
						_LastTaggedSpan = new TaggedContentSpan(htmlTag, s, lineStart, lineEnd - lineStart, start - lineStart, lineEnd - start);
					}
					else {
						// HTML block ends here
						_LastTaggedSpan = null;
					}
					return;
			}

		TRY_MERGE:
			if ((tag = lastTag) == null
				|| tag is MarkdownTag m && m.ContinueToNextLine == false) {
				goto MISMATCH;
			}
			results.Add(_LastTaggedSpan = new TaggedContentSpan(tag, span, 0, 0));
		MISMATCH:
			Result.ClearRange(span.Start, l);
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

		TaggedContentSpan GetPrecedingTaggedSpan(SnapshotSpan span, SnapshotPoint lineStart, ITextSnapshot s) {
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

			var ts = Result.GetPrecedingTaggedSpan(lineStart, _ => true);
			if (ts != null && ts.Tag is MarkdownHeadingTag == false && ts.Update(s)) {
				if (span.Start.Position.CeqAny(ts.Span.Start, ts.Span.End)) {
					return ts;
				}
				var line = s.GetLineFromPosition(ts.Start);
				var lineCount = s.LineCount;
				int lineNum = line.LineNumber;
				var p = lineStart.Position;
				bool includeEmptyLine = ts.Tag is MarkdownFenceTag;
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
			}
			return null;
		}

		static bool IsInRawContentBlock(IClassificationTag tag) {
			return tag is MarkdownTag m && m.IsRawContentBlock;
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
			return new MarkdownHeadingTag[7] {
				null,
				new MarkdownHeadingTag(r.GetClassificationType(Constants.MarkdownHeading1), 1),
				new MarkdownHeadingTag(r.GetClassificationType(Constants.MarkdownHeading2), 2),
				new MarkdownHeadingTag(r.GetClassificationType(Constants.MarkdownHeading3), 3),
				new MarkdownHeadingTag(r.GetClassificationType(Constants.MarkdownHeading4), 4),
				new MarkdownHeadingTag(r.GetClassificationType(Constants.MarkdownHeading5), 5),
				new MarkdownHeadingTag(r.GetClassificationType(Constants.MarkdownHeading6), 6)
			};
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
