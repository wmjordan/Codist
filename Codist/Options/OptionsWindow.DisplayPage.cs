using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using R = Codist.Properties.Resources;

namespace Codist.Options
{
	sealed partial class OptionsWindow
	{
		sealed class DisplayPage : OptionPageFactory
		{
			public override string Name => R.OT_Display;
			public override Features RequiredFeature => Features.None;

			protected override OptionPage CreatePage() {
				return new PageControl();
			}

			sealed class PageControl : OptionPage
			{
				readonly Controls.IntegerBox _TopSpace, _BottomSpace;
				readonly OptionBox<DisplayOptimizations> _MainWindow, _CodeWindow, _MenuLayoutOverride, _HideSearchBox, _HideAccountBox, _HideFeedbackButton, _HideInfoBadgeButton, _CpuMonitor, _MemoryMonitor, _DriveMonitor, _NetworkMonitor;
				readonly OptionBox<BuildOptions> _BuildTimestamp, _ShowOutputWindowAfterBuild;
				readonly TextBox _TaskManagerPath, _TaskManagerParameter;
				readonly Button _BrowseTaskManagerPath;

				public PageControl() {
					var o = Config.Instance.SpecialHighlightOptions;
					SetContents(new TitleBox(R.OT_ExtraLineMargin),
						new DescriptionBox(R.OT_ExtraLineMarginNote),
						new WrapPanel {
							Children = {
								new StackPanel().MakeHorizontal()
									.Add(new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append(R.OTC_TopMargin))
									.Add(new Controls.IntegerBox((int)Config.Instance.TopSpace) { Minimum = 0, Maximum = 255 }.Set(ref _TopSpace))
									.SetLazyToolTip(() => R.OT_TopMarginTip),
								new StackPanel().MakeHorizontal()
									.Add(new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append(R.OTC_BottomMargin))
									.Add(new Controls.IntegerBox((int)Config.Instance.BottomSpace) { Minimum = 0, Maximum = 255 }.Set(ref _BottomSpace))
									.SetLazyToolTip(() => R.OT_BottomMarginTip),
							}
						}.ForEachChild((FrameworkElement b) => b.MinWidth = MinColumnWidth),
						OptionPageControlHelper.CreateOptionBox(Config.Instance.NoSpaceBetweenWrappedLines, v => { Config.Instance.NoSpaceBetweenWrappedLines = v == true; Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight); }, R.OT_NoMarginBetweenWrappedLines),

						new TitleBox(R.OT_ForceGrayscaleTextRendering),
						new DescriptionBox(R.OT_ForceGrayscaleTextRenderingNote),
						new WrapPanel {
							Children = {
								Config.Instance.DisplayOptimizations.CreateOptionBox(DisplayOptimizations.MainWindow, UpdateMainWindowDisplayOption, R.OT_ApplyToMainWindow).Set(ref _MainWindow),
								Config.Instance.DisplayOptimizations.CreateOptionBox(DisplayOptimizations.CodeWindow, UpdateCodeWindowDisplayOption, R.OT_ApplyToCodeWindow).Set(ref _CodeWindow)
							}
						}
						.ForEachChild((CheckBox b) => b.MinWidth = MinColumnWidth)
						.SetLazyToolTip(() => R.OT_ForceGrayscaleTextRenderingTip),
						new TextBox { TextWrapping = TextWrapping.Wrap, Text = R.OT_MacTypeLink, Padding = WpfHelper.SmallMargin, IsReadOnly = true },

						new TitleBox(R.OT_ResourceMonitor),
						new DescriptionBox(R.OT_ResourceMonitorNote),
						new WrapPanel {
							Children = {
								Config.Instance.DisplayOptimizations.CreateOptionBox(DisplayOptimizations.ShowCpu, UpdateResourceManagerOption, R.OT_CpuUsage).Set(ref _CpuMonitor),
								Config.Instance.DisplayOptimizations.CreateOptionBox(DisplayOptimizations.ShowDrive, UpdateResourceManagerOption, R.OT_DriveUsage).Set(ref _DriveMonitor),
								Config.Instance.DisplayOptimizations.CreateOptionBox(DisplayOptimizations.ShowMemory, UpdateResourceManagerOption, R.OT_MemoryUsage).Set(ref _MemoryMonitor),
								Config.Instance.DisplayOptimizations.CreateOptionBox(DisplayOptimizations.ShowNetwork, UpdateResourceManagerOption, R.OT_NetworkUsage).Set(ref _NetworkMonitor)
							}
						},
						new Note(R.OT_TaskManagerNote),
						new Grid {
							ColumnDefinitions = {
								new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), },
								new ColumnDefinition { Width = new GridLength(100, GridUnitType.Pixel) }
							},
							Children = {
								(_TaskManagerPath = new TextBox { Margin = WpfHelper.SmallHorizontalMargin, Text = Config.Instance.TaskManagerPath })
									.SetValue(Grid.SetColumn, 0),
								(_BrowseTaskManagerPath = new Button { Content = R.CMD_Browse, Margin = WpfHelper.SmallHorizontalMargin })
									.SetValue(Grid.SetColumn, 1)
							}
						},
						new Note(R.OT_TaskManagerParameter),
						_TaskManagerParameter = new TextBox { Margin = WpfHelper.SmallHorizontalMargin, Text = Config.Instance.TaskManagerParameter },

