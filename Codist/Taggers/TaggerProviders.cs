using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
	[ContentType(Constants.CodeTypes.HtmlxProjection)]
	[TagType(typeof(IClassificationTag))]
	sealed class CommentTaggerProvider : IViewTaggerProvider
	{
		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
			if (Config.Instance.Features.MatchFlags(Features.SyntaxHighlight) == false
				|| CommentTagger.IsCommentTaggable(buffer) == false
				|| buffer.MayBeEditor() == false) {
				return null;
			}
			var vp = textView.Properties;
			CommentTagger codeTagger;
			var tags = vp.GetOrCreateSingletonProperty(() => new TaggerResult());
			var agg = vp.GetOrCreateSingletonProperty("TagAggregator", () => ServicesHelper.Instance.BufferTagAggregatorFactory.CreateTagAggregator<IClassificationTag>(buffer));
			codeTagger = vp.GetOrCreateSingletonProperty(nameof(CommentTagger), () => CommentTagger.Create(ServicesHelper.Instance.ClassificationTypeRegistry, textView, buffer));
			textView.Closed -= TextViewClosed;
			textView.Closed += TextViewClosed;
			return codeTagger as ITagger<T>;
		}

		void TextViewClosed(object sender, EventArgs args) {
			var textView = sender as ITextView;
			textView.Closed -= TextViewClosed;
			textView.Properties.GetProperty<ITagAggregator<IClassificationTag>>("TagAggregator")?.Dispose();
			if (textView.Properties.TryGetProperty<CommentTagger>(nameof(CommentTagger), out var ct)) {
				ct.Dispose();
				textView.Properties.RemoveProperty(nameof(CommentTagger));
			}
			textView.Properties.RemoveProperty("TagAggregator");
			textView.Properties.RemoveProperty(nameof(CommentTagger));
			textView.Properties.RemoveProperty(typeof(TaggerResult));
		}
	}

	[Export(typeof(ITaggerProvider))]
	[ContentType(Constants.CodeTypes.CSharp)]
	[TagType(typeof(ICodeMemberTag))]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	sealed class CSharpBlockTaggerProvider : ITaggerProvider
	{
		public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
			if (typeof(T) != typeof(ICodeMemberTag) || buffer.MayBeEditor() == false) {
				return null;
			}

			var tagger = buffer.Properties.GetOrCreateSingletonProperty(
				typeof(CSharpBlockTaggerProvider),
				() => new CSharpBlockTagger(buffer)
			);
			return new DisposableTagger<CSharpBlockTagger, ICodeMemberTag>(tagger) as ITagger<T>;
		}
	}

	[Export(typeof(IViewTaggerProvider))]
	[ContentType(Constants.CodeTypes.CSharp)]
	[TagType(typeof(IClassificationTag))]
	sealed class CSharpTaggerProvider : IViewTaggerProvider
	{
		readonly ConditionalWeakTable<ITextBuffer, CSharpTagger> _Taggers = new ConditionalWeakTable<ITextBuffer, CSharpTagger>();
		// for debug info
		int _taggerCount;
		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
			if (typeof(T) == typeof(IClassificationTag)
				&& Config.Instance.Features.MatchFlags(Features.SyntaxHighlight)
				&& buffer.MayBeEditor() // it seems that the analyzer preview windows do not call the View_Close event handler, thus we exclude them here
				) {
				if (_Taggers.TryGetValue(buffer, out var tagger) == false) {
					tagger = new CSharpTagger(this, buffer);
					_Taggers.Add(buffer, tagger);
					++_taggerCount;
				}
				tagger.IncrementReference();
				return tagger as ITagger<T>;
			}
			return null;
		}

		public void DetachTagger(ITextBuffer buffer) {
			if (_Taggers.Remove(buffer)) {
				--_taggerCount;
				Debug.WriteLine(buffer?.GetTextDocument()?.FilePath + " detached tagger");
			}
		}
	}

	[Export(typeof(IClassifierProvider))]
	[ContentType(Constants.CodeTypes.FindResults)]
	sealed class FindResultTaggerProvider : IClassifierProvider
	{
		public IClassifier GetClassifier(ITextBuffer buffer) {
			return Config.Instance.Features.MatchFlags(Features.SyntaxHighlight) == false ? null : new FindResultTagger();
		}
	}

	[Export(typeof(IViewTaggerProvider))]
	[ContentType(Constants.CodeTypes.Code)]
	[TagType(typeof(IClassificationTag))]
	sealed class MarkdownTaggerProvider : IViewTaggerProvider
	{
		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
			// the results produced by the tagger are also reused by the NaviBar and ScrollbarMarker
			if (Config.Instance.Features.HasAnyFlag(Features.SyntaxHighlight | Features.NaviBar | Features.ScrollbarMarkers) == false
				|| textView.TextBuffer.LikeContentType(Constants.CodeTypes.Markdown) == false) {
				return null;
			}
			return textView.Properties.GetOrCreateSingletonProperty(() => new MarkdownTagger(textView, buffer, Config.Instance.Features.MatchFlags(Features.SyntaxHighlight))) as ITagger<T>;
		}
	}

}
