using System;
using System.Windows;
using System.Windows.Controls;

namespace Codist.Controls
{
	sealed class ThemedToolTip : StackPanel
	{
		static Thickness _TitlePadding = new Thickness(5);
		static Thickness _ContentPadding = new Thickness(10, 3, 10, 8);

		public TextBlock Title { get; }
		public TextBlock Content { get; }

		public ThemedToolTip() : this(null, null) {
		}
		public ThemedToolTip(string title, string content) {
			Title = new TextBlock {
				Padding = _TitlePadding,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Background = ThemeHelper.TitleBackgroundBrush,
				Foreground = ThemeHelper.TitleTextBrush,
				TextWrapping = TextWrapping.Wrap
			};
			if (title != null) {
				Title.Text = title;
			}
			Content = new TextBlock {
				Padding = _ContentPadding,
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
