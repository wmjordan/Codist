using System;
using System.Windows;
using System.Windows.Controls;

namespace Codist.Controls
{
	sealed class ThemedToolTip : StackPanel
	{
		public TextBlock Title { get; }
		public TextBlock Content { get; }

		public ThemedToolTip() : this(null, null) {
		}
		public ThemedToolTip(string title, string content) {
			Title = new TextBlock {
				Padding = new Thickness(5),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Background = ThemeHelper.TitleBackgroundBrush,
				Foreground = ThemeHelper.TitleTextBrush,
				TextWrapping = TextWrapping.Wrap
			};
			if (title != null) {
				Title.Text = title;
			}
			Content = new TextBlock {
				Padding = new Thickness(10, 3, 10, 8),
				TextWrapping = TextWrapping.Wrap
			};
			if (content != null) {
				Content.Text = content;
			}
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
