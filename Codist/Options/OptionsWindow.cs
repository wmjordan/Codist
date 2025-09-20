using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using CLR;
using Codist.Controls;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;

namespace Codist.Options
{
	sealed partial class OptionsWindow : Window
	{
		readonly IConfigManager _ConfigManager;
		readonly ListBox _OptionCategoriesBox;
		readonly Label _TitleLabel;
		readonly ScrollViewer _OptionContainer;
		bool _ExitCalled;

		public OptionsWindow() {
			Title = Constants.NameOfMe + " - " + R.T_Options;
			ShowInTaskbar = false;
			MinHeight = 300;
			MinWidth = 480;
			SnapsToDevicePixels = true;
			Owner = Application.Current.MainWindow;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			Resources = SharedDictionaryManager.ThemedControls;
			this.ReferenceProperty(ForegroundProperty, CommonControlsColors.TextBoxTextBrushKey);
			this.ReferenceProperty(BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
			Resources = Application.Current.TryFindResource(VsResourceKeys.ThemedDialogDefaultStylesKey) as ResourceDictionary;
			IsVisibleChanged += WindowIsVisibleChanged;

			_OptionCategoriesBox = new ListBox {
				Items = {
					new GeneralOptionPage(),
					new SyntaxHighlightPage(),
					new SuperQuickInfoPage(),
					new SuperQuickInfoCSharpPage(),
					new NavigationBarPage(),
					new SmartBarPage(),
					new WrapTextPage(),
					new WebSearchPage(),
					new ScrollBarMarkerPage(),
					new DisplayPage(),
					new AutoPunctuation(),
					new ExtensionDeveloperPage(),
				}
			}.ReferenceStyle(VsResourceKeys.ThemedDialogListBoxStyleKey);

			Content = new Border {
				Padding = WpfHelper.MiddleMargin,
				Child = new Grid {
					ColumnDefinitions = {
						new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
						new ColumnDefinition { Width = new GridLength(10, GridUnitType.Star) }
					},
					Children = {
						new StackPanel {
							Margin = WpfHelper.MiddleMargin,
							Children = {
								new TextBlock {
									Text = Constants.NameOfMe,
									Height = 24,
									VerticalAlignment = VerticalAlignment.Bottom,
									FontWeight = FontWeights.Bold
								},
								_OptionCategoriesBox,
							}
						},

						new Grid {
							Margin = WpfHelper.MiddleMargin,
							RowDefinitions = {
								new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
								new RowDefinition { Height = new GridLength(10, GridUnitType.Star) },
								new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
							},
							Children = {
								new Label {}
								.ReferenceProperty(ForegroundProperty, ThemedDialogColors.HeaderTextBrushKey)
								.ReferenceStyle(VsResourceKeys.LabelEnvironment200PercentFontSizeStyleKey)
								.Set(ref _TitleLabel),

								new ScrollViewer {
									Margin = WpfHelper.MiddleMargin,
									VerticalScrollBarVisibility = ScrollBarVisibility.Auto
								}.Set(ref _OptionContainer)
								.ReferenceStyle(VsResourceKeys.ScrollViewerStyleKey)
								.SetValue(Grid.SetRow, 1),

								new WrapPanel {
									HorizontalAlignment = HorizontalAlignment.Right,
									Children = {
										new ThemedButton(R.CMD_SaveHighlightChanges, R.CMDT_SaveChanges, Ok) { IsDefault = true, Width = 80, Margin = new Thickness(10) }.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
										new ThemedButton(R.CMD_Cancel, R.CMDT_UndoChanges, Cancel) { IsCancel = true, Width = 80, Margin = new Thickness(10) }.ReferenceStyle(VsResourceKeys.ButtonStyleKey)
									}
								}.SetValue(Grid.SetRow, 2)
							}
						}
						.SetValue(Grid.SetColumn, 1)
					}
				}
			}.ReferenceProperty(Border.BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
			_OptionCategoriesBox.SelectionChanged += OptionCategoryChanged;
			_OptionCategoriesBox.SelectedIndex = 0;

			_ConfigManager = Config.Instance.BeginUpdate();
		}

		public void ShowOptionPage(string name) {
			foreach (var item in _OptionCategoriesBox.Items) {
				if (item is OptionPageFactory f && f.Name == name) {
					_OptionCategoriesBox.SelectedItem = item;
					break;
				}
			}
		}

		void OptionCategoryChanged(object sender, SelectionChangedEventArgs e) {
			var pageFactory = (OptionPageFactory)_OptionCategoriesBox.SelectedValue;
			_TitleLabel.Content = pageFactory.Name;
			OptionPage page;
			_OptionContainer.Content = page = pageFactory.Page;
			page.IsEnabled = Config.Instance.Features.MatchFlags(pageFactory.RequiredFeature);
		}

		protected override void OnClosed(EventArgs e) {
			Owner.Activate();
			if (!_ExitCalled) {
				Exit(false, true);
			}
			base.OnClosed(e);
		}

		void WindowIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
			if (IsVisible == false) {
				Owner.Activate();
			}
		}

		void Ok() {
			Exit(true, true);
			Close();
		}

		void Exit(bool apply, bool quitConfig) {
			foreach (var item in _OptionCategoriesBox.Items) {
				if (item is OptionPageFactory f) {
					f.UnloadPage();
				}
			}

			if (quitConfig) {
				_ConfigManager.Quit(apply);
			}
			_ExitCalled = true;
		}

		void Cancel() {
			Exit(false, true);
			Close();
		}

		abstract class OptionPageFactory
		{
			OptionPage _Control;

			public abstract string Name { get; }
			public abstract Features RequiredFeature { get; }
			public virtual bool IsSubOption => false;
			public OptionPage Page {
				get => _Control ?? (_Control = CreatePage());
			}
			protected abstract OptionPage CreatePage();

			public override string ToString() {
				return IsSubOption ? "  " + Name : Name;
			}

			public void UnloadPage() {
				_Control?.UnloadPage();
			}
		}

		abstract class OptionPage : ContentPresenter
		{
			protected static readonly Thickness SubOptionMargin = new Thickness(24, 2, 0, 2);
			protected const double MinColumnWidth = 230;
			int _UILock;

			protected OptionPage() {
				Config.RegisterLoadHandler(InternalLoadConfig);
			}

			protected bool IsConfigUpdating => _UILock != 0;

			void InternalLoadConfig(Config config) {
				if (Interlocked.CompareExchange(ref _UILock, 1, 0) == 0) {
					LoadConfig(config);
					_UILock = 0;
				}
			}

			protected void SetContents(params UIElement[] contents) {
				Content = new StackPanel { Margin = WpfHelper.MiddleMargin }.Add(contents);
			}

			protected abstract void LoadConfig(Config config);

			public void UnloadPage() {
				Config.UnregisterLoadHandler(InternalLoadConfig);
			}
		}
	}
}
