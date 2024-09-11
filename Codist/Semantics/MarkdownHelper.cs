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
				int ps, start, end;
				foreach (var line in v.GetSelectedLines()) {
					ps = -1;
					if (p.skipEmptyLine == false
						|| line.Length != 0
							&& (ps = line.CountLinePrecedingWhitespace()) < line.Length) {
						start = line.Start.Position;
						end = line.End.Position;
						if (p.allowLeadingWhitespace) {
							start += ps != -1 ? ps : ts.CountPrecedingWhitespace(start, end);
						}
						if (p.replaceLeadingGlyph != 0) {
							var lg = CountLeadingGlyphsAndWhitespace(ts, start, end, p.replaceLeadingGlyph);
							if (lg != 0) {
								if (lg == p.leadingText.Length && ts.GetText(start, lg) == p.leadingText) {
									continue;
								}
								edit.Delete(start, lg);
							}
						}
						edit.Insert(start, p.leadingText);
					}
				}
			});
		}

		static int CountLeadingGlyphsAndWhitespace(ITextSnapshot snapshot, int lineStart, int lineEnd, char glyph) {
			var p = lineStart;
			var m = true;
			while (lineStart < lineEnd) {
				var c = snapshot[p];
				if (m == false) {
					if (c == glyph) {
						p++;
						m = true;
						continue;
					}
					else {
						return 0;
					}
				}
				if (c.CeqAny(glyph, ' ', '\t')) {
					p++;
				}
				else {
					break;
				}
			}
			return p - lineStart;
		}
	}
}
