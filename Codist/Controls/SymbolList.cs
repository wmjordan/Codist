using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using CLR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using GDI = System.Drawing;
using R = Codist.Properties.Resources;
using WPF = System.Windows.Media;

namespace Codist.Controls
{
	class SymbolList : VirtualList, ISymbolFilterable, INotifyCollectionChanged
	{
		static readonly ExtensionProperty<MenuItem, ValueTuple<SymbolItem, Action<SymbolItem>>> __CommandAction = ExtensionProperty<MenuItem, ValueTuple<SymbolItem, Action<SymbolItem>>>.Register("CommandAction");
		static readonly ExtensionProperty<ListBoxItem, WeakReference<SymbolItem>> __ListBoxItemContent = ExtensionProperty<ListBoxItem, WeakReference<SymbolItem>>.Register("ListBoxItemContent");
		static readonly ContextMenu __ListItemDummyContextMenu = new ContextMenu();
		Predicate<object> _Filter;
		readonly List<SymbolItem> _Symbols;
		CancellationTokenSource _ToolTipCancellationTokenSource, _ContextMenuCancellationTokenSource;

		public SymbolList(SemanticContext semanticContext) {
			_Symbols = new List<SymbolItem>();
			FilteredItems = new ListCollectionView(_Symbols);
			SemanticContext = semanticContext;
			Resources = SharedDictionaryManager.SymbolList;
			// We have to use code to set ItemContainerStyle for event handlers are not supported in XAML DataTemplate
			ItemContainerStyle = new Style {
				TargetType = typeof(ListBoxItem),
				Setters = {
					new Setter { Property = OverridesDefaultStyleProperty, Value = true },
					new Setter { Property = SnapsToDevicePixelsProperty, Value = true },
					new Setter { Property = CursorProperty, Value = Cursors.Arrow },
					new Setter { Property = HeightProperty, Value = 22d },
					new Setter { Property = TemplateProperty, Value = Resources["ListBoxItemTemplate"] as ControlTemplate },
					#region context menu for list items
					new Setter { Property = ContextMenuProperty, Value = __ListItemDummyContextMenu },
					new EventSetter { Event = ContextMenuOpeningEvent, Handler = new ContextMenuEventHandler(OnListItemContextMenuOpening) },
					#endregion
					#region tool tip for list items
					new Setter { Property = ToolTipProperty, Value = String.Empty },
					new Setter { Property = ToolTipService.InitialShowDelayProperty, Value = Config.Instance.QuickInfo.DelayDisplay },
					new Setter { Property = ToolTipService.ShowDurationProperty, Value = 15000 },
					new Setter { Property = ToolTipService.PlacementTargetProperty, Value = this },
					new Setter { Property = ToolTipService.PlacementProperty, Value = PlacementMode.Bottom },
					new EventSetter { Event = ToolTipOpeningEvent, Handler = new ToolTipEventHandler(OnListItemToolTipOpening) },
					#endregion
				},
			};
			PreviewKeyDown += SymbolList_PreviewKeyDown;
		}

		public SemanticContext SemanticContext { get; private set; }
		public IReadOnlyList<SymbolItem> Symbols => _Symbols;
		public SymbolListType ContainerType { get; set; }
		public Func<SymbolItem, UIElement> IconProvider { get; set; }
		public Func<SymbolItem, UIElement> ExtIconProvider { get; set; }
		public SymbolItem SelectedSymbolItem => SelectedItem as SymbolItem;
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		public bool IsPinned { get; set; }

		public SymbolItem Add(SyntaxNode node) {
			var item = new SymbolItem(node, this);
			_Symbols.Add(item);
			return item;
		}
		public SymbolItem Add(ISymbol symbol, bool includeContainerType) {
			var item = new SymbolItem(symbol, this, includeContainerType);
			_Symbols.Add(item);
			return item;
		}
		public SymbolItem Add(ISymbol symbol, ISymbol containerType) {
			var item = new SymbolItem(symbol, this, containerType);
			_Symbols.Add(item);
			return item;
		}
		public SymbolItem Add(Location location) {
			var item = new SymbolItem(location, this);
			_Symbols.Add(item);
			return item;
		}
		public SymbolItem Add(SymbolItem item) {
			_Symbols.Add(item);
			return item;
		}
		public void AddRange(IEnumerable<SymbolItem> items) {
			_Symbols.AddRange(items);
		}

