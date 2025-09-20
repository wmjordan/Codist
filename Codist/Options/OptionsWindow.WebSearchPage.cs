using System;
using System.Windows;
using System.Windows.Controls;
using Codist.Controls;
using Microsoft.Win32;
using R = Codist.Properties.Resources;

namespace Codist.Options
{
	sealed partial class OptionsWindow
	{
		sealed class WebSearchPage : OptionPageFactory
		{
			public override string Name => R.OT_WebSearch;
			public override Features RequiredFeature => Features.SmartBar | Features.SuperQuickInfo;

			protected override OptionPage CreatePage() {
				return new PageControl();
			}

			sealed class PageControl : OptionPage
			{
				readonly TextBox _BrowserPath, _BrowserParameter, _Name, _Url;
				readonly ListBox _List;
				readonly Button _BrowseBrowserPath, _AddButton, _RemoveButton, _MoveUpButton, _ResetButton, _SaveButton;

				public PageControl() {
					var o = Config.Instance.SpecialHighlightOptions;
					SetContents(new Note(R.OT_WebSearchNote),
						new TitleBox(R.OT_SearchEngines),
						new Grid {
							ColumnDefinitions = {
								new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), },
								new ColumnDefinition { Width = new GridLength(90, GridUnitType.Pixel) }
							},
							RowDefinitions = {
								new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
								new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) }
							},
							Children = {
								(_List = new ThemedListBox { Margin = WpfHelper.SmallMargin }),
								new Grid {
									Margin = WpfHelper.SmallMargin,
									ColumnDefinitions = {
										new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
										new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) },
									},
									RowDefinitions = {
										new RowDefinition(),
										new RowDefinition(),
										new RowDefinition()
									},
									Children = {
										new Label { Content = R.OTC_Name, Width = 60 },
										(_Name = new TextBox { IsEnabled = false, Margin = WpfHelper.SmallVerticalMargin }).SetValue(Grid.SetColumn, 1),
										new Label { Content = R.OTC_URL, Width = 60 }.SetValue(Grid.SetRow, 1),
										(_Url = new TextBox { IsEnabled = false, Margin = WpfHelper.SmallVerticalMargin }).SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 1),
										new DescriptionBox(R.OT_SearchParamSubsitution).SetValue(Grid.SetRow, 2).SetValue(Grid.SetColumnSpan, 2)
									}
								}.SetValue(Grid.SetRow, 1),
								new StackPanel {
									Margin = WpfHelper.SmallMargin,
									Children = {
										(_RemoveButton = new Button { Margin = WpfHelper.SmallVerticalMargin, Content = R.CMD_Remove }),
										(_MoveUpButton = new Button { Margin = WpfHelper.SmallVerticalMargin, Content = R.CMD_MoveUp }),
										(_ResetButton = new Button { Margin = WpfHelper.SmallVerticalMargin, Content = R.CMD_Reset }),
									}
								}.SetValue(Grid.SetColumn, 1),
								new StackPanel {
									Margin = WpfHelper.SmallMargin,
									Children = {
										(_AddButton = new Button { Margin = WpfHelper.SmallVerticalMargin, Content = R.CMD_Add }),
										(_SaveButton = new Button { Margin = WpfHelper.SmallVerticalMargin, Content = R.CMD_Update })
									}
								}.SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 1)
							}
						},
						new TitleBox(R.OT_SearchResultBrowser),
						new Note(R.OT_SearchResultBrowserNote),
						new Grid {
							ColumnDefinitions = {
								new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), },
								new ColumnDefinition { Width = new GridLength(100, GridUnitType.Pixel) }
							},
							Children = {
								(_BrowserPath = new TextBox { Margin = WpfHelper.SmallHorizontalMargin, Text = Config.Instance.BrowserPath })
									.SetValue(Grid.SetColumn, 0),
								(_BrowseBrowserPath = new Button { Content = R.CMD_Browse, Margin = WpfHelper.SmallHorizontalMargin })
									.SetValue(Grid.SetColumn, 1)
							}
						},
						new Note(R.OT_BrowserParameter),
						_BrowserParameter = new TextBox { Margin = WpfHelper.SmallHorizontalMargin, Text = Config.Instance.BrowserParameter },
						new DescriptionBox(R.OT_URLSubstitution)
					);

					_BrowserPath.TextChanged += HandleBrowserPathTextChanged;
					_BrowserParameter.TextChanged += HandleBrowserParameterTextChanged;
					_BrowseBrowserPath.Click += HandleBrowseBrowserButtonClick;
					_List.Items.AddRange(Config.Instance.SearchEngines);
					_List.SelectionChanged += HandleListSelectionChanged;
					_AddButton.Click += HandleAddButtonClick;
					_RemoveButton.Click += HandleRemoveButtonClick;
					_MoveUpButton.Click += HandleMoveUpButtonClick;
					_SaveButton.Click += HandleSaveButtonClick;
					_ResetButton.Click += HandleResetButtonClick;
					RefreshSearchEngineUI();
				}

				void HandleBrowserParameterTextChanged(object sender, TextChangedEventArgs e) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.BrowserParameter = _BrowserParameter.Text;
					Config.Instance.FireConfigChangedEvent(Features.WebSearch);
				}

				void HandleBrowserPathTextChanged(object sender, TextChangedEventArgs e) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.BrowserPath = _BrowserPath.Text;
					Config.Instance.FireConfigChangedEvent(Features.WebSearch);
				}

				protected override void LoadConfig(Config config) {
					_BrowserPath.Text = config.BrowserPath;
					_BrowserParameter.Text = config.BrowserParameter;
					ResetSearchEngines(config.SearchEngines);
				}

				void RefreshSearchEngineUI() {
					var se = _List.SelectedItem as SearchEngine;
					if (_RemoveButton.IsEnabled = _SaveButton.IsEnabled = _Name.IsEnabled = _Url.IsEnabled = se != null) {
						_MoveUpButton.IsEnabled = _List.SelectedIndex > 0;
						_Name.Text = se.Name;
						_Url.Text = se.Pattern;
					}
					else {
						_MoveUpButton.IsEnabled = false;
					}
				}
				void ResetSearchEngines(System.Collections.Generic.List<SearchEngine> searchEngines) {
					_List.Items.Clear();
					_List.Items.AddRange(searchEngines);
				}

				void HandleBrowseBrowserButtonClick(object s, RoutedEventArgs args) {
					var d = new OpenFileDialog {
						Title = R.OT_LocateBrowser,
						CheckFileExists = true,
						AddExtension = true,
						Filter = R.F_Executable
					};
					if (d.ShowDialog() == true) {
						_BrowserPath.Text = d.FileName;
					}
				}

				void HandleListSelectionChanged(object s, SelectionChangedEventArgs args) {
					RefreshSearchEngineUI();
				}

				void HandleAddButtonClick(object s, RoutedEventArgs args) {
					var item = new SearchEngine(R.CMD_NewItem, String.Empty);
					_List.Items.Add(item);
					Config.Instance.SearchEngines.Add(item);
					_List.SelectedIndex = _List.Items.Count - 1;
					RefreshSearchEngineUI();
					_Name.Focus();
					Config.Instance.FireConfigChangedEvent(Features.SmartBar);
				}

				void HandleRemoveButtonClick(object s, RoutedEventArgs args) {
					var i = _List.SelectedItem as SearchEngine;
					if (MessageWindow.AskYesNo(R.OT_ConfirmRemoveSearchEngine.Replace("<NAME>", i.Name)) == true) {
						var p = _List.SelectedIndex;
						_List.Items.RemoveAndDisposeAt(p);
						Config.Instance.SearchEngines.RemoveAt(p);
						RefreshSearchEngineUI();
						Config.Instance.FireConfigChangedEvent(Features.WebSearch);
					}
				}

				void HandleMoveUpButtonClick(object s, RoutedEventArgs args) {
					var p = _List.SelectedIndex;
					if (p > 0) {
						var se = Config.Instance.SearchEngines[p];
						_List.Items.RemoveAt(p);
						Config.Instance.SearchEngines.RemoveAt(p);
						_List.Items.Insert(--p, se);
						Config.Instance.SearchEngines.Insert(p, se);
						_List.SelectedIndex = p;
						Config.Instance.FireConfigChangedEvent(Features.WebSearch);
					}
					_MoveUpButton.IsEnabled = p > 0;
				}

				void HandleSaveButtonClick(object s, RoutedEventArgs args) {
					var se = _List.SelectedItem as SearchEngine;
					se.Name = _Name.Text;
					se.Pattern = _Url.Text;
					var p = _List.SelectedIndex;
					_List.Items.RemoveAt(p);
					_List.Items.Insert(p, se);
					Config.Instance.FireConfigChangedEvent(Features.WebSearch);
				}

				void HandleResetButtonClick(object s, RoutedEventArgs args) {
					if (MessageWindow.AskYesNo(R.OT_ConfirmResetSearchEngine) == true) {
						Config.Instance.ResetSearchEngines();
						ResetSearchEngines(Config.Instance.SearchEngines);
						Config.Instance.FireConfigChangedEvent(Features.WebSearch);
					}
				}
			}
		}
	}
}
