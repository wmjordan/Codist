using System.ComponentModel.Composition;
using AppHelpers;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.OverviewMargin;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Margins
{
	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(CSharpMembersMargin.Name)]
	[Order(After = PredefinedMarginNames.OverviewChangeTracking, Before = CommentMargin.MarginName)]
	[MarginContainer(PredefinedMarginNames.VerticalScrollBar)]
	[ContentType(Constants.CodeTypes.CSharp)]
	[TextViewRole(PredefinedTextViewRoles.EmbeddedPeekTextView)]
	[TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
	sealed class CSharpMembersMarginFactory : IWpfTextViewMarginProvider
	{
		[Import]
		internal IViewTagAggregatorFactoryService TagAggregatorFactoryService { get; set; }

		[Import]
		internal IEditorFormatMapService EditorFormatMapService { get; set; }

		/// <summary>Creates an <see cref="IWpfTextViewMargin" /> for the given <see cref="IWpfTextViewHost" />.</summary>
		/// <returns>The <see cref="IWpfTextViewMargin" />. </returns>
		/// <param name="textViewHost">The <see cref="IWpfTextViewHost" /> for which to create the <see cref="IWpfTextViewMargin" />.</param>
		/// <param name="containerMargin">The margin that will contain the newly-created margin.</param>
		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost textViewHost, IWpfTextViewMargin containerMargin) {
			var scrollBar = containerMargin as IVerticalScrollBar;
			return Config.Instance.Features.MatchFlags(Features.ScrollbarMarkers) && scrollBar != null ? new CSharpMembersMargin(textViewHost, scrollBar, this) : null;
		}
	}
}
