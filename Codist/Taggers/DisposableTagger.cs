using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
{
	sealed class DisposableTagger<TTagger, TTag> : ITagger<TTag>, IDisposable
		where TTagger : class, IReuseableTagger, ITagger<TTag>
		where TTag : ITag
	{
		TTagger _tagger;
		public DisposableTagger(TTagger tagger) {
			_tagger = tagger;
			_tagger.AddRef();
			_tagger.TagsChanged += OnTagsChanged;
		}

		void OnTagsChanged(object sender, SnapshotSpanEventArgs e) {
			TagsChanged?.Invoke(sender, e);
		}

		public IEnumerable<ITagSpan<TTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
			return _tagger.GetTags(spans);
		}

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public void Dispose() {
			if (_tagger != null) {
				_tagger.TagsChanged -= OnTagsChanged;
				_tagger.Release();
				_tagger = null;
			}
		}
	}
}