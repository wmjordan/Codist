using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AppHelpers;
using Codist.Controls;
using Codist.Taggers;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using R = Codist.Properties.Resources;

namespace Codist.NaviBar
{
	public sealed class MarkdownBar : NaviBar
	{
		const string DefaultActiveTitle = "Headings";
		static readonly IClassificationType
			_H1 = MarkdownTaggerProvider.HeaderClassificationTypes[1].ClassificationType,
			_H2 = MarkdownTaggerProvider.HeaderClassificationTypes[2].ClassificationType,
			_H3 = MarkdownTaggerProvider.HeaderClassificationTypes[3].ClassificationType,
			_H4 = MarkdownTaggerProvider.HeaderClassificationTypes[4].ClassificationType,
			_H5 = MarkdownTaggerProvider.HeaderClassificationTypes[5].ClassificationType,
			_H6 = MarkdownTaggerProvider.HeaderClassificationTypes[6].ClassificationType,
			_DummyTag1 = MarkdownTaggerProvider.DummyHeaderTags[1].ClassificationType,
			_DummyTag2 = MarkdownTaggerProvider.DummyHeaderTags[2].ClassificationType,
			_DummyTag3 = MarkdownTaggerProvider.DummyHeaderTags[3].ClassificationType,
			_DummyTag4 = MarkdownTaggerProvider.DummyHeaderTags[4].ClassificationType,
			_DummyTag5 = MarkdownTaggerProvider.DummyHeaderTags[5].ClassificationType,
			_DummyTag6 = MarkdownTaggerProvider.DummyHeaderTags[6].ClassificationType;
		readonly TaggerResult _Tags;
		readonly ThemedToolBarText _ActiveTitleLabel;
		MarkdownList _TitleList;
		LocationItem[] _Titles;
		ThemedImageButton _ActiveItem;
		ITextSearchService2 _TextSearch;

		public MarkdownBar(IWpfTextView view, ITextSearchService2 textSearch) : base(view) {
			_TextSearch = textSearch;
			_Tags = view.Properties.GetProperty<TaggerResult>(typeof(TaggerResult));
			Name = nameof(MarkdownBar);
			BindView();
			_ActiveTitleLabel = new ThemedToolBarText(DefaultActiveTitle);
			_ActiveItem = new ThemedImageButton(IconIds.Headings, _ActiveTitleLabel);
			_ActiveItem.Click += ShowTitleList;
			Items.Add(_ActiveItem);
			//AddItem(KnownImageIds.Bold, ToggleBold);
			//AddItem(KnownImageIds.Italic, ToggleItalic);
			//AddItem(KnownImageIds.MarkupTag, ToggleCode);
			//AddItem(KnownImageIds.HyperLink, ToggleHyperLink);
			//AddItem(KnownImageIds.StrikeThrough, ToggleStrikeThrough);
			view.Closed += View_Closed;
		}

		protected internal override void BindView() {
			UnbindView();
			View.Caret.PositionChanged += Update;
			View.TextBuffer.PostChanged += Update;
		}

		protected override void UnbindView() {
			if (_TextSearch != null) {
				View.Caret.PositionChanged -= Update;
				View.TextBuffer.PostChanged -= Update;
			}
		}

		void Update(object sender, EventArgs e) {
			HideMenu();
			_ActiveTitleLabel.Text = _Tags.GetPreceedingTaggedSpan(View.GetCaretPosition().Position)?.ContentText ?? DefaultActiveTitle;
		}

		void HideMenu() {
			if (_TitleList != null) {
				ListContainer.Children.Remove(_TitleList);
				_TitleList.SelectedItem = null;
				_TitleList.Dispose();
				_TitleList = null;
				_ActiveItem.IsHighlighted = false;
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
				View.Selection.Select(new SnapshotSpan(s.Snapshot, s.Start + s.Length - 4, 3), false);
				View.Caret.MoveTo(s.End - 1);
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
			ShowRootItemMenu(0);
		}

		public override void ShowRootItemMenu(int parameter) {
			_ActiveItem.IsHighlighted = true;
			var titles = _Titles = Array.ConvertAll(_Tags.GetTags(), t => new LocationItem(t));
			var menu = new MarkdownList(this) {
				ItemsControlMaxHeight = View.ViewportHeight / 2,
				ItemsSource = titles,
				SelectedIndex = GetSelectedTagIndex(titles, View.GetCaretPosition().Position),
				FilteredItems = new System.Windows.Data.ListCollectionView(titles)
			};
			menu.ScrollToSelectedItem();
			menu.MouseLeftButtonUp += MenuItemSelect;
			if (_Tags.Count > 100) {
				menu.EnableVirtualMode = true;
			}
			if (_TitleList != menu) {
				if (_TitleList != null) {
					ListContainer.Children.Remove(_TitleList);
					_TitleList.Dispose();
				}
				ListContainer.Children.Add(menu);
				_TitleList = menu;
			}
			if (menu != null) {
				Canvas.SetLeft(menu, _ActiveItem.TransformToVisual(_ActiveItem.GetParent<Grid>()).Transform(new Point()).X - View.VisualElement.TranslatePoint(new Point(), View.VisualElement.GetParent<Grid>()).X);
				Canvas.SetTop(menu, -1);
			}
		}
		public override void ShowActiveItemMenu() {
			ShowRootItemMenu(0);
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
				View.VisualElement.Focus();
				(((ListBox)sender).SelectedItem as LocationItem)?.GoToSource(View);
			}
		}

