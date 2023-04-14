using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Codist.AutoBuildVersion;
using Codist.Controls;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using R = Codist.Properties.Resources;

namespace Codist.Commands
{
	[Guid("AE0F7D33-D60C-4531-8881-D19B08C58730")]
	sealed class AutoBuildVersionWindow : Window
	{
		readonly EnvDTE.Project _Project;
		readonly string _ConfigPath;
		readonly BuildSetting _Settings;
		readonly ListBox _Configuration;
		readonly AutoVersionSettingsControl _AssemblyVersion, _AssemblyFileVersion;
		readonly CheckBox _RewriteCopyrightYear;
		readonly string _Copyright;
		readonly string[] _CurrentAssemblyVersion, _CurrentAssemblyFileVersion;

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		public AutoBuildVersionWindow(EnvDTE.Project project) {
			_Project = project;
			BuildConfigSetting.TryGetAssemblyAttributeValues(project, out _CurrentAssemblyVersion, out _CurrentAssemblyFileVersion, out _Copyright);
			Title = R.T_AutoBuildVersion;
			ShowInTaskbar = false;
			Height = 250;
			Width = 600;
			SnapsToDevicePixels = true;
			ResizeMode = ResizeMode.NoResize;
			Content = new Grid {
				Margin = WpfHelper.MiddleMargin,
				ColumnDefinitions = {
						new ColumnDefinition { Width = new GridLength(150) },
						new ColumnDefinition { Width = new GridLength(10, GridUnitType.Star) }
					},
				RowDefinitions = {
					new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
					new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
				},
				Children = {
					new StackPanel {
						Children = {
							new Border {
								BorderThickness = new Thickness(0,0,0,1),
								Child = new StackPanel {
									Orientation = Orientation.Horizontal,
									Children = {
										new Label { Content = R.T_Project }.ReferenceStyle(VsResourceKeys.ThemedDialogLabelStyleKey),
										new Label { Content = project.Name }.ReferenceStyle(VsResourceKeys.ThemedDialogLabelStyleKey),
									}
								}
							}.ReferenceProperty(Border.BorderBrushProperty, VsBrushes.AccentBorderKey),
						}
					}.SetValue(Grid.SetColumnSpan, 2),

					new StackPanel {
						Margin = WpfHelper.SmallMargin,
						Children = {
							new Label { Content = R.T_Configuration }.ReferenceStyle(VsResourceKeys.ThemedDialogLabelStyleKey),
							(_Configuration = new ListBox { }).ReferenceStyle(VsResourceKeys.ThemedDialogListBoxStyleKey),
						}
					}.SetValue(Grid.SetRow, 1),

					new StackPanel {
						Margin = WpfHelper.SmallMargin,
						Children = {
							(_AssemblyVersion = new AutoVersionSettingsControl("AssemblyVersion:", _CurrentAssemblyVersion)),
							(_AssemblyFileVersion = new AutoVersionSettingsControl("AssemblyFileVersion:", _CurrentAssemblyFileVersion)),
							(_RewriteCopyrightYear = new CheckBox { Content = R.T_RewriteCopyrightYear }.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey)),
							new WrapPanel {
								Children = {
									new ThemedButton(R.CMD_Reset, R.CMDT_ResetAutoBuildVersion, Reset){ Width = 80, Margin = new Thickness(10) }.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
									new ThemedButton(R.CMD_SaveBuildSetting, R.CMDT_SaveChanges, Ok) { IsDefault = true, Width = 80, Margin = new Thickness(10) }.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
									new ThemedButton(R.CMD_Cancel, R.CMDT_UndoChanges, Cancel) { IsCancel = true, Width = 80, Margin = new Thickness(10) }.ReferenceStyle(VsResourceKeys.ButtonStyleKey)
								}
							}
						}
					}.SetValue(Grid.SetRow, 1).SetValue(Grid.SetColumn, 1),

				}
			}.ReferenceProperty(TextBlock.ForegroundProperty, VsBrushes.ToolWindowTextKey);
			this.ReferenceProperty(Border.BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);

			_ConfigPath = BuildSetting.GetConfigPath(project);
			_Settings = BuildSetting.Load(_ConfigPath) ?? new BuildSetting();

			_Configuration.Items.Add("<Any>");
			_Configuration.Items.AddRange(project.ConfigurationManager.ConfigurationRowNames as object[]);
			_Configuration.SelectionChanged += Configuration_SelectionChanged;
			_Configuration.SelectedIndex = 0;
		}

