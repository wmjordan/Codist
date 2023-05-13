using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AppHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using GDI = System.Drawing;
using R = Codist.Properties.Resources;
using Task = System.Threading.Tasks.Task;
using WPF = System.Windows.Media;

namespace Codist.Controls
{
	class SymbolList : VirtualList, ISymbolFilterable, INotifyCollectionChanged, IDisposable
	{
		Predicate<object> _Filter;
		readonly ToolTip _SymbolTip;
		readonly List<SymbolItem> _Symbols;
		//ListBoxItem _HighlightItem;

		public SymbolList(SemanticContext semanticContext) {
			_Symbols = new List<SymbolItem>();
			FilteredItems = new ListCollectionView(_Symbols);
			SemanticContext = semanticContext;
			_SymbolTip = new ToolTip {
				Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
				PlacementTarget = this
			};
			Resources = SharedDictionaryManager.SymbolList;
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
				GoToSourceAsync(SelectedIndex == -1 && HasItems
					? ItemContainerGenerator.Items[0] as SymbolItem
					: SelectedItem as SymbolItem);
				e.Handled = true;
			}

			async void GoToSourceAsync(SymbolItem i) {
				try {
					await i?.GoToSourceAsync();
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
			if (typeNamespace?.IsGlobalNamespace != false) {
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
			return (s.Symbol as IPropertySymbol)?.IsStatic == true;
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
			IconProvider = s => ((s.Symbol as IFieldSymbol)?.IsStatic == true) ? GetColorPreviewIcon(ColorHelper.GetBrush(s.Symbol.Name) ?? ColorHelper.GetSystemBrush(s.Symbol.Name)) : null;
		}
		void SetupListForKnownImageIds() {
			ContainerType = SymbolListType.VsKnownImage;
			IconProvider = s => {
				return s.Symbol is IFieldSymbol f && f.HasConstantValue && f.Type.SpecialType == SpecialType.System_Int32
					? ThemeHelper.GetImage((int)f.ConstantValue)
					: null;
			};
		}
		void SetupListForKnownMonikers() {
			ContainerType = SymbolListType.VsKnownImage;
			IconProvider = s => {
				return s.Symbol is IPropertySymbol p && p.IsStatic
					? ThemeHelper.GetImage(p.Name)
					: null;
			};
		}
		static Border GetColorPreviewIcon(WPF.Brush brush) {
			return brush == null ? null : new Border {
				BorderThickness = WpfHelper.TinyMargin,
				BorderBrush = ThemeHelper.MenuTextBrush,
				SnapsToDevicePixels = true,
				Background = brush,
				Height = ThemeHelper.DefaultIconSize,
				Width = ThemeHelper.DefaultIconSize,
			};
		}
		#endregion

		#region Context menu
		protected override void OnContextMenuOpening(ContextMenuEventArgs e) {
			base.OnContextMenuOpening(e);
			ShowContextMenu(e);
		}

		internal void ShowContextMenu(RoutedEventArgs e) {
			var item = SelectedSymbolItem;
			if (item == null
				|| (item.Symbol == null && item.SyntaxNode == null)
				|| (e.OriginalSource as DependencyObject).GetParentOrSelf<ListBoxItem>() == null) {
				e.Handled = true;
				return;
			}
			if (ContextMenu is CSharpSymbolContextMenu m) {
				m.Dispose();
			}
			ContextMenu = m = new CSharpSymbolContextMenu(item.Symbol, item.SyntaxNode, SemanticContext) {
				Resources = SharedDictionaryManager.ContextMenu,
				Foreground = ThemeHelper.ToolWindowTextBrush,
				IsEnabled = true,
			};
			SetupContextMenu(m, item);
			m.AddTitleItem(item.SyntaxNode?.GetDeclarationSignature() ?? item.Symbol.GetOriginalName());
			m.IsOpen = true;
		}

		void SetupContextMenu(CSharpSymbolContextMenu menu, SymbolItem item) {
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
				SetupMenuCommand(item, IconIds.SelectCode, R.CMD_SelectCode, s => {
					if (s.IsExternal) {
						s.SyntaxNode.SelectNode(true);
					}
					else {
						s.Container.SemanticContext.View.SelectNode(s.SyntaxNode, true);
					}
				});
				//SetupMenuCommand(item, KnownImageIds.Copy, "Copy Code", s => Clipboard.SetText(s.SyntaxNode.ToFullString()));
				item.SetSymbolToSyntaxNode();
			}
		}

		void SetupMenuCommand(SymbolItem item, int imageId, string title, Action<SymbolItem> action) {
			var mi = new ThemedMenuItem(imageId, title, (s, args) => {
				var (i, a) = (ValueTuple<SymbolItem, Action<SymbolItem>>)((MenuItem)s).Tag;
				a(i);
			}) {
				Tag = (item, action)
			};
			ContextMenu.Items.Add(mi);
		}
		#endregion

		#region Tool Tip
		protected override void OnMouseEnter(MouseEventArgs e) {
			base.OnMouseEnter(e);
			if (_SymbolTip.Tag == null) {
				_SymbolTip.Tag = DateTime.Now;
				SizeChanged -= SizeChanged_RelocateToolTip;
				MouseMove -= MouseMove_ChangeToolTip;
				MouseLeave -= MouseLeave_HideToolTip;
				SizeChanged += SizeChanged_RelocateToolTip;
				MouseMove += MouseMove_ChangeToolTip;
				MouseLeave += MouseLeave_HideToolTip;
				//if (ContainerType == SymbolListType.NodeList) {
				//	UnhookHighlightMouseEvent();
				//	MouseMove += MouseMove_HighlightCode;
				//	MouseLeave += MouseLeave_ClearCodeHighlight;
				//}
			}
		}

		//void MouseLeave_ClearCodeHighlight(object sender, MouseEventArgs e) {
		//	UnhookHighlightMouseEvent();
		//	_HighlightItem = null;
		//}

		//void MouseMove_HighlightCode(object sender, MouseEventArgs e) {
		//	if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.RangeHighlight) == false) {
		//		return;
		//	}
		//	var li = GetMouseEventTarget(e);
		//	if (li != null
		//		&& _HighlightItem != li
		//		&& li.Content is SymbolItem si
		//		&& si.IsExternal == false) {
		//		_HighlightItem = li;
		//		TextViewOverlay.Get(SemanticContext.View)
		//			?.SetRangeAdornment(si.SyntaxNode.Span.CreateSnapshotSpan(SemanticContext.View.TextSnapshot));
		//	}
		//}

		//void UnhookHighlightMouseEvent() {
		//	MouseMove -= MouseMove_HighlightCode;
		//	MouseLeave -= MouseLeave_ClearCodeHighlight;
		//}

		void MouseLeave_HideToolTip(object sender, MouseEventArgs e) {
			UnhookMouseEventAndHideToolTip();
		}

		void UnhookMouseEventAndHideToolTip() {
			SizeChanged -= SizeChanged_RelocateToolTip;
			MouseMove -= MouseMove_ChangeToolTip;
			MouseLeave -= MouseLeave_HideToolTip;
			HideToolTip();
		}

		internal void HideToolTip() {
			_SymbolTip.IsOpen = false;
			_SymbolTip.Content = null;
			_SymbolTip.Tag = null;
		}

		[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
		async void MouseMove_ChangeToolTip(object sender, MouseEventArgs e) {
			var li = GetMouseEventTarget(e);
			if (li != null && _SymbolTip.Tag != li) {
				await ShowToolTipForItemAsync(li);
			}
		}

		void SizeChanged_RelocateToolTip(object sender, SizeChangedEventArgs e) {
			if (_SymbolTip.IsOpen) {
				_SymbolTip.IsOpen = false;
				_SymbolTip.IsOpen = true;
			}
		}

		async Task ShowToolTipForItemAsync(ListBoxItem li) {
			_SymbolTip.Tag = li;
			_SymbolTip.Content = await CreateItemToolTipAsync(li);
			_SymbolTip.IsOpen = true;
		}

		async Task<object> CreateItemToolTipAsync(ListBoxItem li) {
			SymbolItem item;
			var sc = SemanticContext;
			if ((item = li.Content as SymbolItem) == null
				|| sc == null
				|| await sc.UpdateAsync(default) == false) {
				return null;
			}

			if (item.SyntaxNode != null) {
				return await CreateSyntaxNodeToolTipAsync(item, sc);
			}
			if (item.Symbol != null) {
				return await CreateSymbolToolTipAsync(item, sc);
			}
			if (item.Location != null) {
				return CreateLocationToolTip(item, sc);
			}
			return null;
		}

		async Task<object> CreateSyntaxNodeToolTipAsync(SymbolItem item, SemanticContext sc) {
			if (item.Symbol != null) {
				await item.RefreshSymbolAsync(default);
			}
			else {
				await item.SetSymbolToSyntaxNodeAsync();
			}
			if (item.Symbol != null) {
				var tip = ToolTipHelper.CreateToolTip(item.Symbol, ContainerType == SymbolListType.NodeList, sc);
				if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.LineOfCode)) {
					tip.AddTextBlock()
						.Append(R.T_LineOfCode + (item.SyntaxNode.GetLineSpan().Length + 1).ToString());
				}
				return tip;
			}
			return ((Microsoft.CodeAnalysis.CSharp.SyntaxKind)item.SyntaxNode.RawKind).GetSyntaxBrief();
		}