		public void ClearSymbols() {
			foreach (var item in _Symbols) {
				item.Release();
			}
			_Symbols.Clear();
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}
		public void RefreshItemsSource(bool force = false) {
			if (force) {
				ItemsSource = null;
			}
			if (_Filter != null) {
				FilteredItems.Filter = _Filter;
				ItemsSource = FilteredItems;
			}
			else {
				ItemsSource = _Symbols;
			}
			if (SelectedIndex == -1 && HasItems) {
				SelectedIndex = 0;
			}
		}

		void SymbolList_PreviewKeyDown(object sender, KeyEventArgs e) {
			if (e.OriginalSource is TextBox == false || e.Handled) {
				return;
			}
			if (e.Key == Key.Enter) {
				System.Collections.ObjectModel.ReadOnlyCollection<object> items;
				var item = SelectedIndex == -1 && HasItems && (items = ItemContainerGenerator.Items).Count != 0
					? items[0] as SymbolItem
					: SelectedItem as SymbolItem;
				if (item != null) {
					GoToSourceAsync(item);
				}
				e.Handled = true;
			}

			async void GoToSourceAsync(SymbolItem i) {
				try {
					await i.GoToSourceAsync();
				}
				catch (OperationCanceledException) {
				}
			}
		}

		#region Analysis commands
		internal void AddNamespaceItems(ISymbol[] symbols, ISymbol highlight) {
			var c = highlight != null ? CodeAnalysisHelper.GetSpecificSymbolComparer(highlight) : null;
			foreach (var item in symbols) {
				var s = Add(new SymbolItem(item, this, false));
				if (c != null && c(item)) {
					SelectedItem = s;
					c = null;
				}
			}
		}

		internal void SetupForSpecialTypes(ITypeSymbol type) {
			INamespaceSymbol typeNamespace;
			if (type == null) {
				return;
			}
			switch (type.TypeKind) {
				case TypeKind.Dynamic:
					return;
				case TypeKind.Enum:
					if (type.GetAttributes().Any(a => a.AttributeClass.MatchTypeName(nameof(FlagsAttribute), "System"))) {
						ContainerType = SymbolListType.EnumFlags;
						return;
					}
					break;
			}
			typeNamespace = type.ContainingNamespace;
			if (typeNamespace.IsExplicitNamespace() == false) {
				return;
			}
			string typeName = type.Name;
			switch (typeNamespace.ToString()) {
				case "System.Drawing":
					switch (typeName) {
						case nameof(GDI.SystemBrushes):
						case nameof(GDI.SystemPens):
						case nameof(GDI.SystemColors):
							SetupListForSystemColors();
							return;
						case nameof(GDI.Color):
						case nameof(GDI.Brushes):
						case nameof(GDI.Pens):
							SetupListForColors();
							return;
						case nameof(GDI.KnownColor):
							SetupListForKnownColors();
							return;
					}
					return;
				case "System.Windows":
					if (typeName == nameof(SystemColors)) {
						SetupListForSystemColors();
					}
					return;
				case "System.Windows.Media":
					switch (typeName) {
						case nameof(WPF.Colors):
						case nameof(WPF.Brushes):
							SetupListForColors(); return;
					}
					return;
				case "Microsoft.VisualStudio.PlatformUI":
					switch (typeName) {
						case nameof(EnvironmentColors): SetupListForVsUIColors(EnvironmentColors.Category); return;
						case nameof(CommonControlsColors): SetupListForVsUIColors(CommonControlsColors.Category); return;
						case nameof(CommonDocumentColors): SetupListForVsUIColors(CommonDocumentColors.Category); return;
						case nameof(HeaderColors): SetupListForVsUIColors(HeaderColors.Category); return;
						case nameof(InfoBarColors): SetupListForVsUIColors(InfoBarColors.Category); return;
						case nameof(ProgressBarColors): SetupListForVsUIColors(ProgressBarColors.Category); return;
						case nameof(SearchControlColors): SetupListForVsUIColors(SearchControlColors.Category); return;
						case nameof(StartPageColors): SetupListForVsUIColors(StartPageColors.Category); return;
						case nameof(ThemedDialogColors): SetupListForVsUIColors(ThemedDialogColors.Category); return;
						case nameof(TreeViewColors): SetupListForVsUIColors(TreeViewColors.Category); return;
					}
					return;
				case "Microsoft.VisualStudio.Shell":
					switch (typeName) {
						case nameof(VsColors): SetupListForVsResourceColors(); return;
						case nameof(VsBrushes): SetupListForVsResourceBrushes(); return;
					}
					return;
				case "Microsoft.VisualStudio.Imaging":
					switch (typeName) {
						case nameof(KnownImageIds):
							SetupListForKnownImageIds();
							break;
						case nameof(KnownMonikers):
							SetupListForKnownMonikers();
							break;
					}
					return;
			}
		}

