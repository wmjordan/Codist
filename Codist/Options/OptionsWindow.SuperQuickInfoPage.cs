using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Codist.Controls;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;

namespace Codist.Options
{
	sealed partial class OptionsWindow
	{
		sealed class SuperQuickInfoPage : OptionPageFactory
		{
			public override string Name => R.T_SuperQuickInfo;
			public override Features RequiredFeature => Features.SuperQuickInfo;

			protected override OptionPage CreatePage() {
				return new PageControl();
			}

			sealed class PageControl : OptionPage
			{
				readonly OptionBox<QuickInfoOptions> _DisableUntilShift, _CtrlSuppress, _Selection, _Color;
				readonly OptionBox<QuickInfoOptions>[] _Options;
				readonly IntegerBox _MaxWidth, _MaxHeight, _DisplayDelay;
				readonly ColorButton _BackgroundButton;

				public PageControl() {
					var o = Config.Instance.QuickInfoOptions;
					SetContents(new Note(R.OT_QuickInfoNote),
						_DisableUntilShift = o.CreateOptionBox(QuickInfoOptions.CtrlQuickInfo, UpdateConfig, R.OT_HideQuickInfoUntilShift)
							.SetLazyToolTip(() => R.OT_HideQuickInfoUntilShiftTip),
						_CtrlSuppress = o.CreateOptionBox(QuickInfoOptions.CtrlSuppress, UpdateConfig, R.OT_CtrlSuppressQuickInfo).SetLazyToolTip(() => R.OT_CtrlSuppressQuickInfoTip),
						_Selection = o.CreateOptionBox(QuickInfoOptions.Selection, UpdateConfig, R.OT_SelectionInfo)
							.SetLazyToolTip(() => R.OT_SelectionInfoTip),
						_Color = o.CreateOptionBox(QuickInfoOptions.Color, UpdateConfig, R.OT_ColorInfo)
							.SetLazyToolTip(() => R.OT_ColorInfoTip),

						new TitleBox(R.OT_LimitSize),
						new WrapPanel {
							Children = {
								new StackPanel().MakeHorizontal()
									.Add(new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append(R.OT_MaxWidth))
									.Add(_MaxWidth = new Controls.IntegerBox(Config.Instance.QuickInfo.MaxWidth) { Minimum = 0, Maximum = 5000, Step = 100 }.UseVsTheme())
									.SetLazyToolTip(() => R.OT_MaxWidthTip),
								new StackPanel().MakeHorizontal()
									.Add(new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append(R.OT_MaxHeight))
									.Add(_MaxHeight = new Controls.IntegerBox(Config.Instance.QuickInfo.MaxHeight) { Minimum = 0, Maximum = 5000, Step = 50 }.UseVsTheme())
									.SetLazyToolTip(() => R.OT_MaxHeightTip),
							}
						}.ForEachChild((FrameworkElement b) => b.MinWidth = MinColumnWidth),
						new DescriptionBox(R.OT_LimitContentSizeNote),
						new DescriptionBox(R.OT_UnlimitedSize),

						new TitleBox(R.OT_DelayDisplay),
						new StackPanel().MakeHorizontal()
							.Add(new TextBlock { MinWidth = 240, Margin = WpfHelper.SmallHorizontalMargin, Text = R.OT_DelayTime })
							.Add(_DisplayDelay = new Controls.IntegerBox(Config.Instance.QuickInfo.DelayDisplay) { Minimum = 0, Maximum = 100000, Step = 100 }.UseVsTheme()),
						new DescriptionBox(R.OT_DelayDisplayNote),

						new TitleBox(R.T_Color),
						new WrapPanel {
							Children = {
								new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append(R.OT_QuickInfoBackground),
								new ColorButton(Config.Instance.QuickInfo.BackColor, R.T_Color, UpdateQuickInfoBackgroundColor).ReferenceStyle(VsResourceKeys.ButtonStyleKey).Set(ref _BackgroundButton)
							}
						}
					);
					_MaxHeight.ValueChanged += UpdateQuickInfoValue;
					_MaxWidth.ValueChanged += UpdateQuickInfoValue;
					_DisplayDelay.ValueChanged += UpdateQuickInfoValue;
					_Options = new[] { _DisableUntilShift, _CtrlSuppress, _Selection, _Color };
					_BackgroundButton.DefaultColor = () => ThemeCache.ToolTipBackgroundBrush.Color;
				}

				protected override void LoadConfig(Config config) {
					var o = config.QuickInfoOptions;
					Array.ForEach(_Options, i => i.UpdateWithOption(o));
					_MaxHeight.Value = config.QuickInfo.MaxHeight;
					_MaxWidth.Value = config.QuickInfo.MaxWidth;
					_DisplayDelay.Value = config.QuickInfo.DelayDisplay;
					_BackgroundButton.Color = config.QuickInfo.BackColor;
				}

				void UpdateConfig(QuickInfoOptions options, bool set) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, set);
					Config.Instance.FireConfigChangedEvent(Features.SuperQuickInfo);
				}

				void UpdateQuickInfoValue(object sender, DependencyPropertyChangedEventArgs args) {
					if (sender == _MaxHeight) {
						Config.Instance.QuickInfo.MaxHeight = _MaxHeight.Value;
					}
					else if (sender == _MaxWidth) {
						Config.Instance.QuickInfo.MaxWidth = _MaxWidth.Value;
					}
					else if (sender == _DisplayDelay) {
						Config.Instance.QuickInfo.DelayDisplay = _DisplayDelay.Value;
					}
					Config.Instance.FireConfigChangedEvent(Features.SuperQuickInfo);
				}

				void UpdateQuickInfoBackgroundColor(Color color) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.QuickInfo.BackColor = color;
					Config.Instance.FireConfigChangedEvent(Features.SuperQuickInfo);
				}

				internal void Refresh() {
					_BackgroundButton.Color = Config.Instance.QuickInfo.BackColor;
				}
			}
		}
	}
}
