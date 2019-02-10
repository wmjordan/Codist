using System.ComponentModel.Composition;
using AppHelpers;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Margins
{
	/// <summary>
	/// Export a <see cref="IWpfTextViewMarginProvider"/>, which returns an instance of the margin for the editor to use.
	/// </summary>
	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(CommentMargin.MarginName)]
	[Order(After = PredefinedMarginNames.OverviewChangeTracking, Before = PredefinedMarginNames.OverviewMark)]
	[MarginContainer(PredefinedMarginNames.VerticalScrollBar)]
	[ContentType(Constants.CodeTypes.Code)]
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	sealed class CommentMarginFactory : IWpfTextViewMarginProvider
	{
		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
			var scrollBarContainer = marginContainer as IVerticalScrollBar;
			return Config.Instance.Features.MatchFlags(Features.ScrollbarMarkers)
				&& scrollBarContainer != null
				&& Classifiers.CommentTaggerProvider.IsCommentTaggable(wpfTextViewHost.TextView)
				? new CommentMargin(wpfTextViewHost.TextView, scrollBarContainer)
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
			return Config.Instance.Features.MatchFlags(Features.ScrollbarMarkers) && scrollBar != null ? new CSharpMembersMargin(wpfTextViewHost.TextView, scrollBar) : null;
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
			return Config.Instance.Features.MatchFlags(Features.ScrollbarMarkers) && scrollBarContainer != null
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
			return Config.Instance.Features.MatchFlags(Features.ScrollbarMarkers) && scrollBarContainer != null
				? new SelectionMargin(wpfTextViewHost.TextView, scrollBarContainer)
				: null;
		}
	}
}
