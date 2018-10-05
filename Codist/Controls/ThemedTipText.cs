using System.Windows;
using System.Windows.Controls;

namespace Codist.Controls
{
	sealed class ThemedTipText : TextBlock
	{
		public ThemedTipText() {
			Foreground = ThemeHelper.ToolTipTextBrush;
			TextWrapping = TextWrapping.Wrap;
		}
		public ThemedTipText(string text) : this() {
			Inlines.Add(text);
		}
		public ThemedTipText(string text, bool bold) : this() {
			this.Append(text, true);
		}
	}
}
