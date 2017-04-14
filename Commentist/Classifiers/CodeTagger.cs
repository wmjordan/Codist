//#define DIAG_TIMING
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Commentist.Classifiers
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
			textView.Closed += (s, args) => { tagger.Dispose(); };
			return new CodeTagger(ClassificationRegistry, tagger) as ITagger<T>;
        }
    }

	enum CodeType
	{
		None, CSharp, Markup
	}

	class CodeTagger : ITagger<ClassificationTag>
    {
		static ClassificationTag[] _classifications;
		readonly ITagAggregator<IClassificationTag> _aggregator;

        static readonly string[] Comments = { "//", "/*", "'", "#", "<!--" };

#pragma warning disable 67
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
#pragma warning restore 67

        internal CodeTagger(IClassificationTypeRegistryService registry, ITagAggregator<IClassificationTag> aggregator)
        {
			if (_classifications == null) {
				var t = typeof(CommentStyle);
				var styleNames = Enum.GetNames(t);
				_classifications = new ClassificationTag[styleNames.Length];
				foreach (var styleName in styleNames) {
					var f = t.GetField(styleName);
					var d = f.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
					if (d.Length == 0) {
						continue;
					}
					var ct = registry.GetClassificationType((d[0] as System.ComponentModel.DescriptionAttribute).Description);
					_classifications[(int)f.GetValue(null)] = new ClassificationTag(ct);
				}
			}
            _aggregator = aggregator;
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

			var codeType = GetCodeType(contentType);
			TagSpan<ClassificationTag> mayBeType = null;
			foreach (var tagSpan in _aggregator.GetTags(spans)) {
				var className = tagSpan.Tag.ClassificationType.Classification;
				if (codeType == CodeType.CSharp) {
					switch (className) {
						case Constants.ClassName:
						case Constants.InterfaceName:
						case Constants.StructName:
						case Constants.EnumName:
							yield return new TagSpan<ClassificationTag>(tagSpan.Span.GetSpans(snapshot)[0], (ClassificationTag)tagSpan.Tag);
							continue;
						case Constants.PreProcessorKeyword:
							var ss = tagSpan.Span.GetSpans(snapshot)[0];
							if (ss.GetText() == "region") {
								yield return new TagSpan<ClassificationTag>(ss, (ClassificationTag)tagSpan.Tag);
							}
							continue;
						default:
							break;
					}
				}
				
				var c = TagComments(className, snapshot, tagSpan, codeType == CodeType.Markup);
				if (c != null) {
					yield return c;
				}
			}
        }

		static TagSpan<ClassificationTag> TagComments(string className, ITextSnapshot snapshot, IMappingTagSpan<IClassificationTag> tagSpan, bool isMarkup) {
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
			foreach (string t in Comments) {
				if (text.StartsWith(t, StringComparison.OrdinalIgnoreCase)) {
					endOfCommentToken = t.Length;
					break;
				}
			}

			if (endOfCommentToken == 0 && !isMarkup) {
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

			var endOfContent = tl;
			if (isMarkup && commentStart > 0) {
				if (!text.EndsWith("-->", StringComparison.Ordinal)) {
					return null;
				}

				endOfContent -= 3;
			}
			//TODO: identify legal block comment start tag
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

				ctag = _classifications[(int)item.StyleID];
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

        private static CodeType GetCodeType(IContentType contentType)
        {
			return contentType.IsOfType("CSharp") ? CodeType.CSharp
				: contentType.IsOfType("html") || contentType.IsOfType("htmlx") || contentType.IsOfType("XAML") || contentType.IsOfType("XML") ? CodeType.Markup
				: CodeType.None;
        }
    }

}
