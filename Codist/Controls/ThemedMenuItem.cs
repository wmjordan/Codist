using System;
using System.Collections.Specialized;
using System.Linq;
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
					Items.Add(new SubMenuPlaceHolder());
				}
			}
		}

		public double SubMenuMaxHeight {
			get => (double)GetValue(SubMenuMaxHeightProperty);
			set => SetValue(SubMenuMaxHeightProperty, value);
		}

		protected bool HasExplicitItems => HasItems && Items.Count > 1;

		public void ClearItems() {
			if (_SubMenuHeader == null) {
				Items.Clear();
				return;
			}
			for (int i = Items.Count - 1; i >= 0; i--) {
				if (Items[i] is SubMenuPlaceHolder) {
					continue;
				}
				Items.RemoveAt(i);
			}
		}

		public void Filter(string[] keywords) {
			if (keywords.Length == 0) {
				foreach (UIElement item in Items) {
					item.Visibility = item is SubMenuPlaceHolder ? Visibility.Collapsed : Visibility.Visible;
				}
				return;
			}
			foreach (UIElement item in Items) {
				var menuItem = item as MenuItem;
				if (menuItem == null) {
					item.Visibility = Visibility.Collapsed;
					continue;
				}
				var b = menuItem.Header as TextBlock;
				if (b == null) {
					continue;
				}
				if (FilterSignature(b.GetText(), keywords)) {
					menuItem.Visibility = Visibility.Visible;
					if (menuItem.HasItems) {
						foreach (MenuItem sub in menuItem.Items) {
							sub.Visibility = Visibility.Visible;
						}
					}
					continue;
				}
				var matchedSubItem = false;
				if (menuItem.HasItems) {
					foreach (MenuItem sub in menuItem.Items) {
						b = sub.Header as TextBlock;
						if (b == null) {
							continue;
						}
						if (FilterSignature(b.GetText(), keywords)) {
							matchedSubItem = true;
							sub.Visibility = Visibility.Visible;
						}
						else {
							sub.Visibility = Visibility.Collapsed;
						}
					}
				}
				menuItem.Visibility = matchedSubItem ? Visibility.Visible : Visibility.Collapsed;
			}

			bool FilterSignature(string text, string[] words) {
				return words.All(p => text.IndexOf(p, StringComparison.OrdinalIgnoreCase) != -1);
			}
		}

		sealed class SubMenuPlaceHolder : Separator
		{
			public SubMenuPlaceHolder() {
				Visibility = Visibility.Collapsed;
			}
		}
	}
}
