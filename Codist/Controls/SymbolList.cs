using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AppHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using GDI = System.Drawing;
using Task = System.Threading.Tasks.Task;
using WPF = System.Windows.Media;
using R = Codist.Properties.Resources;

namespace Codist.Controls
{
	sealed class SymbolList : VirtualList, ISymbolFilterable
	{
		Predicate<object> _Filter;
		readonly ToolTip _SymbolTip;
		readonly List<SymbolItem> _Symbols;

		public SymbolList(SemanticContext semanticContext) {
			_Symbols = new List<SymbolItem>();
			FilteredItems = new ListCollectionView(_Symbols);
			SemanticContext = semanticContext;
			_SymbolTip = new ToolTip {
				Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
				PlacementTarget = this
			};
			Resources = SharedDictionaryManager.SymbolList;
		}

		public SemanticContext SemanticContext { get; }
		public IReadOnlyList<SymbolItem> Symbols => _Symbols;
		public SymbolListType ContainerType { get; set; }
		public Func<SymbolItem, UIElement> IconProvider { get; set; }
		public Func<SymbolItem, UIElement> ExtIconProvider { get; set; }
		public SymbolItem SelectedSymbolItem => SelectedItem as SymbolItem;

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

		public void Clear() {
			_Symbols.Clear();
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

		protected override void OnPreviewKeyDown(KeyEventArgs e) {
			base.OnPreviewKeyDown(e);
			if (e.OriginalSource is TextBox == false || e.Handled) {
				return;
			}
			if (e.Key == Key.Enter) {
				var item = SelectedIndex == -1 && HasItems
					? ItemContainerGenerator.Items[0] as SymbolItem
					: SelectedItem as SymbolItem;
				if (item != null) {
					item.GoToSource();
				}
				e.Handled = true;
			}
		}

		#region Analysis commands
		internal void AddNamespaceItems(ISymbol[] symbols, ISymbol highlight) {
			foreach (var item in symbols) {
				var i = Add(item, false);
				if (item == highlight) {
					SelectedItem = i;
				}
			}
		}
		internal (int count, int external) AddSymbolMembers(ISymbol symbol) {
			var count = AddSymbolMembers(symbol, null);
			var mi = 0;
			var type = symbol as INamedTypeSymbol;
			if (type != null) {
				switch (type.TypeKind) {
					case TypeKind.Class:
						while ((type = type.BaseType) != null && type.IsCommonClass() == false) {
							mi += AddSymbolMembers(type, type.ToDisplayString(CodeAnalysisHelper.MemberNameFormat));
						}
						break;
					case TypeKind.Interface:
						foreach (var item in type.AllInterfaces) {
							mi += AddSymbolMembers(item, item.ToDisplayString(CodeAnalysisHelper.MemberNameFormat));
						}
						break;
				}
			}
			return (count, mi);
		}

		int AddSymbolMembers(ISymbol source, string typeCategory) {
			var nsOrType = source as INamespaceOrTypeSymbol;
			var members = nsOrType.GetMembers().RemoveAll(m => {
				if (m.IsImplicitlyDeclared) {
					return true;
				}
				if (m.Kind == SymbolKind.Method) {
					var ms = (IMethodSymbol)m;
					if (ms.AssociatedSymbol != null) {
						return true;
					}
					switch (ms.MethodKind) {
						case MethodKind.PropertyGet:
						case MethodKind.PropertySet:
						case MethodKind.EventAdd:
						case MethodKind.EventRemove:
							return true;
					}
				}
				return false;
			});
			SetupForSpecialTypes(this, source.ContainingNamespace.ToString(), source.Name);
			if (source.Kind == SymbolKind.NamedType && ((INamedTypeSymbol)source).TypeKind == TypeKind.Enum) {
				// sort enum members by value
				members = members.Sort(CodeAnalysisHelper.CompareByFieldIntegerConst);
			}
			else {
				members = members.Sort(CodeAnalysisHelper.CompareByAccessibilityKindName);
			}
			foreach (var item in members) {
				var i = Add(item, false);
				if (typeCategory != null) {
					i.Hint = typeCategory;
				}
			}
			return members.Length;

			void SetupForSpecialTypes(SymbolList list, string typeNamespace, string typeName) {
				switch (typeNamespace) {
					case "System.Drawing":
						switch (typeName) {
							case nameof(GDI.SystemBrushes):
							case nameof(GDI.SystemPens):
							case nameof(GDI.SystemColors):
								SetupListForSystemColors(list); return;
							case nameof(GDI.Color):
							case nameof(GDI.Brushes):
							case nameof(GDI.Pens):
								SetupListForColors(list); return;
							case nameof(GDI.KnownColor): SetupListForKnownColors(list); return;
						}
						return;
					case "System.Windows":
						if (typeName == nameof(SystemColors)) {
							SetupListForSystemColors(list);
						}
						return;
					case "System.Windows.Media":
						switch (typeName) {
							case nameof(WPF.Colors):
							case nameof(WPF.Brushes):
								SetupListForColors(list); return;
						}
						return;
					case "Microsoft.VisualStudio.PlatformUI":
						switch (typeName) {
							case nameof(EnvironmentColors): SetupListForVsUIColors(list, typeof(EnvironmentColors)); return;
							case nameof(CommonControlsColors): SetupListForVsUIColors(list, typeof(CommonControlsColors)); return;
							case nameof(CommonDocumentColors): SetupListForVsUIColors(list, typeof(CommonDocumentColors)); return;
							case nameof(HeaderColors): SetupListForVsUIColors(list, typeof(HeaderColors)); return;
							case nameof(InfoBarColors): SetupListForVsUIColors(list, typeof(InfoBarColors)); return;
							case nameof(ProgressBarColors): SetupListForVsUIColors(list, typeof(ProgressBarColors)); return;
							case nameof(SearchControlColors): SetupListForVsUIColors(list, typeof(SearchControlColors)); return;
							case nameof(StartPageColors): SetupListForVsUIColors(list, typeof(StartPageColors)); return;
							case nameof(ThemedDialogColors): SetupListForVsUIColors(list, typeof(ThemedDialogColors)); return;
							case nameof(TreeViewColors): SetupListForVsUIColors(list, typeof(TreeViewColors)); return;
						}
						return;
					case "Microsoft.VisualStudio.Shell":
						switch (typeName) {
							case nameof(VsColors): SetupListForVsResourceColors(list, typeof(VsColors)); return;
							case nameof(VsBrushes): SetupListForVsResourceBrushes(list, typeof(VsBrushes)); return;
						}
						return;
					case "Microsoft.VisualStudio.Imaging":
						switch (typeName) {
							case nameof(KnownImageIds):
								SetupListForKnownImageIds(list);
								break;
							case nameof(KnownMonikers):
								SetupListForKnownMonikers(list);
								break;
						}
						return;
				}
			}

			void SetupListForVsUIColors(SymbolList symbolList, Type type) {
				symbolList.ContainerType = SymbolListType.PredefinedColors;
				symbolList.IconProvider = s => ((s.Symbol as IPropertySymbol)?.IsStatic == true) ? GetColorPreviewIcon(ColorHelper.GetVsThemeBrush(type, s.Symbol.Name)) : null;
			}
			void SetupListForVsResourceColors(SymbolList symbolList, Type type) {
				symbolList.ContainerType = SymbolListType.PredefinedColors;
				symbolList.IconProvider = s => ((s.Symbol as IPropertySymbol)?.IsStatic == true) ? GetColorPreviewIcon(ColorHelper.GetVsResourceColor(type, s.Symbol.Name)) : null;
			}
			void SetupListForVsResourceBrushes(SymbolList symbolList, Type type) {
				symbolList.ContainerType = SymbolListType.PredefinedColors;
				symbolList.IconProvider = s => ((s.Symbol as IPropertySymbol)?.IsStatic == true) ? GetColorPreviewIcon(ColorHelper.GetVsResourceBrush(type, s.Symbol.Name)) : null;
			}
			void SetupListForSystemColors(SymbolList symbolList) {
				symbolList.ContainerType = SymbolListType.PredefinedColors;
				symbolList.IconProvider = s => ((s.Symbol as IPropertySymbol)?.IsStatic == true) ? GetColorPreviewIcon(ColorHelper.GetSystemBrush(s.Symbol.Name)) : null;
			}
			void SetupListForColors(SymbolList symbolList) {
				symbolList.ContainerType = SymbolListType.PredefinedColors;
				symbolList.IconProvider = s => ((s.Symbol as IPropertySymbol)?.IsStatic == true) ? GetColorPreviewIcon(ColorHelper.GetBrush(s.Symbol.Name)) : null;
			}
			void SetupListForKnownColors(SymbolList symbolList) {
				symbolList.ContainerType = SymbolListType.PredefinedColors;
				symbolList.IconProvider = s => ((s.Symbol as IFieldSymbol)?.IsStatic == true) ? GetColorPreviewIcon(ColorHelper.GetBrush(s.Symbol.Name) ?? ColorHelper.GetSystemBrush(s.Symbol.Name)) : null;
			}
			void SetupListForKnownImageIds(SymbolList symbolList) {
				symbolList.ContainerType = SymbolListType.VsKnownImage;
				symbolList.IconProvider = s => {
					var f = s.Symbol as IFieldSymbol;
					return f == null || f.HasConstantValue == false || f.Type.SpecialType != SpecialType.System_Int32
						? null
						: ThemeHelper.GetImage((int)f.ConstantValue);
				};
			}
			void SetupListForKnownMonikers(SymbolList symbolList) {
				symbolList.ContainerType = SymbolListType.VsKnownImage;
				symbolList.IconProvider = s => {
					var p = s.Symbol as IPropertySymbol;
					return p == null || p.IsStatic == false
						? null
						: ThemeHelper.GetImage(p.Name);
				};
			}
			Border GetColorPreviewIcon(WPF.Brush brush) {
				return brush == null ? null : new Border {
					BorderThickness = WpfHelper.TinyMargin,
					BorderBrush = ThemeHelper.MenuTextBrush,
					SnapsToDevicePixels = true,
					Background = brush,
					Height = ThemeHelper.DefaultIconSize,
					Width = ThemeHelper.DefaultIconSize,
				};
			}
		}
		#endregion

		#region Context menu
		protected override void OnContextMenuOpening(ContextMenuEventArgs e) {
			base.OnContextMenuOpening(e);
			ShowContextMenu(e);
		}

		internal void ShowContextMenu(RoutedEventArgs e) {
			var m = ContextMenu as CSharpSymbolContextMenu;
			if (m == null) {
				ContextMenu = m = new CSharpSymbolContextMenu(SemanticContext) {
					Resources = SharedDictionaryManager.ContextMenu,
					Foreground = ThemeHelper.ToolWindowTextBrush,
					IsEnabled = true,
				};
			}
			var item = SelectedSymbolItem;
			if (item == null
				|| (item.Symbol == null && item.SyntaxNode == null)
				|| (e.OriginalSource as DependencyObject).GetParentOrSelf<ListBoxItem>() == null) {
				e.Handled = true;
				return;
			}
			m.Symbol = item.Symbol;
			m.SyntaxNode = item.SyntaxNode;
			m.Items.Clear();
			SetupContextMenu(m, item);
			m.AddTitleItem(item.SyntaxNode?.GetDeclarationSignature() ?? item.Symbol.GetOriginalName());
			m.IsOpen = true;
		}

		void SetupContextMenu(CSharpSymbolContextMenu menu, SymbolItem item) {
			if (item.SyntaxNode != null) {
				SetupMenuCommand(item, IconIds.SelectCode, R.CMD_SelectCode, s => s.Container.SemanticContext.View.SelectNode(s.SyntaxNode, true));
				//SetupMenuCommand(item, KnownImageIds.Copy, "Copy Code", s => Clipboard.SetText(s.SyntaxNode.ToFullString()));
				item.SetSymbolToSyntaxNode();
			}
			if (item.Symbol != null) {
				if (item.SyntaxNode == null && item.Symbol.HasSource()) {
					menu.AddSymbolNodeCommands();
				}
				else {
					menu.AddSymbolCommands();
				}
				menu.Items.Add(new Separator());
				menu.SyntaxNode = item.SyntaxNode;
				menu.Symbol = item.Symbol;
				menu.AddAnalysisCommands();
			}
		}

		void SetupMenuCommand(SymbolItem item, int imageId, string title, Action<SymbolItem> action) {
			var mi = new ThemedMenuItem(imageId, title, (s, args) => {
				var i = (ValueTuple<SymbolItem, Action<SymbolItem>>)((MenuItem)s).Tag;
				i.Item2(i.Item1);
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
				SizeChanged += SizeChanged_RelocateToolTip;
				MouseMove += MouseMove_ChangeToolTip;
				MouseLeave += MouseLeave_HideToolTip;
			}
		}

		void MouseLeave_HideToolTip(object sender, MouseEventArgs e) {
			HideToolTip();
		}

		public void HideToolTip() {
			SizeChanged -= SizeChanged_RelocateToolTip;
			MouseMove -= MouseMove_ChangeToolTip;
			MouseLeave -= MouseLeave_HideToolTip;
			_SymbolTip.IsOpen = false;
			_SymbolTip.Content = null;
			_SymbolTip.Tag = null;
		}

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
			if ((item = li.Content as SymbolItem) == null
				|| await SemanticContext.UpdateAsync(default).ConfigureAwait(true) == false) {
				return null;
			}

			if (item.SyntaxNode != null) {
				if (item.Symbol != null) {
					item.RefreshSymbol();
				}
				else {
					item.SetSymbolToSyntaxNode();
				}
				if (item.Symbol != null) {
					var tip = ToolTipFactory.CreateToolTip(item.Symbol, ContainerType == SymbolListType.NodeList, SemanticContext);
					if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.LineOfCode)) {
						tip.AddTextBlock()
							.Append(R.T_LineOfCode + (item.SyntaxNode.GetLineSpan().Length + 1).ToString());
					}
					return tip;
				}
				return ((Microsoft.CodeAnalysis.CSharp.SyntaxKind)item.SyntaxNode.RawKind).GetSyntaxBrief();
			}
			if (item.Symbol != null) {
				item.RefreshSymbol();
				var tip = ToolTipFactory.CreateToolTip(item.Symbol, false, SemanticContext);
				if (ContainerType == SymbolListType.SymbolReferrers && item.Location.IsInSource) {
					// append location info to tip
					item.ShowSourceReference(tip.AddTextBlock().Append(R.T_SourceReference).AppendLine());
				}
				return tip;
			}
			if (item.Location != null) {
				if (item.Location.IsInSource) {
					var f = item.Location.SourceTree.FilePath;
					return new ThemedToolTip(Path.GetFileName(f), String.Join(Environment.NewLine,
						R.T_Folder + Path.GetDirectoryName(f),
						R.T_Line + (item.Location.GetLineSpan().StartLinePosition.Line + 1).ToString(),
						R.T_Project + SemanticContext.GetDocument(item.Location.SourceTree)?.Project.Name
					));
				}
				else {
					return new ThemedToolTip(item.Location.MetadataModule.Name, R.T_ContainingAssembly + item.Location.MetadataModule.ContainingAssembly);
				}
			}
			return null;
		}
		#endregion

