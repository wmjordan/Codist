using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
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
#pragma warning disable 649
		[Import]
		internal IClassificationTypeRegistryService ClassificationRegistry;

		[Import]
		internal IBufferTagAggregatorFactoryService Aggregator;
#pragma warning restore 649

		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
			var tagger = Aggregator.CreateTagAggregator<IClassificationTag>(buffer);
			var tags = textView.Properties.GetOrCreateSingletonProperty(() => new TaggerResult());
			textView.Closed += (s, args) => { tagger.Dispose(); };
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
		static ClassificationTag _throwClassification;
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
				var t = typeof(CommentStyle);
				var styleNames = Enum.GetNames(t);
				_commentClassifications = new ClassificationTag[styleNames.Length];
				foreach (var styleName in styleNames) {
					var f = t.GetField(styleName);
					var d = f.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
					if (d.Length == 0) {
						continue;
					}
					var ct = registry.GetClassificationType((d[0] as System.ComponentModel.DescriptionAttribute).Description);
					_commentClassifications[(int)f.GetValue(null)] = new ClassificationTag(ct);
				}
			}
			_throwClassification = new ClassificationTag(registry.GetClassificationType(Constants.ThrowKeyword));

            _aggregator = aggregator;
			_tags = tags;
			_codeType = codeType;
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
				tagSpans = _aggregator.GetTags(new SnapshotSpan(snapshot, 0, snapshot.Length));
				_tags.LastParsed = snapshot.Length;
			}
			else {
				var start = spans[0].Start;
				var end = spans[spans.Count - 1].End;

				for (int i = _tags.Tags.Count - 1; i >= 0; i--) {
					var t = _tags.Tags[i];
					if (start <= t.Start && t.Start <= end
						|| start <= t.End && t.End <= end
						|| t.Start <= start && end <= t.End) {

						// remove suspicious tags within parsing range
						if (t.Start >= _tags.LastParsed) {
							_tags.Tags.RemoveAt(i);
						}
						// return cached tags if spans are within parsed tags
						else {
							yield return new TagSpan<ClassificationTag>(new SnapshotSpan(snapshot, t.Start, t.Length), t.Tag);
						}
					}
				}

				// parse the updated part
				if (end > _tags.LastParsed) {
					tagSpans = _aggregator.GetTags(new SnapshotSpan(snapshot, _tags.LastParsed, end.Position - _tags.LastParsed));
					_tags.LastParsed = end;
				}
				else {
					yield break;
				}
			}

			foreach (var tagSpan in tagSpans) {
				var className = tagSpan.Tag.ClassificationType.Classification;
				if (_codeType == CodeType.CSharp) {
					switch (className) {
						case Constants.ClassName:
						case Constants.InterfaceName:
						case Constants.StructName:
						case Constants.EnumName:
							yield return _tags.Add(new TagSpan<ClassificationTag>(tagSpan.Span.GetSpans(snapshot)[0], (ClassificationTag)tagSpan.Tag));
							continue;
						case Constants.PreProcessorKeyword:
							var ss = tagSpan.Span.GetSpans(snapshot)[0];
							var t = ss.GetText();
							if (t == "region" || t == "pragma") {
								yield return _tags.Add(new TagSpan<ClassificationTag>(ss, (ClassificationTag)tagSpan.Tag));
							}
							continue;
						case Constants.Keyword:
							ss = tagSpan.Span.GetSpans(snapshot)[0];
							t = ss.GetText();
							if (t == "throw") {
								yield return _tags.Add(new TagSpan<ClassificationTag>(ss, _throwClassification));
							}
							continue;
						default:
							break;
					}
				}
				
				var c = TagComments(className, snapshot, tagSpan);
				if (c != null) {
					yield return _tags.Add(c);
				}
			}
        }

		TagSpan<ClassificationTag> TagComments(string className, ITextSnapshot snapshot, IMappingTagSpan<IClassificationTag> tagSpan) {
			// find spans that the language service has already classified as comments ...
			if (className.IndexOf("Comment", StringComparison.OrdinalIgnoreCase) == -1) {
				return null;
			}

			var ss = tagSpan.Span.GetSpans(snapshot);
			if (ss.Count == 0) {
				return null;
			}

			// ... and from those, ones that match our comment strings
			var snapshotSpan = ss[0];

			var text = snapshotSpan.GetText();
			if (String.IsNullOrWhiteSpace(text)) {
				return null;
			}

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
    }

}
