using System.ComponentModel.Composition;
using AppHelpers;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Margins
{
	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(CommentMargin.MarginName)]
	[Order(After = PredefinedMarginNames.OverviewChangeTracking, Before = PredefinedMarginNames.OverviewMark)]
	[MarginContainer(PredefinedMarginNames.VerticalScrollBar)]
	[ContentType(Constants.CodeTypes.Code)]
	[ContentType("projection")]
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	sealed class CommentMarginFactory : IWpfTextViewMarginProvider
	{
		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
			var scrollBarContainer = marginContainer as IVerticalScrollBar;
			var textView = wpfTextViewHost.TextView;
			return Config.Instance.Features.MatchFlags(Features.ScrollbarMarkers)
				&& scrollBarContainer != null
				&& Taggers.CommentTagger.IsCommentTaggable(textView.TextBuffer)
				&& textView.TextBuffer.MayBeEditor()
				? new CommentMargin(textView, scrollBarContainer)
				: null;
		}
	}

	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name("MarkdownMargin")]
	[Order(After = PredefinedMarginNames.OverviewChangeTracking, Before = CommentMargin.MarginName)]
	[MarginContainer(PredefinedMarginNames.VerticalScrollBar)]
	[ContentType(Constants.CodeTypes.Code)]
	[TextViewRole(PredefinedTextViewRoles.EmbeddedPeekTextView)]
	[TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
	sealed class MarkdownMarginFactory : IWpfTextViewMarginProvider
	{
		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
			var scrollBar = marginContainer as IVerticalScrollBar;
			var textView = wpfTextViewHost.TextView;
			return Config.Instance.Features.MatchFlags(Features.ScrollbarMarkers)
				&& scrollBar != null
				&& textView.TextBuffer.LikeContentType(Constants.CodeTypes.Markdown)
				&& textView.TextBuffer.MayBeEditor()
				? new CommentMargin(textView, scrollBar)
				: null;
		}
	}

	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(CSharpMembersMargin.MarginName)]
	[Order(After = PredefinedMarginNames.OverviewChangeTracking, Before = CommentMargin.MarginName)]
	[MarginContainer(PredefinedMarginNames.VerticalScrollBar)]
	[ContentType(Constants.CodeTypes.CSharp)]
	[TextViewRole(PredefinedTextViewRoles.EmbeddedPeekTextView)]
	[TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
	sealed class CSharpMembersMarginFactory : IWpfTextViewMarginProvider
	{
		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
			var scrollBar = marginContainer as IVerticalScrollBar;
			return Config.Instance.Features.MatchFlags(Features.ScrollbarMarkers)
				&& scrollBar != null
				&& wpfTextViewHost.TextView.TextBuffer.MayBeEditor()
				? new CSharpMembersMargin(wpfTextViewHost.TextView, scrollBar)
				: null;
		}
	}

	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(DisableChangeTrackerMargin.MarginName)]
	[MarginContainer(PredefinedMarginNames.LeftSelection)]
	[ContentType(Constants.CodeTypes.Code)]
	[TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
	sealed class DisableChangeTrackerMarginFactory : IWpfTextViewMarginProvider
	{
		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
			return CodistPackage.VsVersion.Major >= 17 ? new DisableChangeTrackerMargin(marginContainer) : null;
		}
	}

	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(LineNumberMargin.MarginName)]
	[Order(Before = PredefinedMarginNames.OverviewChangeTracking)]
	[MarginContainer(PredefinedMarginNames.VerticalScrollBar)]
	[ContentType(Constants.CodeTypes.Text)]
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	sealed class LineNumberMarginFactory : IWpfTextViewMarginProvider
	{
		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
			var scrollBarContainer = marginContainer as IVerticalScrollBar;
			return Config.Instance.Features.MatchFlags(Features.ScrollbarMarkers)
				&& scrollBarContainer != null
				&& wpfTextViewHost.TextView.TextBuffer.MayBeEditor()
				? new LineNumberMargin(wpfTextViewHost.TextView, scrollBarContainer)
				: null;
		}
	}

	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(SelectionMargin.MarginName)]
	[Order(Before = PredefinedMarginNames.OverviewChangeTracking)]
	[MarginContainer(PredefinedMarginNames.VerticalScrollBar)]
	[ContentType(Constants.CodeTypes.Text)]
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	sealed class SelectionMarginFactory : IWpfTextViewMarginProvider
	{
		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
			var scrollBarContainer = marginContainer as IVerticalScrollBar;
			return Config.Instance.Features.MatchFlags(Features.ScrollbarMarkers)
				&& scrollBarContainer != null
				&& wpfTextViewHost.TextView.TextBuffer.MayBeEditor()
				? new SelectionMargin(wpfTextViewHost.TextView, scrollBarContainer)
				: null;
		}
	}
}