		static bool IsStaticProperty(SymbolItem s) {
			return s.Symbol is IPropertySymbol ps && ps.IsStatic;
		}
		void SetupListForVsUIColors(Guid category) {
			ContainerType = SymbolListType.PredefinedColors;
			IconProvider = s => IsStaticProperty(s) ? GetColorPreviewIcon(ColorHelper.GetVsThemeBrush(category, s.Symbol.Name)) : null;
		}
		void SetupListForVsResourceColors() {
			ContainerType = SymbolListType.PredefinedColors;
			IconProvider = s => IsStaticProperty(s) ? GetColorPreviewIcon(ColorHelper.GetVsResourceColor(s.Symbol.Name)) : null;
		}
		void SetupListForVsResourceBrushes() {
			ContainerType = SymbolListType.PredefinedColors;
			IconProvider = s => IsStaticProperty(s) ? GetColorPreviewIcon(ColorHelper.GetVsResourceBrush(s.Symbol.Name)) : null;
		}
		void SetupListForSystemColors() {
			ContainerType = SymbolListType.PredefinedColors;
			IconProvider = s => IsStaticProperty(s) ? GetColorPreviewIcon(ColorHelper.GetSystemBrush(s.Symbol.Name)) : null;
		}
		void SetupListForColors() {
			ContainerType = SymbolListType.PredefinedColors;
			IconProvider = s => IsStaticProperty(s) ? GetColorPreviewIcon(ColorHelper.GetBrush(s.Symbol.Name)) : null;
		}
		void SetupListForKnownColors() {
			ContainerType = SymbolListType.PredefinedColors;
			IconProvider = s => ((s.Symbol is IFieldSymbol f) && f.IsStatic)
				? GetColorPreviewIcon(ColorHelper.GetBrush(s.Symbol.Name) ?? ColorHelper.GetSystemBrush(s.Symbol.Name))
				: null;
		}
		void SetupListForKnownImageIds() {
			ContainerType = SymbolListType.VsKnownImage;
			IconProvider = s => {
				return s.Symbol is IFieldSymbol f
					&& f.HasConstantValue
					&& f.Type.SpecialType == SpecialType.System_Int32
					&& f.DeclaredAccessibility == Accessibility.Public
					? VsImageHelper.GetImage((int)f.ConstantValue)
					: null;
			};
		}
		void SetupListForKnownMonikers() {
			ContainerType = SymbolListType.VsKnownImage;
			IconProvider = s => {
				return s.Symbol is IPropertySymbol p && p.IsStatic
					? VsImageHelper.GetImage(p.Name)
					: null;
			};
		}
		static Border GetColorPreviewIcon(WPF.Brush brush) {
			return brush == null ? null : new Border {
				BorderThickness = WpfHelper.TinyMargin,
				BorderBrush = ThemeHelper.MenuTextBrush,
				SnapsToDevicePixels = true,
				Background = brush,
				Height = VsImageHelper.DefaultIconSize,
				Width = VsImageHelper.DefaultIconSize,
			};
		}
		#endregion

