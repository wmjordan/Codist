using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AppHelpers;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Task = System.Threading.Tasks.Task;

namespace Codist.NaviBar
{
	public sealed class CSharpBar : NaviBar
	{
		internal const string SyntaxNodeRange = nameof(SyntaxNodeRange);
		readonly IAdornmentLayer _SyntaxNodeRangeAdornment;
		readonly SemanticContext _SemanticContext;

		CancellationTokenSource _cancellationSource = new CancellationTokenSource();
		readonly RootItem _RootItem;
		NodeItem _MouseHoverItem;
		SymbolList _SymbolList;
		ThemedImageButton _ActiveItem;

		public CSharpBar(IWpfTextView textView) : base(textView) {
			_SyntaxNodeRangeAdornment = View.GetAdornmentLayer(SyntaxNodeRange);
			_SemanticContext = SemanticContext.GetOrCreateSingetonInstance(textView);
			Name = nameof(CSharpBar);
			Items.Add(_RootItem = new RootItem(this));
			View.TextBuffer.Changed += TextBuffer_Changed;
			View.Selection.SelectionChanged += Update;
			View.Closed += ViewClosed;
			Config.Updated += Config_Updated;
			Update(this, EventArgs.Empty);
			if (_SemanticContext.Compilation != null) {
				foreach(var m in _SemanticContext.Compilation.Members) {
					if (m.IsKind(SyntaxKind.NamespaceDeclaration)) {
						_RootItem.SetText(m.GetDeclarationSignature());
						break;
					}
				}
			}
		}

