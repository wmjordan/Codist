using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
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
	sealed class CommentTaggerProvider : IViewTaggerProvider
	{
		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
			if (Config.Instance.Features.MatchFlags(Features.SyntaxHighlight) == false) {
				return null;
			}
			var codeType = GetCodeType(textView.TextBuffer.ContentType);
			if (codeType == CodeType.None) {
				return null;
			}
			var vp = textView.Properties;
			var tagger = vp.GetOrCreateSingletonProperty(() => ServicesHelper.Instance.BufferTagAggregatorFactory.CreateTagAggregator<IClassificationTag>(buffer));
			var tags = vp.GetOrCreateSingletonProperty(() => new TaggerResult());
			var codeTagger = vp.GetOrCreateSingletonProperty(() => CommentTagger.Create(ServicesHelper.Instance.ClassificationTypeRegistry, tagger, tags, codeType));
			textView.Closed += TextViewClosed;
			return codeTagger as ITagger<T>;
		}

		internal static bool IsCommentTaggable(ITextView view) {
			return GetCodeType(view.TextBuffer.ContentType) != CodeType.None;
		}
		static CodeType GetCodeType(IContentType contentType) {
			return contentType.IsOfType(Constants.CodeTypes.CSharp) ? CodeType.CSharp
				: contentType.IsOfType("html") || contentType.IsOfType("htmlx") || contentType.IsOfType("XAML") || contentType.IsOfType("XML") ? CodeType.Markup
				: contentType.IsOfType("code++.css") ? CodeType.Css
				: contentType.IsOfType("TypeScript") || contentType.IsOfType("JavaScript") ? CodeType.Js
				: contentType.IsOfType("C/C++") ? CodeType.C
				: CodeType.None;
		}

		void TextViewClosed(object sender, EventArgs args) {
			var textView = sender as ITextView;
			textView.Properties.GetProperty<ITagAggregator<IClassificationTag>>(typeof(ITagAggregator<IClassificationTag>))?.Dispose();
			textView.Properties.GetProperty<CommentTagger>(typeof(CommentTagger))?.Dispose();
			textView.Closed -= TextViewClosed;
		}

		abstract class CommentTagger : ITagger<IClassificationTag>, IDisposable
		{
			static ClassificationTag[] __CommentClassifications;
			static readonly Dictionary<IClassificationType, bool> __CommentClasses = new Dictionary<IClassificationType, bool>();
			readonly ITagAggregator<IClassificationTag> _Aggregator;
			readonly TaggerResult _Tags;
#if DEBUG
			readonly HashSet<string> _ClassificationTypes = new HashSet<string>();
#endif

#pragma warning disable 67
			public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
#pragma warning restore 67

			protected CommentTagger(IClassificationTypeRegistryService registry, ITagAggregator<IClassificationTag> aggregator, TaggerResult tags) {
				if (__CommentClassifications == null) {
					var t = typeof(CommentStyleTypes);
					var styleNames = Enum.GetNames(t);
					__CommentClassifications = new ClassificationTag[styleNames.Length];
					foreach (var styleName in styleNames) {
						var f = t.GetField(styleName);
						var d = f.GetCustomAttribute<ClassificationTypeAttribute>();
						if (d == null) {
							continue;
						}
						var ct = registry.GetClassificationType(d.ClassificationTypeNames);
						__CommentClassifications[(int)f.GetValue(null)] = new ClassificationTag(ct);
						__CommentClasses[ct] = true;
					}
				}

				_Aggregator = aggregator;
				_Tags = tags;
				_Aggregator.BatchedTagsChanged += AggregatorBatchedTagsChanged;
			}

			internal FrameworkElement Margin { get; set; }

			protected abstract int GetCommentStartIndex(string comment);
			protected abstract int GetCommentEndIndex(string comment);

			public static CommentTagger Create(IClassificationTypeRegistryService registry, ITagAggregator<IClassificationTag> aggregator, TaggerResult tags, CodeType type) {
				switch (type) {
					case CodeType.CSharp: return new CSharpCommentTagger(registry, aggregator, tags);
					case CodeType.Markup: return new MarkupCommentTagger(registry, aggregator, tags);
					case CodeType.C: return new CCommentTagger(registry, aggregator, tags);
					case CodeType.Css: return new CssCommentTagger(registry, aggregator, tags);
					case CodeType.Js: return new JsCommentTagger(registry, aggregator, tags);
				}
				return null;
			}

			public IEnumerable<ITagSpan<IClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
				if (spans.Count == 0
				|| Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.SpecialComment) == false) {
					yield break;
				}

				var snapshot = spans[0].Snapshot;
				IEnumerable<IMappingTagSpan<IClassificationTag>> tagSpans;
				try {
					if (_Tags.LastParsed == 0) {
						// perform a full parse for the first time
						Debug.WriteLine("Full parse");
						tagSpans = _Aggregator.GetTags(new SnapshotSpan(snapshot, 0, snapshot.Length));
						_Tags.LastParsed = snapshot.Length;
					}
					else {
						//var start = spans[0].Start;
						//var end = spans[spans.Count - 1].End;
						//Debug.WriteLine($"Get tag [{start.Position}..{end.Position})");

						tagSpans = _Aggregator.GetTags(spans);
					}
				}
				catch (ObjectDisposedException ex) {
					// HACK: TagAggregator could be disposed during editing, to be investigated further
					Debug.WriteLine(ex.Message);
					yield break;
				}

				foreach (var tagSpan in tagSpans) {
#if DEBUG
					var c = tagSpan.Tag.ClassificationType.Classification;
					if (_ClassificationTypes.Add(c)) {
						Debug.WriteLine("Classification type: " + c);
					}
#endif
					var ts = TagComments(tagSpan.Span.GetSpans(snapshot)[0], tagSpan);
					if (ts != null) {
						yield return _Tags.Add(ts);
					}
				}
			}

			protected virtual TaggedContentSpan TagComments(SnapshotSpan snapshotSpan, IMappingTagSpan<IClassificationTag> tagSpan) {
				// find spans that the language service has already classified as comments ...
				if (IsComment(tagSpan.Tag.ClassificationType) == false) {
					return null;
				}
				var text = snapshotSpan.GetText();
				//NOTE: markup comment span does not include comment start token
				var endOfCommentStartToken = GetCommentStartIndex(text);
				if (endOfCommentStartToken < 0) {
					return null;
				}
				var tl = text.Length;
				var commentStart = endOfCommentStartToken;
				while (commentStart < tl) {
					if (Char.IsWhiteSpace(text[commentStart])) {
						++commentStart;
					}
					else {
						break;
					}
				}

				var endOfContent = GetCommentEndIndex(text);

				ClassificationTag ctag = null;
				CommentLabel label = null;
				var startOfContent = 0;
				foreach (var item in Config.Instance.Labels) {
					var c = commentStart + item.LabelLength;
					if (c >= tl
						|| text.IndexOf(item.Label, commentStart, item.Comparison) != commentStart) {
						continue;
					}

					var followingChar = text[c];
					if (item.AllowPunctuationDelimiter && Char.IsPunctuation(followingChar)) {
						c++;
					}
					else if (!Char.IsWhiteSpace(followingChar)) {
						continue;
					}

					if (label == null || label.LabelLength < item.LabelLength) {
						ctag = __CommentClassifications[(int)item.StyleID];
						label = item;
						startOfContent = c;
					}
				}

				if (startOfContent == 0 || ctag == null) {
					return null;
				}

				// ignore whitespaces in content
				while (startOfContent < tl) {
					if (Char.IsWhiteSpace(text[startOfContent])) {
						++startOfContent;
					}
					else {
						break;
					}
				}
				while (endOfContent > startOfContent) {
					if (Char.IsWhiteSpace(text[endOfContent - 1])) {
						--endOfContent;
					}
					else {
						break;
					}
				}

				return label.StyleApplication == CommentStyleApplication.Tag
					? new TaggedContentSpan(snapshotSpan.Snapshot, ctag, snapshotSpan.Start + commentStart, label.LabelLength, startOfContent, endOfContent - startOfContent)
					: label.StyleApplication == CommentStyleApplication.Content
					? new TaggedContentSpan(snapshotSpan.Snapshot, ctag, snapshotSpan.Start + startOfContent, endOfContent - startOfContent, startOfContent, endOfContent - startOfContent)
					: new TaggedContentSpan(snapshotSpan.Snapshot, ctag, snapshotSpan.Start + commentStart, endOfContent - commentStart, startOfContent, endOfContent - startOfContent);
			}

			protected static bool Matches(SnapshotSpan span, string text) {
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

			static bool IsComment(IClassificationType classification) {
				return __CommentClasses.TryGetValue(classification, out var c)
					? c
					: (__CommentClasses[classification] = classification.Classification.IndexOf("Comment", StringComparison.OrdinalIgnoreCase) != -1);
			}

			void AggregatorBatchedTagsChanged(object sender, EventArgs args) {
				if (Margin != null) {
					Margin.InvalidateVisual();
				}
			}

			#region IDisposable Support
			private bool disposedValue = false;

			void Dispose(bool disposing) {
				if (!disposedValue) {
					if (disposing) {
						_Aggregator.BatchedTagsChanged -= AggregatorBatchedTagsChanged;
					}
					disposedValue = true;
				}
			}

			public void Dispose() {
				Dispose(true);
			}
			#endregion
		}

		sealed class CCommentTagger : CommentTagger
		{
			public CCommentTagger(IClassificationTypeRegistryService registry, ITagAggregator<IClassificationTag> aggregator, TaggerResult tags) : base(registry, aggregator, tags) {
			}

			protected override int GetCommentStartIndex(string comment) {
				if (comment.Length > 2 && comment[0] == '/' && (comment[1] == '/' || comment[1] == '*')) {
					return 2;
				}
				return -1;
			}
			protected override int GetCommentEndIndex(string comment) {
				return comment.EndsWith("*/", StringComparison.Ordinal) ? comment.Length - 2 : comment.Length;
			}
		}

		sealed class CssCommentTagger : CommentTagger
		{
			public CssCommentTagger(IClassificationTypeRegistryService registry, ITagAggregator<IClassificationTag> aggregator, TaggerResult tags) : base(registry, aggregator, tags) {
			}

			protected override int GetCommentStartIndex(string comment) {
				return 0;
			}
			protected override int GetCommentEndIndex(string comment) {
				return comment == "*/" ? 0 : comment.Length;
			}
		}

		sealed class CSharpCommentTagger : CommentTagger
		{
			readonly IClassificationType _PreprocessorKeyword;

			public CSharpCommentTagger(IClassificationTypeRegistryService registry, ITagAggregator<IClassificationTag> aggregator, TaggerResult tags) : base(registry, aggregator, tags) {
				_PreprocessorKeyword = registry.GetClassificationType("preprocessor keyword");
			}
			protected override TaggedContentSpan TagComments(SnapshotSpan snapshotSpan, IMappingTagSpan<IClassificationTag> tagSpan) {
				if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.CompilerDirective)
					&& tagSpan.Tag.ClassificationType == _PreprocessorKeyword) {
					return Matches(snapshotSpan, "region")
						? null
						: Matches(snapshotSpan, "pragma")
						? new TaggedContentSpan(snapshotSpan.Snapshot, tagSpan.Tag, snapshotSpan.Start, snapshotSpan.Span.Length, 6, snapshotSpan.Span.Length - 6)
						: Matches(snapshotSpan, "if")
						? new TaggedContentSpan(snapshotSpan.Snapshot, tagSpan.Tag, snapshotSpan.Start, snapshotSpan.Span.Length, 2, snapshotSpan.Span.Length - 2)
						: Matches(snapshotSpan, "else")
						? new TaggedContentSpan(snapshotSpan.Snapshot, tagSpan.Tag, snapshotSpan.Start, snapshotSpan.Span.Length, 4, snapshotSpan.Span.Length - 4)
						: null;
				}
				return base.TagComments(snapshotSpan, tagSpan);
			}
			protected override int GetCommentStartIndex(string comment) {
				return comment.Length > 2 && comment[0] == '/' && (comment[1] == '/' || comment[1] == '*')
					? 2
					: -1;
			}
			protected override int GetCommentEndIndex(string comment) {
				return comment[1] == '*' ? comment.Length - 2 : comment.Length;
			}
		}

		sealed class JsCommentTagger : CommentTagger
		{
			public JsCommentTagger(IClassificationTypeRegistryService registry, ITagAggregator<IClassificationTag> aggregator, TaggerResult tags) : base(registry, aggregator, tags) {
			}

			protected override int GetCommentStartIndex(string comment) {
				return 0;
			}
			protected override int GetCommentEndIndex(string comment) {
				return comment.EndsWith("*/", StringComparison.Ordinal) ? comment.Length - 2 : comment.Length;
			}
		}

		sealed class MarkupCommentTagger : CommentTagger
		{
			public MarkupCommentTagger(IClassificationTypeRegistryService registry, ITagAggregator<IClassificationTag> aggregator, TaggerResult tags) : base(registry, aggregator, tags) {
			}

			protected override int GetCommentStartIndex(string comment) {
				return 0;
			}

			protected override int GetCommentEndIndex(string comment) {
				return comment.EndsWith("-->", StringComparison.Ordinal) ? comment.Length - 3 : comment.Length;
			}
		}

		enum CodeType
		{
			None, CSharp, Markup, C, Css, Js
		}
	}
}
