using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AppHelpers;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using R = Codist.Properties.Resources;

namespace Codist.Options
{
	[ToolboxItem(false)]
	abstract class OptionsPage : UIElementDialogPage
	{
		protected static readonly Thickness SubOptionMargin = new Thickness(24, 0, 0, 0);
		protected const double MinColumnWidth = 230;
		Action<Config> _ConfigChangeHandler;
		int _UILock;

		protected OptionsPage() {
			Config.Loaded += (s, args) => LoadConfig(s as Config);
		}

		void LoadConfig(Config config) {
			if (Interlocked.CompareExchange(ref _UILock, 1, 0) == 0) {
				_ConfigChangeHandler?.Invoke(config);
				_UILock = 0;
			}
		}

		protected abstract Features Feature { get; }
		protected UserControl Control { get; set; }
		internal bool IsConfigUpdating => _UILock != 0;

		protected override void OnActivate(CancelEventArgs e) {
			base.OnActivate(e);
			if (Feature != Features.None && Control != null) {
				Control.IsEnabled = Config.Instance.Features.MatchFlags(Feature);
			}
			Config.Instance.BeginUpdate();
		}

		protected override void OnClosed(EventArgs e) {
			base.OnClosed(e);
			Config.Instance.EndUpdate(false);
		}

		protected override void OnApply(PageApplyEventArgs e) {
			base.OnApply(e);
			Config.Instance.EndUpdate(e.ApplyBehavior == ApplyKind.Apply);
		}

		//protected override void OnDeactivate(CancelEventArgs e) {
		//	base.OnDeactivate(e);
		//}

		internal void AddConfigChangeHandler(Action<Config> action) {
			_ConfigChangeHandler += action;
		}
	}

	abstract class OptionsPageContainer : UserControl
	{
		readonly TabControl _Tabs;

		public OptionsPage Page { get; }

		protected OptionsPageContainer(OptionsPage page) {
			Page = page;
			Content = _Tabs = new TabControl { Margin = WpfHelper.SmallMargin };
			page.AddConfigChangeHandler(LoadConfig);
		}

		public void AddPage(object title, params UIElement[] contents) {
			var p = new StackPanel {
				Margin = WpfHelper.SmallMargin,
			};
			foreach (var item in contents) {
				p.Children.Add(new Border { Child = item, Padding = WpfHelper.SmallMargin });
			}
			_Tabs.Items.Add(new TabItem {
				Header = title,
				Content = new ScrollViewer {
					Content = p,
					VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
					Margin = WpfHelper.ScrollerMargin
				}
			});
		}

		protected abstract void LoadConfig(Config config);
	}

