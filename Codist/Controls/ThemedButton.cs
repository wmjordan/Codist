using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;

namespace Codist.Controls
{
	public sealed class ThemedButton : Button
	{
		readonly Action _clickHanler;

		public ThemedButton(int imageId, object toolTip, Action onClickHandler)
			: this(ThemeHelper.GetImage(imageId), toolTip, onClickHandler) { }

		public ThemedButton(object content, object toolTip, Action onClickHandler) {
			Content = content;
			ToolTip = toolTip;
			Margin = WpfHelper.GlyphMargin;
			this.ReferenceProperty(BackgroundProperty, CommonControlsColors.ButtonBrushKey);
			_clickHanler = onClickHandler;
			Click += ThemedButton_Click;
			this.ReferenceCrispImageBackground(Microsoft.VisualStudio.PlatformUI.EnvironmentColors.MainWindowActiveCaptionColorKey);
		}

		void ThemedButton_Click(object sender, RoutedEventArgs e) {
			_clickHanler?.Invoke();
		}
	}

	public sealed class ThemedToggleButton : System.Windows.Controls.Primitives.ToggleButton
	{
		TextBlock _Text;

		public ThemedToggleButton(int imageId, string toolTip) {
			Content = new StackPanel {
				Children = {
					ThemeHelper.GetImage(imageId)
				}
			};
			ToolTip = toolTip;
			this.ReferenceCrispImageBackground(EnvironmentColors.MainWindowActiveCaptionColorKey);
		}

		public TextBlock Text {
			get => _Text;
			set {
				var p = Content as StackPanel;
				if (_Text == null) {
					p.Orientation = Orientation.Horizontal;
					p.Children.Add(_Text = value);
				}
				else {
					p.Children[1] = _Text = value;
				}
			}
		}
	}
}
