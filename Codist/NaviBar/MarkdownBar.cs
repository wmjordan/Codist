using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Codist.Classifiers;
using Codist.Controls;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

namespace Codist.NaviBar
{
	public sealed class MarkdownBar : ToolBar
	{
		const string DefaultActiveTitle = "Headings";
		readonly IWpfTextView _View;
		readonly ITextSearchService2 _TextSearch;
		readonly ExternalAdornment _ListContainer;
		readonly TaggerResult _Tags;
		readonly ThemedToolBarText _ActiveTitleLabel;
		ItemList _TitleList;
		ThemedTextBox _FinderBox;
		Predicate<object> _Filter;
		LocationItem[] _Titles;

		public MarkdownBar(IWpfTextView view, ITextSearchService2 textSearch) {
			_View = view;
			_TextSearch = textSearch;
			_ListContainer = _View.Properties.GetOrCreateSingletonProperty(() => new ExternalAdornment(view));
			_Tags = _View.Properties.GetProperty<TaggerResult>(typeof(TaggerResult));
			Name = nameof(MarkdownBar);
			this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
			view.Properties.AddProperty(nameof(NaviBar), this);
			view.Selection.SelectionChanged += Update;
			view.Closed += View_Closed;
			Resources = SharedDictionaryManager.Menu;
			SetResourceReference(BackgroundProperty, VsBrushes.CommandBarMenuBackgroundGradientKey);
			SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextInactiveKey);
			_ActiveTitleLabel = new ThemedToolBarText(DefaultActiveTitle);
			Items.Add(new ThemedButton(new StackPanel {
				Orientation = Orientation.Horizontal,
				Children = { ThemeHelper.GetImage(KnownImageIds.PageHeader), _ActiveTitleLabel }
			}, null, ShowTitleList) { Padding = WpfHelper.SmallMargin });
			//AddItem(KnownImageIds.Bold, ToggleBold);
			//AddItem(KnownImageIds.Italic, ToggleItalic);
			//AddItem(KnownImageIds.MarkupTag, ToggleCode);
			//AddItem(KnownImageIds.HyperLink, ToggleHyperLink);
			//AddItem(KnownImageIds.StrikeThrough, ToggleStrikeThrough);
		}

		void View_Closed(object sender, EventArgs e) {
			_View.Closed -= View_Closed;
			_View.Selection.SelectionChanged -= Update;
		}

		void Update(object sender, EventArgs e) {
			HideMenu();
			if (_Titles != null) {
				var i = GetSelectedTagIndex(_View.GetCaretPosition().Position);
				if (i != -1) {
					_ActiveTitleLabel.Text = _Titles[i].Text;
					return;
				}
			}
			_ActiveTitleLabel.Text = DefaultActiveTitle;
		}

		void HideMenu() {
			if (_TitleList != null) {
				_ListContainer.Children.Remove(_TitleList);
				_TitleList.SelectedItem = null;
				_TitleList = null;
			}
		}

		void ToggleBold(object sender, RoutedEventArgs e) {
			WrapWith("**", "**", true);
		}

		void ToggleItalic(object sender, RoutedEventArgs e) {
			WrapWith("_", "_", true);
		}

		void ToggleCode(object sender, RoutedEventArgs e) {
			WrapWith("`", "`", true);
		}

		void ToggleHyperLink(object sender, RoutedEventArgs e) {
			var s = WrapWith("[", "](url)", false);
			if (s.Snapshot != null) {
				// select the "url"
				_View.Selection.Select(new SnapshotSpan(s.Snapshot, s.Start + s.Length - 4, 3), false);
				_View.Caret.MoveTo(s.End - 1);
			}
		}

		void ToggleStrikeThrough(object sender, RoutedEventArgs e) {
			WrapWith("~~", "~~", true);
		}