		SnapshotSpan WrapWith(string prefix, string suffix, bool selectModified) {
			string s = View.GetFirstSelectionText();
			var firstModified = View.WrapWith(prefix, suffix);
			if (s != null && Keyboard.Modifiers.MatchFlags(ModifierKeys.Control | ModifierKeys.Shift)
				&& View.FindNext(_TextSearch, s, TextEditorHelper.GetFindOptionsFromKeyboardModifiers()) == false) {
				//
			}
			else if (selectModified) {
				View.SelectSpan(firstModified);
			}
			return firstModified;
		}

		void View_Closed(object sender, EventArgs e) {
			var view = sender as ITextView;
			view.Closed -= View_Closed;
			view.Properties.RemoveProperty(typeof(TaggerResult));
			_TextSearch = null;
			if (_TitleList != null) {
				_TitleList.Dispose();
				_TitleList = null;
			}
		}

		//void AddItem(int imageId, RoutedEventHandler clickHandler) {
		//	Items.Add(new ThemedButton(ThemeHelper.GetImage(imageId), null, clickHandler) { Padding = WpfHelper.SmallMargin });
		//}

		sealed class MarkdownList : VirtualList
		{
			readonly MarkdownBar _Bar;
			ThemedTextBox _FinderBox;
			Predicate<object> _Filter;

			public MarkdownList(MarkdownBar bar) {
				Style = SharedDictionaryManager.VirtualList.Get<Style>(typeof(VirtualList));
				Container = bar.ListContainer;
				Header = new StackPanel {
					Margin = WpfHelper.MenuItemMargin,
					Children = {
						new Separator { Tag = new ThemedMenuText(R.CMD_SearchTitles) },
						new StackPanel {
							Orientation = Orientation.Horizontal,
							Children = {
								ThemeHelper.GetImage(IconIds.Search).WrapMargin(WpfHelper.GlyphMargin),
								(_FinderBox = new ThemedTextBox { MinWidth = 150 }),
								new Border {
									BorderThickness = WpfHelper.TinyMargin,
									CornerRadius = new CornerRadius(3),
									Margin = WpfHelper.SmallHorizontalMargin,
									Child = new StackPanel {
										Children = {
											new ThemedButton(IconIds.ClearFilter, R.CMD_ClearFilter, ClearFilter)
										},
										Orientation = Orientation.Horizontal
									}
								}.ReferenceProperty(BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey)
							}
						},
					}
				};
				Footer = new TextBlock { Margin = WpfHelper.MenuItemMargin }
						.ReferenceProperty(TextBlock.ForegroundProperty, EnvironmentColors.SystemGrayTextBrushKey)     
						.Append(ThemeHelper.GetImage(IconIds.LineOfCode))
						.Append(bar.View.TextSnapshot.LineCount);
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
						((LocationItem)ItemContainerGenerator.Items[0]).GoToSource(_Bar.View);
					}
					else {
						((LocationItem)SelectedItem).GoToSource(_Bar.View);
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
				var ct = span.Tag.ClassificationType;
				if (ct == _H1 || ct == _DummyTag1) {
					Content.FontWeight = FontWeights.Bold;
					_ImageId = IconIds.Heading1;
				}
				else if (ct == _H2 || ct == _DummyTag2) {
					_ImageId = IconIds.Heading2;
				}
				else if (ct == _H3 || ct == _DummyTag3) {
					_ImageId = IconIds.Heading3;
					Content.Padding = _H3Padding;
				}
				else if (ct == _H4 || ct == _DummyTag4) {
					_ImageId = IconIds.Heading4;
					Content.Padding = _H4Padding;
				}
				else if (ct == _H5 || ct == _DummyTag5) {
					_ImageId = IconIds.Heading5;
					Content.Padding = _H5Padding;
				}
				else if (ct == _H6 || ct == _DummyTag6) {
					_ImageId = IconIds.None;
					Content.Padding = _H6Padding;
				}
			}

			public override int ImageId => _ImageId;
			public TaggedContentSpan Span => _Span;
			public string Text => _Span.ContentText;
			public void GoToSource(ITextView view) {
				view.SelectSpan(_Span.Start + _Span.ContentOffset, _Span.ContentLength, 1);
			}
		}
	}
}
