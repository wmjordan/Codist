using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
{
	abstract class CachedTaggerBase : ITagger<IClassificationTag>
	{
		ITextView _TextView;
		TaggerResult _Tags;
		readonly List<TaggedContentSpan> _TaggedContents = new List<TaggedContentSpan>(3);

		protected CachedTaggerBase(ITextView textView) {
			_TextView = textView;
			_TextView.TextBuffer.ChangedLowPriority += TextView_TextBufferChanged;
			_Tags = textView.GetOrCreateSingletonProperty<TaggerResult>();
			textView.Closed += TextView_Closed;
		}

		protected ITextView TextView => _TextView;
		public TaggerResult Result => _Tags;
		protected abstract bool DoFullParseAtFirstLoad { get; }
		public bool Disabled { get; set; }

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public IEnumerable<ITagSpan<IClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
			if (Disabled || spans.Count == 0) {
				return Array.Empty<ITagSpan<IClassificationTag>>();
			}
			if (_Tags.LastParsed == 0 && DoFullParseAtFirstLoad && _Tags.Version == 0) {
				return ParseSnapshot();
			}
			var parseSpans = spans;
			_TaggedContents.Clear();
			return ParseSpans(parseSpans);
		}

		// perform a full parse for the first time
		IEnumerable<ITagSpan<IClassificationTag>> ParseSnapshot() {
			"Full parse".Log();
			var snapshot = _TextView.TextSnapshot;
			foreach (var span in snapshot.Lines.Select(l => l.Extent)) {
				Parse(span, _TaggedContents);
			}
			_Tags.AddRange(_TaggedContents);
			_Tags.LastParsed = snapshot.Length;
			return _TaggedContents;
		}

		IEnumerable<ITagSpan<IClassificationTag>> ParseSpans(IEnumerable<SnapshotSpan> parseSpans) {
			foreach (var span in parseSpans) {
				Parse(span, _TaggedContents);
			}
			foreach (var item in _TaggedContents) {
				yield return item.ContentLength == 0 ? item : _Tags.Add(item);
			}
		}

		protected void OnTagsChanged(SnapshotSpanEventArgs args) {
			if (args == null) {
				args = new SnapshotSpanEventArgs(_TextView.VisualSnapshot.ToSnapshotSpan());
			}
			TagsChanged?.Invoke(this, args);
		}

		protected abstract void Parse(SnapshotSpan span, ICollection<TaggedContentSpan> results);

		void TextView_TextBufferChanged(object sender, TextContentChangedEventArgs args) {
			if (args.Changes.Count == 0) {
				return;
			}
			_Tags.PurgeOutdatedTags(args);
			if (DoFullParseAtFirstLoad) {
				ReparseChangedLines(args);
			}
		}

		void ReparseChangedLines(TextContentChangedEventArgs args) {
			var changedLines = new NormalizedSnapshotSpanCollection(GetLinesOfChanges(args));
			if (changedLines.Count > Math.Max(1000, _TextView.TextSnapshot.LineCount / 3)) {
				// if too many lines are changed, we reparse the whole document,
				// when full parse is required to cache parse results
				_Tags.Reset();
				_Tags.Version = 0;
				_TaggedContents.Clear();
				ParseSnapshot();
			}
			else {
				foreach (var dummy in GetTags(changedLines)) {
				}
			}
		}

		void TextView_Closed(object sender, EventArgs e) {
			if (_TextView != null) {
				_TaggedContents.Clear();
				_TextView.TextBuffer.ChangedLowPriority -= TextView_TextBufferChanged;
				_TextView.Properties.RemoveProperty(typeof(TaggerResult));
				_TextView.Closed -= TextView_Closed;
				_TextView = null;
				_Tags = null;
			}
		}

		static IEnumerable<SnapshotSpan> GetLinesOfChanges(TextContentChangedEventArgs args) {
			var ss = args.After;
			int lastEnd = 0;
			foreach (var item in args.Changes) {
				var line = ss.GetLineFromPosition(item.NewPosition);
				if (line.Start.Position < lastEnd) {
					continue;
				}
				yield return line.Extent;
				int lineEnd = line.EndIncludingLineBreak.Position;
				while (lineEnd < item.NewEnd) {
					line = ss.GetLineFromPosition(lineEnd);
					yield return line.Extent;
					lineEnd = line.EndIncludingLineBreak.Position;
				}
				lastEnd = lineEnd;
			}
		}
	}
}
