using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Imaging;
using AppHelpers;
using Microsoft.VisualStudio.PlatformUI;

namespace Codist.Controls
{
	interface IMemberFilterable
	{
		bool Filter(MemberFilterTypes filterTypes);
	}

	sealed class SearchScopeBox : UserControl
	{
		readonly ThemedToggleButton _ProjectFilter, _DocumentFilter;
		bool _uiLock;

		public event EventHandler FilterChanged;

		public SearchScopeBox() {
			_ProjectFilter = CreateButton(KnownImageIds.CSProjectNode, "Current Project");
			_DocumentFilter = CreateButton(KnownImageIds.CSSourceFile, "Current Document");
			Margin = WpfHelper.SmallHorizontalMargin;
			Content = new Border {
				BorderThickness = WpfHelper.TinyMargin,
				CornerRadius = new CornerRadius(3),
				Child = new StackPanel {
					Children = {
						_DocumentFilter, _ProjectFilter,
					},
					Orientation = Orientation.Horizontal
				}
			}.ReferenceProperty(BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey);
			_DocumentFilter.IsChecked = true;
		}

		public ScopeType Filter { get; private set; }

		public UIElementCollection Contents => ((StackPanel)((Border)Content).Child).Children;

		ThemedToggleButton CreateButton(int imageId, string toolTip) {
			var b = new ThemedToggleButton(imageId, toolTip).ClearMargin().ClearBorder();
			b.Checked += UpdateFilterValue;
			return b;
		}

		void UpdateFilterValue(object sender, RoutedEventArgs eventArgs) {
			if (_uiLock) {
				return;
			}
			_uiLock = true;
			_ProjectFilter.IsChecked = _DocumentFilter.IsChecked = false;
			(sender as ThemedToggleButton).IsChecked = true;
			_uiLock = false;
			var f = sender == _DocumentFilter ? ScopeType.ActiveDocument
				: sender == _ProjectFilter ? ScopeType.ActiveProject
				: ScopeType.Undefined;
			if (Filter != f) {
				Filter = f;
				FilterChanged?.Invoke(this, EventArgs.Empty);
			}
		}
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
			_FilterButtons.FilterChanged += FilterBox_Changed;
			_FilterButtons.FilterCleared += FilterBox_Clear;
			_FilterBox.TextChanged += FilterBox_Changed;
		}
		public ThemedTextBox FilterBox => _FilterBox;

		public bool FocusTextBox() {
			if (_FilterBox.IsVisible) {
				return _FilterBox.Focus();
			}
			_FilterBox.IsVisibleChanged -= FilterBox_IsVisibleChanged;
			_FilterBox.IsVisibleChanged += FilterBox_IsVisibleChanged;
			return false;
		}

