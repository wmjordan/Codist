using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace Codist.Controls
{
	sealed class CSharpSymbolContextMenu : ContextMenu
	{
		SyntaxNode _Node;
		ISymbol _Symbol;
		readonly SemanticContext _SemanticContext;
		readonly bool _IsVsProject;

		public CSharpSymbolContextMenu(SemanticContext semanticContext, bool isVsProject) {
			Resources = SharedDictionaryManager.ContextMenu;
			Foreground = ThemeHelper.ToolWindowTextBrush;
			this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
			_SemanticContext = semanticContext;
			_IsVsProject = isVsProject;
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

		public void AddAnalysisCommands() {
			switch (_Symbol.Kind) {
				case SymbolKind.Method:
				case SymbolKind.Property:
				case SymbolKind.Event:
					Items.Add(CreateItem(KnownImageIds.ShowCallerGraph, "Find Callers...", () => FindCallers(_Symbol)));
					if (_Symbol.MayHaveOverride()) {
						Items.Add(CreateItem(KnownImageIds.OverloadBehavior, "Find Overrides...", () => FindOverrides(_Symbol)));
					}
					var st = _Symbol.ContainingType;
					if (st != null && st.TypeKind == TypeKind.Interface) {
						Items.Add(CreateItem(KnownImageIds.ImplementInterface, "Find Implementations...", () => FindImplementations(_Symbol)));
					}
					if (_Symbol.Kind != SymbolKind.Event) {
						CreateCommandsForReturnTypeCommand();
					}
					if (_Symbol.Kind == SymbolKind.Method
						&& (_Symbol as IMethodSymbol).MethodKind == MethodKind.Constructor
						&& st.SpecialType == SpecialType.None) {
						CreateInstanceCommandsForType(st);
					}
					break;
				case SymbolKind.Field:
				case SymbolKind.Local:
				case SymbolKind.Parameter:
					CreateCommandsForReturnTypeCommand();
					break;
				case SymbolKind.NamedType:
					CreateCommandForNamedType(_Symbol as INamedTypeSymbol);
					break;
				case SymbolKind.Namespace:
					Items.Add(CreateItem(KnownImageIds.ListMembers, "Find Members...", () => FindMembers(_Symbol)));
					break;
			}
			if (_Node != null && _Node.IsDeclaration() && _Symbol.Kind != SymbolKind.Namespace) {
				Items.Add(CreateItem(KnownImageIds.ShowReferencedElements, "Find Referenced Symbols...", () => FindReferencedSymbols(_Symbol)));
			}
			//Items.Add(CreateCommandMenu("Find references...", KnownImageIds.ReferencedDimension, _Symbol, "No reference found", FindReferences));
			Items.Add(CreateItem(KnownImageIds.FindSymbol, "Find Symbol with Name " + _Symbol.Name + "...", () => FindSymbolWithName(_Symbol)));
			Items.Add(CreateItem(KnownImageIds.ReferencedDimension, "Find All References", FindAllReferences));
		}

		public void AddGoToAnyCommands() {
			Items.Add(CreateItem(KnownImageIds.ListMembers, "Go to Member", GoToMember));
			Items.Add(CreateItem(KnownImageIds.Type, "Go to Type", GoToType));
			Items.Add(CreateItem(KnownImageIds.FindSymbol, "Go to Symbol", GoToSymbol));
		}

		MenuItem CreateItem(int imageId, string title) {
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
						Items.Add(CreateItem(KnownImageIds.ShowCallerGraph, "Find Callers...", () => FindCallers(symbol)));
					}
				}
				else if (t.InstanceConstructors.Length > 0) {
					Items.Add(CreateItem(KnownImageIds.ShowCallerGraph, "Find Constructor Callers...", () => FindCallers(t)));
				}
			}
			Items.Add(CreateItem(KnownImageIds.ListMembers, "Find Members...", () => FindMembers(t)));
			if (t.IsStatic) {
				return;
			}
			if (t.IsSealed == false) {
				if (t.TypeKind == TypeKind.Class) {
					Items.Add(CreateItem(KnownImageIds.NewClass, "Find Derived Classes...", () => FindDerivedClasses(t)));
				}
				else if (t.TypeKind == TypeKind.Interface) {
					Items.Add(CreateItem(KnownImageIds.ImplementInterface, "Find Implementations...", () => FindImplementations(t)));
				}
			}
			Items.Add(CreateItem(KnownImageIds.ExtensionMethod, "Find Extensions...", () => FindExtensionMethods(t)));
			if (t.SpecialType == SpecialType.None) {
				CreateInstanceCommandsForType(t);
			}
		}

		void CreateInstanceCommandsForType(INamedTypeSymbol t) {
			Items.Add(CreateItem(KnownImageIds.NewItem, "Find Instance Producer...", () => FindInstanceProducer(t)));
			Items.Add(CreateItem(KnownImageIds.Parameter, "Find Instance as Parameter...", () => FindInstanceAsParameter(t)));
		}

		void CreateCommandsForReturnTypeCommand() {
			var type = _Symbol.GetReturnType();
			if (type != null && type.SpecialType == SpecialType.None) {
				Items.Add(CreateItem(KnownImageIds.ListMembers, "Find Members of " + type.Name + type.GetParameterString() + "...", () => FindMembers(type)));
				if (type.IsStatic == false) {
					Items.Add(CreateItem(KnownImageIds.ExtensionMethod, "Find Extensions for " + type.Name + type.GetParameterString() + "...", () => FindExtensionMethods(type)));
				}
				if (type.ContainingAssembly.GetSourceType() != AssemblySource.Metadata) {
					Items.Add(CreateItem(KnownImageIds.GoToDeclaration, "Go to " + type.Name + type.GetParameterString(), () => type.GoToSource()));
				}
			}
		}

		void FindCallers(ISymbol symbol) {
			var doc = _SemanticContext.Document;
			var docs = System.Collections.Immutable.ImmutableHashSet.CreateRange(doc.Project.GetRelatedProjectDocuments());
			List<SymbolCallerInfo> callers;
			switch (symbol.Kind) {
				case SymbolKind.Method:
				case SymbolKind.Property:
				case SymbolKind.Event:
					callers = ThreadHelper.JoinableTaskFactory.Run(() => SymbolFinder.FindCallersAsync(symbol, doc.Project.Solution, docs, default)).ToList();
					break;
				case SymbolKind.NamedType:
					var tempResults = new HashSet<SymbolCallerInfo>(SymbolCallerInfoComparer.Instance);
					ThreadHelper.JoinableTaskFactory.Run(async () => {
						foreach (var item in (symbol as INamedTypeSymbol).InstanceConstructors) {
							foreach (var c in await SymbolFinder.FindCallersAsync(item, doc.Project.Solution, docs, default)) {
								tempResults.Add(c);
							}
						}
					});
					(callers = new List<SymbolCallerInfo>(tempResults.Count)).AddRange(tempResults);
					break;
				default: return;
			}
			callers.Sort((a, b) => CodeAnalysisHelper.CompareSymbol(a.CallingSymbol, b.CallingSymbol));
			var m = new SymbolMenu(_SemanticContext);
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

		void FindDerivedClasses(ISymbol symbol) {
			var classes = ThreadHelper.JoinableTaskFactory.Run(() => SymbolFinder.FindDerivedClassesAsync(symbol as INamedTypeSymbol, _SemanticContext.Document.Project.Solution, null, default)).Cast<ISymbol>().ToList();
			classes.Sort((a, b) => a.Name.CompareTo(b.Name));
			ShowSymbolMenuForResult(symbol, classes, " derived classes", false);
		}

		void FindOverrides(ISymbol symbol) {
			var m = new SymbolMenu(_SemanticContext);
			int c = 0;
			foreach (var ov in ThreadHelper.JoinableTaskFactory.Run(() => SymbolFinder.FindOverridesAsync(symbol, _SemanticContext.Document.Project.Solution, null, default))) {
				m.Menu.Add(ov, ov.ContainingType);
				++c;
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(WpfHelper.MemberNameFormat), true)
				.Append(" overrides: ")
				.Append(c.ToString());
			m.Show();
		}

		void FindImplementations(ISymbol symbol) {
			var implementations = new List<ISymbol>(ThreadHelper.JoinableTaskFactory.Run(() => SymbolFinder.FindImplementationsAsync(symbol, _SemanticContext.Document.Project.Solution, null, default)));
			implementations.Sort((a, b) => a.Name.CompareTo(b.Name));
			var m = new SymbolMenu(_SemanticContext);
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

		void FindMembers(ISymbol symbol) {
			var m = new SymbolMenu(_SemanticContext, symbol.Kind == SymbolKind.Namespace ? SymbolListType.TypeList : SymbolListType.None);
			var (count, inherited) = m.Menu.AddSymbolMembers(symbol, _IsVsProject);
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(WpfHelper.MemberNameFormat), true)
				.Append($" members: {count} ({inherited} inherited)");
			m.Show();
		}

		void FindInstanceAsParameter(ISymbol symbol) {
			ThreadHelper.JoinableTaskFactory.Run(async () => {
				var members = await (symbol as ITypeSymbol).FindInstanceAsParameterAsync(_SemanticContext.Document.Project, default);
				ShowSymbolMenuForResult(symbol, members, " as parameter", true);
			});
		}

		void FindInstanceProducer(ISymbol symbol) {
			ThreadHelper.JoinableTaskFactory.Run(async () => {
				var members = await (symbol as ITypeSymbol).FindSymbolInstanceProducerAsync(_SemanticContext.Document.Project, default);
				ShowSymbolMenuForResult(symbol, members, " producers", true);
			});
		}

		void FindExtensionMethods(ISymbol symbol) {
			ThreadHelper.JoinableTaskFactory.Run(async () => {
				var members = await (symbol as ITypeSymbol).FindExtensionMethodsAsync(_SemanticContext.Document.Project, default);
				ShowSymbolMenuForResult(symbol, members, " extensions", true);
			});
		}

		void FindReferencedSymbols(ISymbol symbol) {
			var m = new SymbolMenu(_SemanticContext);
			var c = 0;
			foreach (var item in _SemanticContext.Node.FindReferencingSymbols(_SemanticContext.SemanticModel, true)) {
				var member = item.Key;
				var i = m.Menu.Add(member, true);
				if (item.Value > 1) {
					i.Hint = "* " + item.Value.ToString();
				}
				++c;
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(WpfHelper.MemberNameFormat), true)
				.Append(" referenced members: ")
				.Append(c.ToString());
			m.Show();
		}

		void FindSymbolWithName(ISymbol symbol) {
			var result = _SemanticContext.SemanticModel.Compilation.FindDeclarationMatchName(symbol.Name, Keyboard.Modifiers == ModifierKeys.Control, true, default);
			ShowSymbolMenuForResult(symbol, new List<ISymbol>(result), " name alike", true);
		}

		void ShowSymbolMenuForResult(ISymbol source, List<ISymbol> members, string suffix, bool groupByType) {
			members.Sort(CodeAnalysisHelper.CompareSymbol);
			var m = new SymbolMenu(_SemanticContext);
			m.Title.SetGlyph(ThemeHelper.GetImage(source.GetImageId()))
				.Append(source.ToDisplayString(WpfHelper.MemberNameFormat), true)
				.Append(suffix);
			ITypeSymbol containingType = null;
			foreach (var item in members) {
				if (groupByType && item.ContainingType != containingType) {
					m.Menu.Add((containingType = item.ContainingType), false)
						.Type = SymbolItemType.Container;
				}
				m.Menu.Add(item, false);
			}
			m.Show();
		}

		#region Menu event handlers

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

		sealed class SymbolCallerInfoComparer : IEqualityComparer<SymbolCallerInfo>
		{
			internal static readonly SymbolCallerInfoComparer Instance = new SymbolCallerInfoComparer();

			public bool Equals(SymbolCallerInfo x, SymbolCallerInfo y) {
				return x.CallingSymbol == y.CallingSymbol;
			}

			public int GetHashCode(SymbolCallerInfo obj) {
				return obj.CallingSymbol.GetHashCode();
			}
		}
	}

	sealed class SymbolMenu
	{
		readonly ExternalAdornment _Container;

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
			Menu.Header = new StackPanel {
				Margin = WpfHelper.MenuItemMargin,
				Children = {
						(Title = new ThemedMenuText { TextAlignment = TextAlignment.Center, Padding = WpfHelper.SmallMargin }),
						(FilterBox = new SymbolFilterBox(Menu)),
						new Separator()
					}
			};
			Menu.HeaderButtons = new ThemedButton(KnownImageIds.Close, "Close", () => {
				_Container.Children.Remove(Menu);
			});
			Menu.ReferenceCrispImageBackground(EnvironmentColors.MainWindowActiveCaptionColorKey);
			Menu.MouseLeftButtonUp += MenuItemSelect;
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
			var menu = sender as SymbolList;
			if (menu.SelectedIndex == -1 || (e.OriginalSource as DependencyObject)?.GetParent<ListBoxItem>() == null) {
				return;
			}
			_Container.FocusOnTextView();
			(menu.SelectedItem as SymbolItem)?.GoToSource();
		}

		void OnMenuKeyUp(object sender, KeyEventArgs e) {
			if (e.Key == Key.Escape) {
				_Container.Children.Remove(Menu);
				e.Handled = true;
			}
		}
	}
}
