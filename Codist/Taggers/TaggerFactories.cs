using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
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
				|| buffer.MayBeEditor() == false && textView.TextBuffer.ContentType.IsOfType("RoslynPreviewContentType") == false
				) {
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
		static readonly string[] __TaggableRoles = new[] { PredefinedTextViewRoles.Document, PredefinedTextViewRoles.EmbeddedPeekTextView };

		readonly Dictionary<ITextView, CSharpTagger> _Taggers = new Dictionary<ITextView, CSharpTagger>();

		// note: cache the latest used tagger to improve performance
		//   In C# code editor, even displaying the Quick Info will call the CreateTagger method,
		//   thus we cache the last accessed tagger, identified by ITextView and ITextBuffer,
		//   in CSharpTaggerProvider and CSharpTagger respectively, to avoid dictionary lookup
		CSharpTagger _LastTagger;

		// for debug info
		int _taggerCount;

		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
			return (typeof(T) == typeof(IClassificationTag)) ? GetTagger(textView, buffer) as ITagger<T> : null;
		}

		ITagger<IClassificationTag> GetTagger(ITextView textView, ITextBuffer buffer) {
			if (Config.Instance.Features.MatchFlags(Features.SyntaxHighlight) == false
				|| buffer.MayBeEditor() == false // it seems that the analyzer preview windows do not call the View_Close event handler, thus we exclude them here
				|| textView.TextBuffer.ContentType.IsOfType("RoslynPreviewContentType")
				|| textView.Roles.ContainsAny(__TaggableRoles) == false && textView.TextBuffer.ContentType.TypeName != Constants.CodeTypes.InteractiveContent
				|| textView.Roles.Contains("PREVIEWTOOLTIPTEXTVIEWROLE")
				|| textView.Roles.Contains("STICKYSCROLL_TEXT_VIEW") // disables advanced highlight for it does not work well if any style contains font size definition
				) {
				return null;
			}
			CSharpTagger tagger;
			if (textView == _LastTagger?.View) {
				tagger = _LastTagger;
			}
			else if (_Taggers.TryGetValue(textView, out tagger)) {
				_LastTagger = tagger;
			}
			else {
				_Taggers.Add(textView, _LastTagger = tagger = new CSharpTagger(this, textView as IWpfTextView));
				++_taggerCount;
				textView.Closed += TextView_Closed;
			}
			return tagger.GetTagger(buffer);
		}

		void TextView_Closed(object sender, EventArgs e) {
			var view = sender as ITextView;
			view.Closed -= TextView_Closed;
			if (_LastTagger?.View == view) {
				_LastTagger = null;
			}
			if (_Taggers.TryGetValue(view, out var tagger)) {
				tagger.Dispose();
				_Taggers.Remove(view);
				--_taggerCount;
			}
		}
	}

	[Export(typeof(IClassifierProvider))]
	[ContentType(Constants.CodeTypes.FindResults)]
	sealed class FindResultTaggerProvider : IClassifierProvider
	{
		public IClassifier GetClassifier(ITextBuffer buffer) {
			return Config.Instance.Features.MatchFlags(Features.SyntaxHighlight)
				? new FindResultTagger()
				: null;
		}
	}

	[Export(typeof(IViewTaggerProvider))]
	[ContentType(Constants.CodeTypes.Code)]
	[ContentType(Constants.CodeTypes.Markdown)]
	[ContentType(Constants.CodeTypes.VsMarkdown)]
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