	static class OptionPageControlHelper
	{
		public static OptionBox<TOption> CreateOptionBox<TOption> (this TOption initialValue, TOption option, Action<TOption, bool> checkEventHandler, string title) where TOption : struct, Enum {
			return new OptionBox<TOption>(initialValue, option, checkEventHandler) {
				Content = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap }
			};
		}
		public static OptionBox CreateOptionBox(bool initialValue, Action<bool?> checkEventHandler, string title) {
			return new OptionBox(initialValue, checkEventHandler) {
				Content = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap }
			};
		}

		public static void BindDependentOptionControls(this System.Windows.Controls.Primitives.ToggleButton checkBox, params UIElement[] dependentControls) {
			checkBox.Checked += (s, args) => Array.ForEach(dependentControls, c => c.IsEnabled = true);
			checkBox.Unchecked += (s, args) => Array.ForEach(dependentControls, c => c.IsEnabled = false);
		}
	}

	[Guid("3B2F0A1D-5279-496B-A342-33F083404A80")]
	sealed class GeneralOptionsPage : OptionsPage
	{
		UIElement _Child;

		protected override Features Feature => Features.None;
		protected override UIElement Child => _Child ?? (_Child = new PageControl(this));

		sealed class PageControl : OptionsPageContainer
		{
			readonly OptionBox<Features> _SyntaxHighlight, _SuperQuickInfo, _SmartBar, _NavigationBar, _ScrollbarMarker;
			readonly OptionBox<Features>[] _Options;
			readonly Button _LoadButton, _SaveButton;
			readonly Note _NoticeBox;

			public PageControl(OptionsPage page) : base(page) {
				var o = Config.Instance.Features;
				AddPage(R.OT_General,
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
								.SetLazyToolTip(() => R.OT_ScrollbarMarkerTip))
						}
					},
					_NoticeBox = new Note(R.OT_FeatureChangesTip) { BorderThickness = WpfHelper.TinyMargin, Visibility = Visibility.Collapsed },

					new TitleBox(R.OT_ConfigurationFile),
					new DescriptionBox(R.OT_ConfigurationFileTip),
					new WrapPanel {
						Children = {
							(_LoadButton = new Button { Name = "_Load", Content = R.CMD_Load, ToolTip = R.OT_LoadConfigFileTip }),
							(_SaveButton = new Button { Name = "_Save", Content = R.CMD_Save, ToolTip = R.OT_SaveConfigFileTip })
						}
					}
					);
				AddPage(R.OT_About,
					new TitleBox(R.OT_ThankYou),
					new Note(R.OT_ProjectWebSite),
					new TextBlock { Margin = new Thickness(23, 0, 3, 0) }.AppendLink("github.com/wmjordan/Codist", "https://github.com/wmjordan/Codist", R.CMD_GotoProjectWebSite),
					new Note(R.OT_ReportBugsAndSuggestions),
					new TextBlock { Margin = new Thickness(23, 0, 3, 0) }.AppendLink("github.com/wmjordan/Codist/issues", "https://github.com/wmjordan/Codist/issues", R.CMD_PostIssue),
					new Note(R.OT_LatestRelease),
					new TextBlock { Margin = new Thickness(23, 0, 3, 0) }.AppendLink("github.com/wmjordan/Codist/releases", "https://github.com/wmjordan/Codist/releases", R.CMD_GotoProjectReleasePage),
					new Note(R.OT_SupportCodst),
					new TextBlock { Margin = new Thickness(23, 0, 3, 0) }.AppendLink(R.CMD_DonateLink, "https://www.paypal.me/wmzuo/19.99", R.CMDT_OpenDonatePage),
					new TextBlock { Margin = new Thickness(23, 0, 3, 0) }.AppendLink(R.CMD_WechatDonateLink, ShowWechatQrCode, R.CMDT_OpenWechatQrCode),
					new DescriptionBox(R.OT_DonateLinkTip)
					);
				_Options = new[] { _SyntaxHighlight, _SuperQuickInfo, _SmartBar, _NavigationBar, _ScrollbarMarker };
				foreach (var item in _Options) {
					item.MinWidth = 120;
					item.Margin = WpfHelper.MiddleMargin;
					item.PreviewMouseDown += HighlightNoticeBox;
				}
				foreach (var item in new[] { _LoadButton, _SaveButton }) {
					item.MinWidth = 120;
					item.Margin = WpfHelper.MiddleMargin;
					item.Click += LoadOrSaveConfig;
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
			}

			void HighlightNoticeBox(object sender, MouseButtonEventArgs e) {
				_NoticeBox.BorderBrush = SystemColors.HighlightBrush;
				_NoticeBox.Background = SystemColors.HighlightBrush.Alpha(0.3);
				_NoticeBox.Visibility = Visibility.Visible;
				foreach (var item in _Options) {
					item.PreviewMouseDown -= HighlightNoticeBox;
				}
			}

			protected override void LoadConfig(Config config) {
				var o = config.Features;
				Array.ForEach(_Options, i => i.UpdateWithOption(o));
			}

			void UpdateConfig(Features options, bool set) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.Set(options, set);
				Config.Instance.FireConfigChangedEvent(options);
			}

			void LoadOrSaveConfig(object sender, EventArgs args) {
				if (sender == _LoadButton) {
					var d = new OpenFileDialog {
						Title = R.T_LoadConfig,
						FileName = "Codist.json",
						DefaultExt = "json",
						Filter = R.T_ConfigFileFilter
					};
					if (d.ShowDialog() != true) {
						return;
					}
					try {
						Config.LoadConfig(d.FileName);
						if (Version.TryParse(Config.Instance.Version, out var newVersion)
							&& newVersion > Version.Parse(Config.CurrentVersion)) {
							MessageBox.Show(R.T_NewVersionConfig, nameof(Codist), MessageBoxButton.OK, MessageBoxImage.Information);
						}
						System.IO.File.Copy(d.FileName, Config.ConfigPath, true);
					}
					catch (Exception ex) {
						MessageBox.Show(R.T_ErrorLoadingConfig + ex.Message, nameof(Codist));
					}
				}
				else {
					var d = new SaveFileDialog {
						Title = R.T_SaveConfig,
						FileName = "Codist.json",
						DefaultExt = "json",
						Filter = R.T_ConfigFileFilter
					};
					if (d.ShowDialog() != true) {
						return;
					}
					Config.Instance.SaveConfig(d.FileName);
				}
			}
		}
	}

	[Guid("09020157-B191-464F-8F9B-F3100596BDF0")]
	sealed class SuperQuickInfoOptionsPage : OptionsPage
	{
		UIElement _Child;

		protected override Features Feature => Features.SuperQuickInfo;
		protected override UIElement Child => _Child ?? (_Child = new PageControl(this));

		sealed class PageControl : OptionsPageContainer
		{
			readonly OptionBox<QuickInfoOptions> _CtrlQuickInfo, _Selection, _Color, _ClickAndGo;
			readonly OptionBox<QuickInfoOptions> _OverrideDefaultDocumentation, _DocumentationFromBaseType, _DocumentationFromInheritDoc, _TextOnlyDoc, _ReturnsDoc, _RemarksDoc, _ExceptionDoc, _SeeAlsoDoc, _ExampleDoc, _AlternativeStyle;
			readonly OptionBox<QuickInfoOptions> _Attributes, _BaseType, _BaseTypeInheritence, _Declaration, _SymbolLocation, _Interfaces, _InterfacesInheritence, _NumericValues, _String, _Parameter, _InterfaceImplementations, _TypeParameters, _NamespaceTypes, _MethodOverload, _InterfaceMembers;
			readonly OptionBox<QuickInfoOptions>[] _Options;
			readonly Controls.IntegerBox _MaxWidth, _MaxHeight, _ExtraHeight;

			public PageControl(OptionsPage page) : base(page) {
				var o = Config.Instance.QuickInfoOptions;
				AddPage(R.OT_General,
					new Note(R.OT_QuickInfoNote),
					_CtrlQuickInfo = o.CreateOptionBox(QuickInfoOptions.CtrlQuickInfo, UpdateConfig, R.OT_HideQuickInfoUntilShift)
						.SetLazyToolTip(() => R.OT_HideQuickInfoUntilShiftTip),
					_Selection = o.CreateOptionBox(QuickInfoOptions.Selection, UpdateConfig, R.OT_SelectionInfo)
						.SetLazyToolTip(() => R.OT_SelectionInfoTip),
					_Color = o.CreateOptionBox(QuickInfoOptions.Color, UpdateConfig, R.OT_ColorInfo)
						.SetLazyToolTip(() => R.OT_ColorInfoTip),

					new TitleBox(R.OT_ItemSize),
					new DescriptionBox(R.OT_ItemSizeNote),
					new WrapPanel {
						Children = {
							new StackPanel().MakeHorizontal()
								.Add(new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append(R.OT_MaxWidth))
								.Add(_MaxWidth = new Controls.IntegerBox((int)Config.Instance.QuickInfoMaxWidth) { Minimum = 0, Maximum = 5000, Step = 100 })
								.SetLazyToolTip(() => R.OT_MaxWidthTip),
							new StackPanel().MakeHorizontal()
								.Add(new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append(R.OT_MaxHeight))
								.Add(_MaxHeight = new Controls.IntegerBox((int)Config.Instance.QuickInfoMaxHeight) { Minimum = 0, Maximum = 5000, Step = 50 })
								.SetLazyToolTip(() => R.OT_MaxHeightTip),
						}
					}.ForEachChild((FrameworkElement b) => b.MinWidth = MinColumnWidth),
					new DescriptionBox(R.OT_UnlimitedSize),
					new StackPanel().MakeHorizontal()
						.Add(new TextBlock { MinWidth = 240, Margin = WpfHelper.SmallHorizontalMargin, Text = R.OT_ExtraXmlDocSize })
						.Add(_ExtraHeight = new Controls.IntegerBox((int)Config.Instance.QuickInfoXmlDocExtraHeight) { Minimum = 0, Maximum = 1000, Step = 50 })
						.SetLazyToolTip(() => R.OT_ExtraXmlDocSizeTip)
				);

				AddPage(R.OT_CSharp,
					new Note(R.OT_CSharpNote),
					new TitleBox(R.OT_QuickInfoOverride),
					new DescriptionBox(R.OT_QuickInfoOverrideNote),
					_ClickAndGo = o.CreateOptionBox(QuickInfoOptions.ClickAndGo, UpdateConfig, R.OT_ClickAndGo)
						.SetLazyToolTip(() => R.OT_ClickAndGoTip),
					_OverrideDefaultDocumentation = o.CreateOptionBox(QuickInfoOptions.OverrideDefaultDocumentation, UpdateConfig, R.OT_OverrideXmlDoc)
						.SetLazyToolTip(() => R.OT_OverrideXmlDocTip),
					_DocumentationFromBaseType = o.CreateOptionBox(QuickInfoOptions.DocumentationFromBaseType, UpdateConfig, R.OT_InheritXmlDoc)
						.SetLazyToolTip(() => R.OT_InheritXmlDocTip),
					_DocumentationFromInheritDoc = o.CreateOptionBox(QuickInfoOptions.DocumentationFromInheritDoc, UpdateConfig, R.OT_InheritDoc)
						.SetLazyToolTip(() => R.OT_InheritDocTip),
					_TextOnlyDoc = o.CreateOptionBox(QuickInfoOptions.TextOnlyDoc, UpdateConfig, R.OT_TextOnlyXmlDoc)
						.SetLazyToolTip(() => R.OT_TextOnlyXmlDocTip),
					_ReturnsDoc = o.CreateOptionBox(QuickInfoOptions.ReturnsDoc, UpdateConfig, R.OT_ShowReturnsXmlDoc)
						.SetLazyToolTip(() => R.OT_ShowReturnsXmlDocTip),
					_RemarksDoc = o.CreateOptionBox(QuickInfoOptions.RemarksDoc, UpdateConfig, R.OT_ShowRemarksXmlDoc)
						.SetLazyToolTip(() => R.OT_ShowRemarksXmlDocTip),
					_ExceptionDoc = o.CreateOptionBox(QuickInfoOptions.ExceptionDoc, UpdateConfig, R.OT_ShowExceptionXmlDoc)
						.SetLazyToolTip(() => R.OT_ShowExceptionXmlDocTip),
					_SeeAlsoDoc = o.CreateOptionBox(QuickInfoOptions.SeeAlsoDoc, UpdateConfig, R.OT_ShowSeeAlsoXmlDoc)
						.SetLazyToolTip(() => R.OT_ShowSeeAlsoXmlDocTip),
					_ExampleDoc = o.CreateOptionBox(QuickInfoOptions.ExampleDoc, UpdateConfig, R.OT_ShowExampleXmlDoc)
						.SetLazyToolTip(() => R.OT_ShowExampleXmlDocTip),
					_AlternativeStyle = o.CreateOptionBox(QuickInfoOptions.AlternativeStyle, UpdateConfig, R.OT_AlternativeStyle)
						.SetLazyToolTip(() => R.OT_AlternativeStyleTip),

					new TitleBox(R.OT_AdditionalQuickInfo),
					new DescriptionBox(R.OT_AdditionalQuickInfoNote),
					_Attributes = o.CreateOptionBox(QuickInfoOptions.Attributes, UpdateConfig, R.OT_Attributes)
						.SetLazyToolTip(() => R.OT_AttributesTip),
					_BaseType = o.CreateOptionBox(QuickInfoOptions.BaseType, UpdateConfig, R.OT_BaseType)
						.SetLazyToolTip(() => R.OT_BaseTypeTip),
					_BaseTypeInheritence = o.CreateOptionBox(QuickInfoOptions.BaseTypeInheritence, UpdateConfig, R.OT_AllAncestorTypes)
						.SetLazyToolTip(() => R.OT_AllAncestorTypesTip),
					_Declaration = o.CreateOptionBox(QuickInfoOptions.Declaration, UpdateConfig, R.OT_Declaration)
						.SetLazyToolTip(() => R.OT_DesclarationTip),
					_Interfaces = o.CreateOptionBox(QuickInfoOptions.Interfaces, UpdateConfig, R.OT_Interfaces)
						.SetLazyToolTip(() => R.OT_InterfacesTip),
					_InterfacesInheritence = o.CreateOptionBox(QuickInfoOptions.InterfacesInheritence, UpdateConfig, R.OT_InheritedInterfaces)
						.SetLazyToolTip(() => R.OT_InheritedInterfacesTip),
					_InterfaceImplementations = o.CreateOptionBox(QuickInfoOptions.InterfaceImplementations, UpdateConfig, R.OT_InterfaceImplementation)
						.SetLazyToolTip(() => R.OT_InterfaceImplementationTip),
					_InterfaceMembers = o.CreateOptionBox(QuickInfoOptions.InterfaceMembers, UpdateConfig, R.OT_InterfaceMembers)
						.SetLazyToolTip(() => R.OT_InterfaceMembersTip),
					_MethodOverload = o.CreateOptionBox(QuickInfoOptions.MethodOverload, UpdateConfig, R.OT_MethodOverloads)
						.SetLazyToolTip(() => R.OT_MethodOverloadsTip),
					_Parameter = o.CreateOptionBox(QuickInfoOptions.Parameter, UpdateConfig, R.OT_ParameterOfMethod)
						.SetLazyToolTip(() => R.OT_ParameterOfMethodTip),
					_TypeParameters = o.CreateOptionBox(QuickInfoOptions.TypeParameters, UpdateConfig, R.OT_TypeParameter)
						.SetLazyToolTip(() => R.OT_TypeParameterTip),
					_SymbolLocation = o.CreateOptionBox(QuickInfoOptions.SymbolLocation, UpdateConfig, R.OT_SymbolLocation)
						.SetLazyToolTip(() => R.OT_SymbolLocationTip),
					_NumericValues = o.CreateOptionBox(QuickInfoOptions.NumericValues, UpdateConfig, R.OT_NumericForms)
						.SetLazyToolTip(() => R.OT_NumericFormsTip),
					_String = o.CreateOptionBox(QuickInfoOptions.String, UpdateConfig, R.OT_StringInfo)
						.SetLazyToolTip(() => R.OT_StringInfoTip)
					);

				_MaxHeight.ValueChanged += UpdateQuickInfoSize;
				_MaxWidth.ValueChanged += UpdateQuickInfoSize;
				_ExtraHeight.ValueChanged += UpdateQuickInfoSize;
				_Options = new[] { _CtrlQuickInfo, _Selection, _Color, _ClickAndGo, _OverrideDefaultDocumentation, _DocumentationFromBaseType, _DocumentationFromInheritDoc, _TextOnlyDoc, _ReturnsDoc, _RemarksDoc, _ExceptionDoc, _SeeAlsoDoc, _ExampleDoc, _AlternativeStyle, _Attributes, _BaseType, _BaseTypeInheritence, _Declaration, _SymbolLocation, _Interfaces, _InterfacesInheritence, _NumericValues, _String, _Parameter, _InterfaceImplementations, _TypeParameters, /*_NamespaceTypes, */_MethodOverload, _InterfaceMembers };
				foreach (var item in new[] { _DocumentationFromBaseType, _DocumentationFromInheritDoc, _TextOnlyDoc, _ReturnsDoc, _RemarksDoc, _ExceptionDoc, _SeeAlsoDoc, _ExampleDoc, _BaseTypeInheritence, _InterfacesInheritence }) {
					item.WrapMargin(SubOptionMargin);
				}
				_OverrideDefaultDocumentation.BindDependentOptionControls(_DocumentationFromBaseType, _DocumentationFromInheritDoc, _TextOnlyDoc, _ReturnsDoc, _RemarksDoc, _ExceptionDoc, _SeeAlsoDoc, _ExampleDoc);
				_BaseType.BindDependentOptionControls(_BaseTypeInheritence);
				_Interfaces.BindDependentOptionControls(_InterfacesInheritence);
			}

			protected override void LoadConfig(Config config) {
				var o = config.QuickInfoOptions;
				Array.ForEach(_Options, i => i.UpdateWithOption(o));
				_MaxHeight.Value = (int)config.QuickInfoMaxHeight;
				_MaxWidth.Value = (int)config.QuickInfoMaxWidth;
				_ExtraHeight.Value = (int)config.QuickInfoXmlDocExtraHeight;
			}

			void UpdateConfig(QuickInfoOptions options, bool set) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.Set(options, set);
				Config.Instance.FireConfigChangedEvent(Features.SuperQuickInfo);
			}

			void UpdateQuickInfoSize(object sender, DependencyPropertyChangedEventArgs args) {
				if (sender == _MaxHeight) {
					Config.Instance.QuickInfoMaxHeight = _MaxHeight.Value;
				}
				else if (sender == _MaxWidth) {
					Config.Instance.QuickInfoMaxWidth = _MaxWidth.Value;
				}
				else if (sender == _ExtraHeight) {
					Config.Instance.QuickInfoXmlDocExtraHeight = _ExtraHeight.Value;
				}
				Config.Instance.FireConfigChangedEvent(Features.SuperQuickInfo);
			}
		}
	}

	[Guid("CF07BC0B-EF35-499B-8E7A-595638E93474")]
	sealed class SyntaxHighlightOptionsPage : OptionsPage
	{
		UIElement _Child;

		protected override Features Feature => Features.SyntaxHighlight;
		protected override UIElement Child => _Child ?? (_Child = new PageControl(this));

		sealed class PageControl : OptionsPageContainer
		{
			readonly OptionBox<SpecialHighlightOptions> _CommentTaggerBox, _SearchResultBox;

			public PageControl(OptionsPage page) : base(page) {
				var o = Config.Instance.SpecialHighlightOptions;
				AddPage(R.OT_General,
					new Note(new TextBlock()
						.Append(R.OT_ConfigSyntaxNote)
						.AppendLink(R.CMD_ConfigureSyntaxHighlight, _ => Commands.SyntaxCustomizerWindowCommand.Execute(null, EventArgs.Empty), R.CMDT_ConfigureSyntaxHighlight)),
					new TitleBox(R.OT_ExtraHighlight),
					_CommentTaggerBox = o.CreateOptionBox(SpecialHighlightOptions.SpecialComment, UpdateConfig, R.OT_EnableCommentTagger),
					_SearchResultBox = o.CreateOptionBox(SpecialHighlightOptions.SearchResult, UpdateConfig, R.OT_HighlightSearchResults),
					new DescriptionBox("*: The highlight search results feature is under development and may not work as expected")
					);
			}

			void UpdateConfig(SpecialHighlightOptions options, bool set) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.Set(options, set);
				Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
			}

			protected override void LoadConfig(Config config) {
				_CommentTaggerBox.UpdateWithOption(config.SpecialHighlightOptions);
				_SearchResultBox.UpdateWithOption(config.SpecialHighlightOptions);
			}
		}
	}

	[Guid("74A725E0-6B48-44F6-84AD-A0FD9D8BF710")]
	sealed class SmartBarOptionsPage : OptionsPage
	{
		UIElement _Child;

		protected override Features Feature => Features.SmartBar;
		protected override UIElement Child => _Child ?? (_Child = new PageControl(this));

		sealed class PageControl : OptionsPageContainer
		{
			readonly OptionBox<SmartBarOptions> _ShiftToggleDisplay, _ManualDisplaySmartBar;
			readonly OptionBox<SmartBarOptions>[] _Options;

			public PageControl(OptionsPage page) : base(page) {
				var o = Config.Instance.SmartBarOptions;
				AddPage(R.OT_Behavior,
					new Note(R.OT_BehaviorTip),
					_ManualDisplaySmartBar = o.CreateOptionBox(SmartBarOptions.ManualDisplaySmartBar, UpdateConfig, R.OT_ManualSmartBar)
						.SetLazyToolTip(() => R.OT_ManualSmartBarTip),
					_ShiftToggleDisplay = o.CreateOptionBox(SmartBarOptions.ShiftToggleDisplay, UpdateConfig, R.OT_ToggleSmartBar)
						.SetLazyToolTip(() => R.OT_ToggleSmartBarTip),
					new DescriptionBox(R.OT_ToggleSmartBarNote)
					);

				_Options = new[] { _ShiftToggleDisplay, _ManualDisplaySmartBar };
			}

			protected override void LoadConfig(Config config) {
				var o = config.SmartBarOptions;
				Array.ForEach(_Options, i => i.UpdateWithOption(o));
			}

			void UpdateConfig(SmartBarOptions options, bool set) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.Set(options, set);
				Config.Instance.FireConfigChangedEvent(Features.SmartBar);
			}
		}
	}

	[Guid("CEBD6083-49F4-4579-94FF-C2774FFB4F9A")]
	sealed class NavigationBarPage : OptionsPage
	{
		UIElement _Child;

		protected override Features Feature => Features.NaviBar;
		protected override UIElement Child => _Child ?? (_Child = new PageControl(this));

		sealed class PageControl : OptionsPageContainer
		{
			readonly OptionBox<NaviBarOptions> _SyntaxDetail, _SymbolToolTip, _RegionOnBar, _StripRegionNonLetter, _RangeHighlight,
				_ParameterList, _ParameterListShowParamName, _FieldValue, _AutoPropertyAnnotation, _PartialClassMember, _BaseClassMember, _Region, _RegionInMember, _LineOfCode;
			readonly OptionBox<NaviBarOptions>[] _Options;

			public PageControl(OptionsPage page) : base(page) {
				var o = Config.Instance.NaviBarOptions;
				AddPage(R.OT_General,
					new Note(R.OT_NaviBarNote),

					new TitleBox(R.OT_CSharpNaviBar),
					_SyntaxDetail = o.CreateOptionBox(NaviBarOptions.SyntaxDetail, UpdateConfig, R.OT_SyntaxDetail)
						.SetLazyToolTip(() => R.OT_SyntaxDetailTip),
					_SymbolToolTip = o.CreateOptionBox(NaviBarOptions.SymbolToolTip, UpdateConfig, R.OT_SymbolToolTip)
						.SetLazyToolTip(() => R.OT_SymbolToolTipTip),
					_RegionOnBar = o.CreateOptionBox(NaviBarOptions.RegionOnBar, UpdateConfig, R.OT_Region)
						.SetLazyToolTip(() => R.OT_RegionTip),
					_StripRegionNonLetter = o.CreateOptionBox(NaviBarOptions.StripRegionNonLetter, UpdateConfig, R.OT_TrimNonLetterRegion)
						.SetLazyToolTip(() => R.OT_TrimNonLetterRegionTip),
					_RangeHighlight = o.CreateOptionBox(NaviBarOptions.RangeHighlight, UpdateConfig, R.OT_HighlightSyntaxRange)
						.SetLazyToolTip(() => R.OT_HighlightSyntaxRangeTip),

					new TitleBox(R.OT_CSharpNaviBarMenu),
					_ParameterList = o.CreateOptionBox(NaviBarOptions.ParameterList, UpdateConfig, R.OT_MethodParameterList)
						.SetLazyToolTip(() => R.OT_MethodParameterListTip),
					_ParameterListShowParamName = o.CreateOptionBox(NaviBarOptions.ParameterListShowParamName, UpdateConfig, R.OT_ShowMethodParameterName)
						.SetLazyToolTip(() => R.OT_ShowMethodParameterNameTip),
					_FieldValue = o.CreateOptionBox(NaviBarOptions.FieldValue, UpdateConfig, R.OT_ValueOfFields)
						.SetLazyToolTip(() => R.OT_ValueOfFieldsTip),
					_AutoPropertyAnnotation = o.CreateOptionBox(NaviBarOptions.AutoPropertyAnnotation, UpdateConfig, R.OT_PropertyAccessors)
						.SetLazyToolTip(() => R.OT_PropertyAccessorsTip),
					_PartialClassMember = o.CreateOptionBox(NaviBarOptions.PartialClassMember, UpdateConfig, R.OT_IncludePartialTypeMembers)
						.SetLazyToolTip(() => R.OT_IncludePartialTypeMembersTip),
					_BaseClassMember = o.CreateOptionBox(NaviBarOptions.BaseClassMember, UpdateConfig, R.OT_IncludeBaseTypeMembers)
						.SetLazyToolTip(() => R.OT_IncludeBaseTypeMembersTip),
					_Region = o.CreateOptionBox(NaviBarOptions.Region, UpdateConfig, R.OT_IncludeRegions)
						.SetLazyToolTip(() => R.OT_IncludeRegionsTip),
					_RegionInMember = o.CreateOptionBox(NaviBarOptions.RegionInMember, UpdateConfig, R.OT_IncludeMemberRegions)
						.SetLazyToolTip(() => R.OT_IncludeMemberRegionsTip),
					_LineOfCode = o.CreateOptionBox(NaviBarOptions.LineOfCode, UpdateConfig, R.OT_LineOfCode)
						.SetLazyToolTip(() => R.OT_LineOfCodeTip),
					new DescriptionBox(R.OT_NaviBarUpdateNote),

					new TitleBox(R.OT_ShortcutKeys),
					new DescriptionBox(R.OT_NaviBarShortcutKeys),
					new DescriptionBox(R.OT_ShortcutKeysNote)
					);

				_Options = new[] { _SyntaxDetail, _SymbolToolTip, _RegionOnBar, _StripRegionNonLetter, _RangeHighlight,
				_ParameterList, _ParameterListShowParamName, _FieldValue, _AutoPropertyAnnotation, _PartialClassMember, _BaseClassMember, _Region, _RegionInMember, _LineOfCode };
				foreach (var item in new[] { _StripRegionNonLetter, _ParameterListShowParamName, _AutoPropertyAnnotation, _RegionInMember }) {
					item.WrapMargin(SubOptionMargin);
				}
				_RegionOnBar.BindDependentOptionControls(_StripRegionNonLetter);
				_ParameterList.BindDependentOptionControls(_ParameterListShowParamName);
				_FieldValue.BindDependentOptionControls(_AutoPropertyAnnotation);
				_Region.BindDependentOptionControls(_RegionInMember);
			}

			protected override void LoadConfig(Config config) {
				var o = config.NaviBarOptions;
				Array.ForEach(_Options, i => i.UpdateWithOption(o));
			}

			void UpdateConfig(NaviBarOptions options, bool set) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.Set(options, set);
				Config.Instance.FireConfigChangedEvent(Features.NaviBar);
			}
		}
	}

	[Guid("CA523740-3DF6-4A0A-B01B-3C5D4CBD7AFF")]
	sealed class ScrollBarMarkerPage : OptionsPage
	{
		UIElement _Child;

		protected override Features Feature => Features.ScrollbarMarkers;
		protected override UIElement Child => _Child ?? (_Child = new PageControl(this));

		sealed class PageControl : OptionsPageContainer
		{
			readonly OptionBox<MarkerOptions> _LineNumber, _Selection, _SpecialComment, _MarkerDeclarationLine, _LongMemberDeclaration, _TypeDeclaration, _MethodDeclaration, _RegionDirective, _CompilerDirective, _SymbolReference;
			readonly OptionBox<MarkerOptions>[] _Options;

			public PageControl(OptionsPage page) : base(page) {
				var o = Config.Instance.MarkerOptions;
				AddPage(R.OT_General,
					new TitleBox(R.OT_AllLanguages),
					new DescriptionBox(R.OT_AllLanguagesNote),
					_LineNumber = o.CreateOptionBox(MarkerOptions.LineNumber, UpdateConfig, R.OT_LineNumber)
						.SetLazyToolTip(() => R.OT_LineNumberTip),
					_Selection = o.CreateOptionBox(MarkerOptions.Selection, UpdateConfig, R.OT_Selection)
						.SetLazyToolTip(() => R.OT_SelectionTip),
					_SpecialComment = o.CreateOptionBox(MarkerOptions.SpecialComment, UpdateConfig, R.OT_TaggedComments)
						.SetLazyToolTip(() => R.OT_TaggedCommentsTip),

					new TitleBox(R.OT_CSharp),
					new DescriptionBox(R.OT_CSharpMarkerNote),
					_MarkerDeclarationLine = o.CreateOptionBox(MarkerOptions.MemberDeclaration, UpdateConfig, R.OT_MemberDeclarationLine)
						.SetLazyToolTip(() => R.OT_MemberDeclarationLineTip),
					_LongMemberDeclaration = o.CreateOptionBox(MarkerOptions.LongMemberDeclaration, UpdateConfig, R.OT_LongMethodName)
						.SetLazyToolTip(() => R.OT_LongMethodNameTip),
					_TypeDeclaration = o.CreateOptionBox(MarkerOptions.TypeDeclaration, UpdateConfig, R.OT_TypeName)
						.SetLazyToolTip(() => R.OT_TypeNameTip),
					_MethodDeclaration = o.CreateOptionBox(MarkerOptions.MethodDeclaration, UpdateConfig, R.OT_MethodDeclarationSpot)
						.SetLazyToolTip(() => R.OT_MethodDeclarationSpotTip),
					_RegionDirective = o.CreateOptionBox(MarkerOptions.RegionDirective, UpdateConfig, R.OT_RegionName)
						.SetLazyToolTip(() => R.OT_RegionNameTip),
					_CompilerDirective = o.CreateOptionBox(MarkerOptions.CompilerDirective, UpdateConfig, R.OT_CompilerDirective)
						.SetLazyToolTip(() => R.OT_CompilerDirectiveTip),
					_SymbolReference = o.CreateOptionBox(MarkerOptions.SymbolReference, UpdateConfig, R.OT_MatchSymbol)
						.SetLazyToolTip(() => R.OT_MatchSymbolTip)
					);
				_Options = new[] { _LineNumber, _Selection, _SpecialComment, _MarkerDeclarationLine, _LongMemberDeclaration, _TypeDeclaration, _MethodDeclaration, _RegionDirective, _CompilerDirective, _SymbolReference };
				var declarationSubOptions = new[] { _LongMemberDeclaration, _TypeDeclaration, _MethodDeclaration, _RegionDirective };
				foreach (var item in declarationSubOptions) {
					item.WrapMargin(SubOptionMargin);
				}
				_MarkerDeclarationLine.BindDependentOptionControls(declarationSubOptions);
			}

			protected override void LoadConfig(Config config) {
				var o = config.MarkerOptions;
				Array.ForEach(_Options, i => i.UpdateWithOption(o));
			}

			void UpdateConfig(MarkerOptions options, bool set) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.Set(options, set);
				Config.Instance.FireConfigChangedEvent(Features.ScrollbarMarkers);
			}
		}
	}


	[Guid("3C54350C-A369-46F8-A74B-5180DA804DA1")]
	sealed class ExtensionDeveloperPage : OptionsPage
	{
		UIElement _Child;

		protected override Features Feature => Features.None;
		protected override UIElement Child => _Child ?? (_Child = new PageControl(this));

		sealed class PageControl : OptionsPageContainer
		{
			readonly OptionBox<BuildOptions> _BuildVsixAutoIncrement;
			readonly OptionBox<DeveloperOptions> _ShowDocumentContentType, _ShowSyntaxClassificationInfo;

			public PageControl(OptionsPage page) : base(page) {
				AddPage(R.OT_General,
					new Note(R.OT_ExtensionNote),

					new TitleBox(R.OT_SyntaxDiagnostics),
					new DescriptionBox(R.OT_SyntaxDiagnosticsNote),
					_ShowDocumentContentType = Config.Instance.DeveloperOptions.CreateOptionBox(DeveloperOptions.ShowDocumentContentType, UpdateConfig, R.OT_AddShowDocumentContentType)
						.SetLazyToolTip(() => R.OT_AddShowDocumentContentTypeTip),
					_ShowSyntaxClassificationInfo = Config.Instance.DeveloperOptions.CreateOptionBox(DeveloperOptions.ShowSyntaxClassificationInfo, UpdateConfig, R.OT_AddShowSyntaxClassifcationInfo)
						.SetLazyToolTip(() => R.OT_AddShowSyntaxClassifcationInfoTip),

					new TitleBox(R.OT_Build),
					_BuildVsixAutoIncrement = Config.Instance.BuildOptions.CreateOptionBox(BuildOptions.VsixAutoIncrement, UpdateConfig, R.OT_AutoIncrementVsixVersion)
						.SetLazyToolTip(() => R.OT_AutoIncrementVsixVersionTip)
					);
			}

			protected override void LoadConfig(Config config) {
				var o = config.BuildOptions;
				_BuildVsixAutoIncrement.UpdateWithOption(o);
			}

			void UpdateConfig(BuildOptions options, bool set) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.Set(options, set);
				Config.Instance.FireConfigChangedEvent(Features.None);
			}

			void UpdateConfig(DeveloperOptions options, bool set) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.Set(options, set);
				Config.Instance.FireConfigChangedEvent(Features.None);
			}

		}
	}

	[Guid("D04C5293-3BC6-4AEC-86CE-3922EE029DE4")]
	sealed class DisplayPage : OptionsPage
	{
		PageControl _Child;

		protected override Features Feature => Features.None;
		protected override UIElement Child => _Child ?? (_Child = new PageControl(this));

		sealed class PageControl : OptionsPageContainer
		{
			readonly Controls.IntegerBox _TopSpace, _BottomSpace;
			readonly OptionBox<DisplayOptimizations> _MainWindow, _CodeWindow, _MenuLayoutOverride;
			readonly OptionBox<BuildOptions> _BuildTimestamp;

			public PageControl(OptionsPage page) : base(page) {
				AddPage(R.OT_General,
					new TitleBox(R.OT_ExtraLineMargin),
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

					new TitleBox(R.OT_Output),
					new DescriptionBox(R.OT_OutputNote),
					new WrapPanel {
						Children = {
							Config.Instance.BuildOptions.CreateOptionBox(BuildOptions.BuildTimestamp, UpdateConfig, R.OT_BuildTimestamp).Set(ref _BuildTimestamp).SetLazyToolTip(() => R.OT_BuildTimestampTip)
						}
					}
					.ForEachChild((CheckBox b) => b.MinWidth = MinColumnWidth),

					new TitleBox(R.OT_LayoutOverride),
					new DescriptionBox(R.OT_LayoutOverrideNote),
					new WrapPanel {
						Children = {
							Config.Instance.DisplayOptimizations.CreateOptionBox(DisplayOptimizations.CompactMenu, UpdateMenuLayoutOption, R.OT_OverrideMainMenu).Set(ref _MenuLayoutOverride)
						}
					}
					.ForEachChild((CheckBox b) => b.MinWidth = MinColumnWidth)
					);
				_TopSpace.ValueChanged += _TopSpace_ValueChanged;
				_BottomSpace.ValueChanged += _BottomSpace_ValueChanged;

				_MenuLayoutOverride.IsEnabled = Application.Current.MainWindow
					.GetFirstVisualChild<Grid>(i => i.Name == "RootGrid")
					?.GetFirstVisualChild<Border>(i => i.Name == "MainWindowTitleBar")
					?.Child is DockPanel;
			}

			protected override void LoadConfig(Config config) {
				_TopSpace.Value = (int)config.TopSpace;
				_BottomSpace.Value = (int)config.BottomSpace;
				var o = config.DisplayOptimizations;
				_MainWindow.UpdateWithOption(o);
				_CodeWindow.UpdateWithOption(o);
				_BuildTimestamp.UpdateWithOption(config.BuildOptions);
				//_UseLayoutRounding.UpdateWithOption(o);
			}

			void UpdateCodeWindowDisplayOption(DisplayOptimizations options, bool value) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.Set(options, value);
				WpfHelper.SetUITextRenderOptions(TextEditorHelper.GetActiveWpfDocumentView()?.VisualElement, value);
				Config.Instance.FireConfigChangedEvent(Features.None);
			}

			void UpdateMainWindowDisplayOption(DisplayOptimizations options, bool value) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.Set(options, value);
				WpfHelper.SetUITextRenderOptions(Application.Current.MainWindow, value);
				Config.Instance.FireConfigChangedEvent(Features.None);
			}

			void UpdateConfig(BuildOptions options, bool set) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.Set(options, set);
				Config.Instance.FireConfigChangedEvent(Features.None);
			}

			void UpdateMenuLayoutOption(DisplayOptimizations options, bool value) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.Set(options, value);
				if (value) {
					Controls.LayoutOverrider.CompactMenu();
				}
				else {
					Controls.LayoutOverrider.UndoCompactMenu();
					//MessageBox.Show(R.T_LayoutOverrideRestore, nameof(Codist), MessageBoxButton.OK, MessageBoxImage.Information);
				}
				Config.Instance.FireConfigChangedEvent(Features.None);
			}

			void _BottomSpace_ValueChanged(object sender, DependencyPropertyChangedEventArgs e) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.BottomSpace = (int)e.NewValue;
				Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
			}

			void _TopSpace_ValueChanged(object sender, DependencyPropertyChangedEventArgs e) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.TopSpace = (int)e.NewValue;
				Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
			}
		}
	}

	[Guid("4BD9DEDE-B83D-4552-8197-45BF050E20CA")]
	sealed class WebSearchPage : OptionsPage
	{
		UIElement _Child;

		protected override Features Feature => Features.SmartBar;
		protected override UIElement Child => _Child ?? (_Child = new PageControl(this));

		sealed class PageControl : OptionsPageContainer
		{
			readonly TextBox _BrowserPath, _BrowserParameter, _SearchEngineName, _SearchEngineUrl;
			readonly ListBox _SearchEngineList;
			readonly Button _BrowseBrowserPath, _AddSearchButton, _RemoveSearchButton, _MoveUpSearchButton, _ResetSearchButton, _SaveSearchButton;

			public PageControl(OptionsPage page) : base(page) {
				AddPage(R.OT_WebSearch,
					new Note(R.OT_WebSearchNote),
					new TitleBox(R.OT_SearchEngines),
					new Grid {
						ColumnDefinitions = {
							new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), },
							new ColumnDefinition { Width = new GridLength(80, GridUnitType.Pixel) }
						},
						RowDefinitions = {
							new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
							new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) }
						},
						Children = {
							(_SearchEngineList = new ListView {
								Margin = WpfHelper.SmallMargin,
								View = new GridView {
									Columns = {
										new GridViewColumn { Header = R.OT_Name, Width = 100, DisplayMemberBinding = new Binding("Name") },
										new GridViewColumn { Header = R.OT_URLPattern, Width = 220, DisplayMemberBinding = new Binding("Pattern") }
									}
								}
							}),
							new Grid {
								Margin = WpfHelper.SmallMargin,
								ColumnDefinitions = {
									new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
									new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) },
								},
								RowDefinitions = {
									new RowDefinition { },
									new RowDefinition { },
									new RowDefinition { }
								},
								Children = {
									new Label { Content = R.OTC_Name, Width = 60 },
									(_SearchEngineName = new TextBox { IsEnabled = false, Margin = WpfHelper.SmallVerticalMargin }).SetValue(Grid.SetColumn, 1),
									new Label { Content = R.OTC_URL, Width = 60 }.SetValue(Grid.SetRow, 1),
									(_SearchEngineUrl = new TextBox { IsEnabled = false, Margin = WpfHelper.SmallVerticalMargin }).SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 1),
									new DescriptionBox(R.OT_SearchParamSubsitution).SetValue(Grid.SetRow, 2).SetValue(Grid.SetColumnSpan, 2)
								}
							}.SetValue(Grid.SetRow, 1),
							new StackPanel {
								Margin = WpfHelper.SmallMargin,
								Children = {
									(_RemoveSearchButton = new Button { Margin = WpfHelper.SmallVerticalMargin, Content = R.CMD_Remove }),
									(_MoveUpSearchButton = new Button { Margin = WpfHelper.SmallVerticalMargin, Content = R.CMD_MoveUp }),
									(_ResetSearchButton = new Button { Margin = WpfHelper.SmallVerticalMargin, Content = R.CMD_Reset }),
								}
							}.SetValue(Grid.SetColumn, 1),
							new StackPanel {
								Margin = WpfHelper.SmallMargin,
								Children = {
									(_AddSearchButton = new Button { Margin = WpfHelper.SmallVerticalMargin, Content = R.CMD_Add }),
									(_SaveSearchButton = new Button { Margin = WpfHelper.SmallVerticalMargin, Content = R.CMD_Update })
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

				_BrowserPath.TextChanged += _BrowserPath_TextChanged;
				_BrowserParameter.TextChanged += _BrowserParameter_TextChanged;
				_BrowseBrowserPath.Click += (s, args) => {
					var d = new OpenFileDialog {
						Title = R.OT_LocateBrowser,
						CheckFileExists = true,
						AddExtension = true,
						Filter = R.OT_ExecutableFileFilter
					};
					if (d.ShowDialog() == true) {
						_BrowserPath.Text = d.FileName;
					}
				};
				_SearchEngineList.Items.AddRange(Config.Instance.SearchEngines);
				_SearchEngineList.SelectionChanged += (s, args) => RefreshSearchEngineUI();
				_AddSearchButton.Click += (s, args) => {
					var item = new SearchEngine(R.CMD_NewItem, String.Empty);
					_SearchEngineList.Items.Add(item);
					Config.Instance.SearchEngines.Add(item);
					_SearchEngineList.SelectedIndex = _SearchEngineList.Items.Count - 1;
					RefreshSearchEngineUI();
					_SearchEngineName.Focus();
					Config.Instance.FireConfigChangedEvent(Features.SmartBar);
				};
				_RemoveSearchButton.Click += (s, args) => {
					var i = _SearchEngineList.SelectedItem as SearchEngine;
					if (MessageBox.Show(R.OT_ConfirmRemoveSearchEngine.Replace("<NAME>", i.Name), nameof(Codist), MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
						var p = _SearchEngineList.SelectedIndex;
						_SearchEngineList.Items.RemoveAt(p);
						Config.Instance.SearchEngines.RemoveAt(p);
						RefreshSearchEngineUI();
						Config.Instance.FireConfigChangedEvent(Features.WebSearch);
					}
				};
				_MoveUpSearchButton.Click += (s, args) => {
					var p = _SearchEngineList.SelectedIndex;
					if (p > 0) {
						var se = Config.Instance.SearchEngines[p];
						_SearchEngineList.Items.RemoveAt(p);
						Config.Instance.SearchEngines.RemoveAt(p);
						_SearchEngineList.Items.Insert(--p, se);
						Config.Instance.SearchEngines.Insert(p, se);
						_SearchEngineList.SelectedIndex = p;
						Config.Instance.FireConfigChangedEvent(Features.WebSearch);
					}
					_MoveUpSearchButton.IsEnabled = p > 0;
				};
				_SaveSearchButton.Click += (s, args) => {
					var se = _SearchEngineList.SelectedItem as SearchEngine;
					se.Name = _SearchEngineName.Text;
					se.Pattern = _SearchEngineUrl.Text;
					var p = _SearchEngineList.SelectedIndex;
					_SearchEngineList.Items.RemoveAt(p);
					_SearchEngineList.Items.Insert(p, se);
					Config.Instance.FireConfigChangedEvent(Features.WebSearch);
				};
				_ResetSearchButton.Click += (s, args) => {
					if (MessageBox.Show(R.OT_ConfirmResetSearchEngine, nameof(Codist), MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
						Config.Instance.ResetSearchEngines();
						ResetSearchEngines(Config.Instance.SearchEngines);
						Config.Instance.FireConfigChangedEvent(Features.WebSearch);
					}
				};
				RefreshSearchEngineUI();
			}

			void _BrowserParameter_TextChanged(object sender, TextChangedEventArgs e) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.BrowserParameter = _BrowserParameter.Text;
				Config.Instance.FireConfigChangedEvent(Features.WebSearch);
			}

			void _BrowserPath_TextChanged(object sender, TextChangedEventArgs e) {
				if (Page.IsConfigUpdating) {
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
				var se = _SearchEngineList.SelectedItem as SearchEngine;
				if (_RemoveSearchButton.IsEnabled = _SaveSearchButton.IsEnabled = _SearchEngineName.IsEnabled = _SearchEngineUrl.IsEnabled = se != null) {
					_MoveUpSearchButton.IsEnabled = _SearchEngineList.SelectedIndex > 0;
					_SearchEngineName.Text = se.Name;
					_SearchEngineUrl.Text = se.Pattern;
				}
				else {
					_MoveUpSearchButton.IsEnabled = false;
				}
			}
			void ResetSearchEngines(System.Collections.Generic.List<SearchEngine> searchEngines) {
				_SearchEngineList.Items.Clear();
				_SearchEngineList.Items.AddRange(searchEngines);
			}
		}
	}
}
