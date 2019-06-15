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

namespace Codist.Controls
{
	sealed class SymbolList : ItemList, ISymbolFilterable {
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
		public void RefreshItemsSource() {
			if (_Filter != null) {
				FilteredItems.Filter = _Filter;
				ItemsSource = FilteredItems;
			}
			else {
				ItemsSource = _Symbols;
			}
		}

		protected override void OnPreviewKeyDown(KeyEventArgs e) {
			base.OnPreviewKeyDown(e);
			if (e.OriginalSource is TextBox == false || e.Handled) {
				return;
			}
			if (e.Key == Key.Enter) {
				if (SelectedIndex == -1 && HasItems) {
					(ItemContainerGenerator.Items[0] as SymbolItem)?.GoToSource();
				}
				else {
					(SelectedItem as SymbolItem)?.GoToSource();
				}
				e.Handled = true;
			}
		}

		#region Analysis commands

		public (int count, int inherited) AddSymbolMembers(ISymbol symbol) {
			var count = AddSymbolMembers(symbol, null);
			var mi = 0;
			var type = symbol as INamedTypeSymbol;
			if (type != null) {
				switch (type.TypeKind) {
					case TypeKind.Class:
						while ((type = type.BaseType) != null && type.IsCommonClass() == false) {
							mi += AddSymbolMembers(type, type.ToDisplayString(WpfHelper.MemberNameFormat));
						}
						break;
					case TypeKind.Interface:
						foreach (var item in type.AllInterfaces) {
							mi += AddSymbolMembers(item, item.ToDisplayString(WpfHelper.MemberNameFormat));
						}
						break;
				}
			}
			return (count, mi);
		}

		int AddSymbolMembers(ISymbol source, string typeCategory) {
			var nsOrType = source as INamespaceOrTypeSymbol;
			var members = nsOrType.GetMembers().RemoveAll(m => (m as IMethodSymbol)?.AssociatedSymbol != null || m.IsImplicitlyDeclared);
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
							case nameof(GDI.SystemBrushes): SetupListForSystemColors(list); return;
							case nameof(GDI.Color):
							case nameof(GDI.Brushes):
							case nameof(GDI.KnownColor): SetupListForKnownColors(list); return;
						}
						return;
					case "System.Windows":
						if (typeName == nameof(SystemColors)) {
							SetupListForSystemColors(list);
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
						if (typeName == nameof(KnownImageIds)) {
							SetupListForKnownImageIds(list);
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
			void SetupListForKnownColors(SymbolList symbolList) {
				symbolList.ContainerType = SymbolListType.PredefinedColors;
				symbolList.IconProvider = s => ((s.Symbol as IPropertySymbol)?.IsStatic == true) ? GetColorPreviewIcon(ColorHelper.GetBrush(s.Symbol.Name) ?? ColorHelper.GetSystemBrush(s.Symbol.Name)) : null;
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
			m.Items.Clear();
			SetupContextMenu(m, item);
			m.AddTitleItem(item.SyntaxNode?.GetDeclarationSignature() ?? item.Symbol.GetOriginalName());
			m.IsOpen = true;
		}

		void SetupContextMenu(CSharpSymbolContextMenu menu, SymbolItem item) {
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
						Clipboard.SetDataObject(s.Symbol.GetOriginalName());
					}
					catch (SystemException) {
						// ignore failure
					}
				});
				menu.Items.Add(new Separator());
				menu.SyntaxNode = item.SyntaxNode;
				menu.Symbol = item.Symbol;
				menu.AddAnalysisCommands();
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
			if (item.Location != null) {
				if (item.Location.IsInSource) {
					var p = item.Location.SourceTree.FilePath;
					return new ThemedToolTip(Path.GetFileName(p), $"Folder: {Path.GetDirectoryName(p)}{Environment.NewLine}Line: {item.Location.GetLineSpan().StartLinePosition.Line + 1}");
				}
				else {
					return new ThemedToolTip(item.Location.MetadataModule.Name, $"Containing assembly: {item.Location.MetadataModule.ContainingAssembly}");
				}
			}
			return null;
		}
		#endregion

		#region ISymbolFilterable
		SymbolFilterKind ISymbolFilterable.SymbolFilterKind {
			get => ContainerType == SymbolListType.TypeList ? SymbolFilterKind.Type : SymbolFilterKind.Member;
		}
		void ISymbolFilterable.Filter(string[] keywords, int filterFlags) {
			switch (ContainerType) {
				case SymbolListType.TypeList:
					_Filter = FilterByTypeKinds(keywords, (TypeFilterTypes)filterFlags);
					break;
				case SymbolListType.Locations:
					_Filter = FilterByLocations(keywords);
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
			Predicate<object> FilterByTypeKinds(string[] k, TypeFilterTypes typeFilter) {
				var noKeyword = k.Length == 0;
				if (noKeyword && typeFilter == TypeFilterTypes.All) {
					return null;
				}
				if (noKeyword) {
					return o => {
						var i = (SymbolItem)o;
						return i.Symbol != null && SymbolFilterBox.FilterBySymbol(typeFilter, i.Symbol);
					};
				}
				var comparison = Char.IsUpper(k[0][0]) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
				return o => {
					var i = (SymbolItem)o;
					return i.Symbol != null
						&& SymbolFilterBox.FilterBySymbol(typeFilter, i.Symbol)
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
			get => _Content ?? (_Content = Symbol != null
				? CreateContentForSymbol(Symbol, _IncludeContainerType, true)
				: SyntaxNode != null
					? new ThemedMenuText().Append(SyntaxNode.GetDeclarationSignature())
					: new ThemedMenuText());
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
		public SymbolItem(Location location, SymbolList list) {
			Container = list;
			Location = location;
			if (location.IsInSource) {
				var filePath = location.SourceTree.FilePath;
				_Content = new ThemedMenuText(Path.GetFileNameWithoutExtension(filePath)).Append(Path.GetExtension(filePath), ThemeHelper.SystemGrayTextBrush);
				_Hint = Path.GetFileName(Path.GetDirectoryName(filePath));
				_ImageId = KnownImageIds.CSFile;
			}
			else {
				var m = location.MetadataModule;
				_Content = new ThemedMenuText(Path.GetFileNameWithoutExtension(m.Name)).Append(Path.GetExtension(m.Name), ThemeHelper.SystemGrayTextBrush);
				_Hint = String.Empty;
				_ImageId = KnownImageIds.Module;
			}
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
			if (Location != null && Location.IsInSource) {
				Location.GoToSource();
			}
			else if (SyntaxNode != null) {
				RefreshSyntaxNode();
				SyntaxNode.GetIdentifierToken().GetLocation().GoToSource();
			}
			else if (Symbol != null) {
				RefreshSymbol();
				Symbol.GoToSource();
			}
		}
		public bool SelectIfContainsPosition(int position) {
			if (IsExternal || SyntaxNode == null || SyntaxNode.FullSpan.Contains(position, true) == false) {
				return false;
			}
			Container.SelectedItem = this;
			return true;
		}
		static ThemedMenuText CreateContentForSymbol(ISymbol symbol, bool includeType, bool includeParameter) {
			var t = new ThemedMenuText();
			if (includeType && symbol.ContainingType != null) {
				t.Append(symbol.ContainingType.Name + symbol.ContainingType.GetParameterString() + ".", ThemeHelper.SystemGrayTextBrush);
			}
			t.Append(symbol.GetOriginalName());
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
	}
	enum SymbolItemType
	{
		Normal,
		External,
		Container,
	}
}