		#region ISymbolFilterable
		SymbolFilterKind ISymbolFilterable.SymbolFilterKind {
			get => ContainerType == SymbolListType.TypeList ? SymbolFilterKind.Type
				: ContainerType == SymbolListType.SymbolReferrers ? SymbolFilterKind.Usage
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
				default:
					_Filter = FilterByMemberTypes(keywords, (MemberFilterTypes)filterFlags);
					break;
			}
			RefreshItemsSource();

			Predicate<object> FilterByMemberTypes(string[] k, MemberFilterTypes memberFilter) {
				var noKeyword = k.Length == 0;
				if (noKeyword && memberFilter == MemberFilterTypes.All) {
					return null;
				}
				if (noKeyword) {
					return o => {
						var i = (SymbolItem)o;
						return i.Symbol != null ? SymbolFilterBox.FilterBySymbol(memberFilter, i.Symbol) : SymbolFilterBox.FilterByImageId(memberFilter, i.ImageId);
					};
				}
				var comparison = Char.IsUpper(k[0][0]) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
				return o => {
					var i = (SymbolItem)o;
					return (i.Symbol != null
							? SymbolFilterBox.FilterBySymbol(memberFilter, i.Symbol)
							: SymbolFilterBox.FilterByImageId(memberFilter, i.ImageId))
						&& MatchKeywords(i.Content.GetText(), k, comparison);
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
				var comparison = Char.IsUpper(k[0][0]) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
				return o => {
					var i = (SymbolItem)o;
					return i.Symbol != null
						&& SymbolFilterBox.FilterBySymbolType(typeFilter, i.Symbol)
						&& MatchKeywords(i.Content.GetText(), k, comparison);
				};
			}
			Predicate<object> FilterByLocations(string[] k) {
				if (k.Length == 0) {
					return null;
				}
				var comparison = Char.IsUpper(k[0][0]) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
				return o => {
					var i = (SymbolItem)o;
					return i.Location != null
						&& (MatchKeywords(((System.Windows.Documents.Run)i.Content.Inlines.FirstInline).Text, k, comparison)
								|| MatchKeywords(i.Hint, k, comparison));
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
				var comparison = Char.IsUpper(k[0][0]) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
				return o => {
					var i = (SymbolItem)o;
					return SymbolFilterBox.FilterByUsage(filter, i)
						&& (i.Symbol != null
							? SymbolFilterBox.FilterBySymbol(filter, i.Symbol)
							: SymbolFilterBox.FilterByImageId(filter, i.ImageId))
						&& MatchKeywords(i.Content.GetText(), k, comparison);
				};
			}
			bool MatchKeywords(string text, string[] k, StringComparison c) {
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
				if (await SemanticContext.UpdateAsync(default).ConfigureAwait(true)) {
					i.RefreshSyntaxNode();
					var s = args.Source as FrameworkElement;
					MouseMove -= BeginDragHandler;
					DragOver += DragOverHandler;
					Drop += DropHandler;
					DragEnter += DragOverHandler;
					DragLeave += DragLeaveHandler;
					QueryContinueDrag += QueryContinueDragHandler;
					var r = DragDrop.DoDragDrop(s, i, DragDropEffects.Copy | DragDropEffects.Move);
					var t = Footer as TextBlock;
					if (t != null) {
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
				var t = Footer as TextBlock;
				if (t != null) {
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
			var t = Footer as TextBlock;
			if (t != null) {
				t.Text = null;
			}
			e.Handled = true;
		}

		void DropHandler(object sender, DragEventArgs e) {
			var li = GetDragEventTarget(e);
			SymbolItem source, target;
			if (li != null && (target = li.Content as SymbolItem)?.SyntaxNode != null
				&& (source = GetDragData(e)) != null) {
				target.RefreshSyntaxNode();
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

	}


	sealed class ExtIconProvider
	{
		ExtIconProvider(bool containerIsInterface) {
			_ContainerIsInterface = containerIsInterface;
		}
		public static readonly ExtIconProvider Default = new ExtIconProvider(false);
		public static readonly ExtIconProvider InterfaceMembers = new ExtIconProvider(true);
		readonly bool _ContainerIsInterface;

		public StackPanel GetExtIcons(SymbolItem symbolItem) {
			return GetSpecialSymbolIcon(symbolItem.Symbol);
		}

		public StackPanel GetExtIconsWithUsage(SymbolItem symbolItem) {
			var icons = GetSpecialSymbolIcon(symbolItem.Symbol);
			if (symbolItem.Usage != SymbolUsageKind.Normal) {
				AddSymbolUsageIcons(ref icons, symbolItem.Usage);
			}
			return icons;
		}

		StackPanel GetSpecialSymbolIcon(ISymbol symbol) {
			StackPanel icons = null;
			switch (symbol.Kind) {
				case SymbolKind.Method:
					var ms = symbol as IMethodSymbol;
					if (ms.IsAsync || ms.ReturnType.IsAwaitable()) {
						AddIcon(ref icons, IconIds.AsyncMember);
					}
					if (ms.IsGenericMethod) {
						AddIcon(ref icons, IconIds.Generic);
					}
					if (_ContainerIsInterface == false) {
						if (ms.IsAbstract) {
							AddIcon(ref icons, IconIds.AbstractMember);
						}
						else if (ms.IsExtensionMethod) {
							AddIcon(ref icons, IconIds.ExtensionMethod);
						}
						else {
							if (ms.IsSealed) {
								AddIcon(ref icons, IconIds.SealedMethod);
							}
							if (ms.IsOverride) {
								AddIcon(ref icons, IconIds.OverrideMethod);
							}
						}
					}
					break;
				case SymbolKind.NamedType:
					var type = symbol as INamedTypeSymbol;
					if (type.IsGenericType) {
						AddIcon(ref icons, IconIds.Generic);
					}
					if (type.TypeKind == TypeKind.Class) {
						if (type.IsSealed && type.IsStatic == false) {
							AddIcon(ref icons, IconIds.SealedClass);
						}
						else if (type.IsAbstract) {
							AddIcon(ref icons, IconIds.AbstractClass);
						}
					}
					break;
				case SymbolKind.Field:
					var f = symbol as IFieldSymbol;
					if (f.IsConst) {
						return null;
					}
					if (f.IsReadOnly) {
						AddIcon(ref icons, IconIds.ReadonlyField);
					}
					else if (f.IsVolatile) {
						AddIcon(ref icons, IconIds.VolatileField);
					}
					break;
				case SymbolKind.Event:
					if (_ContainerIsInterface == false) {
						if (symbol.IsAbstract) {
							AddIcon(ref icons, IconIds.AbstractMember);
						}
						else {
							if (symbol.IsSealed) {
								AddIcon(ref icons, IconIds.SealedEvent);
							}
							if (symbol.IsOverride) {
								AddIcon(ref icons, IconIds.OverrideEvent);
							}
						}
					}
					break;
				case SymbolKind.Property:
					if (_ContainerIsInterface == false) {
						if ((ms = ((IPropertySymbol)symbol).SetMethod) == null) {
							AddIcon(ref icons, IconIds.ReadonlyProperty);
						}
						else if (ms.IsInitOnly()) {
							AddIcon(ref icons, IconIds.InitonlyProperty);
						}
						if (symbol.IsAbstract) {
							AddIcon(ref icons, IconIds.AbstractMember);
						}
						else {
							if (symbol.IsSealed) {
								AddIcon(ref icons, IconIds.SealedProperty);
							}
							if (symbol.IsOverride) {
								AddIcon(ref icons, IconIds.OverrideProperty);
							}
						}
					}
					break;
				case SymbolKind.Namespace:
					return null;
			}
			if (symbol.IsStatic) {
				AddIcon(ref icons, IconIds.StaticMember);
			}
			return icons;
		}

		static void AddSymbolUsageIcons(ref StackPanel icons, SymbolUsageKind usage) {
			if (usage.MatchFlags(SymbolUsageKind.Write)) {
				AddIcon(ref icons, IconIds.UseToWrite);
			}
			else if (usage.MatchFlags(SymbolUsageKind.Catch)) {
				AddIcon(ref icons, IconIds.UseToCatch);
			}
			else if (usage.HasAnyFlag(SymbolUsageKind.Attach | SymbolUsageKind.Detach)) {
				if (usage.MatchFlags(SymbolUsageKind.Attach)) {
					AddIcon(ref icons, IconIds.AttachEvent);
				}
				if (usage.MatchFlags(SymbolUsageKind.Detach)) {
					AddIcon(ref icons, IconIds.DetachEvent);
				}
			}
			else if (usage.MatchFlags(SymbolUsageKind.TypeCast)) {
				AddIcon(ref icons, IconIds.UseToCast);
			}
			else if (usage.MatchFlags(SymbolUsageKind.TypeParameter)) {
				AddIcon(ref icons, IconIds.UseAsTypeParameter);
			}
			else if (usage.MatchFlags(SymbolUsageKind.Delegate)) {
				AddIcon(ref icons, IconIds.UseAsDelegate);
			}
		}

		static void AddIcon(ref StackPanel container, int imageId) {
			if (container == null) {
				container = new StackPanel { Orientation = Orientation.Horizontal };
			}
			container.Children.Add(ThemeHelper.GetImage(imageId));
		}
	}


	public class SymbolItemTemplateSelector : DataTemplateSelector
	{
		public override DataTemplate SelectTemplate(object item, DependencyObject container) {
			var c = container as FrameworkElement;
			var i = item as SymbolItem;
			if (i is null || i.Symbol is null && i.SyntaxNode is null && i.Location is null) {
				return c.FindResource("LabelTemplate") as DataTemplate;
			}
			else {
				return c.FindResource("SymbolItemTemplate") as DataTemplate;
			}
		}
	}

	enum SymbolListType
	{
		None,
		/// <summary>
		/// Previews KnownImageIds
		/// </summary>
		VsKnownImage,
		/// <summary>
		/// Previews predefined colors
		/// </summary>
		PredefinedColors,
		/// <summary>
		/// Enables drag and drop
		/// </summary>
		NodeList,
		/// <summary>
		/// Filter by type kinds
		/// </summary>
		TypeList,
		/// <summary>
		/// Lists source code locations
		/// </summary>
		Locations,
		/// <summary>
		/// List of symbol referrers
		/// </summary>
		SymbolReferrers,
	}
}
