using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Margins
{
	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(LineNumberMargin.MarginName)]
	[Order(After = PredefinedMarginNames.OverviewChangeTracking, Before = PredefinedMarginNames.OverviewMark)]
	[MarginContainer(PredefinedMarginNames.VerticalScrollBar)]
	[ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
	sealed class LineNumberMarginFactory : IWpfTextViewMarginProvider
	{
#pragma warning disable 649
		[Import]
		internal IEditorFormatMapService EditorFormatMapService;
		[Import]
		internal IViewTagAggregatorFactoryService ViewTagAggregatorFactoryService;
#pragma warning restore 649

		#region IWpfTextViewMarginProvider

		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
			var scrollBarContainer = marginContainer as IVerticalScrollBar;
			return scrollBarContainer != null
				? new LineNumberMargin(wpfTextViewHost, scrollBarContainer, this)
				: null;
		}

		#endregion
	}
}
