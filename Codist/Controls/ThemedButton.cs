using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace Codist.Controls;

public sealed class ThemedButton : Button, IContextMenuHost, IDisposable
{
	readonly Action _ClickAction;
	readonly RoutedEventHandler _ClickHandler;
	readonly int _ImageId;

	public ThemedButton(object content, object toolTip) {
		Content = content;
		ToolTip = toolTip;
		MinWidth = 0;
		this.ReferenceStyle(VsResourceKeys.ButtonStyleKey)
			.ReferenceCrispImageBackground(EnvironmentColors.MainWindowActiveCaptionColorKey);
		IsEnabledChanged += ThemedButton_IsEnabledChanged;
	}

	void ThemedButton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e) {
		(Content as UIElement)?.UseGrayscaleIcon(!IsEnabled);
	}

	public ThemedButton(int imageId, object toolTip, Action onClickHandler)
		: this(VsImageHelper.GetImage(imageId), toolTip, onClickHandler) {
		_ImageId = imageId;
		if (toolTip is string t) {
			ToolTipOpening += CreateThemedToolTip;
		}
	}
	public ThemedButton(int imageId, object toolTip, RoutedEventHandler clickHandler)
		: this(VsImageHelper.GetImage(imageId), toolTip, clickHandler) {
		_ImageId = imageId;
		if (toolTip is string t) {
			ToolTipOpening += CreateThemedToolTip;
		}
	}
	public ThemedButton(int imageId, string text, object toolTip, Action onClickHandler)
		: this(new StackPanel {
			Orientation = Orientation.Horizontal,
			Children = { VsImageHelper.GetImage(imageId).WrapMargin(WpfHelper.GlyphMargin), new TextBlock { Text = text } }
		}, toolTip, onClickHandler) {
		_ImageId = imageId;
		if (toolTip is string t) {
			ToolTipOpening += CreateThemedToolTip;
		}
	}

	public ThemedButton(object content, object toolTip, Action onClickHandler)
		: this(content, toolTip) {
		_ClickAction = onClickHandler;
		this.HandleEvent(Button.ClickEvent, ThemedButton_Click);
	}

	public ThemedButton(object content, object toolTip, RoutedEventHandler clickHandler)
		: this(content, toolTip) {
		this.HandleEvent(Button.ClickEvent, _ClickHandler = clickHandler);
	}

	void CreateThemedToolTip(object sender, ToolTipEventArgs e) {
		ToolTipOpening -= CreateThemedToolTip;
		ToolTip = new CommandToolTip(_ImageId, ToolTip as string);
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
	internal void Release() {
		IsPressed = false;
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
	public TextBlock Header { get; }
	public bool IsChecked {
		get => _IsChecked;
		set {
			SetValue(IsCheckedProperty, _IsChecked = value);
			this.ReferenceCrispImageBackground(value ? VsColors.FileTabSelectedGradientTopKey : EnvironmentColors.MainWindowActiveCaptionColorKey);
		}
	}
	public bool IsHighlighted {
		get => _IsHighlighted;
		set => SetValue(IsHighlightedProperty, _IsHighlighted = value);
	}
	public bool IsHeaderVisible {
		get => Header?.Visibility == Visibility.Visible;
		set => Header?.ToggleVisibility(value);
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

public class ThemedToggleButton : ToggleButton, IDisposable
{
	TextBlock _Text;

	public ThemedToggleButton(int imageId, string toolTip) {
		Content = new StackPanel {
			Children = {
				VsImageHelper.GetImage(imageId)
			}
		};
		ToolTip = new CommandToolTip(imageId, toolTip);
		this.ReferenceProperty(TextBlock.ForegroundProperty, CommonControlsColors.ButtonTextBrushKey)
			.ReferenceCrispImageBackground(CommonControlsColors.ButtonDefaultColorKey);
	}

	public ThemedToggleButton(int imageId, string toolTip, RoutedEventHandler changedHandler)
		: this(imageId, toolTip) {
		Checked += changedHandler;
		Unchecked += changedHandler;
	}

	protected override void OnChecked(RoutedEventArgs e) {
		base.OnChecked(e);
		this.ReferenceProperty(TextBlock.ForegroundProperty, VsBrushes.HighlightTextKey)
			.ReferenceCrispImageBackground(VsColors.HighlightKey);
	}

	protected override void OnUnchecked(RoutedEventArgs e) {
		base.OnUnchecked(e);
		this.ReferenceProperty(TextBlock.ForegroundProperty, CommonControlsColors.ButtonTextBrushKey)
			.ReferenceCrispImageBackground(CommonControlsColors.ButtonDefaultColorKey);
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

	public void SetText(string text) {
		(Text ??= new TextBlock()).Text = text;
	}

	public void Dispose() {
		if (Content is StackPanel p) {
			p.Children.DisposeCollection();
		}
		Content = null;
	}
}

public sealed class ThemedMenuButton : ThemedToggleButton
{
	readonly Action<ContextMenu> _MenuBuilder, _MenuShownHandler;

	public ThemedMenuButton(int imageId, string toolTip, Action<ContextMenu> menuBuilder, Action<ContextMenu> menuShownHandler = null) : base(imageId, toolTip) {
		this.InheritStyle<ThemedToggleButton>(SharedDictionaryManager.ThemedControls);
		Checked += ShowMenu;
		_MenuBuilder = menuBuilder;
		_MenuShownHandler = menuShownHandler;
	}

	void ShowMenu(object sender, RoutedEventArgs e) {
		ContextMenu m;
		if ((m = ContextMenu) is null) {
			m = ContextMenu = new ContextMenu {
				PlacementTarget = this,
				Resources = SharedDictionaryManager.ContextMenu
			};
			m.Closed += Menu_Closed;
			_MenuBuilder(m);
		}
		_MenuShownHandler?.Invoke(m);
		m.IsOpen = true;
	}

	void Menu_Closed(object sender, RoutedEventArgs e) {
		IsChecked = false;
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

	public IEnumerable<Control> Controls => _ControlPanel.Children.OfType<Control>();

	public int ControlCount => _ControlPanel.Children.Count;

	public ThemedControlGroup AddRange(params Control[] controls) {
		var children = _ControlPanel.Children;
		foreach (var item in controls) {
			SetItemStyle(item);
			children.Add(item);
		}
		return this;
	}

	public ThemedControlGroup AddRange(IEnumerable<Control> controls) {
		var children = _ControlPanel.Children;
		foreach (var item in controls) {
			SetItemStyle(item);
			children.Add(item);
		}
		return this;
	}

	public void Insert(int index, Control control) {
		SetItemStyle(control);
		_ControlPanel.Children.Insert(index, control);
	}

	static void SetItemStyle(Control item) {
		item.Padding = WpfHelper.NoMargin;
		item.Margin = WpfHelper.NoMargin;
		item.BorderThickness = WpfHelper.NoMargin;
		item.MinHeight = 10;
	}
}
