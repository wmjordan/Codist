using System.Windows;
using System.Windows.Controls;

namespace Codist.Controls
{
	sealed class ToolTipText : TextBlock
	{
		public ToolTipText() {
			Foreground = ThemeHelper.ToolTipTextBrush;
			TextWrapping = TextWrapping.Wrap;
		}
		public ToolTipText(string text) : this() {
			Inlines.Add(text);
		}
		public ToolTipText(string text, bool bold) : this() {
			this.Append(text, true);
		}
	}
}
