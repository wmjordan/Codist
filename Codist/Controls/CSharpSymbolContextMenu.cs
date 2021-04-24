using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Task = System.Threading.Tasks.Task;
using R = Codist.Properties.Resources;

namespace Codist.Controls
{
	sealed class CSharpSymbolContextMenu : ContextMenu
	{
		SyntaxNode _Node;
		ISymbol _Symbol;
		readonly SemanticContext _SemanticContext;

		public CSharpSymbolContextMenu(SemanticContext semanticContext) {
			Resources = SharedDictionaryManager.ContextMenu;
			Foreground = ThemeHelper.ToolWindowTextBrush;
			this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
			_SemanticContext = semanticContext;
		}

		public SyntaxNode SyntaxNode {
			get => _Node;
			set => _Node = value;
		}

		public ISymbol Symbol {
			get => _Symbol;
			set => _Symbol = value?.Kind == SymbolKind.Alias ? ((IAliasSymbol)value).Target : value;
		}

		public event RoutedEventHandler ItemClicked;

		public void AddNodeCommands() {
			if (_Node != null) {
				Items.Add(CreateItem(IconIds.GoToDefinition, R.CMD_GoToDefinition, GoToNode));
				Items.Add(CreateItem(IconIds.SelectCode, R.CMD_SelectCode, SelectNode));
			}
		}
		public void AddSymbolNodeCommands() {
			if (_Symbol.HasSource()) {
				Items.Add(CreateItem(IconIds.GoToDefinition, R.CMD_GoToDefinition, GoToSymbolDefinition));
				if (_Symbol.Kind != SymbolKind.Namespace && _Node == null) {
					Items.Add(CreateItem(IconIds.SelectCode, R.CMD_SelectCode, () => _Symbol.GetSyntaxNode().SelectNode(true)));
				}
			}
			else if (_Node != null) {
				Items.Add(CreateItem(IconIds.SelectCode, R.CMD_SelectCode, SelectNode));
			}
			if (_Symbol != null) {
				Items.Add(CreateItem(IconIds.Copy, R.CMD_CopySymbolName, CopySymbolName));
				if (_Symbol.IsQualifiable()) {
					Items.Add(CreateItem(IconIds.Copy, R.CMD_CopyQualifiedSymbolName, CopyQualifiedSymbolName));
				}
				if (_Symbol.Kind == SymbolKind.Field && ((IFieldSymbol)_Symbol).HasConstantValue) {
					Items.Add(CreateItem(IconIds.Constant, R.CMD_CopyConstantValue, CopyConstantValue));
				}
			}
		}

		public void AddSymbolCommands() {
			Items.Add(CreateItem(IconIds.Copy, R.CMD_CopySymbolName, CopySymbolName));
			if (_Symbol != null && _Symbol.IsQualifiable()) {
				Items.Add(CreateItem(IconIds.Copy, R.CMD_CopyQualifiedSymbolName, CopyQualifiedSymbolName));
			}
		}

		public void AddAnalysisCommands() {
			if (_SemanticContext.Document == null) {
				return;
			}
			switch (_Symbol.Kind) {
				case SymbolKind.Method:
				case SymbolKind.Property:
				case SymbolKind.Event:
				case SymbolKind.Field:
					CreateCommandForMembers();
					break;
				case SymbolKind.Local:
				case SymbolKind.Parameter:
					CreateCommandsForReturnTypeCommand();
					break;
				case SymbolKind.NamedType:
					CreateCommandForNamedType(_Symbol as INamedTypeSymbol);
					break;
				case SymbolKind.Namespace:
					Items.Add(CreateItem(IconIds.ListMembers, R.CMD_FindMembers, () => _SemanticContext.FindMembersAsync(_Symbol)));
					break;
			}
			if (_SemanticContext.SemanticModel != null) {
				if (_Node != null && _Node.Kind().IsDeclaration()
					&& _Node.SyntaxTree == _SemanticContext.SemanticModel.SyntaxTree
					&& _Symbol.Kind != SymbolKind.Namespace) {
					Items.Add(CreateItem(IconIds.FindReferencingSymbols, R.CMD_FindReferencedSymbols, FindReferencedSymbols));
				}
				//Items.Add(CreateCommandMenu("Find references...", KnownImageIds.ReferencedDimension, _Symbol, "No reference found", FindReferences));
				Items.Add(CreateItem(IconIds.FindSymbolsWithName, R.CMD_FindSymbolwithName.Replace("<NAME>", _Symbol.Name), () => _SemanticContext.FindSymbolWithName(_Symbol)));
			}
		}