						new TitleBox(R.OT_Output),
						new DescriptionBox(R.OT_OutputNote),
						new WrapPanel {
							Children = {
								Config.Instance.BuildOptions.CreateOptionBox(BuildOptions.BuildTimestamp, UpdateConfig, R.OT_BuildTimestamp).Set(ref _BuildTimestamp).SetLazyToolTip(() => R.OT_BuildTimestampTip),
								Config.Instance.BuildOptions.CreateOptionBox(BuildOptions.ShowOutputPaneAfterBuild, UpdateConfig, R.OT_ShowOutputPaneAfterBuild).Set(ref _ShowOutputWindowAfterBuild)
							}
						}
						.ForEachChild((CheckBox b) => b.MinWidth = MinColumnWidth),

						new TitleBox(R.OT_LayoutOverride),
						new DescriptionBox(R.OT_LayoutOverrideNote),
						new WrapPanel {
							Children = {
								Config.Instance.DisplayOptimizations.CreateOptionBox(DisplayOptimizations.CompactMenu, UpdateMenuLayoutOption, R.OT_OverrideMainMenu).Set(ref _MenuLayoutOverride),
								Config.Instance.DisplayOptimizations.CreateOptionBox(DisplayOptimizations.HideSearchBox, UpdateHideSearchBoxOption, R.OT_HideSearchBox).Set(ref _HideSearchBox),
								Config.Instance.DisplayOptimizations.CreateOptionBox(DisplayOptimizations.HideAccountBox, UpdateHideAccountBoxOption, R.OT_HideAccountIcon).Set(ref _HideAccountBox),
								Config.Instance.DisplayOptimizations.CreateOptionBox(DisplayOptimizations.HideFeedbackBox, UpdateHideFeedbackButtonOption, R.OT_HideFeedbackButton).Set(ref _HideFeedbackButton),
								Config.Instance.DisplayOptimizations.CreateOptionBox(DisplayOptimizations.HideInfoBadgeButton, UpdateHideInfoBadgeButtonOption, R.OT_HideInfoBadgeButton).Set(ref _HideInfoBadgeButton),
							}
						}
						.ForEachChild((CheckBox b) => b.MinWidth = MinColumnWidth)
					);

					_TopSpace.ValueChanged += _TopSpace_ValueChanged;
					_BottomSpace.ValueChanged += _BottomSpace_ValueChanged;
					_TaskManagerPath.TextChanged += _TaskManagerPath_TextChanged;
					_TaskManagerParameter.TextChanged += _TaskManagerParameter_TextChanged;
					_BrowseTaskManagerPath.Click += (s, args) => {
						var d = new OpenFileDialog {
							Title = R.OT_LocateTaskManager,
							CheckFileExists = true,
							AddExtension = true,
							Filter = R.F_Executable
						};
						if (d.ShowDialog() == true) {
							_TaskManagerPath.Text = d.FileName;
						}
					};

