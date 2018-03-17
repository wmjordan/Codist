using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Margins
{
	/// <summary>
	/// Export a <see cref="IWpfTextViewMarginProvider"/>, which returns an instance of the margin for the editor to use.
	/// </summary>
	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(CodeMargin.MarginName)]
	[Order(After = PredefinedMarginNames.OverviewChangeTracking, Before = PredefinedMarginNames.OverviewMark)]
	[MarginContainer(PredefinedMarginNames.VerticalScrollBar)]
	[ContentType(Constants.CodeTypes.CSharp)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
	sealed class CodeMarginFactory : IWpfTextViewMarginProvider
	{
#pragma warning disable 649
		[Import]
		internal IEditorFormatMapService EditorFormatMapService;
		[Import]
		internal IViewTagAggregatorFactoryService ViewTagAggregatorFactoryService;
#pragma warning restore 649

		#region IWpfTextViewMarginProvider

		/// <summary>
		/// Creates an <see cref="IWpfTextViewMargin"/> for the given <see cref="IWpfTextViewHost"/>.
		/// </summary>
		/// <param name="wpfTextViewHost">The <see cref="IWpfTextViewHost"/> for which to create the <see cref="IWpfTextViewMargin"/>.</param>
		/// <param name="marginContainer">The margin that will contain the newly-created margin.</param>
		/// <returns>The <see cref="IWpfTextViewMargin"/>.
		/// The value may be null if this <see cref="IWpfTextViewMarginProvider"/> does not participate for this context.
		/// </returns>
		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
			var scrollBarContainer = marginContainer as IVerticalScrollBar;
			return scrollBarContainer != null
				? new CodeMargin(wpfTextViewHost, scrollBarContainer, this)
				: null;
		}

		#endregion
	}
}