		private void CreateCommandForMembers() {
			if (_Symbol.Kind != SymbolKind.Method || IsExternallyCallable(((IMethodSymbol)_Symbol).MethodKind)) {
				var filter = Keyboard.Modifiers == ModifierKeys.Control ? (Predicate<ISymbol>)(s => s == _Symbol) : null;
				Items.Add(CreateItem(IconIds.FindReferrers, R.CMD_FindReferrers, () => _SemanticContext.FindReferrersAsync(_Symbol, filter)));
			}
			if (_Symbol.MayHaveOverride()) {
				Items.Add(CreateItem(IconIds.FindOverloads, R.CMD_FindOverrides, () => _SemanticContext.FindOverridesAsync(_Symbol)));
			}
			var st = _Symbol.ContainingType;
			if (st != null && st.TypeKind == TypeKind.Interface) {
				Items.Add(CreateItem(IconIds.FindImplementations, R.CMD_FindImplementations, () => _SemanticContext.FindImplementationsAsync(_Symbol)));
			}
			if (_Symbol.Kind != SymbolKind.Event) {
				CreateCommandsForReturnTypeCommand();
			}
			if (_Symbol.Kind == SymbolKind.Method) {
				switch (((IMethodSymbol)_Symbol).MethodKind) {
					case MethodKind.Constructor:
						if (st.SpecialType == SpecialType.None) {
							CreateInstanceCommandsForType(st);
						}
						break;
					case MethodKind.StaticConstructor:
					case MethodKind.Destructor:
						break;
					default:
						Items.Add(CreateItem(IconIds.FindMethodsMatchingSignature, R.CMD_FindMethodsSameSignature, () => _SemanticContext.FindMethodsBySignature(_Symbol)));
						break;
				}
			}

			bool IsExternallyCallable(MethodKind methodKind) {
				switch (methodKind) {
					case MethodKind.AnonymousFunction:
					case MethodKind.LocalFunction:
					case MethodKind.Destructor:
					case MethodKind.StaticConstructor:
						return false;
				}
				return true;
			}
		}

		void CreateCommandForNamedType(INamedTypeSymbol t) {
			if (t.TypeKind == TypeKind.Class || t.TypeKind == TypeKind.Struct) {
				var ctor = _SemanticContext.NodeIncludeTrivia.GetObjectCreationNode();
				if (ctor != null) {
					var symbol = _SemanticContext.SemanticModel.GetSymbolOrFirstCandidate(ctor);
					if (symbol != null) {
						Items.Add(CreateItem(IconIds.FindReferrers, R.CMD_FindCallers, () => _SemanticContext.FindReferrersAsync(symbol)));
					}
				}
				else if (t.InstanceConstructors.Length > 0) {
					Items.Add(CreateItem(IconIds.FindReferrers, R.CMD_FindConstructorCallers, () => _SemanticContext.FindReferrersAsync(t, s => s.Kind == SymbolKind.Method)));
				}
			}
			Items.Add(CreateItem(IconIds.FindTypeReferrers, R.CMD_FindTypeReferrers, () => _SemanticContext.FindReferrersAsync(t, s => s.Kind == SymbolKind.NamedType, IsTypeReference)));
			Items.Add(CreateItem(IconIds.ListMembers, R.CMD_FindMembers, () => _SemanticContext.FindMembersAsync(t)));
			if (t.IsStatic) {
				return;
			}
			if (t.IsSealed == false) {
				if (t.TypeKind == TypeKind.Class) {
					Items.Add(CreateItem(IconIds.FindDerivedTypes, R.CMD_FindDerivedClasses, () => _SemanticContext.FindDerivedClassesAsync(t)));
				}
				else if (t.TypeKind == TypeKind.Interface) {
					Items.Add(CreateItem(IconIds.FindImplementations, R.CMD_FindImplementations, () => _SemanticContext.FindImplementationsAsync(t)));
					Items.Add(CreateItem(IconIds.FindDerivedTypes, R.CMD_FindInheritedInterfaces, () => _SemanticContext.FindSubInterfacesAsync(t)));
				}
			}
			if (t.TypeKind == TypeKind.Delegate) {
				Items.Add(CreateItem(IconIds.FindMethodsMatchingSignature, R.CMD_FindMethodsSameSignature, () => _SemanticContext.FindMethodsBySignature(_Symbol)));
			}
			Items.Add(CreateItem(IconIds.ExtensionMethod, R.CMD_FindExtensions, () => _SemanticContext.FindExtensionMethodsAsync(t)));
			if (t.SpecialType == SpecialType.None) {
				CreateInstanceCommandsForType(t);
			}
		}

