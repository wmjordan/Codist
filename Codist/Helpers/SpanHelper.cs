using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CLR;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist;

static class SpanHelper
{
	public static bool Contains(this TextSpan span, int position, bool inclusive) {
		return span.Contains(position) || (inclusive && span.End == position);
	}
	public static bool Contains(this SnapshotSpan span, int position, bool inclusive) {
		return span.Contains(position) || (inclusive && span.End == position);
	}
	public static bool Contains(this TextSpan span, ITextSelection selection, bool inclusive) {
		var start = selection.Start.Position.Position;
		var end = selection.End.Position.Position;
		return span.Contains(start) && (span.Contains(end) || inclusive && span.End == end);
	}

	public static SnapshotSpan CreateSnapshotSpan(this TextSpan span, ITextSnapshot snapshot) {
		return span.End <= snapshot.Length
			? new SnapshotSpan(snapshot, span.Start, span.Length)
			: default;
	}
	public static SnapshotSpan CreateSnapshotSpan(this Span span, ITextSnapshot snapshot) {
		return span.End < snapshot.Length
			? new SnapshotSpan(snapshot, span.Start, span.Length)
			: default;
	}
	public static SnapshotSpan ToSnapshotSpan(this ITextSnapshot snapshot) {
		return new SnapshotSpan(snapshot, 0, snapshot.Length);
	}
	public static Span ToSpan(this TextSpan span) {
		return new Span(span.Start, span.Length);
	}
	public static TextSpan ToTextSpan(this SnapshotSpan span) {
		return new TextSpan(span.Start, span.Length);
	}
	public static Span ToSpan(this SnapshotSpan span) {
		return new Span(span.Start, span.Length);
	}
	public static TextSpan ToTextSpan(this Span span) {
		return new TextSpan(span.Start, span.Length);
	}
	public static ITrackingSpan ToTrackingSpan(this SnapshotSpan span) {
		return span.Snapshot.CreateTrackingSpan(span.ToSpan(), SpanTrackingMode.EdgeInclusive);
	}

	public static Span GetLineSpan(this SnapshotSpan span) {
		return Span.FromBounds(span.Snapshot.GetLineNumberFromPosition(span.Start),
			span.Snapshot.GetLineNumberFromPosition(span.End));
	}
	/// <summary>
	/// <para>Gets the start and end line number from a given <see cref="TextSpan"/>.</para>
	/// <para>Note: Make sure that the <paramref name="span"/> is within the <paramref name="snapshot"/>.</para>
	/// </summary>
	public static Span GetLineSpan(this ITextSnapshot snapshot, TextSpan span) {
		return Span.FromBounds(snapshot.GetLineNumberFromPosition(span.Start),
			snapshot.GetLineNumberFromPosition(span.End));
	}

	public static SnapshotSpan MapTo(this ITextSnapshot oldSnapshot, Span span, ITextSnapshot newSnapshot) {
		return new SnapshotSpan(oldSnapshot.CreateTrackingPoint(span.Start, PointTrackingMode.Negative).GetPoint(newSnapshot), span.Length);
	}