		void FilterBox_Clear(object sender, EventArgs e) {
			if (_FilterBox.Text.Length > 0) {
				_FilterBox.Text = String.Empty;
			}
			else {
				FilterBox_Changed(sender, e);
			}
		}
		void FilterBox_Changed(object sender, EventArgs e) {
			Filter(_FilterBox.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), _FilterButtons.Filters);
			FocusTextBox();
		}

		void FilterBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
			if (_FilterBox.IsVisible) {
				_FilterBox.Focus();
				_FilterBox.SelectAll();
				_FilterBox.IsVisibleChanged -= FilterBox_IsVisibleChanged;
			}
		}

		void Filter(string[] keywords, MemberFilterTypes filters) {
			bool useModifierFilter = filters != MemberFilterTypes.All;
			if (keywords.Length == 0) {
				foreach (UIElement item in _Items) {
					item.Visibility = item is ThemedMenuItem.MenuItemPlaceHolder == false
						&& (useModifierFilter == false || item is IMemberFilterable menuItem && menuItem.Filter(filters))
						? Visibility.Visible
						: Visibility.Collapsed;
				}
				return;
			}
			IMemberFilterable filterable;
			foreach (UIElement item in _Items) {
				var menuItem = item as MenuItem;
				if (useModifierFilter) {
					filterable = item as IMemberFilterable;
					if (filterable != null) {
						if (filterable.Filter(filters) == false && (menuItem == null || menuItem.HasItems == false)) {
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
							filterable = sub as IMemberFilterable;
							if (filterable != null) {
								if (filterable.Filter(filters) == false) {
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
		internal static bool FilterByImageId(MemberFilterTypes filterTypes, int imageId) {
			switch (imageId) {
				case KnownImageIds.ClassPublic:
				case KnownImageIds.InterfacePublic:
				case KnownImageIds.StructurePublic:
				case KnownImageIds.EnumerationPublic:
				case KnownImageIds.DelegatePublic:
				case KnownImageIds.Namespace:
					return filterTypes.MatchFlags(MemberFilterTypes.Public | MemberFilterTypes.TypeAndNamespace);
				case KnownImageIds.ClassPrivate:
				case KnownImageIds.InterfacePrivate:
				case KnownImageIds.StructurePrivate:
				case KnownImageIds.EnumerationPrivate:
				case KnownImageIds.DelegatePrivate:
					return filterTypes.MatchFlags(MemberFilterTypes.Private | MemberFilterTypes.TypeAndNamespace);
				case KnownImageIds.ClassProtected:
				case KnownImageIds.InterfaceProtected:
				case KnownImageIds.StructureProtected:
				case KnownImageIds.EnumerationProtected:
				case KnownImageIds.DelegateProtected:
					return filterTypes.MatchFlags(MemberFilterTypes.Protected | MemberFilterTypes.TypeAndNamespace);
				case KnownImageIds.ClassInternal:
				case KnownImageIds.InterfaceInternal:
				case KnownImageIds.StructureInternal:
				case KnownImageIds.EnumerationInternal:
				case KnownImageIds.DelegateInternal:
					return filterTypes.MatchFlags(MemberFilterTypes.Internal | MemberFilterTypes.TypeAndNamespace);
				case KnownImageIds.ClassShortcut:
				case KnownImageIds.InterfaceShortcut:
				case KnownImageIds.StructureShortcut:
					return filterTypes.MatchFlags(MemberFilterTypes.TypeAndNamespace);
				case KnownImageIds.MethodPublic:
				case KnownImageIds.TypePublic: // constructor
				case KnownImageIds.OperatorPublic:
				case KnownImageIds.ConvertPartition: // conversion
					return filterTypes.MatchFlags(MemberFilterTypes.Public | MemberFilterTypes.Method);
				case KnownImageIds.MethodProtected:
				case KnownImageIds.TypeProtected: // constructor
				case KnownImageIds.OperatorProtected:
					return filterTypes.MatchFlags(MemberFilterTypes.Protected | MemberFilterTypes.Method);
				case KnownImageIds.MethodInternal:
				case KnownImageIds.TypeInternal: // constructor
				case KnownImageIds.OperatorInternal:
					return filterTypes.MatchFlags(MemberFilterTypes.Internal | MemberFilterTypes.Method);
				case KnownImageIds.MethodPrivate:
				case KnownImageIds.TypePrivate: // constructor
				case KnownImageIds.OperatorPrivate:
					return filterTypes.MatchFlags(MemberFilterTypes.Private | MemberFilterTypes.Method);
				case KnownImageIds.DeleteListItem: // deconstructor
				case KnownImageIds.ExtensionMethod:
					return filterTypes.MatchFlags(MemberFilterTypes.Method);
				case KnownImageIds.FieldPublic:
				case KnownImageIds.ConstantPublic:
				case KnownImageIds.PropertyPublic:
				case KnownImageIds.EventPublic:
					return filterTypes.MatchFlags(MemberFilterTypes.Public | MemberFilterTypes.FieldAndProperty);
				case KnownImageIds.FieldProtected:
				case KnownImageIds.ConstantProtected:
				case KnownImageIds.PropertyProtected:
				case KnownImageIds.EventProtected:
					return filterTypes.MatchFlags(MemberFilterTypes.Protected | MemberFilterTypes.FieldAndProperty);
				case KnownImageIds.FieldInternal:
				case KnownImageIds.ConstantInternal:
				case KnownImageIds.PropertyInternal:
				case KnownImageIds.EventInternal:
					return filterTypes.MatchFlags(MemberFilterTypes.Internal | MemberFilterTypes.FieldAndProperty);
				case KnownImageIds.FieldPrivate:
				case KnownImageIds.ConstantPrivate:
				case KnownImageIds.PropertyPrivate:
				case KnownImageIds.EventPrivate:
					return filterTypes.MatchFlags(MemberFilterTypes.Private | MemberFilterTypes.FieldAndProperty);
				case KnownImageIds.Numeric: // #region
					return filterTypes == MemberFilterTypes.All;
			}
			return true;
		}

		sealed class MemberFilterButtonGroup : UserControl
		{
			readonly ThemedToggleButton _FieldFilter, _MethodFilter, _TypeFilter, _PublicFilter, _PrivateFilter;
			bool _uiLock;

			public event EventHandler FilterChanged;
			public event EventHandler FilterCleared;

			public MemberFilterButtonGroup() {
				_FieldFilter = CreateButton(KnownImageIds.Field, "Fields and properties");
				_MethodFilter = CreateButton(KnownImageIds.Method, "Methods, delegates and events");
				_TypeFilter = CreateButton(KnownImageIds.EntityContainer, "Types");

				_PublicFilter = CreateButton(KnownImageIds.ModulePublic, "Public and protected members");
				_PrivateFilter = CreateButton(KnownImageIds.ModulePrivate, "Internal and private members");

				Margin = WpfHelper.SmallHorizontalMargin;
				Content = new Border {
					BorderThickness = WpfHelper.TinyMargin,
					CornerRadius = new CornerRadius(3),
					Child = new StackPanel {
						Children = {
							_PublicFilter, _PrivateFilter,
							new Border{ Width = 1, BorderThickness = WpfHelper.TinyMargin }.ReferenceProperty(BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey),
							_FieldFilter, _MethodFilter, _TypeFilter,
							new Border{ Width = 1, BorderThickness = WpfHelper.TinyMargin }.ReferenceProperty(BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey),
							new ThemedButton(KnownImageIds.StopFilter, "Clear filter", ClearFilter).ClearMargin().ClearBorder(),
						},
						Orientation = Orientation.Horizontal
					}
				}.ReferenceProperty(BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey);
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
					f |= MemberFilterTypes.TypeAndNamespace;
				}
				if (f.HasAnyFlag(MemberFilterTypes.AllMembers) == false) {
					f |= MemberFilterTypes.AllMembers;
				}
				if (_PublicFilter.IsChecked == true) {
					f |= MemberFilterTypes.Public | MemberFilterTypes.Protected;
				}
				if (_PrivateFilter.IsChecked == true) {
					f |= MemberFilterTypes.Internal | MemberFilterTypes.Private;
				}
				if (f.HasAnyFlag(MemberFilterTypes.AllAccessibility) == false) {
					f |= MemberFilterTypes.AllAccessibility;
				}
				if (Filters != f) {
					Filters = f;
					FilterChanged?.Invoke(this, EventArgs.Empty);
				}
			}

			void ClearFilter() {
				_uiLock = true;
				_FieldFilter.IsChecked = _MethodFilter.IsChecked = _TypeFilter.IsChecked
					= _PublicFilter.IsChecked = _PrivateFilter.IsChecked = false;
				_uiLock = false;
				if (Filters != MemberFilterTypes.All) {
					Filters = MemberFilterTypes.All;
				}
				FilterCleared?.Invoke(this, EventArgs.Empty);
			}

			ThemedToggleButton CreateButton(int imageId, string toolTip) {
				var b = new ThemedToggleButton(imageId, toolTip).ClearMargin().ClearBorder();
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
		TypeAndNamespace = 1 << 2,
		AllMembers = FieldAndProperty | Method | TypeAndNamespace,
		Public = 1 << 3,
		Protected = 1 << 4,
		Internal = 1 << 5,
		Private = 1 << 6,
		AllAccessibility = Public | Protected | Private | Internal,
		All = AllMembers | AllAccessibility
	}

	enum ScopeType
	{
		Undefined,
		ActiveDocument,
		ActiveProject,
		Solution,
		OpenedDocument
	}
}
