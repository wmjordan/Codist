using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CLR;
using Codist.Controls;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;

namespace Codist.Options
{
	sealed class TitleBox : ContentControl
	{
		public TitleBox() {
			Margin = new Thickness(0, 3, 0, 0);
			Content = new Border {
				BorderThickness = new Thickness(0, 0, 0, 1),
				BorderBrush = SystemColors.ControlDarkDarkBrush,
				Padding = WpfHelper.SmallHorizontalMargin,
				Child = Title = new TextBlock() { FontWeight = FontWeights.Bold }
			};
		}

		public TitleBox(string text) : this() {
			Title.Text = text;
		}

		public TextBlock Title { get; }
	}

	sealed class Note : Label
	{
		public Note(string text) {
			Content = new TextBlock {
				Text = text,
				TextWrapping = TextWrapping.Wrap
			};
		}
		public Note(TextBlock text) {
			Content = text;
			text.TextWrapping = TextWrapping.Wrap;
		}
	}

	sealed class DescriptionBox : TextBlock
	{
		public DescriptionBox(string text) {
			Margin = new Thickness(23, 0, 3, 0);
			TextWrapping = TextWrapping.Wrap;
			Foreground = SystemColors.GrayTextBrush;
			Text = text;
		}
	}
	sealed class OptionBox<TOption> : CheckBox where TOption : struct, Enum
	{
		readonly TOption _Option;
		readonly Action<TOption, bool> _CheckEventHandler;

		public OptionBox() {
			Margin = WpfHelper.SmallMargin;
		}
		public OptionBox(TOption initialValue, TOption option, Action<TOption, bool> checkEventHandler) : this() {
			IsChecked = initialValue.MatchFlags(option);
			_Option = option;
			_CheckEventHandler = checkEventHandler;
		}
		public OptionBox(TOption initialValue, TOption option, Action<TOption, bool> checkEventHandler, Features updateFeature) : this(initialValue, option, checkEventHandler) {
			_CheckEventHandler += (_, __) => Config.Instance.FireConfigChangedEvent(updateFeature);
		}
		public void UpdateWithOption(TOption newValue) {
			IsChecked = newValue.MatchFlags(_Option);
		}
		protected override void OnChecked(RoutedEventArgs e) {
			base.OnChecked(e);
			_CheckEventHandler?.Invoke(_Option, true);
		}
		protected override void OnUnchecked(RoutedEventArgs e) {
			base.OnUnchecked(e);
			_CheckEventHandler?.Invoke(_Option, false);
		}
	}
	sealed class OptionBox : CheckBox
	{
		readonly Action<bool?> _CheckEventHandler;

		public OptionBox() {
			Margin = WpfHelper.SmallMargin;
		}
		public OptionBox(bool initialValue, Action<bool?> checkEventHandler) : this() {
			IsChecked = initialValue;
			_CheckEventHandler = checkEventHandler;
		}
		public OptionBox(bool? initialValue, Action<bool?> checkEventHandler) : this() {
			IsChecked = initialValue;
			_CheckEventHandler = checkEventHandler;
		}
		protected override void OnIndeterminate(RoutedEventArgs e) {
			base.OnIndeterminate(e);
			_CheckEventHandler?.Invoke(null);
		}
		protected override void OnChecked(RoutedEventArgs e) {
			base.OnChecked(e);
			_CheckEventHandler?.Invoke(true);
		}
		protected override void OnUnchecked(RoutedEventArgs e) {
			base.OnUnchecked(e);
			_CheckEventHandler?.Invoke(false);
		}
	}

	sealed class ColorButton : Button
	{
		readonly Border _Border;
		readonly Action<Color> _ColorChangedHandler;
		bool _IsColorChanging;

		public ColorButton(Color color, string text, Action<Color> colorChangedHandler) {
			Content = new StackPanel {
				Orientation = Orientation.Horizontal,
				Children = {
						(_Border = new Border {
							Background = new SolidColorBrush(color),
							BorderThickness = WpfHelper.TinyMargin,
							Width = 16, Height = 16,
							Margin = WpfHelper.GlyphMargin
						}),
						new TextBlock { Text = text }
					}
			};
			Width = 120;
			Margin = WpfHelper.SmallMargin;
			_ColorChangedHandler = colorChangedHandler;
		}
		public Func<Color> DefaultColor { get; set; }
		public Color Color {
			get => (_Border.Background as SolidColorBrush).Color;
			set {
				if (Color != value) {
					_Border.Background = new SolidColorBrush(value);
					if (_IsColorChanging == false) {
						_IsColorChanging = true;
						try {
							_ColorChangedHandler?.Invoke(value);
						}
						finally {
							_IsColorChanging = false;
						}
					}
				}
			}
		}
		public Brush Brush => _Border.Background;

		public void UseVsTheme() {
			_Border.ReferenceProperty(Border.BorderBrushProperty, VsBrushes.CommandBarMenuIconBackgroundKey);
			this.ReferenceStyle(VsResourceKeys.ButtonStyleKey);
		}

		protected override void OnClick() {
			base.OnClick();
			if (ContextMenu == null) {
				ContextMenu = new ContextMenu {
					Resources = SharedDictionaryManager.ContextMenu,
					Items = {
							new ThemedMenuItem(IconIds.PickColor, R.CMD_PickColor, PickColor),
							new ThemedMenuItem(IconIds.Reset, R.CMD_ResetColor, ResetColor),
							new ThemedMenuItem(IconIds.Copy, R.CMD_CopyColor, CopyColor),
							new ThemedMenuItem(IconIds.Paste, R.CMD_PasteColor, PasteColor),
						},
					Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
					PlacementTarget = this
				};
				ContextMenu.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
			}
			ContextMenu.IsOpen = true;
		}

		void CopyColor(object sender, RoutedEventArgs e) {
			try {
				Clipboard.SetDataObject(Color.ToHexString());
			}
			catch (System.Runtime.InteropServices.ExternalException) {
				// ignore
			}
		}

		void PasteColor(object sender, RoutedEventArgs e) {
			Color = GetClipboardColor();
		}

		void ResetColor(object sender, RoutedEventArgs e) {
			Color = default;
		}

		void PickColor(object sender, RoutedEventArgs e) {
			using (var c = new System.Windows.Forms.ColorDialog() {
				FullOpen = true
			}) {
				if (Color.A != 0) {
					c.Color = Color.ToGdiColor();
				}
				else if (DefaultColor != null) {
					c.Color = DefaultColor().ToGdiColor();
				}
				if (c.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
					Color = c.Color.ToWpfColor();
				}
			}
		}

		static Color GetClipboardColor() {
			string c;
			try {
				c = Clipboard.GetText();
			}
			catch (System.Runtime.InteropServices.ExternalException) {
				return default;
			}
			UIHelper.ParseColor(c, out var color, out _);
			return color;
		}
	}
}
