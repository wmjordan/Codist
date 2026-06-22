using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using CLR;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using R = Codist.Properties.Resources;

namespace Codist;

static class EditCommands
{
	public static void JoinSelectedLines(this ITextView view) {
		if (!view.TryGetFirstSelectionSpan(out var span)
			|| view.Selection.Mode != TextSelectionMode.Stream) {
			return;
		}
		bool noSlash = NoSlash(view);
		var history = ServicesHelper.Instance.TextUndoHistory.GetHistory(view.TextBuffer);
		using var transaction = history.CreateTransaction(R.CMD_JoinLines);
		using var edit = view.TextBuffer.CreateEdit();
		foreach (var item in view.Selection.SelectedSpans) {
			JoinSpan(edit, item, noSlash);
		}
		if (edit.HasEffectiveChanges) {
			edit.Apply();
			transaction.Complete();
		}

		static bool NoSlash(ITextView v) {
			var ct = v.TextBuffer.ContentType;
			return ct.IsOfType(Constants.CodeTypes.CSharp)
				|| ct.IsOfType(Constants.CodeTypes.CPlusPlus)
				|| ct.LikeContentType("TypeScript")
				|| ct.LikeContentType("Java");
		}

		static void JoinSpan(ITextEdit edit, SnapshotSpan span, bool noSlash) {
			const char FullWidthSpace = (char)0x3000;
			var t = span.GetText();
			ReusableResourceHolder<StringBuilder> b = ReusableStringBuilder.AcquireDefault(100);
			StringBuilder sb = null;
			var p = 0;
			bool newLine = false;
			char c;
			for (int i = 0; i < t.Length; i++) {
				switch (t[i]) {
					case '\r':
					case '\n':
						newLine = true;
						goto case ' ';
					case ' ':
					case FullWidthSpace:
					case '\t':
						int n;
						for (n = i + 1; n < t.Length; n++) {
							switch (t[n]) {
								case ' ':
								case FullWidthSpace:
								case '\t':
									continue;
								case '/':
									if (newLine && noSlash && (t[n - 1] == '/' || n + 1 < t.Length && t[n + 1] == '/')) {
										continue;
									}
									goto default;
								case '\n':
								case '\r':
									newLine = true;
									continue;
								default:
									goto CHECK_NEW_LINE;
							}
						}
					CHECK_NEW_LINE:
						if (newLine == false) {
							continue;
						}
						if (sb == null) {
							b = ReusableStringBuilder.AcquireDefault(t.Length);
							sb = b.Resource;
						}
						if (p == 0) {
							span = new SnapshotSpan(span.Snapshot, span.Start.Position + i, span.Length - i);
						}
						else {
							sb.Append(t, p, i - p);
						}
						if (i > 0
							&& n < t.Length
							&& !(c = t[n]).CeqAny('.', ')', ']', '>', '\'', '<')
							&& c < 0x2E80
							&& !(c = t[i - 1]).CeqAny('(', '[', '<', '\'', '>')
							&& c < 0x2E80) {
							sb.Append(' ');
						}
						i = n;
						p = n;
						newLine = false;
						break;
				}
			}

			if (p > 0) {
				if (p < t.Length) {
					span = new SnapshotSpan(span.Snapshot, span.Start, span.Length - (t.Length - p));
				}
				edit.Replace(span, sb.ToString());
			}
			b.Dispose();
		}
	}

