using System;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.SmartBars
{
	sealed class TextSmartBar : SmartBar
	{
		public TextSmartBar(IWpfTextView textView, Microsoft.VisualStudio.Text.Operations.ITextSearchService2 textSearchService) : base(textView, textSearchService) {
		}

		protected override BarType Type => BarType.PlainText;
		protected override bool JoinLinesCommandOnToolBar => true;
	}
}