					_MenuLayoutOverride.IsEnabled = CodistPackage.VsVersion.Major == 15;
				}

				protected override void LoadConfig(Config config) {
					_TopSpace.Value = (int)config.TopSpace;
					_BottomSpace.Value = (int)config.BottomSpace;
					var o = config.DisplayOptimizations;
					_MainWindow.UpdateWithOption(o);
					_CodeWindow.UpdateWithOption(o);
					_HideAccountBox.UpdateWithOption(o);
					_HideFeedbackButton.UpdateWithOption(o);
					_HideSearchBox.UpdateWithOption(o);
					_HideInfoBadgeButton.UpdateWithOption(o);
					_BuildTimestamp.UpdateWithOption(config.BuildOptions);
					_ShowOutputWindowAfterBuild.UpdateWithOption(config.BuildOptions);
				}

				void UpdateCodeWindowDisplayOption(DisplayOptimizations options, bool value) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, value);
					WpfHelper.SetUITextRenderOptions(TextEditorHelper.GetActiveWpfDocumentView()?.VisualElement, value);
					Config.Instance.FireConfigChangedEvent(Features.None);
				}

				void UpdateMainWindowDisplayOption(DisplayOptimizations options, bool value) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, value);
					WpfHelper.SetUITextRenderOptions(Application.Current.MainWindow, value);
					Config.Instance.FireConfigChangedEvent(Features.None);
				}

				void UpdateResourceManagerOption(DisplayOptimizations options, bool value) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, value);
					Display.ResourceMonitor.Reload(Config.Instance.DisplayOptimizations);
					Config.Instance.FireConfigChangedEvent(Features.None);
				}

				void UpdateConfig(BuildOptions options, bool set) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, set);
					Config.Instance.FireConfigChangedEvent(Features.None);
				}

				void UpdateMenuLayoutOption(DisplayOptimizations options, bool value) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, value);
					if (value) {
						Display.LayoutOverride.CompactMenu();
					}
					else {
						Display.LayoutOverride.UndoCompactMenu();
					}
					Config.Instance.FireConfigChangedEvent(Features.None);
				}

				void UpdateHideSearchBoxOption(DisplayOptimizations options, bool value) {
					ToggleTitleBarElement(options, value, DisplayOptimizations.HideSearchBox);
				}

				void UpdateHideAccountBoxOption(DisplayOptimizations options, bool value) {
					ToggleTitleBarElement(options, value, DisplayOptimizations.HideAccountBox);
				}

				void UpdateHideFeedbackButtonOption(DisplayOptimizations options, bool value) {
					ToggleTitleBarElement(options, value, DisplayOptimizations.HideFeedbackBox);
				}

				void UpdateHideInfoBadgeButtonOption(DisplayOptimizations options, bool value) {
					ToggleTitleBarElement(options, value, DisplayOptimizations.HideInfoBadgeButton);
				}

				void ToggleTitleBarElement(DisplayOptimizations options, bool value, DisplayOptimizations element) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, value);
					Display.LayoutOverride.ToggleUIElement(element, !value);
					Config.Instance.FireConfigChangedEvent(Features.None);
				}

				void _BottomSpace_ValueChanged(object sender, DependencyPropertyChangedEventArgs e) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.BottomSpace = (int)e.NewValue;
					Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
				}

				void _TopSpace_ValueChanged(object sender, DependencyPropertyChangedEventArgs e) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.TopSpace = (int)e.NewValue;
					Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
				}

				void _TaskManagerParameter_TextChanged(object sender, TextChangedEventArgs e) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.TaskManagerParameter = _TaskManagerParameter.Text;
					Config.Instance.FireConfigChangedEvent(Features.None);
				}

				void _TaskManagerPath_TextChanged(object sender, TextChangedEventArgs e) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.TaskManagerPath = _TaskManagerPath.Text;
					Config.Instance.FireConfigChangedEvent(Features.None);
				}
			}
		}
	}
}
