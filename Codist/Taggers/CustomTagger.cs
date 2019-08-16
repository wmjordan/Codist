using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.Taggers
{
	sealed class CustomTagger : CachedTaggerBase
	{
		readonly ITextTagger[] _Taggers;

		public CustomTagger(ITextView view, ITextTagger[] taggers) : base(view) {
			_Taggers = taggers;
		}

		protected override TaggedContentSpan Parse(ITextSnapshot textSnapshot, SnapshotSpan span) {
			if (_Taggers.Length == 0) {
				return null;
			}
			var t = span.GetText();
			foreach (var tagger in _Taggers) {
				return tagger.Tag(t);
			}
			return null;
		}
	}

	interface ITextTagger
	{
		TaggedContentSpan Tag(string text);
	}
}
