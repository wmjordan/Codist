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
using Microsoft.CodeAnalysis;

namespace Codist.Controls
{
	interface ISymbolFilterable
	{
		SymbolFilterKind SymbolFilterKind { get; }
		void Filter(string[] keywords, int filterFlags);
	}
	interface ISymbolFilter
	{
		bool Filter(int filterFlags);
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
	sealed class SymbolFilterBox : StackPanel
	{
		readonly ThemedTextBox _FilterBox;
		readonly FilterButtonGroup _FilterButtons;
		readonly ISymbolFilterable _Filter;

		public SymbolFilterBox(ISymbolFilterable filter) {
			Orientation = Orientation.Horizontal;
			Margin = WpfHelper.MenuItemMargin;
			Children.Add(ThemeHelper.GetImage(KnownImageIds.Filter).WrapMargin(WpfHelper.GlyphMargin));
			Children.Add(_FilterBox = new ThemedTextBox {
				MinWidth = 150,
				ToolTip = new ThemedToolTip("Result Filter", "Filter items in this menu.\nUse space to separate keywords.")
			});
			if (filter.SymbolFilterKind == SymbolFilterKind.Type) {
				Children.Add(_FilterButtons = new TypeFilterButtonGroup());
			}
			else {
				Children.Add(_FilterButtons = new MemberFilterButtonGroup());
			}
			_Filter = filter;
			_FilterButtons.FilterChanged += FilterBox_Changed;
			_FilterButtons.FilterCleared += FilterBox_Clear;
			_FilterBox.TextChanged += FilterBox_Changed;
			_FilterBox.SetOnVisibleSelectAll();
		}
		public ThemedTextBox FilterBox => _FilterBox;

		public bool FocusTextBox() {
			return _FilterBox.GetFocus();
		}
		public void UpdateNumbers(IEnumerable<ISymbol> symbols) {
			if (symbols != null) {
				_FilterButtons.UpdateNumbers(symbols);
			}
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
			_Filter.Filter(_FilterBox.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), _FilterButtons.Filters);
			FocusTextBox();
		}

		internal static bool FilterByImageId(MemberFilterTypes filterTypes, int imageId) {
			switch (imageId) {
				case KnownImageIds.ClassPublic:
				case KnownImageIds.InterfacePublic:
				case KnownImageIds.StructurePublic:
				case KnownImageIds.EnumerationPublic:
				case KnownImageIds.EnumerationItemPublic:
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
					return filterTypes.MatchFlags(MemberFilterTypes.Public | MemberFilterTypes.FieldAndProperty);
				case KnownImageIds.FieldProtected:
				case KnownImageIds.ConstantProtected:
				case KnownImageIds.PropertyProtected:
					return filterTypes.MatchFlags(MemberFilterTypes.Protected | MemberFilterTypes.FieldAndProperty);
				case KnownImageIds.FieldInternal:
				case KnownImageIds.ConstantInternal:
				case KnownImageIds.PropertyInternal:
					return filterTypes.MatchFlags(MemberFilterTypes.Internal | MemberFilterTypes.FieldAndProperty);
				case KnownImageIds.FieldPrivate:
				case KnownImageIds.ConstantPrivate:
				case KnownImageIds.PropertyPrivate:
					return filterTypes.MatchFlags(MemberFilterTypes.Private | MemberFilterTypes.FieldAndProperty);
				case KnownImageIds.EventPublic:
					return filterTypes.MatchFlags(MemberFilterTypes.Public | MemberFilterTypes.Event);
				case KnownImageIds.EventProtected:
					return filterTypes.MatchFlags(MemberFilterTypes.Protected | MemberFilterTypes.Event);
				case KnownImageIds.EventInternal:
					return filterTypes.MatchFlags(MemberFilterTypes.Internal | MemberFilterTypes.Event);
				case KnownImageIds.EventPrivate:
					return filterTypes.MatchFlags(MemberFilterTypes.Private | MemberFilterTypes.Event);
				case KnownImageIds.Numeric: // #region
					return filterTypes == MemberFilterTypes.All;
			}
			return false;
		}

