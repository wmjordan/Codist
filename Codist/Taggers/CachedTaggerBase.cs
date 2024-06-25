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

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public IEnumerable<ITagSpan<IClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
			if (spans.Count == 0) {
				return Array.Empty<ITagSpan<IClassificationTag>>();
			}
			if (_Tags.LastParsed == 0 && DoFullParseAtFirstLoad) {
				return ParseSnapshot();
			}
			var parseSpans = spans;
			_TaggedContents.Clear();
			return ParseSpans(parseSpans);
		}

		// perform a full parse for the first time
		IEnumerable<ITagSpan<IClassificationTag>> ParseSnapshot() {
			System.Diagnostics.Debug.WriteLine("Full parse");
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

		protected abstract void Parse(SnapshotSpan span, ICollection<TaggedContentSpan> results);

		void TextView_TextBufferChanged(object sender, TextContentChangedEventArgs args) {
			if (args.Changes.Count == 0) {
				return;
			}
			_Tags.PurgeOutdatedTags(args);
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
	}
}