		async Task<object> CreateSymbolToolTipAsync(SymbolItem item, SemanticContext sc) {
			await item.RefreshSymbolAsync(default);
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
			var sourceText = sourceTree.GetText();
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
			get => ContainerType == SymbolListType.TypeList ? SymbolFilterKind.Type
				: ContainerType == SymbolListType.SymbolReferrers ? SymbolFilterKind.Usage
				: ContainerType == SymbolListType.NodeList ? SymbolFilterKind.Node
				: SymbolFilterKind.Member;
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
				Handler(item, e);
			}

			async void Handler(SymbolItem i, MouseEventArgs args) {
				if (await SemanticContext.UpdateAsync(default).ConfigureAwait(false)) {
					await i.RefreshSyntaxNodeAsync().ConfigureAwait(false);
					await SyncHelper.SwitchToMainThreadAsync();
					var s = args.Source as FrameworkElement;
					MouseMove -= BeginDragHandler;
					DragOver += DragOverHandler;
					Drop += DropHandler;
					DragEnter += DragOverHandler;
					DragLeave += DragLeaveHandler;
					QueryContinueDrag += QueryContinueDragHandler;
					var r = DragDrop.DoDragDrop(s, i, DragDropEffects.Copy | DragDropEffects.Move);
					if (Footer is TextBlock t) {
						t.Text = null;
					}
					DragOver -= DragOverHandler;
					Drop -= DropHandler;
					DragEnter -= DragOverHandler;
					DragLeave -= DragLeaveHandler;
					QueryContinueDrag -= QueryContinueDragHandler;
				}
			}
		}

		void DragOverHandler(object sender, DragEventArgs e) {
			var li = GetDragEventTarget(e);
			SymbolItem target, source;
			// todo Enable dragging child before parent node
			if (li != null && (target = li.Content as SymbolItem)?.SyntaxNode != null
				&& (source = GetDragData(e)) != null && source != target
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
				UnhookMouseEventAndHideToolTip();
				//UnhookHighlightMouseEvent();
				_SymbolTip.PlacementTarget = null;
				ClearSymbols();
				if (ContextMenu is IDisposable d) {
					d.Dispose();
				}
				SelectedItem = null;
				ItemsSource = null;
				SemanticContext = null;
				IconProvider = null;
				ExtIconProvider = null;
			}
		}
	}
}