		void Configuration_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (e.RemovedItems.Count > 0) {
				var r = e.RemovedItems[0] as string;
				SetBuildConfigSettings(r);
			}
			ReadBuildConfigSettings(_Configuration.SelectedItem as string);
		}

		void SetBuildConfigSettings(string r) {
			bool o = _Settings.TryGetValue(r, out var s);
			if (o == false) {
				s = new BuildConfigSetting();
			}
			s.AssemblyVersion = _AssemblyVersion.Setting;
			s.AssemblyFileVersion = _AssemblyFileVersion.Setting;
			s.UpdateLastYearNumberInCopyright = _RewriteCopyrightYear.IsChecked == true;
			if (o == false && s.ShouldRewrite) {
				_Settings.Add(r, s);
			}
			else if (o && s.ShouldRewrite == false) {
				_Settings.Remove(r);
			}
		}

		void ReadBuildConfigSettings(string config) {
			if (_Settings.TryGetValue(config, out var s)) {
				_AssemblyVersion.Set(s.AssemblyVersion);
				_AssemblyFileVersion.Set(s.AssemblyFileVersion);
				_RewriteCopyrightYear.IsChecked = s.UpdateLastYearNumberInCopyright;
			}
			else {
				_AssemblyVersion.Reset();
				_AssemblyFileVersion.Reset();
				_RewriteCopyrightYear.IsChecked = false;
			}
		}

		void Reset() {
			var config = _Configuration.SelectedItem as string;
			_Settings.Remove(config);
			ReadBuildConfigSettings(config);
		}

		void Ok() {
			SetBuildConfigSettings(_Configuration.SelectedItem as string);
			File.WriteAllText(_ConfigPath, JsonConvert.SerializeObject(_Settings, Formatting.None, new Newtonsoft.Json.Converters.StringEnumConverter()));
			Close();
		}

		void Cancel() {
			Close();
		}

		sealed class AutoVersionSettingsControl : StackPanel
		{
			readonly AutoVersionModeControl _Major, _Minor, _Build, _Revision;
			readonly Label _Preview;
			readonly string _OriginalValues;
			readonly string[] _PreviewValues;
			bool _UiLock;

			public AutoVersionSettingsControl(string title, string[] previewValues) {
				Margin = WpfHelper.SmallMargin;
				this.Add(
					new StackPanel {
						Orientation = Orientation.Horizontal,
						Children = {
							new Label { Content = title, MinWidth = 150 }.ReferenceStyle(VsResourceKeys.ThemedDialogLabelStyleKey),
							(_Preview = new Label {}.ReferenceStyle(VsResourceKeys.ThemedDialogLabelStyleKey))
						}
					},
					new StackPanel {
						Orientation = Orientation.Horizontal,
						Margin = WpfHelper.SmallMargin,
						Children = {
							(_Major = new AutoVersionModeControl()),
							new TextBlock { Text = "." }.WrapMargin(WpfHelper.SmallHorizontalMargin),
							(_Minor = new AutoVersionModeControl()),
							new TextBlock { Text = "." }.WrapMargin(WpfHelper.SmallHorizontalMargin),
							(_Build = new AutoVersionModeControl()),
							new TextBlock { Text = "." }.WrapMargin(WpfHelper.SmallHorizontalMargin),
							(_Revision = new AutoVersionModeControl())
						}
					}
				);

				if ((_PreviewValues = previewValues) != null) {
					IsEnabled = true;
					_OriginalValues = String.Join(".", previewValues);
					_Major.SelectionChanged += Items_CurrentChanged;
					_Minor.SelectionChanged += Items_CurrentChanged;
					_Build.SelectionChanged += Items_CurrentChanged;
					_Revision.SelectionChanged += Items_CurrentChanged;
					Items_CurrentChanged(this, EventArgs.Empty);
				}
				else {
					IsEnabled = false;
					_Preview.Content = R.T_VersionNumberNotFound;
				}
			}

			void Items_CurrentChanged(object sender, EventArgs e) {
				if (_UiLock) {
					return;
				}
				UpdateVersionChangePreview();
			}

			void UpdateVersionChangePreview() {
				_Preview.Content = Setting.ShouldRewrite
					? (_OriginalValues + " => " + Setting.Rewrite(_PreviewValues[0], _PreviewValues[1], _PreviewValues[2], _PreviewValues[3]))
					: _OriginalValues;
			}

			public VersionSetting Setting => new VersionSetting((VersionRewriteMode)_Major.SelectedIndex,
				(VersionRewriteMode)_Minor.SelectedIndex,
				(VersionRewriteMode)_Build.SelectedIndex,
				(VersionRewriteMode)_Revision.SelectedIndex);

			public void Reset() {
				_UiLock = true;
				_Major.SelectedIndex = 0;
				_Minor.SelectedIndex = 0;
				_Build.SelectedIndex = 0;
				_Preview.Content = _OriginalValues;
				_UiLock = false;
				_Revision.SelectedIndex = 0;
			}

			public void Set(VersionSetting setting) {
				if (setting is null) {
					Reset();
					return;
				}
				_UiLock = true;
				_Major.SelectedIndex = (int)setting.Major;
				_Minor.SelectedIndex = (int)setting.Minor;
				_Build.SelectedIndex = (int)setting.Build;
				_Revision.SelectedIndex = (int)setting.Revision;
				_UiLock = false;
				UpdateVersionChangePreview();
			}

			public VersionSetting Get() {
				return new VersionSetting((VersionRewriteMode)_Major.SelectedIndex, (VersionRewriteMode)_Minor.SelectedIndex, (VersionRewriteMode)_Build.SelectedIndex, (VersionRewriteMode)_Revision.SelectedIndex);
			}
		}

		sealed class AutoVersionModeControl : ComboBox
		{
			public AutoVersionModeControl() {
				IsEditable = false;
				Items.AddRange(R.T_VersionRewriteMode.Split('/'));
				SelectedIndex = 0;
				MinWidth = 90;
				this.ReferenceStyle(VsResourceKeys.ComboBoxStyleKey);
			}
		}
	}
}
