using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Codist.Controls;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;

namespace Codist.Options
{
	sealed partial class OptionsWindow
	{
		sealed class WrapTextPage : OptionPageFactory
		{
			public override string Name => R.OT_WrapText;
			public override Features RequiredFeature => Features.SmartBar;
			public override bool IsSubOption => true;

			protected override OptionPage CreatePage() {
				return new PageControl();
			}

			sealed class PageControl : OptionPage
			{
				readonly TextBox _Name, _Pattern, _Indicator;
				readonly ThemedListBox _List;
				readonly Button _AddButton, _RemoveButton, _MoveUpButton, _ResetButton;

				public PageControl() {
					var o = Config.Instance.SpecialHighlightOptions;
					SetContents(new Note(R.OT_WrapTextNote),
						new TitleBox(R.OT_WrapTexts),
						new Grid {
							ColumnDefinitions = {
								new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), },
								new ColumnDefinition { Width = new GridLength(90, GridUnitType.Pixel) },
							},
							RowDefinitions = {
								new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
								new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) }
							},
							Children = {
								(_List = new ThemedListBox { Margin = WpfHelper.SmallMargin, MaxHeight = 250 }),
								new Grid {
									Margin = WpfHelper.SmallMargin,
									ColumnDefinitions = {
										new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
										new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) },
									},
									RowDefinitions = {
										new RowDefinition(),
										new RowDefinition(),
										new RowDefinition(),
										new RowDefinition()
									},
									Children = {
										new Label { Content = R.OTC_Name, Width = 70 },
										(_Name = new TextBox { IsEnabled = false, Margin = WpfHelper.SmallVerticalMargin }).SetValue(Grid.SetColumn, 1),
										new Label { Content = R.OTC_Pattern, Width = 70 }.SetValue(Grid.SetRow, 1),
										(_Pattern = new TextBox { IsEnabled = false, Margin = WpfHelper.SmallVerticalMargin, AcceptsReturn = true, AcceptsTab = true }).SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 1),
										new Label { Content = R.OTC_Indicator, Width = 70 }.SetValue(Grid.SetRow, 2),
										(_Indicator = new TextBox { IsEnabled = false, Margin = WpfHelper.SmallVerticalMargin, Width = 40, MaxLength = 1, HorizontalAlignment = HorizontalAlignment.Left }).SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 2),
										new DescriptionBox(R.OT_WrapTextSelectionIndicator).SetValue(Grid.SetRow, 3).SetValue(Grid.SetColumnSpan, 2)
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
										(_AddButton = new Button { Margin = WpfHelper.SmallVerticalMargin, Content = R.CMD_NewItem })
									}
								}.SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 1)
							}
						}
					);

					_List.Items.AddRange(Config.Instance.WrapTexts.Select(i => new WrapTextContainer(i)));
					_List.SelectionChanged += HandleListSelectedChanged;
					_AddButton.Click += HandleAddButtonClick;
					_RemoveButton.Click += HandleRemoveButtonClick;
					_MoveUpButton.Click += HandleMoveUpButtonClick;
					_ResetButton.Click += HandleResetButtonClick;
					_Name.GotFocus += HandleTextGotFocus;
					_Pattern.GotFocus += HandleTextGotFocus;
					_Indicator.GotFocus += HandleTextGotFocus;
					RefreshWrapTextUI();
				}

				protected override void LoadConfig(Config config) {
					ResetWrapTexts(config.WrapTexts);
				}

				void RefreshWrapTextUI() {
					var c = _List.SelectedItem as WrapTextContainer;
					if (_RemoveButton.IsEnabled = _Name.IsEnabled = _Pattern.IsEnabled = _Indicator.IsEnabled = c != null) {
						_MoveUpButton.IsEnabled = _List.SelectedIndex > 0;
						var t = c.WrapText;
						_Name.Text = t.Name;
						_Pattern.Text = t.Pattern;
						_Indicator.Text = t.Indicator.ToString();
					}
					else {
						_MoveUpButton.IsEnabled = false;
					}
				}
				void ResetWrapTexts(System.Collections.Generic.List<WrapText> wrapTexts) {
					_List.Items.Clear();
					_List.Items.AddRange(wrapTexts.Select(i => new WrapTextContainer(i)));
				}

				void HandleListSelectedChanged(object s, SelectionChangedEventArgs args) {
					RefreshWrapTextUI();
				}

				void HandleAddButtonClick(object s, RoutedEventArgs args) {
					var item = new WrapText((_List.SelectedItem as WrapTextContainer)?.WrapText?.Pattern ?? "<c>" + WrapText.DefaultIndicator + "</c>", R.CMD_NewItem);
					var container = new WrapTextContainer(item);
					_List.Items.Add(container);
					Config.Instance.WrapTexts.Add(item);
					_List.SelectedIndex = _List.Items.Count - 1;
					_List.ScrollIntoView(container);
					RefreshWrapTextUI();
					_Name.Focus();
					Config.Instance.FireConfigChangedEvent(Features.SmartBar);
				}

				void HandleRemoveButtonClick(object s, RoutedEventArgs args) {
					var p = _List.SelectedIndex;
					_List.Items.RemoveAndDisposeAt(p);
					Config.Instance.WrapTexts.RemoveAt(p);
					RefreshWrapTextUI();
					Config.Instance.FireConfigChangedEvent(Features.WrapText);
				}

				void HandleMoveUpButtonClick(object s, RoutedEventArgs args) {
					var p = _List.SelectedIndex;
					if (p > 0) {
						var se = Config.Instance.WrapTexts[p];
						_List.Items.RemoveAt(p);
						Config.Instance.WrapTexts.RemoveAt(p);
						var container = new WrapTextContainer(se);
						_List.Items.Insert(--p, container);
						Config.Instance.WrapTexts.Insert(p, se);
						_List.SelectedIndex = p;
						_List.ScrollIntoView(container);
						Config.Instance.FireConfigChangedEvent(Features.WrapText);
					}
					_MoveUpButton.IsEnabled = p > 0;
				}

				void HandleTextGotFocus(object s, RoutedEventArgs args) {
					var sender = (TextBox)s;
					sender.LostFocus += UpdateWrapText;
				}

				void UpdateWrapText(object s, RoutedEventArgs e) {
					var sender = (TextBox)s;
					sender.LostFocus -= UpdateWrapText;

					var container = _List.SelectedItem as WrapTextContainer;
					var t = container?.WrapText;
					if (t is null) {
						return;
					}
					if (sender == _Name) {
						if (_Name.Text == t.Name) {
							return;
						}
						t.Name = _Name.Text;
					}
					else if (sender == _Pattern) {
						if (_Pattern.Text == t.Pattern) {
							return;
						}
						t.Pattern = _Pattern.Text;
					}
					else if (sender == _Indicator) {
						var indicator = _Indicator.Text.Length > 0 ? _Indicator.Text[0] : WrapText.DefaultIndicator;
						if (indicator == t.Indicator) {
							return;
						}
						t.Indicator = indicator;
					}

					container.Refresh();
					Config.Instance.FireConfigChangedEvent(Features.WrapText);
				}

				void HandleResetButtonClick(object s, RoutedEventArgs args) {
					if (MessageWindow.AskYesNo(R.OT_ConfirmResetWrapText) == true) {
						Config.Instance.ResetWrapTexts();
						ResetWrapTexts(Config.Instance.WrapTexts);
						Config.Instance.FireConfigChangedEvent(Features.WrapText);
					}
				}
			}

			sealed class WrapTextContainer : ListBoxItem
			{
				readonly WrapText _WrapText;
				readonly TextBlock _Name, _Pattern;

				public WrapTextContainer(WrapText wrapText) {
					_WrapText = wrapText;
					StackPanel p;
					Content = p = new StackPanel {
						Orientation = Orientation.Horizontal,
						Children = {
							VsImageHelper.GetImage(IconIds.WrapText)
								.WrapMargin(WpfHelper.GlyphMargin)
								.SetProperty(Image.VerticalAlignmentProperty, VerticalAlignment.Top),
							new TextBlock { Text = wrapText.Name, FontWeight = FontWeights.Bold, MinWidth = 140 }
								.Set(ref _Name),
							new TextBlock { Text = wrapText.Pattern, Margin = WpfHelper.MiddleHorizontalMargin }
								.ReferenceProperty(ForegroundProperty, VsBrushes.GrayTextKey)
								.Set(ref _Pattern)
						}
					};
					p.SetBackgroundForCrispImage(ThemeCache.ToolWindowBackgroundColor);
				}

				public WrapText WrapText => _WrapText;

				public void Refresh() {
					_Name.Text = _WrapText.Name;
					_Pattern.Text = _WrapText.Pattern;
				}
			}
		}
	}
}
