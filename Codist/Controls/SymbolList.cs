using System;
using System.Collections.Generic;
using System.Linq;
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
using WPF = System.Windows.Media;
using Task = System.Threading.Tasks.Task;

namespace Codist.Controls
{
	sealed class SymbolList : ListBox, ISymbolFilterable {
		public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register("Header", typeof(UIElement), typeof(SymbolList));
		public static readonly DependencyProperty HeaderButtonsProperty = DependencyProperty.Register("HeaderButtons", typeof(UIElement), typeof(SymbolList));
		public static readonly DependencyProperty FooterProperty = DependencyProperty.Register("Footer", typeof(UIElement), typeof(SymbolList));
		public static readonly DependencyProperty ItemsControlMaxHeightProperty = DependencyProperty.Register("ItemsControlMaxHeight", typeof(double), typeof(SymbolList));
		Predicate<object> _Filter;
		readonly ToolTip _SymbolTip;
		readonly CSharpSymbolContextMenu _ContextMenu;

		public SymbolList(SemanticContext semanticContext) {
			SetValue(VirtualizingPanel.IsVirtualizingProperty, true);
			SetValue(VirtualizingPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
			ItemsControlMaxHeight = 500;
			HorizontalContentAlignment = HorizontalAlignment.Stretch;
			Symbols = new List<SymbolItem>();
			FilteredSymbols = new ListCollectionView(Symbols);
			Resources = SharedDictionaryManager.SymbolList;
			SemanticContext = semanticContext;
			_SymbolTip = new ToolTip {
				Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
				PlacementTarget = this
			};
			ContextMenu = _ContextMenu = new CSharpSymbolContextMenu(semanticContext, false) {
				Resources = SharedDictionaryManager.ContextMenu,
				Foreground = ThemeHelper.ToolWindowTextBrush,
				IsEnabled = true,
			};
		}

		public UIElement Header {
			get => GetValue(HeaderProperty) as UIElement;
			set => SetValue(HeaderProperty, value);
		}
		public UIElement HeaderButtons {
			get => GetValue(HeaderButtonsProperty) as UIElement;
			set => SetValue(HeaderButtonsProperty, value);
		}
		public UIElement Footer {
			get => GetValue(FooterProperty) as UIElement;
			set => SetValue(FooterProperty, value);
		}
		public double ItemsControlMaxHeight {
			get => (double)GetValue(ItemsControlMaxHeightProperty);
			set => SetValue(ItemsControlMaxHeightProperty, value);
		}
		public SemanticContext SemanticContext { get; }
		public List<SymbolItem> Symbols { get; }
		public ListCollectionView FilteredSymbols { get; }
		public FrameworkElement Container { get; set; }
		public SymbolListType ContainerType { get; set; }
		public Func<SymbolItem, UIElement> IconProvider { get; set; }
		public SymbolItem SelectedSymbolItem => SelectedItem as SymbolItem;

		public SymbolItem Add(SyntaxNode node) {
			var item = new SymbolItem(node, this);
			Symbols.Add(item);
			return item;
		}
		public SymbolItem Add(ISymbol symbol, bool includeContainerType) {
			var item = new SymbolItem(symbol, this, includeContainerType);
			Symbols.Add(item);
			return item;
		}
		public SymbolItem Add(ISymbol symbol, ISymbol containerType) {
			var item = new SymbolItem(symbol, this, containerType);
			Symbols.Add(item);
			return item;
		}
		public void RefreshItemsSource() {
			if (_Filter != null) {
				FilteredSymbols.Filter = _Filter;
				ItemsSource = FilteredSymbols;
			}
			else {
				ItemsSource = Symbols;
			}
		}
		public void ScrollToSelectedItem() {
			if (SelectedIndex == -1) {
				return;
			}
			try {
				UpdateLayout();
			}
			catch (InvalidOperationException) {
				// ignore
#if DEBUG
				throw;
#endif
			}
			ScrollIntoView(ItemContainerGenerator.Items[SelectedIndex]);
		}

		protected override void OnPreviewKeyDown(KeyEventArgs e) {
			base.OnPreviewKeyDown(e);
			if (e.Key == Key.Tab) {
				e.Handled = true;
				return;
			}
			if (e.OriginalSource is TextBox == false) {
				return;
			}
			switch (e.Key) {
				case Key.Enter:
					if (SelectedIndex == -1 && HasItems) {
						(ItemContainerGenerator.Items[0] as SymbolItem)?.GoToSource();
					}
					else {
						(SelectedItem as SymbolItem)?.GoToSource();
					}
					e.Handled = true;
					break;
				case Key.Up:
					if (SelectedIndex > 0) {
						SelectedIndex--;
						ScrollToSelectedItem();
					}
					else if (HasItems) {
						SelectedIndex = this.ItemCount() - 1;
						ScrollToSelectedItem();
					}
					e.Handled = true;
					break;
				case Key.Down:
					if (SelectedIndex < this.ItemCount() - 1) {
						SelectedIndex++;
						ScrollToSelectedItem();
					}
					else if (HasItems) {
						SelectedIndex = 0;
						ScrollToSelectedItem();
					}
					e.Handled = true;
					break;
				case Key.PageUp:
					if (SelectedIndex >= 10) {
						SelectedIndex -= 10;
						ScrollToSelectedItem();
					}
					else if (HasItems) {
						SelectedIndex = 0;
						ScrollToSelectedItem();
					}
					e.Handled = true;
					break;
				case Key.PageDown:
					if (SelectedIndex >= this.ItemCount() - 10) {
						SelectedIndex = this.ItemCount() - 1;
						ScrollToSelectedItem();
					}
					else if (HasItems) {
						SelectedIndex += 10;
						ScrollToSelectedItem();
					}
					e.Handled = true;
					break;
			}
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
			base.OnRenderSizeChanged(sizeInfo);
			if (sizeInfo.WidthChanged) {
				var left = Canvas.GetLeft(this);
				Canvas.SetLeft(this, left < 0 ? 0 : left);
				if (Container != null) {
					var top = Canvas.GetTop(this);
					if (top + sizeInfo.NewSize.Height > Container.RenderSize.Height) {
						Canvas.SetTop(this, Container.RenderSize.Height - sizeInfo.NewSize.Height);
					}
					if (left + sizeInfo.NewSize.Width > Container.RenderSize.Width) {
						Canvas.SetLeft(this, Container.ActualWidth - sizeInfo.NewSize.Width);
					}
				}
			}
		}

		#region Analysis commands

		public (int count, int inherited) AddSymbolMembers(ISymbol symbol, bool isVsProject) {
			var count = AddSymbolMembers(symbol, isVsProject, null);
			var mi = 0;
			var type = symbol as INamedTypeSymbol;
			if (type != null) {
				switch (type.TypeKind) {
					case TypeKind.Class:
						while ((type = type.BaseType) != null && type.IsCommonClass() == false) {
							mi += AddSymbolMembers(type, isVsProject, type.ToDisplayString(WpfHelper.MemberNameFormat));
						}
						break;
					case TypeKind.Interface:
						foreach (var item in type.AllInterfaces) {
							mi += AddSymbolMembers(item, isVsProject, item.ToDisplayString(WpfHelper.MemberNameFormat));
						}
						break;
				}
			}
			return (count, mi);
		}

		public int AddSymbolMembers(ISymbol source, bool isVsProject, string typeCategory) {
			var nsOrType = source as INamespaceOrTypeSymbol;
			var members = nsOrType.GetMembers().RemoveAll(m => (m as IMethodSymbol)?.AssociatedSymbol != null || m.IsImplicitlyDeclared);
			if (isVsProject) {
				switch (nsOrType.Name) {
					case nameof(KnownImageIds):
						ContainerType = SymbolListType.VsKnownImage;
						IconProvider = s => {
							var f = s.Symbol as IFieldSymbol;
							return f == null || f.HasConstantValue == false || f.Type.SpecialType != SpecialType.System_Int32
								? null
								: ThemeHelper.GetImage((int)f.ConstantValue);
						};
						break;
					case nameof(EnvironmentColors): SetupListForVsUIColors(this, typeof(EnvironmentColors)); break;
					case nameof(CommonControlsColors): SetupListForVsUIColors(this, typeof(CommonControlsColors)); break;
					case nameof(CommonDocumentColors): SetupListForVsUIColors(this, typeof(CommonDocumentColors)); break;
					case nameof(HeaderColors): SetupListForVsUIColors(this, typeof(HeaderColors)); break;
					case nameof(InfoBarColors): SetupListForVsUIColors(this, typeof(InfoBarColors)); break;
					case nameof(ProgressBarColors): SetupListForVsUIColors(this, typeof(ProgressBarColors)); break;
					case nameof(SearchControlColors): SetupListForVsUIColors(this, typeof(SearchControlColors)); break;
					case nameof(StartPageColors): SetupListForVsUIColors(this, typeof(StartPageColors)); break;
					case nameof(ThemedDialogColors): SetupListForVsUIColors(this, typeof(ThemedDialogColors)); break;
					case nameof(TreeViewColors): SetupListForVsUIColors(this, typeof(TreeViewColors)); break;
					case nameof(VsColors): SetupListForVsResourceColors(this, typeof(VsColors)); break;
					case nameof(VsBrushes): SetupListForVsResourceBrushes(this, typeof(VsBrushes)); break;
				}
			}
			else {
				switch (nsOrType.Name) {
					case nameof(SystemColors):
					case nameof(GDI.SystemBrushes): SetupListForSystemColors(this); break;
					case nameof(GDI.Color):
					case nameof(GDI.Brushes):
					case nameof(WPF.Colors):
					case nameof(GDI.KnownColor): SetupListForKnownColors(this); break;
				}
			}
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
			void SetupListForKnownColors(SymbolList symbolList) {
				symbolList.ContainerType = SymbolListType.PredefinedColors;
				symbolList.IconProvider = s => ((s.Symbol as IPropertySymbol)?.IsStatic == true) ? GetColorPreviewIcon(ColorHelper.GetBrush(s.Symbol.Name) ?? ColorHelper.GetSystemBrush(s.Symbol.Name)) : null;
			}
			Border GetColorPreviewIcon(WPF.Brush brush) {
				return new Border {
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
			var item = SelectedSymbolItem;
			if (item == null
				|| (item.Symbol == null && item.SyntaxNode == null)
				|| (e.OriginalSource as DependencyObject).GetParentOrSelf<ListBoxItem>() == null) {
				e.Handled = true;
				return;
			}
			ContextMenu.Items.Clear();
			SetupContextMenu(item);
			ContextMenu.Items.Add(new MenuItem {
				Header = item.SyntaxNode?.GetDeclarationSignature() ?? item.Symbol.Name,
				IsEnabled = false,
				Icon = null,
				HorizontalContentAlignment = HorizontalAlignment.Right
			});
		}

		void SetupContextMenu(SymbolItem item) {
			if (item.SyntaxNode != null) {
				SetupMenuCommand(item, KnownImageIds.BlockSelection, "Select Code", s => s.Container.SemanticContext.View.SelectNode(s.SyntaxNode, true));
				//SetupMenuCommand(item, KnownImageIds.Copy, "Copy Code", s => Clipboard.SetText(s.SyntaxNode.ToFullString()));
				item.SetSymbolToSyntaxNode();
			}
			if (item.Symbol != null) {
				if (item.SyntaxNode == null && item.Symbol.HasSource()) {
					SetupMenuCommand(item, KnownImageIds.GoToDefinition, "Go to Code", s => s.Symbol.GoToSource());
					SetupMenuCommand(item, KnownImageIds.BlockSelection, "Select Code", s => s.Symbol.GetSyntaxNode().SelectNode(true));
				}
				SetupMenuCommand(item, KnownImageIds.DisplayName, "Copy Symbol Name", s => {
					try {
						Clipboard.SetDataObject(s.Symbol.Name);
					}
					catch (SystemException) {
						// ignore failure
					}
				});
				_ContextMenu.Items.Add(new Separator());
				_ContextMenu.SyntaxNode = item.SyntaxNode;
				_ContextMenu.Symbol = item.Symbol;
				_ContextMenu.AddAnalysisCommands();
			}
		}

		void SetupMenuCommand(SymbolItem item, int imageId, string title, Action<SymbolItem> action) {
			var mi = new ThemedMenuItem {
				Icon = ThemeHelper.GetImage(imageId),
				Header = new ThemedMenuText(title),
				Tag = (item, action)
			};
			mi.Click += (s, args) => {
				var i = (ValueTuple<SymbolItem, Action<SymbolItem>>)((MenuItem)s).Tag;
				i.Item2(i.Item1);
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
				|| await SemanticContext.UpdateAsync(default) == false) {
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
					var tip = ToolTipFactory.CreateToolTip(item.Symbol, ContainerType == SymbolListType.NodeList, SemanticContext.SemanticModel.Compilation);
					if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.LineOfCode)) {
						tip.AddTextBlock()
							.Append("Line of code: " + (item.SyntaxNode.GetLineSpan().Length + 1).ToString());
					}
					return tip;
				}
				return item.SyntaxNode.GetSyntaxBrief();
			}
			if (item.Symbol != null) {
				item.RefreshSymbol();
				return ToolTipFactory.CreateToolTip(item.Symbol, false, SemanticContext.SemanticModel.Compilation);
			}
			return null;
		}
		#endregion

