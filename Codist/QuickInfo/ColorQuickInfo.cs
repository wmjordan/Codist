using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using CLR;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Codist.QuickInfo
{
	sealed class ColorQuickInfo : SingletonQuickInfoSource
	{
		protected override Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			return Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color)
				? Task.FromResult(InternalGetQuickInfoItem(session))
				: Task.FromResult<QuickInfoItem>(null);
		}

		static QuickInfoItem InternalGetQuickInfoItem(IAsyncQuickInfoSession session) {
			var buffer = session.TextView.TextBuffer;
			var snapshot = session.TextView.TextSnapshot;
			var extent = TextNavigationHelper.GetExtentOfWord(snapshot, session.GetTriggerPoint(snapshot).GetValueOrDefault(), 9);
			if (extent.Length.IsOutside(3, 9)) {
				return null;
			}
			var word = snapshot.GetText(extent);
			SolidColorBrush brush;
			if (String.Equals(word, "rgb", StringComparison.OrdinalIgnoreCase)
				|| String.Equals(word, "rgba", StringComparison.OrdinalIgnoreCase)
				|| String.Equals(word, "hsl", StringComparison.OrdinalIgnoreCase)
				|| String.Equals(word, "hsla", StringComparison.OrdinalIgnoreCase)) {
				var expr = TextNavigationHelper.GetFollowingParenthesesExpression(snapshot, extent.End, Math.Min(snapshot.Length, extent.End + 64));
				if (expr.IsEmpty) {
					return null;
				}
				brush = ColorHelper.ParseColorComponents(snapshot.GetText(expr), word[0] == 'h');
			}
			else {
				brush = ColorHelper.GetBrush(word);
			}
			if (brush == null) {
				if ((extent.Length.CeqAny(6, 8))
					&& extent.Start > 0
					&& Char.IsPunctuation(snapshot.GetText(extent.Start - 1, 1)[0])) {
					word = "#" + word;
				}
				brush = ColorHelper.GetBrush(word);
			}
			return brush != null && session.Mark(nameof(ColorQuickInfoUI))
				? new QuickInfoItem(extent.CreateSnapshotSpan(snapshot).ToTrackingSpan(), new ColorInfoBlock(brush))
				: null;
		}

		static class TextNavigationHelper
		{
			public static Span GetExtentOfWord(ITextSnapshot snapshot, SnapshotPoint position, int scope) {
				if (position.Position.IsOutside(0, snapshot.Length - 1))
					return default;

				var line = position.GetContainingLine().Extent;
				int start = line.Start, end = line.End;
				char c;
				if (Char.IsLetterOrDigit(c = snapshot[position])) {
					start = FindWordStart(line, position, Math.Max(0, start - scope));
				}
				else if (c == '#') {
					start = position;
				}
				else {
					return default;
				}
				end = FindWordEnd(line, position, Math.Min(end, position.Position + scope));
				return Span.FromBounds(start, end);
			}

			static int FindWordStart(SnapshotSpan text, int position, int bound) {
				int start = position;
				char c;
				while (start > bound) {
					if (Char.IsLetterOrDigit(c = text.Snapshot[--start])) {
						continue;
					}
					return c == '#'
						? start
						: start + 1;
				}
				return start;
			}

			static int FindWordEnd(SnapshotSpan text, int position, int bound) {
				int end = position + 1;
				while (end < bound && Char.IsLetterOrDigit(text.Snapshot[end])) {
					end++;
				}
				return end;
			}

			public static Span GetFollowingParenthesesExpression(ITextSnapshot snapshot, int start, int end) {
				start += snapshot.CountPrecedingWhitespace(start, end);
				if (snapshot[start] != '(') {
					return default;
				}
				++start;
				for (int i = start; i < end; i++) {
					if (snapshot[i] == ')') {
						return Span.FromBounds(start, i);
					}
				}
				return default;
			}
		}
	}
}