		bool IsTypeReference(SyntaxNode node) {
			var p = node.Parent.UnqualifyExceptNamespace();
			switch (p.Kind()) {
				case SyntaxKind.TypeOfExpression:
				case SyntaxKind.SimpleMemberAccessExpression:
				case SyntaxKind.CatchDeclaration:
				case SyntaxKind.CastExpression:
				case SyntaxKind.IsExpression:
				case SyntaxKind.IsPatternExpression:
				case SyntaxKind.AsExpression:
				case SyntaxKind.InvocationExpression:
					return true;
				case SyntaxKind.GenericName:
					return p.Parent.IsKind(SyntaxKind.ObjectCreationExpression) || IsTypeReference(p);
				case SyntaxKind.TypeArgumentList:
					p = p.Parent;
					goto case SyntaxKind.GenericName;
				case SyntaxKind.QualifiedName:
					return IsTypeReference(p);
				case SyntaxKind.DeclarationPattern:
					return p.Parent.IsKind(SyntaxKind.IsPatternExpression) || p.Parent.IsKind(SyntaxKind.CasePatternSwitchLabel);
			}
			return false;
		}

		void CreateCommandsForReturnTypeCommand() {
			var type = _Symbol.GetReturnType();
			if (type != null && type.SpecialType == SpecialType.None && type.TypeKind != TypeKind.TypeParameter && type.IsTupleType == false) {
				var et = type.ResolveElementType();
				Items.Add(CreateItem(IconIds.ListMembers, R.CMD_FindMembersOf.Replace("<TYPE>", et.Name + et.GetParameterString()), () => _SemanticContext.FindMembersAsync(et)));
				if (type.IsStatic == false) {
					Items.Add(CreateItem(IconIds.ExtensionMethod, R.CMD_FindExtensionsFor.Replace("<TYPE>", type.GetTypeName()), () => _SemanticContext.FindExtensionMethodsAsync(type)));
				}
				if (et.ContainingAssembly.GetSourceType() != AssemblySource.Metadata) {
					Items.Add(CreateItem(IconIds.GoToReturnType, R.CMD_GoTo.Replace("<TYPE>", et.GetTypeName()), () => et.GoToSource()));
				}
			}
		}

		public void AddFindAllReferencesCommand() {
			Items.Add(CreateItem(IconIds.FindReference, R.CMD_FindAllReferences, FindAllReferences));
		}

		public void AddGoToAnyCommands() {
			Items.Add(CreateItem(IconIds.GoToMember, R.CMD_GotoMember, GoToMember));
			Items.Add(CreateItem(IconIds.GoToType, R.CMD_GotoType, GoToType));
			Items.Add(CreateItem(IconIds.GoToSymbol, R.CMD_GotoSymbol, GoToSymbol));
		}

		public void AddTitleItem(string name) {
			Items.Add(new MenuItem {
				Header = name,
				IsEnabled = false,
				Icon = null,
				HorizontalContentAlignment = HorizontalAlignment.Right
			});
		}

		static MenuItem CreateItem(int imageId, string title) {
			return new MenuItem {
				Icon = ThemeHelper.GetImage(imageId),
				Header = new ThemedMenuText { Text = title }
			};
		}

		public MenuItem CreateItem(int imageId, string title, RoutedEventHandler clickHandler) {
			var item = CreateItem(imageId, title);
			item.Click += Item_Click;
			item.Click += clickHandler;
			return item;
		}

		public MenuItem CreateItem(int imageId, string title, Action clickHandler) {
			var item = CreateItem(imageId, title);
			item.Click += Item_Click;
			if (clickHandler != null) {
				item.Click += (s, args) => clickHandler();
			}
			return item;
		}

		public MenuItem CreateItem(int imageId, string title, Func<Task> asyncAction) {
			var item = CreateItem(imageId, title);
			item.Click += Item_Click;
			if (asyncAction != null) {
				item.Click += new AsyncCommand(asyncAction).AsyncEventHandler;
			}
			return item;
		}

