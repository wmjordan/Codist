using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Events;
using Microsoft.VisualStudio.PlatformUI;
using AppHelpers;

namespace Codist.Display
{
	static class LayoutOverrider
	{
		static DockPanel __TitleBar, __MenuHolder;
		static InteractiveControlContainer __Account, __Menu;
		static StackPanel __TitleBarButtons;
		static Border __TitleBlock;
		static readonly string __RootSuffix = GetRootSuffix();
		static readonly string __DefaultTitle = "Visual Studio" + __RootSuffix;
		static bool __LayoutElementNotFound;

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
				case DisplayOptimizations.HideSearchBox: boxName = CodistPackage.VsVersion.Major == 15 ? "PART__SearchBox" : "SearchBox"; break;
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
				__LayoutElementNotFound = true;
			}
		}

		public static void CompactMenu() {
			if (__MenuHolder != null) {
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
			__TitleBar = t;
			__MenuHolder = menuHolder;
			menuHolder.Visibility = Visibility.Collapsed;

			menuHolder.Children.RemoveAt(1);
			menuHolder.Children.RemoveAt(0);

			t.Children.Insert(2, __Menu = new InteractiveControlContainer(menu) { Margin = new Thickness(0, 7, 5, 4) });

			var sn = System.IO.Path.GetFileNameWithoutExtension(CodistPackage.DTE.Solution.FileName);
			var title = new TextBlock {
				FontWeight = FontWeights.Bold,
				Text = sn.Length > 0 ? sn + __RootSuffix : __DefaultTitle
			}.ReferenceProperty(TextBlock.ForegroundProperty, EnvironmentColors.SystemCaptionTextBrushKey);
			t.Children.Insert(3, __TitleBlock = new Border {
					Child = title,
					Padding = new Thickness(7, 9, 7, 4)
				}.ReferenceProperty(Border.BackgroundProperty, EnvironmentColors.SystemActiveCaptionBrushKey));

			(__TitleBarButtons = t.GetFirstVisualChild<StackPanel>(i => i.Name == "WindowTitleBarButtons"))
				.Children.Insert(0, __Account = new InteractiveControlContainer(account));

			SolutionEvents.OnBeforeOpenSolution += BeforeOpenSolution;
			SolutionEvents.OnAfterCloseSolution += AfterCloseSolution;
			Application.Current.MainWindow.Activated += MainWindowActivated;
			Application.Current.MainWindow.Deactivated += MainWindowDeactivated;
		}

		public static void UndoCompactMenu() {
			if (__MenuHolder is null) {
				return;
			}
			ThreadHelper.ThrowIfNotOnUIThread();
			__TitleBar.Children.Remove(__Menu);
			__TitleBar.Children.Remove(__TitleBlock);
			__TitleBarButtons.Children.Remove(__Account);

			var menu = __Menu.Content as UIElement;
			__Menu.Content = null;
			var account = __Account.Content as UIElement;
			__Account.Content = null;

			__TitleBlock.Child = null;

			__MenuHolder.Children.Insert(0, account);
			DockPanel.SetDock(account, Dock.Right);
			__MenuHolder.Children.Insert(1, menu);
			__MenuHolder.Visibility = Visibility.Visible;

			if (__TitleBar.Children[3] is TextBlock vsTitle) {
				vsTitle.Visibility = Visibility.Visible;
			}

			SolutionEvents.OnBeforeOpenSolution -= BeforeOpenSolution;
			SolutionEvents.OnAfterCloseSolution -= AfterCloseSolution;
			Application.Current.MainWindow.Activated -= MainWindowActivated;
			Application.Current.MainWindow.Deactivated -= MainWindowDeactivated;

			__Menu = __Account = null;
			__MenuHolder = null;
			__TitleBlock = null;
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
			if (__LayoutElementNotFound && options.HasAnyFlag(DisplayOptimizations.HideSearchBox | DisplayOptimizations.HideFeedbackBox | DisplayOptimizations.HideAccountBox)) {
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
			if (__TitleBlock?.Child is TextBlock t) {
				t.Text = System.IO.Path.GetFileNameWithoutExtension(args.SolutionFilename) + __RootSuffix;
			}
		}
		static void AfterCloseSolution (object sender, EventArgs args) {
			if (__TitleBlock?.Child is TextBlock t) {
				t.Text = __DefaultTitle;
			}
		}
		static void MainWindowActivated(object sender, EventArgs args) {
			(__TitleBlock?.Child as TextBlock).SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.SystemCaptionTextBrushKey);
		}
		static void MainWindowDeactivated(object sender, EventArgs args) {
			(__TitleBlock?.Child as TextBlock).SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.SystemInactiveCaptionTextBrushKey);
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
