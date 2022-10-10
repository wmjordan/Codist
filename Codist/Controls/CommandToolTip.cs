using System;
using System.Windows.Controls;
using System.Windows;

namespace Codist.Controls
{
	sealed class CommandToolTip : StackPanel
	{
		const int TipWidth = 300;

		public CommandToolTip(int imageId, string tip) {
			Orientation = Orientation.Horizontal;
			var icon = ThemeHelper.GetImage(imageId, ThemeHelper.MiddleIconSize).WrapMargin(WpfHelper.MiddleMargin);
			icon.VerticalAlignment = VerticalAlignment.Top;
			Children.Add(icon);
			var p = tip.IndexOf('\n');
			if (p == -1) {
				Children.Add(new TextBlock { Margin = WpfHelper.SmallMargin, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, MaxWidth = TipWidth }.Append(tip));
				return;
			}
			Children.Add(new StackPanel {
				Margin = WpfHelper.SmallMargin,
				Children = {
					new TextBlock { FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, MaxWidth = TipWidth }.Append(tip.Substring(0, p > 0 && tip[p - 1] == '\r' ? p - 1 : p)),
					new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = TipWidth }.Append(tip.Substring(p + 1))
				}
			});
		}

		public CommandToolTip(int imageId, string tipTitle, UIElement tipContent) {
			Orientation = Orientation.Horizontal;
			var icon = ThemeHelper.GetImage(imageId, ThemeHelper.MiddleIconSize).WrapMargin(WpfHelper.MiddleMargin);
			icon.VerticalAlignment = VerticalAlignment.Top;
			Children.Add(icon);
			if (tipContent is FrameworkElement f) {
				f.MaxWidth = TipWidth;
			}
			Children.Add(new StackPanel {
				Margin = WpfHelper.SmallMargin,
				Children = {
					new TextBlock { FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, MaxWidth = TipWidth }.Append(tipTitle),
					tipContent
				}
			});
		}
	}
}
