using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;

namespace Codist.Controls
{
	static class LayoutOverrider
	{
		static DockPanel _TitleBar, _MenuHolder;
		static InteractiveControlContainer _Account, _Menu;
		static StackPanel _TitleBarButtons;

		public static void CompactMenu() {
			var w = Application.Current.MainWindow;
			var g = w.GetFirstVisualChild<Grid>(i => i.Name == "RootGrid");
			if (g == null || g.Children.Count < 2) {
				return;
			}
			var t = g.GetFirstVisualChild<Border>(i => i.Name == "MainWindowTitleBar")?.Child as DockPanel;
			if (t == null) {
				return;
			}

			var menuHolder = g.Children[1] as DockPanel;
			if (menuHolder == null) {
				return;
			}
			var account = menuHolder.Children[0] as ItemsControl;
			if (account is null) {
				return;
			}
			var menu = menuHolder.Children[1] as ContentPresenter;
			if (menu is null) {
				return;
			}

			_TitleBar = t;
			_MenuHolder = menuHolder;
			_MenuHolder.Visibility = Visibility.Collapsed;

			menuHolder.Children.RemoveAt(1);
			menuHolder.Children.RemoveAt(0);

			t.Children.Insert(2, _Menu = new InteractiveControlContainer(menu) { Margin = new Thickness(0, 7, 5, 5) });

			(_TitleBarButtons = t.GetFirstVisualChild<StackPanel>(i => i.Name == "WindowTitleBarButtons"))
				.Children.Insert(0, _Account = new InteractiveControlContainer(account));
		}

		public static void UndoCompactMenu() {
			if (_MenuHolder == null) {
				return;
			}
			_TitleBar.Children.Remove(_Menu);
			_TitleBar.GetFirstVisualChild<StackPanel>(i => i.Name == "WindowTitleBarButtons");
			_TitleBarButtons.Children.Remove(_Account);
			var menu = _Menu.Content as UIElement;
			_Menu.Content = null;
			var account = _Account.Content as UIElement;
			_Account.Content = null;
			_MenuHolder.Children.Insert(0, account);
			DockPanel.SetDock(account, Dock.Right);
			_MenuHolder.Children.Insert(1, menu);
			_MenuHolder.Visibility = Visibility.Visible;
			_Menu = _Account = null;
			_MenuHolder = null;
		}

		sealed class InteractiveControlContainer : ContentControl, INonClientArea
		{
			int INonClientArea.HitTest(Point point) {
				return 1;
			}
			public InteractiveControlContainer(UIElement content) {
				Content = content;
			}
		}
	}
}
