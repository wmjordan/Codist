using System.Windows;
using System.Windows.Controls;

namespace Codist.Controls
{
	sealed class ThemedText : TextBlock
	{
		public ThemedText() {
			TextWrapping = TextWrapping.Wrap;
			SetResourceReference(ForegroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ButtonTextBrushKey);
		}
		public ThemedText(string text) : this() {
			Inlines.Add(text);
		}
		public ThemedText(string text, bool bold) : this() {
			this.Append(text, bold);
		}
	}
}
