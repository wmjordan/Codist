using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CLR;
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
			__H1 = MarkdownTagger.HeaderClassificationTypes[1].ClassificationType,
			__H2 = MarkdownTagger.HeaderClassificationTypes[2].ClassificationType,
			__H3 = MarkdownTagger.HeaderClassificationTypes[3].ClassificationType,
			__H4 = MarkdownTagger.HeaderClassificationTypes[4].ClassificationType,
			__H5 = MarkdownTagger.HeaderClassificationTypes[5].ClassificationType,
			__H6 = MarkdownTagger.HeaderClassificationTypes[6].ClassificationType,
			__DummyTag1 = MarkdownTagger.DummyHeaderTags[1].ClassificationType,
			__DummyTag2 = MarkdownTagger.DummyHeaderTags[2].ClassificationType,
			__DummyTag3 = MarkdownTagger.DummyHeaderTags[3].ClassificationType,
			__DummyTag4 = MarkdownTagger.DummyHeaderTags[4].ClassificationType,
			__DummyTag5 = MarkdownTagger.DummyHeaderTags[5].ClassificationType,
			__DummyTag6 = MarkdownTagger.DummyHeaderTags[6].ClassificationType;
		readonly TaggerResult _Tags;
		readonly ThemedToolBarText _ActiveTitleLabel;
		MarkdownList _TitleList;
		int _FilterLevel;
		LocationItem[] _Titles;
		readonly ThemedImageButton _ActiveItem;
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
			_ActiveTitleLabel.Text = _Tags.GetPrecedingTaggedSpan(View.GetCaretPosition().Position)?.ContentText ?? DefaultActiveTitle;
		}

		void HideMenu() {
			if (_TitleList != null) {
				ViewOverlay.Remove(_TitleList);
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
			var m = WrapWith("[", "](url)", false);
			foreach (var s in m) {
				if (s.Snapshot != null) {
					// select the "url"
					View.Selection.Select(new SnapshotSpan(s.Snapshot, s.Start.Position + s.Length - 4, 3), false);
					View.Caret.MoveTo(s.End - 1);
					return;
				}
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
			var menu = new MarkdownList(this, titles) {
				ItemsControlMaxHeight = View.ViewportHeight / 2,
			};
			menu.ScrollToSelectedItem();
			menu.MouseLeftButtonUp += MenuItemSelect;
			if (_Tags.Count > 100) {
				menu.EnableVirtualMode = true;
			}
			if (_TitleList != menu) {
				if (_TitleList != null) {
					ViewOverlay.Remove(_TitleList);
					_TitleList.Dispose();
				}
				ViewOverlay.Add(menu);
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

		IEnumerable<SnapshotSpan> WrapWith(string prefix, string suffix, bool selectModified) {
			string s = View.GetFirstSelectionText();
			var modified = View.WrapWith(prefix, suffix);
			if (s != null && Keyboard.Modifiers.MatchFlags(ModifierKeys.Control | ModifierKeys.Shift)
				&& View.FindNext(_TextSearch, s, TextEditorHelper.GetFindOptionsFromKeyboardModifiers()) == false) {
				//
			}
			else if (selectModified && modified != null) {
				View.SelectSpans(modified);
			}
			return modified;
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

		sealed class MarkdownList : VirtualList
		{
			readonly MarkdownBar _Bar;
			readonly ThemedTextBox _FinderBox;
			Predicate<object> _Filter;

			public MarkdownList(MarkdownBar bar, LocationItem[] titles) {
				Style = SharedDictionaryManager.VirtualList.Get<Style>(typeof(VirtualList));
				Container = bar.ViewOverlay;
				ItemsSource = titles;
				SelectedIndex = GetSelectedTagIndex(titles, bar.View.GetCaretPosition().Position);
				FilteredItems = new System.Windows.Data.ListCollectionView(titles);

				var b = new ThemedButton[] {
					new ThemedButton(IconIds.Heading1, R.CMD_FilterToHeading1, ShowHeading1),
					new ThemedButton(IconIds.Heading2, R.CMD_FilterToHeading2, ShowHeading2),
					new ThemedButton(IconIds.Heading3, R.CMD_FilterToHeading3, ShowHeading3),
					new ThemedButton(IconIds.Heading4, R.CMD_FilterToHeading4, ShowHeading4),
					new ThemedButton(IconIds.Heading5, R.CMD_FilterToHeading5, ShowHeading5),
					new ThemedButton(IconIds.ClearFilter, R.CMD_ClearFilter, ClearFilter)
				};
				Header = new StackPanel {
					Margin = WpfHelper.MenuItemMargin,
					Children = {
						new Separator { Tag = new ThemedMenuText(R.CMD_SearchTitles) },
						new StackPanel {
							Orientation = Orientation.Horizontal,
							Children = {
								ThemeHelper.GetImage(IconIds.Search).WrapMargin(WpfHelper.GlyphMargin),
								(_FinderBox = new ThemedTextBox { MinWidth = 150 }),
								new ThemedControlGroup(b) { Margin = WpfHelper.SmallHorizontalMargin }
							}
						},
					}
				};
				Footer = new TextBlock { Margin = WpfHelper.MenuItemMargin }
						.ReferenceProperty(TextBlock.ForegroundProperty, EnvironmentColors.SystemGrayTextBrushKey)
						.AddImage(IconIds.LineOfCode)
						.Append(bar.View.TextSnapshot.LineCount);
				_FinderBox.TextChanged += SearchCriteriaChanged;
				_FinderBox.SetOnVisibleSelectAll();
				_Bar = bar;
				if (bar._FilterLevel != 0) {
					b[bar._FilterLevel - 1].Press();
				}
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
					FilteredItems.Filter = _Filter;
					ItemsSource = FilteredItems;
				}
				else {
					ItemsSource = _Bar._Titles;
				}
			}

			void ShowHeading1() {
				ShowHeading(1);
			}
			void ShowHeading2() {
				ShowHeading(2);
			}
			void ShowHeading3() {
				ShowHeading(3);
			}
			void ShowHeading4() {
				ShowHeading(4);
			}
			void ShowHeading5() {
				ShowHeading(5);
			}

			void ShowHeading(int heading) {
				_Bar._FilterLevel = heading;
				SearchCriteriaChanged(this, null);
			}

			void ClearFilter() {
				if (_FinderBox.Text.Length > 0) {
					_Bar._FilterLevel = 0;
					_FinderBox.Text = String.Empty;
					_FinderBox.Focus();
				}
				else if (_Bar._FilterLevel != 0) {
					_Bar._FilterLevel = 0;
					_Filter = null;
					RefreshItemsSource();
				}
			}

			void SearchCriteriaChanged(object sender, TextChangedEventArgs e) {
				if (String.IsNullOrWhiteSpace(_FinderBox.Text)) {
					if (_Bar._FilterLevel == 0) {
						_Filter = null;
					}
					else {
						_Filter = o => ((LocationItem)o).Level <= _Bar._FilterLevel;
					}
				}
				else {
					var k = _FinderBox.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
					var comparison = Char.IsUpper(k[0][0]) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
					if (_Bar._FilterLevel == 0) {
						_Filter = o => MatchKeywords((LocationItem)o, k, comparison);
					}
					else {
						_Filter = o => {
							var item = (LocationItem)o;
							return MatchKeywords(item, k, comparison) && item.Level <= _Bar._FilterLevel;
						};
					}
				}
				RefreshItemsSource();

				bool MatchKeywords(LocationItem title, string[] keywords, StringComparison c) {
					var m = 0;
					var text = title.Text;
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
				__H3Padding = new Thickness(10, 0, 0, 0),
				__H4Padding = new Thickness(20, 0, 0, 0),
				__H5Padding = new Thickness(30, 0, 0, 0),
				__H6Padding = new Thickness(40, 0, 0, 0);
			readonly TaggedContentSpan _Span;
			readonly int _ImageId;
			readonly int _Level;

			public LocationItem(TaggedContentSpan span) {
				_Span = span;
				Content = new ThemedMenuText(span.ContentText);
				var ct = span.Tag.ClassificationType;
				if (ct.CeqAny(__H1, __DummyTag1)) {
					Content.FontWeight = FontWeights.Bold;
					_Level = 1;
					_ImageId = IconIds.Heading1;
				}
				else if (ct.CeqAny(__H2, __DummyTag2)) {
					_Level = 2;
					_ImageId = IconIds.Heading2;
				}
				else if (ct.CeqAny(__H3, __DummyTag3)) {
					_Level = 3;
					_ImageId = IconIds.Heading3;
					Content.Padding = __H3Padding;
				}
				else if (ct.CeqAny(__H4, __DummyTag4)) {
					_Level = 4;
					_ImageId = IconIds.Heading4;
					Content.Padding = __H4Padding;
				}
				else if (ct.CeqAny(__H5, __DummyTag5)) {
					_Level = 5;
					_ImageId = IconIds.Heading5;
					Content.Padding = __H5Padding;
				}
				else if (ct.CeqAny(__H6, __DummyTag6)) {
					_Level = 6;
					_ImageId = IconIds.None;
					Content.Padding = __H6Padding;
				}
			}

			public override int ImageId => _ImageId;
			public TaggedContentSpan Span => _Span;
			public int Level => _Level;
			public string Text => _Span.ContentText;
			public void GoToSource(ITextView view) {
				view.SelectSpan(_Span.Start + _Span.ContentOffset, _Span.ContentLength, 1);
			}
		}
	}
}
