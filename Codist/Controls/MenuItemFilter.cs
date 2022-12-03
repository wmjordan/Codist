using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.CodeAnalysis;

namespace Codist.Controls
{
	sealed class MenuItemFilter : ISymbolFilterable
	{
		readonly ItemCollection _Items;
		public MenuItemFilter(ItemCollection items) {
			_Items = items;
		}

		public SymbolFilterKind SymbolFilterKind => SymbolFilterKind.Member;

		public void Filter(string[] keywords, int filterFlags) {
			bool useModifierFilter = (MemberFilterTypes)filterFlags != MemberFilterTypes.All;
			if (keywords.Length == 0) {
				foreach (UIElement item in _Items) {
					item.Visibility = item is ThemedMenuItem.MenuItemPlaceHolder == false
						&& (useModifierFilter == false || item is ISymbolFilter menuItem && menuItem.Filter(filterFlags))
						? Visibility.Visible
						: Visibility.Collapsed;
				}
				return;
			}
			ISymbolFilter filterable;
			foreach (UIElement item in _Items) {
				var menuItem = item as MenuItem;
				if (useModifierFilter) {
					filterable = item as ISymbolFilter;
					if (filterable != null) {
						if (filterable.Filter(filterFlags) == false && (menuItem == null || menuItem.HasItems == false)) {
							item.Visibility = Visibility.Collapsed;
							continue;
						}
						item.Visibility = Visibility.Visible;
					}
				}
				if (menuItem == null) {
					item.Visibility = Visibility.Collapsed;
					continue;
				}
				var b = menuItem.Header as TextBlock;
				if (b == null) {
					continue;
				}
				if (FilterSignature(b.GetText(), keywords)) {
					menuItem.Visibility = Visibility.Visible;
					if (menuItem.HasItems) {
						foreach (MenuItem sub in menuItem.Items) {
							sub.Visibility = Visibility.Visible;
						}
					}
					continue;
				}
				var matchedSubItem = false;
				if (menuItem.HasItems) {
					foreach (MenuItem sub in menuItem.Items) {
						if (useModifierFilter) {
							filterable = sub as ISymbolFilter;
							if (filterable != null) {
								if (filterable.Filter(filterFlags) == false) {
									sub.Visibility = Visibility.Collapsed;
									continue;
								}
								sub.Visibility = Visibility.Visible;
							}
						}
						b = sub.Header as TextBlock;
						if (b == null) {
							continue;
						}
						if (FilterSignature(b.GetText(), keywords)) {
							matchedSubItem = true;
							sub.Visibility = Visibility.Visible;
						}
						else {
							sub.Visibility = Visibility.Collapsed;
						}
					}
				}
				menuItem.Visibility = matchedSubItem ? Visibility.Visible : Visibility.Collapsed;
			}

			bool FilterSignature(string text, string[] words) {
				return words.All(p => text.IndexOf(p, StringComparison.OrdinalIgnoreCase) != -1);
			}
		}
	}
}