		internal static bool FilterBySymbol(MemberFilterTypes filterTypes, ISymbol symbol) {
			MemberFilterTypes symbolFlags;
			if (symbol.Kind == SymbolKind.Alias) {
				symbol = ((IAliasSymbol)symbol).Target;
			}
			switch (symbol.DeclaredAccessibility) {
				case Accessibility.Private: symbolFlags = MemberFilterTypes.Private; break;
				case Accessibility.Protected: symbolFlags = MemberFilterTypes.Protected; break;
				case Accessibility.Internal: symbolFlags = MemberFilterTypes.Internal; break;
				case Accessibility.ProtectedAndInternal:
				case Accessibility.ProtectedOrInternal: symbolFlags = MemberFilterTypes.Internal | MemberFilterTypes.Protected; break;
				case Accessibility.Public: symbolFlags = MemberFilterTypes.Public; break;
				default: symbolFlags = MemberFilterTypes.None; break;
			}
			switch (symbol.Kind) {
				case SymbolKind.Event: symbolFlags |= MemberFilterTypes.Event; break;
				case SymbolKind.Field: symbolFlags |= MemberFilterTypes.Field; break;
				case SymbolKind.Method: symbolFlags |= MemberFilterTypes.Method; break;
				case SymbolKind.NamedType:
				case SymbolKind.Namespace: symbolFlags |= MemberFilterTypes.TypeAndNamespace; break;
				case SymbolKind.Property: symbolFlags |= MemberFilterTypes.Property; break;
			}
			return filterTypes.MatchFlags(symbolFlags);
		}

		internal static bool FilterBySymbol(TypeFilterTypes filterTypes, ISymbol symbol) {
			TypeFilterTypes symbolFlags;
			if (symbol.Kind == SymbolKind.Alias) {
				symbol = ((IAliasSymbol)symbol).Target;
			}

			switch (symbol.DeclaredAccessibility) {
				case Accessibility.Private: symbolFlags = TypeFilterTypes.Private; break;
				case Accessibility.Protected: symbolFlags = TypeFilterTypes.Protected; break;
				case Accessibility.Internal: symbolFlags = TypeFilterTypes.Internal; break;
				case Accessibility.ProtectedAndInternal:
				case Accessibility.ProtectedOrInternal: symbolFlags = TypeFilterTypes.Internal | TypeFilterTypes.Protected; break;
				case Accessibility.Public: symbolFlags = TypeFilterTypes.Public; break;
				default: symbolFlags = TypeFilterTypes.None; break;
			}
			if (symbol.Kind == SymbolKind.NamedType) {
				switch (((INamedTypeSymbol)symbol).TypeKind) {
					case TypeKind.Class: symbolFlags |= TypeFilterTypes.Class; break;
					case TypeKind.Struct: symbolFlags |= TypeFilterTypes.Struct; break;
					case TypeKind.Interface: symbolFlags |= TypeFilterTypes.Interface; break;
					case TypeKind.Delegate: symbolFlags |= TypeFilterTypes.Delegate; break;
					case TypeKind.Enum: symbolFlags |= TypeFilterTypes.Enum; break;
				}
			}
			else if (symbol.Kind == SymbolKind.Namespace) {
				symbolFlags |= TypeFilterTypes.Namespace;
			}
			return filterTypes.MatchFlags(symbolFlags);
		}

		static void ToggleFilterButton(ThemedToggleButton button, int label) {
			if (label > 0) {
				button.Visibility = Visibility.Visible;
				(button.Text ?? (button.Text = new ThemedMenuText())).Text = label.ToString();
			}
			else {
				button.Visibility = Visibility.Collapsed;
			}
		}

		abstract class FilterButtonGroup : UserControl
		{
			public event EventHandler FilterChanged;
			public event EventHandler FilterCleared;

			public abstract int Filters { get; }
			protected abstract void UpdateFilterValue();
			public abstract void ClearFilter();
			public abstract void UpdateNumbers(IEnumerable<ISymbol> symbols);

