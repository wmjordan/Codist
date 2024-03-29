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
			foreach (var tagger in _Taggers) {
				tagger.GetTags(in span, results);
			}
		}

		public static TextTaggerBase GetStartWithTagger(IClassificationTag tag, string prefix, bool skipLeadingWhitespace, bool ignoreCase) {
			return new StartWithTagger { Prefix = prefix, IgnoreCase = ignoreCase, Tag = tag, SkipLeadingWhitespace = skipLeadingWhitespace };
		}
		public static TextTaggerBase GetContainsTagger(IClassificationTag tag, string pattern, bool ignoreCase) {
			return new ContainsTextTagger { Content = pattern, IgnoreCase = ignoreCase, Tag = tag };
		}
		public static TextTaggerBase GetRegexTagger(IClassificationTag tag, string pattern, int useGroup, bool ignoreCase) {
			return new RegexTagger { Pattern = pattern, IgnoreCase = ignoreCase, UseGroup = useGroup, Tag = tag };
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

			public override void GetTags(in SnapshotSpan span, ICollection<TaggedContentSpan> results) {
				if (SkipLeadingWhitespace) {
					var l = span.Length;
					for (int i = 0; i < l; i++) {
						if (Char.IsWhiteSpace(span.CharAt(i)) == false) {
							if (span.HasTextAtOffset(_Prefix, IgnoreCase, i)) {
								results.Add(new TaggedContentSpan(Tag, span, i += _PrefixLength, l - i));
							}
							return;
						}
					}
				}
				else if (span.StartsWith(_Prefix, IgnoreCase)) {
					results.Add(new TaggedContentSpan(Tag, span, _PrefixLength, span.Length - _PrefixLength));
				}
			}
		}

		sealed class ContainsTextTagger : TextTaggerBase
		{
			string _Content;
			int _ContentLength;

			public string Content { get => _Content; set { _Content = value; _ContentLength = value.Length; } }

			public override void GetTags(in SnapshotSpan span, ICollection<TaggedContentSpan> results) {
				var i = span.IndexOf(_Content, 0, IgnoreCase);
				if (i >= 0) {
					results.Add(new TaggedContentSpan(Tag, span.Snapshot, span.Start.Position + i, span.Length - i, i, span.Length - _ContentLength));
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

			public override void GetTags(in SnapshotSpan span, ICollection<TaggedContentSpan> results) {
				if (_Expression == null) {
					_Expression = new Regex(_Pattern, MakeRegexOptions());
				}
				var m = _Expression.Match(span.GetText());
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

			RegexOptions MakeRegexOptions() {
				return (IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None)
					| RegexOptions.CultureInvariant
					| RegexOptions.Compiled;
			}
		}
	}

	abstract class TextTaggerBase : ITextTagger
	{
		public IClassificationTag Tag { get; set; }
		public bool IgnoreCase { get; set; }

		public abstract void GetTags(in SnapshotSpan span, ICollection<TaggedContentSpan> results);
	}
}
