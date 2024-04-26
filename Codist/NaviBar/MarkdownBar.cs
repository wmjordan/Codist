using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CLR;
using Codist.Controls;
using Codist.Taggers;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using R = Codist.Properties.Resources;

namespace Codist.NaviBar
{
	public sealed class MarkdownBar : NaviBar
	{
		readonly TaggerResult _Tags;
		readonly ThemedImageButton _ActiveTitle;
		readonly ThemedToolBarText _ActiveTitleLabel;
		readonly List<ThemedImageButton> _TagButtons = new List<ThemedImageButton>();
		MarkdownList _TitleList;
		int _FilterLevel;
		LocationItem[] _Titles;
		ITextSearchService2 _TextSearch;
		ITextStructureNavigator _TextNavigator;

		public MarkdownBar(IWpfTextView view, ITextSearchService2 textSearch) : base(view) {
			_TextSearch = textSearch;
			_TextNavigator = ServicesHelper.Instance.TextStructureNavigator.GetTextStructureNavigator(view.TextBuffer);
			_Tags = view.Properties.GetProperty<TaggerResult>(typeof(TaggerResult));
			Name = nameof(MarkdownBar);
			BindView();
			_ActiveTitleLabel = new ThemedToolBarText(R.T_Headings);
			Items.Add(_ActiveTitle = new ThemedImageButton(IconIds.Headings, _ActiveTitleLabel) { MaxWidth = 250 }
				.HandleEvent(ButtonBase.ClickEvent, ShowTitleList));
			AddTagButton(IconIds.TagBold, R.CMD_MarkBold, ToggleBold);
			AddTagButton(IconIds.TagItalic, R.CMD_MarkItalic, ToggleItalic);
			AddTagButton(IconIds.TagCode, R.CMD_MarkCode, ToggleCode);
			AddTagButton(IconIds.TagHyperLink, R.CMD_MarkLink, ToggleHyperLink);
			AddTagButton(IconIds.TagHighlight, R.CMD_MarkHighlight, ToggleHighlight);
			AddTagButton(IconIds.TagStrikeThrough, R.CMD_MarkStrikeThrough, ToggleStrikeThrough);
			AddTagButton(IconIds.TagUnderline, R.CMD_MarkUnderline, ToggleUnderline);
			AddTagButton(IconIds.Heading1, R.CMD_Heading1, MarkHeading1);
			AddTagButton(IconIds.Heading2, R.CMD_Heading2, MarkHeading2);
			AddTagButton(IconIds.Heading3, R.CMD_Heading3, MarkHeading3);
			AddTagButton(IconIds.Quotation, R.CMD_MarkQuotation, MarkQuotation);
			AddTagButton(IconIds.UnorderedList, R.CMD_UnorderedList, MarkUnorderedList);
			AddTagButton(IconIds.Indent, R.CMD_Indent, (s, args) => TextEditorHelper.ExecuteEditorCommand("Edit.IncreaseLineIndent"));
			AddTagButton(IconIds.Unindent, R.CMD_Unindent, (s, args) => TextEditorHelper.ExecuteEditorCommand("Edit.DecreaseLineIndent"));
			Items.AddRange(_TagButtons);
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
			View.VisualElement.PreviewKeyDown -= ClearRepeatAction;
			View.VisualElement.MouseLeftButtonUp -= RepeatAction;
			View.UnregisterRepeatingAction();
		}

		void Update(object sender, EventArgs e) {
			HideMenu();
			_ActiveTitleLabel.Text = _Tags.GetPrecedingTaggedSpan(View.GetCaretPosition(), i => i.Tag is MarkdownTitleTag)?.ContentText ?? R.T_Headings;
		}

		void HideMenu() {
			if (_TitleList != null) {
				ViewOverlay.Remove(_TitleList);
				_TitleList.SelectedItem = null;
				_TitleList.Dispose();
				_TitleList = null;
				_ActiveTitle.IsHighlighted = false;
			}
		}

