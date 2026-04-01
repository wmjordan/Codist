using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Codist.Controls;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text.Editor;
using R = Codist.Properties.Resources;

namespace Codist.SnippetTexts;

/// <summary>
/// A picker control for wrap text.
/// </summary>
sealed class WrapTextPicker : UserControl
{
	readonly ThemedTextBox _SearchBox;
	readonly ThemedListBox _ListBox;
	readonly ListCollectionView _ListView;
	readonly List<WrapText> _AllItems;
	readonly Popup _ListContainer;
	readonly IWpfTextView _TextView;
	readonly TextViewOverlay _Overlay;
	ActiveWrapTextTracker _Tracker;

	public event EventHandler<EventArgs<WrapText>> ItemSelected;

	public WrapText SelectedItem => _ListBox.SelectedItem as WrapText;

	public WrapTextPicker(IWpfTextView view, List<WrapText> items) {
		_AllItems = items;
		_TextView = view;
		_Overlay = TextViewOverlay.GetOrCreate(view);

		_SearchBox = new ThemedTextBox {
			VerticalContentAlignment = VerticalAlignment.Center,
			Width = 140
		};
		_ListView = new ListCollectionView(_AllItems) { Filter = FilterPredicate };
		_ListBox = new ThemedListBox {
			Margin = WpfHelper.MiddleHorizontalMargin,
			ItemsSource = _ListView,
			DisplayMemberPath = "Name",
			ItemContainerStyle = new Style(typeof(ListBoxItem)) {
				Setters = {
					new Setter(ToolTipService.ToolTipProperty,
						new Binding {
							Path = new PropertyPath("."),
							Converter = WrapTextToToolTipConverter.Instance
						}),
					new Setter(ToolTipService.PlacementProperty, PlacementMode.Right)
				}
			},
			Width = 140,
			MinHeight = 40,
			MaxHeight = 250,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		_ListContainer = new Popup {
			AllowsTransparency = true,
			Child = _ListBox,
			StaysOpen = true,
			Placement = PlacementMode.Bottom,
			PlacementTarget = _SearchBox,
		};
		Content = new ToolBarTray {
			IsLocked = true,
			ToolBars = {
				 new ToolBar {
					Margin = new Thickness(-120, 0, 0, 0),
					BorderThickness = WpfHelper.TinyMargin,
					Padding = WpfHelper.SmallMargin,
					IsOverflowOpen = false,
					Items = {
						new ThemedToolBarText {
							Text = R.OT_WrapText,
							VerticalAlignment = VerticalAlignment.Center,
							Margin = WpfHelper.SmallHorizontalMargin,
							Width = 120
						},
						_SearchBox,
						new Button { Content = VsImageHelper.GetImage(IconIds.Settings) }
							.SetLazyToolTip(i => new CommandToolTip(IconIds.Settings, R.CMDT_CustomizeWrapTexts))
							.HandleEvent(Button.ClickEvent, HandleOptionButton)
					}
				}.HideOverflow()
				.ReferenceProperty(BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey)
				.ReferenceProperty(BorderBrushProperty, EnvironmentColors.ToolWindowBorderBrushKey)
			}
		};

		Loaded += OnLoaded;
		PreviewKeyDown += OnPreviewKeyDown;
		_SearchBox.TextChanged += OnSearchTextChanged;
	}

	public void Show() {
		if (_Tracker != null) {
			return;
		}
		_Tracker = ActiveWrapTextTracker.Get(_TextView);
		var recent = _Tracker.Active;
		if (recent != null && _AllItems.Contains(recent)) {
			_ListView.MoveCurrentTo(recent);
			_ListBox.SelectedItem = recent;
		}

		_Overlay.Position(this, Mouse.GetPosition(_Overlay), 250);
		_Overlay.Add(this);
		_TextView.Selection.SelectionChanged += WrapTextCancelled;
		_TextView.VisualElement.SizeChanged += WrapTextCancelled;
		_ListBox.MouseLeftButtonUp += OnListSelectionChanged;
		Unloaded += HandleUnloaded;
		Application.Current.MainWindow.Deactivated += WrapTextCancelled;
		Application.Current.MainWindow.LocationChanged += WrapTextCancelled;
	}

	void WrapTextCancelled(object sender, EventArgs e) {
		Close();
	}

	public void Close() {
		if (_Tracker is null) {
			return;
		}

		_ListContainer.IsOpen = false;
		_TextView.Selection.SelectionChanged -= WrapTextCancelled;
		_TextView.VisualElement.SizeChanged -= WrapTextCancelled;
		_ListBox.MouseLeftButtonUp -= OnListSelectionChanged;
		Unloaded -= HandleUnloaded;
		Application.Current.MainWindow.Deactivated -= WrapTextCancelled;
		Application.Current.MainWindow.LocationChanged -= WrapTextCancelled;
		_Overlay.RemoveAndDispose(this);
		_Tracker = null;
	}

	void HandleUnloaded(object sender, RoutedEventArgs e) {
		_ListContainer.IsOpen = false;
		Close();
	}

	void HandleOptionButton(object sender, RoutedEventArgs e) {
		Close();
		Commands.OptionsWindowCommand.ShowOptionPage(R.OT_WrapText);
	}

	void OnLoaded(object sender, RoutedEventArgs e) {
		Loaded += OnLoaded;
		_SearchBox.Focus();

		_ListContainer.IsOpen = true;

		if (_ListBox.SelectedItem == null && _ListView.Cast<WrapText>().Any()) {
			_ListBox.SelectedIndex = 0;
		}

		if (_ListBox.SelectedItem != null) {
			_ListBox.ScrollIntoView(_ListBox.SelectedItem);
		}
	}

	void OnPreviewKeyDown(object sender, KeyEventArgs e) {
		if (e.Key == Key.Enter) {
			if (_ListBox.SelectedItem is WrapText i) {
				OnSelect(i);
				e.Handled = true;
			}
			return;
		}

		if (e.Key == Key.Escape) {
			Close();
			e.Handled = true;
			return;
		}

		if (_SearchBox.IsKeyboardFocusWithin) {
			int currentIndex = _ListBox.SelectedIndex;
			int totalCount = _ListView.Cast<WrapText>().Count();

			switch (e.Key) {
				case Key.Up:
					if (currentIndex > 0) {
						_ListBox.SelectedIndex = currentIndex - 1;
					}
					e.Handled = true;
					break;
				case Key.Down:
					if (currentIndex < totalCount - 1) {
						_ListBox.SelectedIndex = currentIndex + 1;
					}
					e.Handled = true;
					break;
				case Key.PageUp:
					_ListBox.SelectedIndex = Math.Max(0, currentIndex - 10);
					e.Handled = true;
					break;
				case Key.PageDown:
					_ListBox.SelectedIndex = Math.Min(totalCount - 1, currentIndex + 10);
					e.Handled = true;
					break;
			}

			if (e.Handled && _ListBox.SelectedItem != null) {
				_ListBox.ScrollIntoView(_ListBox.SelectedItem);
			}
		}
	}

	void OnSearchTextChanged(object sender, TextChangedEventArgs e) {
		_ListView.Refresh();

		if (_ListBox.SelectedItem == null && _ListView.Cast<WrapText>().Any()) {
			_ListBox.SelectedIndex = 0;
		}
	}

	void OnSelect(WrapText wrapText) {
		ItemSelected?.Invoke(this, new EventArgs<WrapText>(wrapText));
		_Tracker.Active = wrapText;
		wrapText.WrapSelections(_TextView);
		Close();
	}

	void OnListSelectionChanged(object sender, RoutedEventArgs e) {
		if (_ListBox.SelectedItem is WrapText t) {
			OnSelect(t);
		}
	}

	bool FilterPredicate(object obj) {
		var text = _SearchBox.Text;
		return string.IsNullOrWhiteSpace(text)
			|| ((WrapText)obj)?.Name?.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
	}

	sealed class WrapTextToToolTipConverter : IValueConverter
	{
		public readonly static WrapTextToToolTipConverter Instance = new();
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is WrapText wrapText) {
				var textBlock = new TextBlock {
					Foreground = SymbolFormatter.Instance.PlainText,
					FontFamily = ThemeCache.CodeTextFont,
					FontSize = ThemeCache.ToolTipFontSize,
					Padding = new Thickness(4)
				};
				wrapText.Render(textBlock.Inlines);
				return textBlock;
			}
			return null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();
	}
}
