using System;
using System.Windows;
using System.Windows.Controls;
using AppHelpers;

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

		public TextBlock Title { get; private set; }
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
			_CheckEventHandler += (o, v) => Config.Instance.FireConfigChangedEvent(updateFeature);
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
}
