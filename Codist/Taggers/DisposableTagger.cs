using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
{
	sealed class DisposableTagger<TTagger, TTag> : ITagger<TTag>, IDisposable
		where TTagger : class, IReusableTagger, ITagger<TTag>
		where TTag : ITag
	{
		TTagger _Tagger;
		public DisposableTagger(TTagger tagger) {
			_Tagger = tagger;
			_Tagger.AddRef();
			_Tagger.TagsChanged += OnTagsChanged;
		}

		void OnTagsChanged(object sender, SnapshotSpanEventArgs e) {
			TagsChanged?.Invoke(sender, e);
		}

		public IEnumerable<ITagSpan<TTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
			return _Tagger.GetTags(spans);
		}

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public void Dispose() {
			if (_Tagger != null) {
				_Tagger.TagsChanged -= OnTagsChanged;
				_Tagger.Release();
				_Tagger = null;
			}
		}
	}
}