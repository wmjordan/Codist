using System;
using System.Windows;
using System.Windows.Controls;

namespace Codist.Controls
{
	public class ThemedButton : Button
	{
		readonly Action _clickHanler;

		public ThemedButton(int imageId, object toolTip, Action onClickHandler)
			: this(ThemeHelper.GetImage(imageId), toolTip, onClickHandler) { }

		public ThemedButton(object content, object toolTip, Action onClickHandler) {
			Content = content;
			ToolTip = toolTip;
			Margin = WpfHelper.GlyphMargin;
			Background = System.Windows.Media.Brushes.Transparent;
			_clickHanler = onClickHandler;
			Click += ThemedButton_Click;
			this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
		}

		private void ThemedButton_Click(object sender, RoutedEventArgs e) {
			_clickHanler?.Invoke();
			var t = this.GetTemplate();
		}
	}

	public class ThemedToggleButton : System.Windows.Controls.Primitives.ToggleButton
	{
		public ThemedToggleButton(int imageId, string toolTip) {
			Content = ThemeHelper.GetImage(imageId);
			ToolTip = toolTip;
			Margin = WpfHelper.NoMargin;
			Background = System.Windows.Media.Brushes.Transparent;
			this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
		}
	}
}
