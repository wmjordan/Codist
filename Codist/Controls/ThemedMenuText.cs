using System.Windows.Controls;
using Microsoft.VisualStudio.PlatformUI;

namespace Codist.Controls
{
	sealed class ThemedMenuText : TextBlock
	{
		public ThemedMenuText() {
			SetResourceReference(ForegroundProperty, EnvironmentColors.SystemMenuTextBrushKey);
		}
		public ThemedMenuText(string text) : this() {
			Inlines.Add(text);
		}
		public ThemedMenuText(string text, bool bold) : this() {
			this.Append(text, bold);
		}
	}
}