		#region ISymbolFilterable
		SymbolFilterKind ISymbolFilterable.SymbolFilterKind {
			get => ContainerType == SymbolListType.TypeList ? SymbolFilterKind.Type : SymbolFilterKind.Member;
		}
		void ISymbolFilterable.Filter(string[] keywords, int filterFlags) {
			if (ContainerType == SymbolListType.TypeList) {
				_Filter = FilterByTypeKinds(keywords, (TypeFilterTypes)filterFlags);
			}
			else {
				_Filter = FilterByMemberTypes(keywords, (MemberFilterTypes)filterFlags);
			}
			RefreshItemsSource();

			Predicate<object> FilterByMemberTypes(string[] k, MemberFilterTypes memberFilter) {
				var noKeyword = keywords.Length == 0;
				if (noKeyword && memberFilter == MemberFilterTypes.All) {
					return null;
				}
				if (noKeyword) {
					return o => {
						var i = (SymbolItem)o;
						return i.Symbol != null ? SymbolFilterBox.FilterBySymbol(memberFilter, i.Symbol) : SymbolFilterBox.FilterByImageId(memberFilter, i.ImageId);
					};
				}
				var comparison = Char.IsUpper(keywords[0][0]) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
				return o => {
					var i = (SymbolItem)o;
					return (i.Symbol != null ? SymbolFilterBox.FilterBySymbol(memberFilter, i.Symbol) : SymbolFilterBox.FilterByImageId(memberFilter, i.ImageId))
							&& keywords.All(p => i.Content.GetText().IndexOf(p, comparison) != -1);
				};
			}
			Predicate<object> FilterByTypeKinds(string[] k, TypeFilterTypes typeFilter) {
				var noKeyword = keywords.Length == 0;
				if (noKeyword && typeFilter == TypeFilterTypes.All) {
					return null;
				}
				if (noKeyword) {
					return o => {
						var i = (SymbolItem)o;
						return i.Symbol != null ? SymbolFilterBox.FilterBySymbol(typeFilter, i.Symbol) : false;
					};
				}
				var comparison = Char.IsUpper(k[0][0]) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
				return o => {
					var i = (SymbolItem)o;
					return i.Symbol != null
						&& SymbolFilterBox.FilterBySymbol(typeFilter, i.Symbol)
						&& keywords.All(p => i.Content.GetText().IndexOf(p, comparison) != -1);
				};
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

		async void BeginDragHandler(object sender, MouseEventArgs e) {
			SymbolItem item;
			if (e.LeftButton != MouseButtonState.Pressed || (item = GetMouseEventData(e)) == null) {
				return;
			}
			if (item.SyntaxNode != null && await SemanticContext.UpdateAsync(default)) {
				item.RefreshSyntaxNode();
				var s = e.Source as FrameworkElement;
				MouseMove -= BeginDragHandler;
				DragOver += DragOverHandler;
				Drop += DropHandler;
				DragEnter += DragOverHandler;
				DragLeave += DragLeaveHandler;
				QueryContinueDrag += QueryContinueDragHandler;
				var r = DragDrop.DoDragDrop(s, item, DragDropEffects.Copy | DragDropEffects.Move);
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
					t.Text = (copy ? "Copy " : "Move ")
						+ (e.GetPosition(li).Y < li.ActualHeight / 2 ? "before " : "after ")
						+ target.SyntaxNode.GetDeclarationSignature();
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

	sealed class SymbolItem /*: INotifyPropertyChanged*/
	{
		UIElement _Icon;
		int _ImageId;
		TextBlock _Content;
		string _Hint;
		readonly bool _IncludeContainerType;

		//public event PropertyChangedEventHandler PropertyChanged;
		public int ImageId => _ImageId != 0 ? _ImageId : (_ImageId = Symbol != null ? Symbol.GetImageId() : SyntaxNode != null ? SyntaxNode.GetImageId() : -1);
		public UIElement Icon => _Icon ?? (_Icon = Container.IconProvider?.Invoke(this) ?? ThemeHelper.GetImage(ImageId != -1 ? ImageId : 0));
		public string Hint {
			get => _Hint ?? (_Hint = Symbol != null ? GetSymbolConstaintValue(Symbol) : String.Empty);
			set => _Hint = value;
		}
		public SymbolItemType Type { get; set; }
		public bool IsExternal => Type == SymbolItemType.External
			|| Container.ContainerType == SymbolListType.None && Symbol?.ContainingAssembly.GetSourceType() == AssemblySource.Metadata;
		public TextBlock Content {
			get => _Content ?? (_Content = Symbol != null ? CreateContentForSymbol(Symbol, _IncludeContainerType, true) : SyntaxNode != null ? new ThemedMenuText().Append(SyntaxNode.GetDeclarationSignature()) : new ThemedMenuText());
			set => _Content = value;
		}
		public Location Location { get; set; }
		public SyntaxNode SyntaxNode { get; private set; }
		public ISymbol Symbol { get; private set; }
		public SymbolList Container { get; }

		public SymbolItem(SymbolList list) {
			Container = list;
			Content = new ThemedMenuText();
			_ImageId = -1;
		}
		public SymbolItem(ISymbol symbol, SymbolList list, ISymbol containerSymbol)
			: this (symbol, list, false) {
			_ImageId = containerSymbol.GetImageId();
			_Content = CreateContentForSymbol(containerSymbol, false, true);
		}
		public SymbolItem(ISymbol symbol, SymbolList list, bool includeContainerType) {
			Symbol = symbol;
			Container = list;
			_IncludeContainerType = includeContainerType;
		}

		public SymbolItem(SyntaxNode node, SymbolList list) {
			SyntaxNode = node;
			Container = list;
		}

		public void GoToSource() {
			if (Location != null) {
				Location.GoToSource();
			}
			else if (Symbol != null) {
				RefreshSymbol();
				Symbol.GoToSource();
			}
			else if (SyntaxNode != null) {
				RefreshSyntaxNode();
				SyntaxNode.GetIdentifierToken().GetLocation().GoToSource();
			}
		}
		public bool SelectIfContainsPosition(int position) {
			if (IsExternal == false && SyntaxNode != null && SyntaxNode.FullSpan.Contains(position, true)) {
				Container.SelectedItem = this;
				return true;
			}
			return false;
		}
		public ThemedMenuText CreateContentForSymbol(ISymbol symbol, bool includeType, bool includeParameter) {
			var t = new ThemedMenuText();
			if (includeType && symbol.ContainingType != null) {
				t.Append(symbol.ContainingType.Name + symbol.ContainingType.GetParameterString() + ".", ThemeHelper.SystemGrayTextBrush);
			}
			t.Append(symbol.Name);
			if (includeParameter) {
				t.Append(symbol.GetParameterString(), ThemeHelper.SystemGrayTextBrush);
			}
			return t;
		}

		static string GetSymbolConstaintValue(ISymbol symbol) {
			if (symbol.Kind == SymbolKind.Field) {
				var f = symbol as IFieldSymbol;
				if (f.HasConstantValue) {
					return f.ConstantValue?.ToString();
				}
			}
			return null;
		}
		internal void SetSymbolToSyntaxNode() {
			Symbol = Container.SemanticContext.GetSymbolAsync(SyntaxNode).ConfigureAwait(false).GetAwaiter().GetResult();
		}
		internal void RefreshSyntaxNode() {
			var node = Container.SemanticContext.RelocateDeclarationNode(SyntaxNode);
			if (node != null && node != SyntaxNode) {
				SyntaxNode = node;
			}
		}
		internal void RefreshSymbol() {
			var symbol = Container.SemanticContext.RelocateSymbolAsync(Symbol).ConfigureAwait(false).GetAwaiter().GetResult();
			if (symbol != null && symbol != Symbol) {
				Symbol = symbol;
			}
		}
	}


	public class SymbolItemTemplateSelector : DataTemplateSelector
	{
		public override DataTemplate SelectTemplate(object item, DependencyObject container) {
			var c = container as FrameworkElement;
			var i = item as SymbolItem;
			if (i != null && (i.Symbol != null || i.SyntaxNode != null)) {
				return c.FindResource("SymbolItemTemplate") as DataTemplate;
			}
			else {
				return c.FindResource("LabelTemplate") as DataTemplate;
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
		TypeList
	}
	enum SymbolItemType
	{
		Normal,
		External,
		Container,
	}
}
