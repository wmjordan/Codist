using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Codist.Controls;
using Microsoft.VisualStudio.Text;

namespace Codist.Taggers
{
	sealed class TaggerResult
	{
		// hack This is used to prevent the syntax classification info function from altering this result
		internal static bool IsLocked;

		// hack We assume that it is thread-safe to organize the tags with this
		readonly SortedSet<TaggedContentSpan> _Tags = new SortedSet<TaggedContentSpan>(Comparer<TaggedContentSpan>.Create((x, y) => {
			if (ReferenceEquals(x, y)) {
				return 0;
			}
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

		public TaggedContentSpan GetPrecedingTaggedSpan(SnapshotPoint position, Predicate<TaggedContentSpan> predicate) {
			var tags = _Tags;
			TaggedContentSpan t = null;
			foreach (var tag in tags.GetViewBetween(new TaggedContentSpan(0, 0), new TaggedContentSpan(position.Position + 1, 0)).Reverse()) {
				if (tag.Contains(position) && predicate(tag)) {
					return tag;
				}
				if (position > tag.Start && (t == null || tag.Start > t.Start) && predicate(tag)) {
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
			//Array.Sort(r, (x, y) => x.Start - y.Start);
			return r;
		}
		public ImmutableArray<TaggedContentSpan> GetTags(Func<TaggedContentSpan, bool> predicate) {
			return ImmutableArray.CreateRange(_Tags.Where(predicate))
				.Sort((x, y) => x.Start - y.Start);
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
				MessageWindow.Error(String.Join("\n",
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
}
