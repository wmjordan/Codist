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
			this.ReferenceCrispImageBackground(Microsoft.VisualStudio.PlatformUI.EnvironmentColors.MainWindowActiveCaptionColorKey);
		}

		void ThemedButton_Click(object sender, RoutedEventArgs e) {
			_clickHanler?.Invoke();
		}
	}

	public class ThemedToggleButton : System.Windows.Controls.Primitives.ToggleButton
	{
		public ThemedToggleButton(int imageId, string toolTip) {
			Content = ThemeHelper.GetImage(imageId);
			ToolTip = toolTip;
			Margin = WpfHelper.NoMargin;
			Background = System.Windows.Media.Brushes.Transparent;
			this.ReferenceCrispImageBackground(Microsoft.VisualStudio.PlatformUI.EnvironmentColors.MainWindowActiveCaptionColorKey);
		}
	}
}
