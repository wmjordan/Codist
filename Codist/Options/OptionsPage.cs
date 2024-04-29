using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using CLR;
using Codist.Controls;
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
			Config.RegisterLoadHandler (config => LoadConfig(config));
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
				Control.IsEnabled = Config.Instance.Features.HasAnyFlag(Feature);
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
			readonly OptionBox<Features> _SyntaxHighlight, _SuperQuickInfo, _SmartBar, _NavigationBar, _ScrollbarMarker, _JumpListEnhancer, _AutoSurround;
			readonly OptionBox<Features>[] _Options;
			readonly Button _LoadButton, _SaveButton, _OpenConfigFolderButton;
			readonly Note _NoticeBox;

			public PageControl(OptionsPage page) : base(page) {
				Thickness linkMargin = new Thickness(23, 0, 3, 0);
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
								.SetLazyToolTip(() => R.OT_ScrollbarMarkerTip)),
							(_JumpListEnhancer = o.CreateOptionBox(Features.JumpList, UpdateConfig, R.T_JumpList)
								.SetLazyToolTip(() => R.OT_JumpListTip)),
							(_AutoSurround = o.CreateOptionBox(Features.AutoSurround, UpdateConfig, R.T_AutoSurround)
								.SetLazyToolTip(() => R.OT_AutoSurround))
						}
					},
					_NoticeBox = new Note(R.OT_FeatureChangesTip) { BorderThickness = WpfHelper.TinyMargin, Visibility = Visibility.Collapsed },

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
					new Note(R.OT_ProjectWebSite),
					new TextBlock { Margin = linkMargin }.AppendLink("github.com/wmjordan/Codist", "https://github.com/wmjordan/Codist", R.CMD_GotoProjectWebSite),
					new Note(R.OT_ReportBugsAndSuggestions),
					new TextBlock { Margin = linkMargin }.AppendLink("github.com/wmjordan/Codist/issues", "https://github.com/wmjordan/Codist/issues", R.CMD_PostIssue),
					new Note(R.OT_LatestRelease),
					new TextBlock { Margin = linkMargin }.AppendLink("github.com/wmjordan/Codist/releases", "https://github.com/wmjordan/Codist/releases", R.CMD_GotoProjectReleasePage),
					new TitleBox(R.OT_SupportCodst),
					new TextBlock { Margin = linkMargin }.AppendLink(R.CMD_DonateLink, "https://www.paypal.me/wmzuo/19.99", R.CMDT_OpenDonatePage),
					new TextBlock { Margin = linkMargin }.AppendLink(R.CMD_WechatDonateLink, ShowWechatQrCode, R.CMDT_OpenWechatQrCode),
					new DescriptionBox(R.OT_DonateLinkTip)
					);
				_Options = new[] { _SyntaxHighlight, _SuperQuickInfo, _SmartBar, _NavigationBar, _ScrollbarMarker, _JumpListEnhancer, _AutoSurround };
				foreach (var item in _Options) {
					item.MinWidth = 120;
					item.Margin = WpfHelper.MiddleMargin;
					if (item.CeqAny(_JumpListEnhancer, _AutoSurround) == false) {
						item.PreviewMouseDown += HighlightNoticeBox;
					}
				}
				foreach (var item in new[] { _LoadButton, _SaveButton, _OpenConfigFolderButton }) {
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
				_NoticeBox.Background = SystemColors.HighlightBrush.Alpha(WpfHelper.DimmedOpacity);
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
						Filter = R.T_ConfigFileFilter
					};
					if (d.ShowDialog() != true) {
						return;
					}
					Config.Instance.SaveConfig(d.FileName);
				}
				else {
					try {
						if (System.IO.File.Exists(Config.ConfigPath)) {
							System.Diagnostics.Process.Start(System.IO.Path.GetDirectoryName(Config.ConfigPath));
						}
					}
					catch (Exception ex) {
						System.Diagnostics.Debug.WriteLine(ex);
					}
				}
			}
		}
	}

	[Guid("09020157-B191-464F-8F9B-F3100596BDF0")]
	sealed class SuperQuickInfoOptionsPage : OptionsPage
	{
		PageControl _Child;

		protected override Features Feature => Features.SuperQuickInfo;
		protected override UIElement Child => _Child ?? (_Child = new PageControl(this));

		sealed class PageControl : OptionsPageContainer
		{
			readonly OptionBox<QuickInfoOptions> _DisableUntilShift, _CtrlSuppress, _Selection, _Color;
			readonly OptionBox<QuickInfoOptions> _OverrideDefaultDocumentation, _DocumentationFromBaseType, _DocumentationFromInheritDoc, _TextOnlyDoc, _OrdinaryDoc, _ReturnsDoc, _RemarksDoc, _ExceptionDoc, _SeeAlsoDoc, _ExampleDoc, _AlternativeStyle, _ContainingType, _CodeFontForXmlDocSymbol;
			readonly OptionBox<QuickInfoOptions> _NodeRange, _Attributes, _BaseType, _Declaration, _SymbolLocation, _Interfaces, _NumericValues, _String, _Parameter, _InterfaceImplementations, _TypeParameters, _NamespaceTypes, _MethodOverload, _InterfaceMembers, _EnumMembers;
			readonly OptionBox<QuickInfoOptions>[] _Options;
			readonly Controls.IntegerBox _MaxWidth, _MaxHeight, _DisplayDelay;
			readonly ColorButton _BackgroundButton;

			public PageControl(OptionsPage page) : base(page) {
				var o = Config.Instance.QuickInfoOptions;
				AddPage(R.OT_General,
					new Note(R.OT_QuickInfoNote),
					_DisableUntilShift = o.CreateOptionBox(QuickInfoOptions.CtrlQuickInfo, UpdateConfig, R.OT_HideQuickInfoUntilShift)
						.SetLazyToolTip(() => R.OT_HideQuickInfoUntilShiftTip),
					_CtrlSuppress = o.CreateOptionBox(QuickInfoOptions.CtrlSuppress, UpdateConfig, R.OT_CtrlSuppressQuickInfo).SetLazyToolTip(() => R.OT_CtrlSuppressQuickInfoTip),
					_Selection = o.CreateOptionBox(QuickInfoOptions.Selection, UpdateConfig, R.OT_SelectionInfo)
						.SetLazyToolTip(() => R.OT_SelectionInfoTip),
					_Color = o.CreateOptionBox(QuickInfoOptions.Color, UpdateConfig, R.OT_ColorInfo)
						.SetLazyToolTip(() => R.OT_ColorInfoTip),

					new TitleBox(R.OT_LimitSize),
					new DescriptionBox(R.OT_LimitContentSizeNote),
					new WrapPanel {
						Children = {
							new StackPanel().MakeHorizontal()
								.Add(new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append(R.OT_MaxWidth))
								.Add(_MaxWidth = new Controls.IntegerBox(Config.Instance.QuickInfo.MaxWidth) { Minimum = 0, Maximum = 5000, Step = 100 })
								.SetLazyToolTip(() => R.OT_MaxWidthTip),
							new StackPanel().MakeHorizontal()
								.Add(new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append(R.OT_MaxHeight))
								.Add(_MaxHeight = new Controls.IntegerBox(Config.Instance.QuickInfo.MaxHeight) { Minimum = 0, Maximum = 5000, Step = 50 })
								.SetLazyToolTip(() => R.OT_MaxHeightTip),
						}
					}.ForEachChild((FrameworkElement b) => b.MinWidth = MinColumnWidth),
					new DescriptionBox(R.OT_UnlimitedSize),

					new TitleBox(R.OT_DelayDisplay),
					new DescriptionBox(R.OT_DelayDisplayNote),
					new StackPanel().MakeHorizontal()
						.Add(new TextBlock { MinWidth = 240, Margin = WpfHelper.SmallHorizontalMargin, Text = R.OT_DelayTime })
						.Add(_DisplayDelay = new Controls.IntegerBox(Config.Instance.QuickInfo.DelayDisplay) { Minimum = 0, Maximum = 100000, Step = 100 }),

					new TitleBox(R.T_Color),
					new WrapPanel {
						Children = {
							new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append(R.OT_QuickInfoBackground),
							new ColorButton(Colors.Transparent, R.T_Color, UpdateQuickInfoBackgroundColor).Set(ref _BackgroundButton)
						}
					}
				);

				AddPage(R.OT_CSharp,
					new Note(R.OT_CSharpNote),
					new TitleBox(R.OT_QuickInfoOverride),
					new DescriptionBox(R.OT_QuickInfoOverrideNote),
					_AlternativeStyle = o.CreateOptionBox(QuickInfoOptions.AlternativeStyle, UpdateConfig, R.OT_AlternativeStyle)
						.SetLazyToolTip(() => R.OT_AlternativeStyleTip),
					_NodeRange = o.CreateOptionBox(QuickInfoOptions.NodeRange, UpdateConfig, R.OT_NodeRange)
						.SetLazyToolTip(() => R.OT_NodeRangeTip),
					_OverrideDefaultDocumentation = o.CreateOptionBox(QuickInfoOptions.OverrideDefaultDocumentation, UpdateConfig, R.OT_OverrideXmlDoc)
						.SetLazyToolTip(() => R.OT_OverrideXmlDocTip),
					_DocumentationFromBaseType = o.CreateOptionBox(QuickInfoOptions.DocumentationFromBaseType, UpdateConfig, R.OT_InheritXmlDoc)
						.SetLazyToolTip(() => R.OT_InheritXmlDocTip),
					_DocumentationFromInheritDoc = o.CreateOptionBox(QuickInfoOptions.DocumentationFromInheritDoc, UpdateConfig, R.OT_InheritDoc)
						.SetLazyToolTip(() => R.OT_InheritDocTip),
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
					_TextOnlyDoc = o.CreateOptionBox(QuickInfoOptions.TextOnlyDoc, UpdateConfig, R.OT_TextOnlyXmlDoc)
						.SetLazyToolTip(() => R.OT_TextOnlyXmlDocTip),
					_OrdinaryDoc = o.CreateOptionBox(QuickInfoOptions.OrdinaryCommentDoc, UpdateConfig, R.OT_UseOrdinaryComment)
						.SetLazyToolTip(() => R.OT_UseOrdinaryCommentTip),
					_ContainingType = o.CreateOptionBox(QuickInfoOptions.ContainingType, UpdateConfig, R.OT_ShowSeeContainingType)
						.SetLazyToolTip(() => R.OT_ShowSeeContainingTypeTip),
					_CodeFontForXmlDocSymbol = o.CreateOptionBox(QuickInfoOptions.UseCodeFontForXmlDocSymbol, UpdateConfig, R.OT_UseCodeEditorFontForSee)
						.SetLazyToolTip(() => R.OT_UseCodeEditorFontForSeeTip),

					new TitleBox(R.OT_AdditionalQuickInfo),
					new DescriptionBox(R.OT_AdditionalQuickInfoNote),
					_Attributes = o.CreateOptionBox(QuickInfoOptions.Attributes, UpdateConfig, R.OT_Attributes)
						.SetLazyToolTip(() => R.OT_AttributesTip),
					_BaseType = o.CreateOptionBox(QuickInfoOptions.BaseType, UpdateConfig, R.OT_BaseType)
						.SetLazyToolTip(() => R.OT_BaseTypeTip),
					_Declaration = o.CreateOptionBox(QuickInfoOptions.Declaration, UpdateConfig, R.OT_Declaration)
						.SetLazyToolTip(() => R.OT_DesclarationTip),
					_EnumMembers = o.CreateOptionBox(QuickInfoOptions.Enum, UpdateConfig, R.OT_EnumMembers)
						.SetLazyToolTip(() => R.OT_EnumMembersTip),
					_Interfaces = o.CreateOptionBox(QuickInfoOptions.Interfaces, UpdateConfig, R.OT_Interfaces)
						.SetLazyToolTip(() => R.OT_InterfacesTip),
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

				_MaxHeight.ValueChanged += UpdateQuickInfoValue;
				_MaxWidth.ValueChanged += UpdateQuickInfoValue;
				_DisplayDelay.ValueChanged += UpdateQuickInfoValue;
				_Options = new[] { _DisableUntilShift, _CtrlSuppress, _Selection, _Color, _OverrideDefaultDocumentation, _DocumentationFromBaseType, _DocumentationFromInheritDoc, _TextOnlyDoc, _ReturnsDoc, _RemarksDoc, _ExceptionDoc, _SeeAlsoDoc, _ExampleDoc, _AlternativeStyle, _ContainingType, _CodeFontForXmlDocSymbol, _Attributes, _BaseType, _Declaration, _EnumMembers, _SymbolLocation, _Interfaces, _NumericValues, _String, _Parameter, _InterfaceImplementations, _TypeParameters, /*_NamespaceTypes, */_MethodOverload, _InterfaceMembers };
				foreach (var item in new[] { _DocumentationFromBaseType, _DocumentationFromInheritDoc, _TextOnlyDoc, _OrdinaryDoc, _ReturnsDoc, _RemarksDoc, _ExceptionDoc, _SeeAlsoDoc, _ExampleDoc, _ContainingType, _CodeFontForXmlDocSymbol }) {
					item.WrapMargin(SubOptionMargin);
				}
				_OverrideDefaultDocumentation.BindDependentOptionControls(_DocumentationFromBaseType, _DocumentationFromInheritDoc, _TextOnlyDoc, _OrdinaryDoc, _ReturnsDoc, _RemarksDoc, _ExceptionDoc, _SeeAlsoDoc, _ExampleDoc, _ContainingType, _CodeFontForXmlDocSymbol);
				_BackgroundButton.DefaultColor = () => ThemeHelper.ToolTipBackgroundBrush.Color;
				_BackgroundButton.Color = Config.Instance.QuickInfo.BackColor;
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
				if (Page.IsConfigUpdating) {
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
				Config.Instance.QuickInfo.BackColor = color;
				Config.Instance.FireConfigChangedEvent(Features.SuperQuickInfo);
			}
		}
	}

	[Guid("CF07BC0B-EF35-499B-8E7A-595638E93474")]
	sealed class SyntaxHighlightOptionsPage : OptionsPage
	{
		PageControl _Child;

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
		PageControl _Child;

		protected override Features Feature => Features.SmartBar;
		protected override UIElement Child => _Child ?? (_Child = new PageControl(this));

		sealed class PageControl : OptionsPageContainer
		{
			readonly OptionBox<SmartBarOptions> _ShiftToggleDisplay, _ManualDisplaySmartBar, _UnderscoreBold, _UnderscoreItalic, _DoubleIndentRefactoring;
			readonly OptionBox<SmartBarOptions>[] _Options;

			public PageControl(OptionsPage page) : base(page) {
				var o = Config.Instance.SmartBarOptions;
				AddPage(R.OT_Behavior,
					new Note(R.OT_BehaviorTip),
					_ManualDisplaySmartBar = o.CreateOptionBox(SmartBarOptions.ManualDisplaySmartBar, UpdateConfig, R.OT_ManualSmartBar)
						.SetLazyToolTip(() => R.OT_ManualSmartBarTip),
					_ShiftToggleDisplay = o.CreateOptionBox(SmartBarOptions.ShiftToggleDisplay, UpdateConfig, R.OT_ToggleSmartBar)
						.SetLazyToolTip(() => R.OT_ToggleSmartBarTip),
					new DescriptionBox(R.OT_ToggleSmartBarNote),

					new TitleBox(R.OT_CSharpSmartBar),
					_DoubleIndentRefactoring = o.CreateOptionBox(SmartBarOptions.DoubleIndentRefactoring, UpdateConfig, R.OT_DoubleIndentation)
						.SetLazyToolTip(() => R.OT_DoubleIndentationTip),

					new TitleBox(R.OT_MarkdownSmartBar),
					_UnderscoreBold = o.CreateOptionBox(SmartBarOptions.UnderscoreBold, UpdateConfig, R.OT_BoldPreferUnderscore)
						.SetLazyToolTip(() => R.OT_PreferUnderscoreTip),
					_UnderscoreItalic = o.CreateOptionBox(SmartBarOptions.UnderscoreItalic, UpdateConfig, R.OT_ItalicPreferUnderscore)
						.SetLazyToolTip(() => R.OT_PreferUnderscoreTip)
					);

				_Options = new[] { _ShiftToggleDisplay, _ManualDisplaySmartBar, _DoubleIndentRefactoring, _UnderscoreBold, _UnderscoreItalic };
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
		PageControl _Child;

		protected override Features Feature => Features.NaviBar;
		protected override UIElement Child => _Child ?? (_Child = new PageControl(this));

		sealed class PageControl : OptionsPageContainer
		{
			readonly OptionBox<NaviBarOptions> _SyntaxDetail, _SymbolToolTip, _RegionOnBar, _StripRegionNonLetter, _RangeHighlight, _CtrlGoToSource,
				_ParameterList, _ParameterListShowParamName, _FieldValue, _AutoPropertyAsField, _MemberType, _PartialClassMember, _BaseClassMember, _Region, _RegionInMember, _LineOfCode;
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
					_AutoPropertyAsField = o.CreateOptionBox(NaviBarOptions.AutoPropertiesAsFields, UpdateConfig, R.OT_FilterAutoPropertiesAsFields),
					new DescriptionBox(R.OT_FilterAutoPropertiesAsFieldsNote),
					_CtrlGoToSource = o.CreateOptionBox(NaviBarOptions.CtrlGoToSource, UpdateConfig, R.OT_CtrlGoToSource),

					new TitleBox(R.OT_CSharpNaviBarMenu),
					_ParameterList = o.CreateOptionBox(NaviBarOptions.ParameterList, UpdateConfig, R.OT_MethodParameterList)
						.SetLazyToolTip(() => R.OT_MethodParameterListTip),
					_ParameterListShowParamName = o.CreateOptionBox(NaviBarOptions.ParameterListShowParamName, UpdateConfig, R.OT_ShowMethodParameterName)
						.SetLazyToolTip(() => R.OT_ShowMethodParameterNameTip),
					_FieldValue = o.CreateOptionBox(NaviBarOptions.FieldValue, UpdateConfig, R.OT_ValueOfFields)
						.SetLazyToolTip(() => R.OT_ValueOfFieldsTip),
					_MemberType = o.CreateOptionBox(NaviBarOptions.MemberType, UpdateConfig, R.OT_MemberTypes).SetLazyToolTip(() => R.OT_MemberTypesTip),
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
				_ParameterList, _ParameterListShowParamName, _FieldValue, _AutoPropertyAsField, _MemberType, _PartialClassMember, _BaseClassMember, _Region, _RegionInMember, _LineOfCode };
				foreach (var item in new[] { _StripRegionNonLetter, _ParameterListShowParamName, _RegionInMember }) {
					item.WrapMargin(SubOptionMargin);
				}
				_RegionOnBar.BindDependentOptionControls(_StripRegionNonLetter);
				_ParameterList.BindDependentOptionControls(_ParameterListShowParamName);
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
		PageControl _Child;

		protected override Features Feature => Features.ScrollbarMarkers;
		protected override UIElement Child => _Child ?? (_Child = new PageControl(this));

		sealed class PageControl : OptionsPageContainer
		{
			readonly OptionBox<MarkerOptions> _LineNumber, _Selection, _SpecialComment, _MarkerDeclarationLine, _LongMemberDeclaration, _TypeDeclaration, _MethodDeclaration, _RegionDirective, _CompilerDirective, _SymbolReference, _DisableChangeTracker;
			readonly OptionBox<MarkerOptions>[] _Options;
			readonly ColorButton _SymbolReferenceButton, _SymbolWriteButton, _SymbolDefinitionButton;

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
					_DisableChangeTracker = o.CreateOptionBox(MarkerOptions.DisableChangeTracker, UpdateConfig, R.OT_DisableChangeTracker)
						.SetLazyToolTip(() => R.OT_DisableChangeTrackerTip),

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
						.SetLazyToolTip(() => R.OT_MatchSymbolTip),
					new WrapPanel {
						Children = {
							new StackPanel().MakeHorizontal().Add(
								new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append(R.OT_MatchSymbolColor),
								new ColorButton(Margins.SymbolReferenceMarkerStyle.DefaultReferenceMarkerColor, R.T_Color, UpdateSymbolReferenceColor).Set(ref _SymbolReferenceButton)
							),
							new StackPanel().MakeHorizontal().Add(
								new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append(R.OT_WriteSymbolColor),
								new ColorButton(Margins.SymbolReferenceMarkerStyle.DefaultWriteMarkerColor, R.T_Color, UpdateSymbolWriteColor).Set(ref _SymbolWriteButton)
								),
							new StackPanel().MakeHorizontal().Add(
								new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append(R.OT_SymbolDefinitionColor),
								new ColorButton(Margins.SymbolReferenceMarkerStyle.DefaultSymbolDefinitionColor, R.T_Color, UpdateSymbolDefinitionColor).Set(ref _SymbolDefinitionButton)
								)
						}
					}.WrapMargin(SubOptionMargin)
					);
				_Options = new[] {
					_LineNumber, _Selection, _SpecialComment, _DisableChangeTracker,
					_MarkerDeclarationLine, _LongMemberDeclaration, _TypeDeclaration, _MethodDeclaration, _RegionDirective, _CompilerDirective, _SymbolReference
				};
				var dubOptions = new[] { _LongMemberDeclaration, _TypeDeclaration, _MethodDeclaration, _RegionDirective };
				foreach (var item in dubOptions) {
					item.WrapMargin(SubOptionMargin);
				}
				_MarkerDeclarationLine.BindDependentOptionControls(dubOptions);
				_SymbolReference.BindDependentOptionControls(_SymbolReferenceButton, _SymbolWriteButton, _SymbolDefinitionButton);
				_DisableChangeTracker.IsEnabled = CodistPackage.VsVersion.Major >= 17;
				_SymbolReferenceButton.DefaultColor = () => Margins.SymbolReferenceMarkerStyle.DefaultReferenceMarkerColor;
				_SymbolReferenceButton.Color = Config.Instance.SymbolReferenceMarkerSettings.ReferenceMarker;
				_SymbolWriteButton.DefaultColor = () => Margins.SymbolReferenceMarkerStyle.DefaultWriteMarkerColor;
				_SymbolWriteButton.Color = Config.Instance.SymbolReferenceMarkerSettings.WriteMarker;
				_SymbolDefinitionButton.DefaultColor = () => Margins.SymbolReferenceMarkerStyle.DefaultSymbolDefinitionColor;
				_SymbolDefinitionButton.Color = Config.Instance.SymbolReferenceMarkerSettings.SymbolDefinition;
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

			void UpdateSymbolReferenceColor(Color color) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.SymbolReferenceMarkerSettings.ReferenceMarkerColor = color.ToHexString();
				if (color.A == 0) {
					_SymbolReferenceButton.Color = Margins.SymbolReferenceMarkerStyle.DefaultReferenceMarkerColor;
				}
				Config.Instance.FireConfigChangedEvent(Features.ScrollbarMarkers);
			}

			void UpdateSymbolWriteColor(Color color) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.SymbolReferenceMarkerSettings.WriteMarkerColor = color.ToHexString();
				if (color.A == 0) {
					_SymbolWriteButton.Color = Margins.SymbolReferenceMarkerStyle.DefaultWriteMarkerColor;
				}
				Config.Instance.FireConfigChangedEvent(Features.ScrollbarMarkers);
			}

			void UpdateSymbolDefinitionColor(Color color) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.SymbolReferenceMarkerSettings.SymbolDefinitionColor = color.ToHexString();
				if (color.A == 0) {
					_SymbolDefinitionButton.Color = Margins.SymbolReferenceMarkerStyle.DefaultSymbolDefinitionColor;
				}
				Config.Instance.FireConfigChangedEvent(Features.ScrollbarMarkers);
			}
		}
	}

	[Guid("3C54350C-A369-46F8-A74B-5180DA804DA1")]
	sealed class ExtensionDeveloperPage : OptionsPage
	{
		PageControl _Child;

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
					_ShowDocumentContentType = Config.Instance.DeveloperOptions.CreateOptionBox(DeveloperOptions.ShowWindowInformer, UpdateConfig, R.OT_AddShowActiveWindowProperties)
						.SetLazyToolTip(() => R.OT_AddShowActiveWindowPropertiesTip),
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
			readonly OptionBox<DisplayOptimizations> _MainWindow, _CodeWindow, _MenuLayoutOverride, _HideSearchBox, _HideAccountBox, _HideFeedbackButton, _HideCodePilotButton, _HideInfoBadgeButton, _CpuMonitor, _MemoryMonitor, _DriveMonitor, _NetworkMonitor;
			readonly OptionBox<BuildOptions> _BuildTimestamp, _ShowOutputWindowAfterBuild;
			readonly TextBox _TaskManagerPath, _TaskManagerParameter;
			readonly Button _BrowseTaskManagerPath;

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
							Config.Instance.DisplayOptimizations.CreateOptionBox(DisplayOptimizations.HideCopilotButton, UpdateHideCodePilotButtonOption, R.OT_HideCopilotButton).Set(ref _HideCodePilotButton),
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
						Filter = R.OT_ExecutableFileFilter
					};
					if (d.ShowDialog() == true) {
						_TaskManagerPath.Text = d.FileName;
					}
				};

				_MenuLayoutOverride.IsEnabled = CodistPackage.VsVersion.Major == 15;
				_HideCodePilotButton.IsEnabled = CodistPackage.VsVersion.Major > 17 || CodistPackage.VsVersion.Major == 17 && CodistPackage.VsVersion.Minor > 9;
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
				_HideCodePilotButton.UpdateWithOption(o);
				_HideInfoBadgeButton.UpdateWithOption(o);
				_BuildTimestamp.UpdateWithOption(config.BuildOptions);
				_ShowOutputWindowAfterBuild.UpdateWithOption(config.BuildOptions);
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

			void UpdateResourceManagerOption(DisplayOptimizations options, bool value) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.Set(options, value);
				Display.ResourceMonitor.Reload(Config.Instance.DisplayOptimizations);
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

			void UpdateHideCodePilotButtonOption(DisplayOptimizations options, bool value) {
				ToggleTitleBarElement(options, value, DisplayOptimizations.HideCopilotButton);
			}

			void UpdateHideInfoBadgeButtonOption(DisplayOptimizations options, bool value) {
				ToggleTitleBarElement(options, value, DisplayOptimizations.HideInfoBadgeButton);
			}

			void ToggleTitleBarElement(DisplayOptimizations options, bool value, DisplayOptimizations element) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.Set(options, value);
				Display.LayoutOverride.ToggleUIElement(element, !value);
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

			void _TaskManagerParameter_TextChanged(object sender, TextChangedEventArgs e) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.TaskManagerParameter = _TaskManagerParameter.Text;
				Config.Instance.FireConfigChangedEvent(Features.None);
			}

			void _TaskManagerPath_TextChanged(object sender, TextChangedEventArgs e) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.TaskManagerPath = _TaskManagerPath.Text;
				Config.Instance.FireConfigChangedEvent(Features.None);
			}
		}
	}

	[Guid("4BD9DEDE-B83D-4552-8197-45BF050E20CA")]
	sealed class WebSearchPage : OptionsPage
	{
		PageControl _Child;

		protected override Features Feature => Features.SmartBar | Features.SuperQuickInfo;
		protected override UIElement Child => _Child ?? (_Child = new PageControl(this));

		sealed class PageControl : OptionsPageContainer
		{
			readonly TextBox _BrowserPath, _BrowserParameter, _Name, _Url;
			readonly ListBox _List;
			readonly Button _BrowseBrowserPath, _AddButton, _RemoveButton, _MoveUpButton, _ResetButton, _SaveButton;

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
							(_List = new ListView {
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
				_List.Items.AddRange(Config.Instance.SearchEngines);
				_List.SelectionChanged += (s, args) => RefreshSearchEngineUI();
				_AddButton.Click += (s, args) => {
					var item = new SearchEngine(R.CMD_NewItem, String.Empty);
					_List.Items.Add(item);
					Config.Instance.SearchEngines.Add(item);
					_List.SelectedIndex = _List.Items.Count - 1;
					RefreshSearchEngineUI();
					_Name.Focus();
					Config.Instance.FireConfigChangedEvent(Features.SmartBar);
				};
				_RemoveButton.Click += (s, args) => {
					var i = _List.SelectedItem as SearchEngine;
					if (MessageWindow.AskYesNo(R.OT_ConfirmRemoveSearchEngine.Replace("<NAME>", i.Name)) == true) {
						var p = _List.SelectedIndex;
						_List.Items.RemoveAndDisposeAt(p);
						Config.Instance.SearchEngines.RemoveAt(p);
						RefreshSearchEngineUI();
						Config.Instance.FireConfigChangedEvent(Features.WebSearch);
					}
				};
				_MoveUpButton.Click += (s, args) => {
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
				};
				_SaveButton.Click += (s, args) => {
					var se = _List.SelectedItem as SearchEngine;
					se.Name = _Name.Text;
					se.Pattern = _Url.Text;
					var p = _List.SelectedIndex;
					_List.Items.RemoveAt(p);
					_List.Items.Insert(p, se);
					Config.Instance.FireConfigChangedEvent(Features.WebSearch);
				};
				_ResetButton.Click += (s, args) => {
					if (MessageWindow.AskYesNo(R.OT_ConfirmResetSearchEngine) == true) {
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
		}
	}

	[Guid("6A7DC1C9-2C62-43ED-9A72-B51B23CB9579")]
	sealed class WrapTextPage : OptionsPage
	{
		PageControl _Child;

		protected override Features Feature => Features.None;
		protected override UIElement Child => _Child ?? (_Child = new PageControl(this));

		sealed class PageControl : OptionsPageContainer
		{
			readonly TextBox _Name, _Pattern, _Indicator;
			readonly ListView _List;
			readonly Button _AddButton, _RemoveButton, _MoveUpButton, _ResetButton, _SaveButton;

			public PageControl(OptionsPage page) : base(page) {
				AddPage(R.OT_WrapText,
					new Note(R.OT_WrapTextNote),
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
							(_List = new ListView {
								Margin = WpfHelper.SmallMargin,
								View = new GridView {
									Columns = {
										new GridViewColumn { Header = R.OT_Name, Width = 80, DisplayMemberBinding = new Binding(nameof(WrapText.Name)) },
										new GridViewColumn { Header = R.OT_WrapTextPattern, Width = 180, DisplayMemberBinding = new Binding(nameof(WrapText.Pattern)) },
										new GridViewColumn { Header = R.OT_Indicator, Width = 60, DisplayMemberBinding = new Binding(nameof(WrapText.Indicator)) }
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
									new RowDefinition(),
									new RowDefinition(),
									new RowDefinition(),
									new RowDefinition()
								},
								Children = {
									new Label { Content = R.OTC_Name, Width = 60 },
									(_Name = new TextBox { IsEnabled = false, Margin = WpfHelper.SmallVerticalMargin }).SetValue(Grid.SetColumn, 1),
									new Label { Content = R.OTC_Pattern, Width = 60 }.SetValue(Grid.SetRow, 1),
									(_Pattern = new TextBox { IsEnabled = false, Margin = WpfHelper.SmallVerticalMargin }).SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 1),
									new Label { Content = R.OTC_Indicator, Width = 60 }.SetValue(Grid.SetRow, 2),
									(_Indicator = new TextBox { IsEnabled = false, Margin = WpfHelper.SmallVerticalMargin, Width = 40, MaxLength = 1 }).SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 2),
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

				_List.Items.AddRange(Config.Instance.WrapTexts);
				_List.SelectionChanged += (s, args) => RefreshWrapTextUI();
				_AddButton.Click += (s, args) => {
					var item = new WrapText("<c>" + WrapText.DefaultIndicator + "</c>", R.CMD_NewItem);
					_List.Items.Add(item);
					Config.Instance.WrapTexts.Add(item);
					_List.SelectedIndex = _List.Items.Count - 1;
					RefreshWrapTextUI();
					_Name.Focus();
					Config.Instance.FireConfigChangedEvent(Features.SmartBar);
				};
				_RemoveButton.Click += (s, args) => {
					var p = _List.SelectedIndex;
					_List.Items.RemoveAndDisposeAt(p);
					Config.Instance.WrapTexts.RemoveAt(p);
					RefreshWrapTextUI();
					Config.Instance.FireConfigChangedEvent(Features.WrapText);
				};
				_MoveUpButton.Click += (s, args) => {
					var p = _List.SelectedIndex;
					if (p > 0) {
						var se = Config.Instance.WrapTexts[p];
						_List.Items.RemoveAt(p);
						Config.Instance.WrapTexts.RemoveAt(p);
						_List.Items.Insert(--p, se);
						Config.Instance.WrapTexts.Insert(p, se);
						_List.SelectedIndex = p;
						Config.Instance.FireConfigChangedEvent(Features.WrapText);
					}
					_MoveUpButton.IsEnabled = p > 0;
				};
				_SaveButton.Click += (s, args) => {
					var se = _List.SelectedItem as WrapText;
					se.Name = _Name.Text;
					se.Pattern = _Pattern.Text;
					se.Indicator = _Indicator.Text.Length > 0 ? _Indicator.Text[0] : WrapText.DefaultIndicator;
					var p = _List.SelectedIndex;
					_List.Items.RemoveAt(p);
					_List.Items.Insert(p, se);
					Config.Instance.FireConfigChangedEvent(Features.WrapText);
				};
				_ResetButton.Click += (s, args) => {
					if (MessageWindow.AskYesNo(R.OT_ConfirmResetWrapText) == true) {
						Config.Instance.ResetWrapTexts();
						ResetWrapTexts(Config.Instance.WrapTexts);
						Config.Instance.FireConfigChangedEvent(Features.WrapText);
					}
				};
				RefreshWrapTextUI();
			}

			protected override void LoadConfig(Config config) {
				ResetWrapTexts(config.WrapTexts);
			}

			void RefreshWrapTextUI() {
				var se = _List.SelectedItem as WrapText;
				if (_RemoveButton.IsEnabled = _SaveButton.IsEnabled = _Name.IsEnabled = _Pattern.IsEnabled = _Indicator.IsEnabled = se != null) {
					_MoveUpButton.IsEnabled = _List.SelectedIndex > 0;
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
				_List.Items.AddRange(wrapTexts);
			}
		}
	}

	[Guid("496442FC-A36A-4C7A-B312-5D84B2631565")]
	sealed class AutoSurroundSelectionPage : OptionsPage
	{
		PageControl _Child;

		protected override Features Feature => Features.AutoSurround;
		protected override UIElement Child => _Child ?? (_Child = new PageControl(this));

		sealed class PageControl : OptionsPageContainer
		{
			readonly OptionBox<AutoSurroundSelectionOptions> _TrimSelection;

			public PageControl(OptionsPage page) : base(page) {
				AddPage(R.OT_General,
					new Note(R.OT_AutoSurroundSelectionNote),

					_TrimSelection = Config.Instance.AutoSurroundSelectionOptions.CreateOptionBox(AutoSurroundSelectionOptions.Trim, UpdateConfig, R.OT_TrimBeforeSurround)
						.SetLazyToolTip(() => R.OT_TrimBeforeSurroundTip)
					);
			}

			protected override void LoadConfig(Config config) {
				var o = config.AutoSurroundSelectionOptions;
				_TrimSelection.UpdateWithOption(o);
			}

			void UpdateConfig(AutoSurroundSelectionOptions options, bool set) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.Set(options, set);
				Config.Instance.FireConfigChangedEvent(Features.None);
			}
		}
	}
}
