using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
{
	sealed class CustomTagger : CachedTaggerBase
	{
		readonly ITextTagger[] _Taggers;
		readonly bool _FullParse;

		public CustomTagger(ITextView view, ITextTagger[] taggers, bool fullParse) : base(view) {
			_Taggers = taggers;
			_FullParse = fullParse;
		}
		protected override bool DoFullParseAtFirstLoad => _FullParse;
		protected override void Parse(SnapshotSpan span, ICollection<TaggedContentSpan> results) {
			if (_Taggers.Length == 0) {
				return;
			}
			var t = span.GetText();
			foreach (var tagger in _Taggers) {
				tagger.GetTags(t, ref span, results);
			}
		}

		public static TextTaggerBase GetStartWithTagger(IClassificationTag tag, string prefix, bool skipLeadingWhitespace, StringComparison comparison) {
			return new StartWithTagger { Prefix = prefix, StringComparison = comparison, Tag = tag, SkipLeadingWhitespace = skipLeadingWhitespace };
		}
		public static TextTaggerBase GetRegexTagger(IClassificationTag tag, string pattern, int useGroup, StringComparison comparison) {
			return new RegexTagger { Pattern = pattern, StringComparison = comparison, UseGroup = useGroup, Tag = tag };
		}

		sealed class StartWithTagger : TextTaggerBase
		{
			string _Prefix;
			int _PrefixLength;

			public bool SkipLeadingWhitespace { get; set; }
			public string Prefix {
				get { return _Prefix; }
				set { _Prefix = value; _PrefixLength = value.Length; }
			}

			public override void GetTags(string text, ref SnapshotSpan span, ICollection<TaggedContentSpan> results) {
				if (SkipLeadingWhitespace) {
					for (int i = 0; i < text.Length; i++) {
						if (Char.IsWhiteSpace(text[i]) == false) {
							if (text.IndexOf(_Prefix, i, _PrefixLength, StringComparison) == i) {
								results.Add(new TaggedContentSpan(Tag, span, i += _PrefixLength, text.Length - i));
							}
							return;
						}
					}
				}
				else if (text.StartsWith(Prefix, StringComparison)) {
					results.Add(new TaggedContentSpan(Tag, span, _PrefixLength, text.Length - _PrefixLength));
				}
			}
		}

		sealed class ContainsTextTagger : TextTaggerBase
		{
			string _Content;
			int _ContentLength;

			public string Content { get => _Content; set { _Content = value; _ContentLength = value.Length; } }

			public override void GetTags(string text, ref SnapshotSpan span, ICollection<TaggedContentSpan> results) {
				int i = text.IndexOf(_Content, StringComparison);
				if (i >= 0) {
					results.Add(new TaggedContentSpan(Tag, span.Snapshot, span.Start.Position + i, span.Length - i, i, text.Length - _ContentLength));
				}
			}
		}

		sealed class RegexTagger : TextTaggerBase
		{
			Regex _Expression;
			string _Pattern;

			public string Pattern {
				get => _Pattern;
				set { _Pattern = value; _Expression = null; }
			}
			public int UseGroup { get; set; }

			public override void GetTags(string text, ref SnapshotSpan span, ICollection<TaggedContentSpan> results) {
				if (_Expression == null) {
					_Expression = new Regex(_Pattern, MakeRegexOptions());
				}
				var m = _Expression.Match(text);
				if (m.Success == false) {
					return;
				}
				if (UseGroup > 0) {
					if (m.Groups.Count >= UseGroup) {
						var g = m.Groups[UseGroup];
						if (g.Success) {
							results.Add(new TaggedContentSpan(Tag, span.Snapshot, span.Start.Position + g.Index, g.Length, 0, g.Length));
						}
					}
				}
				else {
					results.Add(new TaggedContentSpan(Tag, span.Snapshot, span.Start.Position + m.Index, m.Length, 0, m.Length));
				}
			}

			private RegexOptions MakeRegexOptions() {
				var c = StringComparison;
				return (c == StringComparison.OrdinalIgnoreCase || c == StringComparison.CurrentCultureIgnoreCase || c == StringComparison.InvariantCultureIgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None)
					| (c != StringComparison.CurrentCultureIgnoreCase && c != StringComparison.CurrentCulture ? RegexOptions.CultureInvariant : RegexOptions.None)
					| RegexOptions.Compiled;
			}
		}
	}

	abstract class TextTaggerBase : ITextTagger
	{
		public IClassificationTag Tag { get; set; }
		public virtual StringComparison StringComparison { get; set; }

		public abstract void GetTags(string text, ref SnapshotSpan span, ICollection<TaggedContentSpan> results);
	}
}
