using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using AppHelpers;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Taggers
{
	[Export(typeof(IViewTaggerProvider))]
	[ContentType(Constants.CodeTypes.Code)]
	[TagType(typeof(IClassificationTag))]
	sealed class MarkdownTaggerProvider : IViewTaggerProvider
	{
		internal static readonly ClassificationTag[] HeaderClassificationTypes = new ClassificationTag[7];
		internal static readonly ClassificationTag[] DummyHeaderTags = new ClassificationTag[7]; // used when syntax highlight is disabled

		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
			// the results produced by the tagger are also reused by the NaviBar and ScrollbarMarker
			if (Config.Instance.Features.HasAnyFlag(Features.SyntaxHighlight | Features.NaviBar | Features.ScrollbarMarkers) == false) {
				return null;
			}
			if (textView.TextBuffer.LikeContentType(Constants.CodeTypes.Markdown) == false) {
				return null;
			}
			if (HeaderClassificationTypes[1] == null) {
				InitHeaderClassificationTypes();
			}
			return textView.Properties.GetOrCreateSingletonProperty(() => new MarkdownTagger(textView, Config.Instance.Features.MatchFlags(Features.SyntaxHighlight))) as ITagger<T>;
		}

		static void InitHeaderClassificationTypes() {
			var r = ServicesHelper.Instance.ClassificationTypeRegistry;
			HeaderClassificationTypes[1] = new ClassificationTag(r.GetClassificationType(Constants.MarkdownHeading1));
			HeaderClassificationTypes[2] = new ClassificationTag(r.GetClassificationType(Constants.MarkdownHeading2));
			HeaderClassificationTypes[3] = new ClassificationTag(r.GetClassificationType(Constants.MarkdownHeading3));
			HeaderClassificationTypes[4] = new ClassificationTag(r.GetClassificationType(Constants.MarkdownHeading4));
			HeaderClassificationTypes[5] = new ClassificationTag(r.GetClassificationType(Constants.MarkdownHeading5));
			HeaderClassificationTypes[6] = new ClassificationTag(r.GetClassificationType(Constants.MarkdownHeading6));
			DummyHeaderTags[1] = new ClassificationTag(TextEditorHelper.CreateClassificationType(Constants.CodeText));
			DummyHeaderTags[2] = new ClassificationTag(TextEditorHelper.CreateClassificationType(Constants.CodeText));
			DummyHeaderTags[3] = new ClassificationTag(TextEditorHelper.CreateClassificationType(Constants.CodeText));
			DummyHeaderTags[4] = new ClassificationTag(TextEditorHelper.CreateClassificationType(Constants.CodeText));
			DummyHeaderTags[5] = new ClassificationTag(TextEditorHelper.CreateClassificationType(Constants.CodeText));
			DummyHeaderTags[6] = new ClassificationTag(TextEditorHelper.CreateClassificationType(Constants.CodeText));
		}

		sealed class MarkdownTagger : CachedTaggerBase
		{
			readonly ClassificationTag[] _Tags;
			public MarkdownTagger(ITextView textView, bool syntaxHighlightEnabled) : base(textView) {
				_Tags = syntaxHighlightEnabled ? HeaderClassificationTypes : DummyHeaderTags;
			}
			protected override bool DoFullParseAtFirstLoad => true;
			protected override void Parse(SnapshotSpan span, ICollection<TaggedContentSpan> results) {
				var t = span.GetText();
				if (t.Length < 1 || t[0] != '#') {
					return;
				}
				int c = 1, w = 0;
				for (int i = 1; i < t.Length; i++) {
					switch (t[i]) {
						case '#': if (w == 0) { ++c; } continue;
						case ' ':
						case '\t': ++w; continue;
					}
					break;
				}
				w += c;
				results.Add(new TaggedContentSpan(_Tags[c], span, w, t.Length - w));
			}
		}
	}
}
