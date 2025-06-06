using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.PlatformUI;

namespace Codist.Controls
{
	sealed class ThemedToolTip : StackPanel
	{
		static Thickness __TitlePadding = new Thickness(5);
		static Thickness __ContentPadding = new Thickness(8, 3, 8, 8);

		public ThemedToolTip() : this(null, null) {
		}
		public ThemedToolTip(string title, string content) {
			Title = new TextBlock {
				Padding = __TitlePadding,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				TextWrapping = TextWrapping.Wrap
			}
			.ReferenceProperty(TextBlock.BackgroundProperty, EnvironmentColors.MainWindowActiveCaptionBrushKey)
			.ReferenceProperty(TextBlock.ForegroundProperty, EnvironmentColors.MainWindowActiveCaptionTextBrushKey);
			if (title != null) {
				Title.Text = title;
			}
			Children.Add(Title);
			Content = AddTextBlock();
			if (content != null) {
				Content.Text = content;
			}
			if (Config.Instance.QuickInfo.MaxWidth > 0) {
				MaxWidth = Config.Instance.QuickInfo.MaxWidth;
			}
			this.SetProperty(TextBlock.FontFamilyProperty, ThemeCache.ToolTipFont)
				.SetProperty(TextBlock.FontSizeProperty, ThemeCache.ToolTipFontSize);
		}

		public TextBlock Title { get; }
		public TextBlock Content { get; }

		public TextBlock AddTextBlock() {
			var t = new TextBlock { Padding = __ContentPadding, TextWrapping = TextWrapping.Wrap }
				.ReferenceProperty(TextBlock.BackgroundProperty, EnvironmentColors.ToolTipBrushKey)
				.ReferenceProperty(TextBlock.ForegroundProperty, EnvironmentColors.ToolTipTextBrushKey);
			Children.Add(t);
			return t;
		}
		public Border AddBorder() {
			var t = new Border { Padding = __ContentPadding }
				.ReferenceProperty(Border.BackgroundProperty, EnvironmentColors.ToolTipBrushKey);
			Children.Add(t);
			return t;
		}

		protected override void OnVisualParentChanged(DependencyObject oldParent) {
			var c = this.GetParent<ContentPresenter>();
			if (c != null) {
				c.Margin = WpfHelper.NoMargin;
			}
			base.OnVisualParentChanged(oldParent);
		}
	}
}
