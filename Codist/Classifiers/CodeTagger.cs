using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Classifiers
{
	[Export(typeof(IViewTaggerProvider))]
    [ContentType("code")]
    [TagType(typeof(ClassificationTag))]
    public class CodeTaggerProvider : IViewTaggerProvider
    {
		[Import]
		internal IClassificationTypeRegistryService ClassificationRegistry = null;

		[Import]
		internal IBufferTagAggregatorFactoryService Aggregator = null;

		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
			var tagger = Aggregator.CreateTagAggregator<IClassificationTag>(buffer);
			textView.Closed += (s, args) => { tagger.Dispose(); };
			var tags = textView.Properties.GetOrCreateSingletonProperty(() => new TaggerResult());
			var codeTagger = new CodeTagger(ClassificationRegistry, tagger, tags, CodeTagger.GetCodeType(textView.TextBuffer.ContentType));
			tags.Tagger = codeTagger;
			return codeTagger as ITagger<T>;
        }
    }

	enum CodeType
	{
		None, CSharp, Markup
	}

	class CodeTagger : ITagger<ClassificationTag>
    {
		static ClassificationTag[] _commentClassifications;
		static ClassificationTag _exitClassification;
		readonly ITagAggregator<IClassificationTag> _aggregator;
		readonly TaggerResult _tags;
		readonly CodeType _codeType;

		static readonly string[] CSharpComments = { "//", "/*" };
		static readonly string[] Comments = { "//", "/*", "'", "#", "<!--" };

#pragma warning disable 67
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
#pragma warning restore 67

        internal CodeTagger(IClassificationTypeRegistryService registry, ITagAggregator<IClassificationTag> aggregator, TaggerResult tags, CodeType codeType)
        {
			if (_commentClassifications == null) {
				var t = typeof(CommentStyles);
				var styleNames = Enum.GetNames(t);
				_commentClassifications = new ClassificationTag[styleNames.Length];
				foreach (var styleName in styleNames) {
					var f = t.GetField(styleName);
					var d = f.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
					if (d == null) {
						continue;
					}
					var ct = registry.GetClassificationType(d.Description);
					_commentClassifications[(int)f.GetValue(null)] = new ClassificationTag(ct);
				}
			}
			_exitClassification = new ClassificationTag(registry.GetClassificationType(Constants.CodeExitKeyword));

            _aggregator = aggregator;
			_tags = tags;
			_codeType = codeType;
			//_aggregator.TagsChanged += (s, args) => {
			//	ReparseChanged(args.Span);
			//};
			_aggregator.BatchedTagsChanged += (s, args) => {
				if (Margin != null) {
					Margin.InvalidateVisual();
				}
			};
		}

		internal FrameworkElement Margin { get; set; }

		void ReparseChanged(IMappingSpan span) {
			Debug.WriteLine($"reparse changed [{span.Start.GetPoint(span.AnchorBuffer, PositionAffinity.Predecessor).Value.Position}..{span.End.GetPoint(span.AnchorBuffer, PositionAffinity.Predecessor).Value.Position})");
			foreach (var tagSpan in _aggregator.GetTags(span.GetSpans(span.AnchorBuffer))) {
				var className = tagSpan.Tag.ClassificationType.Classification;
				if (_codeType == CodeType.CSharp) {
					switch (className) {
						case Constants.CodeClassName:
						case Constants.CodeInterfaceName:
						case Constants.CodeStructName:
						case Constants.CodeEnumName:
							var start = tagSpan.Span.Start.GetPoint(tagSpan.Span.AnchorBuffer, PositionAffinity.Predecessor).Value.Position;
							var end = tagSpan.Span.End.GetPoint(tagSpan.Span.AnchorBuffer, PositionAffinity.Predecessor).Value.Position;
							Debug.WriteLine($"tag changed add def: {className} [{start}..{end})");
							if (end > start) {
								_tags.Add(start, end, (ClassificationTag)tagSpan.Tag);
							}
							continue;
						case Constants.CodeKeyword:
							//if (Matches(ss, "class") || Matches(ss, "interface") || Matches(ss, "enum") || Matches(ss, "struct")) {
							//	Debug.WriteLine($"find def: {className} at {tagSpan.Span.Start.GetPoint(tagSpan.Span.AnchorBuffer, PositionAffinity.Predecessor).Value.Position}");
							//	yield return _tags.Add(new TagSpan<ClassificationTag>(ss, (ClassificationTag)tagSpan.Tag));
							//}
							var ss = tagSpan.Span.GetSpans(tagSpan.Span.AnchorBuffer)[0];
							if (Matches(ss, "throw") || Matches(ss, "return")) {
								_tags.Add(new TagSpan<ClassificationTag>(ss, _exitClassification));
							}
							continue;
					}
				};
			}
		}

		public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0) {
				yield break;
			}

			var snapshot = spans[0].Snapshot;
			var contentType = snapshot.TextBuffer.ContentType;
            if (!contentType.IsOfType("code")) {
				yield break;
			}
			IEnumerable<IMappingTagSpan<IClassificationTag>> tagSpans;
			if (_tags.LastParsed == 0) {
				// perform a full parse for the first time
				Debug.WriteLine("Full parse");
				tagSpans = _aggregator.GetTags(new SnapshotSpan(snapshot, 0, snapshot.Length));
				_tags.LastParsed = snapshot.Length;
			}
			else {
				var start = spans[0].Start;
				var end = spans[spans.Count - 1].End;
				Debug.WriteLine($"Get tag [{start.Position}..{end.Position})");

				tagSpans = _aggregator.GetTags(spans);
				//for (int i = _tags.Tags.Count - 1; i >= 0; i--) {
				//	var t = _tags.Tags[i];
				//	if (start <= t.Start && t.Start < end
				//		|| start <= t.End && t.End < end
				//		|| t.Start <= start && end < t.End) {

				//		// remove suspicious tags within parsing range
				//		if (t.Start >= _tags.LastParsed) {
				//			_tags.Tags.RemoveAt(i);
				//		}
				//		// return cached tags if spans are within parsed tags
				//		else {
				//			Debug.WriteLine($"reuse cache [{t.Start}..{t.End}) {snapshot.GetText(t.Start, t.Length)} {t.Tag.ClassificationType.Classification}");
				//			yield return new TagSpan<ClassificationTag>(new SnapshotSpan(snapshot, t.Start, t.Length), t.Tag);
				//		}
				//	}
				//}

				//// parse the updated part
				//if (end > _tags.LastParsed) {
				//	Debug.WriteLine($"parse updated {snapshot.GetText(_tags.LastParsed, end.Position - _tags.LastParsed)}");
				//	tagSpans = _aggregator.GetTags(new SnapshotSpan(snapshot, _tags.LastParsed, end.Position - _tags.LastParsed));
				//	_tags.LastParsed = end.Position;
				//}
				//else {
				//	yield break;
				//}
			}

			foreach (var tagSpan in tagSpans) {
				var className = tagSpan.Tag.ClassificationType.Classification;
				var ss = tagSpan.Span.GetSpans(snapshot)[0];
				if (_codeType == CodeType.CSharp) {
					switch (className) {
						case Constants.CodeClassName:
						case Constants.CodeInterfaceName:
						case Constants.CodeStructName:
						case Constants.CodeEnumName:
							Debug.WriteLine($"find def: {className} at {tagSpan.Span.Start.GetPoint(tagSpan.Span.AnchorBuffer, PositionAffinity.Predecessor).Value.Position}");
							yield return _tags.Add(new TagSpan<ClassificationTag>(tagSpan.Span.GetSpans(snapshot)[0], (ClassificationTag)tagSpan.Tag));
							continue;
						case Constants.CodePreprocessorKeyword:
							if (Matches(ss, "region") || Matches(ss, "pragma")) {
								yield return _tags.Add(new TagSpan<ClassificationTag>(ss, (ClassificationTag)tagSpan.Tag));
							}
							continue;
						case Constants.CodeKeyword:
							//if (Matches(ss, "class") || Matches(ss, "interface") || Matches(ss, "enum") || Matches(ss, "struct")) {
							//	Debug.WriteLine($"find def: {className} at {tagSpan.Span.Start.GetPoint(tagSpan.Span.AnchorBuffer, PositionAffinity.Predecessor).Value.Position}");
							//	yield return _tags.Add(new TagSpan<ClassificationTag>(ss, (ClassificationTag)tagSpan.Tag));
							//}
							if (Matches(ss, "throw") || Matches(ss, "return") || Matches(ss, "yield")) {
								yield return _tags.Add(new TagSpan<ClassificationTag>(ss, _exitClassification));
							}
							continue;
						default:
							break;
					}
				}
				
				var c = TagComments(className, ss, tagSpan);
				if (c != null) {
					yield return _tags.Add(c);
				}
			}
        }

		TagSpan<ClassificationTag> TagComments(string className, SnapshotSpan snapshotSpan, IMappingTagSpan<IClassificationTag> tagSpan) {
			// find spans that the language service has already classified as comments ...
			if (className.IndexOf("Comment", StringComparison.OrdinalIgnoreCase) == -1) {
				return null;
			}

			var text = snapshotSpan.GetText();
			//NOTE: markup comment span does not include comment start token
			var endOfCommentToken = 0;
			foreach (string t in _codeType == CodeType.CSharp ? CSharpComments : Comments) {
				if (text.StartsWith(t, StringComparison.OrdinalIgnoreCase)) {
					endOfCommentToken = t.Length;
					break;
				}
			}

			if (endOfCommentToken == 0 && _codeType != CodeType.Markup) {
				return null;
			}

			var tl = text.Length;
			var commentStart = endOfCommentToken;
			while (commentStart < tl) {
				if (Char.IsWhiteSpace(text[commentStart])) {
					++commentStart;
				}
				else {
					break;
				}
			}

			//TODO: code type context-awared end of comment
			var endOfContent = tl;
			if (_codeType == CodeType.Markup && commentStart > 0) {
				if (!text.EndsWith("-->", StringComparison.Ordinal)) {
					return null;
				}

				endOfContent -= 3;
			}
			else if (text.StartsWith("/*", StringComparison.Ordinal)) {
				endOfContent -= 2;
			}

			ClassificationTag ctag = null;
			CommentLabel label = null;
			var startOfContent = 0;
			foreach (var item in Config.Instance.Labels) {
				startOfContent = commentStart + item.LabelLength;
				if (startOfContent >= tl
					|| text.IndexOf(item.Label, commentStart, item.Comparison) != commentStart) {
					continue;
				}

				var followingChar = text[commentStart + item.LabelLength];
				if (item.AllowPunctuationDelimiter && Char.IsPunctuation(followingChar)) {
					startOfContent++;
				}
				else if (!Char.IsWhiteSpace(followingChar)) {
					continue;
				}

				ctag = _commentClassifications[(int)item.StyleID];
				label = item;
				//switch (item.StyleID) {
				//	case CommentStyle.Deletion:
				//		var t = item.Tag;
				//		if (item.TagLength == 2 && t[0] == '/' && t[1] == '/') {
				//			if (!(startOfContent < tl && text[startOfContent] != '/')) {
				//				ctag = null;
				//			}
				//		}
				//		break;
				//	case CommentStyle.ToDo:
				//	case CommentStyle.Note:
				//		break;
				//}
				break;
			}

			if (startOfContent == 0 || ctag == null) {
				return null;
			}

			// ignore whitespaces in content
			while (startOfContent < tl) {
				if (Char.IsWhiteSpace(text, startOfContent)) {
					++startOfContent;
				}
				else {
					break;
				}
			}
			while (endOfContent > startOfContent) {
				if (Char.IsWhiteSpace(text, endOfContent - 1)) {
					--endOfContent;
				}
				else {
					break;
				}
			}

			var span = label.StyleApplication == CommentStyleApplication.Tag
				? new SnapshotSpan(snapshotSpan.Snapshot, snapshotSpan.Start + commentStart, label.LabelLength)
				: label.StyleApplication == CommentStyleApplication.Content
				? new SnapshotSpan(snapshotSpan.Snapshot, snapshotSpan.Start + startOfContent, endOfContent - startOfContent)
				: new SnapshotSpan(snapshotSpan.Snapshot, snapshotSpan.Start + commentStart, endOfCommentToken - commentStart);
			return new TagSpan<ClassificationTag>(span, ctag);
		}

        internal static CodeType GetCodeType(IContentType contentType)
        {
			return contentType.IsOfType("CSharp") ? CodeType.CSharp
				: contentType.IsOfType("html") || contentType.IsOfType("htmlx") || contentType.IsOfType("XAML") || contentType.IsOfType("XML") ? CodeType.Markup
				: CodeType.None;
        }

		static bool Matches(SnapshotSpan span, string text) {
			if (span.Length < text.Length) {
				return false;
			}
			int start = span.Start;
			int end = span.End;
			var s = span.Snapshot;
			// the span can contain white spaces at the start or at the end, skip them
			while (Char.IsWhiteSpace(s[--end]) && end > 0) {
			}
			while (Char.IsWhiteSpace(s[start]) && start < end) {
				start++;
			}
			if (++end - start != text.Length) {
				return false;
			}
			for (int i = start, ti = 0; i < end; i++, ti++) {
				if (s[i] != text[ti]) {
					return false;
				}
			}
			return true;
		}
    }

}