		void ShowTitleList(object sender, RoutedEventArgs e) {
			if (_TitleList != null) {
				HideMenu();
				return;
			}
			var menu = new ItemList() {
				Container = _ListContainer,
				Header = new StackPanel {
					Margin = WpfHelper.MenuItemMargin,
					Children = {
						new Separator { Tag = new ThemedMenuText("Search Titles") },
						new StackPanel {
							Orientation = Orientation.Horizontal,
							Children = {
								ThemeHelper.GetImage(KnownImageIds.SearchContract).WrapMargin(WpfHelper.GlyphMargin),
								(_FinderBox = new ThemedTextBox(true) { MinWidth = 150 }),
								new ThemedButton(KnownImageIds.StopFilter, "Clear filter", ClearFilter)
							}
						},
					}
				},
				Footer = new TextBlock { Margin = WpfHelper.MenuItemMargin }
						.ReferenceProperty(TextBlock.ForegroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.SystemGrayTextBrushKey),
			};
			menu.ItemsControlMaxHeight = _View.ViewportHeight / 2;
			_Titles = Array.ConvertAll(_Tags.GetTags(), t => new LocationItem(t));
			menu.ItemsSource = _Titles;
			menu.SelectedIndex = GetSelectedTagIndex(_View.GetCaretPosition().Position);
			menu.ScrollToSelectedItem();
			menu.FilteredItems = new System.Windows.Data.ListCollectionView(_Titles);
			menu.MouseLeftButtonUp += MenuItemSelect;
			_FinderBox.TextChanged += SearchCriteriaChanged;
			_FinderBox.SetOnVisibleSelectAll();
			if (_Tags.Count > 100) {
				ScrollViewer.SetCanContentScroll(menu, true);
			}
			if (_TitleList != menu) {
				_ListContainer.Children.Remove(_TitleList);
				_ListContainer.Children.Add(menu);
				_TitleList = menu;
			}
			if (menu != null) {
				Canvas.SetLeft(menu, (sender as UIElement).TransformToVisual(_View.VisualElement).Transform(new Point()).X);
				Canvas.SetTop(menu, -1);
			}
		}

		int GetSelectedTagIndex(int p) {
			int selectedIndex = -1;
			for (int i = 0; i < _Titles.Length; i++) {
				if (_Titles[i].Span.Start > p) {
					break;
				}
				selectedIndex = i;
			}
			return selectedIndex;
		}

		void ClearFilter() {
			if (_FinderBox.Text.Length > 0) {
				_FinderBox.Text = String.Empty;
				_FinderBox.Focus();
			}
		}

		void SearchCriteriaChanged(object sender, TextChangedEventArgs e) {
			if (String.IsNullOrWhiteSpace(_FinderBox.Text)) {
				_Filter = null;
				RefreshItemsSource();
				return;
			}
			var k = _FinderBox.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			var comparison = Char.IsUpper(k[0][0]) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
			_Filter = o => MatchKeywords((o as LocationItem).Text, k, comparison);
			RefreshItemsSource();

			bool MatchKeywords(string text, string[] keywords, StringComparison c) {
				var m = 0;
				foreach (var item in keywords) {
					if ((m = text.IndexOf(item, m, c)) == -1) {
						return false;
					}
				}
				return true;
			}
		}

		public void RefreshItemsSource() {
			if (_Filter != null) {
				_TitleList.FilteredItems.Filter = _Filter;
				_TitleList.ItemsSource = _TitleList.FilteredItems;
			}
			else {
				_TitleList.ItemsSource = _Titles;
			}
		}

		void MenuItemSelect(object sender, MouseButtonEventArgs e) {
			if (e.OccursOn<ListBoxItem>()) {
				_View.VisualElement.Focus();
				(((ListBox)sender).SelectedItem as LocationItem)?.GoToSource(_View);
			}
		}

		SnapshotSpan WrapWith(string prefix, string suffix, bool selectModified) {
			string s = _View.GetFirstSelectionText();
			var firstModified = _View.WrapWith(prefix, suffix);
			if (s != null && Keyboard.Modifiers == ModifierKeys.Control && _View.FindNext(_TextSearch, s) == false) {
				//
			}
			else if (selectModified) {
				_View.SelectSpan(firstModified);
			}
			return firstModified;
		}

		void AddItem(int imageId, RoutedEventHandler clickHandler) {
			Items.Add(new ThemedButton(ThemeHelper.GetImage(imageId), null, clickHandler) { Padding = WpfHelper.SmallMargin });
		}

		sealed class LocationItem : ListItem
		{
			readonly TaggedContentSpan _Span;

			public LocationItem(TaggedContentSpan span) {
				_Span = span;
				Content = new ThemedMenuText(span.ContentText);
				if (span.Length == 1) {
					Content.FontWeight = FontWeights.Bold;
				}
				else if (span.Length > 2) {
					Content.Padding = new Thickness((span.Length - 2) * 10, 0, 0, 0);
				}
			}

			public override int ImageId {
				get {
					switch (_Span.Length) {
						case 1: return KnownImageIds.LevelOne;
						case 2: return KnownImageIds.LevelTwo;
						case 3: return KnownImageIds.LevelThree;
						case 4: return KnownImageIds.LevelFour;
						case 5: return KnownImageIds.LevelFive;
						default: return KnownImageIds.LevelAll;
					}
				}
			}
			public TaggedContentSpan Span => _Span;
			public string Text => _Span.ContentText;
			public void GoToSource(ITextView view) {
				view.SelectSpan(_Span.Start + _Span.ContentOffset, 0);
			}
		}
	}
}
