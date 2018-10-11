using System.Windows;
using System.Windows.Controls;

namespace Codist.Controls
{
	sealed class ThemedTipText : TextBlock
	{
		public ThemedTipText() {
			TextWrapping = TextWrapping.Wrap;
			SetResourceReference(ForegroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ButtonTextBrushKey);
		}
		public ThemedTipText(string text) : this() {
			Inlines.Add(text);
		}
		public ThemedTipText(string text, bool bold) : this() {
			this.Append(text, bold);
		}
	}
}
