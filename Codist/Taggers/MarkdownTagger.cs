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
		internal static readonly ClassificationTag[] HeaderClassificationTypes = InitHeaderClassificationTypes();
		internal static readonly ClassificationTag[] DummyHeaderTags = new ClassificationTag[7] {
			null,
			new ClassificationTag(TextEditorHelper.CreateClassificationType(Constants.CodeText)),
			new ClassificationTag(TextEditorHelper.CreateClassificationType(Constants.CodeText)),
			new ClassificationTag(TextEditorHelper.CreateClassificationType(Constants.CodeText)),
			new ClassificationTag(TextEditorHelper.CreateClassificationType(Constants.CodeText)),
			new ClassificationTag(TextEditorHelper.CreateClassificationType(Constants.CodeText)),
			new ClassificationTag(TextEditorHelper.CreateClassificationType(Constants.CodeText))
		}; // used when syntax highlight is disabled

		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
			// the results produced by the tagger are also reused by the NaviBar and ScrollbarMarker
			if (Config.Instance.Features.HasAnyFlag(Features.SyntaxHighlight | Features.NaviBar | Features.ScrollbarMarkers) == false) {
				return null;
			}
			if (textView.TextBuffer.LikeContentType(Constants.CodeTypes.Markdown) == false) {
				return null;
			}
			return textView.Properties.GetOrCreateSingletonProperty(() => new MarkdownTagger(textView, Config.Instance.Features.MatchFlags(Features.SyntaxHighlight))) as ITagger<T>;
		}

		static ClassificationTag[] InitHeaderClassificationTypes() {
			var r = ServicesHelper.Instance.ClassificationTypeRegistry;
			return new ClassificationTag[7] {
				null,
				new ClassificationTag(r.GetClassificationType(Constants.MarkdownHeading1)),
				new ClassificationTag(r.GetClassificationType(Constants.MarkdownHeading2)),
				new ClassificationTag(r.GetClassificationType(Constants.MarkdownHeading3)),
				new ClassificationTag(r.GetClassificationType(Constants.MarkdownHeading4)),
				new ClassificationTag(r.GetClassificationType(Constants.MarkdownHeading5)),
				new ClassificationTag(r.GetClassificationType(Constants.MarkdownHeading6))
			};
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