		#region Context menu
		[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
		async void OnListItemContextMenuOpening(object sender, ContextMenuEventArgs e) {
			var sc = SemanticContext;
			if (sc == null) {
				return;
			}

			var item = SelectedSymbolItem;
			try {
				var ct = SyncHelper.CancelAndRetainToken(ref _ContextMenuCancellationTokenSource);
				await sc.UpdateAsync(ct);
				if (item != null) {
					if (item.Symbol != null) {
						await item.RefreshSymbolAsync(ct);
					}
					else if (item.SyntaxNode != null) {
						await item.SetSymbolToSyntaxNodeAsync(ct);
					}
				}
			}
			catch (OperationCanceledException) {
				// ignore
			}
			catch (Exception ex) {
				MessageWindow.Error(ex, null, null, this);
				e.Handled = true;
				return;
			}

			if (item.Symbol == null && item.SyntaxNode == null) {
				e.Handled = true;
				return;
			}
			CSharpSymbolContextMenu m;
			((ListBoxItem)sender).ContextMenu = m = new CSharpSymbolContextMenu(item.Symbol, item.SyntaxNode, SemanticContext) {
				Resources = SharedDictionaryManager.ContextMenu,
				Foreground = ThemeHelper.ToolWindowTextBrush,
				IsEnabled = true,
			};
			SetupContextMenu(m, item);
			m.AddTitleItem(item.SyntaxNode?.GetDeclarationSignature() ?? item.Symbol.GetOriginalName());
			m.IsOpen = true;
			e.Handled = true;
		}

		static void SetupContextMenu(CSharpSymbolContextMenu menu, SymbolItem item) {
			if (item.Symbol != null) {
				menu.AddAnalysisCommands();
				menu.Items.Add(new Separator());
				if (item.SyntaxNode == null && item.Symbol.HasSource()) {
					menu.AddSymbolNodeCommands();
				}
				else {
					menu.AddCopyAndSearchSymbolCommands();
				}
			}
			if (item.SyntaxNode != null) {
				menu.Items.Add(SetupMenuCommand(item, IconIds.SelectCode, R.CMD_SelectCode, s => {
					if (s.IsExternal) {
						s.SyntaxNode.SelectNode(true);
					}
					else {
						s.Container.SemanticContext.View.SelectNode(s.SyntaxNode, true);
					}
				}));
				//SetupMenuCommand(item, KnownImageIds.Copy, "Copy Code", s => Clipboard.SetText(s.SyntaxNode.ToFullString()));
				item.SetSymbolToSyntaxNode();
			}
		}

		static ThemedMenuItem SetupMenuCommand(SymbolItem item, int imageId, string title, Action<SymbolItem> action) {
			var mi = new ThemedMenuItem(imageId, title, (s, args) => {
				var (i, a) = __CommandAction.Get((MenuItem)s);
				a(i);
			});
			__CommandAction.Set(mi, (item, action));
			return mi;
		}
		#endregion

		#region Tool Tip
		[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
		async void OnListItemToolTipOpening(object sender, ToolTipEventArgs args) {
			var item = args.Source as ListBoxItem;
			try {
				if (item.Content is SymbolItem si
					&& (item.ToolTip is string s && s.Length == 0
						|| __ListBoxItemContent.Get(item).TryGetTarget(out var t) == false
						|| t != si)) {
					item.ToolTip = await CreateItemToolTipAsync(si);
					// note: ListBoxItem, the container of SymbolItem,
					//   may be reused and filled with other SymbolItem, while we scroll the SymbolList.
					//   Thus we use the extensive property to make sure the SymbolItem and ToolTip are a couple.
					__ListBoxItemContent.Set(item, new WeakReference<SymbolItem>(si));
				}
			}
			catch (OperationCanceledException) {
				// ignore
			}
			catch (Exception ex) {
				ex.Log();
			}
		}

		async Task<object> CreateItemToolTipAsync(SymbolItem item) {
			var sc = SemanticContext;
			CancellationToken ct;
			if (sc == null
				|| await sc.UpdateAsync(ct = SyncHelper.CancelAndRetainToken(ref _ToolTipCancellationTokenSource)) == false) {
				return null;
			}

			// the sequences of these conditions are important
			if (item.SyntaxNode != null) {
				return await CreateSyntaxNodeToolTipAsync(item, sc, ct);
			}
			if (item.Symbol != null) {
				return await CreateSymbolToolTipAsync(item, sc, ct);
			}
			if (item.Location != null) {
				return CreateLocationToolTip(item, sc);
			}
			return null;
		}

		async Task<object> CreateSyntaxNodeToolTipAsync(SymbolItem item, SemanticContext sc, CancellationToken ct) {
			if (item.Symbol != null) {
				await item.RefreshSymbolAsync(ct);
			}
			else {
				await item.SetSymbolToSyntaxNodeAsync(ct);
			}
			if (item.Symbol != null) {
				var tip = ToolTipHelper.CreateToolTip(item.Symbol, ContainerType == SymbolListType.NodeList, sc);
				if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.LineOfCode)) {
					tip.AddTextBlock()
						.Append(R.T_LineOfCode + (item.SyntaxNode.GetLineSpan().Length + 1).ToString());
				}
#if DEBUG
				tip.AddTextBlock().Append(item.SyntaxNode.GetQualifiedSignature());
#endif
				return tip;
			}
			return ((Microsoft.CodeAnalysis.CSharp.SyntaxKind)item.SyntaxNode.RawKind).GetSyntaxBrief();
		}

