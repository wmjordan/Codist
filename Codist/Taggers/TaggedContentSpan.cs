using System;
using System.Diagnostics;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
{
	[DebuggerDisplay("{Start}..{End} {Tag.ClassificationType}")]
	sealed class TaggedContentSpan : ITagSpan<IClassificationTag>
	{
		public IClassificationTag Tag { get; }
		public SnapshotSpan Span => new SnapshotSpan(TextSnapshot, Start, Length);
		public ITrackingSpan TrackingSpan { get; private set; }
		public int Start { get; private set; }
		public int Length { get; private set; }
		public int ContentOffset { get; }
		public int ContentLength { get; }

		public int End => Start + Length;
		public string Text => TrackingSpan.GetText(TextSnapshot);
		public string ContentText => (Start + ContentOffset + ContentLength) <= TextSnapshot.Length ?TextSnapshot.GetText(Start + ContentOffset, ContentLength) : String.Empty;

		public ITextSnapshot TextSnapshot { get; private set; }

		public TaggedContentSpan(IClassificationTag tag, SnapshotSpan span, int contentOffset, int contentLength)
			: this(tag, span.Snapshot, span.Start, span.Length, contentOffset, contentLength) {
		}

		public TaggedContentSpan(IClassificationTag tag, ITextSnapshot textSnapshot, int tagStart, int tagLength, int contentOffset, int contentLength) {
			TextSnapshot = textSnapshot;
			TrackingSpan = textSnapshot.CreateTrackingSpan(tagStart, tagLength, SpanTrackingMode.EdgeInclusive);
			Tag = tag;
			Start = tagStart;
			Length = tagLength;
			ContentOffset = contentOffset;
			ContentLength = contentLength;
		}

		internal TaggedContentSpan(int start, int length) {
			Start = start;
			Length = length;
		}

		public bool Contains(int position) {
			return position >= Start && position < End;
		}
		public bool Update(ITextSnapshot snapshot) {
			if (TrackingSpan.TextBuffer != snapshot.TextBuffer) {
				return true;
			}
			var span = TrackingSpan.GetSpan(snapshot);
			if ((Length = span.Length) > 0) {
				Start = span.Start;
				TextSnapshot = snapshot;
				TrackingSpan = snapshot.CreateTrackingSpan(Start, Length, SpanTrackingMode.EdgeInclusive);
				return true;
			}
			return false;
		}

		public override string ToString() {
			return ContentText;
		}
	}
}
