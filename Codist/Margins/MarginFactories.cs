using System.ComponentModel.Composition;
using AppHelpers;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Margins
{
	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(nameof(CommentMargin))]
	[Order(After = PredefinedMarginNames.OverviewChangeTracking, Before = PredefinedMarginNames.OverviewMark)]
	[MarginContainer(PredefinedMarginNames.VerticalScrollBar)]
	[ContentType(Constants.CodeTypes.Code)]
	[ContentType(Constants.CodeTypes.Projection)]
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	sealed class CommentMarginFactory : IWpfTextViewMarginProvider
	{
		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
			var textView = wpfTextViewHost.TextView;
			return Config.Instance.Features.MatchFlags(Features.ScrollbarMarkers)
				&& marginContainer is IVerticalScrollBar scrollBar
				&& Taggers.CommentTagger.IsCommentTaggable(textView.TextBuffer)
				&& textView.TextBuffer.MayBeEditor()
				? new CommentMargin(textView, scrollBar)
				: null;
		}
	}

	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name("MarkdownMargin")]
	[Order(After = PredefinedMarginNames.OverviewChangeTracking, Before = nameof(CommentMargin))]
	[MarginContainer(PredefinedMarginNames.VerticalScrollBar)]
	[ContentType(Constants.CodeTypes.Code)]
	[ContentType(Constants.CodeTypes.Markdown)]
	[ContentType(Constants.CodeTypes.VsMarkdown)]
	[TextViewRole(PredefinedTextViewRoles.EmbeddedPeekTextView)]
	[TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
	sealed class MarkdownMarginFactory : IWpfTextViewMarginProvider
	{
		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
			var textView = wpfTextViewHost.TextView;
			return Config.Instance.Features.MatchFlags(Features.ScrollbarMarkers)
				&& marginContainer is IVerticalScrollBar scrollBar
				&& textView.TextBuffer.LikeContentType(Constants.CodeTypes.Markdown)
				&& textView.TextBuffer.MayBeEditor()
				? new CommentMargin(textView, scrollBar)
				: null;
		}
	}

	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(nameof(CSharpMembersMargin))]
	[Order(After = PredefinedMarginNames.OverviewChangeTracking, Before = nameof(CommentMargin))]
	[MarginContainer(PredefinedMarginNames.VerticalScrollBar)]
	[ContentType(Constants.CodeTypes.CSharp)]
	[TextViewRole(PredefinedTextViewRoles.EmbeddedPeekTextView)]
	[TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
	sealed class CSharpMembersMarginFactory : IWpfTextViewMarginProvider
	{
		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
			return Config.Instance.Features.MatchFlags(Features.ScrollbarMarkers)
				&& marginContainer is IVerticalScrollBar scrollBar
				&& wpfTextViewHost.TextView.TextBuffer.MayBeEditor()
				? new CSharpMembersMargin(wpfTextViewHost.TextView, scrollBar)
				: null;
		}
	}

	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(nameof(DisableChangeTrackerMargin))]
	[MarginContainer(PredefinedMarginNames.LeftSelection)]
	[ContentType(Constants.CodeTypes.Text)]
	[TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
	sealed class DisableChangeTrackerMarginFactory : IWpfTextViewMarginProvider
	{
		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
			return CodistPackage.VsVersion.Major >= 17
				&& wpfTextViewHost.TextView.TextBuffer.MayBeEditor()
				? new DisableChangeTrackerMargin(marginContainer)
				: null;
		}
	}

	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(nameof(LineNumberMargin))]
	[Order(Before = PredefinedMarginNames.OverviewChangeTracking)]
	[MarginContainer(PredefinedMarginNames.VerticalScrollBar)]
	[ContentType(Constants.CodeTypes.Code)]
	[ContentType(Constants.CodeTypes.Markdown)]
	[ContentType(Constants.CodeTypes.VsMarkdown)]
	[ContentType(Constants.CodeTypes.Output)]
	[ContentType(Constants.CodeTypes.InteractiveContent)]
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	sealed class LineNumberMarginFactory : IWpfTextViewMarginProvider
	{
		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
			return Config.Instance.Features.MatchFlags(Features.ScrollbarMarkers)
				&& marginContainer is IVerticalScrollBar scrollBar
				&& wpfTextViewHost.TextView.TextBuffer.MayBeEditor()
				? new LineNumberMargin(wpfTextViewHost.TextView, scrollBar)
				: null;
		}
	}

	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(nameof(SelectionMargin))]
	[Order(Before = PredefinedMarginNames.OverviewChangeTracking)]
	[MarginContainer(PredefinedMarginNames.VerticalScrollBar)]
	[ContentType(Constants.CodeTypes.Code)]
	[ContentType(Constants.CodeTypes.Markdown)]
	[ContentType(Constants.CodeTypes.VsMarkdown)]
	[ContentType(Constants.CodeTypes.Output)]
	[ContentType(Constants.CodeTypes.InteractiveContent)]
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	sealed class SelectionMarginFactory : IWpfTextViewMarginProvider
	{
		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
			return Config.Instance.Features.MatchFlags(Features.ScrollbarMarkers)
				&& marginContainer is IVerticalScrollBar scrollBar
				&& wpfTextViewHost.TextView.TextBuffer.MayBeEditor()
				? new SelectionMargin(wpfTextViewHost.TextView, scrollBar)
				: null;
		}
	}
}
