using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Microsoft.VisualStudio.PlatformUI;

namespace Codist.Controls;

/// <summary>
/// Button with popup content for help messages.
/// </summary>
public class HelpMessageButton : ToggleButton
{
	readonly Popup _popup;
	readonly Border _popupFocusTarget;

	public static readonly DependencyProperty HelpContentProperty =
		DependencyProperty.Register(
			nameof(HelpContent),
			typeof(object),
			typeof(HelpMessageButton),
			new PropertyMetadata(null));

	public HelpMessageButton() {
		Content = VsImageHelper.GetImage(IconIds.Question);
		HorizontalAlignment = HorizontalAlignment.Left;
		VerticalAlignment = VerticalAlignment.Center;
		HorizontalContentAlignment = HorizontalAlignment.Center;
		VerticalContentAlignment = VerticalAlignment.Center;
		Padding = WpfHelper.SmallMargin;
		Resources = SharedDictionaryManager.ThemedControls;
		BorderThickness = default;

		_popup = new Popup {
			PlacementTarget = this,
			Placement = PlacementMode.Bottom,
			StaysOpen = false,
			AllowsTransparency = true
		};

		_popupFocusTarget = new Border {
			Focusable = true,
			FocusVisualStyle = null,
			BorderThickness = WpfHelper.TinyMargin,
			Padding = WpfHelper.SmallMargin,
		}.ReferenceProperty(BorderBrushProperty, EnvironmentColors.ToolTipBorderBrushKey);

		var contentControl = new ContentControl();
		contentControl.SetBinding(ContentProperty, new Binding(nameof(HelpContent)) { Source = this });
		_popupFocusTarget.Child = contentControl;

		_popup.Child = _popupFocusTarget;

	}

	public HelpMessageButton(int iconId, string message) : this() {
		HelpContent = new CommandToolTip(iconId, message);
	}

	public object HelpContent {
		get => GetValue(HelpContentProperty);
		set => SetValue(HelpContentProperty, value);
	}

	protected override void OnChecked(RoutedEventArgs e) {
		base.OnChecked(e);
		Unloaded += HandleUnloaded;
		_popup.Closed += HandlePopupClose;
		_popup.IsOpen = true;
	}

	protected override void OnUnchecked(RoutedEventArgs e) {
		base.OnUnchecked(e);
		Unloaded -= HandleUnloaded;
		_popup.Closed -= HandlePopupClose;
		_popup.IsOpen = false;
	}

	void HandlePopupClose(object sender, EventArgs e) {
		IsChecked = false;
	}

	void HandleUnloaded(object sender, RoutedEventArgs e) {
		_popup.Closed -= HandlePopupClose;
		Unloaded -= HandleUnloaded;
	}
}