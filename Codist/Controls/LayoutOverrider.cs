using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Events;
using Microsoft.VisualStudio.PlatformUI;
using AppHelpers;

namespace Codist.Controls
{
	static class LayoutOverrider
	{
		static DockPanel _TitleBar, _MenuHolder;
		static InteractiveControlContainer _Account, _Menu;
		static StackPanel _TitleBarButtons;
		static Border _TitleBlock;
		static readonly string _RootSuffix = GetRootSuffix();
		static readonly string __DefaultTitle = "Visual Studio" + _RootSuffix;
		static bool _LayoutElementNotFound;

		static string GetRootSuffix() {
			var args = Environment.GetCommandLineArgs();
			for (int i = 1; i < args.Length; i++) {
				if (String.Equals(args[i], "/rootsuffix", StringComparison.OrdinalIgnoreCase) && i+1 < args.Length) {
					return " | " + args[i + 1];
				}
			}
			return null;
		}

		public static void ToggleUIElement(DisplayOptimizations element, bool show) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var w = Application.Current.MainWindow;
			var g = w.GetFirstVisualChild<Grid>(i => i.Name == "RootGrid");
			if (g is null || g.Children.Count < 2) {
				return;
			}
			string boxName;
			switch (element) {
				case DisplayOptimizations.HideSearchBox: boxName = CodistPackage.VsVersion.Major == 15 ? "PART_SearchBox" : "SearchBox"; break;
				case DisplayOptimizations.HideAccountBox: boxName = "IDCardGrid"; break;
				case DisplayOptimizations.HideFeedbackBox: boxName = "FeedbackButton"; break;
				default: return;
			}
			var t = CodistPackage.VsVersion.Major == 15
				? g.GetFirstVisualChild<FrameworkElement>(i => i.Name == boxName)
					?.GetParent<ContentPresenter>(i => System.Windows.Media.VisualTreeHelper.GetParent(i) is StackPanel)
				: g.GetFirstVisualChild<FrameworkElement>(i => i.Name == boxName)
					?.GetParent<ContentPresenter>(i => i.Name == "DataTemplatePresenter");
			if (t != null) {
				t.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
			}
			else {
				_LayoutElementNotFound = true;
			}
		}

		public static void CompactMenu() {
			if (_MenuHolder != null) {
				return;
			}
			ThreadHelper.ThrowIfNotOnUIThread();
			var w = Application.Current.MainWindow;
			var g = w.GetFirstVisualChild<Grid>(i => i.Name == "RootGrid");
			if (g is null || g.Children.Count < 2) {
				return;
			}
			var t = g.GetFirstVisualChild<Border>(i => i.Name == "MainWindowTitleBar")?.Child as DockPanel;
			if (t is null) {
				return;
			}

			var menuHolder = g.Children[1] as DockPanel;
			if (menuHolder is null || menuHolder.Children.Count < 2) {
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
			if (t.Children[3] is TextBlock vsTitle) {
				vsTitle.Visibility = Visibility.Collapsed;
			}
			_TitleBar = t;
			_MenuHolder = menuHolder;
			menuHolder.Visibility = Visibility.Collapsed;

			menuHolder.Children.RemoveAt(1);
			menuHolder.Children.RemoveAt(0);

			t.Children.Insert(2, _Menu = new InteractiveControlContainer(menu) { Margin = new Thickness(0, 7, 5, 4) });

			var title = new TextBlock { FontWeight = FontWeights.Bold };
			title.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.SystemCaptionTextBrushKey);
			var sn = System.IO.Path.GetFileNameWithoutExtension(CodistPackage.DTE.Solution.FileName);
			title.Text = sn.Length > 0 ? sn + _RootSuffix : __DefaultTitle;
			t.Children.Insert(3, _TitleBlock = new Border { Child = title, Padding = new Thickness(7, 9, 7, 4) });
			_TitleBlock.SetResourceReference(Border.BackgroundProperty, EnvironmentColors.SystemActiveCaptionBrushKey);

			(_TitleBarButtons = t.GetFirstVisualChild<StackPanel>(i => i.Name == "WindowTitleBarButtons"))
				.Children.Insert(0, _Account = new InteractiveControlContainer(account));

			SolutionEvents.OnBeforeOpenSolution += BeforeOpenSolution;
			SolutionEvents.OnAfterCloseSolution += AfterCloseSolution;
			Application.Current.MainWindow.Activated += MainWindowActivated;
			Application.Current.MainWindow.Deactivated += MainWindowDeactivated;
		}

