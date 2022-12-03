using System.Windows.Controls;
using Microsoft.VisualStudio.PlatformUI;

namespace Codist.Controls
{
	sealed class ThemedToolBarText : TextBlock
	{
		public ThemedToolBarText() {
			SetResourceReference(ForegroundProperty, EnvironmentColors.SystemMenuTextBrushKey);
		}
		public ThemedToolBarText(string text) : this() {
			Inlines.Add(text);
		}
	}
}