		public MenuItem CreateItem(int imageId, Action<MenuItem> itemConfigurator, Action clickHandler) {
			var item = new MenuItem { Icon = ThemeHelper.GetImage(imageId) };
			itemConfigurator(item);
			item.Click += Item_Click;
			if (clickHandler != null) {
				item.Click += (s, args) => clickHandler();
			}
			return item;
		}

		void Item_Click(object sender, RoutedEventArgs e) {
			ItemClicked?.Invoke(sender, e);
		}

		void CreateInstanceCommandsForType(INamedTypeSymbol t) {
			Items.Add(CreateItem(IconIds.InstanceProducer, R.CMD_FindInstanceProducer, () => _SemanticContext.FindInstanceProducerAsync(t)));
			Items.Add(CreateItem(IconIds.Argument, R.CMD_FindInstanceAsParameter, () => _SemanticContext.FindInstanceAsParameterAsync(t)));
		}

		void FindReferencedSymbols() {
			var m = new SymbolMenu(_SemanticContext);
			var c = 0;
			var containerType = _Symbol.ContainingType ?? _Symbol;
			var loc = _Node.SyntaxTree.FilePath;
			foreach (var s in _Node.FindReferencingSymbols(_SemanticContext.SemanticModel, true)
					.OrderBy(i => i.Key.ContainingType == containerType ? null : (i.Key.ContainingType ?? i.Key).Name)
					.ThenBy(i => i.Key.Name)
					.Select(i => i.Key)) {
				var sl = s.DeclaringSyntaxReferences.First();
				SymbolItem i;
				if (sl.SyntaxTree.FilePath != loc) {
					i = m.Menu.Add(sl.ToLocation());
					i.Content.FontWeight = FontWeights.Bold;
					i.Content.HorizontalAlignment = HorizontalAlignment.Center;
					loc = sl.SyntaxTree.FilePath;
				}
				i = m.Menu.Add(s, false);
				if (s.ContainingType.Equals(containerType) == false) {
					i.Hint = (s.ContainingType ?? s).ToDisplayString(CodeAnalysisHelper.MemberNameFormat);
				}
				++c;
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(_Symbol.GetImageId()))
				.Append(_Symbol.ToDisplayString(CodeAnalysisHelper.MemberNameFormat), true)
				.Append(R.T_ReferencedSymbols)
				.Append(c.ToString());
			m.Show();
		}

		#region Menu event handlers
		void CopySymbolName(object sender, RoutedEventArgs args) {
			try {
				Clipboard.SetDataObject(_Symbol.GetOriginalName());
			}
			catch (SystemException) {
				// ignore failure
			}
		}
		void CopyQualifiedSymbolName(object sender, RoutedEventArgs args) {
			try {
				var s = _Symbol.OriginalDefinition;
				string t;
				switch (s.Kind) {
					case SymbolKind.Namespace:
					case SymbolKind.NamedType: t = s.ToDisplayString(CodeAnalysisHelper.QualifiedTypeNameFormat); break;
					case SymbolKind.Method:
						var m = s as IMethodSymbol;
						if (m.ReducedFrom != null) {
							s = m.ReducedFrom;
						}
						goto default;
					default:
						t = s.ToDisplayString(CodeAnalysisHelper.TypeMemberNameFormat);
						break;
				}
				Clipboard.SetDataObject(t);
			}
			catch (SystemException) {
				// ignore failure
			}
		}
		void CopyConstantValue(object sender, RoutedEventArgs args) {
			var f = _Symbol as IFieldSymbol;
			if (f.HasConstantValue == false) {
				return;
			}
			try {
				Clipboard.SetDataObject(f.ConstantValue?.ToString() ?? "null");
			}
			catch (SystemException) {
				// ignore failure
			}
		}
		static void FindAllReferences(object sender, RoutedEventArgs args) {
			TextEditorHelper.ExecuteEditorCommand("Edit.FindAllReferences");
		}
		static void GoToMember(object sender, RoutedEventArgs args) {
			TextEditorHelper.ExecuteEditorCommand("Edit.GoToMember");
		}
		static void GoToType(object sender, RoutedEventArgs args) {
			TextEditorHelper.ExecuteEditorCommand("Edit.GoToType");
		}
		static void GoToSymbol(object sender, RoutedEventArgs args) {
			TextEditorHelper.ExecuteEditorCommand("Edit.GoToSymbol");
		}
		void GoToNode(object sender, RoutedEventArgs args) {
			_Node.GetReference().GoToSource();
		}
		void SelectNode(object sender, RoutedEventArgs args) {
			_Node.SelectNode(true);
		}
		void GoToSymbolDefinition(object sender, RoutedEventArgs args) {
			var locs = _Symbol.GetSourceReferences();
			if (locs.Length == 1) {
				locs[0].GoToSource();
			}
			else {
				_SemanticContext.ShowLocations(_Symbol, locs, (sender as UIElement).GetParent<ListBoxItem>());
			}
		}
		#endregion

