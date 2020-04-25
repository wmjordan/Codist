using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using AppHelpers;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;

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

		public static void BindDependentOptionControls(this CheckBox checkBox, params UIElement[] dependentControls) {
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

			public PageControl(OptionsPage page) : base(page) {
				var o = Config.Instance.Features;
				AddPage("General",
					new TitleBox("Feature Controllers"),
					new DescriptionBox("Use the following checkboxes to control features provided by Codist."),
					new WrapPanel {
						Children = {
							(_SyntaxHighlight = o.CreateOptionBox(Features.SyntaxHighlight, UpdateConfig, "Syntax Highlight")
								.SetLazyToolTip(() => "Provides advanced syntax highlight and comment taggers")),
							(_SuperQuickInfo = o.CreateOptionBox(Features.SuperQuickInfo, UpdateConfig, "Super Quick Info")
								.SetLazyToolTip(() => "Provides enhancements to Quick Info (code tooltips)")),
							(_SmartBar = o.CreateOptionBox(Features.SmartBar, UpdateConfig, "Smart Bar")
								.SetLazyToolTip(() => "Provides a dynamic floating toolbar in your code editor")),
							(_NavigationBar = o.CreateOptionBox(Features.NaviBar, UpdateConfig, "Navigation Bar")
								.SetLazyToolTip(() => "Provides an enhanced navigation bar for C# and Markdown languages")),
							(_ScrollbarMarker = o.CreateOptionBox(Features.ScrollbarMarkers, UpdateConfig, "Scrollbar Markers")
								.SetLazyToolTip(() => "Provides additional markers on the scrollbar"))
						}
					},
					new Note("Changes will take place on NEWLY OPENED document windows. Currently opened document windows WILL NOT BE AFFECTED"),

					new TitleBox("Configuration File"),
					new DescriptionBox("Use the following buttons to backup your settings or share the file with others"),
					new WrapPanel {
						Children = {
							(_LoadButton = new Button { Name = "_Load", Content = "Load...", ToolTip = "Restore configurations from a file..." }),
							(_SaveButton = new Button { Name = "_Save", Content = "Save...", ToolTip = "Backup configurations to a file..." })
						}
					}
					);
				AddPage("About",
					new TitleBox("Thank You for Using Codist"),
					new Note("Report bugs and suggesions to:"),
					new TextBlock { Margin = new Thickness(23, 0, 3, 0) }.AppendLink("github.com/wmjordan/Codist", "https://github.com/wmjordan/Codist", "Go to project web site"),
					new Note("Latest release:"),
					new TextBlock { Margin = new Thickness(23, 0, 3, 0) }.AppendLink("github.com/wmjordan/Codist/releases", "https://github.com/wmjordan/Codist/releases", "Go to project release page"),
					new Note("Support future development of Codist:"),
					new TextBlock { Margin = new Thickness(23, 0, 3, 0) }.AppendLink("Donate via PayPal", "https://www.paypal.me/wmzuo/19.99", "Open your browser and donate to project Codist"),
					new DescriptionBox("Recommended donation value is $19.99. But you can modify the amount to any value if you like")
					);
				_Options = new[] { _SyntaxHighlight, _SuperQuickInfo, _SmartBar, _NavigationBar, _ScrollbarMarker };
				foreach (var item in _Options) {
					item.MinWidth = 120;
					item.Margin = WpfHelper.MiddleMargin;
				}
				foreach (var item in new[] { _LoadButton, _SaveButton }) {
					item.MinWidth = 120;
					item.Margin = WpfHelper.MiddleMargin;
					item.Click += LoadOrSaveConfig;
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
						Title = "Load Codist configuration file...",
						FileName = "Codist.json",
						DefaultExt = "json",
						Filter = "Codist configuration file|*.json|All files|*.*"
					};
					if (d.ShowDialog() != true) {
						return;
					}
					try {
						Config.LoadConfig(d.FileName);
						System.IO.File.Copy(d.FileName, Config.ConfigPath, true);
					}
					catch (Exception ex) {
						MessageBox.Show("Error occured while loading config file: " + ex.Message, nameof(Codist));
					}
				}
				else {
					var d = new SaveFileDialog {
						Title = "Save Codist configuration file...",
						FileName = "Codist.json",
						DefaultExt = "json",
						Filter = "Codist configuration file|*.json|All files|*.*"
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
				AddPage("General",
					new Note("This page changes the behavior of all types of Quick Infos (code tool tips)"),
					_CtrlQuickInfo = o.CreateOptionBox(QuickInfoOptions.CtrlQuickInfo, UpdateConfig, "Hide Quick Info until Shift key is pressed")
						.SetLazyToolTip(() => "Suppresses tool tip until pressing Shift key"),
					_Selection = o.CreateOptionBox(QuickInfoOptions.Selection, UpdateConfig, "Selection info")
						.SetLazyToolTip(() => "Shows selection information, such as character count, line count, Unicode of character, etc."),
					_Color = o.CreateOptionBox(QuickInfoOptions.Color, UpdateConfig, "Color info")
						.SetLazyToolTip(() => "Shows color information for predefined color names and hexidemical values"),

					new TitleBox("Item Size"),
					new DescriptionBox("Limit the maximum size of each items in Quick Info, preventing any item from taking up the whole screen"),
					new WrapPanel {
						Children = {
							new StackPanel().MakeHorizontal()
								.Add(new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append("Max width:"))
								.Add(_MaxWidth = new Controls.IntegerBox((int)Config.Instance.QuickInfoMaxWidth) { Minimum = 0, Maximum = 5000, Step = 100 })
								.SetLazyToolTip(() => "This option limits the max width of a Quick Info item"),
							new StackPanel().MakeHorizontal()
								.Add(new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append("Max height:"))
								.Add(_MaxHeight = new Controls.IntegerBox((int)Config.Instance.QuickInfoMaxHeight) { Minimum = 0, Maximum = 5000, Step = 50 })
								.SetLazyToolTip(() => "This option limits the max height of a Quick Info item"),
						}
					}.ForEachChild((FrameworkElement b) => b.MinWidth = MinColumnWidth),
					new DescriptionBox("Set the above value to 0 to for unlimited size"),
					new StackPanel().MakeHorizontal()
						.Add(new TextBlock { MinWidth = 240, Margin = WpfHelper.SmallHorizontalMargin, Text = "More max-height for C# XML Documentation:" })
						.Add(_ExtraHeight = new Controls.IntegerBox((int)Config.Instance.QuickInfoXmlDocExtraHeight) { Minimum = 0, Maximum = 1000, Step = 50 })
						.SetLazyToolTip(() => "This option plus max-height is the max height for the C# XML Documentation")
				);

				AddPage("C#",
					new Note("This page changes the behavior of C# Quick Infos (code tool tips)"),
					new TitleBox("Quick Info Override"),
					new DescriptionBox("The following options allow overriding C# Quick Info with more features"),
					_ClickAndGo = o.CreateOptionBox(QuickInfoOptions.ClickAndGo, UpdateConfig, "Click and go to source code of symbol definition")
						.SetLazyToolTip(() => "Makes symbols in Quick Info clickable to the source code"),
					_OverrideDefaultDocumentation = o.CreateOptionBox(QuickInfoOptions.OverrideDefaultDocumentation, UpdateConfig, "Override default XML Documentation")
						.SetLazyToolTip(() => "Reformats the XML Documentation, making them selectable, copiable and clickable"),
					_DocumentationFromBaseType = o.CreateOptionBox(QuickInfoOptions.DocumentationFromBaseType, UpdateConfig, "Inherits from base type or interfaces")
						.SetLazyToolTip(() => "Displays XML Doc from base types or interfaces if it is absent in active symbol"),
					_DocumentationFromInheritDoc = o.CreateOptionBox(QuickInfoOptions.DocumentationFromInheritDoc, UpdateConfig, "Inherits from <inheritdoc cref=\"\"/> target")
						.SetLazyToolTip(() => "Supports inheritdoc which uses XML Doc in referenced symbol"),
					_TextOnlyDoc = o.CreateOptionBox(QuickInfoOptions.TextOnlyDoc, UpdateConfig, "Allow text only (no <summary/>) XML Doc")
						.SetLazyToolTip(() => "Displays the text content of XML Doc if <summary/> is absent"),
					_ReturnsDoc = o.CreateOptionBox(QuickInfoOptions.ReturnsDoc, UpdateConfig, "Show <returns/> XML Doc")
						.SetLazyToolTip(() => "Displays the content of <returns/>"),
					_RemarksDoc = o.CreateOptionBox(QuickInfoOptions.RemarksDoc, UpdateConfig, "Show <remarks/> XML Doc")
						.SetLazyToolTip(() => "Displays the content of <remarks/>"),
					_ExceptionDoc = o.CreateOptionBox(QuickInfoOptions.ExceptionDoc, UpdateConfig, "Show <exception/> XML Doc")
						.SetLazyToolTip(() => "Displays the content of <exception/>"),
					_SeeAlsoDoc = o.CreateOptionBox(QuickInfoOptions.SeeAlsoDoc, UpdateConfig, "Show <seealso/> links")
						.SetLazyToolTip(() => "Displays referenced symbols of <seealso/>"),
					_ExampleDoc = o.CreateOptionBox(QuickInfoOptions.ExampleDoc, UpdateConfig, "Show <example/> XML Doc")
						.SetLazyToolTip(() => "Displays the content of <example/>"),
					_AlternativeStyle = o.CreateOptionBox(QuickInfoOptions.AlternativeStyle, UpdateConfig, "Use alternative style")
						.SetLazyToolTip(() => "Uses an alternative style to display Quick Info, and unify the topmost link in the symbol definition part of Quick Info"),

					new TitleBox("Additional Quick Info"),
					new DescriptionBox("The following options allow adding more items to the Quick Info"),
					_Attributes = o.CreateOptionBox(QuickInfoOptions.Attributes, UpdateConfig, "Attributes")
						.SetLazyToolTip(() => "Displays attributes of a symbol"),
					_BaseType = o.CreateOptionBox(QuickInfoOptions.BaseType, UpdateConfig, "Base type")
						.SetLazyToolTip(() => "Displays base type of a symbol"),
					_BaseTypeInheritence = o.CreateOptionBox(QuickInfoOptions.BaseTypeInheritence, UpdateConfig, "All ancestor types")
						.SetLazyToolTip(() => "Displays base type of a symbol along inheritance relations"),
					_Declaration = o.CreateOptionBox(QuickInfoOptions.Declaration, UpdateConfig, "Declaration")
						.SetLazyToolTip(() => "Displays declaration information of a symbol if it is not a public instance one, as well as event or delegate signatures"),
					_Interfaces = o.CreateOptionBox(QuickInfoOptions.Interfaces, UpdateConfig, "Interfaces")
						.SetLazyToolTip(() => "Displays interfaces implemented by the symbol"),
					_InterfacesInheritence = o.CreateOptionBox(QuickInfoOptions.InterfacesInheritence, UpdateConfig, "Inherited interfaces")
						.SetLazyToolTip(() => "Displays inherited interfaces implemented by the symbol"),
					_InterfaceImplementations = o.CreateOptionBox(QuickInfoOptions.InterfaceImplementations, UpdateConfig, "Interface implementation")
						.SetLazyToolTip(() => "Displays the interface member if it is implemented by the symbol"),
					_InterfaceMembers = o.CreateOptionBox(QuickInfoOptions.InterfaceMembers, UpdateConfig, "Interface members")
						.SetLazyToolTip(() => "Displays members of an interface"),
					_MethodOverload = o.CreateOptionBox(QuickInfoOptions.MethodOverload, UpdateConfig, "Method overloads")
						.SetLazyToolTip(() => "Displays method overloads and applicable extension methods"),
					_Parameter = o.CreateOptionBox(QuickInfoOptions.Parameter, UpdateConfig, "Parameter of method")
						.SetLazyToolTip(() => "Displays information of parameter if active symbol is a parameter of a method"),
					_TypeParameters = o.CreateOptionBox(QuickInfoOptions.TypeParameters, UpdateConfig, "Type parameter")
						.SetLazyToolTip(() => "Displays information of type parameters"),
					_SymbolLocation = o.CreateOptionBox(QuickInfoOptions.SymbolLocation, UpdateConfig, "Symbol location")
						.SetLazyToolTip(() => "Displays the location where a symbol is defined"),
					_NumericValues = o.CreateOptionBox(QuickInfoOptions.NumericValues, UpdateConfig, "Numeric forms")
						.SetLazyToolTip(() => "Displays decimal, hexidecimal, binary forms of a number"),
					_String = o.CreateOptionBox(QuickInfoOptions.String, UpdateConfig, "String length, hash and content")
						.SetLazyToolTip(() => "Displays length, hash code and content of strings")

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
				AddPage("General",
					new Note(new TextBlock()
						.Append("To configure syntax highlight and manage comment taggers, use the ")
						.AppendLink("Configure Codist Syntax Highlight", _ => Commands.SyntaxCustomizerWindowCommand.Execute(null, EventArgs.Empty), "Open the syntax highlight configuration dialog window")
						.Append(" command under the Tools menu.")),
					new TitleBox("Extra highlight"),
					_CommentTaggerBox = o.CreateOptionBox(SpecialHighlightOptions.SpecialComment, UpdateConfig, "Enable comment tagger"),
					_SearchResultBox = o.CreateOptionBox(SpecialHighlightOptions.SearchResult, UpdateConfig, "Highlight search results (*)"),
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
			readonly TextBox _BrowserPath, _BrowserParameter, _SearchEngineName, _SearchEngineUrl;
			readonly ListBox _SearchEngineList;
			readonly Button _BrowseBrowserPath, _AddSearchButton, _RemoveSearchButton, _MoveUpSearchButton, _ResetSearchButton, _SaveSearchButton;

			public PageControl(OptionsPage page) : base(page) {
				var o = Config.Instance.SmartBarOptions;
				AddPage("Behavior",
					new Note("This page changes the behavior of Smart Bar, which by default appears when you select something in a text editor window"),
					_ManualDisplaySmartBar = o.CreateOptionBox(SmartBarOptions.ManualDisplaySmartBar, UpdateConfig, "Manually display Smart Bar")
						.SetLazyToolTip(() => "Don't automatically display Smart Bar when the selection is changed, combine with the following option"),
					_ShiftToggleDisplay = o.CreateOptionBox(SmartBarOptions.ShiftToggleDisplay, UpdateConfig, "Show/hide with Shift key")
						.SetLazyToolTip(() => "Toggle the display of Smart Bar with Shift-key"),
					new DescriptionBox("Double tap Shift-key to show, single tap Shift-key to hide")
					);

				AddPage("Web Search",
					new Note("This page defines search engines which can be accessed via right clicking the Find button on the Smart Bar"),
					new TitleBox("Search Engines"),
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
										new GridViewColumn { Header = "Name", Width = 100, DisplayMemberBinding = new Binding("Name") },
										new GridViewColumn { Header = "URL Pattern", Width = 220, DisplayMemberBinding = new Binding("Pattern") }
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
									new Label { Content = "Name: ", Width = 60 },
									(_SearchEngineName = new TextBox { IsEnabled = false, Margin = WpfHelper.SmallVerticalMargin }).SetValue(Grid.SetColumn, 1),
									new Label { Content = "URL: ", Width = 60 }.SetValue(Grid.SetRow, 1),
									(_SearchEngineUrl = new TextBox { IsEnabled = false, Margin = WpfHelper.SmallVerticalMargin }).SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 1),
									new DescriptionBox("Use %s for search keyword").SetValue(Grid.SetRow, 2).SetValue(Grid.SetColumnSpan, 2)
								}
							}.SetValue(Grid.SetRow, 1),
							new StackPanel {
								Margin = WpfHelper.SmallMargin,
								Children = {
									(_RemoveSearchButton = new Button { Margin = WpfHelper.SmallVerticalMargin, Content = "Remove" }),
									(_MoveUpSearchButton = new Button { Margin = WpfHelper.SmallVerticalMargin, Content = "Move up" }),
									(_ResetSearchButton = new Button { Margin = WpfHelper.SmallVerticalMargin, Content = "Reset" }),
								}
							}.SetValue(Grid.SetColumn, 1),
							new StackPanel {
								Margin = WpfHelper.SmallMargin,
								Children = {
									(_AddSearchButton = new Button { Margin = WpfHelper.SmallVerticalMargin, Content = "Add" }),
									(_SaveSearchButton = new Button { Margin = WpfHelper.SmallVerticalMargin, Content = "Update" })
								}
							}.SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 1)
						}
					},
					new TitleBox("Search Result Browser"),
					new Note("Browser path (empty to use system default browser):"),
					new Grid {
						ColumnDefinitions = {
							new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), },
							new ColumnDefinition { Width = new GridLength(100, GridUnitType.Pixel) }
						},
						Children = {
							(_BrowserPath = new TextBox { Margin = WpfHelper.SmallHorizontalMargin, Text = Config.Instance.BrowserPath })
								.SetValue(Grid.SetColumn, 0),
							(_BrowseBrowserPath = new Button { Content = "Browse...", Margin = WpfHelper.SmallHorizontalMargin })
								.SetValue(Grid.SetColumn, 1)
						}
					},
					new Note("Browser parameter (optional, empty to use URL as parameter):"),
					_BrowserParameter = new TextBox { Margin = WpfHelper.SmallHorizontalMargin, Text = Config.Instance.BrowserParameter },
					new DescriptionBox("Use %u for search engine URL")

				);

				_Options = new[] { _ShiftToggleDisplay, _ManualDisplaySmartBar };
				_BrowserPath.TextChanged += _BrowserPath_TextChanged;
				_BrowserParameter.TextChanged += _BrowserParameter_TextChanged;
				_BrowseBrowserPath.Click += (s, args) => {
					var d = new OpenFileDialog {
						Title = "Locate your web browser to view search results",
						CheckFileExists = true,
						AddExtension = true,
						Filter = "Executable files (*.exe)|*.exe" };
					if (d.ShowDialog() == true) {
						_BrowserPath.Text = d.FileName;
					}
				};
				_SearchEngineList.Items.AddRange(Config.Instance.SearchEngines);
				_SearchEngineList.SelectionChanged += (s, args)=> RefreshSearchEngineUI();
				_AddSearchButton.Click += (s, args) => {
					var item = new SearchEngine("New Item", String.Empty);
					_SearchEngineList.Items.Add(item);
					Config.Instance.SearchEngines.Add(item);
					_SearchEngineList.SelectedIndex = _SearchEngineList.Items.Count - 1;
					RefreshSearchEngineUI();
					_SearchEngineName.Focus();
					Config.Instance.FireConfigChangedEvent(Features.SmartBar);
				};
				_RemoveSearchButton.Click += (s, args) => {
					var i = _SearchEngineList.SelectedItem as SearchEngine;
					if (MessageBox.Show("Are you sure to remove search engine " + i.Name + "?", nameof(Codist), MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
						var p = _SearchEngineList.SelectedIndex;
						_SearchEngineList.Items.RemoveAt(p);
						Config.Instance.SearchEngines.RemoveAt(p);
						RefreshSearchEngineUI();
						Config.Instance.FireConfigChangedEvent(Features.SmartBar);
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
						Config.Instance.FireConfigChangedEvent(Features.SmartBar);
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
					Config.Instance.FireConfigChangedEvent(Features.SmartBar);
				};
				_ResetSearchButton.Click += (s, args) => {
					if (MessageBox.Show("Do you want to reset search engines to default ones?\nALL existing items will be REMOVED.", nameof(Codist), MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
						Config.Instance.ResetSearchEngines();
						ResetSearchEngines(Config.Instance.SearchEngines);
						Config.Instance.FireConfigChangedEvent(Features.SmartBar);
					}
				};
				RefreshSearchEngineUI();
			}

			void _BrowserParameter_TextChanged(object sender, TextChangedEventArgs e) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.BrowserParameter = _BrowserParameter.Text;
				Config.Instance.FireConfigChangedEvent(Features.SmartBar);
			}

			void _BrowserPath_TextChanged(object sender, TextChangedEventArgs e) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.BrowserPath = _BrowserPath.Text;
				Config.Instance.FireConfigChangedEvent(Features.SmartBar);
			}

			protected override void LoadConfig(Config config) {
				var o = config.SmartBarOptions;
				Array.ForEach(_Options, i => i.UpdateWithOption(o));
				_BrowserPath.Text = config.BrowserPath;
				_BrowserParameter.Text = config.BrowserParameter;
				ResetSearchEngines(config.SearchEngines);
			}

			void UpdateConfig(SmartBarOptions options, bool set) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.Set(options, set);
				Config.Instance.FireConfigChangedEvent(Features.SmartBar);
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

	[Guid("CEBD6083-49F4-4579-94FF-C2774FFB4F9A")]
	sealed class NavigationBarPage : OptionsPage
	{
		UIElement _Child;

		protected override Features Feature => Features.NaviBar;
		protected override UIElement Child => _Child ?? (_Child = new PageControl(this));

		sealed class PageControl : OptionsPageContainer
		{
			readonly OptionBox<NaviBarOptions> _SyntaxDetail, _SymbolToolTip, _RegionOnBar, _StripRegionNonLetter, _RangeHighlight,
				_ParameterList, _ParameterListShowParamName, _FieldValue, _AutoPropertyAnnotation, _PartialClassMember, _Region, _RegionInMember, _LineOfCode;
			readonly OptionBox<NaviBarOptions>[] _Options;

			public PageControl(OptionsPage page) : base(page) {
				var o = Config.Instance.NaviBarOptions;
				AddPage("General",
					new Note("Currently navigation bar works for C# and markdown documents only"),

					new TitleBox("C# Navigation Bar"),
					_SyntaxDetail = o.CreateOptionBox(NaviBarOptions.SyntaxDetail, UpdateConfig, "Syntax detail")
						.SetLazyToolTip(() => "Displays syntax nodes (for instance, foreach, while, assignment and other statements) inside a member on navigation bar"),
					_SymbolToolTip = o.CreateOptionBox(NaviBarOptions.SymbolToolTip, UpdateConfig, "Symbol tool tip")
						.SetLazyToolTip(() => "Displays tool tip for items enclosing caret on navigation bar"),
					_RegionOnBar = o.CreateOptionBox(NaviBarOptions.RegionOnBar, UpdateConfig, "Region")
						.SetLazyToolTip(() => "Displays #region names enclosing caret on navigation bar"),
					_StripRegionNonLetter = o.CreateOptionBox(NaviBarOptions.StripRegionNonLetter, UpdateConfig, "Trim non-letter characters in region")
						.SetLazyToolTip(() => "Trims non-letter characters in region names, for instance, remove \"===========\" alike notations"),
					_RangeHighlight = o.CreateOptionBox(NaviBarOptions.RangeHighlight, UpdateConfig, "Highlight syntax range")
						.SetLazyToolTip(() => "Highlights the range of an item, on which the mouse is hovering, in the code editor"),

					new TitleBox("C# Navigation Bar Drop-down Menu"),
					_ParameterList = o.CreateOptionBox(NaviBarOptions.ParameterList, UpdateConfig, "Method parameter list")
						.SetLazyToolTip(() => "Displays parameters of methods in the drop-down menu of navigation bar"),
					_ParameterListShowParamName = o.CreateOptionBox(NaviBarOptions.ParameterListShowParamName, UpdateConfig, "Show parameter name instead of parameter type")
						.SetLazyToolTip(() => "Displays parameter names in method parameter list"),
					_FieldValue = o.CreateOptionBox(NaviBarOptions.FieldValue, UpdateConfig, "Initial value of fields and properties")
						.SetLazyToolTip(() => "Displays the initial values of fields and properties in the drop-down menu of navigation bar"),
					_AutoPropertyAnnotation = o.CreateOptionBox(NaviBarOptions.AutoPropertyAnnotation, UpdateConfig, "Property accessors")
						.SetLazyToolTip(() => "Displays accessor types of properties (\"{;;}\" for getter and setter, \"{;}\" for getter only) in the drop-down menu of navigation bar"),
					_PartialClassMember = o.CreateOptionBox(NaviBarOptions.PartialClassMember, UpdateConfig, "Include partial type and members")
						.SetLazyToolTip(() => "Displays members defined in other code files for partial types in the drop-down menu of navigation bar"),
					_Region = o.CreateOptionBox(NaviBarOptions.Region, UpdateConfig, "#region and #endregion")
						.SetLazyToolTip(() => "Displays #region and #endregion directives in the drop-down menu of navigation bar"),
					_RegionInMember = o.CreateOptionBox(NaviBarOptions.RegionInMember, UpdateConfig, "Include #region within member")
						.SetLazyToolTip(() => "Displays #region and #endregion directives defined within members of types in the drop-down menu of navigation bar"),
					_LineOfCode = o.CreateOptionBox(NaviBarOptions.LineOfCode, UpdateConfig, "Line of code")
						.SetLazyToolTip(() => "Displays number of lines for current type or members in the drop-down menu of navigation bar and tool tips"),
					new DescriptionBox("The above options will take effect when the navigation bar is repopulated"),

					new TitleBox("Shortcut Keys"),
					new DescriptionBox("Shortcut keys to access the menus of the navigation bar:\r\nCtrl+`, Ctrl+`: Edit.SearchDeclaration\r\nCtrl+1, Ctrl+1: Edit.SearchClassMember"),
					new DescriptionBox("Use the Keyboard mapper options page in Options dialog to reconfigure them")
					);

				_Options = new[] { _SyntaxDetail, _SymbolToolTip, _RegionOnBar, _StripRegionNonLetter, _RangeHighlight,
				_ParameterList, _ParameterListShowParamName, _FieldValue, _AutoPropertyAnnotation, _PartialClassMember, _Region, _RegionInMember, _LineOfCode };
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
				AddPage("General",
					new TitleBox("All languages"),
					new DescriptionBox("Markers for all code types"),
					_LineNumber = o.CreateOptionBox(MarkerOptions.LineNumber, UpdateConfig, "Line number")
						.SetLazyToolTip(() => "Draws line number values on the scroll bar"),
					_Selection = o.CreateOptionBox(MarkerOptions.Selection, UpdateConfig, "Selection")
						.SetLazyToolTip(() => "Draws selection ranges on the scroll bar"),
					_SpecialComment = o.CreateOptionBox(MarkerOptions.SpecialComment, UpdateConfig, "Tagged comments")
						.SetLazyToolTip(() => "Draws markers for tagged comments like To-Do, Hack, Note, etc. on the scroll bar"),

					new TitleBox("C#"),
					new DescriptionBox("Markers for C# type and member declarations and special directives"),
					_MarkerDeclarationLine = o.CreateOptionBox(MarkerOptions.MemberDeclaration, UpdateConfig, "Member declaration line")
						.SetLazyToolTip(() => "Draws lines indicating spans of type and member declarations on the scroll bar, with corresponding syntax highlight colors"),
					_LongMemberDeclaration = o.CreateOptionBox(MarkerOptions.LongMemberDeclaration, UpdateConfig, "Long method name")
						.SetLazyToolTip(() => "Draws names of long methods on the scroll bar"),
					_TypeDeclaration = o.CreateOptionBox(MarkerOptions.TypeDeclaration, UpdateConfig, "Type name")
						.SetLazyToolTip(() => "Draws names of declared types on the scroll bar"),
					_MethodDeclaration = o.CreateOptionBox(MarkerOptions.MethodDeclaration, UpdateConfig, "Method declaration spot")
						.SetLazyToolTip(() => "Draws squares for each methods on the scroll bar"),
					_RegionDirective = o.CreateOptionBox(MarkerOptions.RegionDirective, UpdateConfig, "#region name")
						.SetLazyToolTip(() => "Draws #region names on the scroll bar"),
					_CompilerDirective = o.CreateOptionBox(MarkerOptions.CompilerDirective, UpdateConfig, "Compiler directive")
						.SetLazyToolTip(() => "Draws circles for compiler directives on the scroll bar"),
					_SymbolReference = o.CreateOptionBox(MarkerOptions.SymbolReference, UpdateConfig, "Match symbol")
						.SetLazyToolTip(() => "Draws squares for other locations the same as current symbol under caret on the scroll bar")
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
				AddPage("General",
					new Note("This page contains functions for Visual Studio extension developers"),

					new TitleBox("Syntax Diagnostics"),
					new DescriptionBox("Peek the results of syntax classifiers and taggers"),
					_ShowDocumentContentType = Config.Instance.DeveloperOptions.CreateOptionBox(DeveloperOptions.ShowDocumentContentType, UpdateConfig, "Add \"Show Document Content Type\" command to File menu")
						.SetLazyToolTip(() => "Displays ContentType and base types of the active document"),
					_ShowSyntaxClassificationInfo = Config.Instance.DeveloperOptions.CreateOptionBox(DeveloperOptions.ShowSyntaxClassificationInfo, UpdateConfig, "Add \"Show Syntax Classification Info\" command to Smart Bar")
						.SetLazyToolTip(() => "Displays results of classifiers and taggers for selected content"),

					new TitleBox("Build"),
					_BuildVsixAutoIncrement = Config.Instance.BuildOptions.CreateOptionBox(BuildOptions.VsixAutoIncrement, UpdateConfig, "Automatically increment version revision number in .vsixmanifest file")
						.SetLazyToolTip(() => "Add 1 to the last number of your VSIX version, by modifying the .vsixmanifest file in the project, after each successful build")
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
			readonly OptionBox<DisplayOptimizations> _MainWindow, _CodeWindow/*, _UseLayoutRounding*/;

			public PageControl(OptionsPage page) : base(page) {
				AddPage("General",
					new TitleBox("Extra Line Margin"),
					new DescriptionBox("Add extra space between lines in code document window"),
					new WrapPanel {
						Children = {
							new StackPanel().MakeHorizontal()
								.Add(new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append("Top margin:"))
								.Add(_TopSpace = new Controls.IntegerBox((int)Config.Instance.TopSpace) { Minimum = 0, Maximum = 255 })
								.SetLazyToolTip(() => "This option adds extra margin above each line in code editor"),
							new StackPanel().MakeHorizontal()
								.Add(new TextBlock { MinWidth = 120, Margin = WpfHelper.SmallHorizontalMargin }.Append("Bottom margin:"))
								.Add(_BottomSpace = new Controls.IntegerBox((int)Config.Instance.BottomSpace) { Minimum = 0, Maximum = 255 })
								.SetLazyToolTip(() => "This option adds extra margin below each line in code editor"),
						}
					}.ForEachChild((FrameworkElement b) => b.MinWidth = MinColumnWidth),
					OptionPageControlHelper.CreateOptionBox(Config.Instance.NoSpaceBetweenWrappedLines, v => { Config.Instance.NoSpaceBetweenWrappedLines = v == true; Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight); }, "Don't add margin between wrapped lines"),

					new TitleBox("Force Grayscale Text Rendering"),
					new DescriptionBox("Disable ClearType"),
					new WrapPanel {
						Children = {
							(_MainWindow = Config.Instance.DisplayOptimizations.CreateOptionBox(DisplayOptimizations.MainWindow, UpdateMainWindowDisplayOption, "Apply to main window")),
							(_CodeWindow = Config.Instance.DisplayOptimizations.CreateOptionBox(DisplayOptimizations.CodeWindow, UpdateCodeWindowDisplayOption, "Apply to code window"))
						}
					}
					.ForEachChild((CheckBox b) => b.MinWidth = MinColumnWidth)
					.SetLazyToolTip(() => "If you feel text in the code window or the main window blurry and spotted with colors. Check these options to make text rendered in grayscale and see whether it helps."),
					new TextBox { TextWrapping = TextWrapping.Wrap, Text = "Note: For best text rendering effects, it is recommended to use MacType, which can be downloaded from: \nhttps://github.com/snowie2000/mactype/releases", Padding = WpfHelper.SmallMargin, IsReadOnly = true }
					);
				_TopSpace.ValueChanged += _TopSpace_ValueChanged;
				_BottomSpace.ValueChanged += _BottomSpace_ValueChanged;
			}

			protected override void LoadConfig(Config config) {
				_TopSpace.Value = (int)config.TopSpace;
				_BottomSpace.Value = (int)config.BottomSpace;
				var o = config.DisplayOptimizations;
				_MainWindow.UpdateWithOption(o);
				_CodeWindow.UpdateWithOption(o);
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

			void UpdateUseLayoutRoundingOption(DisplayOptimizations options, bool value) {
				if (Page.IsConfigUpdating) {
					return;
				}
				Config.Instance.Set(options, value);
				if (value) {
					Application.Current.MainWindow.UseLayoutRounding = value;
				}
				else {
					Application.Current.MainWindow.ClearValue(UseLayoutRoundingProperty);
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
}
