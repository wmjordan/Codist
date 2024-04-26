using System;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace Codist.Controls
{
	public sealed class ThemedButton : Button, IContextMenuHost, IDisposable
	{
		readonly Action _ClickAction;
		readonly RoutedEventHandler _ClickHandler;

		public ThemedButton(object content, object toolTip) {
			Content = content;
			ToolTip = toolTip;
			MinWidth = 0;
			this.ReferenceStyle(VsResourceKeys.ButtonStyleKey)
				.ReferenceCrispImageBackground(EnvironmentColors.MainWindowActiveCaptionColorKey);
		}

		public ThemedButton(int imageId, object toolTip, Action onClickHandler)
			: this(VsImageHelper.GetImage(imageId), toolTip, onClickHandler) { }
		public ThemedButton(int imageId, string text, object toolTip, Action onClickHandler)
			: this(new StackPanel {
				Orientation = Orientation.Horizontal,
				Children = { VsImageHelper.GetImage(imageId).WrapMargin(WpfHelper.GlyphMargin), new TextBlock { Text = text } }
			}, toolTip, onClickHandler) { }

		public ThemedButton(object content, object toolTip, Action onClickHandler)
			: this(content, toolTip) {
			_ClickAction = onClickHandler;
			this.HandleEvent(Button.ClickEvent, ThemedButton_Click);
		}

		public ThemedButton(object content, object toolTip, RoutedEventHandler clickHandler)
			: this(content, toolTip) {
			this.HandleEvent(Button.ClickEvent, _ClickHandler = clickHandler);
		}

		public void ShowContextMenu(RoutedEventArgs args) {
		}

		void ThemedButton_Click(object sender, RoutedEventArgs e) {
			_ClickAction?.Invoke();
		}

		internal void PerformClick() {
			OnClick();
		}

		internal void Press() {
			IsPressed = !IsPressed;
			OnClick();
		}

		public void Dispose() {
			if (Content is StackPanel p) {
				p.Children.DisposeCollection();
			}
			this.DetachEvent(Button.ClickEvent, ThemedButton_Click);
			if (_ClickHandler != null) {
				this.DetachEvent(Button.ClickEvent, _ClickHandler);
			}
			Content = null;
		}
	}

	public class ThemedImageButton : Button, IDisposable
	{
		public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register("IsChecked", typeof(bool), typeof(ThemedImageButton));
		public static readonly DependencyProperty IsHighlightedProperty = DependencyProperty.Register("IsHighlighted", typeof(bool), typeof(ThemedImageButton));
		bool _IsChecked, _IsHighlighted;

		public ThemedImageButton(int imageId) : this(imageId, (TextBlock)null) { }
		public ThemedImageButton(int imageId, string content) : this(imageId, new TextBlock { Text = content }) { }
		public ThemedImageButton(int imageId, TextBlock content) {
			Content = content != null ?
				(object)new StackPanel {
					Orientation = Orientation.Horizontal,
					Children = {
						VsImageHelper.GetImage(imageId).WrapMargin(WpfHelper.SmallHorizontalMargin),
						content
					}
				} : VsImageHelper.GetImage(imageId).WrapMargin(WpfHelper.SmallHorizontalMargin);
			Header = content;
			this.ReferenceStyle(typeof(ThemedImageButton))
				.ReferenceCrispImageBackground(EnvironmentColors.MainWindowActiveCaptionColorKey);
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

		public virtual void Dispose() {
			if (Content is StackPanel p) {
				p.Children.DisposeCollection();
			}
			Content = null;
		}
	}

	public sealed class ThemedToggleButton : ToggleButton, IDisposable
	{
		TextBlock _Text;

		public ThemedToggleButton(int imageId, string toolTip) {
			Content = new StackPanel {
				Children = {
					VsImageHelper.GetImage(imageId)
				}
			};
			ToolTip = toolTip;
			this.ReferenceCrispImageBackground(EnvironmentColors.MainWindowActiveCaptionColorKey);
		}

		public ThemedToggleButton(int imageId, string toolTip, RoutedEventHandler changedHandler)
			: this(imageId, toolTip) {
			Checked += changedHandler;
			Unchecked += changedHandler;
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

		public void Dispose() {
			if (Content is StackPanel p) {
				p.Children.DisposeCollection();
			}
			Content = null;
		}
	}

	public sealed class ThemedControlGroup : Border
	{
		readonly StackPanel _ControlPanel;

		public ThemedControlGroup() {
			BorderThickness = WpfHelper.TinyMargin;
			CornerRadius = new CornerRadius(3);
			Child = _ControlPanel = new StackPanel { Orientation = Orientation.Horizontal };
			this.ReferenceProperty(BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey);
		}

		public ThemedControlGroup(params Control[] controls) : this() {
			AddRange(controls);
		}

		public ThemedControlGroup AddRange(params Control[] controls) {
			foreach (var item in controls) {
				item.Padding = WpfHelper.NoMargin;
				item.Margin = WpfHelper.NoMargin;
				item.BorderThickness = WpfHelper.NoMargin;
				item.MinHeight = 10;
				item.SetValue(ToolTipService.PlacementProperty, PlacementMode.Left);
				_ControlPanel.Add(item);
			}
			return this;
		}
	}
}
