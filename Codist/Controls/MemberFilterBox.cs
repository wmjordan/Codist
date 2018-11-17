using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Imaging;
using AppHelpers;

namespace Codist.Controls
{
	interface IMemberFilterable
	{
		bool Filter(MemberFilterTypes filterTypes);
	}

	sealed class MemberFilterBox : StackPanel
	{
		readonly ThemedTextBox _FilterBox;
		readonly MemberFilterButtonGroup _FilterButtons;
		readonly ItemCollection _Items;

		public MemberFilterBox(ItemCollection items) {
			Orientation = Orientation.Horizontal;
			Margin = WpfHelper.MenuItemMargin;
			Children.Add(ThemeHelper.GetImage(KnownImageIds.Filter).WrapMargin(WpfHelper.GlyphMargin));
			Children.Add(_FilterBox = new ThemedTextBox() {
				MinWidth = 150,
				ToolTip = new ThemedToolTip("Result Filter", "Filter items in this menu.\nUse space to separate keywords.")
			});
			Children.Add(_FilterButtons = new MemberFilterButtonGroup());
			_Items = items;
			_FilterButtons.FilterChanged += FilterChanged;
			_FilterBox.TextChanged += FilterChanged;
		}

		void FilterChanged(object sender, EventArgs e) {
			Filter(_FilterBox.Text.Split(' '), _FilterButtons.Filters);
		}
		void Filter(string[] keywords, MemberFilterTypes filters) {
			bool useModifierFilter = filters != MemberFilterTypes.None;
			if (keywords.Length == 0) {
				foreach (UIElement item in _Items) {
					item.Visibility = item is ThemedMenuItem.MenuItemPlaceHolder 
						|| (useModifierFilter && item is IMemberFilterable menuItem && menuItem.Filter(filters) == false)
						? Visibility.Collapsed
						: Visibility.Visible;
				}
				return;
			}
			IMemberFilterable filterable;
			foreach (UIElement item in _Items) {
				if (useModifierFilter) {
					filterable = item as IMemberFilterable;
					if (filterable != null) {
						if (filterable.Filter(filters) == false) {
							item.Visibility = Visibility.Collapsed;
							continue;
						}
						item.Visibility = Visibility.Visible;
					}
				}
				var menuItem = item as MenuItem;
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
							filterable = item as IMemberFilterable;
							if (filterable != null) {
								if (filterable.Filter(filters) == false) {
									item.Visibility = Visibility.Collapsed;
									continue;
								}
								item.Visibility = Visibility.Visible;
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

		internal static bool FilterByImageId(MemberFilterTypes filterTypes, int imageId) {
			switch (imageId) {
				case KnownImageIds.ClassPublic:
				case KnownImageIds.InterfacePublic:
				case KnownImageIds.StructurePublic:
				case KnownImageIds.EnumerationPublic:
					return filterTypes.HasAnyFlag(MemberFilterTypes.Public | MemberFilterTypes.NestedType);
				case KnownImageIds.ClassPrivate:
				case KnownImageIds.InterfacePrivate:
				case KnownImageIds.StructurePrivate:
				case KnownImageIds.EnumerationPrivate:
					return filterTypes.HasAnyFlag(MemberFilterTypes.Private | MemberFilterTypes.NestedType);
				case KnownImageIds.ClassProtected:
				case KnownImageIds.InterfaceProtected:
				case KnownImageIds.StructureProtected:
				case KnownImageIds.EnumerationProtected:
					return filterTypes.HasAnyFlag(MemberFilterTypes.Protected | MemberFilterTypes.NestedType);
				case KnownImageIds.ClassInternal:
				case KnownImageIds.InterfaceInternal:
				case KnownImageIds.StructureInternal:
				case KnownImageIds.EnumerationInternal:
					return filterTypes.HasAnyFlag(MemberFilterTypes.Internal | MemberFilterTypes.NestedType);
				case KnownImageIds.ClassShortcut:
				case KnownImageIds.InterfaceShortcut:
				case KnownImageIds.StructureShortcut:
					return filterTypes.HasAnyFlag(MemberFilterTypes.NestedType);
				case KnownImageIds.MethodPublic:
				case KnownImageIds.NewItem:
				case KnownImageIds.OperatorPublic:
					return filterTypes.HasAnyFlag(MemberFilterTypes.Public | MemberFilterTypes.Method);
				case KnownImageIds.MethodProtected:
				case KnownImageIds.OperatorProtected:
					return filterTypes.HasAnyFlag(MemberFilterTypes.Protected | MemberFilterTypes.Method);
				case KnownImageIds.MethodInternal:
				case KnownImageIds.OperatorInternal:
					return filterTypes.HasAnyFlag(MemberFilterTypes.Internal | MemberFilterTypes.Method);
				case KnownImageIds.MethodPrivate:
				case KnownImageIds.OperatorPrivate:
					return filterTypes.HasAnyFlag(MemberFilterTypes.Private | MemberFilterTypes.Method);
				case KnownImageIds.FieldPublic:
				case KnownImageIds.ConstantPublic:
				case KnownImageIds.PropertyPublic:
				case KnownImageIds.EventPublic:
					return filterTypes.HasAnyFlag(MemberFilterTypes.Public | MemberFilterTypes.FieldAndProperty);
				case KnownImageIds.FieldProtected:
				case KnownImageIds.ConstantProtected:
				case KnownImageIds.PropertyProtected:
				case KnownImageIds.EventProtected:
					return filterTypes.HasAnyFlag(MemberFilterTypes.Protected | MemberFilterTypes.FieldAndProperty);
				case KnownImageIds.FieldInternal:
				case KnownImageIds.ConstantInternal:
				case KnownImageIds.PropertyInternal:
				case KnownImageIds.EventInternal:
					return filterTypes.HasAnyFlag(MemberFilterTypes.Internal | MemberFilterTypes.FieldAndProperty);
				case KnownImageIds.FieldPrivate:
				case KnownImageIds.ConstantPrivate:
				case KnownImageIds.PropertyPrivate:
				case KnownImageIds.EventPrivate:
					return filterTypes.HasAnyFlag(MemberFilterTypes.Private | MemberFilterTypes.FieldAndProperty);
			}
			return true;
		}

		sealed class MemberFilterButtonGroup : UserControl
		{
			static readonly Thickness _Margin = new Thickness(3, 0, 3, 0);
			readonly ThemedToggleButton _FieldFilter, _MethodFilter, _TypeFilter, _PublicFilter, _InternalFilter, _ProtectFilter, _PrivateFilter;
			bool _uiLock;

			public event EventHandler FilterChanged;

			public MemberFilterButtonGroup() {
				_FieldFilter = CreateButton(KnownImageIds.Field, "Fields and properties");
				_MethodFilter = CreateButton(KnownImageIds.Method, "Methods, delegates and events");
				_TypeFilter = CreateButton(KnownImageIds.Type, "Nested types");

				_PublicFilter = CreateButton(KnownImageIds.TypePublic, "Public members");
				_InternalFilter = CreateButton(KnownImageIds.TypeInternal, "Internal members");
				_ProtectFilter = CreateButton(KnownImageIds.TypeProtected, "Protected members");
				_PrivateFilter = CreateButton(KnownImageIds.TypePrivate, "Private members");

				Margin = _Margin;
				Content = new Border {
					BorderThickness = WpfHelper.TinyMargin,
					BorderBrush = ThemeHelper.TextBoxBorderBrush,
					CornerRadius = new CornerRadius(3),
					Child = new StackPanel {
						Children = {
							new ThemedButton(KnownImageIds.StopFilter, "Clear filter", ClearFilter) { Margin = WpfHelper.NoMargin, BorderThickness = WpfHelper.NoMargin },
							new Border{ Width = 1, BorderThickness = WpfHelper.TinyMargin, BorderBrush = ThemeHelper.TextBoxBorderBrush },
							_FieldFilter, _MethodFilter, _TypeFilter,
							new Border{ Width = 1, BorderThickness = WpfHelper.TinyMargin, BorderBrush = ThemeHelper.TextBoxBorderBrush },
							_PublicFilter, _InternalFilter, _ProtectFilter, _PrivateFilter
						},
						Orientation = Orientation.Horizontal
					}
				};
			}

			public MemberFilterTypes Filters { get; private set; } = MemberFilterTypes.All;

			void UpdateFilterValue(object sender, RoutedEventArgs eventArgs) {
				if (_uiLock) {
					return;
				}
				var f = MemberFilterTypes.None;
				if (_FieldFilter.IsChecked == true) {
					f |= MemberFilterTypes.FieldAndProperty;
				}
				if (_MethodFilter.IsChecked == true) {
					f |= MemberFilterTypes.Method;
				}
				if (_TypeFilter.IsChecked == true) {
					f |= MemberFilterTypes.NestedType;
				}
				if (_PublicFilter.IsChecked == true) {
					f |= MemberFilterTypes.Public;
				}
				if (_ProtectFilter.IsChecked == true) {
					f |= MemberFilterTypes.Protected;
				}
				if (_PrivateFilter.IsChecked == true) {
					f |= MemberFilterTypes.Private;
				}
				if (_InternalFilter.IsChecked == true) {
					f |= MemberFilterTypes.Internal;
				}
				if (f == MemberFilterTypes.None) {
					f = MemberFilterTypes.All;
				}
				if (Filters != f) {
					Filters = f;
					FilterChanged?.Invoke(this, EventArgs.Empty);
				}
			}

			void ClearFilter() {
				_uiLock = true;
				_FieldFilter.IsChecked = _MethodFilter.IsChecked = _TypeFilter.IsChecked
					= _PublicFilter.IsChecked = _ProtectFilter.IsChecked = _PrivateFilter.IsChecked
					= _InternalFilter.IsChecked = false;
				_uiLock = false;
				if (Filters != MemberFilterTypes.All) {
					Filters = MemberFilterTypes.All;
					FilterChanged?.Invoke(this, EventArgs.Empty);
				}
			}

			ThemedToggleButton CreateButton(int imageId, string toolTip) {
				var b = new ThemedToggleButton(imageId, toolTip) { BorderThickness = WpfHelper.NoMargin };
				b.Checked += UpdateFilterValue;
				b.Unchecked += UpdateFilterValue;
				return b;
			}
		}
	}

	[Flags]
	enum MemberFilterTypes
	{
		None,
		FieldAndProperty = 1,
		Method = 1 << 1,
		NestedType = 1 << 2,
		AllMembers = FieldAndProperty | Method | NestedType,
		Public = 1 << 3,
		Protected = 1 << 4,
		Internal = 1 << 5,
		Private = 1 << 6,
		AllAccessibility = Public | Protected | Private | Internal,
		All = AllMembers | AllAccessibility
	}
}
