using System;
using System.Windows.Documents;
using Microsoft.VisualStudio.Shell;

namespace Codist.Controls
{
	public class ThemedHyperlink : Hyperlink
	{
		public ThemedHyperlink() {
			SetResourceReference(StyleProperty, VsResourceKeys.ThemedDialogHyperlinkStyleKey);
		}
	}
}
