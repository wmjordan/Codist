using System;
using System.Collections.Generic;
using CLR;
using Microsoft.VisualStudio.Text;
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
		static readonly ClassificationTag __QuotationTag = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationTag(Constants.MarkdownQuotation);
		static readonly ClassificationTag __OrderedListTag = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationTag(Constants.MarkdownOrderedList);
		static readonly ClassificationTag __UnorderedListTag = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationTag(Constants.MarkdownUnorderedList);
		static readonly ClassificationTag __CodeBlockTag = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationTag(Constants.MarkdownCodeBlock);
		static readonly ClassificationTag __ThematicBreakTag = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationTag(Constants.MarkdownThematicBreak);

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
			var lastTaggedSpan = GetPrecedingTaggedSpan(span, span.Start, s);
			PARSE_LEADING_CHAR:
			lineStart = start;
			int n = 0;
			switch (c) {
				case '\t':
				INDENTED_CODE_BLOCK:
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
					if (IsThematicBreak(s, start, lineEnd, c)) {
						results.Add(_LastTaggedSpan = new TaggedContentSpan(__ThematicBreakTag, s, lineStart, lineEnd - lineStart, start - lineStart, lineEnd - start));
						return;
					}

					if (c == '_') {
						goto TRY_MERGE;
					}
					goto case '+';
				case '+': // candidate of bullet
					if (++start < lineEnd) {
						if ((c = s[start]).IsCodeWhitespaceChar()) {
							results.Add(_LastTaggedSpan = new TaggedContentSpan(__OrderedListTag, s, lineStart, lineEnd - lineStart, start - lineStart, lineEnd - start));
							goto PARSE_LEADING_CHAR;
						}
						goto TRY_MERGE;
					}
					else {
						return;
					}
			}

		TRY_MERGE:
			if ((tag = lastTaggedSpan?.Tag) == null
				|| tag is MarkdownTag m && m.ContinueToNextLine == false) {
				goto MISMATCH;
			}
			results.Add(new TaggedContentSpan(tag, span, 0, 0));
		MISMATCH:
			Result.ClearRange(span.Start, l);
		}

		TaggedContentSpan GetPrecedingTaggedSpan(SnapshotSpan span, SnapshotPoint lineStart, ITextSnapshot s) {
			if (_LastTaggedSpan != null) {
				if (_LastTaggedSpan.Update(s)
					&& _LastTaggedSpan.Span.End == span.Start
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
