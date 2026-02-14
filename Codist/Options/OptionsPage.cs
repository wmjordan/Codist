using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;

namespace Codist.Options
{
	static class OptionPageControlHelper
	{
		public static OptionBox<TOption> CreateOptionBox<TOption> (this TOption initialValue, TOption option, Action<TOption, bool> checkEventHandler, string title) where TOption : struct, Enum {
			return new OptionBox<TOption>(initialValue, option, checkEventHandler) {
				Content = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap }
			}.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey);
		}
		public static OptionBox CreateOptionBox(bool initialValue, Action<bool?> checkEventHandler, string title) {
			return new OptionBox(initialValue, checkEventHandler) {
				Content = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap }
			}.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey);
		}

		public static void ApplyMargin(this Thickness margin, params FrameworkElement[] elements) {
			foreach (var item in elements) {
				item.Margin = margin;
			}
		}
		public static void BindDependentOptionControls(this System.Windows.Controls.Primitives.ToggleButton checkBox, params UIElement[] dependentControls) {
			var enabled = checkBox.IsChecked == true;
			foreach (var item in dependentControls) {
				item.IsEnabled = enabled;
			}
			checkBox.Checked += (s, args) => Array.ForEach(dependentControls, c => c.IsEnabled = true);
			checkBox.Unchecked += (s, args) => Array.ForEach(dependentControls, c => c.IsEnabled = false);
		}
	}
}