		protected override void OnMouseMove(MouseEventArgs e) {
			base.OnMouseMove(e);
			if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.RangeHighlight) == false) {
				return;
			}
			if (_MouseHoverItem != null) {
				var p = e.GetPosition(_MouseHoverItem);
				if (p.X != 0 && p.Y != 0 && _MouseHoverItem.Contains(p)) {
					return;
				}
			}
			for (int i = Items.Count - 1; i >= 0; i--) {
				var item = Items[i] as NodeItem;
				if (item == null || item.Contains(e.GetPosition(item)) == false) {
					continue;
				}

				_MouseHoverItem = item;
				if (_SyntaxNodeRangeAdornment.IsEmpty == false) {
					_SyntaxNodeRangeAdornment.RemoveAllAdornments();
				}
				var span = item.Node.Span.CreateSnapshotSpan(View.TextSnapshot);
				if (span.Length > 0) {
					try {
						HighlightNodeRanges(item.Node, span);
					}
					catch (ObjectDisposedException) {
						// ignore
						_MouseHoverItem = null;
					}
				}
				return;
			}
			if (_SyntaxNodeRangeAdornment.IsEmpty == false) {
				_SyntaxNodeRangeAdornment.RemoveAllAdornments();
				_MouseHoverItem = null;
			}
		}

		void HighlightNodeRanges(SyntaxNode node, SnapshotSpan span) {
			_SyntaxNodeRangeAdornment.AddAdornment(span, null, new GeometryAdornment(ThemeHelper.MenuHoverBackgroundColor, View.TextViewLines.GetMarkerGeometry(span), 3));
			var p = View.Caret.Position.BufferPosition;
			if (span.Contains(p) == false) {
				return;
			}
			var n = _SemanticContext.GetNode(p, false, false);
			while (n != null && node.Contains(n)) {
				if (n.Span.Start != span.Start
					&& n.Span.Length != span.Length) {
					var nodeKind = n.Kind();
					if (nodeKind != SyntaxKind.Block) {
						span = n.Span.CreateSnapshotSpan(View.TextSnapshot);
						_SyntaxNodeRangeAdornment.AddAdornment(span, null, new GeometryAdornment(ThemeHelper.MenuHoverBackgroundColor, View.TextViewLines.GetMarkerGeometry(span), nodeKind.IsSyntaxBlock() || nodeKind.IsDeclaration() ? 1 : 0));
					}
				}
				n = n.Parent;
			}
		}

		protected override void OnMouseLeave(MouseEventArgs e) {
			base.OnMouseLeave(e);
			if (_SyntaxNodeRangeAdornment.IsEmpty == false) {
				_SyntaxNodeRangeAdornment.RemoveAllAdornments();
				_MouseHoverItem = null;
			}
		}

		void TextBuffer_Changed(object sender, TextContentChangedEventArgs e) {
			HideMenu();
			_RootItem.ClearSymbolList();
		}

		async void Update(object sender, EventArgs e) {
			HideMenu();
			SyncHelper.CancelAndDispose(ref _cancellationSource, true);
			var cs = _cancellationSource;
			if (cs != null) {
				try {
					await Update(SyncHelper.CancelAndRetainToken(ref _cancellationSource));
				}
				catch (OperationCanceledException) {
					// ignore
				}
			}
			async Task Update(CancellationToken token) {
				var nodes = await UpdateModelAndGetContainingNodesAsync(token);
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
				var c = Math.Min(Items.Count, nodes.Count);
				int i, i2;
				for (i = 1, i2 = 0; i < c && i2 < c; i2++) {
					var n = nodes[i2];
					if (token.IsCancellationRequested) {
						return;
					}
					if (Items[i] is NodeItem ni && ni.Node == n) {
						// keep the NaviItem if node is not updated
						++i;
						continue;
					}
					if (n.IsKind(SyntaxKind.NamespaceDeclaration)) {
						continue;
					}
					break;
				}
				if ((i == 1 || i2 < nodes.Count && nodes[i2].Kind().IsTypeOrNamespaceDeclaration()) && _RootItem.FilterText.Length == 0) {
					// clear type and namespace menu items if a type is changed
					_RootItem.ClearSymbolList();
				}
				c = Items.Count;
				// remove nodes out of range
				while (c > i) {
					Items.RemoveAt(--c);
				}
				c = nodes.Count;
				NodeItem memberNode = null;
				while (i2 < c) {
					if (token.IsCancellationRequested) {
						return;
					}
					var node = nodes[i2];
					if (node.IsKind(SyntaxKind.NamespaceDeclaration)) {
						_RootItem.SetText(node.GetDeclarationSignature());
						++i2;
						continue;
					}
					var newItem = new NodeItem(this, node);
					if (memberNode == null && node.Kind().IsMemberDeclaration()) {
						memberNode = newItem;
						((TextBlock)newItem.Header).FontWeight = FontWeights.Bold;
						((TextBlock)newItem.Header).SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.FileTabSelectedTextBrushKey);
						newItem.IsChecked = true;
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.ReferencingTypes)) {
							newItem.ReferencedDocs.AddRange(node.FindRelatedTypes(_SemanticContext.SemanticModel, token).Take(5));
						}
					}
					Items.Add(newItem);
					++i2;
				}
				if (memberNode == null) {
					memberNode = Items.GetFirst<NodeItem>(n => n.HasReferencedDocs);
				}
				if (memberNode != null && memberNode.HasReferencedDocs) {
					foreach (var doc in memberNode.ReferencedDocs) {
						Items.Add(new DocItem(this, doc));
					}
				}
			}
		}

		async Task<List<SyntaxNode>> UpdateModelAndGetContainingNodesAsync(CancellationToken token) {
			var start = View.GetCaretPosition();
			if (await _SemanticContext.UpdateAsync(start, token) == false) {
				return new List<SyntaxNode>();
			}
			return _SemanticContext.GetContainingNodes(start, Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.SyntaxDetail), Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.RegionOnBar));
		}

		void ViewClosed(object sender, EventArgs e) {
			View.Selection.SelectionChanged -= Update;
			View.TextBuffer.Changed -= TextBuffer_Changed;
			Config.Updated -= Config_Updated;
			SyncHelper.CancelAndDispose(ref _cancellationSource, false);
			View.Closed -= ViewClosed;
		}

		void Config_Updated(object sender, ConfigUpdatedEventArgs e) {
			if (e.UpdatedFeature == Features.NaviBar) {
				for (int i = Items.Count - 1; i > 0; i--) {
					var item = Items[i] as NodeItem;
					if (item != null) {
						Items.RemoveAt(i);
					}
				}
				Update(this, EventArgs.Empty);
			}
		}

		#region Menu handler
		public override void ShowRootItemMenu() {
			_RootItem.ShowNamespaceAndTypeMenu();
		}
		public override void ShowActiveItemMenu() {
			for (int i = Items.Count - 1; i >= 0; i--) {
				var item = Items[i] as NodeItem;
				if (item != null && item.Node.Kind().IsTypeDeclaration()) {
					item.PerformClick();
					return;
				}
			}
		}
		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
			base.OnRenderSizeChanged(sizeInfo);
			PositionMenu();
		}

		void SetupSymbolListMenu(SymbolList list) {
			list.MouseLeftButtonUp += MenuItemSelect;
		}

		void ShowMenu(ThemedImageButton barItem, SymbolList menu) {
			if (_SymbolList != menu) {
				ListContainer.Children.Remove(_SymbolList);
				ListContainer.Children.Add(menu);
				_SymbolList = menu;
				if (_ActiveItem != null) {
					_ActiveItem.IsHighlighted = false;
				}
			}
			_ActiveItem = barItem;
			barItem.IsHighlighted = true;
			menu.ItemsControlMaxHeight = ListContainer.ActualHeight / 2;
			menu.RefreshItemsSource();
			menu.ScrollToSelectedItem();
			menu.PreviewKeyUp -= OnMenuKeyUp;
			menu.PreviewKeyUp += OnMenuKeyUp;
			PositionMenu();
		}

		void PositionMenu() {
			if (_SymbolList != null) {
				Canvas.SetLeft(_SymbolList, _ActiveItem.TransformToVisual(_ActiveItem.GetParent<Grid>()).Transform(new Point()).X - View.VisualElement.TranslatePoint(new Point(), View.VisualElement.GetParent<Grid>()).X);
				Canvas.SetTop(_SymbolList, -1);
			}
		}

		void OnMenuKeyUp(object sender, KeyEventArgs e) {
			if (e.Key == Key.Escape) {
				HideMenu();
				e.Handled = true;
			}
			else if (e.Key == Key.Tab && _ActiveItem != null) {
				int i;
				if (Keyboard.Modifiers.MatchFlags(ModifierKeys.Shift)) {
					if ((i = Items.IndexOf(_ActiveItem)) > 0) {
						((ThemedImageButton)Items[i - 1]).PerformClick();
					}
				}
				else {
					if ((i = Items.IndexOf(_ActiveItem)) < Items.Count - 1) {
						((ThemedImageButton)Items[i + 1]).PerformClick();
					}
				}
				e.Handled = true;
			}
		}

		void HideMenu() {
			if (_SymbolList != null) {
				ListContainer.Children.Remove(_SymbolList);
				_SymbolList.SelectedItem = null;
				_SymbolList = null;
				if (_ActiveItem != null) {
					_ActiveItem.IsHighlighted = false;
				}
				_ActiveItem = null;
			}
		}

		void MenuItemSelect(object sender, MouseButtonEventArgs e) {
			var menu = sender as SymbolList;
			if (menu.SelectedIndex == -1 || e.OccursOn<ListBoxItem>() == false) {
				return;
			}
			View.VisualElement.Focus();
			(menu.SelectedItem as SymbolItem)?.GoToSource();
		}
		#endregion

		static ThemedMenuText SetHeader(SyntaxNode node, bool includeContainer, bool highlight, bool includeParameterList) {
			var title = node.GetDeclarationSignature();
			if (title == null) {
				return null;
			}
			if (node.IsStructuredTrivia && Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.StripRegionNonLetter)) {
				title = TrimNonLetterOrDigitCharacters(title);
			}
			if (includeContainer == false && node.Kind().IsTypeDeclaration()) {
				var p = node.Parent;
				while (p.Kind().IsTypeDeclaration()) {
					title = "..." + title;
					p = p.Parent;
				}
			}
			var t = new ThemedMenuText();
			if (includeContainer) {
				var p = node.Parent;
				if (node is VariableDeclaratorSyntax) {
					p = p.Parent.Parent;
				}
				if (p is BaseTypeDeclarationSyntax) {
					t.Append(((BaseTypeDeclarationSyntax)p).Identifier.ValueText + ".", ThemeHelper.SystemGrayTextBrush);
				}
			}
			t.Append(title, highlight);
			if (includeParameterList && Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.ParameterList)) {
				AddParameterList(t, node);
			}
			return t;
		}

		static void AddParameterList(TextBlock t, SyntaxNode node) {
			ParameterListSyntax p = null;
			if (node is BaseMethodDeclarationSyntax) {
				p = ((BaseMethodDeclarationSyntax)node).ParameterList;
			}
			else if (node.IsKind(SyntaxKind.DelegateDeclaration)) {
				p = ((DelegateDeclarationSyntax)node).ParameterList;
			}
			else if (node is OperatorDeclarationSyntax) {
				p = ((OperatorDeclarationSyntax)node).ParameterList;
			}
			if (p != null) {
				var useParamName = Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.ParameterListShowParamName);
				t.Append(p.GetParameterListSignature(useParamName), ThemeHelper.SystemGrayTextBrush);
			}
		}

		static string TrimNonLetterOrDigitCharacters(string title) {
			int s = 0, e = title.Length;
			for (int i = 0; i < title.Length; i++) {
				if (Char.IsLetterOrDigit(title[i])) {
					s = i;
					break;
				}
			}
			for (int i = title.Length - 1; i >= s; i--) {
				if (Char.IsLetterOrDigit(title[i])) {
					e = i + 1;
					break;
				}
			}
			return s > 0 || e < title.Length ? title.Substring(s, e - s) : title;
		}

		sealed class RootItem : ThemedImageButton, IContextMenuHost
		{
			readonly CSharpBar _Bar;
			readonly SymbolList _Menu;
			readonly MemberFinderBox _FinderBox;
			readonly SearchScopeBox _ScopeBox;
			readonly TextBlock _Note;

			public RootItem(CSharpBar bar) : base(KnownImageIds.Namespace, new ThemedToolBarText()) {
				_Bar = bar;
				this.ReferenceCrispImageBackground(EnvironmentColors.MainWindowActiveCaptionColorKey);
				SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextActiveKey);
				_Menu = new SymbolList(bar._SemanticContext) {
					Container = _Bar.ListContainer,
					ContainerType = SymbolListType.NodeList,
					Header = new StackPanel {
						Margin = WpfHelper.MenuItemMargin,
						Children = {
							new Separator { Tag = new ThemedMenuText("Search Declaration") },
							new StackPanel {
								Orientation = Orientation.Horizontal,
								Children = {
									ThemeHelper.GetImage(KnownImageIds.SearchContract).WrapMargin(WpfHelper.GlyphMargin),
									(_FinderBox = new MemberFinderBox() { MinWidth = 150 }),
									(_ScopeBox = new SearchScopeBox {
										Contents = {
											new ThemedButton(KnownImageIds.StopFilter, "Clear filter", ClearFilter).ClearBorder()
										}
									}),
								}
							},
						}
					},
					Footer = _Note = new TextBlock { Margin = WpfHelper.MenuItemMargin }
						.ReferenceProperty(TextBlock.ForegroundProperty, EnvironmentColors.SystemGrayTextBrushKey)
				};
				_Bar.SetupSymbolListMenu(_Menu);
				_FinderBox.TextChanged += SearchCriteriaChanged;
				_ScopeBox.FilterChanged += SearchCriteriaChanged;
				_ScopeBox.FilterChanged += (s, args) => _FinderBox.Focus();
			}

			public string FilterText => _FinderBox.Text;

			public void ClearSymbolList() {
				_Menu.NeedsRefresh = true;
			}
			internal void SetText(string text) {
				((TextBlock)Header).Text = text;
			}

			protected override void OnClick() {
				base.OnClick();
				if (_Bar._SymbolList == _Menu) {
					_Bar.HideMenu();
					return;
				}
				ShowNamespaceAndTypeMenu();
			}

			internal void ShowNamespaceAndTypeMenu() {
				if (_Menu.NeedsRefresh) {
					_Menu.NeedsRefresh = false;
					_Menu.Clear();
					_Menu.ItemsSource = null;
				}
				PopulateTypes();
				_Note.Clear();
				if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.LineOfCode)) {
					  _Note.Append(ThemeHelper.GetImage(KnownImageIds.Code))
						.Append(_Bar.View.TextSnapshot.LineCount);
				}
				_Bar.ShowMenu(this, _Menu);
			}

			void PopulateTypes() {
				if (_FinderBox.Text.Length == 0) {
					if (_Menu.Symbols.Count == 0) {
						AddNamespaceAndTypes();
					}
					else {
						MarkEnclosingType();
					}
				}
			}

			void MarkEnclosingType() {
				int pos = _Bar.View.GetCaretPosition();
				for (int i = _Menu.Symbols.Count - 1; i >= 0; i--) {
					if (_Menu.Symbols[i].SelectIfContainsPosition(pos)) {
						return;
					}
				}
			}

			void AddNamespaceAndTypes() {
				foreach (var node in _Bar._SemanticContext.Compilation.ChildNodes()) {
					if (node.Kind().IsTypeOrNamespaceDeclaration()) {
						_Menu.Add(node);
						AddTypeDeclarations(node);
					}
				}
				MarkEnclosingType();
			}

			void AddTypeDeclarations(SyntaxNode node) {
				foreach (var child in node.ChildNodes()) {
					if (child.Kind().IsTypeOrNamespaceDeclaration()) {
						var i = _Menu.Add(child);
						string prefix = null;
						var p = child.Parent;
						while (p.Kind().IsTypeDeclaration()) {
							prefix = "..." + prefix;
							p = p.Parent;
						}
						if (prefix != null) {
							i.Content.Inlines.InsertBefore(i.Content.Inlines.FirstInline, new System.Windows.Documents.Run(prefix));
						}
						AddTypeDeclarations(child);
					}
				}
			}

			void ClearFilter() {
				if (_FinderBox.Text.Length > 0) {
					_FinderBox.Text = String.Empty;
				}
				_FinderBox.Focus();
			}

			void SearchCriteriaChanged(object sender, EventArgs e) {
				SyncHelper.CancelAndDispose(ref _Bar._cancellationSource, true);
				_Menu.ItemsSource = null;
				_Menu.Clear();
				var s = _FinderBox.Text;
				if (s.Length == 0) {
					_Menu.ContainerType = SymbolListType.NodeList;
					ShowNamespaceAndTypeMenu();
					return;
				}
				_Menu.ContainerType = SymbolListType.None;
				try {
					switch (_ScopeBox.Filter) {
						case ScopeType.ActiveDocument:
							FindInDocument(s);
							break;
						case ScopeType.ActiveProject:
							FindInProject(s);
							break;
					}
					_Menu.RefreshItemsSource();
					_Menu.UpdateLayout();
				}
				catch (OperationCanceledException) {
					// ignores cancellation
				}
				catch (ObjectDisposedException) { }
			}
			void FindInDocument(string text) {
				var cancellationToken = _Bar._cancellationSource.GetToken();
				foreach (var item in _Bar._SemanticContext.Compilation.GetDecendantDeclarations(cancellationToken)) {
					if (item.GetDeclarationSignature().IndexOf(text, StringComparison.OrdinalIgnoreCase) != -1) {
						var i = _Menu.Add(item);
						i.Content = SetHeader(item, true, false, true);
					}
				}
			}
			void FindInProject(string text) {
				SyncHelper.RunSync(() => FindDeclarationsAsync(text, _Bar._cancellationSource.GetToken()));
			}

			async Task FindDeclarationsAsync(string symbolName, CancellationToken token) {
				var result = await _Bar._SemanticContext.Document.Project.FindDeclarationsAsync(symbolName, 50, false, false, SymbolFilter.All, token).ConfigureAwait(false);
				foreach (var item in result) {
					if (token.IsCancellationRequested) {
						break;
					}
					_Menu.Add(item, true);
				}
			}

			void IContextMenuHost.ShowContextMenu(RoutedEventArgs args) {
				ShowNamespaceAndTypeMenu();
			}

			sealed class MemberFinderBox : ThemedTextBox
			{
				public MemberFinderBox() : base() {
					IsVisibleChanged += (s, args) => {
						var b = s as TextBox;
						if (b.IsVisible) {
							b.Focus();
							b.SelectAll();
						}
					};
				}
			}
		}

		sealed class NodeItem : ThemedImageButton, ISymbolFilter, IContextMenuHost
		{
			readonly int _ImageId;
			readonly CSharpBar _Bar;
			SymbolList _Menu;
			SymbolFilterBox _FilterBox;
			int _PartialCount;
			ISymbol _Symbol;
			List<ISymbol> _ReferencedDocs;

			public NodeItem(CSharpBar bar, SyntaxNode node)
				: base (node.GetImageId(), new ThemedMenuText(node.GetDeclarationSignature() ?? String.Empty)) {
				_Bar = bar;
				_ImageId = node.GetImageId();
				Node = node;
				this.ReferenceCrispImageBackground(EnvironmentColors.MainWindowActiveCaptionColorKey);
				SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextActiveKey);
				Click += HandleClick;
				this.UseDummyToolTip();
			}

			public SyntaxNode Node { get; private set; }
			public ISymbol Symbol => _Symbol ?? (_Symbol = SyncHelper.RunSync(() => _Bar._SemanticContext.GetSymbolAsync(Node, _Bar._cancellationSource.GetToken())));
			public bool HasReferencedDocs => _ReferencedDocs != null && _ReferencedDocs.Count > 0;
			public List<ISymbol> ReferencedDocs => _ReferencedDocs ?? (_ReferencedDocs = new List<ISymbol>());

			public void ShowContextMenu(RoutedEventArgs args) {
				if (ContextMenu == null) {
					var m = new CSharpSymbolContextMenu(_Bar._SemanticContext) {
						SyntaxNode = Node
					};
					m.AddNodeCommands();
					var s = Symbol;
					if (s != null) {
						m.Symbol = s;
						m.Items.Add(new Separator());
						m.AddAnalysisCommands();
						m.AddSymbolCommands();
						m.AddTitleItem(Node.GetDeclarationSignature());
					}
					//m.PlacementTarget = this;
					//m.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
					ContextMenu = m;
				}
				ContextMenu.IsOpen = true;
			}

			async void HandleClick(object sender, RoutedEventArgs e) {
				SyncHelper.CancelAndDispose(ref _Bar._cancellationSource, true);
				if (_Menu != null && _Bar._SymbolList == _Menu) {
					_Bar.HideMenu();
					return;
				}
				if (Node.Kind().IsTypeDeclaration() == false) {
					var span = Node.FullSpan;
					if (span.Contains(_Bar._SemanticContext.Position) && Node.SyntaxTree.FilePath == _Bar._SemanticContext.Document.FilePath
						|| Node.IsKind(SyntaxKind.RegionDirectiveTrivia)) {
						_Bar.View.SelectNode(Node, Keyboard.Modifiers != ModifierKeys.Control);
					}
					else {
						Node.GetIdentifierToken().GetLocation().GoToSource();
					}
					return;
				}

				var ct = _Bar._cancellationSource.GetToken();
				await CreateMenuForTypeSymbolNodeAsync(ct);

				_FilterBox.UpdateNumbers((Symbol as ITypeSymbol)?.GetMembers().Select(s => new SymbolItem(s, _Menu, false)));
				var footer = (TextBlock)_Menu.Footer;
				if (_PartialCount > 1) {
					footer.Append(ThemeHelper.GetImage(KnownImageIds.OpenDocumentFromCollection))
						.Append(_PartialCount);
				}
				if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.LineOfCode)) {
					footer.Append(ThemeHelper.GetImage(KnownImageIds.Code))
						.Append(Node.GetLineSpan().Length + 1);
				}
				_Bar.ShowMenu(this, _Menu);
				_FilterBox?.FocusFilterBox();
			}

			async Task CreateMenuForTypeSymbolNodeAsync(CancellationToken cancellationToken) {
				if (_Menu != null) {
					((TextBlock)_Menu.Footer).Clear();
					await RefreshItemsAsync(Node, cancellationToken);
					return;
				}
				_Menu = new SymbolList(_Bar._SemanticContext) {
					Container = _Bar.ListContainer,
					ContainerType = SymbolListType.NodeList,
					ExtIconProvider = s => GetExtIcons(s.SyntaxNode)
				};
				_Menu.Header = new WrapPanel {
					Orientation = Orientation.Horizontal,
					Children = {
							new ThemedButton(new ThemedMenuText(Node.GetDeclarationSignature(), true)
									.SetGlyph(ThemeHelper.GetImage(Node.GetImageId())), null,
									() => _Bar._SemanticContext.RelocateDeclarationNode(Node).GetLocation().GoToSource()) {
								BorderThickness = WpfHelper.TinyMargin,
								Margin = WpfHelper.SmallHorizontalMargin,
								Padding = WpfHelper.SmallHorizontalMargin,
							},
							(_FilterBox = new SymbolFilterBox(_Menu)),
						}
				};
				_Menu.Footer = new TextBlock { Margin = WpfHelper.MenuItemMargin }
					.ReferenceProperty(TextBlock.ForegroundProperty, EnvironmentColors.SystemGrayTextBrushKey);
				_Bar.SetupSymbolListMenu(_Menu);
				await AddItemsAsync(Node, cancellationToken);
				if (_Menu.Symbols.Count > 100) {
					_Menu.EnableVirtualMode = true;
				}
			}

			async Task AddItemsAsync(SyntaxNode node, CancellationToken cancellationToken) {
				AddMemberDeclarations(node, false);
				if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.PartialClassMember)
					&& (node as BaseTypeDeclarationSyntax).Modifiers.Any(SyntaxKind.PartialKeyword)) {
					await AddPartialTypeDeclarationsAsync(node as BaseTypeDeclarationSyntax, cancellationToken);
				}
			}

			async Task RefreshItemsAsync(SyntaxNode node, CancellationToken cancellationToken) {
				var sm = _Bar._SemanticContext.SemanticModel;
				await _Bar._SemanticContext.UpdateAsync(cancellationToken).ConfigureAwait(true);
				if (sm != _Bar._SemanticContext.SemanticModel) {
					_Menu.Clear();
					_Symbol = null;
					Node = _Bar._SemanticContext.RelocateDeclarationNode(Node);
					await AddItemsAsync(Node, cancellationToken);
					_Menu.RefreshItemsSource(true);
					return;
				}
				// select node item which contains caret
				var pos = _Bar.View.GetCaretPosition();
				foreach (var item in _Menu.Symbols) {
					if (item.Usage != SymbolUsageKind.Container) {
						if (item.IsExternal || cancellationToken.IsCancellationRequested
							|| item.SelectIfContainsPosition(pos)) {
							break;
						}
					}
				}
			}
			async Task AddPartialTypeDeclarationsAsync(BaseTypeDeclarationSyntax node, CancellationToken cancellationToken) {
				await _Bar._SemanticContext.UpdateAsync(cancellationToken).ConfigureAwait(true);
				var symbol = await _Bar._SemanticContext.GetSymbolAsync(node, cancellationToken).ConfigureAwait(true);
				if (symbol == null) {
					return;
				}
				var current = node.SyntaxTree;
				int c = 1;
				foreach (var item in symbol.DeclaringSyntaxReferences) {
					if (item.SyntaxTree == current || String.Equals(item.SyntaxTree.FilePath, current.FilePath, StringComparison.OrdinalIgnoreCase)) {
						continue;
					}
					var partial = await item.GetSyntaxAsync(cancellationToken);
					var i = _Menu.Add(partial);
					i.Location = item.SyntaxTree.GetLocation(item.Span);
					i.Content.Text = System.IO.Path.GetFileName(item.SyntaxTree.FilePath);
					i.Usage = SymbolUsageKind.Container;
					AddMemberDeclarations(partial, true);
					++c;
				}
				_PartialCount = c;
			}
			void AddMemberDeclarations(SyntaxNode node, bool isExternal) {
				const byte UNDEFINED = 0xFF, TRUE = 1, FALSE = 0;
				var directives = Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.Region)
					? node.GetDirectives(d => d.IsKind(SyntaxKind.RegionDirectiveTrivia) || d.IsKind(SyntaxKind.EndRegionDirectiveTrivia))
					: null;
				byte regionJustStart = UNDEFINED; // undefined, prevent #endregion show up on top of menu items
				bool selected = false;
				int pos = _Bar.View.GetCaretPosition();
				SyntaxNode lastNode = null;
				foreach (var child in node.ChildNodes()) {
					var childKind = child.Kind();
					if (childKind.IsMemberDeclaration() == false && childKind.IsTypeDeclaration() == false) {
						continue;
					}
					if (directives != null) {
						for (var i = 0; i < directives.Count; i++) {
							var d = directives[i];
							if (d.SpanStart < child.SpanStart) {
								if (d.IsKind(SyntaxKind.RegionDirectiveTrivia)) {
									if (lastNode == null || lastNode.Span.Contains(d.SpanStart) == false) {
										AddStartRegion(d, isExternal);
									}
									regionJustStart = TRUE;
								}
								else if (d.IsKind(SyntaxKind.EndRegionDirectiveTrivia)) {
									// don't show #endregion if preceeding item is #region
									if (regionJustStart == FALSE) {
										if (lastNode == null || lastNode.Span.Contains(d.SpanStart) == false) {
											var item = new SymbolItem(_Menu);
											_Menu.Add(item);
											item.Content
												.Append("#endregion ").Append(d.GetDeclarationSignature())
												.Foreground = ThemeHelper.SystemGrayTextBrush;
										}
									}
								}
								directives.RemoveAt(i);
								--i;
							}
						}
						if (directives.Count == 0) {
							directives = null;
						}
					}
					if (childKind == SyntaxKind.FieldDeclaration || childKind == SyntaxKind.EventFieldDeclaration) {
						AddVariables(((BaseFieldDeclarationSyntax)child).Declaration.Variables, isExternal, pos);
					}
					else {
						var i = _Menu.Add(child);
						if (isExternal) {
							i.Usage = SymbolUsageKind.External;
						}
						if (selected == false && i.SelectIfContainsPosition(pos)) {
							selected = true;
						}
						ShowNodeValue(i);
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.ParameterList)) {
							AddParameterList(i.Content, child);
						}
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.RegionInMember) == false) {
							lastNode = child;
						}
					}
					// a member is added between #region and #endregion
					regionJustStart = FALSE;
				}
				if (directives != null) {
					foreach (var item in directives) {
						if (item.IsKind(SyntaxKind.RegionDirectiveTrivia)
							&& (lastNode == null || lastNode.Span.Contains(item.SpanStart) == false)) {
							AddStartRegion(item, isExternal);
						}
					}
				}
			}

			void AddStartRegion(DirectiveTriviaSyntax d, bool isExternal) {
				var item = _Menu.Add(d);
				item.Hint = "#region";
				item.Content = SetHeader(d, false, false, false);
				if (isExternal) {
					item.Usage = SymbolUsageKind.External;
				}
			}

			void AddVariables(SeparatedSyntaxList<VariableDeclaratorSyntax> fields, bool isExternal, int pos) {
				foreach (var item in fields) {
					var i = _Menu.Add(item);
					if (isExternal) {
						i.Usage = SymbolUsageKind.External;
					}
					i.SelectIfContainsPosition(pos);
					ShowNodeValue(i);
				}
			}

			static StackPanel GetExtIcons(SyntaxNode node) {
				StackPanel icons = null;
				switch (node) {
					case BaseMethodDeclarationSyntax m:
						var p1 = m.ParameterList.Parameters.FirstOrDefault();
						var isExt = false;
						if (p1 != null && p1.Modifiers.Any(SyntaxKind.ThisKeyword)) {
							AddIcon(ref icons, KnownImageIds.ExtensionMethod);
							isExt = true;
						}
						foreach (var modifier in m.Modifiers) {
							switch (modifier.Kind()) {
								case SyntaxKind.AsyncKeyword: AddIcon(ref icons, KnownImageIds.DynamicGroup); break;
								case SyntaxKind.AbstractKeyword: AddIcon(ref icons, KnownImageIds.DialogTemplate); break;
								case SyntaxKind.StaticKeyword:
									if (isExt == false)
										AddIcon(ref icons, KnownImageIds.Link);
									break;
							}
						}
						break;
					case BasePropertyDeclarationSyntax p:
						foreach (var modifier in p.Modifiers) {
							switch (modifier.Kind()) {
								case SyntaxKind.StaticKeyword: AddIcon(ref icons, KnownImageIds.Link); break;
								case SyntaxKind.AbstractKeyword: AddIcon(ref icons, KnownImageIds.DialogTemplate); break;
							}
						}
						break;
					case BaseFieldDeclarationSyntax f:
						foreach (var modifier in f.Modifiers) {
							switch (modifier.Kind()) {
								case SyntaxKind.ReadOnlyKeyword: AddIcon(ref icons, KnownImageIds.EncapsulateField); break;
								case SyntaxKind.VolatileKeyword: AddIcon(ref icons, KnownImageIds.SetField); break;
								case SyntaxKind.StaticKeyword: AddIcon(ref icons, KnownImageIds.Link); break;
							}
						}
						break;
					case VariableDeclaratorSyntax v:
						return GetExtIcons(node.Parent.Parent);
					case BaseTypeDeclarationSyntax c:
						foreach (var modifier in c.Modifiers) {
							switch (modifier.Kind()) {
								case SyntaxKind.SealedKeyword: AddIcon(ref icons, KnownImageIds.ClassSealed); break;
								case SyntaxKind.AbstractKeyword: AddIcon(ref icons, KnownImageIds.AbstractClass); break;
								case SyntaxKind.StaticKeyword: AddIcon(ref icons, KnownImageIds.Link); break;
							}
						}
						break;
				}
				return icons;

				void AddIcon(ref StackPanel container, int imageId) {
					if (container == null) {
						container = new StackPanel { Orientation = Orientation.Horizontal };
					}
					container.Children.Add(ThemeHelper.GetImage(imageId));
				}
			}

			static void ShowNodeValue(SymbolItem item) {
				if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.FieldValue) == false) {
					return;
				}
				switch (item.SyntaxNode.Kind()) {
					case SyntaxKind.VariableDeclarator:
						item.Hint = ((VariableDeclaratorSyntax)item.SyntaxNode).Initializer?.Value?.ToString();
						break;
					case SyntaxKind.EnumMemberDeclaration:
						ShowEnumMemberValue(item);
						break;
					case SyntaxKind.PropertyDeclaration:
						ShowPropertyValue(item);
						break;
				}

				void ShowEnumMemberValue(SymbolItem enumItem) {
					var v = ((EnumMemberDeclarationSyntax)enumItem.SyntaxNode).EqualsValue;
					if (v != null) {
						enumItem.Hint = v.Value?.ToString();
					}
					else {
						enumItem.SetSymbolToSyntaxNode();
						enumItem.Hint = ((IFieldSymbol)enumItem.Symbol).ConstantValue?.ToString();
					}
				}

				void ShowPropertyValue(SymbolItem propertyItem) {
					var p = (PropertyDeclarationSyntax)propertyItem.SyntaxNode;
					if (p.Initializer != null) {
						propertyItem.Hint = p.Initializer.Value.ToString();
					}
					else if (p.ExpressionBody != null) {
						propertyItem.Hint = p.ExpressionBody.ToString();
					}
					else if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.AutoPropertyAnnotation)) {
						var a = p.AccessorList.Accessors;
						if (a.Count == 2) {
							if (a[0].Body == null && a[0].ExpressionBody == null && a[1].Body == null && a[1].ExpressionBody == null) {
								propertyItem.Hint = "{;;}";
							}
						}
						else if (a.Count == 1) {
							if (a[0].Body == null && a[0].ExpressionBody == null) {
								propertyItem.Hint = "{;}";
							}
						}
					}
				}
			}

			protected override void OnToolTipOpening(ToolTipEventArgs e) {
				base.OnToolTipOpening(e);
				if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.SymbolToolTip) == false) {
					ToolTip = null;
					return;
				}

				if (this.HasDummyToolTip()) {
					// todo: handle updated syntax node for RootItem
					if (Symbol != null) {
						var tip = ToolTipFactory.CreateToolTip(Symbol, true, _Bar._SemanticContext.SemanticModel.Compilation);
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.LineOfCode)) {
							tip.AddTextBlock()
						   .Append("Line of code: " + (Node.GetLineSpan().Length + 1));
						}
						ToolTip = tip;
					}
					else {
						ToolTip = Node.Kind().GetSyntaxBrief();
					}
					this.SetTipOptions();
				}
			}

			bool ISymbolFilter.Filter(int filterTypes) {
				return SymbolFilterBox.FilterByImageId((MemberFilterTypes)filterTypes, _ImageId);
			}
		}

		sealed class DocItem : ThemedImageButton
		{
			readonly CSharpBar _Bar;
			readonly ISymbol _SyntaxTree;

			public DocItem(CSharpBar bar, ISymbol syntaxTree)
				: base (KnownImageIds.GoToDefinition, new ThemedMenuText(syntaxTree.GetOriginalName())) {
				_Bar = bar;
				_SyntaxTree = syntaxTree;
				Opacity = 0.8;
			}

			protected override void OnClick() {
				base.OnClick();
				_SyntaxTree.GoToSource();
			}
		}
		sealed class GeometryAdornment : UIElement
		{
			readonly DrawingVisual _child;

			public GeometryAdornment(Color color, Geometry geometry, double thickness) {
				_child = new DrawingVisual();
				using (var context = _child.RenderOpen()) {
					context.DrawGeometry(new SolidColorBrush(color.Alpha(25)), thickness < 0.1 ? null : new Pen(ThemeHelper.MenuHoverBorderBrush, thickness), geometry);
				}
				AddVisualChild(_child);
			}

			protected override int VisualChildrenCount => 1;

			protected override Visual GetVisualChild(int index) {
				return _child;
			}
		}
	}
}
