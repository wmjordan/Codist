using System.ComponentModel.Composition;
using AppHelpers;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Margins
{
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
}
