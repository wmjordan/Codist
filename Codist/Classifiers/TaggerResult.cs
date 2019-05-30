using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Classifiers
{
	sealed class TaggerResult
	{
		/// <summary>The snapshot version.</summary>
		public int Version { get; set; }
		/// <summary>The first parsed position.</summary>
		public int Start { get; set; }
		/// <summary>The last parsed position.</summary>
		public int LastParsed { get; set; }
		/// <summary>The parsed tags.</summary>
		public List<TaggedContentSpan> Tags { get; set; } = new List<TaggedContentSpan>();

		public TaggedContentSpan Add(TaggedContentSpan tag) {
			var s = tag.Span;
			if (s.Start < Start) {
				Start = s.Start;
			}
			for (int i = Tags.Count - 1; i >= 0; i--) {
				if (Tags[i].Contains(s.Start)) {
					return Tags[i] = tag;
				}
			}
			Tags.Add(tag);
			return tag;
		}

		public void Reset() {
			Start = LastParsed = 0;
			Tags.Clear();
		}
	}

	[DebuggerDisplay("{Start}..{End} {Tag.ClassificationType}")]
	sealed class TaggedContentSpan : ITagSpan<IClassificationTag>
	{
		public IClassificationTag Tag { get; }
		public SnapshotSpan Span => new SnapshotSpan(TextSnapshot, Start, Length);
		public int Start { get; private set; }
		public int Length { get; }
		public int ContentOffset { get; }
		public int ContentLength { get; }

		public int End => Start + Length;
		public string Text => Span.GetText();

		public ITextSnapshot TextSnapshot { get; }

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
		public void Shift(int delta) {
			Start += delta;
		}
	}
}
