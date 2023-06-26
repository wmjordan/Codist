using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CLR;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.PlatformUI;
using R = Codist.Properties.Resources;

namespace Codist.Controls
{
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
				MinWidth = 120,
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
					_FilterGroups = new FilterButtonGroup[] { new AccessibilityFilterButtonGroup(), new InstanceFilterButtonGroup(), new SymbolUsageFilterButtonGroup(), new MemberFilterButtonGroup() };
					break;
				case SymbolFilterKind.Node:
					_FilterGroups = new FilterButtonGroup[] { new AccessibilityFilterButtonGroup(), new MemberFilterButtonGroup(Config.Instance.NaviBarOptions) };
					break;
				default:
					_FilterGroups = new FilterButtonGroup[] { new AccessibilityFilterButtonGroup(), new InstanceFilterButtonGroup(), new MemberFilterButtonGroup() };
					break;
			}
			_FilterContainer
				.Add(_FilterGroups)
				.Add(new ThemedButton(IconIds.ClearFilter, R.CMD_ClearFilter, ClearFilters) { MinHeight = 10 }
					.SetValue(ToolTipService.SetPlacement, PlacementMode.Left)
					.ClearSpacing());
			_Filter = filter;
			foreach (var item in _FilterGroups) {
				item.FilterChanged += FilterBox_Changed;
			}
			_FilterBox.TextChanged += FilterBox_Changed;
			_FilterBox.SetOnVisibleSelectAll();
		}

		public string FilterText => _FilterBox.Text;

		public event EventHandler<FilterEventArgs> FilterChanged;

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
		public void ClearFilters() {
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
				FilterChanged?.Invoke(this, new FilterEventArgs(filters, _FilterBox.Text));
			}
		}
		void FilterBox_Changed(object sender, EventArgs e) {
			var filters = GetFilterFlags();
			_Filter.Filter(_FilterBox.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), filters);
			FocusFilterBox();
			FilterChanged?.Invoke(this, new FilterEventArgs(filters, _FilterBox.Text));
		}

		public int GetFilterFlags() {
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
				case IconIds.ImplicitConversion:
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
				case IconIds.Destructor:
				case IconIds.ExtensionMethod:
					return filterTypes.MatchFlags(MemberFilterTypes.Method);
				case KnownImageIds.FieldPublic:
				case KnownImageIds.ConstantPublic:
				case KnownImageIds.PropertyPublic:
					return filterTypes.MatchFlags(MemberFilterTypes.Public | MemberFilterTypes.Field);
				case IconIds.PublicPropertyMethod:
					return filterTypes.MatchFlags(MemberFilterTypes.Public | MemberFilterTypes.Property);
				case KnownImageIds.FieldProtected:
				case KnownImageIds.ConstantProtected:
				case KnownImageIds.PropertyProtected:
					return filterTypes.MatchFlags(MemberFilterTypes.Protected | MemberFilterTypes.Field);
				case IconIds.ProtectedPropertyMethod:
					return filterTypes.MatchFlags(MemberFilterTypes.Protected | MemberFilterTypes.Property);
				case KnownImageIds.FieldInternal:
				case KnownImageIds.ConstantInternal:
				case KnownImageIds.PropertyInternal:
					return filterTypes.MatchFlags(MemberFilterTypes.Internal | MemberFilterTypes.Field);
				case IconIds.InternalPropertyMethod:
					return filterTypes.MatchFlags(MemberFilterTypes.Internal | MemberFilterTypes.Property);
				case KnownImageIds.FieldPrivate:
				case KnownImageIds.ConstantPrivate:
				case KnownImageIds.PropertyPrivate:
					return filterTypes.MatchFlags(MemberFilterTypes.Private | MemberFilterTypes.Field);
				case IconIds.PrivatePropertyMethod:
					return filterTypes.MatchFlags(MemberFilterTypes.Private | MemberFilterTypes.Property);
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
				case IconIds.LocalFunction:
					return filterTypes.MatchFlags(MemberFilterTypes.Method | MemberFilterTypes.Private);
				case IconIds.SwitchSection:
					return filterTypes.MatchFlags(MemberFilterTypes.AllMembers);
			}
			return false;
		}

		internal static bool FilterBySymbol(MemberFilterTypes filterTypes, ISymbol symbol) {
			MemberFilterTypes symbolFlags;
			if (symbol is null) {
				return false;
			}
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
			symbolFlags |= symbol.IsStatic ? MemberFilterTypes.Static : MemberFilterTypes.Instance;
			return filterTypes.MatchFlags(symbolFlags);
		}

		internal static bool FilterBySymbolType(MemberFilterTypes filterTypes, ISymbol symbol) {
			MemberFilterTypes symbolFlags;
			if (symbol is null) {
				return false;
			}
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
			if (item.Usage.HasAnyFlag(SymbolUsageKind.Trigger)) {
				m |= MemberFilterTypes.Trigger;
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

		internal sealed class FilterEventArgs : EventArgs
		{
			public readonly int FilterFlags;
			public readonly string FilterText;

			public FilterEventArgs(int filterFlags, string filter) {
				FilterFlags = filterFlags;
				FilterText = filter;
			}
		}

		abstract class FilterButtonGroup : UserControl
		{
			public event EventHandler FilterChanged;

			/// <summary>
			/// Returns a number which indicates the combination of filters.
			/// </summary>
			public abstract int Filters { get; }

			/// <summary>
			/// Updates the value of <see cref="Filters"/> according to button states.
			/// </summary>
			protected abstract void UpdateFilterValue();
			public abstract void ClearFilter();
			/// <summary>
			/// Updates counters on buttons after symbol list <paramref name="symbols"/> is populated.
			/// </summary>
			/// <param name="symbols">The list of symbols.</param>
			public abstract void UpdateNumbers(IEnumerable<SymbolItem> symbols);

			protected void OnFilterChanged() {
				FilterChanged?.Invoke(this, EventArgs.Empty);
			}

			protected ThemedToggleButton CreateButton(int imageId, string toolTip) {
				var b = new ThemedToggleButton(imageId, toolTip).ClearSpacing();
				b.Checked += UpdateFilterValueHandler;
				b.Unchecked += UpdateFilterValueHandler;
				return b;
			}
			protected static Border CreateSeparator() {
				return new Border { Width = 1, BorderThickness = WpfHelper.TinyMargin }
					.ReferenceProperty(BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey);
			}

			void UpdateFilterValueHandler(object sender, RoutedEventArgs eventArgs) {
				UpdateFilterValue();
			}
		}

		sealed class AccessibilityFilterButtonGroup : FilterButtonGroup
		{
			readonly ThemedToggleButton _PublicFilter, _ProtectedFilter, _InternalFilter, _PrivateFilter;
			readonly Border _Separator;
			MemberFilterTypes _Filters;
			bool _UiLock;

			public override int Filters => (int)_Filters;

			public AccessibilityFilterButtonGroup() {
				_PublicFilter = CreateButton(IconIds.PublicSymbols, R.T_Public);
				_ProtectedFilter = CreateButton(IconIds.ProtectedSymbols, R.T_Protected);
				_InternalFilter = CreateButton(IconIds.InternalSymbols, R.T_Internal);
				_PrivateFilter = CreateButton(IconIds.PrivateSymbols, R.T_Private);
				_Filters = MemberFilterTypes.AllAccessibilities;
				Content = new StackPanel {
					Children = {
						_PublicFilter, _ProtectedFilter, _InternalFilter, _PrivateFilter,
						(_Separator = CreateSeparator())
					},
					Orientation = Orientation.Horizontal
				}.ReferenceProperty(BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey);
			}

			protected override void UpdateFilterValue() {
				if (_UiLock) {
					return;
				}
				var f = MemberFilterTypes.None;
				if (_PublicFilter.IsChecked == true) {
					f |= MemberFilterTypes.Public;
				}
				if (_ProtectedFilter.IsChecked == true) {
					f |= MemberFilterTypes.Protected;
				}
				if (_InternalFilter.IsChecked == true) {
					f |= MemberFilterTypes.Internal;
				}
				if (_PrivateFilter.IsChecked == true) {
					f |= MemberFilterTypes.Private;
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
				int pu = 0, pro = 0, i = 0, p = 0;
				foreach (var item in symbols) {
					var symbol = item.Symbol;
					if (symbol == null || symbol.IsImplicitlyDeclared) {
						continue;
					}
					if (symbol.CanBeReferencedByName == false
						&& (symbol.Kind != SymbolKind.Method || ((IMethodSymbol)symbol).MethodKind != MethodKind.Constructor)) {
						continue;
					}
					switch (symbol.DeclaredAccessibility) {
						case Accessibility.Private:
							++p; break;
						case Accessibility.Internal:
							++i; break;
						case Accessibility.ProtectedAndInternal:
						case Accessibility.ProtectedOrInternal:
							++pro; ++i; break;
						case Accessibility.Public:
							++pu; break;
						case Accessibility.Protected:
							++pro; break;
					}
				}
				ToggleFilterButton(_PublicFilter, pu);
				ToggleFilterButton(_ProtectedFilter, pro);
				ToggleFilterButton(_InternalFilter, i);
				ToggleFilterButton(_PrivateFilter, p);
				_Separator.Visibility = (pu != 0 || pro != 0 || i != 0 || p != 0) ? Visibility.Visible : Visibility.Collapsed;
			}

			public override void ClearFilter() {
				_UiLock = true;
				_PublicFilter.IsChecked = _ProtectedFilter.IsChecked = _InternalFilter.IsChecked = _PrivateFilter.IsChecked = false;
				_UiLock = false;
				_Filters |= MemberFilterTypes.AllAccessibilities;
			}
		}

		sealed class TypeFilterButtonGroup : FilterButtonGroup
		{
			readonly ThemedToggleButton _ClassFilter, _StructFilter, _EnumFilter, _InterfaceFilter, _DelegateFilter, _NamespaceFilter;
			readonly Border _Separator;
			MemberFilterTypes _Filters;
			bool _UiLock;

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
				if (_UiLock) {
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
				_UiLock = true;
				_ClassFilter.IsChecked = _InterfaceFilter.IsChecked = _DelegateFilter.IsChecked
					= _StructFilter.IsChecked = _EnumFilter.IsChecked = _NamespaceFilter.IsChecked = false;
				_UiLock = false;
				_Filters |= MemberFilterTypes.AllTypes;
			}
		}

		sealed class MemberFilterButtonGroup : FilterButtonGroup
		{
			readonly ThemedToggleButton _FieldFilter, _PropertyFilter, _EventFilter, _MethodFilter, _TypeFilter;
			readonly Border _Separator;
			readonly bool _AutoPropertyAsField;
			MemberFilterTypes _Filters;
			bool _UiLock;

			public override int Filters => (int)_Filters;

			public MemberFilterButtonGroup(NaviBarOptions options = NaviBarOptions.None) {
				bool autoPropertyAsField = _AutoPropertyAsField = options.MatchFlags(NaviBarOptions.AutoPropertiesAsFields);
				_FieldFilter = CreateButton(IconIds.Field, autoPropertyAsField ? R.T_FieldsAndAutoProperties : R.T_FieldsProperties);
				_MethodFilter = CreateButton(IconIds.Method, autoPropertyAsField ? R.T_AccessorsAndMethods : R.T_Methods);
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
				if (_UiLock) {
					return;
				}
				var f = MemberFilterTypes.None;
				if (_FieldFilter.IsChecked == true) {
					f |= _AutoPropertyAsField ? MemberFilterTypes.Field : MemberFilterTypes.FieldOrProperty;
				}
				if (_PropertyFilter?.IsChecked == true) {
					f |= MemberFilterTypes.Property;
				}
				if (_EventFilter.IsChecked == true) {
					f |= MemberFilterTypes.Event;
				}
				if (_MethodFilter.IsChecked == true) {
					f |= _AutoPropertyAsField ? MemberFilterTypes.Property | MemberFilterTypes.Method : MemberFilterTypes.Method;
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
				HashSet<ISymbol> backingFields = null;
				IEnumerable<ISymbol> members;
				if (_AutoPropertyAsField) {
					members = symbols.Select(s => s.Symbol).ToArray();
					backingFields = new HashSet<ISymbol>();
					foreach (var item in members) {
						if (item != null
							&& item.IsImplicitlyDeclared
							&& item.Kind == SymbolKind.Field
							&& ((IFieldSymbol)item).AssociatedSymbol != null) {
							backingFields.Add(((IFieldSymbol)item).AssociatedSymbol);
						}
					}
				}
				else {
					members = symbols.Select(s => s.Symbol);
				}
				foreach (var symbol in members) {
					if (symbol == null || symbol.IsImplicitlyDeclared) {
						continue;
					}
					if (symbol.CanBeReferencedByName == false) {
						if ((symbol is IMethodSymbol ms) && (ms.MethodKind == MethodKind.Constructor || ms.MethodKind == MethodKind.StaticConstructor)) {
							++m;
						}
						continue;
					}
					switch (symbol.Kind) {
						case SymbolKind.Event:
							++e; break;
						case SymbolKind.Field:
							++f; break;
						case SymbolKind.Property:
							if (_AutoPropertyAsField
								&& (symbol.IsAbstract || backingFields.Contains(symbol) == false)) {
								++m;
							}
							else {
								++f;
							}
							break;
						case SymbolKind.Method:
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
				_UiLock = true;
				_FieldFilter.IsChecked = _MethodFilter.IsChecked = _EventFilter.IsChecked
					= _TypeFilter.IsChecked = false;
				if (_PropertyFilter != null) {
					_PropertyFilter.IsChecked = false;
				}
				_UiLock = false;
				_Filters |= MemberFilterTypes.AllMembers;
			}
		}

		sealed class InstanceFilterButtonGroup : FilterButtonGroup
		{
			readonly ThemedToggleButton _StaticMemberFilter, _InstanceMemberFilter;
			readonly Border _Separator;
			MemberFilterTypes _Filters;
			bool _UiLock;

			public override int Filters => (int)_Filters;

			public InstanceFilterButtonGroup() {
				_StaticMemberFilter = CreateButton(IconIds.StaticMember, R.T_Static);
				_InstanceMemberFilter = CreateButton(IconIds.InstanceMember, R.T_Instance);

				_Filters = MemberFilterTypes.AllInstance;
				Content = new StackPanel {
					Children = {
						_StaticMemberFilter, _InstanceMemberFilter,
						(_Separator = CreateSeparator())
					},
					Orientation = Orientation.Horizontal
				};
			}

			protected override void UpdateFilterValue() {
				if (_UiLock) {
					return;
				}
				var f = MemberFilterTypes.None;
				if (_InstanceMemberFilter.IsChecked == true) {
					f |= MemberFilterTypes.Instance;
				}
				if (_StaticMemberFilter.IsChecked == true) {
					f |= MemberFilterTypes.Static;
				}
				if (f.HasAnyFlag(MemberFilterTypes.AllInstance) == false) {
					f |= MemberFilterTypes.AllInstance;
				}
				if (_Filters != f) {
					_Filters = f;
					OnFilterChanged();
				}
			}

			public override void UpdateNumbers(IEnumerable<SymbolItem> symbols) {
				int i = 0, s = 0;
				foreach (var item in symbols) {
					var symbol = item.Symbol;
					if (symbol == null || symbol.IsImplicitlyDeclared) {
						continue;
					}
					if (symbol.IsStatic) {
						s++;
					}
					else {
						i++;
					}
				}
				ToggleFilterButton(_StaticMemberFilter, s);
				ToggleFilterButton(_InstanceMemberFilter, i);
				_Separator.Visibility = (i != 0 || s != 0) ? Visibility.Visible : Visibility.Collapsed;
			}

			public override void ClearFilter() {
				_UiLock = true;
				_InstanceMemberFilter.IsChecked = _StaticMemberFilter.IsChecked = false;
				_UiLock = false;
				_Filters |= MemberFilterTypes.AllInstance;
			}
		}

		sealed class SymbolUsageFilterButtonGroup : FilterButtonGroup
		{
			readonly ThemedToggleButton _WriteFilter, _ReadFilter, _EventFilter, _TypeCastFilter, _TypeReferenceFilter;
			readonly Border _Separator;
			MemberFilterTypes _Filters;
			bool _UiLock;

			public override int Filters => (int)_Filters;

			public SymbolUsageFilterButtonGroup() {
				_WriteFilter = CreateButton(IconIds.UseToWrite, R.T_Write);
				_ReadFilter = CreateButton(IconIds.UseAsDelegate, R.T_DelegateEvent);
				_EventFilter = CreateButton(IconIds.TriggerEvent, R.T_TriggerEvent);
				_TypeCastFilter = CreateButton(IconIds.UseToCast, R.T_TypeConversion);
				_TypeReferenceFilter = CreateButton(IconIds.UseAsTypeParameter, R.T_TypeReferenceOrArgument);
				_Filters = MemberFilterTypes.AllUsages;
				Content = new StackPanel {
					Children = {
						_WriteFilter, _ReadFilter, _EventFilter, _TypeCastFilter, _TypeReferenceFilter,
						(_Separator = CreateSeparator())
					},
					Orientation = Orientation.Horizontal
				};
			}

			protected override void UpdateFilterValue() {
				if (_UiLock) {
					return;
				}
				var f = MemberFilterTypes.None;
				if (_WriteFilter.IsChecked == true) {
					f |= MemberFilterTypes.Write;
				}
				if (_ReadFilter.IsChecked == true) {
					f |= MemberFilterTypes.Read;
				}
				if (_EventFilter.IsChecked == true) {
					f |= MemberFilterTypes.Trigger;
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
				int r = 0, w = 0, tc = 0, iv = 0, tr = 0;
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
					if (t.MatchFlags(SymbolUsageKind.Trigger)) {
						++iv;
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
				ToggleFilterButton(_EventFilter, iv);
				ToggleFilterButton(_TypeCastFilter, tc);
				ToggleFilterButton(_TypeReferenceFilter, tr);
				_Separator.Visibility = (w != 0 || r != 0 || tc != 0 || tr != 0) ? Visibility.Visible : Visibility.Collapsed;
			}

			public override void ClearFilter() {
				_UiLock = true;
				_WriteFilter.IsChecked = _ReadFilter.IsChecked = _EventFilter.IsChecked =  _TypeCastFilter.IsChecked = _TypeReferenceFilter.IsChecked = false;
				_UiLock = false;
				_Filters |= MemberFilterTypes.AllUsages;
			}
		}
	}
}