		void StickTagButton(object sender, RoutedEventArgs e) {
			if (sender is ThemedImageButton b) {
				if (b.IsChecked) {
					b.IsChecked = false;
					View.UnregisterRepeatingAction();
				}
				else {
					View.UnregisterRepeatingAction();
					b.IsChecked = true;
					View.RegisterRepeatingAction(b.PerformClick, CancelRepeatAction);
					View.VisualElement.MouseLeftButtonUp += RepeatAction;
					View.VisualElement.PreviewKeyDown += ClearRepeatAction;
				}
			}
		}

		void RepeatAction(object sender, EventArgs e) {
			View.TryRepeatAction();
		}

		void ClearRepeatAction(object sender, EventArgs e) {
			View.UnregisterRepeatingAction();
		}

		void CancelRepeatAction() {
			View.VisualElement.MouseLeftButtonUp -= RepeatAction;
			View.VisualElement.PreviewKeyDown -= ClearRepeatAction;
			foreach (var item in _TagButtons) {
				item.IsChecked = false;
			}
		}

		void AddTagButton(int iconId, string toolTip, RoutedEventHandler clickHandler) {
			_TagButtons.Add(new ThemedImageButton(iconId) { ToolTip = new CommandToolTip(iconId, $"{toolTip}{Environment.NewLine}{R.T_RepeatCommandOnSelection}") }
				.HandleEvent(ButtonBase.ClickEvent, CheckStickyButton)
				.HandleEvent(ButtonBase.ClickEvent, clickHandler)
				.HandleEvent(MouseRightButtonUpEvent, StickTagButton));
		}

		void CheckStickyButton(object sender, RoutedEventArgs e) {
			if (e.Source is ThemedImageButton b
				&& b.IsChecked
				&& (b.InputHitTest(Mouse.GetPosition(b)) as DependencyObject).GetParentOrSelf<ThemedImageButton>() == b) {
				View.UnregisterRepeatingAction();
				e.Handled = true;
				return;
			}
		}

		void ToggleBold(object sender, RoutedEventArgs e) {
			if (Config.Instance.SmartBarOptions.MatchFlags(SmartBarOptions.UnderscoreBold)) {
				WrapWith("__", "__", true);
			}
			else {
				WrapWith("**", "**", true);
			}
		}

		void ToggleItalic(object sender, RoutedEventArgs e) {
			if (Config.Instance.SmartBarOptions.MatchFlags(SmartBarOptions.UnderscoreItalic)) {
				WrapWith("_", "_", true);
			}
			else {
				WrapWith("*", "*", true);
			}
		}

		void ToggleCode(object sender, RoutedEventArgs e) {
			WrapWith("`", "`", true);
		}

		void ToggleHyperLink(object sender, RoutedEventArgs e) {
			foreach (var s in WrapWith("[", "](url)", false)) {
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

		void ToggleHighlight(object sender, RoutedEventArgs e) {
			WrapWith("==", "==", true);
		}

		void ToggleUnderline(object sender, RoutedEventArgs e) {
			WrapWith("<u>", "</u>", true);
		}

		void MarkHeading1(object sender, RoutedEventArgs e) {
			MarkdownHelper.MarkList(View, MarkdownHelper.Heading1, true, MarkdownHelper.HeadingGlyph);
		}

		void MarkHeading2(object sender, RoutedEventArgs e) {
			MarkdownHelper.MarkList(View, MarkdownHelper.Heading2, true, MarkdownHelper.HeadingGlyph);
		}

		void MarkHeading3(object sender, RoutedEventArgs e) {
			MarkdownHelper.MarkList(View, MarkdownHelper.Heading3, true, MarkdownHelper.HeadingGlyph);
		}

		void MarkUnorderedList(object sender, RoutedEventArgs e) {
			MarkdownHelper.MarkList(View, MarkdownHelper.UnorderedList, true, default, true);
		}

		void MarkQuotation(object sender, RoutedEventArgs e) {
			MarkdownHelper.MarkList(View, MarkdownHelper.Quotation, false, default, true);
		}

		void ShowTitleList(object sender, RoutedEventArgs e) {
			if (_TitleList != null) {
				HideMenu();
				return;
			}
			ShowRootItemMenu(0);
		}

		public override void ShowRootItemMenu(int parameter) {
			_ActiveTitle.IsHighlighted = true;
			var tags = _Tags.GetTags(i => i.Tag is MarkdownTitleTag);
			var titles = _Titles = new LocationItem[tags.Length];
			for (int i = 0; i < titles.Length; i++) {
				titles[i] = new LocationItem(tags[i]);
			}
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
					_TitleList.MouseLeftButtonUp -= MenuItemSelect;
					ViewOverlay.Remove(_TitleList);
					_TitleList.Dispose();
				}
				ViewOverlay.Add(menu);
				_TitleList = menu;
			}
			if (menu != null) {
				Canvas.SetLeft(menu, _ActiveTitle.TransformToVisual(_ActiveTitle.GetParent<Grid>()).Transform(new Point()).X - View.VisualElement.TranslatePoint(new Point(), View.VisualElement.GetParent<Grid>()).X);
				Canvas.SetTop(menu, -1);
			}
		}
		public override void ShowActiveItemMenu() {
			ShowRootItemMenu(0);
		}

