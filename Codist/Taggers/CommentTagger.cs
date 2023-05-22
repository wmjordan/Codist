using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using CLR;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
{
	abstract class CommentTagger : ITagger<IClassificationTag>, IDisposable
	{
		static ClassificationTag[] __CommentClassifications;
		static readonly Dictionary<IClassificationType, bool> __CommentClasses = new Dictionary<IClassificationType, bool>();
		ITagAggregator<IClassificationTag> _Aggregator;
		TaggerResult _Tags;
		ITextView _TextView;
		ITextBuffer _Buffer;

#if DEBUG
		readonly HashSet<string> _ClassificationTypes = new HashSet<string>();
#endif
		public event EventHandler TagAdded;
#pragma warning disable 67
		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
#pragma warning restore 67

		protected CommentTagger(IClassificationTypeRegistryService registry, ITextView textView, ITextBuffer buffer) {
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

			_Buffer = buffer;
			_TextView = textView;
			buffer.Changed += TextBuffer_Changed;
			buffer.ContentTypeChanged += TextBuffer_ContentTypeChanged;
			_Tags = textView.Properties.GetProperty<TaggerResult>(typeof(TaggerResult));
			_Aggregator = textView.Properties.GetProperty<ITagAggregator<IClassificationTag>>("TagAggregator");
			_Aggregator.BatchedTagsChanged += AggregatorBatchedTagsChanged;
		}

		internal FrameworkElement Margin { get; set; }

		protected abstract int GetCommentStartIndex(string comment);
		protected abstract int GetCommentEndIndex(string comment);

		public static CommentTagger Create(IClassificationTypeRegistryService registry, ITextView textView, ITextBuffer textBuffer) {
			switch (GetCodeType(textBuffer)) {
				case CodeType.CSharp: return new CSharpCommentTagger(registry, textView, textBuffer);
				case CodeType.Markup: return new MarkupCommentTagger(registry, textView, textBuffer);
				case CodeType.C: return new CCommentTagger(registry, textView, textBuffer);
				case CodeType.Css: return new CssCommentTagger(registry, textView, textBuffer);
				case CodeType.Js: return new JsCommentTagger(registry, textView, textBuffer);
			}
			return null;
		}

		public IEnumerable<ITagSpan<IClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
			if (spans.Count == 0
			|| Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.SpecialComment) == false
			|| _Tags is null) {
				yield break;
			}

			var snapshot = spans[0].Snapshot;
			IEnumerable<IMappingTagSpan<IClassificationTag>> tagSpans;
			try {
				if (_Tags.LastParsed == 0) {
					// perform a full parse at the first time
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
				var ss = tagSpan.Span.GetSpans(snapshot);
				if (ss.Count == 0) {
					continue;
				}
				ts = TagComments(ss[0], tagSpan);
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
			_Tags.ClearRange(snapshotSpan.Start, snapshotSpan.Length);
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

			ClassificationTag tag = null;
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
					tag = __CommentClassifications[(int)item.StyleID];
					label = item;
					contentStart = c;
				}
			}

			if (contentStart == 0 || tag == null) {
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

			switch (label.StyleApplication) {
				case CommentStyleApplication.Tag:
					return new TaggedContentSpan(tag, snapshotSpan.Snapshot, snapshotSpan.Start.Position + commentStart, label.LabelLength, contentStart - commentStart, contentEnd - contentStart);
				case CommentStyleApplication.Content:
					return new TaggedContentSpan(tag, snapshotSpan.Snapshot, snapshotSpan.Start.Position + contentStart, contentEnd - contentStart, 0, contentEnd - contentStart);
				default:
					return new TaggedContentSpan(tag, snapshotSpan.Snapshot, snapshotSpan.Start.Position + commentStart, contentEnd - commentStart, contentStart - commentStart, contentEnd - contentStart);
			}
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

		void AggregatorBatchedTagsChanged(object sender, BatchedTagsChangedEventArgs args) {
			Margin?.InvalidateVisual();
		}

		internal static bool IsCommentTaggable(ITextBuffer buffer) {
			return GetCodeType(buffer) != CodeType.None;
		}
		static CodeType GetCodeType(ITextBuffer textBuffer) {
			var t = textBuffer.ContentType;
			var c = t.IsOfType(Constants.CodeTypes.CSharp) || t.IsOfType("HTMLXProjection") ? CodeType.CSharp
				: t.IsOfType("html") || t.IsOfType("htmlx") || t.IsOfType("XAML") || t.IsOfType("XML") || t.IsOfType(Constants.CodeTypes.HtmlxProjection) ? CodeType.Markup
				: t.IsOfType("code++.css") ? CodeType.Css
				: t.IsOfType("TypeScript") || t.IsOfType("JavaScript") ? CodeType.Js
				: t.IsOfType("C/C++") ? CodeType.C
				: CodeType.None;
			if (c != CodeType.None) {
				return c;
			}
			var f = textBuffer.GetTextDocument()?.FilePath;
			if (f == null) {
				return CodeType.None;
			}
			switch (f.Substring(f.LastIndexOf('.') + 1).ToLowerInvariant()) {
				case "js": return CodeType.Js;
				case "c":
				case "cpp":
				case "h":
				case "cxx":
					return CodeType.C;
				case "css":
					return CodeType.Css;
				case "cshtml":
					return CodeType.CSharp;
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

		void TextBuffer_Changed(object sender, TextContentChangedEventArgs args) {
			if (args.Changes.Count == 0) {
				return;
			}
			_Tags.PurgeOutdatedTags(args);
		}

		void TextBuffer_ContentTypeChanged(object sender, ContentTypeChangedEventArgs e) {
			if (GetCodeType(e.After.TextBuffer) != GetCodeType(e.Before.TextBuffer)) {
				Dispose();
			}
		}

		#region IDisposable Support
		public void Dispose() {
			if (_Tags != null) {
				_Aggregator.BatchedTagsChanged -= AggregatorBatchedTagsChanged;
				_Aggregator = null;
				_Tags.Reset();
				_Tags = null;
				_Buffer.Changed -= TextBuffer_Changed;
				_Buffer.ContentTypeChanged -= TextBuffer_ContentTypeChanged;
				_Buffer = null;
				_TextView.Properties.RemoveProperty(nameof(CommentTagger));
				_TextView = null;
				Margin = null;
			}
		}
		#endregion

		enum CodeType
		{
			None, CSharp, Markup, C, Css, Js
		}

		sealed class CCommentTagger : CommentTagger
		{
			public CCommentTagger(IClassificationTypeRegistryService registry, ITextView textView, ITextBuffer buffer) : base(registry, textView, buffer) {
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
			public CssCommentTagger(IClassificationTypeRegistryService registry, ITextView textView, ITextBuffer buffer) : base(registry, textView, buffer) {
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
			readonly ClassificationTag _PreprocessorKeywordTag;

			public CSharpCommentTagger(IClassificationTypeRegistryService registry, ITextView textView, ITextBuffer buffer) : base(registry, textView, buffer) {
				_PreprocessorKeywordTag = new ClassificationTag(registry.GetClassificationType(Constants.CodePreprocessorKeyword));
			}
			protected override TaggedContentSpan TagComments(SnapshotSpan snapshotSpan, IMappingTagSpan<IClassificationTag> tagSpan) {
				if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.CompilerDirective)
					&& tagSpan.Tag.ClassificationType.Classification == Constants.CodePreprocessorKeyword) {
					return Matches(snapshotSpan, "pragma") || Matches(snapshotSpan, "if") || Matches(snapshotSpan, "else")
						? new TaggedContentSpan(_PreprocessorKeywordTag, snapshotSpan, 0, 0)
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
			public JsCommentTagger(IClassificationTypeRegistryService registry, ITextView textView, ITextBuffer buffer) : base(registry, textView, buffer) {
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
			public MarkupCommentTagger(IClassificationTypeRegistryService registry, ITextView textView, ITextBuffer buffer) : base(registry, textView, buffer) {
			}

			protected override int GetCommentStartIndex(string comment) {
				return 0;
			}

			protected override int GetCommentEndIndex(string comment) {
				return comment.EndsWith("-->", StringComparison.Ordinal) ? comment.Length - 3 : comment.Length;
			}
		}
	}
}
