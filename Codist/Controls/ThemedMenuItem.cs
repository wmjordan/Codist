using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Codist.Controls
{
	public class ThemedMenuItem : MenuItem
	{
		public static readonly DependencyProperty SubMenuHeaderProperty = DependencyProperty.Register("SubMenuHeader", typeof(object), typeof(ThemedMenuItem));
		public static readonly DependencyProperty SubMenuMaxHeightProperty = DependencyProperty.Register("SubMenuMaxHeight", typeof(double), typeof(ThemedMenuItem));

		object _SubMenuHeader;

		public ThemedMenuItem() {
			SubMenuMaxHeight = 300;
			this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
		}

		/// <summary>Gets or sets the header of the pop up submenu. If the header is set and no sub items are in the menu, an invisible <see cref="Separator"/> will be added to make the menu possible to popup when it is clicked.</summary>
		public object SubMenuHeader {
			get => _SubMenuHeader;
			set {
				SetValue(SubMenuHeaderProperty, _SubMenuHeader = value);
				if (_SubMenuHeader != null && HasItems == false) {
					Items.Add(new Separator { Visibility = Visibility.Collapsed });
				}
			}
		}

		public double SubMenuMaxHeight {
			get => (double)GetValue(SubMenuMaxHeightProperty);
			set => SetValue(SubMenuMaxHeightProperty, value);
		}

		public void ClearItems() {
			if (_SubMenuHeader == null) {
				Items.Clear();
			}
			else {
				for (int i = Items.Count - 1; i > 0; i--) {
					Items.RemoveAt(i);
				}
			}
		}
	}
}
