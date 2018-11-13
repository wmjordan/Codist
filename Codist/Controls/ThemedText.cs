using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.PlatformUI;

namespace Codist.Controls
{
	sealed class ThemedTipText : TextBlock
	{
		public ThemedTipText() {
			TextWrapping = TextWrapping.Wrap;
			SetResourceReference(ForegroundProperty, ThemeHelper.ToolTipTextBrush);
		}
		public ThemedTipText(string text) : this() {
			Inlines.Add(text);
		}
		public ThemedTipText(string text, bool bold) : this() {
			this.Append(text, bold);
		}
		public ThemedTipText InvertLastRun() {
			var last = Inlines.LastInline;
			last.Background = ThemeHelper.ToolTipTextBrush;
			last.Foreground = ThemeHelper.ToolTipBackgroundBrush;
			return this;
		}
		public ThemedTipText UnderlineLastRun() {
			Inlines.LastInline.TextDecorations = System.Windows.TextDecorations.Underline;
			return this;
		}
	}
	sealed class ThemedToolBarText : TextBlock
	{
		public ThemedToolBarText() {
			SetResourceReference(ForegroundProperty, EnvironmentColors.SystemMenuTextBrushKey);
		}
		public ThemedToolBarText(string text) : this() {
			Inlines.Add(text);
		}
	}
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