		async Task<object> CreateSymbolToolTipAsync(SymbolItem item, SemanticContext sc, CancellationToken ct) {
			await item.RefreshSymbolAsync(ct);
			var tip = ToolTipHelper.CreateToolTip(item.Symbol, false, sc);
			if (ContainerType == SymbolListType.SymbolReferrers && item.Location.IsInSource) {
				// append location info to tip
				ShowSourceReference(tip.AddTextBlock().Append(R.T_SourceReference).AppendLine(), item.Location);
			}
			return tip;
		}

		static ThemedToolTip CreateLocationToolTip(SymbolItem item, SemanticContext sc) {
			var l = item.Location;
			if (l.IsInSource) {
				var f = l.SourceTree.FilePath;
				return new ThemedToolTip(Path.GetFileName(f), String.Join(Environment.NewLine,
					R.T_Folder + Path.GetDirectoryName(f),
					R.T_Line + (l.GetLineSpan().StartLinePosition.Line + 1).ToString(),
					R.T_Project + sc.GetDocument(l.SourceTree)?.Project.Name
				));
			}
			return new ThemedToolTip(l.MetadataModule.Name, String.Join(Environment.NewLine,
				R.T_ContainingAssembly + l.MetadataModule.ContainingAssembly,
				R.T_AssemblyDirectory + sc.SemanticModel.Compilation.GetReferencedAssemblyPath(l.MetadataModule.ContainingAssembly).folder
			));
		}

		static void ShowSourceReference(TextBlock text, Location location) {
			var sourceTree = location.SourceTree;
			var sourceSpan = location.SourceSpan;
			var sourceText = sourceTree.GetText(default);
			var t = sourceText.ToString(new TextSpan(Math.Max(sourceSpan.Start - 100, 0), Math.Min(sourceSpan.Start, 100)));
			int i = t.LastIndexOfAny(new[] { '\r', '\n' });
			text.Append(i != -1 ? t.Substring(i).TrimStart() : t.TrimStart())
				.Append(sourceText.ToString(sourceSpan), true);
			t = sourceText.ToString(new TextSpan(sourceSpan.End, Math.Min(sourceTree.Length - sourceSpan.End, 100)));
			i = t.IndexOfAny(new[] { '\r', '\n' });
			text.Append((i != -1 ? t.Substring(0, i) : t).TrimEnd());
		}
		#endregion

		#region ISymbolFilterable
		SymbolFilterKind ISymbolFilterable.SymbolFilterKind {
			get => ContainerType.Case(SymbolListType.TypeList, SymbolFilterKind.Type,
				SymbolListType.SymbolReferrers, SymbolFilterKind.Usage,
				SymbolListType.NodeList, SymbolFilterKind.Node,
				SymbolFilterKind.Member);
		}

