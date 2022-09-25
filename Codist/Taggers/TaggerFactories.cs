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
		readonly ConditionalWeakTable<ITextView, CSharpTagger> _Taggers = new ConditionalWeakTable<ITextView, CSharpTagger>();
		// for debug info
		int _taggerCount;
		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
			return (typeof(T) == typeof(IClassificationTag)) ? CreateTagger(textView, buffer) as ITagger<T> : null;
		}

		CSharpTagger CreateTagger(ITextView textView, ITextBuffer buffer) {
			if (Config.Instance.Features.MatchFlags(Features.SyntaxHighlight)
				&& buffer.MayBeEditor() // it seems that the analyzer preview windows do not call the View_Close event handler, thus we exclude them here
				&& textView.TextBuffer.ContentType.IsOfType("RoslynPreviewContentType") == false
				&& textView.Roles.Contains("PREVIEWTOOLTIPTEXTVIEWROLE") == false
				) {
				if (textView.Roles.Contains("DIFF") && (textView as System.Windows.FrameworkElement)?.Parent != null) {
					// hack workaround for inline DIFF view
					return null;
				}
				if (_Taggers.TryGetValue(textView, out var tagger) == false) {
					_Taggers.Add(textView, tagger = new CSharpTagger(this, textView as IWpfTextView, buffer));
					Debug.WriteLine("Attached tagger " + buffer.GetTextDocument()?.FilePath);
					++_taggerCount;
					textView.Closed += TextView_Closed;
				}
				else if (tagger.TextBuffer != buffer) {
					tagger.Dispose();
					_Taggers.Remove(textView);
					_Taggers.Add(textView, tagger = new CSharpTagger(this, textView as IWpfTextView, buffer));
				}
				return tagger;
			}
			return null;
		}

		void TextView_Closed(object sender, EventArgs e) {
			var view = sender as ITextView;
			view.Closed -= TextView_Closed;
			if (_Taggers.TryGetValue(view, out var tagger)) {
				Debug.WriteLine("Detach tagger " + tagger.TextBuffer?.GetTextDocument()?.FilePath);
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
			return Config.Instance.Features.MatchFlags(Features.SyntaxHighlight) == false ? null : new FindResultTagger();
		}
	}

	[Export(typeof(IViewTaggerProvider))]
	[ContentType(Constants.CodeTypes.Code)]
	[ContentType(Constants.CodeTypes.Markdown)]
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