			protected void OnFilterChanged() {
				FilterChanged?.Invoke(this, EventArgs.Empty);
			}

			protected void OnFilterCleared() {
				FilterCleared?.Invoke(this, EventArgs.Empty);
			}

			protected ThemedToggleButton CreateButton(int imageId, string toolTip) {
				var b = new ThemedToggleButton(imageId, toolTip).ClearMargin().ClearBorder();
				b.Checked += UpdateFilterValue;
				b.Unchecked += UpdateFilterValue;
				return b;
			}

			void UpdateFilterValue(object sender, RoutedEventArgs eventArgs) {
				UpdateFilterValue();
			}
		}

		sealed class TypeFilterButtonGroup : FilterButtonGroup
		{
			readonly ThemedToggleButton _ClassFilter, _StructFilter, _EnumFilter, _InterfaceFilter, _DelegateFilter, _NamespaceFilter, _PublicFilter, _PrivateFilter;
			TypeFilterTypes _Filters;
			bool _uiLock;

			public override int Filters => (int)_Filters;

			public TypeFilterButtonGroup() {
				_ClassFilter = CreateButton(KnownImageIds.Class, "Classes");
				_InterfaceFilter = CreateButton(KnownImageIds.Interface, "Interfaces");
				_DelegateFilter = CreateButton(KnownImageIds.Delegate, "Delegates");
				_StructFilter = CreateButton(KnownImageIds.Structure, "Structures");
				_EnumFilter = CreateButton(KnownImageIds.Enumeration, "Enumerations");
				_NamespaceFilter = CreateButton(KnownImageIds.Namespace, "Namespaces");

				_PublicFilter = CreateButton(KnownImageIds.ModulePublic, "Public and protected types");
				_PrivateFilter = CreateButton(KnownImageIds.ModulePrivate, "Internal and private types");

				_Filters = TypeFilterTypes.All;
				Margin = WpfHelper.SmallHorizontalMargin;
				Content = new Border {
					BorderThickness = WpfHelper.TinyMargin,
					CornerRadius = new CornerRadius(3),
					Child = new StackPanel {
						Children = {
							_PublicFilter, _PrivateFilter,
							new Border{ Width = 1, BorderThickness = WpfHelper.TinyMargin }.ReferenceProperty(BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey),
							_ClassFilter, _InterfaceFilter, _DelegateFilter, _StructFilter, _EnumFilter, _NamespaceFilter,
							new Border{ Width = 1, BorderThickness = WpfHelper.TinyMargin }.ReferenceProperty(BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey),
							new ThemedButton(KnownImageIds.StopFilter, "Clear filter", ClearFilter).ClearBorder(),
						},
						Orientation = Orientation.Horizontal
					}
				}.ReferenceProperty(BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey);
			}

			protected override void UpdateFilterValue() {
				if (_uiLock) {
					return;
				}
				var f = TypeFilterTypes.None;
				if (_ClassFilter.IsChecked == true) {
					f |= TypeFilterTypes.Class;
				}
				if (_StructFilter.IsChecked == true) {
					f |= TypeFilterTypes.Struct;
				}
				if (_EnumFilter.IsChecked == true) {
					f |= TypeFilterTypes.Enum;
				}
				if (_DelegateFilter.IsChecked == true) {
					f |= TypeFilterTypes.Delegate;
				}
				if (_InterfaceFilter.IsChecked == true) {
					f |= TypeFilterTypes.Interface;
				}
				if (_NamespaceFilter.IsChecked == true) {
					f |= TypeFilterTypes.Namespace;
				}
				if (f.HasAnyFlag(TypeFilterTypes.AllTypes) == false) {
					f |= TypeFilterTypes.AllTypes;
				}
				if (_PublicFilter.IsChecked == true) {
					f |= TypeFilterTypes.Public | TypeFilterTypes.Protected;
				}
				if (_PrivateFilter.IsChecked == true) {
					f |= TypeFilterTypes.Internal | TypeFilterTypes.Private;
				}
				if (f.HasAnyFlag(TypeFilterTypes.AllAccessibility) == false) {
					f |= TypeFilterTypes.AllAccessibility;
				}
				if (_Filters != f) {
					_Filters = f;
					OnFilterChanged();
				}
			}

