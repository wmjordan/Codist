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
			if (Config.Instance.QuickInfoMaxWidth > 0) {
				MaxWidth = Config.Instance.QuickInfoMaxWidth;
			}
		}

		protected override void OnVisualParentChanged(DependencyObject oldParent) {
			var c = this.GetVisualParent<ContentPresenter>();
			if (c != null) {
				c.Margin = WpfHelper.NoMargin;
			}
			base.OnVisualParentChanged(oldParent);
		}
	}
}