		void ISymbolFilterable.Filter(string[] keywords, int filterFlags) {
			switch (ContainerType) {
				case SymbolListType.TypeList:
					_Filter = FilterByTypeKinds(keywords, (MemberFilterTypes)filterFlags);
					break;
				case SymbolListType.Locations:
					_Filter = FilterByLocations(keywords);
					break;
				case SymbolListType.SymbolReferrers:
					_Filter = ((MemberFilterTypes)filterFlags).MatchFlags(MemberFilterTypes.AllUsages)
						? FilterByMemberTypes(keywords, (MemberFilterTypes)filterFlags)
						: FilterByUsages(keywords, (MemberFilterTypes)filterFlags);
					break;
				case SymbolListType.NodeList:
					_Filter = FilterByNodeTypes(keywords, (MemberFilterTypes)filterFlags);
					break;
				default:
					_Filter = FilterByMemberTypes(keywords, (MemberFilterTypes)filterFlags);
					break;
			}
			RefreshItemsSource();

			Predicate<object> FilterByNodeTypes(string[] k, MemberFilterTypes memberFilter) {
				var noKeyword = k.Length == 0;
				if (noKeyword && memberFilter == MemberFilterTypes.All) {
					return null;
				}
				if (noKeyword) {
					return o => SymbolFilterBox.FilterByImageId(memberFilter, ((SymbolItem)o).ImageId);
				}
				return o => SymbolFilterBox.FilterByImageId(memberFilter, ((SymbolItem)o).ImageId)
						&& MatchKeywords(((SymbolItem)o).Content.GetText(), k);
			}
			Predicate<object> FilterByMemberTypes(string[] k, MemberFilterTypes memberFilter) {
				var noKeyword = k.Length == 0;
				if (noKeyword && memberFilter == MemberFilterTypes.All) {
					return null;
				}
				if (noKeyword) {
					return o => SymbolFilterBox.FilterBySymbol(memberFilter, ((SymbolItem)o).Symbol);
				}
				return o => {
					var i = (SymbolItem)o;
					return SymbolFilterBox.FilterBySymbol(memberFilter, i.Symbol)
						&& MatchKeywords(i.Content.GetText(), k);
				};
			}
			Predicate<object> FilterByTypeKinds(string[] k, MemberFilterTypes typeFilter) {
				var noKeyword = k.Length == 0;
				if (noKeyword && typeFilter == MemberFilterTypes.All) {
					return null;
				}
				if (noKeyword) {
					return o => {
						var i = (SymbolItem)o;
						return i.Symbol != null && SymbolFilterBox.FilterBySymbolType(typeFilter, i.Symbol);
					};
				}
				return o => {
					var i = (SymbolItem)o;
					return i.Symbol != null
						&& SymbolFilterBox.FilterBySymbolType(typeFilter, i.Symbol)
						&& MatchKeywords(i.Content.GetText(), k);
				};
			}
			Predicate<object> FilterByLocations(string[] k) {
				if (k.Length == 0) {
					return null;
				}
				return o => {
					var i = (SymbolItem)o;
					return i.Location != null
						&& (MatchKeywords(((System.Windows.Documents.Run)i.Content.Inlines.FirstInline).Text, k)
								|| MatchKeywords(i.Hint, k));
				};
			}
			Predicate<object> FilterByUsages(string[] k, MemberFilterTypes filter) {
				var noKeyword = k.Length == 0;
				if (noKeyword && filter == MemberFilterTypes.All) {
					return null;
				}
				if (noKeyword) {
					return o => {
						var i = (SymbolItem)o;
						return SymbolFilterBox.FilterByUsage(filter, i)
							&& (i.Symbol != null ? SymbolFilterBox.FilterBySymbol(filter, i.Symbol) : SymbolFilterBox.FilterByImageId(filter, i.ImageId));
					};
				}
				return o => {
					var i = (SymbolItem)o;
					return SymbolFilterBox.FilterByUsage(filter, i)
						&& (i.Symbol != null
							? SymbolFilterBox.FilterBySymbol(filter, i.Symbol)
							: SymbolFilterBox.FilterByImageId(filter, i.ImageId))
						&& MatchKeywords(i.Content.GetText(), k);
				};
			}
			bool MatchKeywords(string text, string[] k) {
				var c = Char.IsUpper(k[0][0]) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
				var m = 0;
				foreach (var item in k) {
					if ((m = text.IndexOf(item, m, c)) == -1) {
						return false;
					}
				}
				return true;
			}
		}
		#endregion

		#region Drag and drop
		protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e) {
			base.OnPreviewMouseLeftButtonDown(e);
			if (ContainerType != SymbolListType.NodeList) {
				return;
			}
			var item = GetMouseEventData(e);
			if (item != null && SemanticContext != null && item.SyntaxNode != null) {
				MouseMove -= BeginDragHandler;
				MouseMove += BeginDragHandler;
			}
		}

		SymbolItem GetMouseEventData(MouseEventArgs e) {
			return GetMouseEventTarget(e)?.Content as SymbolItem;
		}

		ListBoxItem GetItemFromPoint(Point point) {
			return (InputHitTest(point) as DependencyObject).GetParentOrSelf<ListBoxItem>();
		}

		ListBoxItem GetMouseEventTarget(MouseEventArgs e) {
			return GetItemFromPoint(e.GetPosition(this));
		}

		ListBoxItem GetDragEventTarget(DragEventArgs e) {
			return GetItemFromPoint(e.GetPosition(this));
		}

		static SymbolItem GetDragData(DragEventArgs e) {
			return e.Data.GetData(typeof(SymbolItem)) as SymbolItem;
		}