			public override void UpdateNumbers(IEnumerable<ISymbol> symbols) {
				int c = 0, s = 0, e = 0, d = 0, i = 0, n = 0, pu = 0, pi = 0;
				foreach (var item in symbols) {
					if (item == null || item.IsImplicitlyDeclared) {
						continue;
					}
					switch (item.Kind) {
						case SymbolKind.NamedType:
							switch (((INamedTypeSymbol)item).TypeKind) {
								case TypeKind.Class: ++c; break;
								case TypeKind.Struct: ++s; break;
								case TypeKind.Interface: ++i; break;
								case TypeKind.Delegate: ++d; break;
								case TypeKind.Enum: ++e; break;
							}
							break;
						case SymbolKind.Namespace:
							++n; break;
					}
					switch (item.DeclaredAccessibility) {
						case Accessibility.Private:
						case Accessibility.Internal:
						case Accessibility.ProtectedAndInternal:
						case Accessibility.ProtectedOrInternal:
							++pi; break;
						case Accessibility.Public:
						case Accessibility.Protected:
							++pu; break;
					}
				}
				ToggleFilterButton(_PublicFilter, pu);
				ToggleFilterButton(_PrivateFilter, pi);
				ToggleFilterButton(_ClassFilter, c);
				ToggleFilterButton(_StructFilter, s);
				ToggleFilterButton(_EnumFilter, e);
				ToggleFilterButton(_InterfaceFilter, i);
				ToggleFilterButton(_DelegateFilter, d);
				ToggleFilterButton(_NamespaceFilter, n);
			}

			public override void ClearFilter() {
				_uiLock = true;
				_ClassFilter.IsChecked = _InterfaceFilter.IsChecked = _DelegateFilter.IsChecked
					= _StructFilter.IsChecked = _EnumFilter.IsChecked = _NamespaceFilter.IsChecked
					= _PublicFilter.IsChecked = _PrivateFilter.IsChecked = false;
				_uiLock = false;
				if (_Filters != TypeFilterTypes.All) {
					_Filters = TypeFilterTypes.All;
				}
				OnFilterCleared();
			}
		}

		sealed class MemberFilterButtonGroup : FilterButtonGroup
		{
			readonly ThemedToggleButton _FieldFilter, _PropertyFilter, _EventFilter, _MethodFilter, _TypeFilter, _PublicFilter, _PrivateFilter;
			MemberFilterTypes _Filters;
			bool _uiLock;

			public override int Filters => (int)_Filters;

			public MemberFilterButtonGroup() {
				_FieldFilter = CreateButton(KnownImageIds.Field, "Fields, properties");
				_MethodFilter = CreateButton(KnownImageIds.Method, "Methods");
				_EventFilter = CreateButton(KnownImageIds.Event, "Events");
				_TypeFilter = CreateButton(KnownImageIds.EntityContainer, "Types and delegates");

				_PublicFilter = CreateButton(KnownImageIds.ModulePublic, "Public and protected members");
				_PrivateFilter = CreateButton(KnownImageIds.ModulePrivate, "Internal and private members");

				_Filters = MemberFilterTypes.All;
				Margin = WpfHelper.SmallHorizontalMargin;
				Content = new Border {
					BorderThickness = WpfHelper.TinyMargin,
					CornerRadius = new CornerRadius(3),
					Child = new StackPanel {
						Children = {
							_PublicFilter, _PrivateFilter,
							new Border{ Width = 1, BorderThickness = WpfHelper.TinyMargin }.ReferenceProperty(BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey),
							_FieldFilter, _MethodFilter, _EventFilter, _TypeFilter,
							new Border{ Width = 1, BorderThickness = WpfHelper.TinyMargin }.ReferenceProperty(BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey),
							new ThemedButton(KnownImageIds.StopFilter, "Clear filter", ClearFilter).ClearBorder(),
						},
						Orientation = Orientation.Horizontal
					}
				}.ReferenceProperty(BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey);
			}