		public static void UndoCompactMenu() {
			if (_MenuHolder is null) {
				return;
			}
			ThreadHelper.ThrowIfNotOnUIThread();
			_TitleBar.Children.Remove(_Menu);
			_TitleBar.Children.Remove(_TitleBlock);
			_TitleBarButtons.Children.Remove(_Account);

			var menu = _Menu.Content as UIElement;
			_Menu.Content = null;
			var account = _Account.Content as UIElement;
			_Account.Content = null;

			_TitleBlock.Child = null;

			_MenuHolder.Children.Insert(0, account);
			DockPanel.SetDock(account, Dock.Right);
			_MenuHolder.Children.Insert(1, menu);
			_MenuHolder.Visibility = Visibility.Visible;

			if (_TitleBar.Children[3] is TextBlock vsTitle) {
				vsTitle.Visibility = Visibility.Visible;
			}

			SolutionEvents.OnBeforeOpenSolution -= BeforeOpenSolution;
			SolutionEvents.OnAfterCloseSolution -= AfterCloseSolution;
			Application.Current.MainWindow.Activated -= MainWindowActivated;
			Application.Current.MainWindow.Deactivated -= MainWindowDeactivated;

			_Menu = _Account = null;
			_MenuHolder = null;
			_TitleBlock = null;
		}

		public static void InitializeLayoutOverride() {
			var options = Config.Instance.DisplayOptimizations;
			if (options == DisplayOptimizations.None) {
				return;
			}
			if (options.MatchFlags(DisplayOptimizations.CompactMenu)) {
				CompactMenu();
			}
			InitHideElements(options);
			if (options.MatchFlags(DisplayOptimizations.MainWindow)) {
				WpfHelper.SetUITextRenderOptions(Application.Current.MainWindow, true);
			}
			if (_LayoutElementNotFound) {
				// hack: the UI elements to hide may not be added to app window when this method is executed
				//    the solution load event is exploited to compensate that
				SolutionEvents.OnAfterBackgroundSolutionLoadComplete += OverrideLayoutAfterSolutionLoad;
			}
		}

		public static void Reload(DisplayOptimizations options) {
			if (options.MatchFlags(DisplayOptimizations.CompactMenu)) {
				CompactMenu();
			}
			else {
				UndoCompactMenu();
			}
			ToggleUIElement(DisplayOptimizations.HideSearchBox, !options.MatchFlags(DisplayOptimizations.HideSearchBox));
			ToggleUIElement(DisplayOptimizations.HideAccountBox, !options.MatchFlags(DisplayOptimizations.HideAccountBox));
			ToggleUIElement(DisplayOptimizations.HideFeedbackBox, !options.MatchFlags(DisplayOptimizations.HideFeedbackBox));
			WpfHelper.SetUITextRenderOptions(Application.Current.MainWindow, options.MatchFlags(DisplayOptimizations.MainWindow));
		}

		static void OverrideLayoutAfterSolutionLoad(object sender, EventArgs e) {
			SolutionEvents.OnAfterBackgroundSolutionLoadComplete -= OverrideLayoutAfterSolutionLoad;
			InitHideElements(Config.Instance.DisplayOptimizations);
		}

		static void InitHideElements(DisplayOptimizations options) {
			if (options.MatchFlags(DisplayOptimizations.HideSearchBox)) {
				ToggleUIElement(DisplayOptimizations.HideSearchBox, false);
			}
			if (options.MatchFlags(DisplayOptimizations.HideAccountBox)) {
				ToggleUIElement(DisplayOptimizations.HideAccountBox, false);
			}
			if (options.MatchFlags(DisplayOptimizations.HideFeedbackBox)) {
				ToggleUIElement(DisplayOptimizations.HideFeedbackBox, false);
			}
		}

		static void BeforeOpenSolution(object sender, BeforeOpenSolutionEventArgs args) {
			if (_TitleBlock?.Child is TextBlock t) {
				t.Text = System.IO.Path.GetFileNameWithoutExtension(args.SolutionFilename) + _RootSuffix;
			}
		}
		static void AfterCloseSolution (object sender, EventArgs args) {
			if (_TitleBlock?.Child is TextBlock t) {
				t.Text = __DefaultTitle;
			}
		}
		static void MainWindowActivated(object sender, EventArgs args) {
			(_TitleBlock?.Child as TextBlock).SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.SystemCaptionTextBrushKey);
		}
		static void MainWindowDeactivated(object sender, EventArgs args) {
			(_TitleBlock?.Child as TextBlock).SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.SystemInactiveCaptionTextBrushKey);
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
