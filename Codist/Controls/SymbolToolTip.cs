using System;
using System.Windows;
using System.Windows.Controls;

namespace Codist.Controls
{
	sealed class SymbolToolTip : StackPanel
	{
		public TextBlock Title { get; }
		public TextBlock Content { get; }

		public SymbolToolTip() {
			Margin = new Thickness(-6, -4, -6, -5);
			Title = new TextBlock {
				Padding = new Thickness(5),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Background = ThemeHelper.TitleBackgroundBrush,
				Foreground = ThemeHelper.TitleTextBrush,
				TextWrapping = TextWrapping.Wrap
			};
			Content = new TextBlock {
				Padding = new Thickness(10, 3, 10, 8),
				TextWrapping = TextWrapping.Wrap
			};
			Children.Add(Title);
			Children.Add(Content);
		}
	}
}
