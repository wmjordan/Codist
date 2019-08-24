using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Codist.Taggers;
using Codist.Controls;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

namespace Codist.NaviBar
{
	public sealed class MarkdownBar : ToolBar, INaviBar
	{
		const string DefaultActiveTitle = "Headings";
		static readonly IClassificationType
			_H1 = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(Constants.MarkdownHeading1),
			_H2 = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(Constants.MarkdownHeading2),
			_H3 = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(Constants.MarkdownHeading3),
			_H4 = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(Constants.MarkdownHeading4),
			_H5 = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(Constants.MarkdownHeading5),
			_H6 = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(Constants.MarkdownHeading6);
		readonly IWpfTextView _View;
		readonly ITextSearchService2 _TextSearch;
		readonly ExternalAdornment _ListContainer;
		readonly TaggerResult _Tags;
		readonly ThemedToolBarText _ActiveTitleLabel;
		MarkdownList _TitleList;
		LocationItem[] _Titles;
		UIElement _ActiveItem;

		public MarkdownBar(IWpfTextView view, ITextSearchService2 textSearch) {
			_View = view;
			_TextSearch = textSearch;
			_ListContainer = _View.Properties.GetOrCreateSingletonProperty(() => new ExternalAdornment(view));
			_Tags = _View.Properties.GetProperty<TaggerResult>(typeof(TaggerResult));
			Name = nameof(MarkdownBar);
			this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
			view.Properties.AddProperty(nameof(NaviBar), this);
			view.Selection.SelectionChanged += Update;
			view.TextBuffer.PostChanged += Update;
			view.Closed += View_Closed;
			Resources = SharedDictionaryManager.Menu;
			SetResourceReference(BackgroundProperty, VsBrushes.CommandBarMenuBackgroundGradientKey);
			SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextInactiveKey);
			_ActiveTitleLabel = new ThemedToolBarText(DefaultActiveTitle);
			Items.Add(_ActiveItem = new ThemedButton(new StackPanel {
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
			_ActiveTitleLabel.Text = _Tags.GetPreceedingTaggedSpan(_View.GetCaretPosition().Position)?.ContentText ?? DefaultActiveTitle;
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
			_ActiveItem = sender as UIElement;
			ShowRootItemMenu();
		}

		public void ShowRootItemMenu() {
			var titles = _Titles = Array.ConvertAll(_Tags.GetTags(), t => new LocationItem(t));
			var menu = new MarkdownList(this) {
				ItemsControlMaxHeight = _View.ViewportHeight / 2,
				ItemsSource = titles,
				SelectedIndex = GetSelectedTagIndex(titles, _View.GetCaretPosition().Position),
				FilteredItems = new System.Windows.Data.ListCollectionView(titles)
			};
			menu.ScrollToSelectedItem();
			menu.MouseLeftButtonUp += MenuItemSelect;
			if (_Tags.Count > 100) {
				ScrollViewer.SetCanContentScroll(menu, true);
			}
			if (_TitleList != menu) {
				_ListContainer.Children.Remove(_TitleList);
				_ListContainer.Children.Add(menu);
				_TitleList = menu;
			}
			if (menu != null) {
				Canvas.SetLeft(menu, _ActiveItem.TransformToVisual(_View.VisualElement).Transform(new Point()).X);
				Canvas.SetTop(menu, -1);
			}
		}
		public void ShowActiveItemMenu() {
			ShowRootItemMenu();
		}

		static int GetSelectedTagIndex(LocationItem[] titles, int p) {
			if (titles == null) {
				return -1;
			}
			int selectedIndex = -1;
			for (int i = 0; i < titles.Length; i++) {
				if (titles[i].Span.Start > p) {
					break;
				}
				selectedIndex = i;
			}
			return selectedIndex;
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

		//void AddItem(int imageId, RoutedEventHandler clickHandler) {
		//	Items.Add(new ThemedButton(ThemeHelper.GetImage(imageId), null, clickHandler) { Padding = WpfHelper.SmallMargin });
		//}

		sealed class MarkdownList : ItemList
		{
			readonly MarkdownBar _Bar;
			ThemedTextBox _FinderBox;
			Predicate<object> _Filter;

			public MarkdownList(MarkdownBar bar) {
				Style = SharedDictionaryManager.ItemList.Get<Style>(typeof(ItemList));
				Container = bar._ListContainer;
				Header = new StackPanel {
					Margin = WpfHelper.MenuItemMargin,
					Children = {
						new Separator { Tag = new ThemedMenuText("Search Titles") },
						new StackPanel {
							Orientation = Orientation.Horizontal,
							Children = {
								ThemeHelper.GetImage(KnownImageIds.SearchContract).WrapMargin(WpfHelper.GlyphMargin),
								(_FinderBox = new ThemedTextBox { MinWidth = 150 }),
								new ThemedButton(KnownImageIds.StopFilter, "Clear filter", ClearFilter)
							}
						},
					}
				};
				Footer = new TextBlock { Margin = WpfHelper.MenuItemMargin }
						.ReferenceProperty(TextBlock.ForegroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.SystemGrayTextBrushKey)     
						.Append(ThemeHelper.GetImage(KnownImageIds.Code))
						.Append(bar._View.TextSnapshot.LineCount);
				_FinderBox.TextChanged += SearchCriteriaChanged;
				_FinderBox.SetOnVisibleSelectAll();
				_Bar = bar;
			}

			protected override void OnPreviewKeyDown(KeyEventArgs e) {
				base.OnPreviewKeyDown(e);
				if (e.OriginalSource is TextBox == false || e.Handled) {
					return;
				}
				if (e.Key == Key.Enter) {
					if (SelectedIndex == -1 && HasItems) {
						((LocationItem)ItemContainerGenerator.Items[0]).GoToSource(_Bar._View);
					}
					else {
						((LocationItem)SelectedItem).GoToSource(_Bar._View);
					}
					e.Handled = true;
				}
				else if (e.Key == Key.Escape) {
					_Bar.HideMenu();
				}
			}

			void RefreshItemsSource() {
				if (_Filter != null) {
					_Bar._TitleList.FilteredItems.Filter = _Filter;
					_Bar._TitleList.ItemsSource = _Bar._TitleList.FilteredItems;
				}
				else {
					_Bar._TitleList.ItemsSource = _Bar._Titles;
				}
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
				_Filter = o => MatchKeywords(((LocationItem)o).Text, k, comparison);
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
		}
		sealed class LocationItem : ListItem
		{
			static Thickness
				_H3Padding = new Thickness(10, 0, 0, 0),
				_H4Padding = new Thickness(20, 0, 0, 0),
				_H5Padding = new Thickness(30, 0, 0, 0),
				_H6Padding = new Thickness(40, 0, 0, 0);
			readonly TaggedContentSpan _Span;
			readonly int _ImageId;

			public LocationItem(TaggedContentSpan span) {
				_Span = span;
				Content = new ThemedMenuText(span.ContentText);
				var t = span.Tag.ClassificationType;
				if (t == _H1) {
					Content.FontWeight = FontWeights.Bold;
					_ImageId = KnownImageIds.FlagDarkRed;
				}
				else if (t == _H2) {
					_ImageId = KnownImageIds.FlagDarkPurple;
				}
				else if (t == _H3) {
					_ImageId = KnownImageIds.FlagDarkBlue;
					Content.Padding = _H3Padding;
				}
				else if (t == _H4) {
					_ImageId = KnownImageIds.Flag;
					Content.Padding = _H4Padding;
				}
				else if (t == _H5) {
					_ImageId = KnownImageIds.FlagOutline;
					Content.Padding = _H5Padding;
				}
				else if (t == _H6) {
					_ImageId = KnownImageIds.Blank;
					Content.Padding = _H6Padding;
				}
			}

			public override int ImageId => _ImageId;
			public TaggedContentSpan Span => _Span;
			public string Text => _Span.ContentText;
			public void GoToSource(ITextView view) {
				view.SelectSpan(_Span.Start + _Span.ContentOffset, 0);
			}
		}
	}
}
