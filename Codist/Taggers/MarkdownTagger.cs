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
		internal static readonly MarkdownTitleTag[] HeaderClassificationTypes = InitHeaderClassificationTypes();
		internal static readonly MarkdownTitleTag[] DummyHeaderTags = new MarkdownTitleTag[7] {
			null,
			new MarkdownTitleTag(TextEditorHelper.CreateClassificationCategory(Constants.CodeText), 1),
			new MarkdownTitleTag(TextEditorHelper.CreateClassificationCategory(Constants.CodeText), 2),
			new MarkdownTitleTag(TextEditorHelper.CreateClassificationCategory(Constants.CodeText), 3),
			new MarkdownTitleTag(TextEditorHelper.CreateClassificationCategory(Constants.CodeText), 4),
			new MarkdownTitleTag(TextEditorHelper.CreateClassificationCategory(Constants.CodeText), 5),
			new MarkdownTitleTag(TextEditorHelper.CreateClassificationCategory(Constants.CodeText), 6)
		}; // used when syntax highlight is disabled
		internal static readonly ClassificationTag QuotationTag = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationTag(Constants.MarkdownQuotation);
		internal static readonly ClassificationTag OrderedListTag = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationTag(Constants.MarkdownOrderedList);
		internal static readonly ClassificationTag UnorderedListTag = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationTag(Constants.MarkdownUnorderedList);

		readonly bool _FullParseAtFirstLoad;
		readonly Action<SnapshotSpan, ICollection<TaggedContentSpan>> _SyntaxParser;
		TaggedContentSpan _LastTaggedSpan;
		ITextView _TextView;
		ITextBuffer _TextBuffer;

		public MarkdownTagger(ITextView textView, ITextBuffer buffer, bool syntaxHighlightEnabled) : base(textView) {
			_SyntaxParser = syntaxHighlightEnabled ? SyntaxHighlightEnabledParser : HeadingOnlyParser;
			_TextView = textView;
			_TextBuffer = buffer;
			_FullParseAtFirstLoad = textView.Roles.Contains(PredefinedTextViewRoles.PreviewTextView) == false
				&& textView.Roles.Contains(PredefinedTextViewRoles.Document);
			buffer.ContentTypeChanged += Buffer_ContentTypeChanged;
			textView.Closed += TextView_Closed;
		}

		protected override bool DoFullParseAtFirstLoad => _FullParseAtFirstLoad;

		protected override void Parse(SnapshotSpan span, ICollection<TaggedContentSpan> results) {
			_SyntaxParser(span, results);
		}

		// This parser is used by MarkdownNaviBar when syntax highlight is disabled
		void HeadingOnlyParser(SnapshotSpan span, ICollection<TaggedContentSpan> results) {
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

		void SyntaxHighlightEnabledParser(SnapshotSpan span, ICollection<TaggedContentSpan> results) {
			int l = span.Length, start, lineStart = start = span.Start, lineEnd = lineStart + l;
			BlockState state = BlockState.Default;
			if (l < 1) {
				_LastTaggedSpan = null;
				goto MISMATCH;
			}

			int n = 0;
			var s = span.Snapshot;
			var empty = true;
			IClassificationTag tag;
			for (int p = lineStart; p < lineEnd; p++) {
				switch (s[p]) {
					case '#':
						if (state != BlockState.Title) {
							state = BlockState.Title;
							n = 1;
							start = p;
						}
						else {
							++n;
						}
						empty = false;
						continue;
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
						empty = false;
						if (state == BlockState.Title) {
							goto default;
						}
						if (state != BlockState.OrderedList
							&& (n = p + 1) < lineEnd) {
							while (s[n].IsBetween('0', '9')) {
								if (++n == lineEnd) {
									goto MISMATCH;
								}
							}
							if (s[n] == '.') {
								state = BlockState.OrderedList;
								start = p = n;
							}
							n = 0;
							goto case ' ';
						}
						break;
					case '*':
					case '-':
					case '+':
						empty = false;
						if (state == BlockState.Title) {
							goto default;
						}
						if (state != BlockState.UnorderedList
							&& p + 1 < lineEnd
							&& s[p + 1].CeqAny(' ', '\t')) {
							state = BlockState.UnorderedList;
							start = ++p;
							goto case ' ';
						}
						goto default;
					case '>':
						empty = false;
						if (state == BlockState.Title) {
							goto default;
						}
						if (state != BlockState.Quotation) {
							state = BlockState.Quotation;
							start = p;
						}
						break;
					case ' ':
					case '\t':
					case '\r':
					case '\n':
						switch (state) {
							case BlockState.Quotation:
								tag = QuotationTag;
								start = p + 1;
								break;
							case BlockState.OrderedList:
								tag = OrderedListTag;
								break;
							case BlockState.UnorderedList:
								tag = UnorderedListTag;
								break;
							case BlockState.Title:
								start += n + 1;
								tag = HeaderClassificationTypes[n];
								results.Add(_LastTaggedSpan = new TaggedContentSpan(tag, span, start - lineStart, lineEnd - start));
								return;
							default:
								continue;
						}
						results.Add(_LastTaggedSpan = new TaggedContentSpan(tag, span, start - lineStart, lineEnd - start));
						state = BlockState.Default;
						start = p;
						break;
					default:
						empty = false;
						if (state == BlockState.Quotation) {
							results.Add(_LastTaggedSpan = new TaggedContentSpan(QuotationTag, span, start - lineStart, lineEnd - start));
						}
						goto TRY_MERGE;
				}
			}
			return;

		TRY_MERGE:
			if (empty) {
				_LastTaggedSpan = null;
				goto MISMATCH;
			}
			if (_LastTaggedSpan?.Update(s) == true) {
				tag = GetPrecedingAdjacentClassificationTag(span, span.Start, s);
				if (tag == null) {
					goto MISMATCH;
				}
				results.Add(new TaggedContentSpan(tag, span, 0, 0));
			}
		MISMATCH:
			Result.ClearRange(lineStart, l);
		}

		IClassificationTag GetPrecedingAdjacentClassificationTag(SnapshotSpan span, SnapshotPoint lineStart, ITextSnapshot s) {
			if (_LastTaggedSpan.Span.End == span.Start && _LastTaggedSpan.Tag is MarkdownTitleTag == false) {
				return _LastTaggedSpan.Tag is MarkdownTitleTag ? null : _LastTaggedSpan.Tag;
			}
			var ts = Result.GetPrecedingTaggedSpan(lineStart, s => true);
			if (ts != null && ts.Tag is MarkdownTitleTag == false && ts.Update(s)) {
				if (span.Start.Position.CeqAny(ts.Span.Start, ts.Span.End)) {
					return ts.Tag;
				}
				var line = s.GetLineFromPosition(ts.Start);
				var lineCount = s.LineCount;
				int lineNum = line.LineNumber;
				while (++lineNum < lineCount && line.Start < lineStart) {
					line = s.GetLineFromLineNumber(lineNum);
					if (line.Start > lineStart) {
						return null;
					}
					var contentStart = line.Extent.GetContentStart();
					if (contentStart < 0) {
						return null;
					}
					if (line.Start == lineStart) {
						return ts.Tag;
					}
				}
			}
			return null;
		}

		static MarkdownTitleTag[] InitHeaderClassificationTypes() {
			var r = ServicesHelper.Instance.ClassificationTypeRegistry;
			return new MarkdownTitleTag[7] {
				null,
				new MarkdownTitleTag(r.GetClassificationType(Constants.MarkdownHeading1), 1),
				new MarkdownTitleTag(r.GetClassificationType(Constants.MarkdownHeading2), 2),
				new MarkdownTitleTag(r.GetClassificationType(Constants.MarkdownHeading3), 3),
				new MarkdownTitleTag(r.GetClassificationType(Constants.MarkdownHeading4), 4),
				new MarkdownTitleTag(r.GetClassificationType(Constants.MarkdownHeading5), 5),
				new MarkdownTitleTag(r.GetClassificationType(Constants.MarkdownHeading6), 6)
			};
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

		enum BlockState
		{
			Default,
			Quotation,
			UnorderedList,
			OrderedList,
			Title,
		}
	}
}
