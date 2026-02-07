using System;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.SmartBars;

sealed class TextSmartBar(IWpfTextView textView) : SmartBar(textView)
{
	protected override BarType Type => BarType.PlainText;
}
