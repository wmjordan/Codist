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

namespace Codist.Taggers
{
	abstract class CommentTagger : ITagger<IClassificationTag>, IDisposable
	{
		static ClassificationTag[] __CommentClassifications;
		static readonly Dictionary<IClassificationType, bool> __CommentClasses = new Dictionary<IClassificationType, bool>();
		readonly ITagAggregator<IClassificationTag> _Aggregator;
		readonly TaggerResult _Tags;
#if DEBUG
		readonly HashSet<string> _ClassificationTypes = new HashSet<string>();
#endif
		public event EventHandler TagAdded;
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
			_Aggregator.TagsChanged += AggregatorBatchedTagsChanged;
		}

		internal FrameworkElement Margin { get; set; }

		protected abstract int GetCommentStartIndex(string comment);
		protected abstract int GetCommentEndIndex(string comment);

		public static CommentTagger Create(IClassificationTypeRegistryService registry, ITagAggregator<IClassificationTag> aggregator, TaggerResult tags, ITextBuffer textBuffer) {
			switch (GetCodeType(textBuffer)) {
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

			TaggedContentSpan ts, s = null;
			foreach (var tagSpan in tagSpans) {
#if DEBUG
				var c = tagSpan.Tag.ClassificationType.Classification;
				if (_ClassificationTypes.Add(c)) {
					Debug.WriteLine("Classification type: " + c);
				}
#endif
				ts = TagComments(tagSpan.Span.GetSpans(snapshot)[0], tagSpan);
				if (ts != null) {
					if (s == null) {
						s = ts;
					}
					yield return _Tags.Add(ts);
				}
			}
			if (s != null) {
				TagAdded?.Invoke(this, EventArgs.Empty);
				// note: Don't use the TagsChanged event, otherwise infinite loops will occur
				//TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(s.Span));
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

			var contentEnd = GetCommentEndIndex(text);

			ClassificationTag ctag = null;
			CommentLabel label = null;
			var contentStart = 0;
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
					contentStart = c;
				}
			}

			if (contentStart == 0 || ctag == null) {
				return null;
			}

			// ignore whitespaces in content
			while (contentStart < tl) {
				if (Char.IsWhiteSpace(text[contentStart])) {
					++contentStart;
				}
				else {
					break;
				}
			}
			while (contentEnd > contentStart) {
				if (Char.IsWhiteSpace(text[contentEnd - 1])) {
					--contentEnd;
				}
				else {
					break;
				}
			}

			return label.StyleApplication == CommentStyleApplication.Tag
				? new TaggedContentSpan(snapshotSpan.Snapshot, ctag, snapshotSpan.Start + commentStart, label.LabelLength, contentStart - commentStart, contentEnd - contentStart)
				: label.StyleApplication == CommentStyleApplication.Content
				? new TaggedContentSpan(snapshotSpan.Snapshot, ctag, snapshotSpan.Start + contentStart, contentEnd - contentStart, 0, contentEnd - contentStart)
				: new TaggedContentSpan(snapshotSpan.Snapshot, ctag, snapshotSpan.Start + commentStart, contentEnd - commentStart, contentStart - commentStart, contentEnd - contentStart);
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
			Margin?.InvalidateVisual();
		}

		internal static bool IsCommentTaggable(ITextView view) {
			return GetCodeType(view.TextBuffer) != CodeType.None;
		}
		static CodeType GetCodeType(ITextBuffer textBuffer) {
			var t = textBuffer.ContentType;
			var f = textBuffer.GetTextDocument()?.FilePath;
			if (f != null) {
				f = f.Substring(f.LastIndexOf('.') + 1).ToLowerInvariant();
			}
			var c = t.IsOfType(Constants.CodeTypes.CSharp) ? CodeType.CSharp
				: t.IsOfType("html") || t.IsOfType("htmlx") || t.IsOfType("XAML") || t.IsOfType("XML") ? CodeType.Markup
				: t.IsOfType("code++.css") ? CodeType.Css
				: t.IsOfType("TypeScript") || t.IsOfType("JavaScript") ? CodeType.Js
				: t.IsOfType("C/C++") ? CodeType.C
				: CodeType.None;
			if (c != CodeType.None) {
				return c;
			}
			switch (f) {
				case "js": return CodeType.Js;
				case "c":
				case "cpp":
				case "h":
				case "cxx":
					return CodeType.C;
				case "css":
					return CodeType.Css;
				case "html":
				case "htmlx":
				case "xaml":
				case "xml":
				case "xls":
				case "xlst":
				case "xsd":
				case "config":
					return CodeType.Markup;
			}
			return CodeType.None;
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

		enum CodeType
		{
			None, CSharp, Markup, C, Css, Js
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
					return Matches(snapshotSpan, "pragma") || Matches(snapshotSpan, "if") || Matches(snapshotSpan, "else") /*|| Matches(snapshotSpan, "region")*/
						? new TaggedContentSpan(snapshotSpan.Snapshot, tagSpan.Tag, snapshotSpan.Start, snapshotSpan.Length, 0, 0)
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
	}


	[Export(typeof(IViewTaggerProvider))]
	[ContentType(Constants.CodeTypes.Code)]
	[TagType(typeof(IClassificationTag))]
	sealed class CommentTaggerProvider : IViewTaggerProvider
	{
		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
			if (Config.Instance.Features.MatchFlags(Features.SyntaxHighlight) == false) {
				return null;
			}
			if (CommentTagger.IsCommentTaggable(textView) == false) {
				return null;
			}
			var vp = textView.Properties;
			var tagger = vp.GetOrCreateSingletonProperty(() => ServicesHelper.Instance.BufferTagAggregatorFactory.CreateTagAggregator<IClassificationTag>(buffer));
			var tags = vp.GetOrCreateSingletonProperty(() => new TaggerResult());
			var codeTagger = vp.GetOrCreateSingletonProperty(nameof(CommentTaggerProvider), () => CommentTagger.Create(ServicesHelper.Instance.ClassificationTypeRegistry, tagger, tags, textView.TextBuffer));
			textView.Closed -= TextViewClosed;
			textView.Closed += TextViewClosed;
			return codeTagger as ITagger<T>;
		}

		void TextViewClosed(object sender, EventArgs args) {
			var textView = sender as ITextView;
			textView.Closed -= TextViewClosed;
			textView.Properties.GetProperty<ITagAggregator<IClassificationTag>>(typeof(ITagAggregator<IClassificationTag>))?.Dispose();
			textView.Properties.GetProperty<CommentTagger>(nameof(CommentTaggerProvider))?.Dispose();
		}
	}
}
