using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;

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
			set => _Symbol = value.Kind == SymbolKind.Alias ? ((IAliasSymbol)value).Target : value;
		}

		public event RoutedEventHandler ItemClicked;

		public void AddNodeCommands() {
			if (_Node != null) {
				Items.Add(CreateItem(KnownImageIds.GoToDefinition, "Go to Definition", GoToNode));
				Items.Add(CreateItem(KnownImageIds.BlockSelection, "Select Code", SelectNode));
			}
		}

		public void AddSymbolCommands() {
			Items.Add(CreateItem(KnownImageIds.Copy, "Copy Symbol Name", CopySymbolName));
		}

		public void AddAnalysisCommands() {
			switch (_Symbol.Kind) {
				case SymbolKind.Method:
				case SymbolKind.Property:
				case SymbolKind.Event:
					Items.Add(CreateItem(KnownImageIds.ShowCallerGraph, "Find Callers...", () => FindCallersExt(_Symbol, _SemanticContext)));
					if (_Symbol.MayHaveOverride()) {
						Items.Add(CreateItem(KnownImageIds.OverloadBehavior, "Find Overrides...", () => FindOverrides(_Symbol, _SemanticContext)));
					}
					var st = _Symbol.ContainingType;
					if (st != null && st.TypeKind == TypeKind.Interface) {
						Items.Add(CreateItem(KnownImageIds.ImplementInterface, "Find Implementations...", () => FindImplementations(_Symbol, _SemanticContext)));
					}
					if (_Symbol.Kind != SymbolKind.Event) {
						CreateCommandsForReturnTypeCommand();
					}
					if (_Symbol.Kind == SymbolKind.Method
						&& ((IMethodSymbol)_Symbol).MethodKind == MethodKind.Constructor
						&& st.SpecialType == SpecialType.None) {
						CreateInstanceCommandsForType(st);
					}
					break;
				case SymbolKind.Field:
					Items.Add(CreateItem(KnownImageIds.ShowCallerGraph, "Find Callers...", () => FindCallersExt(_Symbol, _SemanticContext)));
					CreateCommandsForReturnTypeCommand();
					break;
				case SymbolKind.Local:
				case SymbolKind.Parameter:
					CreateCommandsForReturnTypeCommand();
					break;
				case SymbolKind.NamedType:
					CreateCommandForNamedType(_Symbol as INamedTypeSymbol);
					break;
				case SymbolKind.Namespace:
					Items.Add(CreateItem(KnownImageIds.ListMembers, "Find Members...", () => FindMembers(_Symbol, _SemanticContext)));
					break;
			}
			//if (_Node != null && _Node.IsDeclaration()
			//	&& _Node.SyntaxTree == _SemanticContext.SemanticModel.SyntaxTree
			//	&& _Symbol.Kind != SymbolKind.Namespace) {
			//	Items.Add(CreateItem(KnownImageIds.OpenDocumentFromCollection, "Find Referenced Documents...", FindReferencedDocuments));
			//}
			//Items.Add(CreateCommandMenu("Find references...", KnownImageIds.ReferencedDimension, _Symbol, "No reference found", FindReferences));
			Items.Add(CreateItem(KnownImageIds.FindSymbol, "Find Symbol with Name " + _Symbol.Name + "...", () => FindSymbolWithName(_Symbol, _SemanticContext)));
		}

		public void AddFindAllReferencesCommand() {
			Items.Add(CreateItem(KnownImageIds.ReferencedDimension, "Find All References", FindAllReferences));
		}

		public void AddGoToAnyCommands() {
			Items.Add(CreateItem(KnownImageIds.ListMembers, "Go to Member", GoToMember));
			Items.Add(CreateItem(KnownImageIds.Type, "Go to Type", GoToType));
			Items.Add(CreateItem(KnownImageIds.FindSymbol, "Go to Symbol", GoToSymbol));
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

		void Item_Click(object sender, RoutedEventArgs e) {
			ItemClicked?.Invoke(sender, e);
		}

		void CreateCommandForNamedType(INamedTypeSymbol t) {
			if (t.TypeKind == TypeKind.Class || t.TypeKind == TypeKind.Struct) {
				var ctor = _SemanticContext.NodeIncludeTrivia.GetObjectCreationNode();
				if (ctor != null) {
					var symbol = _SemanticContext.SemanticModel.GetSymbolOrFirstCandidate(ctor);
					if (symbol != null) {
						Items.Add(CreateItem(KnownImageIds.ShowCallerGraph, "Find Callers...", () => FindCallersExt(symbol, _SemanticContext)));
					}
				}
				else if (t.InstanceConstructors.Length > 0) {
					Items.Add(CreateItem(KnownImageIds.ShowCallerGraph, "Find Constructor Callers...", () => FindCallersExt(t, _SemanticContext)));
				}
			}
			Items.Add(CreateItem(KnownImageIds.ListMembers, "Find Members...", () => FindMembers(t, _SemanticContext)));
			if (t.IsStatic) {
				return;
			}
			if (t.IsSealed == false) {
				if (t.TypeKind == TypeKind.Class) {
					Items.Add(CreateItem(KnownImageIds.NewClass, "Find Derived Classes...", () => FindDerivedClasses(t, _SemanticContext)));
				}
				else if (t.TypeKind == TypeKind.Interface) {
					Items.Add(CreateItem(KnownImageIds.ImplementInterface, "Find Implementations...", () => FindImplementations(t, _SemanticContext)));
				}
			}
			Items.Add(CreateItem(KnownImageIds.ExtensionMethod, "Find Extensions...", () => FindExtensionMethods(t, _SemanticContext)));
			if (t.SpecialType == SpecialType.None) {
				CreateInstanceCommandsForType(t);
			}
		}

		void CreateInstanceCommandsForType(INamedTypeSymbol t) {
			Items.Add(CreateItem(KnownImageIds.NewItem, "Find Instance Producer...", () => FindInstanceProducer(t, _SemanticContext)));
			Items.Add(CreateItem(KnownImageIds.Parameter, "Find Instance as Parameter...", () => FindInstanceAsParameter(t, _SemanticContext)));
		}

		void CreateCommandsForReturnTypeCommand() {
			var type = _Symbol.GetReturnType();
			if (type != null && type.SpecialType == SpecialType.None && type.IsTupleType == false) {
				var et = type.ResolveElementType();
				Items.Add(CreateItem(KnownImageIds.ListMembers, "Find Members of " + et.Name + et.GetParameterString() + "...", () => FindMembers(et, _SemanticContext)));
				if (type.IsStatic == false) {
					Items.Add(CreateItem(KnownImageIds.ExtensionMethod, "Find Extensions for " + type.Name + type.GetParameterString() + "...", () => FindExtensionMethods(type, _SemanticContext)));
				}
				if (et.ContainingAssembly.GetSourceType() != AssemblySource.Metadata) {
					Items.Add(CreateItem(KnownImageIds.GoToDeclaration, "Go to " + et.Name + et.GetParameterString(), () => et.GoToSource()));
				}
			}
		}

		static void FindCallers(ISymbol symbol, SemanticContext context) {
			var callers = symbol.FindCallers(context.Document.Project);
			if (callers == null) {
				return;
			}
			var m = new SymbolMenu(context);
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(WpfHelper.MemberNameFormat), true)
				.Append(" callers");
			var containerType = symbol.ContainingType;
			foreach (var caller in callers) {
				var s = caller.CallingSymbol;
				var i = m.Menu.Add(s, false);
				i.Location = caller.Locations.FirstOrDefault();
				if (s.ContainingType != containerType) {
					i.Hint = s.ContainingType.ToDisplayString(WpfHelper.MemberNameFormat);
				}
			}
			m.Show();
		}

		static void FindCallersExt(ISymbol symbol, SemanticContext context) {
			var callers = SyncHelper.RunSync(()=> symbol.FindCallersAsync(context.Document.Project));
			if (callers == null) {
				return;
			}
			var m = new SymbolMenu(context);
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(WpfHelper.MemberNameFormat), true)
				.Append(" callers");
			var containerType = symbol.ContainingType;
			foreach (var caller in callers) {
				var s = caller.Key;
				var i = m.Menu.Add(s, false);
				i.Location = caller.Value.FirstOrDefault().Location;
				if (s.ContainingType != containerType) {
					i.Hint = (s.ContainingType ?? s).ToDisplayString(WpfHelper.MemberNameFormat);
				}
			}
			m.Show();
		}

		static void FindDerivedClasses(ISymbol symbol, SemanticContext context) {
			var classes = SyncHelper.RunSync(() => SymbolFinder.FindDerivedClassesAsync(symbol as INamedTypeSymbol, context.Document.Project.Solution, null, default)).ToList();
			ShowSymbolMenuForResult(symbol, context, classes, " derived classes", false);
		}

		static void FindOverrides(ISymbol symbol, SemanticContext context) {
			var m = new SymbolMenu(context);
			int c = 0;
			foreach (var ov in SyncHelper.RunSync(() => SymbolFinder.FindOverridesAsync(symbol, context.Document.Project.Solution, null, default))) {
				m.Menu.Add(ov, ov.ContainingType);
				++c;
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(WpfHelper.MemberNameFormat), true)
				.Append(" overrides: ")
				.Append(c.ToString());
			m.Show();
		}

		static void FindImplementations(ISymbol symbol, SemanticContext context) {
			var implementations = new List<ISymbol>(SyncHelper.RunSync(() => SymbolFinder.FindImplementationsAsync(symbol, context.Document.Project.Solution, null, default)));
			implementations.Sort((a, b) => a.Name.CompareTo(b.Name));
			var m = new SymbolMenu(context);
			if (symbol.Kind == SymbolKind.NamedType) {
				foreach (var impl in implementations) {
					m.Menu.Add(impl, false);
				}
			}
			else {
				foreach (var impl in implementations) {
					m.Menu.Add(impl, impl.ContainingSymbol);
				}
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(WpfHelper.MemberNameFormat), true)
				.Append(" implementations: ")
				.Append(implementations.Count.ToString());
			m.Show();
		}

		static void FindMembers(ISymbol symbol, SemanticContext context) {
			var m = new SymbolMenu(context, symbol.Kind == SymbolKind.Namespace ? SymbolListType.TypeList : SymbolListType.None);
			var (count, inherited) = m.Menu.AddSymbolMembers(symbol);
			if (m.Menu.IconProvider == null) {
				m.Menu.ExtIconProvider = s => GetExtIcons(s.Symbol);
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(WpfHelper.MemberNameFormat), true)
				.Append($" members: {count} ({inherited} inherited)");
			m.Show();
		}

		static void FindInstanceAsParameter(ISymbol symbol, SemanticContext context) {
			var members = SyncHelper.RunSync(() => (symbol as ITypeSymbol).FindInstanceAsParameterAsync(context.Document.Project, default));
			ShowSymbolMenuForResult(symbol, context, members, " as parameter", true);
		}

		static void FindInstanceProducer(ISymbol symbol, SemanticContext context) {
			var members = SyncHelper.RunSync(() => (symbol as ITypeSymbol).FindSymbolInstanceProducerAsync(context.Document.Project, default));
			ShowSymbolMenuForResult(symbol, context, members, " producers", true);
		}

		static void FindExtensionMethods(ISymbol symbol, SemanticContext context) {
			var members = SyncHelper.RunSync(() => (symbol as ITypeSymbol).FindExtensionMethodsAsync(context.Document.Project, default));
			ShowSymbolMenuForResult(symbol, context, members, " extensions", true);
		}

		void FindReferencedDocuments() {
			var m = new SymbolMenu(_SemanticContext);
			var c = 0;
			foreach (var member in _Node.FindRelatedTypes(_SemanticContext.SemanticModel, default)) {
				m.Menu.Add(member, false);
				++c;
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(_Symbol.GetImageId()))
				.Append(_Symbol.ToDisplayString(WpfHelper.MemberNameFormat), true)
				.Append(" referenced documents: ")
				.Append(c.ToString());
			m.Show();
		}

		static void FindSymbolWithName(ISymbol symbol, SemanticContext context) {
			var result = context.SemanticModel.Compilation.FindDeclarationMatchName(symbol.Name, Keyboard.Modifiers == ModifierKeys.Control, true, default);
			ShowSymbolMenuForResult(symbol, context, new List<ISymbol>(result), " name alike", true);
		}

		internal static void ShowLocations(ISymbol symbol, SemanticContext context) {
			var m = new SymbolMenu(context, SymbolListType.Locations);
			var locs = new SortedList<(string, string, int), Location>();
			foreach (var item in symbol.DeclaringSyntaxReferences) {
				locs.Add((System.IO.Path.GetDirectoryName(item.SyntaxTree.FilePath), System.IO.Path.GetFileName(item.SyntaxTree.FilePath), item.Span.Start), item.ToLocation());
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(WpfHelper.MemberNameFormat), true)
				.Append(" source locations: ")
				.Append(locs.Count);
			foreach (var loc in locs) {
				m.Menu.Add(loc.Value);
			}
			foreach (var loc in symbol.Locations.RemoveAll(l => l.IsInMetadata == false).Sort((x, y) => String.CompareOrdinal(x.MetadataModule.Name, y.MetadataModule.Name))) {
				m.Menu.Add(loc);
			}
			m.Show();
		}
		static StackPanel GetExtIcons(ISymbol symbol) {
			StackPanel icons = null;
			switch (symbol.Kind) {
				case SymbolKind.Method:
					var ms = symbol as IMethodSymbol;
					if (ms.IsAsync) {
						icons = AddIcon(icons, KnownImageIds.DynamicGroup);
					}
					if (ms.IsGenericMethod) {
						icons = AddIcon(icons, KnownImageIds.MarkupXML);
					}
					if (ms.IsExtensionMethod) {
						return AddIcon(icons, KnownImageIds.ExtensionMethod);
					}
					break;
				case SymbolKind.NamedType:
					var mt = symbol as INamedTypeSymbol;
					if (mt.IsGenericType) {
						icons = AddIcon(icons, KnownImageIds.MarkupXML);
					}
					if (mt.TypeKind == TypeKind.Class) {
						if (mt.IsSealed && mt.IsStatic == false) {
							icons = AddIcon(icons, KnownImageIds.ClassSealed);
						}
						else if (mt.IsAbstract) {
							icons = AddIcon(icons, KnownImageIds.AbstractClass);
						}
					}
					break;
				case SymbolKind.Field:
					var f = symbol as IFieldSymbol;
					if (f.IsConst) {
						return null;
					}
					if (f.IsReadOnly) {
						icons = AddIcon(icons, KnownImageIds.EncapsulateField);
					}
					else if (f.IsVolatile) {
						icons = AddIcon(icons, KnownImageIds.ModifyField);
					}
					break;
				case SymbolKind.Namespace:
					return null;
			}
			if (symbol.IsStatic) {
				icons = AddIcon(icons, KnownImageIds.Link);
			}
			return icons;

			StackPanel AddIcon(StackPanel container, int imageId) {
				if (container == null) {
					container = new StackPanel { Orientation = Orientation.Horizontal };
				}
				container.Children.Add(ThemeHelper.GetImage(imageId));
				return container;
			}
		}
		static void ShowSymbolMenuForResult<TSymbol>(ISymbol source, SemanticContext context, List<TSymbol> members, string suffix, bool groupByType) where TSymbol : ISymbol {
			members.Sort(CodeAnalysisHelper.CompareSymbol);
			var m = new SymbolMenu(context);
			m.Title.SetGlyph(ThemeHelper.GetImage(source.GetImageId()))
				.Append(source.ToDisplayString(WpfHelper.MemberNameFormat), true)
				.Append(suffix);
			INamedTypeSymbol containingType = null;
			foreach (var item in members) {
				if (groupByType && item.ContainingType != containingType) {
					m.Menu.Add(containingType = item.ContainingType, false)
						.Type = SymbolItemType.Container;
				}
				m.Menu.Add(item, false);
			}
			m.Menu.ExtIconProvider = s => GetExtIcons(s.Symbol);
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
			_Container = semanticContext.View.Properties.GetOrCreateSingletonProperty(() => new ExternalAdornment(semanticContext.View));
			Menu = new SymbolList(semanticContext) {
				Container = _Container,
				ContainerType = listType
			};
			Menu.Header = _HeaderPanel = new StackPanel {
				Margin = WpfHelper.MenuItemMargin,
				Children = {
						(Title = new ThemedMenuText { TextAlignment = TextAlignment.Center, Padding = WpfHelper.SmallMargin }),
						(FilterBox = new SymbolFilterBox(Menu)),
						new Separator()
					}
			};
			Menu.HeaderButtons = new StackPanel {
				Orientation = Orientation.Horizontal,
				Children = {
					new ThemedButton(ThemeHelper.GetImage(KnownImageIds.Unpin), "Pin", TogglePinButton),
					new ThemedButton(KnownImageIds.Close, "Close", () => {
						_Container.Children.Remove(Menu);
						_Container.FocusOnTextView();
					})
				}
			};
			Menu.MouseLeftButtonUp += MenuItemSelect;
			_Container.MakeDraggable(Menu);
		}

		void TogglePinButton(object sender, RoutedEventArgs e) {
			((ThemedButton)e.Source).Content = ThemeHelper.GetImage((Menu.IsPinned = !Menu.IsPinned) ? KnownImageIds.Pin : KnownImageIds.Unpin);
		}

		public void Show() {
			ShowMenu();
			UpdateNumbers();
			FilterBox.FocusTextBox();
		}

		void ShowMenu() {
			//if (_Bar._SymbolList != menu) {
			//	_Bar._SymbolListContainer.Children.Clear();
			_Container.Children.Add(Menu);
			//	_Bar._SymbolList = menu;
			//}
			Menu.ItemsControlMaxHeight = _Container.ActualHeight / 2;
			Menu.RefreshItemsSource();
			Menu.ScrollToSelectedItem();
			Menu.PreviewKeyUp -= OnMenuKeyUp;
			Menu.PreviewKeyUp += OnMenuKeyUp;
			if (Menu.Symbols.Count > 100) {
				ScrollViewer.SetCanContentScroll(Menu, true);
			}

			var p = Mouse.GetPosition(_Container);
			Canvas.SetLeft(Menu, p.X);
			Canvas.SetTop(Menu, p.Y);
			//var point = visual.TransformToVisual(View.VisualElement).Transform(new Point());
			//Canvas.SetLeft(Menu, point.X + visual.RenderSize.Width);
			//Canvas.SetTop(Menu, point.Y);
		}
		void UpdateNumbers() {
			FilterBox.UpdateNumbers(Menu.Symbols.Select(i => i.Symbol));
		}

		void MenuItemSelect(object sender, MouseButtonEventArgs e) {
			var menu = sender as ItemList;
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
