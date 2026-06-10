using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CLR;
using R = Codist.Properties.Resources;

namespace Codist.Options;

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
			readonly OptionBox<Features> _SyntaxHighlight, _SuperQuickInfo, _SmartBar, _NavigationBar, _ScrollbarMarker, _JumpListEnhancer, _WrapText, _AutoSurround, _FileBrowser;
			readonly OptionBox<Features>[] _Options;
			readonly Note _NoticeBox;

			public PageControl() {
				Thickness linkMargin = new(23, 0, 3, 0);
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
							(_WrapText = o.CreateOptionBox(Features.WrapText, UpdateConfig, R.OT_WrapText)
								.SetLazyToolTip(() => R.OT_WrapTextTip)),
							(_AutoSurround = o.CreateOptionBox(Features.AutoSurround, UpdateConfig, R.T_AutoSurround)
								.SetLazyToolTip(() => R.OT_AutoSurround)),
							(_FileBrowser = o.CreateOptionBox(Features.FileBrowser, UpdateConfig, R.T_FileBrowser)
								.SetLazyToolTip(() => R.OT_FileBrowser))
						}
					},
					(_NoticeBox = new Note(R.OT_FeatureChangesTip) { BorderThickness = WpfHelper.TinyMargin, Visibility = Visibility.Collapsed }),

					new TitleBox(R.OT_ThankYou),
					new TextBlock { Margin = WpfHelper.MiddleTopMargin }.Append(R.OT_ProjectWebSite),
					new TextBlock { Margin = linkMargin }.AppendLink("github.com/wmjordan/Codist", "https://github.com/wmjordan/Codist", R.CMDT_GotoProjectWebSite),
					new TextBlock { Margin = WpfHelper.MiddleTopMargin }.Append(R.OT_ReportBugsAndSuggestions),
					new TextBlock { Margin = linkMargin }.AppendLink("github.com/wmjordan/Codist/issues", "https://github.com/wmjordan/Codist/issues", R.CMDT_PostIssue),
					new TextBlock { Margin = WpfHelper.MiddleTopMargin }.Append(R.OT_LatestRelease),
					new TextBlock { Margin = linkMargin }.AppendLink("github.com/wmjordan/Codist/releases", "https://github.com/wmjordan/Codist/releases", R.CMDT_GotoProjectReleasePage),
					new TitleBox(R.OT_SupportCodst),
					new TextBlock { Margin = linkMargin }.AppendLink(R.CMD_DonateLink, "https://www.paypal.me/wmzuo/19.99", R.CMDT_OpenDonatePage),
					new TextBlock { Margin = linkMargin }.AppendLink(R.CMD_WechatDonateLink, ShowWechatQrCode, R.CMDT_OpenWechatQrCode),
					new DescriptionBox(R.OT_DonateLinkTip)
				);
				_Options = [_SyntaxHighlight, _SuperQuickInfo, _SmartBar, _NavigationBar, _ScrollbarMarker, _JumpListEnhancer, _WrapText, _AutoSurround, _FileBrowser];
				foreach (var item in _Options) {
					item.MinWidth = 150;
					item.Margin = WpfHelper.MiddleMargin;
					if (!item.CeqAny(_JumpListEnhancer, _AutoSurround)) {
						item.PreviewMouseDown += HighlightNoticeBox;
					}
				}
			}

			protected override void LoadConfig(Config config) {
				var o = config.Features;
				Array.ForEach(_Options, i => i.UpdateWithOption(o));
			}

			void HighlightNoticeBox(object sender, MouseButtonEventArgs e) {
				_NoticeBox.Highlight();
				_NoticeBox.Visibility = Visibility.Visible;
				foreach (var item in _Options) {
					item.PreviewMouseDown -= HighlightNoticeBox;
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