		int GetSelectedTagIndex() {
			var titles = _Titles;
			var p = View.GetCaretPosition().Position;
			if (titles == null) {
				return -1;
			}
			int selectedIndex = -1;
			for (int i = 0; i < titles.Length; i++) {
				var item = titles[i];
				if (item.Span.Start > p) {
					break;
				}
				if (_FilterLevel == 0 || item.Level <= _FilterLevel) {
					selectedIndex = i;
				}
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
			if (View.Selection.IsEmpty) {
				var e = _TextNavigator.GetExtentOfWord(View.Caret.Position.BufferPosition);
				if (e.IsSignificant == false || e.Span.IsEmpty) {
					return Enumerable.Empty<SnapshotSpan>();
				}
				View.SelectSpan(e.Span);
			}
			string s = View.GetFirstSelectionText();
			var modified = View.WrapWith(prefix, suffix);
			if (s != null && Keyboard.Modifiers.MatchFlags(ModifierKeys.Control | ModifierKeys.Shift)
				&& View.FindNext(_TextSearch, s, TextEditorHelper.GetFindOptionsFromKeyboardModifiers()) == false) {
				return modified;
			}
			if (selectModified && modified != null) {
				View.SelectSpans(modified);
			}
			return modified;
		}

		void View_Closed(object sender, EventArgs e) {
			var view = sender as ITextView;
			view.Closed -= View_Closed;
			view.Properties.RemoveProperty(typeof(TaggerResult));
			_TextSearch = null;
			_TextNavigator = null;
			if (_TitleList != null) {
				_TitleList.MouseLeftButtonUp -= MenuItemSelect;
				_TitleList.Dispose();
				_TitleList = null;
			}
			_TagButtons.Clear();
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
				SelectedIndex = bar.GetSelectedTagIndex();
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
								VsImageHelper.GetImage(IconIds.Search).WrapMargin(WpfHelper.GlyphMargin),
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
				var s = _Bar.GetSelectedTagIndex();
				if (_Filter != null) {
					FilteredItems.Filter = _Filter;
					ItemsSource = FilteredItems;
					if (s >= 0) {
						SelectedItem = _Bar._Titles[s];
					}
					else {
						SelectedIndex = -1;
					}
				}
				else {
					ItemsSource = _Bar._Titles;
					SelectedIndex = s;
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
				var ct = ((MarkdownTitleTag)span.Tag).TitleLevel;
				switch (ct) {
					case 1:
						Content.FontWeight = FontWeights.Bold;
						_Level = 1;
						_ImageId = IconIds.Heading1;
						return;
					case 2:
						_Level = 2;
						_ImageId = IconIds.Heading2;
						return;
					case 3:
						_Level = 3;
						_ImageId = IconIds.Heading3;
						Content.Padding = __H3Padding;
						return;
					case 4:
						_Level = 4;
						_ImageId = IconIds.Heading4;
						Content.Padding = __H4Padding;
						return;
					case 5:
						_Level = 5;
						_ImageId = IconIds.Heading5;
						Content.Padding = __H5Padding;
						return;
					case 6:
						_Level = 6;
						_ImageId = IconIds.None;
						Content.Padding = __H6Padding;
						return;
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
