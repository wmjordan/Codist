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
				readonly ListBox _List;
				readonly Button _AddButton, _RemoveButton, _MoveUpButton, _ResetButton, _SaveButton;

				public PageControl() {
					var o = Config.Instance.SpecialHighlightOptions;
					SetContents(new Note(R.OT_WrapTextNote),
						new TitleBox(R.OT_WrapTexts),
						new Grid {
							ColumnDefinitions = {
								new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), },
								new ColumnDefinition { Width = new GridLength(80, GridUnitType.Pixel) },
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
										new RowDefinition(),
										new RowDefinition()
									},
									Children = {
										new Label { Content = R.OTC_Name, Width = 70 },
										(_Name = new TextBox { IsEnabled = false, Margin = WpfHelper.SmallVerticalMargin }).SetValue(Grid.SetColumn, 1),
										new Label { Content = R.OTC_Pattern, Width = 70 }.SetValue(Grid.SetRow, 1),
										(_Pattern = new TextBox { IsEnabled = false, Margin = WpfHelper.SmallVerticalMargin }).SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 1),
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
										(_AddButton = new Button { Margin = WpfHelper.SmallVerticalMargin, Content = R.CMD_Add }),
										(_SaveButton = new Button { Margin = WpfHelper.SmallVerticalMargin, Content = R.CMD_Update })
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
					_SaveButton.Click += HandleSaveButtonClick;
					_ResetButton.Click += HandleResetButtonClick;
					RefreshWrapTextUI();
				}

				protected override void LoadConfig(Config config) {
					ResetWrapTexts(config.WrapTexts);
				}

				void RefreshWrapTextUI() {
					var c = _List.SelectedItem as WrapTextContainer;
					if (_RemoveButton.IsEnabled = _SaveButton.IsEnabled = _Name.IsEnabled = _Pattern.IsEnabled = _Indicator.IsEnabled = c != null) {
						_MoveUpButton.IsEnabled = _List.SelectedIndex > 0;
						var se = c.WrapText;
						_Name.Text = se.Name;
						_Pattern.Text = se.Pattern;
						_Indicator.Text = se.Indicator.ToString();
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
					var item = new WrapText("<c>" + WrapText.DefaultIndicator + "</c>", R.CMD_NewItem);
					_List.Items.Add(new WrapTextContainer(item));
					Config.Instance.WrapTexts.Add(item);
					_List.SelectedIndex = _List.Items.Count - 1;
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
						_List.Items.Insert(--p, new WrapTextContainer(se));
						Config.Instance.WrapTexts.Insert(p, se);
						_List.SelectedIndex = p;
						Config.Instance.FireConfigChangedEvent(Features.WrapText);
					}
					_MoveUpButton.IsEnabled = p > 0;
				}

				void HandleSaveButtonClick(object s, RoutedEventArgs args) {
					var se = _List.SelectedItem as WrapText;
					se.Name = _Name.Text;
					se.Pattern = _Pattern.Text;
					se.Indicator = _Indicator.Text.Length > 0 ? _Indicator.Text[0] : WrapText.DefaultIndicator;
					var p = _List.SelectedIndex;
					_List.Items.RemoveAt(p);
					_List.Items.Insert(p, new WrapTextContainer(se));
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

				public WrapTextContainer(WrapText wrapText) {
					_WrapText = wrapText;
					StackPanel p;
					Content = p = new StackPanel {
						Orientation = Orientation.Horizontal,
						Children = {
							new TextBlock { Text = wrapText.Name, FontWeight = FontWeights.Bold, MinWidth = 150 }
								.SetGlyph(IconIds.WrapText),
							new TextBlock { Text = wrapText.Pattern, Margin = WpfHelper.MiddleHorizontalMargin }.ReferenceProperty(ForegroundProperty, VsBrushes.GrayTextKey)
						}
					};
					p.SetBackgroundForCrispImage(ThemeCache.ToolWindowBackgroundColor);
				}

				public WrapText WrapText => _WrapText;
			}
		}
	}
}
