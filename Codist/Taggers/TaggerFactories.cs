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

	[Export(typeof(IViewTaggerProvider))]
	[ContentType(Constants.CodeTypes.CSharp)]
	[TagType(typeof(IClassificationTag))]
	sealed class CSharpTaggerProvider : IViewTaggerProvider
	{
		static readonly string[] __TaggableRoles = new[] { PredefinedTextViewRoles.Document, PredefinedTextViewRoles.EmbeddedPeekTextView };

		// note: we could have used WeakDictionary to hold our references,
		//   and release references when the view is finalized
		//   unfortunately memory leak in VS sometimes prevents IWpfTextView from being released properly,
		//   thus the WeakDictionary can't be used
		readonly Dictionary<ITextView, Dictionary<ITextBuffer, CSharpTagger>> _Taggers = new Dictionary<ITextView, Dictionary<ITextBuffer, CSharpTagger>>();

		// note: cache the latest used tagger to improve performance
		//   In C# code editor, even displaying the Quick Info will call the CreateTagger method,
		//   thus we cache the last accessed tagger, identified by ITextView and ITextBuffer,
		//   to avoid quite a few dictionary lookup operations
		ITextView _LastView;
		ITextBuffer _LastTextBuffer;
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
			if (_LastView == textView && _LastTextBuffer == buffer) {
				return _LastTagger;
			}

			if (_Taggers.TryGetValue(textView, out var bufferTaggers)) {
				if (bufferTaggers.TryGetValue(buffer, out var tagger)) {
					return tagger;
				}
				bufferTaggers.Add(_LastTextBuffer = buffer, _LastTagger = CreateTagger());
				return _LastTagger;
			}
			_Taggers.Add(_LastView = textView, new Dictionary<ITextBuffer, CSharpTagger>() {
					{ _LastTextBuffer = buffer, _LastTagger = CreateTagger() }
				});
			textView.Closed += TextView_Closed;
			return _LastTagger;
		}

		void TextView_Closed(object sender, EventArgs e) {
			var view = sender as ITextView;
			view.Closed -= TextView_Closed;
			if (_LastView == view) {
				_LastTextBuffer = null;
				_LastTagger = null;
			}
			if (_Taggers.TryGetValue(view, out var viewTaggers)) {
				foreach (var item in viewTaggers) {
					item.Value.Dispose();
					--_taggerCount;
				}
				_Taggers.Remove(view);
			}
		}

		CSharpTagger CreateTagger() {
			++_taggerCount;
			return new CSharpTagger(CSharpParser.GetOrCreate(_LastView as IWpfTextView), _LastTextBuffer);
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
