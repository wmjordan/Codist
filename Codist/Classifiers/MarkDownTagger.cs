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

namespace Codist.Classifiers
{
	[Export(typeof(IViewTaggerProvider))]
	[ContentType(Constants.CodeTypes.Code)]
	[TagType(typeof(IClassificationTag))]
	sealed class MarkDownTaggerProvider : IViewTaggerProvider
	{
		static readonly IClassificationType[] _HeaderClassificationTypes = new IClassificationType[7];

		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
			if (Config.Instance.Features.MatchFlags(Features.SyntaxHighlight) == false) {
				return null;
			}
			if (textView.TextBuffer.LikeContentType(Constants.CodeTypes.Markdown) == false) {
				return null;
			}
			if (_HeaderClassificationTypes[1] == null) {
				InitHeaderClassificationTypes();
			}
			textView.Closed += TextViewClosed;
			return textView.Properties.GetOrCreateSingletonProperty(() => new MarkDownTagger(textView)) as ITagger<T>;
		}

		static void InitHeaderClassificationTypes() {
			var r = ServicesHelper.Instance.ClassificationTypeRegistry;
			_HeaderClassificationTypes[1] = r.GetClassificationType(Constants.Task1Comment);
			_HeaderClassificationTypes[2] = r.GetClassificationType(Constants.Task2Comment);
			_HeaderClassificationTypes[3] = r.GetClassificationType(Constants.Task3Comment);
			_HeaderClassificationTypes[4] = r.GetClassificationType(Constants.Task4Comment);
			_HeaderClassificationTypes[5] = r.GetClassificationType(Constants.Task5Comment);
			_HeaderClassificationTypes[6] = r.GetClassificationType(Constants.Task6Comment);
		}

		void TextViewClosed(object sender, EventArgs args) {
			var textView = sender as ITextView;
			textView.Closed -= TextViewClosed;
		}

		sealed class MarkDownTagger : ITagger<IClassificationTag>
		{
			readonly ITextView _TextView;
			readonly TaggerResult _Tags;

			public MarkDownTagger(ITextView textView) {
				_TextView = textView;
				_Tags = textView.Properties.GetOrCreateSingletonProperty(() => new TaggerResult());
			}

#pragma warning disable 67
			public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
#pragma warning restore 67

			public IEnumerable<ITagSpan<IClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
				if (spans.Count == 0) {
					yield break;
				}
				IEnumerable<SnapshotSpan> parseSpans = spans;
				var textSnapshot = _TextView.TextSnapshot;

				if (_Tags.LastParsed == 0) {
					// perform a full parse for the first time
					Debug.WriteLine("Full parse");
					parseSpans = textSnapshot.Lines.Select(l => l.Extent);
					_Tags.LastParsed = textSnapshot.Length;
				}

				Parse(textSnapshot, parseSpans);
			}

			void Parse(ITextSnapshot textSnapshot, IEnumerable<SnapshotSpan> parseSpans) {
				foreach (var span in parseSpans) {
					var t = span.GetText();
					if (t.Length > 0 && t[0] == '#') {
						int c = 1, w = 0;
						for (int i = 1; i < t.Length; i++) {
							switch (t[i]) {
								case '#': if (w == 0) { ++c; } continue;
								case ' ': ++w; continue;
							}
							break;
						}
						w += c;
						_Tags.Add(new TaggedContentSpan(textSnapshot, new ClassificationTag(_HeaderClassificationTypes[c]), span.Start, c, w, t.Length - w));
					}
				}
			}
		}
	}
}
