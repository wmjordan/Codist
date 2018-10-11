using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

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
}
