using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
{
	sealed class TaggerResult
	{
		// hack This is used to prevent the syntax classification info function from altering this result
		internal static bool IsLocked;

		// hack We assume that it is threadsafe to organize the tags with this
		readonly SortedSet<TaggedContentSpan> _Tags = new SortedSet<TaggedContentSpan>(Comparer<TaggedContentSpan>.Create((x, y) => {
			var s1 = x.Start;
			var s2 = y.Start;
			return s1 < s2 ?
				x.End <= s2 ? -1 : 0
				: s1 >= y.End ? 1 : 0;
		}));

		/// <summary>The snapshot version.</summary>
		public int Version { get; set; }
		/// <summary>The last parsed position.</summary>
		public int LastParsed { get; set; }
		public bool HasTag => _Tags.Count > 0;
		public int Count => _Tags.Count;

		public TaggedContentSpan GetPreceedingTaggedSpan(int position) {
			var tags = _Tags;
			TaggedContentSpan t = null;
			foreach (var tag in tags.Reverse()) {
				if (tag.Contains(position)) {
					return tag;
				}
				if (position > tag.Start && (t == null || tag.Start > t.Start)) {
					t = tag;
				}
			}
			return t;
		}
		/// <summary>Gets a sorted array which contains parsed tags.</summary>
		public TaggedContentSpan[] GetTags() {
			var tags = _Tags;
			var r = new TaggedContentSpan[tags.Count];
			tags.CopyTo(r);
			Array.Sort(r, (x, y) => x.Start - y.Start);
			return r;
		}
		public TaggedContentSpan Add(TaggedContentSpan tag) {
			if (IsLocked) {
				return tag;
			}
			var tags = _Tags;
			tags.Remove(tag);
			tags.Add(tag);
			return tag;
		}

		public void ClearRange(int start, int length) {
			var span = new TaggedContentSpan(start, length);
			var tags = _Tags;
			while (tags.Remove(span)) {
			}
		}

		public void PurgeOutdatedTags(TextContentChangedEventArgs args) {
			if (Version == args.AfterVersion.VersionNumber) {
				return;
			}
			Debug.WriteLine($"snapshot version: {args.AfterVersion.VersionNumber}");
			var tags = _Tags;
			var after = args.After;
			tags.RemoveWhere(t => t.Update(after) == false);
			// todo remove the item if the following events happen as well:
			// 1. identifier removed before the tag (e.g. "//" before "note")
			// 2. block comment inserted before the tag (e.g. "/*" before tag)
			try {
				Version = args.AfterVersion.VersionNumber;
				LastParsed = args.Before.GetLineFromPosition(args.Changes[0].OldPosition).Start.Position;
			}
			catch (ArgumentOutOfRangeException) {
				MessageBox.Show(String.Join("\n",
					"Code margin exception:", args.Changes[0].OldPosition,
					"Before length:", args.Before.Length,
					"After length:", after.Length
				));
			}
		}

		public void Reset() {
			LastParsed = 0;
			_Tags.Clear();
		}
	}

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
		public string Text => TextSnapshot.GetText(Start, Length);
		public string ContentText => TextSnapshot.GetText(Start + ContentOffset, ContentLength);

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
				if (Tag is IValidationTag t && t.IsValid(span) == false) {
					return false;
				}
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
