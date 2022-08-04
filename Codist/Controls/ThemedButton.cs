using System;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.VisualStudio.PlatformUI;

namespace Codist.Controls
{
	public sealed class ThemedButton : Button, IContextMenuHost
	{
		readonly Action _clickHanler;

		public ThemedButton(object content, object toolTip) {
			Content = content;
			ToolTip = toolTip;
			this.ReferenceProperty(ForegroundProperty, CommonControlsColors.ButtonTextBrushKey);
			this.ReferenceProperty(BackgroundProperty, CommonControlsColors.ButtonBrushKey);
			this.ReferenceCrispImageBackground(EnvironmentColors.MainWindowActiveCaptionColorKey);
		}

		public ThemedButton(int imageId, object toolTip, Action onClickHandler)
			: this(ThemeHelper.GetImage(imageId), toolTip, onClickHandler) { }
		public ThemedButton(int imageId, string text, object toolTip, Action onClickHandler)
			: this(new StackPanel {
				Orientation = Orientation.Horizontal,
				Children = { ThemeHelper.GetImage(imageId).WrapMargin(WpfHelper.GlyphMargin), new TextBlock { Text = text } }
			}, toolTip, onClickHandler) { }

		public ThemedButton(object content, object toolTip, Action onClickHandler)
			: this(content, toolTip) {
			_clickHanler = onClickHandler;
			Click += ThemedButton_Click;
		}

		public ThemedButton(object content, object toolTip, RoutedEventHandler clickHandler)
			: this(content, toolTip) {
			Click += clickHandler;
		}

		public void ShowContextMenu(RoutedEventArgs args) {
		}

		void ThemedButton_Click(object sender, RoutedEventArgs e) {
			_clickHanler?.Invoke();
		}

		internal void PerformClick() {
			OnClick();
		}
	}

	public class ThemedImageButton : Button
	{
		public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register("IsChecked", typeof(bool), typeof(ThemedImageButton));
		public static readonly DependencyProperty IsHighlightedProperty = DependencyProperty.Register("IsHighlighted", typeof(bool), typeof(ThemedImageButton));
		bool _IsChecked, _IsHighlighted;

		public ThemedImageButton(int imageId, TextBlock content) {
			Content = content != null ?
				(object)new StackPanel {
					Orientation = Orientation.Horizontal,
					Children = {
						ThemeHelper.GetImage(imageId).WrapMargin(WpfHelper.SmallHorizontalMargin),
						content
					}
				} : ThemeHelper.GetImage(imageId).WrapMargin(WpfHelper.SmallHorizontalMargin);
			Header = content;
			this.ReferenceStyle(typeof(ThemedImageButton));
			this.ReferenceCrispImageBackground(EnvironmentColors.MainWindowActiveCaptionColorKey);
		}
		public object Header { get; }
		public bool IsChecked {
			get => _IsChecked;
			set => SetValue(IsCheckedProperty, _IsChecked = value);
		}
		public bool IsHighlighted {
			get => _IsHighlighted;
			set => SetValue(IsHighlightedProperty, _IsHighlighted = value);
		}
		internal void PerformClick() {
			OnClick();
		}

		protected override AutomationPeer OnCreateAutomationPeer() {
			return null;
		}
	}

	public sealed class ThemedToggleButton : ToggleButton
	{
		TextBlock _Text;

		public ThemedToggleButton(int imageId, string toolTip) {
			Content = new StackPanel {
				Children = {
					ThemeHelper.GetImage(imageId)
				}
			};
			ToolTip = toolTip;
			ToolTipService.SetPlacement(this, PlacementMode.Left);
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