	public static void CopySelectionWithoutIndentation(this ITextView view) {
		if (!view.HasSingleSelection()
			|| view.Selection.Mode == TextSelectionMode.Box // Don't handle box selection
			) {
			goto BUILTIN_COPY;
		}

		var snapshot = view.TextBuffer.CurrentSnapshot;
		var selection = view.GetPrimarySelectionSpan();
		var startOfSelection = selection.Start.Position;
		var endOfSelection = selection.End.Position;
		ITextSnapshotLine startLine, endLine;
		if (((startLine = snapshot.GetLineFromPosition(startOfSelection)) == null)
			|| (endLine = snapshot.GetLineFromPosition(endOfSelection))?.LineNumber == startLine.LineNumber) {
			goto BUILTIN_COPY;
		}

		var firstLineOffset = startOfSelection - startLine.Start.Position;
		int indentation;
		int p;

		if (firstLineOffset == 0) {
			// The selection starts at the beginning of the first line.
			// Scan line by line, skipping empty and pure-whitespace lines, to find the first line containing non-whitespace text,
			// and use the leading indentation amount of this line as the indentation to be removed.
			indentation = -1;
			for (int lineNum = startLine.LineNumber; lineNum <= endLine.LineNumber; lineNum++) {
				var currentLine = snapshot.GetLineFromLineNumber(lineNum);
				if (currentLine.Extent.IsEmpty) {
					continue;
				}

				p = currentLine.Start.Position;
				while (p < currentLine.End.Position && snapshot[p].IsCodeWhitespaceChar()) {
					++p;
				}
				if (p < currentLine.End.Position) {
					// non-empty line found
					indentation = p - currentLine.Start.Position;
					break;
				}
			}
			if (indentation <= 0) {
				goto BUILTIN_COPY;
			}
		}
		else {
			// The selection starts in the middle of the first line.
			// Check character by character whether the part before the selection is all whitespace, avoiding string allocation by not calling GetText
			for (p = startLine.Start.Position; p < startOfSelection; p++) {
				if (!snapshot[p].IsCodeWhitespaceChar()) {
					goto BUILTIN_COPY;
				}
			}
			indentation = firstLineOffset;
		}

		var spans = new List<SnapshotSpan>();
		var line = startLine;
		var n = startLine.LineNumber;
		var endLineNumber = endLine.LineNumber;
		SnapshotSpan extent;
		int lineStart, indentStart;
		using (var b = ReusableStringBuilder.AcquireDefault(512)) {
			var sb = b.Resource;
			while (true) {
				if (line.Extent.IsEmpty) {
					spans.Add(line.Extent);
					goto NEXT_LINE;
				}
				lineStart = indentStart = line.Start.Position;
				p = lineStart + Math.Min(line.Length - 1, indentation);
				while (indentStart < p) {
					if (snapshot[indentStart].IsCodeWhitespaceChar()) {
						++indentStart;
					}
					else {
						break;
					}
				}
				if (indentStart >= endOfSelection) {
					break;
				}
				extent = new SnapshotSpan(snapshot, Span.FromBounds(indentStart, Math.Min(endOfSelection, line.End.Position)));
				spans.Add(extent);
				sb.Append(extent.GetText().TrimEnd());
			NEXT_LINE:
				if (n < endLineNumber) {
					sb.AppendLine();
					line = snapshot.GetLineFromLineNumber(++n);
				}
				else {
					break;
				}
			}

			var data = new DataObject();
			if (sb.Length < 256 << 10) {
				// don't generate RTF copy if text is too long
				var rtf = ServicesHelper.Instance.RtfService.GenerateRtf(new NormalizedSnapshotSpanCollection(spans), view);
				data.SetText(rtf.TrimEnd(), TextDataFormat.Rtf);
			}
			data.SetText(sb.ToString(), TextDataFormat.UnicodeText);
			try {
				Clipboard.SetDataObject(data, false);
			}
			catch (SystemException) {
				goto BUILTIN_COPY;
			}
		}
		return;

	BUILTIN_COPY:
		TextEditorHelper.ExecuteEditorCommand("Edit.Copy");
	}

	public static void TrimTrailingSpaces(this ITextView view) {
		view.Edit((v, edit) => {
			var snapshot = v.TextSnapshot;
			foreach (var span in v.Selection.SelectedSpans) {
				if (span.IsEmpty) {
					continue;
				}
				var start = span.Start.Position;
				var end = span.End.Position;
				var startLine = snapshot.GetLineFromPosition(start);
				var endLine = snapshot.GetLineFromPosition(end);
				if (startLine.Start.Position != endLine.Start.Position) {
					TrimSpanTrailingSpaces(edit, snapshot, start, startLine.End.Position);
					while (startLine.EndIncludingLineBreak.Position < end
						&& (start = (startLine = snapshot.GetLineFromLineNumber(startLine.LineNumber + 1)).Start.Position) < end) {
						TrimSpanTrailingSpaces(edit, snapshot, start, startLine.End.Position);
					}
				}
				else {
					TrimSpanTrailingSpaces(edit, snapshot, start, end);
				}
			}
		});
	}

	static void TrimSpanTrailingSpaces(ITextEdit edit, ITextSnapshot snapshot, int start, int end) {
		int i;
		for (i = end - 1; i >= start; i--) {
			if (snapshot[i].IsCodeWhitespaceChar() == false) {
				break;
			}
		}
		if (++i != end) {
			edit.Delete(i, end - i);
		}
	}

}
