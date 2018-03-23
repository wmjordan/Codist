using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using AppHelpers;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Classifiers
{
	[Export(typeof(ITaggerProvider))]
	[ContentType(Constants.CodeTypes.CSharp)]
	[TagType(typeof(ICodeMemberTag))]
	sealed class CSharpBlockTaggerProvider : ITaggerProvider
	{
		public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
			if (Config.Instance.Features.MatchFlags(Features.SyntaxHighlight) == false || typeof(T) != typeof(ICodeMemberTag)) {
				return null;
			}

			var tagger = buffer.Properties.GetOrCreateSingletonProperty(
				typeof(CSharpBlockTaggerProvider),
				() => new CSharpBlockTagger(buffer)
			);
			return new DisposableTagger(tagger) as ITagger<T>;
		}
	}


	sealed class DisposableTagger : ITagger<ICodeMemberTag>, IDisposable
	{
		CSharpBlockTagger _tagger;
		public DisposableTagger(CSharpBlockTagger tagger) {
			_tagger = tagger;
			_tagger.AddRef();
			_tagger.TagsChanged += OnTagsChanged;
		}

		void OnTagsChanged(object sender, SnapshotSpanEventArgs e) {
			TagsChanged?.Invoke(sender, e);
		}

		public IEnumerable<ITagSpan<ICodeMemberTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
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