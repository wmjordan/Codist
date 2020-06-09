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
using R = Codist.Properties.Resources;

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
			_DocumentFilter = CreateButton(IconIds.File, R.T_SearchCurrentDocument);
			_ProjectFilter = CreateButton(IconIds.Project, R.T_SearchCurrentProject);
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
		readonly ISymbolFilterable _Filter;
		readonly FilterButtonGroup[] _FilterGroups;
		readonly StackPanel _FilterContainer;

		public SymbolFilterBox(ISymbolFilterable filter) {
			Orientation = Orientation.Horizontal;
			Margin = WpfHelper.MenuItemMargin;
			Children.Add(ThemeHelper.GetImage(IconIds.Filter).WrapMargin(WpfHelper.GlyphMargin));
			Children.Add(_FilterBox = new ThemedTextBox {
				MinWidth = 150,
				Margin = WpfHelper.GlyphMargin,
				ToolTip = new ThemedToolTip(R.T_ResultFilter, R.T_ResultFilterTip)
			});
			Children.Add(new Border {
				BorderThickness = WpfHelper.TinyMargin,
				CornerRadius = new CornerRadius(3),
				Child = _FilterContainer = new StackPanel {Orientation = Orientation.Horizontal }
			}.ReferenceProperty(Border.BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey));
			switch (filter.SymbolFilterKind) {
				case SymbolFilterKind.Type:
					_FilterGroups = new FilterButtonGroup[] { new AccessibilityFilterButtonGroup(), new TypeFilterButtonGroup() };
					break;
				case SymbolFilterKind.Usage:
					_FilterGroups = new FilterButtonGroup[] { new AccessibilityFilterButtonGroup(), new SymbolUsageFilterButtonGroup(), new MemberFilterButtonGroup() };
					break;
				default:
					_FilterGroups = new FilterButtonGroup[] { new AccessibilityFilterButtonGroup(), new MemberFilterButtonGroup() };
					break;
			}
			_FilterContainer.Add(_FilterGroups);
			_FilterContainer.Add(new ThemedButton(IconIds.ClearFilter, R.CMD_ClearFilter, ClearFilters).ClearBorder());
			_Filter = filter;
			foreach (var item in _FilterGroups) {
				item.FilterChanged += FilterBox_Changed;
			}
			_FilterBox.TextChanged += FilterBox_Changed;
			_FilterBox.SetOnVisibleSelectAll();
		}
		//public ThemedTextBox FilterBox => _FilterBox;

		public bool FocusFilterBox() {
			return _FilterBox.GetFocus();
		}
		public void UpdateNumbers(IEnumerable<SymbolItem> symbols) {
			if (symbols != null) {
				foreach (var item in _FilterGroups) {
					item.UpdateNumbers(symbols);
				}
			}
		}
		void ClearFilters() {
			bool needUpdate = false;
			var filters = GetFilterFlags();
			if (filters != 0) {
				foreach (var item in _FilterGroups) {
					item.ClearFilter();
				}
				needUpdate = true;
			}
			if (_FilterBox.Text.Length > 0) {
				_FilterBox.Text = String.Empty;
				needUpdate = true;
			}
			if (needUpdate) {
				_Filter.Filter(_FilterBox.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), (int)MemberFilterTypes.All);
				FocusFilterBox();
			}
		}
		void FilterBox_Changed(object sender, EventArgs e) {
			var filters = GetFilterFlags();
			_Filter.Filter(_FilterBox.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), filters);
			FocusFilterBox();
		}

		int GetFilterFlags() {
			var filters = 0;
			foreach (var item in _FilterGroups) {
				filters |= item.Filters;
			}
			return filters;
		}

		internal static bool FilterByImageId(MemberFilterTypes filterTypes, int imageId) {
			switch (imageId) {
				case KnownImageIds.ClassPublic:
				case KnownImageIds.InterfacePublic:
				case KnownImageIds.StructurePublic:
				case KnownImageIds.EnumerationPublic:
				case IconIds.EnumField:
				case KnownImageIds.DelegatePublic:
				case IconIds.Namespace:
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
				case IconIds.PartialClass:
				case IconIds.PartialInterface:
				case IconIds.PartialStruct:
					return filterTypes.MatchFlags(MemberFilterTypes.TypeAndNamespace);
				case KnownImageIds.MethodPublic:
				case IconIds.PublicConstructor:
				case KnownImageIds.OperatorPublic:
				case IconIds.ConvertOperator:
					return filterTypes.MatchFlags(MemberFilterTypes.Public | MemberFilterTypes.Method);
				case KnownImageIds.MethodProtected:
				case IconIds.ProtectedConstructor:
				case KnownImageIds.OperatorProtected:
					return filterTypes.MatchFlags(MemberFilterTypes.Protected | MemberFilterTypes.Method);
				case KnownImageIds.MethodInternal:
				case IconIds.InternalConstructor:
				case KnownImageIds.OperatorInternal:
					return filterTypes.MatchFlags(MemberFilterTypes.Internal | MemberFilterTypes.Method);
				case KnownImageIds.MethodPrivate:
				case IconIds.PrivateConstructor:
				case KnownImageIds.OperatorPrivate:
					return filterTypes.MatchFlags(MemberFilterTypes.Private | MemberFilterTypes.Method);
				case IconIds.Deconstructor:
				case IconIds.ExtensionMethod:
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
				case IconIds.Region: // #region
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

		internal static bool FilterBySymbolType(MemberFilterTypes filterTypes, ISymbol symbol) {
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
			if (symbol.Kind == SymbolKind.NamedType) {
				switch (((INamedTypeSymbol)symbol).TypeKind) {
					case TypeKind.Class: symbolFlags |= MemberFilterTypes.Class; break;
					case TypeKind.Struct: symbolFlags |= MemberFilterTypes.Struct; break;
					case TypeKind.Interface: symbolFlags |= MemberFilterTypes.Interface; break;
					case TypeKind.Delegate: symbolFlags |= MemberFilterTypes.Delegate; break;
					case TypeKind.Enum: symbolFlags |= MemberFilterTypes.Enum; break;
				}
			}
			else if (symbol.Kind == SymbolKind.Namespace) {
				symbolFlags |= MemberFilterTypes.Namespace;
			}
			return filterTypes.MatchFlags(symbolFlags);
		}

		internal static bool FilterByUsage(MemberFilterTypes filterTypes, SymbolItem item) {
			var m = MemberFilterTypes.None;
			if (item.Usage.HasAnyFlag(SymbolUsageKind.Usage) == false) {
				return false;
			}
			if (item.Usage.MatchFlags(SymbolUsageKind.Write)) {
				m |= MemberFilterTypes.Write;
			}
			if (item.Usage.MatchFlags(SymbolUsageKind.TypeCast)) {
				m |= MemberFilterTypes.TypeCast;
			}
			if (item.Usage.HasAnyFlag(SymbolUsageKind.TypeParameter | SymbolUsageKind.Catch)) {
				m |= MemberFilterTypes.TypeReference;
			}
			if (item.Usage.HasAnyFlag(SymbolUsageKind.Delegate | SymbolUsageKind.Attach | SymbolUsageKind.Detach)) {
				m |= MemberFilterTypes.Read;
			}
			return filterTypes.HasAnyFlag(m);
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

			public abstract int Filters { get; }
			protected abstract void UpdateFilterValue();
			public abstract void ClearFilter();
			public abstract void UpdateNumbers(IEnumerable<SymbolItem> symbols);

			protected void OnFilterChanged() {
				FilterChanged?.Invoke(this, EventArgs.Empty);
			}

			protected ThemedToggleButton CreateButton(int imageId, string toolTip) {
				var b = new ThemedToggleButton(imageId, toolTip).ClearMargin().ClearBorder();
				b.Checked += UpdateFilterValueHandler;
				b.Unchecked += UpdateFilterValueHandler;
				return b;
			}
			protected static Border CreateSeparator() {
				return new Border { Width = 1, BorderThickness = WpfHelper.TinyMargin }.ReferenceProperty(BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey);
			}

			void UpdateFilterValueHandler(object sender, RoutedEventArgs eventArgs) {
				UpdateFilterValue();
			}
		}

		sealed class AccessibilityFilterButtonGroup : FilterButtonGroup
		{
			readonly ThemedToggleButton _PublicFilter, _PrivateFilter;
			readonly Border _Separator;
			MemberFilterTypes _Filters;
			bool _uiLock;

			public override int Filters => (int)_Filters;

			public AccessibilityFilterButtonGroup() {
				_PublicFilter = CreateButton(IconIds.PublicSymbols, R.T_PublicProtectedTypes);
				_PrivateFilter = CreateButton(IconIds.PrivateSymbols, R.T_InternalPrivateTypes);
				_Filters = MemberFilterTypes.AllAccessibilities;
				Content = new StackPanel {
					Children = {
						_PublicFilter, _PrivateFilter,
						(_Separator = CreateSeparator())
					},
					Orientation = Orientation.Horizontal
				}.ReferenceProperty(BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey);
			}

			protected override void UpdateFilterValue() {
				if (_uiLock) {
					return;
				}
				var f = MemberFilterTypes.None;
				if (_PublicFilter.IsChecked == true) {
					f |= MemberFilterTypes.Public | MemberFilterTypes.Protected;
				}
				if (_PrivateFilter.IsChecked == true) {
					f |= MemberFilterTypes.Internal | MemberFilterTypes.Private;
				}
				if (f.HasAnyFlag(MemberFilterTypes.AllAccessibilities) == false) {
					f |= MemberFilterTypes.AllAccessibilities;
				}
				if (_Filters != f) {
					_Filters = f;
					OnFilterChanged();
				}
			}

			public override void UpdateNumbers(IEnumerable<SymbolItem> symbols) {
				int pu = 0, pi = 0;
				foreach (var item in symbols) {
					var symbol = item.Symbol;
					if (symbol == null || symbol.IsImplicitlyDeclared) {
						continue;
					}
					switch (symbol.DeclaredAccessibility) {
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
				_Separator.Visibility = (pu != 0 || pi != 0) ? Visibility.Visible : Visibility.Collapsed;
			}

			public override void ClearFilter() {
				_uiLock = true;
				_PublicFilter.IsChecked = _PrivateFilter.IsChecked = false;
				_uiLock = false;
				_Filters |= MemberFilterTypes.AllAccessibilities;
			}
		}

		sealed class TypeFilterButtonGroup : FilterButtonGroup
		{
			readonly ThemedToggleButton _ClassFilter, _StructFilter, _EnumFilter, _InterfaceFilter, _DelegateFilter, _NamespaceFilter;
			readonly Border _Separator;
			MemberFilterTypes _Filters;
			bool _uiLock;

			public override int Filters => (int)_Filters;

			public TypeFilterButtonGroup() {
				_ClassFilter = CreateButton(IconIds.Class, R.T_Classes);
				_InterfaceFilter = CreateButton(IconIds.Interface, R.T_Interfaces);
				_DelegateFilter = CreateButton(IconIds.Delegate, R.T_Delegates);
				_StructFilter = CreateButton(IconIds.Structure, R.T_Structures);
				_EnumFilter = CreateButton(IconIds.Enum, R.T_Enumerations);
				_NamespaceFilter = CreateButton(IconIds.Namespace, R.T_Namespaces);

				_Filters = MemberFilterTypes.AllTypes;
				Content = new StackPanel {
						Children = {
							_ClassFilter, _InterfaceFilter, _DelegateFilter, _StructFilter, _EnumFilter, _NamespaceFilter,
							(_Separator = CreateSeparator()),
						},
						Orientation = Orientation.Horizontal
					};
			}

			protected override void UpdateFilterValue() {
				if (_uiLock) {
					return;
				}
				var f = MemberFilterTypes.None;
				if (_ClassFilter.IsChecked == true) {
					f |= MemberFilterTypes.Class;
				}
				if (_StructFilter.IsChecked == true) {
					f |= MemberFilterTypes.Struct;
				}
				if (_EnumFilter.IsChecked == true) {
					f |= MemberFilterTypes.Enum;
				}
				if (_DelegateFilter.IsChecked == true) {
					f |= MemberFilterTypes.Delegate;
				}
				if (_InterfaceFilter.IsChecked == true) {
					f |= MemberFilterTypes.Interface;
				}
				if (_NamespaceFilter.IsChecked == true) {
					f |= MemberFilterTypes.Namespace;
				}
				if (f.HasAnyFlag(MemberFilterTypes.AllTypes) == false) {
					f |= MemberFilterTypes.AllTypes;
				}
				if (_Filters != f) {
					_Filters = f;
					OnFilterChanged();
				}
			}

			public override void UpdateNumbers(IEnumerable<SymbolItem> symbols) {
				int c = 0, s = 0, e = 0, d = 0, i = 0, n = 0;
				foreach (var item in symbols) {
					var symbol = item.Symbol;
					if (symbol == null || symbol.IsImplicitlyDeclared) {
						continue;
					}
					switch (symbol.Kind) {
						case SymbolKind.NamedType:
							switch (((INamedTypeSymbol)symbol).TypeKind) {
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
				}
				ToggleFilterButton(_ClassFilter, c);
				ToggleFilterButton(_StructFilter, s);
				ToggleFilterButton(_EnumFilter, e);
				ToggleFilterButton(_InterfaceFilter, i);
				ToggleFilterButton(_DelegateFilter, d);
				ToggleFilterButton(_NamespaceFilter, n);
				_Separator.Visibility = (c != 0 || s != 0 || e != 0 || i != 0 || d != 0 || n != 0) ? Visibility.Visible : Visibility.Collapsed;
			}

			public override void ClearFilter() {
				_uiLock = true;
				_ClassFilter.IsChecked = _InterfaceFilter.IsChecked = _DelegateFilter.IsChecked
					= _StructFilter.IsChecked = _EnumFilter.IsChecked = _NamespaceFilter.IsChecked = false;
				_uiLock = false;
				_Filters |= MemberFilterTypes.AllTypes;
			}
		}

		sealed class MemberFilterButtonGroup : FilterButtonGroup
		{
			readonly ThemedToggleButton _FieldFilter, _PropertyFilter, _EventFilter, _MethodFilter, _TypeFilter;
			readonly Border _Separator;
			MemberFilterTypes _Filters;
			bool _uiLock;

			public override int Filters => (int)_Filters;

			public MemberFilterButtonGroup() {
				_FieldFilter = CreateButton(IconIds.Field, R.T_FieldsProperties);
				_MethodFilter = CreateButton(IconIds.Method, R.T_Methods);
				_EventFilter = CreateButton(IconIds.Event, R.T_Events);
				_TypeFilter = CreateButton(IconIds.TypeAndDelegate, R.T_TypesDelegates);

				_Filters = MemberFilterTypes.AllMembers;
				Content = new StackPanel {
					Children = {
						_FieldFilter, _MethodFilter, _EventFilter, _TypeFilter,
						(_Separator = CreateSeparator())
					},
					Orientation = Orientation.Horizontal
				};
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
				if (_Filters != f) {
					_Filters = f;
					OnFilterChanged();
				}
			}

			public override void UpdateNumbers(IEnumerable<SymbolItem> symbols) {
				int f = 0, m = 0, e = 0, t = 0;
				foreach (var item in symbols) {
					var symbol = item.Symbol;
					if (symbol == null || symbol.IsImplicitlyDeclared) {
						continue;
					}
					switch (symbol.Kind) {
						case SymbolKind.Event:
							++e; break;
						case SymbolKind.Field:
						case SymbolKind.Property:
							++f; break;
						case SymbolKind.Method:
							var sm = symbol as IMethodSymbol;
							if (sm.MethodKind == MethodKind.PropertyGet
								|| sm.MethodKind == MethodKind.PropertySet) {
								continue;
							}
							++m; break;
						case SymbolKind.NamedType:
							++t; break;
					}
				}
				ToggleFilterButton(_FieldFilter, f);
				ToggleFilterButton(_MethodFilter, m);
				ToggleFilterButton(_EventFilter, e);
				ToggleFilterButton(_TypeFilter, t);
				_Separator.Visibility = (f != 0 || m != 0 || e != 0 || t != 0) ? Visibility.Visible : Visibility.Collapsed;
			}

			public override void ClearFilter() {
				_uiLock = true;
				_FieldFilter.IsChecked = _MethodFilter.IsChecked = _EventFilter.IsChecked
					= _TypeFilter.IsChecked = false;
				if (_PropertyFilter != null) {
					_PropertyFilter.IsChecked = false;
				}
				_uiLock = false;
				_Filters |= MemberFilterTypes.AllMembers;
			}
		}

		sealed class SymbolUsageFilterButtonGroup : FilterButtonGroup
		{
			readonly ThemedToggleButton _WriteFilter, _ReadFilter, _EventFilter, _TypeCastFilter, _TypeReferenceFilter;
			readonly Border _Separator;
			MemberFilterTypes _Filters;
			bool _uiLock;

			public override int Filters => (int)_Filters;

			public SymbolUsageFilterButtonGroup() {
				_WriteFilter = CreateButton(IconIds.UseToWrite, R.T_Write);
				_ReadFilter = CreateButton(IconIds.UseAsDelegate, R.T_DelegateEvent);
				_TypeCastFilter = CreateButton(IconIds.UseToCast, R.T_TypeConversion);
				_TypeReferenceFilter = CreateButton(IconIds.UseAsTypeParameter, R.T_TypeReferenceOrArgument);
				_Filters = MemberFilterTypes.AllUsages;
				Content = new StackPanel {
					Children = {
						_WriteFilter, _ReadFilter, _TypeCastFilter, _TypeReferenceFilter,
						(_Separator = CreateSeparator())
					},
					Orientation = Orientation.Horizontal
				};
			}

			protected override void UpdateFilterValue() {
				if (_uiLock) {
					return;
				}
				var f = MemberFilterTypes.None;
				if (_WriteFilter.IsChecked == true) {
					f |= MemberFilterTypes.Write;
				}
				if (_ReadFilter.IsChecked == true) {
					f |= MemberFilterTypes.Read;
				}
				if (_TypeCastFilter.IsChecked == true) {
					f |= MemberFilterTypes.TypeCast;
				}
				if (_TypeReferenceFilter.IsChecked == true) {
					f |= MemberFilterTypes.TypeReference;
				}
				if (f.HasAnyFlag(MemberFilterTypes.AllUsages) == false) {
					f |= MemberFilterTypes.AllUsages;
				}
				if (_Filters != f) {
					_Filters = f;
					OnFilterChanged();
				}
			}

			public override void UpdateNumbers(IEnumerable<SymbolItem> symbols) {
				int r = 0, w = 0, tc = 0, tr = 0;
				foreach (var item in symbols) {
					var t = item.Usage;
					if (t == SymbolUsageKind.Normal) {
						continue;
					}
					if (t.MatchFlags(SymbolUsageKind.Write)) {
						++w;
					}
					if (t.HasAnyFlag(SymbolUsageKind.Delegate | SymbolUsageKind.Attach | SymbolUsageKind.Detach)) {
						++r;
					}
					if (t.MatchFlags(SymbolUsageKind.TypeCast)) {
						++tc;
					}
					if (t.HasAnyFlag(SymbolUsageKind.TypeParameter | SymbolUsageKind.Catch)) {
						++tr;
					}
				}
				ToggleFilterButton(_ReadFilter, r);
				ToggleFilterButton(_WriteFilter, w);
				ToggleFilterButton(_TypeCastFilter, tc);
				ToggleFilterButton(_TypeReferenceFilter, tr);
				_Separator.Visibility = (w != 0 || r != 0 || tc != 0 || tr != 0) ? Visibility.Visible : Visibility.Collapsed;
			}

			public override void ClearFilter() {
				_uiLock = true;
				_WriteFilter.IsChecked = _ReadFilter.IsChecked = _TypeCastFilter.IsChecked = _TypeReferenceFilter.IsChecked = false;
				_uiLock = false;
				_Filters |= MemberFilterTypes.AllUsages;
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
		Type,
		Usage
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
		AllAccessibilities = Public | Protected | Private | Internal,
		Class = 1 << 10,
		Struct = 1 << 11,
		Enum = 1 << 12,
		StructAndEnum = Struct | Enum,
		Delegate = 1 << 13,
		Interface = 1 << 14,
		Namespace = 1 << 15,
		AllTypes = Class | StructAndEnum | Delegate| Interface | Namespace,
		Read = 1 << 20,
		Write = 1 << 21,
		TypeCast = 1 << 22,
		TypeReference = 1 << 23,
		AllUsages = Read | Write | TypeCast | TypeReference,
		All = AllMembers | AllAccessibilities | AllTypes | AllUsages
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
