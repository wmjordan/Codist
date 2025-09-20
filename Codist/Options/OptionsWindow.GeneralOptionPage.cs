using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CLR;
using Codist.Controls;
using Microsoft.Win32;
using R = Codist.Properties.Resources;

namespace Codist.Options
{
	sealed partial class OptionsWindow
	{
		sealed class GeneralOptionPage : OptionPageFactory
		{
			public override string Name => R.OT_General;
			public override Features RequiredFeature => Features.None;

			protected override OptionPage CreatePage() {
				return new PageControl();
			}

			sealed class PageControl : OptionPage
			{
				readonly OptionBox<Features> _SyntaxHighlight, _SuperQuickInfo, _SmartBar, _NavigationBar, _ScrollbarMarker, _JumpListEnhancer, _AutoSurround;
				readonly OptionBox<Features>[] _Options;
				readonly Button _LoadButton, _SaveButton, _OpenConfigFolderButton;
				readonly Note _NoticeBox;

				public PageControl() {
					Thickness linkMargin = new Thickness(23, 0, 3, 0);
					var o = Config.Instance.Features;
					SetContents(
						new TitleBox(R.OT_FeatureControllers),
						new DescriptionBox(R.OT_FeatureControllersTip),
						new WrapPanel {
							Children = {
								(_SyntaxHighlight = o.CreateOptionBox(Features.SyntaxHighlight, UpdateConfig, R.T_SyntaxHighlight)
									.SetLazyToolTip(() => R.OT_SyntaxHighlightTip)),
								(_SuperQuickInfo = o.CreateOptionBox(Features.SuperQuickInfo, UpdateConfig, R.T_SuperQuickInfo)
									.SetLazyToolTip(() => R.OT_QuickInfoTip)),
								(_SmartBar = o.CreateOptionBox(Features.SmartBar, UpdateConfig, R.T_SmartBar)
									.SetLazyToolTip(() => R.OT_SmartBarTip)),
								(_NavigationBar = o.CreateOptionBox(Features.NaviBar, UpdateConfig, R.T_NavigationBar)
									.SetLazyToolTip(() => R.OT_NavigationBarTip)),
								(_ScrollbarMarker = o.CreateOptionBox(Features.ScrollbarMarkers, UpdateConfig, R.T_ScrollbarMarkers)
									.SetLazyToolTip(() => R.OT_ScrollbarMarkerTip)),
								(_JumpListEnhancer = o.CreateOptionBox(Features.JumpList, UpdateConfig, R.T_JumpList)
									.SetLazyToolTip(() => R.OT_JumpListTip)),
								(_AutoSurround = o.CreateOptionBox(Features.AutoSurround, UpdateConfig, R.T_AutoSurround)
									.SetLazyToolTip(() => R.OT_AutoSurround))
							}
						},
						(_NoticeBox = new Note(R.OT_FeatureChangesTip) { BorderThickness = WpfHelper.TinyMargin, Visibility = Visibility.Collapsed }),

						new TitleBox(R.OT_ConfigurationFile),
						new DescriptionBox(R.OT_ConfigurationFileTip),
						new WrapPanel {
							Children = {
								(_LoadButton = new Button { Name = "_Load", Content = R.CMD_Load, ToolTip = R.OT_LoadConfigFileTip }),
								(_SaveButton = new Button { Name = "_Save", Content = R.CMD_Save, ToolTip = R.OT_SaveConfigFileTip }),
								(_OpenConfigFolderButton = new Button { Content = R.CMD_OpenConfigFolder, ToolTip = R.OT_OpenConfigFolderTip})
							}
						},

						new TitleBox(R.OT_ThankYou),
						new TextBlock { Margin = WpfHelper.MiddleTopMargin }.Append(R.OT_ProjectWebSite),
						new TextBlock { Margin = linkMargin }.AppendLink("github.com/wmjordan/Codist", "https://github.com/wmjordan/Codist", R.CMD_GotoProjectWebSite),
						new TextBlock { Margin = WpfHelper.MiddleTopMargin }.Append(R.OT_ReportBugsAndSuggestions),
						new TextBlock { Margin = linkMargin }.AppendLink("github.com/wmjordan/Codist/issues", "https://github.com/wmjordan/Codist/issues", R.CMD_PostIssue),
						new TextBlock { Margin = WpfHelper.MiddleTopMargin }.Append(R.OT_LatestRelease),
						new TextBlock { Margin = linkMargin }.AppendLink("github.com/wmjordan/Codist/releases", "https://github.com/wmjordan/Codist/releases", R.CMD_GotoProjectReleasePage),
						new TitleBox(R.OT_SupportCodst),
						new TextBlock { Margin = linkMargin }.AppendLink(R.CMD_DonateLink, "https://www.paypal.me/wmzuo/19.99", R.CMDT_OpenDonatePage),
						new TextBlock { Margin = linkMargin }.AppendLink(R.CMD_WechatDonateLink, ShowWechatQrCode, R.CMDT_OpenWechatQrCode),
						new DescriptionBox(R.OT_DonateLinkTip)
					);
					_Options = new[] { _SyntaxHighlight, _SuperQuickInfo, _SmartBar, _NavigationBar, _ScrollbarMarker, _JumpListEnhancer, _AutoSurround };
					foreach (var item in _Options) {
						item.MinWidth = 150;
						item.Margin = WpfHelper.MiddleMargin;
						if (item.CeqAny(_JumpListEnhancer, _AutoSurround) == false) {
							item.PreviewMouseDown += HighlightNoticeBox;
						}
					}
					foreach (var item in new[] { _LoadButton, _SaveButton, _OpenConfigFolderButton}) {
						item.MinWidth = 120;
						item.Margin = WpfHelper.MiddleMargin;
						item.Click += LoadOrSaveConfig;
					}
				}

