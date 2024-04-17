using System;
using System.Collections.Generic;
using CLR;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist
{
	static class MarkdownHelper
	{
		public const char HeadingGlyph = '#';
		public const char UnorderedListGlyph = '-';
		public const string Heading1 = "# ";
		public const string Heading2 = "## ";
		public const string Heading3 = "### ";
		public const string Heading4 = "#### ";
		public const string Heading5 = "##### ";
		public const string Heading6 = "###### ";
		public const string UnorderedList = "- ";
		public const string Quotation = "> ";

		/// <summary>
		/// Applies heading and list style format to each line.
		/// </summary>
		/// <param name="view">The view.</param>
		/// <param name="leadingText">The format text for the style, e.g. "# ", "## ", "- ", etc.</param>
		/// <param name="skipEmptyLine">Whether empty lines and whitespace-only lines should be skipped.</param>
		/// <param name="replaceLeadingGlyph">Remove this glyph and whitespace characters before inserting <paramref name="leadingText"/>.</param>
		/// <param name="allowLeadingWhitespace">Whether <paramref name="leadingText"/> will be inserted after leading whitespace.</param>
		public static void MarkList(ITextView view, string leadingText, bool skipEmptyLine, char replaceLeadingGlyph = '\0', bool allowLeadingWhitespace = false) {
			view.Edit((leadingText, skipEmptyLine, allowLeadingWhitespace, replaceLeadingGlyph), (v, p, edit) => {
				var ts = v.TextSnapshot;
				var startLine = ts.GetLineFromPosition(v.Selection.Start.Position);
				var start = startLine.Start;
				var end = v.Selection.End.Position;
				var endLine = ts.GetLineFromPosition(end);
				do {
					if (p.skipEmptyLine == false
						|| startLine.Length != 0
							&& startLine.CountLinePrecedingWhitespace() < startLine.Length) {
						if (p.allowLeadingWhitespace) {
							start += CountLeadingWhitespace(ts, start, end);
						}
						if (p.replaceLeadingGlyph != 0) {
							var lg = CountLeadingGlyphsIncludingWhitespace(ts, start, startLine.End, p.replaceLeadingGlyph);
							if (lg != 0) {
								edit.Delete(start, lg);
							}
						}
						edit.Insert(start, p.leadingText);
					}
					start = startLine.EndIncludingLineBreak;
					startLine = ts.GetLineFromPosition(start);
				} while (start < end);
			});
		}

		static int CountLeadingWhitespace(ITextSnapshot snapshot, SnapshotPoint lineStart, SnapshotPoint lineEnd) {
			var p = lineStart.Position;
			while (lineStart < lineEnd) {
				var c = snapshot[p];
				if (c.IsCodeWhitespaceChar()) {
					p++;
				}
				else {
					break;
				}
			}
			return p - lineStart.Position;
		}

		static int CountLeadingGlyphsIncludingWhitespace(ITextSnapshot snapshot, SnapshotPoint lineStart, SnapshotPoint lineEnd, char ch) {
			var p = lineStart.Position;
			var m = true;
			while (lineStart < lineEnd) {
				var c = snapshot[p];
				if (m == false) {
					if (c == ch) {
						p++;
						m = true;
						continue;
					}
					else {
						return 0;
					}
				}
				if (c.CeqAny(ch, ' ', '\t')) {
					p++;
				}
				else {
					break;
				}
			}
			return p - lineStart.Position;
		}
	}
}