		abstract class CommandItem : MenuItem
		{
			protected CommandItem(int imageId, string title) {
				Icon = ThemeHelper.GetImage(imageId);
				Header = new ThemedMenuText { Text = title };
			}

			public ISymbol Symbol { get; set; }

			public abstract bool QueryStatus();
			public abstract void Execute();
		}

		sealed class AsyncCommand
		{
			readonly Func<Task> _Task;

			public AsyncCommand(Func<Task> action) {
				_Task = action;
			}
			public async void AsyncEventHandler(object sender, RoutedEventArgs args) {
				await Task.Run(_Task);
			}
		}
	}

	sealed class SymbolMenu
	{
		readonly ExternalAdornment _Container;
		readonly StackPanel _HeaderPanel;

		public SymbolList Menu { get; }
		public ThemedMenuText Title { get; }
		public SymbolFilterBox FilterBox { get; }

		public SymbolMenu(SemanticContext semanticContext) : this(semanticContext, SymbolListType.None) { }
		public SymbolMenu(SemanticContext semanticContext, SymbolListType listType) {
			_Container = ExternalAdornment.GetOrCreate(semanticContext.View);
			Menu = new SymbolList(semanticContext) {
				Container = _Container,
				ContainerType = listType
			};
			Menu.Header = _HeaderPanel = new StackPanel {
				Margin = WpfHelper.MenuItemMargin,
				Children = {
						(Title = new ThemedMenuText {
							TextAlignment = TextAlignment.Left,
							Padding = WpfHelper.SmallVerticalMargin
						}),
						(FilterBox = new SymbolFilterBox(Menu) {
							Margin = WpfHelper.NoMargin
						}),
						new Separator()
					}
			};
			Menu.HeaderButtons = new StackPanel {
				Orientation = Orientation.Horizontal,
				Children = {
					new ThemedButton(ThemeHelper.GetImage(IconIds.TogglePinning), R.CMD_Pin, TogglePinButton),
					new ThemedButton(IconIds.Close, R.CMD_Close, () => {
						_Container.Children.Remove(Menu);
						_Container.FocusOnTextView();
					})
				}
			};
			Menu.MouseLeftButtonUp += MenuItemSelect;
			_Container.MakeDraggable(Menu);
		}

		void TogglePinButton(object sender, RoutedEventArgs e) {
			((ThemedButton)e.Source).Content = ThemeHelper.GetImage((Menu.IsPinned = !Menu.IsPinned) ? IconIds.Pin : IconIds.Unpin);
		}

		public void Show(UIElement relativeElement = null) {
			ShowMenu(relativeElement);
			UpdateNumbers();
			FilterBox.FocusFilterBox();
		}

		void ShowMenu(UIElement positionElement) {
			var m = Menu;
			_Container.Children.Add(m);
			m.ItemsControlMaxHeight = _Container.ActualHeight / 2;
			m.RefreshItemsSource();
			m.ScrollToSelectedItem();
			m.PreviewKeyUp -= OnMenuKeyUp;
			m.PreviewKeyUp += OnMenuKeyUp;
			if (m.Symbols.Count > 100) {
				m.EnableVirtualMode = true;
			}
			
			var p = positionElement != null ? positionElement.TranslatePoint(new Point(positionElement.RenderSize.Width, 0), _Container) : Mouse.GetPosition(_Container);
			Canvas.SetLeft(m, p.X);
			Canvas.SetTop(m, p.Y);
		}
		void UpdateNumbers() {
			FilterBox.UpdateNumbers(Menu.Symbols);
		}

		void MenuItemSelect(object sender, MouseButtonEventArgs e) {
			var menu = sender as VirtualList;
			if (e.OccursOn<ListBoxItem>()) {
				_Container.FocusOnTextView();
				(menu.SelectedItem as SymbolItem)?.GoToSource();
			}
		}

		void OnMenuKeyUp(object sender, KeyEventArgs e) {
			if (e.Key == Key.Escape) {
				_Container.Children.Remove(Menu);
				e.Handled = true;
			}
		}
	}
}