		void BeginDragHandler(object sender, MouseEventArgs e) {
			SymbolItem item;
			if (e.LeftButton == MouseButtonState.Pressed
				&& (item = GetMouseEventData(e)) != null
				&& item.SyntaxNode != null) {
				Handler(this, item, e);
			}

			async void Handler(SymbolList me, SymbolItem i, MouseEventArgs args) {
				if (await me.SemanticContext.UpdateAsync(default).ConfigureAwait(false) == false) {
					return;
				}
				await i.RefreshSyntaxNodeAsync().ConfigureAwait(false);
				await SyncHelper.SwitchToMainThreadAsync(default);
				var s = args.Source as FrameworkElement;
				me.MouseMove -= me.BeginDragHandler;
				me.DragOver += me.DragOverHandler;
				me.Drop += me.DropHandler;
				me.DragEnter += me.DragOverHandler;
				me.DragLeave += me.DragLeaveHandler;
				me.QueryContinueDrag += me.QueryContinueDragHandler;
				var r = DragDrop.DoDragDrop(s, i, DragDropEffects.Copy | DragDropEffects.Move);
				if (me.Footer is TextBlock t) {
					t.Text = null;
				}
				me.DragOver -= me.DragOverHandler;
				me.Drop -= me.DropHandler;
				me.DragEnter -= me.DragOverHandler;
				me.DragLeave -= me.DragLeaveHandler;
				me.QueryContinueDrag -= me.QueryContinueDragHandler;
			}
		}

		void DragOverHandler(object sender, DragEventArgs e) {
			var li = GetDragEventTarget(e);
			SymbolItem target, source;
			// todo Enable dragging child before parent node
			if (li != null
				&& (target = li.Content as SymbolItem)?.SyntaxNode != null
				&& (source = GetDragData(e)) != null
				&& source != target
				&& (source.SyntaxNode.SyntaxTree.FilePath != target.SyntaxNode.SyntaxTree.FilePath
					|| source.SyntaxNode.Span.IntersectsWith(target.SyntaxNode.Span) == false)) {
				var copy = e.KeyStates.MatchFlags(DragDropKeyStates.ControlKey);
				e.Effects = copy ? DragDropEffects.Copy : DragDropEffects.Move;
				if (Footer is TextBlock t) {
					t.Text = (e.GetPosition(li).Y < li.ActualHeight / 2
						? (copy ? R.T_CopyBefore : R.T_MoveBefore)
						: (copy ? R.T_CopyAfter : R.T_MoveAfter)
						).Replace("<NAME>", target.SyntaxNode.GetDeclarationSignature());
				}
			}
			else {
				e.Effects = DragDropEffects.None;
			}
			e.Handled = true;
		}

		void DragLeaveHandler(object sender, DragEventArgs e) {
			if (Footer is TextBlock t) {
				t.Text = null;
			}
			e.Handled = true;
		}

		[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
		async void DropHandler(object sender, DragEventArgs e) {
			var li = GetDragEventTarget(e);
			SymbolItem source, target;
			if (li != null && (target = li.Content as SymbolItem)?.SyntaxNode != null
				&& (source = GetDragData(e)) != null) {
				try {
					await target.RefreshSyntaxNodeAsync();
				}
				catch (OperationCanceledException) {
					return;
				}
				var copy = e.KeyStates.MatchFlags(DragDropKeyStates.ControlKey);
				var before = e.GetPosition(li).Y < li.ActualHeight / 2;
				SemanticContext.View.CopyOrMoveSyntaxNode(source.SyntaxNode, target.SyntaxNode, copy, before);
				e.Effects = copy ? DragDropEffects.Copy : DragDropEffects.Move;
			}
			else {
				e.Effects = DragDropEffects.None;
			}
			e.Handled = true;
		}

		void QueryContinueDragHandler(object sender, QueryContinueDragEventArgs e) {
			if (e.EscapePressed) {
				e.Action = DragAction.Cancel;
				e.Handled = true;
			}
		}

		#endregion

		public override void Dispose() {
			base.Dispose();
			if (SemanticContext != null) {
				ClearSymbols();
				if (ContextMenu is IDisposable d) {
					d.Dispose();
				}
				SelectedItem = null;
				ItemsSource = null;
				SemanticContext = null;
				IconProvider = null;
				ExtIconProvider = null;
				SyncHelper.CancelAndDispose(ref _ContextMenuCancellationTokenSource, false);
				SyncHelper.CancelAndDispose(ref _ToolTipCancellationTokenSource, false);
			}
		}
	}
}
