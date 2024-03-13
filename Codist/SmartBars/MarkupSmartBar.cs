using System;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.SmartBars
{
	sealed class MarkupSmartBar : SmartBar
	{
		public MarkupSmartBar(IWpfTextView textView, Microsoft.VisualStudio.Text.Operations.ITextSearchService2 textSearchService) : base(textView, textSearchService) {
		}

		protected override BarType Type => BarType.Markup;
		protected override bool JoinLinesCommandOnToolBar => true;
	}
}
