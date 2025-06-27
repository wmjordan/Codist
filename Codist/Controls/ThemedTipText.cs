using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.PlatformUI;

namespace Codist.Controls
{
	/// <summary>
	/// Selectable <see cref="TextBlock"/> used in Quick Info.
	/// </summary>
	sealed class ThemedTipText : TextBlock
	{
		public ThemedTipText() {
			TextWrapping = TextWrapping.Wrap;
			this.ReferenceProperty(ForegroundProperty, EnvironmentColors.ToolTipTextBrushKey);
			TextEditorWrapper.CreateFor(this);
		}
		public ThemedTipText(string text) : this() {
			Inlines.Add(text);
		}
		public ThemedTipText(string text, bool bold) : this() {
			this.Append(text, bold);
		}
		public ThemedTipText(int iconId) : this() {
			this.SetGlyph(iconId);
		}
		public ThemedTipText(int iconId, string text) : this() {
			this.SetGlyph(iconId).Append(text);
		}
	}
}
