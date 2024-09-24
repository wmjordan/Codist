using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
		static Dictionary<IClassificationType, bool> __CommentClasses = new Dictionary<IClassificationType, bool>(ClassificationTypeComparer.Instance);
		static readonly Dictionary<string, CodeType> __CodeTypeExtensions = InitCodeTypeExtensions();

		static Dictionary<string, CodeType> InitCodeTypeExtensions() {
			return new Dictionary<string, CodeType>(StringComparer.OrdinalIgnoreCase) {
				{ "js", CodeType.Js },
				{ "c", CodeType.C },
				{ "cc", CodeType.C },
				{ "cpp", CodeType.C },
				{ "hpp", CodeType.C },
				{ "h", CodeType.C },
				{ "cxx", CodeType.C },
				{ "css", CodeType.Css },
				{ "less", CodeType.AlternativeC },
				{ "scss", CodeType.AlternativeC },
				{ "json", CodeType.Common },
				{ "cshtml", CodeType.AlternativeC },
				{ "razor", CodeType.AlternativeC },
				{ "go", CodeType.Go },
				{ "html", CodeType.Markup },
				{ "xhtml", CodeType.Markup },
				{ "xaml", CodeType.Markup },
				{ "xml", CodeType.Markup },
				{ "xsl", CodeType.Markup },
				{ "xslt", CodeType.Markup },
				{ "xsd", CodeType.Markup },
				{ "sql", CodeType.Sql },
				{ "py", CodeType.Python },
				{ "sh", CodeType.BashShell },
				{ "ps1", CodeType.BashShell },
				{ "cmd", CodeType.Batch },
				{ "bat", CodeType.Batch },
				{ "ini", CodeType.Common },
			};
		}

		readonly bool _FullParseAtFirstLoad;
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
				var t = typeof(SyntaxHighlight.CommentStyleTypes);
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

			_FullParseAtFirstLoad = textView.Roles.Contains(PredefinedTextViewRoles.PreviewTextView) == false
				&& textView.Roles.Contains(PredefinedTextViewRoles.Document);
			_Buffer = buffer;
			_TextView = textView;
			if (buffer is ITextBuffer2 b) {
				b.ChangedOnBackground += TextBuffer_Changed;
			}
			else {
				buffer.ChangedLowPriority += TextBuffer_Changed;
			}
			buffer.ContentTypeChanged += TextBuffer_ContentTypeChanged;
			_Tags = textView.Properties.GetProperty<TaggerResult>(typeof(TaggerResult));
		}

		internal FrameworkElement Margin { get; set; }

		protected abstract int GetCommentStartIndex(SnapshotSpan content);
		protected abstract int GetCommentEndIndex(SnapshotSpan content);

		public static CommentTagger Create(IClassificationTypeRegistryService registry, ITextView textView, ITextBuffer textBuffer) {
			switch (GetCodeType(textBuffer)) {
				case CodeType.CSharp:
					return new CSharpCommentTagger(registry, textView, textBuffer);
				case CodeType.C:
				case CodeType.Go:
				case CodeType.Rust:
					return new CCommentTagger(registry, textView, textBuffer);
				case CodeType.Css:
				case CodeType.Js:
				case CodeType.Sql:
					return new SlashStarCommentTagger(registry, textView, textBuffer);
				case CodeType.Batch:
					return new BatchFileCommentTagger(registry, textView, textBuffer);
				case CodeType.Markup:
					return new MarkupCommentTagger(registry, textView, textBuffer);
				case CodeType.Python:
				case CodeType.BashShell:
				case CodeType.Common:
					return new CommonCommentTagger(registry, textView, textBuffer);
				case CodeType.AlternativeC:
					return new AlternativeCCommentTagger(registry, textView, textBuffer);
			}
			return null;
		}

		public IEnumerable<ITagSpan<IClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
			if (spans.Count == 0
				|| Config.Instance.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.SpecialComment) == false
				|| _Tags is null) {
				return Enumerable.Empty<ITagSpan<IClassificationTag>>();
			}
			var snapshot = spans[0].Snapshot;
			IEnumerable<IMappingTagSpan<IClassificationTag>> tagSpans;
			try {
				if (_Tags.LastParsed == 0 && _FullParseAtFirstLoad) {
					// perform a full parse at the first time
					"Full parse".Log();
					tagSpans = GetTagAggregator().GetTags(snapshot.ToSnapshotSpan());
					_Tags.LastParsed = snapshot.Length;
				}
				else {
					//var start = spans[0].Start;
					//var end = spans[spans.Count - 1].End;
					//Debug.WriteLine($"Get tag [{start.Position}..{end.Position})");

					tagSpans = GetTagAggregator().GetTags(spans);
				}
			}
			catch (ObjectDisposedException ex) {
				// HACK: TagAggregator could be disposed during editing, to be investigated further
				(ex.ObjectName + " is disposed").Log();
				return Enumerable.Empty<ITagSpan<IClassificationTag>>();
			}

			return GetTags(tagSpans, snapshot);
		}

		IEnumerable<ITagSpan<IClassificationTag>> GetTags(IEnumerable<IMappingTagSpan<IClassificationTag>> tagSpans, ITextSnapshot snapshot) {
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
				//   since we take advantages of ITagAggregator<IClassificationTag>
				//TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(s.Span));
			}
		}

		protected virtual TaggedContentSpan TagComments(SnapshotSpan snapshotSpan, IMappingTagSpan<IClassificationTag> tagSpan) {
			// find spans that the language service has already classified as comments ...
			if (IsComment(tagSpan.Tag.ClassificationType) == false) {
				return null;
			}
			_Tags.ClearRange(snapshotSpan.Start, snapshotSpan.Length);
			//NOTE: markup comment span does not include comment start token
			var endOfCommentStartToken = GetCommentStartIndex(snapshotSpan);
			if (endOfCommentStartToken < 0) {
				return null;
			}
			var spanLength = snapshotSpan.Length;
			var commentStart = endOfCommentStartToken;
			while (commentStart < spanLength) {
				if (Char.IsWhiteSpace(snapshotSpan.CharAt(commentStart))) {
					++commentStart;
				}
				else {
					break;
				}
			}

			var contentEnd = GetCommentEndIndex(snapshotSpan);

			ClassificationTag tag = null;
			CommentLabel label = null;
			var contentStart = 0;
			foreach (var item in Config.Instance.Labels) {
				var c = commentStart + item.LabelLength;
				if (c >= spanLength
					|| snapshotSpan.HasTextAtOffset(item.Label, item.IgnoreCase, commentStart) == false) {
					continue;
				}

				var followingChar = snapshotSpan.CharAt(c);
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
			while (contentStart < spanLength) {
				if (Char.IsWhiteSpace(snapshotSpan.CharAt(contentStart)) == false) {
					break;
				}
				++contentStart;
			}
			while (contentEnd > contentStart) {
				if (Char.IsWhiteSpace(snapshotSpan.CharAt(contentEnd - 1)) == false) {
					break;
				}
				--contentEnd;
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
			if (__CommentClasses.TryGetValue(classification, out var c)) {
				return c;
			}
			// since this is a rare case, we don't use concurrent dictionary,
			//   but use collection cloning and atomic replacement instead
			//   to save some CPU resource for the above normal path
			var d = new Dictionary<IClassificationType, bool>(__CommentClasses, ClassificationTypeComparer.Instance);
			c = d[classification] = classification.Classification.IndexOf("Comment", StringComparison.OrdinalIgnoreCase) != -1;
			__CommentClasses = d;
			return c;
		}

		ITagAggregator<IClassificationTag> GetTagAggregator() {
			if (_Aggregator != null) {
				return _Aggregator;
			}
			var a = ServicesHelper.Instance.ViewTagAggregatorFactory.CreateTagAggregator<IClassificationTag>(_TextView);
			a.BatchedTagsChanged += AggregatorBatchedTagsChanged;
			return _Aggregator = a;
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
				: t.IsOfType("C/C++") ? CodeType.C
				: t.IsOfType("TypeScript") || t.IsOfType("JavaScript") ? CodeType.Js
				: t.IsOfType("code++.MagicPython") ? CodeType.Python
				: t.IsOfType("html") || t.IsOfType("htmlx") || t.IsOfType("XAML") || t.IsOfType("XML") || t.IsOfType(Constants.CodeTypes.HtmlxProjection) ? CodeType.Markup
				: t.IsOfType("Razor") ? CodeType.AlternativeC
				: t.IsOfType("code++.css") ? CodeType.Css
				: t.IsOfType("code++.LESS") ? CodeType.Common
				: t.IsOfType("css.extensions") ? CodeType.AlternativeC
				: t.IsOfType("code++.JSON (Javascript Next)") ? CodeType.Common
				: t.IsOfType("code++.Shell Script (Bash)") || t.IsOfType("InBoxPowerShell") ? CodeType.BashShell
				: t.IsOfType("code++.Batch File") ? CodeType.Batch
				: t.IsOfType("code++.Ini") ? CodeType.Common
				: t.IsOfType("code++.Go") ? CodeType.Go
				: t.IsOfType("code++.Rust") ? CodeType.Rust
				: t.IsOfType("code++.NAnt Build File") ? CodeType.Markup
				: CodeType.None;
			if (c != CodeType.None) {
				return c;
			}
			var f = textBuffer.GetTextDocument()?.FilePath;
			return f != null && __CodeTypeExtensions.TryGetValue(f.Substring(f.LastIndexOf('.') + 1), out var type)
				? type
				: CodeType.None;
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
				if (_Aggregator != null) {
					_Aggregator.BatchedTagsChanged -= AggregatorBatchedTagsChanged;
					_Aggregator = null;
				}
				_Tags.Reset();
				_Tags = null;
				if (_Buffer is ITextBuffer2 b) {
					b.ChangedOnBackground -= TextBuffer_Changed;
				}
				else {
					_Buffer.ChangedLowPriority -= TextBuffer_Changed;
				}
				_Buffer.ContentTypeChanged -= TextBuffer_ContentTypeChanged;
				_Buffer = null;
				_TextView.RemoveProperty<CommentTagger>();
				_TextView = null;
				Margin = null;
			}
		}
		#endregion

		enum CodeType
		{
			None, Common, CSharp, Markup, C, AlternativeC, Css, Go, Rust, Js, Sql, Python, Batch, BashShell
		}

		sealed class CommonCommentTagger : CommentTagger
		{
			public CommonCommentTagger(IClassificationTypeRegistryService registry, ITextView textView, ITextBuffer buffer) : base(registry, textView, buffer) {
			}

			protected override int GetCommentStartIndex(SnapshotSpan content) {
				return 0;
			}

			protected override int GetCommentEndIndex(SnapshotSpan content) {
				return content.Length;
			}
		}

		sealed class SlashStarCommentTagger : CommentTagger
		{
			public SlashStarCommentTagger(IClassificationTypeRegistryService registry, ITextView textView, ITextBuffer buffer) : base(registry, textView, buffer) {
			}

			protected override int GetCommentStartIndex(SnapshotSpan content) {
				return GetStartIndexOfMultilineSlashStartComment(content, 0);
			}
			protected override int GetCommentEndIndex(SnapshotSpan content) {
				return GetEndIndexOfMultilineSlashStartComment(content);
			}

			internal static int GetStartIndexOfMultilineSlashStartComment(SnapshotSpan content, int defaultStartIndex = 0) {
				if (content.Length >= 2 && content.CharAt(0) == '/' && content.CharAt(1) == '*') {
					var i = 2;
					var l = content.Length;
					char c;
					if (content.CharAt(l - 1) == '/' && content.CharAt(l - 2) == '*') {
						l -= 2;
					}
					while (i < l) {
						if (Char.IsWhiteSpace(c = content.CharAt(i)) || c == '*') {
							i++;
							continue;
						}
						break;
					}
					return i;
				}
				return defaultStartIndex;
			}
			static int GetEndIndexOfMultilineSlashStartComment(SnapshotSpan content) {
				int l = content.Length;
				return l >= 2 && content.CharAt(l - 1) == '/' && content.CharAt(l - 2) == '*' ? l - 2 : l;
			}
		}

		sealed class CCommentTagger : CommentTagger
		{
			public CCommentTagger(IClassificationTypeRegistryService registry, ITextView textView, ITextBuffer buffer) : base(registry, textView, buffer) {
			}

			protected override int GetCommentStartIndex(SnapshotSpan content) {
				return content.Length > 2 && content.CharAt(1) == '/' ? 2
					: SlashStarCommentTagger.GetStartIndexOfMultilineSlashStartComment(content, -1);
			}
			protected override int GetCommentEndIndex(SnapshotSpan content) {
				return content.CharAt(1) == '*' ? content.Length - 2 : content.Length;
			}
		}

		sealed class AlternativeCCommentTagger : CommentTagger
		{
			public AlternativeCCommentTagger(IClassificationTypeRegistryService registry, ITextView textView, ITextBuffer buffer) : base(registry, textView, buffer) {
			}

			protected override int GetCommentStartIndex(SnapshotSpan content) {
				return content.Length > 2 && content.CharAt(1) == '/' ? 2
					: SlashStarCommentTagger.GetStartIndexOfMultilineSlashStartComment(content, 0);
			}
			protected override int GetCommentEndIndex(SnapshotSpan content) {
				return content.CharAt(1) == '*' ? content.Length - 2 : content.Length;
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
			protected override int GetCommentStartIndex(SnapshotSpan content) {
				return content.Length > 2
					? content.CharAt(1) == '/'
						? 2
						: SlashStarCommentTagger.GetStartIndexOfMultilineSlashStartComment(content, -1)
					: -1;
			}
			protected override int GetCommentEndIndex(SnapshotSpan content) {
				return content.CharAt(1) == '*' ? content.Length - 2 : content.Length;
			}
		}

		sealed class MarkupCommentTagger : CommentTagger
		{
			public MarkupCommentTagger(IClassificationTypeRegistryService registry, ITextView textView, ITextBuffer buffer) : base(registry, textView, buffer) {
			}

			protected override int GetCommentStartIndex(SnapshotSpan content) {
				return 0;
			}

			protected override int GetCommentEndIndex(SnapshotSpan content) {
				return content.EndsWith("-->") ? content.Length - 3 : content.Length;
			}
		}

		sealed class BatchFileCommentTagger : CommentTagger
		{
			public BatchFileCommentTagger(IClassificationTypeRegistryService registry, ITextView textView, ITextBuffer buffer) : base(registry, textView, buffer) {
			}

			protected override int GetCommentStartIndex(SnapshotSpan content) {
				switch (content.CharAt(0)) {
					case ':':
						return content.CharAt(1) == ':' ? 2 : 0;
					case 'r':
					case 'R':
						return content.CharAt(1).CeqAny('e', 'E') && content.CharAt(2).CeqAny('m', 'M') ? 3 : 0;
				}
				return 0;
			}

			protected override int GetCommentEndIndex(SnapshotSpan content) {
				return content.Length;
			}
		}

		sealed class ClassificationTypeComparer : IEqualityComparer<IClassificationType>
		{
			internal static readonly ClassificationTypeComparer Instance = new ClassificationTypeComparer();

			public bool Equals(IClassificationType x, IClassificationType y) {
				return ReferenceEquals(x, y) || x?.Classification == y?.Classification;
			}

			public int GetHashCode(IClassificationType t) {
				return t?.Classification.GetHashCode() ?? 0;
			}
		}
	}
}