			protected override void UpdateFilterValue() {
				if (_uiLock) {
					return;
				}
				var f = MemberFilterTypes.None;
				if (_FieldFilter.IsChecked == true) {
					f |= _PropertyFilter == null ? MemberFilterTypes.FieldAndProperty : MemberFilterTypes.Field;
				}
				if (_PropertyFilter?.IsChecked == true) {
					f |= MemberFilterTypes.Property;
				}
				if (_EventFilter.IsChecked == true) {
					f |= MemberFilterTypes.Event;
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
				if (_Filters != f) {
					_Filters = f;
					OnFilterChanged();
				}
			}

			public override void UpdateNumbers(IEnumerable<ISymbol> symbols) {
				int f = 0, m = 0, e = 0, t = 0, pu = 0, pi = 0;
				foreach (var item in symbols) {
					if (item == null || item.IsImplicitlyDeclared) {
						continue;
					}
					switch (item.Kind) {
						case SymbolKind.Event:
							++e; break;
						case SymbolKind.Field:
						case SymbolKind.Property:
							++f; break;
						case SymbolKind.Method:
							var sm = item as IMethodSymbol;
							if (sm.MethodKind == MethodKind.PropertyGet
								|| sm.MethodKind == MethodKind.PropertySet) {
								continue;
							}
							++m; break;
						case SymbolKind.NamedType:
							++t; break;
					}
					switch (item.DeclaredAccessibility) {
						case Accessibility.Private:
						case Accessibility.Internal:
						case Accessibility.ProtectedAndInternal:
						case Accessibility.ProtectedOrInternal:
							++pi; break;
						case Accessibility.Public:
						case Accessibility.Protected:
							++pu; break;
					}
				}
				ToggleFilterButton(_PublicFilter, pu);
				ToggleFilterButton(_PrivateFilter, pi);
				ToggleFilterButton(_FieldFilter, f);
				ToggleFilterButton(_MethodFilter, m);
				ToggleFilterButton(_EventFilter, e);
				ToggleFilterButton(_TypeFilter, t);
			}

			public override void ClearFilter() {
				_uiLock = true;
				_FieldFilter.IsChecked = _MethodFilter.IsChecked = _EventFilter.IsChecked
					= _TypeFilter.IsChecked
					= _PublicFilter.IsChecked = _PrivateFilter.IsChecked = false;
				if (_PropertyFilter != null) {
					_PropertyFilter.IsChecked = false;
				}
				_uiLock = false;
				if (_Filters != MemberFilterTypes.All) {
					_Filters = MemberFilterTypes.All;
				}
				OnFilterCleared();
			}
		}
	}

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

	enum SymbolFilterKind
	{
		Undefined,
		Member,
		Type
	}

	[Flags]
	enum MemberFilterTypes
	{
		None,
		Field = 1,
		Property = 1 << 1,
		FieldAndProperty = Field | Property,
		Event = 1 << 2,
		Method = 1 << 3,
		TypeAndNamespace = 1 << 4,
		AllMembers = FieldAndProperty | Event | Method | TypeAndNamespace,
		Public = 1 << 5,
		Protected = 1 << 6,
		Internal = 1 << 7,
		Private = 1 << 8,
		AllAccessibility = Public | Protected | Private | Internal,
		All = AllMembers | AllAccessibility
	}

	[Flags]
	enum TypeFilterTypes
	{
		None,
		Class = 1,
		Struct = 1 << 1,
		Enum = 1 << 2,
		StructAndEnum = Struct | Enum,
		Delegate = 1 << 3,
		Interface = 1 << 5,
		Namespace = 1 << 6,
		AllTypes = Class | StructAndEnum | Delegate| Interface | Namespace,
		Public = 1 << 7,
		Protected = 1 << 8,
		Internal = 1 << 9,
		Private = 1 << 10,
		AllAccessibility = Public | Protected | Private | Internal,
		All = AllTypes | AllAccessibility
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
