using System;
using System.Windows;
using System.Windows.Controls;
using CLR;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Events;
using SysTasks = System.Threading.Tasks;

namespace Codist.Display
{
	static class LayoutOverride
	{
		static DockPanel __TitleBar, __MenuHolder;
		static InteractiveControlContainer __Account, __Menu;
		static StackPanel __TitleBarButtons;
		static Border __TitleBlock;
		static readonly string __RootSuffix = GetRootSuffix();
		static readonly string __DefaultTitle = "Visual Studio" + __RootSuffix;
		static int __Retrial;

		static string GetRootSuffix() {
			var args = Environment.GetCommandLineArgs();
			for (int i = 1; i < args.Length; i++) {
				if (String.Equals(args[i], "/rootsuffix", StringComparison.OrdinalIgnoreCase) && i+1 < args.Length) {
					return " | " + args[i + 1];
				}
			}
			return null;
		}

		public static bool ToggleUIElement(DisplayOptimizations element, bool show) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var w = Application.Current.MainWindow;
			var g = w.GetFirstVisualChild<Grid>(i => i.Name == "RootGrid");
			if (g is null || g.Children.Count < 2) {
				return false;
			}
			Predicate<FrameworkElement> controlMatcher;
			switch (element) {
				case DisplayOptimizations.HideSearchBox: controlMatcher = CodistPackage.VsVersion.Major == 15 ? ControlNameMatcher.PART__SearchBox.Match : (Predicate<FrameworkElement>)ControlNameMatcher.SearchBox.Match; break;
				case DisplayOptimizations.HideAccountBox: controlMatcher = ControlNameMatcher.IDCardGrid.Match; break;
				case DisplayOptimizations.HideFeedbackBox: controlMatcher = ControlAlternativeMatcher.FeedbackButton.Match; break;
				case DisplayOptimizations.HideCopilotButton: controlMatcher = ControlTypeMatcher.CopilotBadgeControl.Match; break;
				case DisplayOptimizations.HideInfoBadgeButton: controlMatcher = ControlTypeMatcher.InfoBadgeControl.Match; break;
				default: return false;
			}
			var t = g.GetFirstVisualChild(controlMatcher);
			if (t != null) {
				t = CodistPackage.VsVersion.Major == 15
					? t.GetParent<ContentPresenter>(i => System.Windows.Media.VisualTreeHelper.GetParent(i) is StackPanel)
					: t.GetParent<ContentPresenter>(i => i.Name == "DataTemplatePresenter");
			}
			if (t == null && element == DisplayOptimizations.HideSearchBox) {
				t = g.GetFirstVisualChild<UserControl>(ControlTypeMatcher.PackageAllInOneSearchButtonPresenter.Match)
					?.GetParent<ContentPresenter>(i => i.Name == "DataTemplatePresenter")
					?.GetParent<FrameworkElement>(i => i.Name == "PART_TitleBarLeftFrameControlContainer");
			}

			if (t == null) {
				return false;
			}
			t.ToggleVisibility(show);
			return true;
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

		public static void Initialize() {
			var options = Config.Instance.DisplayOptimizations;
			if (options == DisplayOptimizations.None) {
				return;
			}
			if (options.MatchFlags(DisplayOptimizations.CompactMenu)) {
				CompactMenu();
			}
			if (options.MatchFlags(DisplayOptimizations.MainWindow)) {
				WpfHelper.SetUITextRenderOptions(Application.Current.MainWindow, true);
			}

			if (options.HasAnyFlag(DisplayOptimizations.HideUIElements)) {
				InitHideElements(options);
			}
			nameof(LayoutOverride).LogInitialized();
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
			ToggleUIElement(DisplayOptimizations.HideCopilotButton, !options.MatchFlags(DisplayOptimizations.HideCopilotButton));
			ToggleUIElement(DisplayOptimizations.HideInfoBadgeButton, !options.MatchFlags(DisplayOptimizations.HideInfoBadgeButton));
			WpfHelper.SetUITextRenderOptions(Application.Current.MainWindow, options.MatchFlags(DisplayOptimizations.MainWindow));
		}

		static void InitHideElements(DisplayOptimizations options) {
			var done = true;
			if (options.MatchFlags(DisplayOptimizations.HideSearchBox)) {
				done &= ToggleUIElement(DisplayOptimizations.HideSearchBox, false);
			}
			if (options.MatchFlags(DisplayOptimizations.HideAccountBox)) {
				done &= ToggleUIElement(DisplayOptimizations.HideAccountBox, false);
			}
			if (options.MatchFlags(DisplayOptimizations.HideFeedbackBox)) {
				done &= ToggleUIElement(DisplayOptimizations.HideFeedbackBox, false);
			}
			if (options.MatchFlags(DisplayOptimizations.HideCopilotButton) && CodistPackage.VsVersion.Major > 16) {
				done &= ToggleUIElement(DisplayOptimizations.HideCopilotButton, false);
			}
			if (options.MatchFlags(DisplayOptimizations.HideInfoBadgeButton)) {
				done &= ToggleUIElement(DisplayOptimizations.HideInfoBadgeButton, false);
			}

			if (done == false && ++__Retrial < 10) {
				_ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => {
					"Retry UI Layout override".Log();
					await SysTasks.Task.Delay(3000);
					await SyncHelper.SwitchToMainThreadAsync();
					InitHideElements(Config.Instance.DisplayOptimizations);
				});
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

		readonly struct ControlNameMatcher
		{
			internal static readonly ControlNameMatcher PART__SearchBox = new ControlNameMatcher("PART__SearchBox");
			internal static readonly ControlNameMatcher SearchBox = new ControlNameMatcher("SearchBox");
			internal static readonly ControlNameMatcher IDCardGrid = new ControlNameMatcher("IDCardGrid");
			internal static readonly ControlNameMatcher FeedbackButton = new ControlNameMatcher("FeedbackButton");

			readonly string _Name;
			ControlNameMatcher(string name) {
				_Name = name;
			}
			public bool Match(FrameworkElement control) {
				return control.Name == _Name;
			}
		}
		readonly struct ControlAlternativeMatcher
		{
			internal static readonly ControlAlternativeMatcher FeedbackButton = new ControlAlternativeMatcher(ControlTypeMatcher.Feedback.Match, ControlNameMatcher.FeedbackButton.Match);

			readonly Predicate<FrameworkElement> _PrimaryCondition, _AlternativeCondition;
			ControlAlternativeMatcher(Predicate<FrameworkElement> condition1, Predicate<FrameworkElement> condition2) {
				_PrimaryCondition = condition1;
				_AlternativeCondition = condition2;
			}
			public bool Match(FrameworkElement control) {
				return _PrimaryCondition(control) || _AlternativeCondition(control);
			}
		}
		readonly struct ControlTypeMatcher
		{
			internal static readonly ControlTypeMatcher PackageAllInOneSearchButtonPresenter = new ControlTypeMatcher("PackageAllInOneSearchButtonPresenter");
			internal static readonly ControlTypeMatcher CopilotBadgeControl = new ControlTypeMatcher("CopilotBadgeControl");
			internal static readonly ControlTypeMatcher Feedback = new ControlTypeMatcher("SendASmileControl");
			internal static readonly ControlTypeMatcher InfoBadgeControl = new ControlTypeMatcher("InfoBadgeControl");

			readonly string _Name;
			ControlTypeMatcher(string name) {
				_Name = name;
			}
			public bool Match(FrameworkElement control) {
				return control.GetType().Name == _Name;
			}
		}
	}
}