				protected override void LoadConfig(Config config) {
					var o = config.Features;
					Array.ForEach(_Options, i => i.UpdateWithOption(o));
				}

				void HighlightNoticeBox(object sender, MouseButtonEventArgs e) {
					_NoticeBox.BorderBrush = SystemColors.HighlightBrush;
					_NoticeBox.Background = SystemColors.HighlightBrush.Alpha(WpfHelper.DimmedOpacity);
					_NoticeBox.ReferenceProperty(ForegroundProperty, Microsoft.VisualStudio.Shell.VsBrushes.WindowTextKey);
					_NoticeBox.Visibility = Visibility.Visible;
					foreach (var item in _Options) {
						item.PreviewMouseDown -= HighlightNoticeBox;
					}
				}

				void LoadOrSaveConfig(object sender, EventArgs args) {
					if (sender == _LoadButton) {
						var d = new OpenFileDialog {
							Title = R.T_LoadConfig,
							FileName = "Codist.json",
							DefaultExt = "json",
							Filter = R.F_Config
						};
						if (d.ShowDialog() != true) {
							return;
						}
						try {
							string file = d.FileName;
							Config.LoadConfig(file);
							if (Version.TryParse(Config.Instance.Version, out var newVersion)
								&& newVersion > Version.Parse(Config.CurrentVersion)) {
								new MessageWindow(R.T_NewVersionConfig, nameof(Codist), MessageBoxButton.OK, MessageBoxImage.Information).ShowDialog();
							}
							if (file != Config.ConfigPath) {
								System.IO.File.Copy(file, Config.ConfigPath, true);
							}
						}
						catch (Exception ex) {
							MessageWindow.Error(R.T_ErrorLoadingConfig + ex.Message);
						}
					}
					else if (sender == _SaveButton) {
						var d = new SaveFileDialog {
							Title = R.T_SaveConfig,
							FileName = "Codist.json",
							DefaultExt = "json",
							Filter = R.F_Config
						};
						if (d.ShowDialog() != true) {
							return;
						}

						try {
							Config.Instance.SaveConfig(d.FileName);
						}
						catch (Exception ex) {
							MessageWindow.Error(ex, R.T_ErrorSavingConfig);
						}
					}
					else {
						try {
							if (System.IO.Directory.Exists(Config.ConfigDirectory) == false) {
								System.IO.Directory.CreateDirectory(Config.ConfigDirectory);
							}
							System.Diagnostics.Process.Start(Config.ConfigDirectory);
						}
						catch (Exception ex) {
							ex.Log();
						}
					}
				}
				void ShowWechatQrCode(object link) {
					new Window {
						Width = 401,
						Height = 401,
						Title = R.CMD_WechatDonateLink,
						Content = new Image {
							Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Codist;component/Resources/wechatQrcode.png"))
						}
					}.ShowDialog();
				}
				void UpdateConfig(Features options, bool set) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, set);
					Config.Instance.FireConfigChangedEvent(options);
				}
			}
		}
	}
}
