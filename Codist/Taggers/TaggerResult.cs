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
		// hack We assume that it is threadsafe to organize the tags with this
		readonly List<TaggedContentSpan> _Tags = new List<TaggedContentSpan>();

		/// <summary>The snapshot version.</summary>
		public int Version { get; set; }
		/// <summary>The first parsed position.</summary>
		public int Start { get; set; }
		/// <summary>The last parsed position.</summary>
		public int LastParsed { get; set; }
		public bool HasTag => _Tags.Count > 0;
		public int Count => _Tags.Count;

		public TaggedContentSpan GetPreceedingTaggedSpan(int position) {
			var tags = _Tags;
			TaggedContentSpan t = null;
			for (int i = tags.Count - 1; i >= 0; i--) {
				var tag = tags[i];
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
			var s = tag.Span;
			if (s.Start < Start) {
				Start = s.Start;
			}
			var tags = _Tags;
			for (int i = tags.Count - 1; i >= 0; i--) {
				if (tags[i].Contains(s.Start)) {
					return tags[i] = tag;
				}
			}
			tags.Add(tag);
			return tag;
		}

		public void PurgeOutdatedTags(TextContentChangedEventArgs args) {
			Debug.WriteLine($"snapshot version: {args.AfterVersion.VersionNumber}");
			foreach (var change in args.Changes) {
				Debug.WriteLine($"change:{change.OldPosition}->{change.NewPosition}");
				var tags = _Tags;
				for (int i = tags.Count - 1; i >= 0; i--) {
					var t = tags[i];
					if (t.Start > change.OldEnd) {
						// shift positions of remained items
						t.Shift(args.After, change.Delta);
					}
					else if (change.OldPosition <= t.End) {
						// remove tags within the updated range
						Debug.WriteLine($"Removed [{t.Start}..{t.End}) {t.Tag.ClassificationType}");
						tags.RemoveAt(i);
					}
				}
			}
			try {
				Version = args.AfterVersion.VersionNumber;
				LastParsed = args.Before.GetLineFromPosition(args.Changes[0].OldPosition).Start.Position;
			}
			catch (ArgumentOutOfRangeException) {
				MessageBox.Show(String.Join("\n",
					"Code margin exception:", args.Changes[0].OldPosition,
					"Before length:", args.Before.Length,
					"After length:", args.After.Length
				));
			}
		}

		public void Reset() {
			Start = LastParsed = 0;
			_Tags.Clear();
		}
	}

	[DebuggerDisplay("{Start}..{End} {Tag.ClassificationType}")]
	sealed class TaggedContentSpan : ITagSpan<IClassificationTag>
	{
		public IClassificationTag Tag { get; }
		public SnapshotSpan Span => new SnapshotSpan(TextSnapshot, Start, Length);
		public int Start { get; private set; }
		public int Length { get; }
		public int ContentOffset { get; private set; }
		public int ContentLength { get; private set; }

		public int End => Start + Length;
		public string Text => TextSnapshot.GetText(Start, Length);
		public string ContentText => TextSnapshot.GetText(Start + ContentOffset, ContentLength);

		public ITextSnapshot TextSnapshot { get; private set; }

		public TaggedContentSpan(ITextSnapshot textSnapshot, IClassificationTag tag, int tagStart, int tagLength, int contentOffset, int contentLength) {
			TextSnapshot = textSnapshot;
			Tag = tag;
			Start = tagStart;
			Length = tagLength;
			ContentOffset = contentOffset;
			ContentLength = contentLength;
		}

		public bool Contains(int position) {
			return position >= Start && position < End;
		}
		public void Shift(ITextSnapshot snapshot, int delta) {
			TextSnapshot = snapshot;
			Start += delta;
		}

		public void SetContent(int start, int length) {
			ContentOffset = start;
			ContentLength = length;
		}

		public override string ToString() {
			return ContentText;
		}
	}
}
