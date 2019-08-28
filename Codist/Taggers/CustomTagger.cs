using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.Taggers
{
	// todo enable users to define their own simple taggers
	sealed class CustomTagger : CachedTaggerBase
	{
		readonly ITextTagger[] _Taggers;
		readonly bool _FullParse;

		public CustomTagger(ITextView view, ITextTagger[] taggers, bool fullParse) : base(view) {
			_Taggers = taggers;
			_FullParse = fullParse;
		}
		protected override bool DoFullParseAtFirstLoad => _FullParse;
		protected override TaggedContentSpan Parse(SnapshotSpan span) {
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
