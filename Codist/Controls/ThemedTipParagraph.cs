using System.Windows.Controls;

namespace Codist.Controls
{
	sealed class ThemedTipParagraph
	{
		public ThemedTipParagraph(int iconId, TextBlock content) {
			Icon = iconId;
			Content = content ?? new ThemedTipText();
		}
		public ThemedTipParagraph(int iconId) : this(iconId, null) {
		}
		public ThemedTipParagraph(TextBlock content) : this(0, content) {
		}

		public int Icon { get; }
		public TextBlock Content { get; }
	}
}
