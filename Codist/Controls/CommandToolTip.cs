using System;
using System.Windows.Controls;
using System.Windows;
using Microsoft.VisualStudio.PlatformUI;

namespace Codist.Controls
{
	sealed class CommandToolTip : StackPanel
	{
		const int TipWidth = 300;
		readonly StackPanel _TextPanel;

		CommandToolTip(int imageId) {
			Orientation = Orientation.Horizontal;
			var icon = VsImageHelper.GetImage(imageId, VsImageHelper.MiddleIconSize).WrapMargin(WpfHelper.MiddleMargin);
			icon.VerticalAlignment = VerticalAlignment.Top;
			Children.Add(icon);
			this.ReferenceProperty(TextBlock.ForegroundProperty, EnvironmentColors.ToolTipTextBrushKey);
		}

		public CommandToolTip(int imageId, string tip) : this(imageId) {
			var p = tip.IndexOf('\n');
			if (p == -1) {
				Children.Add(new TextBlock {
						Margin = WpfHelper.MiddleMargin,
						FontWeight = FontWeights.Bold
					}.WrapAtWidth(TipWidth).Append(tip));
				return;
			}
			Children.Add(_TextPanel = new StackPanel {
				Margin = WpfHelper.MiddleMargin,
				Children = {
					new TextBlock {
							FontWeight = FontWeights.Bold
						}.WrapAtWidth(TipWidth)
						.Append(tip.Substring(0, p > 0 && tip[p - 1] == '\r' ? p - 1 : p)),
					new TextBlock ().WrapAtWidth(TipWidth)
						.Append(tip.Substring(p + 1))
				}
			});
		}

		public CommandToolTip(int imageId, string tipTitle, UIElement tipContent) : this(imageId) {
			if (tipContent is FrameworkElement f) {
				f.MaxWidth = TipWidth;
			}
			Children.Add(_TextPanel = new StackPanel {
				Margin = WpfHelper.MiddleMargin,
				Children = {
					new TextBlock { FontWeight = FontWeights.Bold }.WrapAtWidth(TipWidth).Append(tipTitle),
					tipContent
				}
			});
		}

		public UIElement Description => _TextPanel?.Children.Count > 1 ? _TextPanel.Children[1] : null;

		protected override void OnVisualParentChanged(DependencyObject oldParent) {
			this.GetParent<Border>()
				?.ReferenceProperty(Border.BackgroundProperty, EnvironmentColors.ToolTipBrushKey);
			base.OnVisualParentChanged(oldParent);
		}
	}
}
