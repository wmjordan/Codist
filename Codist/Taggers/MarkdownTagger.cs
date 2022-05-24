using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
{
	sealed class MarkdownTagger : CachedTaggerBase
	{
		internal static readonly ClassificationTag[] HeaderClassificationTypes = InitHeaderClassificationTypes();
		internal static readonly ClassificationTag[] DummyHeaderTags = new ClassificationTag[7] {
			null,
			new ClassificationTag(TextEditorHelper.CreateClassificationCategory(Constants.CodeText)),
			new ClassificationTag(TextEditorHelper.CreateClassificationCategory(Constants.CodeText)),
			new ClassificationTag(TextEditorHelper.CreateClassificationCategory(Constants.CodeText)),
			new ClassificationTag(TextEditorHelper.CreateClassificationCategory(Constants.CodeText)),
			new ClassificationTag(TextEditorHelper.CreateClassificationCategory(Constants.CodeText)),
			new ClassificationTag(TextEditorHelper.CreateClassificationCategory(Constants.CodeText))
		}; // used when syntax highlight is disabled

		readonly ClassificationTag[] _Tags;
		ITextView _TextView;
		ITextBuffer _TextBuffer;

		public MarkdownTagger(ITextView textView, ITextBuffer buffer, bool syntaxHighlightEnabled) : base(textView) {
			_Tags = syntaxHighlightEnabled ? HeaderClassificationTypes : DummyHeaderTags;
			_TextView = textView;
			_TextBuffer = buffer;
			buffer.ContentTypeChanged += Buffer_ContentTypeChanged;
			textView.Closed += TextView_Closed;
		}

		protected override bool DoFullParseAtFirstLoad => true;

		protected override void Parse(SnapshotSpan span, ICollection<TaggedContentSpan> results) {
			var t = span.GetText();
			if (t.Length < 1 || t[0] != '#') {
				Result.ClearRange(span.Start, span.Length);
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

		void Buffer_ContentTypeChanged(object sender, ContentTypeChangedEventArgs e) {
			if (e.AfterContentType.LikeContentType(Constants.CodeTypes.Markdown) == false) {
				TextView_Closed(null, EventArgs.Empty);
			}
		}

		void TextView_Closed(object sender, EventArgs e) {
			var view = _TextView;
			if (view != null) {
				_TextView = null;
				view.Closed -= TextView_Closed;
				view.Properties.RemoveProperty(typeof(MarkdownTagger));

				_TextBuffer.ContentTypeChanged -= Buffer_ContentTypeChanged;
				_TextBuffer = null;
			}
		}
	}
}