	public static char CharAt(this SnapshotSpan span, int index) {
		return span.Snapshot[span.Start.Position + index];
	}
	public static int IndexOf(this SnapshotSpan span, string text, int offset = 0, bool ignoreCase = false) {
		return ignoreCase ? span.IndexOfIgnoreCase(text, offset) : span.IndexOf(text, offset);
	}
	public static int IndexOf(this SnapshotSpan span, string text, int offset, int count, bool ignoreCase = false) {
		if (offset + count < span.Length) {
			span = new SnapshotSpan(span.Start + offset, count);
		}
		return ignoreCase ? span.IndexOfIgnoreCase(text, 0) : span.IndexOf(text, 0);
	}
	static int IndexOf(this SnapshotSpan span, string text, int offset = 0) {
		int i, l = text.Length;
		if (span.Length < l) {
			return -1;
		}
		var snapshot = span.Snapshot;
		var start = offset = span.Start.Position + offset;
		var end = span.End.Position - l;
		char t0 = text[0];
		while (offset <= end) {
			if (snapshot[offset] == t0) {
				for (i = 1; i < l; i++) {
					if (snapshot[offset + i] != text[i]) {
						goto NEXT;
					}
				}
				return offset - start;
			}
		NEXT:
			offset++;
		}
		return -1;
	}
	static int IndexOfIgnoreCase(this SnapshotSpan span, string text, int offset = 0) {
		int i, l = text.Length;
		if (span.Length < l) {
			return -1;
		}
		var snapshot = span.Snapshot;
		var start = offset = span.Start.Position + offset;
		var end = span.End.Position - l;
		char t0 = text[0];
		while (offset < end) {
			if (snapshot[offset] == t0) {
				for (i = 1; i < l; i++) {
					if (AreEqualIgnoreCase(snapshot[start + i], text[i]) == false) {
						goto NEXT;
					}
				}
				return offset - start;
			}
		NEXT:
			offset++;
		}
		return -1;
	}
	public static bool HasTextAtOffset(this SnapshotSpan span, string text, bool ignoreCase = false, int offset = 0) {
		return ignoreCase ? span.HasTextAtOffsetIgnoreCase(text, offset) : span.HasTextAtOffset(text, offset);
	}
	public static bool StartsWith(this SnapshotSpan span, string text) {
		return span.HasTextAtOffset(text, 0);
	}
	public static bool StartsWith(this SnapshotSpan span, string text, bool ignoreCase = false) {
		return ignoreCase ? span.HasTextAtOffsetIgnoreCase(text, 0) : span.HasTextAtOffset(text, 0);
	}
	public static bool EndsWith(this SnapshotSpan span, string text) {
		return span.HasTextAtOffset(text, span.Length - text.Length);
	}
	static bool HasTextAtOffset(this SnapshotSpan span, string text, int offset) {
		int l;
		if (span.Length < offset + (l = text.Length)) {
			return false;
		}
		var snapshot = span.Snapshot;
		var start = span.Start.Position + offset;
		for (int i = 0; i < l; i++) {
			if (snapshot[start + i] != text[i]) {
				return false;
			}
		}
		return true;
	}
	static bool HasTextAtOffsetIgnoreCase(this SnapshotSpan span, string text, int offset) {
		int l;
		if (span.Length < offset + (l = text.Length)) {
			return false;
		}
		var snapshot = span.Snapshot;
		var start = span.Start.Position + offset;
		for (int i = 0; i < l; i++) {
			if (AreEqualIgnoreCase(snapshot[start + i], text[i]) == false) {
				return false;
			}
		}
		return true;
	}
	static bool AreEqualIgnoreCase(char a, char b) {
		return a == b
			|| a.IsBetween('a', 'z') && a - ('a' - 'A') == b
			|| a.IsBetween('A', 'Z') && a + ('a' - 'A') == b;
	}
	public static bool IsEmptyOrWhitespace(this SnapshotSpan span) {
		if (span.Length == 0) {
			return true;
		}
		var snapshot = span.Snapshot;
		var start = span.Start.Position;
		var end = span.End.Position;
		for (int i = start; i < end; i++) {
			if (snapshot[i].IsCodeWhitespaceChar() == false) {
				return false;
			}
		}
		return true;
	}
	public static int GetContentStart(this SnapshotSpan span) {
		if (span.Length == 0) {
			return -1;
		}
		var snapshot = span.Snapshot;
		var start = span.Start.Position;
		var end = span.End.Position;
		for (int i = start; i < end; i++) {
			if (snapshot[i].IsCodeWhitespaceChar() == false) {
				return i;
			}
		}
		return -1;
	}
	public static SnapshotSpan TrimWhitespace(this SnapshotSpan span) {
		if (span.Length == 0) {
			return span;
		}
		var snapshot = span.Snapshot;
		var start = span.Start.Position;
		var end = span.End.Position;
		bool trim = false;
		int i;
		for (i = start; i < end; i++) {
			if (snapshot[i].IsCodeWhitespaceChar() == false) {
				start = i;
				trim = true;
				break;
			}
		}
		if (i == end) {
			return new SnapshotSpan(snapshot, start, 0);
		}
		for (i = end - 1; i > start; i++) {
			if (snapshot[i].IsCodeWhitespaceChar() == false) {
				end = i;
				trim = true;
				break;
			}
		}
		return trim ? new SnapshotSpan(snapshot, start, end - start) : span;
	}
}
