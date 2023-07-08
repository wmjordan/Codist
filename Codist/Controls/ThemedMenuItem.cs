using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Codist.Controls
{
	public class ThemedMenuItem : MenuItem, IDisposable
	{
		public static readonly DependencyProperty SubMenuHeaderProperty = DependencyProperty.Register("SubMenuHeader", typeof(FrameworkElement), typeof(ThemedMenuItem));
		public static readonly DependencyProperty SubMenuMaxHeightProperty = DependencyProperty.Register("SubMenuMaxHeight", typeof(double), typeof(ThemedMenuItem));

		RoutedEventHandler _ClickHandler;
		FrameworkElement _SubMenuHeader;

		public ThemedMenuItem() {
			SubMenuMaxHeight = 300;
			this.ReferenceCrispImageBackground(Microsoft.VisualStudio.PlatformUI.EnvironmentColors.SystemMenuColorKey);
		}
		public ThemedMenuItem(int imageId, string text, RoutedEventHandler clickHandler) : this() {
			if (imageId >= 0) {
				Icon = ThemeHelper.GetImage(imageId);
			}
			Header = new ThemedMenuText(text);
			_ClickHandler = clickHandler;
			Click += _ClickHandler;
		}

		/// <summary>Gets or sets the header of the pop up submenu. If the header is set and no sub items are in the menu, an invisible <see cref="Separator"/> will be added to make the menu possible to popup when it is clicked.</summary>
		public FrameworkElement SubMenuHeader {
			get => _SubMenuHeader;
			set {
				if (value != null) {
					if (HasItems == false) {
						Items.Add(new MenuItemPlaceHolder());
					}
					value.KeyUp += HeaderKeyUp;
				}
				var h = _SubMenuHeader;
				if (_SubMenuHeader != h && h != null) {
					h.KeyUp -= HeaderKeyUp;
				}
				SetValue(SubMenuHeaderProperty, _SubMenuHeader = value);
			}
		}

		public double SubMenuMaxHeight {
			get => (double)GetValue(SubMenuMaxHeightProperty);
			set => SetValue(SubMenuMaxHeightProperty, value);
		}

		protected bool HasExplicitItems => HasItems && Items.Count > 1;

		public void ClearItems() {
			for (int i = Items.Count - 1; i >= 0; i--) {
				if (Items[i] is MenuItemPlaceHolder) {
					continue;
				}
				Items.RemoveAndDisposeAt(i);
			}
		}

		public void PerformClick() {
			OnClick();
		}

		internal void Highlight(bool highlight) {
			IsHighlighted = highlight;
		}

		void HeaderKeyUp(object sender, KeyEventArgs args) {
			switch (args.Key) {
				case Key.Enter:
					if (args.OriginalSource is TextBox) {
						Items.GetFirst<ThemedMenuItem>(i => i.IsEnabled && i.IsVisible)?.PerformClick();
					}
					break;
				case Key.Down: Items.FocusFirst<MenuItem>(); break;
				case Key.Up: Items.FocusLast<MenuItem>(); break;
			}
		}

		public virtual void Dispose() {
			if (_ClickHandler != null) {
				Click -= _ClickHandler;
				_ClickHandler = null;
			}
			this.DisposeCollection();
			SubMenuHeader = null;
			DataContext = null;
		}

		internal sealed class MenuItemPlaceHolder : Separator
		{
			public MenuItemPlaceHolder() {
				Visibility = Visibility.Collapsed;
			}
		}
	}
}
