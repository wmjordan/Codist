using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using R = Codist.Properties.Resources;
using Task = System.Threading.Tasks.Task;
using TH = Microsoft.VisualStudio.Shell.ThreadHelper;

namespace Codist.NaviBar
{
	public sealed class CSharpBar : NaviBar
	{
		internal const string SyntaxNodeRange = nameof(SyntaxNodeRange);
		static string __ProjectWideSearchExpression;

		IAdornmentLayer _SyntaxNodeRangeAdornment;
		SemanticContext _SemanticContext;

		CancellationTokenSource _cancellationSource = new CancellationTokenSource();
		RootItem _RootItem;
		GlobalNamespaceItem _GlobalNamespaceItem;
		NodeItem _MouseHoverItem;
		SymbolList _SymbolList;
		ThemedImageButton _ActiveItem;
		ITextBuffer _Buffer;

		public CSharpBar(IWpfTextView view) : base(view) {
			_SyntaxNodeRangeAdornment = View.GetAdornmentLayer(SyntaxNodeRange);
			Name = nameof(CSharpBar);
			BindView();
			Items.Add(_RootItem = new RootItem(this));
			Items.Add(_GlobalNamespaceItem = new GlobalNamespaceItem(this));
			Update(this, EventArgs.Empty);
			view.Closed += View_Closed;
		}

		protected override void OnMouseMove(MouseEventArgs e) {
			base.OnMouseMove(e);
			if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.RangeHighlight) == false) {
				return;
			}
			var a = _SyntaxNodeRangeAdornment;
			if (a == null) {
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
				if (a.IsEmpty == false) {
					a.RemoveAllAdornments();
				}
				var node = item.Node;
				if (node != null) {
					var span = node.Span.CreateSnapshotSpan(View.TextSnapshot);
					if (span.Length > 0) {
						try {
							HighlightNodeRanges(node, span);
						}
						catch (ObjectDisposedException) {
							// ignore
							_MouseHoverItem = null;
						}
					}
				}
				return;
			}
			if (a.IsEmpty == false) {
				a.RemoveAllAdornments();
				_MouseHoverItem = null;
			}
		}

		internal protected override void BindView() {
			if (_SemanticContext != null) {
				UnbindView();
			}
			_Buffer = View.TextBuffer;
			View.TextBuffer.Changed += TextBuffer_Changed;
			View.Selection.SelectionChanged += Update;
			ListContainer.ChildRemoved += ListContainer_MenuRemoved;
			Config.RegisterUpdateHandler(UpdateCSharpNaviBarConfig);
			SyncHelper.CancelAndDispose(ref _cancellationSource, true);
			_SemanticContext = SemanticContext.GetOrCreateSingetonInstance(View);
		}

		protected override void UnbindView() {
			View.Selection.SelectionChanged -= Update;
			View.TextBuffer.Changed -= TextBuffer_Changed;
			ListContainer.ChildRemoved -= ListContainer_MenuRemoved;
			Config.UnregisterUpdateHandler(UpdateCSharpNaviBarConfig);
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
			if (_SyntaxNodeRangeAdornment?.IsEmpty == false) {
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
			if (_cancellationSource != null) {
				try {
					await UpdateAsync(SyncHelper.CancelAndRetainToken(ref _cancellationSource)).ConfigureAwait(false);
				}
				catch (OperationCanceledException) {
					// ignore
				}
			}
		}

		async Task UpdateAsync(CancellationToken token) {
			var nodes = await UpdateModelAndGetContainingNodesAsync(token);
			await TH.JoinableTaskFactory.SwitchToMainThreadAsync(token);
			int ic = Items.Count, c = Math.Min(ic, nodes.Length);
			int i, i2;
			#region Remove outdated nodes on NaviBar
			const int FirstNodeIndex = 2; // exclude root and globalNs node
			for (i = FirstNodeIndex, i2 = 0; i < c && i2 < c; i2++) {
				if (token.IsCancellationRequested) {
					return;
				}
				CHECK_NODE:
				if (Items[i] is BarItem ni) {
					if (ni.Node == nodes[i2]) {
						// keep the item if corresponding node is not updated
						++i;
						continue;
					}
					else if (ni.ItemType == BarItemType.Namespace
						&& ni.Node.Kind().IsNamespaceDeclaration() == false) {
						if (++i < ic) {
							goto CHECK_NODE;
						}
					}
					if (ni.ItemType == BarItemType.Namespace) {
						i = FirstNodeIndex;
					}
				}
				break;
			}
			c = Items.Count;
			if (i == FirstNodeIndex && _RootItem.FilterText.Length == 0) {
				// clear type and namespace menu items if a type is changed
				_RootItem.ClearSymbolList();
			}
			// remove outdated nodes
			while (c > i) {
				Items.RemoveAndDisposeAt(--c);
			}
			#endregion
			#region Add updated nodes
			c = nodes.Length;
			NodeItem memberNode = null;
			while (i2 < c) {
				if (token.IsCancellationRequested) {
					return;
				}
				var node = nodes[i2];
				if (node.IsKind(SyntaxKind.NamespaceDeclaration)) {
					AddNamespaceNodes(node, ((NamespaceDeclarationSyntax)node).Name);
				}
				else if (node.IsKind(CodeAnalysisHelper.FileScopedNamespaceDeclaration)) {
					AddNamespaceNodes(node, node.GetFileScopedNamespaceDeclarationName());
				}
				else {
					var newItem = new NodeItem(this, node);
					if (memberNode == null && node.Kind().IsMemberDeclaration()) {
						memberNode = newItem;
						((TextBlock)newItem.Header).FontWeight = FontWeights.Bold;
						((TextBlock)newItem.Header).SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.FileTabSelectedTextBrushKey);
						newItem.IsChecked = true;
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.ReferencingTypes)) {
							newItem.ReferencedSymbols.AddRange(node.FindRelatedTypes(_SemanticContext.SemanticModel, token).Take(5));
						}
					}
					Items.Add(newItem);
				}
				++i2;
			}
			#endregion
			#region Add external referenced nodes in node range
			if (memberNode == null) {
				memberNode = Items.GetFirst<NodeItem>(n => n.HasReferencedSymbols);
			}
			if (memberNode != null && memberNode.HasReferencedSymbols) {
				foreach (var doc in memberNode.ReferencedSymbols) {
					Items.Add(new SymbolNodeItem(this, doc));
				}
			} 
			#endregion
		}

		void AddNamespaceNodes(SyntaxNode node, NameSyntax ns) {
			if (node.Parent.IsKind(SyntaxKind.NamespaceDeclaration) == false) {
				var nb = ImmutableArray.CreateBuilder<NameSyntax>();
				while (ns is QualifiedNameSyntax q) {
					ns = q.Left;
					nb.Add(ns);
				}
				for (var i = nb.Count - 1; i >= 0; i--) {
					Items.Add(new NamespaceItem(this, nb[i]));
				}
			}
			Items.Add(new NamespaceItem(this, node));
		}

		async Task<ImmutableArray<SyntaxNode>> UpdateModelAndGetContainingNodesAsync(CancellationToken token) {
			int position = View.GetCaretPosition();
			if (await _SemanticContext.UpdateAsync(position, token).ConfigureAwait(false) == false) {
				return ImmutableArray<SyntaxNode>.Empty;
			}
			var model = _SemanticContext.Model;
			if (model.Compilation == null) {
				return ImmutableArray<SyntaxNode>.Empty;
			}
			// if the caret is at the beginning of the document, move to the first type declaration
			if (position == 0) {
				var n = model.Compilation.GetDecendantDeclarations(token).FirstOrDefault();
				if (n != null) {
					position = n.SpanStart;
				}
			}
			await TH.JoinableTaskFactory.SwitchToMainThreadAsync(token);
			return _SemanticContext.GetContainingNodes(position, Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.SyntaxDetail), Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.RegionOnBar));
		}

		void UpdateCSharpNaviBarConfig(ConfigUpdatedEventArgs e) {
			if (e.UpdatedFeature == Features.NaviBar) {
				for (int i = Items.Count - 1; i > 0; i--) {
					if (Items[i] is GlobalNamespaceItem) {
						break;
					}
					Items.RemoveAndDisposeAt(i);
				}
				Update(this, EventArgs.Empty);
			}
		}

		#region Menu handler
		public override void ShowRootItemMenu(int parameter) {
			_RootItem.ShowNamespaceAndTypeMenu(parameter);
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
			ref var l = ref _SymbolList;
			if (l != menu) {
				ListContainer.Remove(l);
				l = menu;
				ListContainer.Add(menu);
				if (_ActiveItem != null) {
					_ActiveItem.IsHighlighted = false;
				}
			}
			_ActiveItem = barItem;
			barItem.IsHighlighted = true;
			menu.ItemsControlMaxHeight = ListContainer.DisplayHeight / 2;
			menu.RefreshItemsSource();
			menu.ScrollToSelectedItem();
			menu.PreviewKeyUp -= OnMenuKeyUp;
			menu.PreviewKeyUp += OnMenuKeyUp;
			PositionMenu();
			if (ListContainer.Contains(menu) == false) {
				ListContainer.Add(menu);
			}
		}

		void PositionMenu() {
			if (_SymbolList != null && _ActiveItem != null) {
				var x = _ActiveItem.TransformToVisual(_ActiveItem.GetParent<Grid>()).Transform(new Point()).X - View.VisualElement.TranslatePoint(new Point(), View.VisualElement.GetParent<Grid>()).X;
				ListContainer.Position(_SymbolList, new Point(x, -1), 30);
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
			var l = _SymbolList;
			if (l != null) {
				ListContainer.Remove(l);
			}
		}

		void ListContainer_MenuRemoved(object sender, AdornmentChildRemovedEventArgs e) {
			if (_SymbolList == e.RemovedElement) {
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
			if ((menu.SelectedItem as SymbolItem)?.GoToSource() == true) {
				HideMenu();
			}
		}

		void OnKeyboardSelectedItem(object sender, EventArgs args) {
			HideMenu();
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
				if (p is BaseTypeDeclarationSyntax bt) {
					t.Append(bt.Identifier.ValueText + ".", ThemeHelper.SystemGrayTextBrush);
				}
			}
			t.Append(title, highlight, false, SymbolFormatter.Instance.GetBrush(node));
			if (includeParameterList && Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.ParameterList)) {
				AddParameterList(t, node);
			}
			return t;
		}

		ISymbol GetChildSymbolOnNaviBar(BarItem item, CancellationToken cancellationToken) {
			var p = Items.IndexOf(item);
			return p != -1 && p < Items.Count - 1 ? (Items[p + 1] as ISymbolContainer)?.Symbol : null;
		}

		static void AddParameterList(TextBlock t, SyntaxNode node) {
			ParameterListSyntax p = null;
			if (node is BaseMethodDeclarationSyntax bm) {
				p = bm.ParameterList;
			}
			else if (node.IsKind(SyntaxKind.DelegateDeclaration)) {
				p = ((DelegateDeclarationSyntax)node).ParameterList;
			}
			else if (node is OperatorDeclarationSyntax o) {
				p = o.ParameterList;
			}
			if (p != null) {
				t.Append(p.GetParameterListSignature(Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.ParameterListShowParamName)), ThemeHelper.SystemGrayTextBrush);
			}
		}

		static void AddReturnType(SymbolItem i, SyntaxNode node) {
			if (String.IsNullOrEmpty(i.Hint)) {
				var t = node.GetMemberDeclarationType();
				if (t != null) {
					i.Hint = t.ToString();
				}
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

		void View_Closed(object sender, EventArgs e) {
			(sender as ITextView).Closed -= View_Closed;
			_SemanticContext = null;
			_Buffer = null;
			SyncHelper.CancelAndDispose(ref _cancellationSource, false);
			_SyntaxNodeRangeAdornment.RemoveAllAdornments();
			_SyntaxNodeRangeAdornment = null;
			if (_SymbolList != null) {
				DisposeSymbolList(_SymbolList);
				_SymbolList = null;
			}
			_ActiveItem = null;
			_GlobalNamespaceItem = null;
			_RootItem = null;
		}

		void DisposeSymbolList(SymbolList l) {
			if (l != null) {
				l.PreviewKeyUp -= OnMenuKeyUp;
				l.MouseLeftButtonUp -= MenuItemSelect;
				Controls.DragDropHelper.SetScrollOnDragDrop(l, false);
				ListContainer?.Remove(l);
				l.Dispose();
			}
		}

		sealed class RootItem : BarItem, IContextMenuHost
		{
			MemberFinderBox _FinderBox;
			SearchScopeBox _ScopeBox;
			TextBlock _Note;
			SymbolList _Menu;
			IReadOnlyCollection<ISymbol> _IncrementalSearchContainer;
			string _PreviousSearchKeywords;

			public RootItem(CSharpBar bar) : base(bar, IconIds.Search, new ThemedToolBarText()) {
				_Menu = new SymbolList(bar._SemanticContext) {
					Container = Bar.ListContainer,
					ContainerType = SymbolListType.NodeList,
					Header = new StackPanel {
						Margin = WpfHelper.MenuItemMargin,
						Children = {
							new Separator { Tag = new ThemedMenuText(R.CMD_SearchDeclaration) },
							new StackPanel {
								Orientation = Orientation.Horizontal,
								Children = {
									ThemeHelper.GetImage(IconIds.Search).WrapMargin(WpfHelper.GlyphMargin),
									(_FinderBox = new MemberFinderBox() { MinWidth = 150, ToolTip = new ThemedToolTip(R.CMD_SearchDeclaration, R.T_SearchMemberTip) }),
									(_ScopeBox = new SearchScopeBox {
										Contents = {
											new ThemedButton(IconIds.ClearFilter, R.CMD_ClearFilter, ClearFilter).ClearBorder()
										}
									}),
								}
							},
						}
					},
					Footer = _Note = new TextBlock { Margin = WpfHelper.MenuItemMargin }
						.ReferenceProperty(TextBlock.ForegroundProperty, EnvironmentColors.SystemGrayTextBrushKey),
					Owner = this
				};
				Controls.DragDropHelper.SetScrollOnDragDrop(_Menu, true);
				Bar.SetupSymbolListMenu(_Menu);
				_FinderBox.PreviewKeyDown += ChangeSearchScope;
				_FinderBox.TextChanged += SearchCriteriaChanged;
				_FinderBox.IsVisibleChanged += FinderBox_IsVisibleChanged;
				_ScopeBox.FilterChanged += SearchCriteriaChanged;
				_ScopeBox.FilterChanged += ScopeBox_FilterChanged;
			}

			public override BarItemType ItemType => BarItemType.Root;
			public string FilterText => _FinderBox.Text;

			public void ClearSymbolList() {
				_Menu.NeedsRefresh = true;
			}
			internal void SetText(string text) {
				((TextBlock)Header).Text = text;
			}

			protected override void OnClick() {
				base.OnClick();
				if (Bar._SymbolList == _Menu && _Menu.IsVisible) {
					Bar.HideMenu();
					return;
				}
				ShowNamespaceAndTypeMenu((int)ScopeType.Undefined);
			}

			internal void ShowNamespaceAndTypeMenu(int parameter) {
				if (_Menu.NeedsRefresh) {
					_Menu.NeedsRefresh = false;
					_Menu.ClearSymbols();
					_Menu.ItemsSource = null;
				}
				PopulateTypes();
				_Note.Clear();
				if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.LineOfCode)) {
					  _Note.Append(ThemeHelper.GetImage(IconIds.LineOfCode))
						.Append(Bar.View.TextSnapshot.LineCount);
				}
				Bar.ShowMenu(this, _Menu);
				switch ((ScopeType)parameter) {
					case ScopeType.ActiveDocument: _ScopeBox.Filter = ScopeType.ActiveDocument; break;
					case ScopeType.ActiveProject:
						_ScopeBox.Filter = ScopeType.ActiveProject;
						if (String.IsNullOrWhiteSpace(__ProjectWideSearchExpression) == false
							&& _FinderBox.Text != __ProjectWideSearchExpression) {
							SetAndSelectFinderText();
						}
						break;
				}
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
				int pos = Bar.View.GetCaretPosition();
				var symbols = _Menu.Symbols;
				for (int i = symbols.Count - 1; i >= 0; i--) {
					if (symbols[i].SelectIfContainsPosition(pos)) {
						return;
					}
				}
			}

			void AddNamespaceAndTypes() {
				foreach (var node in Bar._SemanticContext.Compilation.ChildNodes()) {
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

			async void SearchCriteriaChanged(object sender, EventArgs e) {
				SyncHelper.CancelAndDispose(ref Bar._cancellationSource, true);
				var ct = Bar._cancellationSource.GetToken();
				try {
					await TH.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
					_Menu.ItemsSource = null;
					_Menu.ClearSymbols();
					var s = _FinderBox.Text.Trim();
					if (s.Length == 0) {
						_Menu.ContainerType = SymbolListType.NodeList;
						ShowNamespaceAndTypeMenu((int)ScopeType.Undefined);
						_IncrementalSearchContainer = null;
						_PreviousSearchKeywords = null;
						if (String.IsNullOrWhiteSpace(__ProjectWideSearchExpression) == false && sender == _ScopeBox) {
							SetAndSelectFinderText();
						}
						else if (_ScopeBox.Filter != ScopeType.ActiveDocument) {
							__ProjectWideSearchExpression = String.Empty;
						}
						return;
					}
					_Menu.ContainerType = SymbolListType.None;
					switch (_ScopeBox.Filter) {
						case ScopeType.ActiveDocument:
							FindInDocument(s, ct);
							break;
						case ScopeType.ActiveProject:
							__ProjectWideSearchExpression = s;
							await FindInProjectAsync(s, ct);
							break;
					}
					await TH.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
					_Menu.RefreshItemsSource();
					_Menu.UpdateLayout();
				}
				catch (OperationCanceledException) {
					// ignores cancellation
				}
				catch (ObjectDisposedException) { }
			}
			void SetAndSelectFinderText() {
				_FinderBox.Text = __ProjectWideSearchExpression;
				_FinderBox.CaretIndex = _FinderBox.Text.Length;
				_FinderBox.SelectAll();
			}
			void ChangeSearchScope(object sender, KeyEventArgs e) {
				if (Keyboard.Modifiers == ModifierKeys.None) {
					if (e.Key == Key.OemPlus || e.Key == Key.Add) {
						_ScopeBox.Filter = ScopeType.ActiveProject;
						e.Handled = true;
					}
					else if (e.Key == Key.OemMinus || e.Key == Key.Subtract) {
						_ScopeBox.Filter = ScopeType.ActiveDocument;
						e.Handled = true;
					}
				}
			}
			void FindInDocument(string text, CancellationToken token) {
				var filter = CodeAnalysisHelper.CreateNameFilter(text, false, Char.IsUpper(text[0]));
				foreach (var item in Bar._SemanticContext.Compilation.GetDecendantDeclarations(token)) {
					if (filter(item.GetDeclarationSignature())) {
						var i = _Menu.Add(item);
						i.Content = SetHeader(item, true, false, true);
					}
				}
			}
			async Task FindInProjectAsync(string text, CancellationToken token) {
				await FindDeclarationsAsync(text, token);
			}

			async Task FindDeclarationsAsync(string symbolName, CancellationToken token) {
				const int MaxResultLimit = 500;
				IReadOnlyCollection<ISymbol> result;
				if (_PreviousSearchKeywords != null
					&& symbolName.StartsWith(_PreviousSearchKeywords)
					&& _IncrementalSearchContainer?.Count < MaxResultLimit) {
					var filter = CodeAnalysisHelper.CreateNameFilter(symbolName, false, Char.IsUpper(symbolName[0]));
					result = _IncrementalSearchContainer.Where(i => filter(i.Name)).ToList();
				}
				else {
					// todo find async, sort later, incrementally
					_IncrementalSearchContainer = result = await Bar._SemanticContext.Document.Project.FindDeclarationsAsync(symbolName, MaxResultLimit, false, Char.IsUpper(symbolName[0]), SymbolFilter.All, token).ConfigureAwait(false);
					_PreviousSearchKeywords = symbolName;
				}
				int c = 0;
				foreach (var item in result) {
					if (token.IsCancellationRequested || ++c > 50) {
						break;
					}
					_Menu.Add(item, true);
				}
			}

			void ScopeBox_FilterChanged(object sender, EventArgs e) {
				_FinderBox.Focus();
			}

			void FinderBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args) {
				if ((bool)args.NewValue == false) {
					_IncrementalSearchContainer = null;
					_PreviousSearchKeywords = null;
				}
				else if (_ScopeBox.Filter != ScopeType.ActiveDocument
					&& _FinderBox.Text != __ProjectWideSearchExpression) {
					SetAndSelectFinderText();
				}
			}

			void IContextMenuHost.ShowContextMenu(RoutedEventArgs args) {
				ShowNamespaceAndTypeMenu((int)ScopeType.Undefined);
			}

			public override void Dispose() {
				if (_FinderBox != null) {
					if (_Menu != null) {
						Bar.DisposeSymbolList(_Menu);
						_Menu = null;
					}
					base.Dispose();
					_IncrementalSearchContainer = null;
					_FinderBox.PreviewKeyDown -= ChangeSearchScope;
					_FinderBox.TextChanged -= SearchCriteriaChanged;
					_FinderBox.IsVisibleChanged -= FinderBox_IsVisibleChanged;
					_FinderBox = null;
					_ScopeBox.FilterChanged -= SearchCriteriaChanged;
					_ScopeBox.FilterChanged -= ScopeBox_FilterChanged;
					_ScopeBox = null;
					_Note = null;
					DataContext = null;
				}
			}

			sealed class MemberFinderBox : ThemedTextBox
			{
				public MemberFinderBox() {
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

		sealed class GlobalNamespaceItem : BarItem
		{
			SymbolList _Menu;
			SymbolFilterBox _FilterBox;

			public GlobalNamespaceItem(CSharpBar bar) : base(bar, IconIds.GlobalNamespace, new ThemedToolBarText()) {
				Click += HandleClick;
				this.UseDummyToolTip();
			}

			public override BarItemType ItemType => BarItemType.GlobalNamespace;

			async void HandleClick(object sender, RoutedEventArgs e) {
				SyncHelper.CancelAndDispose(ref Bar._cancellationSource, true);
				if (_Menu != null && Bar._SymbolList == _Menu && _Menu.IsVisible) {
					Bar.HideMenu();
					return;
				}
				var ct = Bar._cancellationSource.GetToken();
				try {
					await CreateMenuForGlobalNamespaceNodeAsync(ct);
					_FilterBox.UpdateNumbers(_Menu.Symbols);
					Bar.ShowMenu(this, _Menu);
				}
				catch (OperationCanceledException) {
					// ignore
				}
			}

			async Task CreateMenuForGlobalNamespaceNodeAsync(CancellationToken cancellationToken) {
				if (_Menu != null) {
					((TextBlock)_Menu.Footer).Clear();
					Controls.DragDropHelper.SetScrollOnDragDrop(_Menu, false);
					await RefreshItemsAsync(cancellationToken);
					return;
				}

				_Menu = new SymbolList(Bar._SemanticContext) {
					Container = Bar.ListContainer,
					ContainerType = SymbolListType.TypeList,
					ExtIconProvider = ExtIconProvider.Default.GetExtIcons,
					EnableVirtualMode = true,
					Owner = this
				};
				Controls.DragDropHelper.SetScrollOnDragDrop(_Menu, true);
				if (_FilterBox != null) {
					_FilterBox.FilterChanged -= FilterChanged;
				}
				_Menu.Header = _FilterBox = new SymbolFilterBox(_Menu);
				_FilterBox.FilterChanged += FilterChanged;
				_Menu.Footer = new TextBlock { Margin = WpfHelper.MenuItemMargin }
					.ReferenceProperty(TextBlock.ForegroundProperty, EnvironmentColors.SystemGrayTextBrushKey);
				Bar.SetupSymbolListMenu(_Menu);
				await Bar._SemanticContext.UpdateAsync(cancellationToken).ConfigureAwait(true);
				var d = Bar._SemanticContext.Document;
				if (d != null) {
					var items = await Bar._SemanticContext.GetNamespacesAndTypesAsync((await d.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false)).GlobalNamespace, cancellationToken).ConfigureAwait(false);
					await TH.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
					_Menu.AddNamespaceItems(items, Bar.GetChildSymbolOnNaviBar(this, cancellationToken));
				}
			}

			void FilterChanged(object sender, SymbolFilterBox.FilterEventArgs e) {
				if (e.FilterText.Length == 0) {
					SelectChild(default);
				}
			}

			async Task RefreshItemsAsync(CancellationToken cancellationToken) {
				var ctx = Bar._SemanticContext;
				var sm = ctx.SemanticModel;
				await ctx.UpdateAsync(cancellationToken).ConfigureAwait(true);
				if (sm == ctx.SemanticModel) {
					SelectChild(cancellationToken);
					return;
				}
				_Menu.ClearSymbols();
				var d = ctx.Document;
				if (d != null) {
					var items = await ctx.GetNamespacesAndTypesAsync((await d.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false)).GlobalNamespace, cancellationToken).ConfigureAwait(false);
					await TH.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
					_Menu.AddNamespaceItems(items, Bar.GetChildSymbolOnNaviBar(this, cancellationToken));
				}
				_Menu.RefreshItemsSource(true);
			}

			void SelectChild(CancellationToken cancellationToken) {
				var child = Bar.GetChildSymbolOnNaviBar(this, cancellationToken);
				if (child != null && _Menu.HasItems) {
					var c = CodeAnalysisHelper.GetSpecificSymbolComparer(child);
					_Menu.SelectedItem = _Menu.Symbols.FirstOrDefault(s => c(s.Symbol));
				}
			}

			public override void Dispose() {
				if (_Menu != null) {
					Bar.DisposeSymbolList(_Menu);
					_Menu = null;
				}
				base.Dispose();
				Click -= HandleClick;
				if (_FilterBox != null) {
					_FilterBox.FilterChanged -= FilterChanged;
					_FilterBox = null;
				}
				DataContext = null;
			}
		}

		sealed class NamespaceItem : BarItem, IContextMenuHost, ISymbolContainer
		{
			SymbolList _Menu;
			SymbolFilterBox _FilterBox;
			ISymbol _Symbol;

			public NamespaceItem(CSharpBar bar, SyntaxNode node) : base(bar, IconIds.Namespace, new ThemedToolBarText()) {
				Node = node;
				_Symbol = SyncHelper.RunSync(() => Bar._SemanticContext.GetSymbolAsync(node, Bar._cancellationSource.GetToken()));
				((TextBlock)Header).Text = _Symbol.Name;
				Click += HandleClick;
				this.UseDummyToolTip();
			}

			public override BarItemType ItemType => BarItemType.Namespace;
			public bool IsSymbolNode { get; }
			public ISymbol Symbol => _Symbol;

			async void HandleClick(object sender, RoutedEventArgs e) {
				SyncHelper.CancelAndDispose(ref Bar._cancellationSource, true);
				if (_Menu != null && Bar._SymbolList == _Menu && _Menu.IsVisible) {
					Bar.HideMenu();
					return;
				}
				var ct = Bar._cancellationSource.GetToken();
				try {
					await CreateMenuForNamespaceNodeAsync(ct);
					await TH.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
					_FilterBox.UpdateNumbers(_Menu.Symbols);
					Bar.ShowMenu(this, _Menu);
				}
				catch (OperationCanceledException) {
					// ignore
				}
			}

			async Task CreateMenuForNamespaceNodeAsync(CancellationToken cancellationToken) {
				if (_Menu != null) {
					((TextBlock)_Menu.Footer).Clear();
					Controls.DragDropHelper.SetScrollOnDragDrop(_Menu, false);
					await RefreshItemsAsync(cancellationToken);
					return;
				}
				_Menu = new SymbolList(Bar._SemanticContext) {
					Container = Bar.ListContainer,
					ContainerType = SymbolListType.TypeList,
					ExtIconProvider = ExtIconProvider.Default.GetExtIcons,
					EnableVirtualMode = true,
					Owner = this
				};
				Controls.DragDropHelper.SetScrollOnDragDrop(_Menu, true);
				if (_FilterBox != null) {
					_FilterBox.FilterChanged -= FilterChanged;
				}
				_Menu.Header = new WrapPanel {
					Orientation = Orientation.Horizontal,
					Children = {
							new Border {
								Child = new ThemedMenuText(_Symbol.Name, true).SetGlyph(ThemeHelper.GetImage(IconIds.Namespace)),
								BorderThickness = WpfHelper.TinyMargin,
								Margin = WpfHelper.SmallHorizontalMargin,
								Padding = WpfHelper.SmallHorizontalMargin,
							},
							(_FilterBox = new SymbolFilterBox(_Menu)),
						}
				};
				_FilterBox.FilterChanged += FilterChanged;
				_Menu.Footer = new TextBlock { Margin = WpfHelper.MenuItemMargin }
					.ReferenceProperty(TextBlock.ForegroundProperty, EnvironmentColors.SystemGrayTextBrushKey);
				Bar.SetupSymbolListMenu(_Menu);
				await Bar._SemanticContext.UpdateAsync(cancellationToken).ConfigureAwait(false);
				var items = await Bar._SemanticContext.GetNamespacesAndTypesAsync(_Symbol as INamespaceSymbol, cancellationToken).ConfigureAwait(false);
				await TH.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
				_Menu.AddNamespaceItems(items, Bar.GetChildSymbolOnNaviBar(this, cancellationToken));
			}

			void FilterChanged(object sender, SymbolFilterBox.FilterEventArgs e) {
				if (e.FilterText.Length == 0) {
					SelectChild(default);
				}
			}

			async Task RefreshItemsAsync(CancellationToken cancellationToken) {
				var ctx = Bar._SemanticContext;
				var sm = ctx.SemanticModel;
				await ctx.UpdateAsync(cancellationToken).ConfigureAwait(false);
				if (sm != ctx.SemanticModel) {
					_Menu.ClearSymbols();
					_Symbol = await ctx.RelocateSymbolAsync(_Symbol, cancellationToken).ConfigureAwait(false);
					//_Node = Bar._SemanticContext.RelocateDeclarationNode(_Node);
					var items = await ctx.GetNamespacesAndTypesAsync(_Symbol as INamespaceSymbol, cancellationToken).ConfigureAwait(false);
					await TH.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
					_Menu.AddNamespaceItems(items, Bar.GetChildSymbolOnNaviBar(this, cancellationToken));
					_Menu.RefreshItemsSource(true);
				}
				else {
					SelectChild(cancellationToken);
				}
			}

			void SelectChild(CancellationToken cancellationToken) {
				var child = Bar.GetChildSymbolOnNaviBar(this, cancellationToken);
				if (child != null && _Menu.HasItems) {
					var c = CodeAnalysisHelper.GetSpecificSymbolComparer(child);
					_Menu.SelectedItem = _Menu.Symbols.FirstOrDefault(s => c(s.Symbol));
				}
			}

			void IContextMenuHost.ShowContextMenu(RoutedEventArgs args) {
				if (ContextMenu == null) {
					var m = new CSharpSymbolContextMenu(Symbol, null, Bar._SemanticContext);
					var s = Symbol;
					if (s != null) {
						m.AddAnalysisCommands();
						m.AddCopyAndSearchSymbolCommands();
						m.AddTitleItem(s.Name);
					}
					ContextMenu = m;
				}
				ContextMenu.IsOpen = true;
			}

			public override void Dispose() {
				if (Node != null) {
					if (_Menu != null) {
						Bar.DisposeSymbolList(_Menu);
						_Menu = null;
					}
					base.Dispose();
					Click -= HandleClick;
					_Symbol = null;
					if (_FilterBox != null) {
						_FilterBox.FilterChanged -= FilterChanged;
						_FilterBox = null;
					}
					if (ContextMenu is IDisposable d) {
						d.Dispose();
						ContextMenu = null;
					}
					DataContext = null;
				}
			}
		}

		sealed class NodeItem : BarItem, ISymbolFilter, IContextMenuHost, ISymbolContainer
		{
			readonly int _ImageId;
			SymbolList _Menu;
			SymbolFilterBox _FilterBox;
			int _PartialCount;
			ISymbol _Symbol;
			List<ISymbol> _ReferencedSymbols;

			public NodeItem(CSharpBar bar, SyntaxNode node)
				: base (bar, node.GetImageId(), new ThemedMenuText(node.GetDeclarationSignature() ?? String.Empty)) {
				_ImageId = node.GetImageId();
				Node = node;
				Click += HandleClick;
				this.UseDummyToolTip();
			}

			public override BarItemType ItemType => BarItemType.Node;
			public bool IsSymbolNode => false;
			public ISymbol Symbol => _Symbol ?? (_Symbol = SyncHelper.RunSync(() => Bar._SemanticContext.GetSymbolAsync(Node, Bar._cancellationSource.GetToken())));
			public bool HasReferencedSymbols => _ReferencedSymbols != null && _ReferencedSymbols.Count > 0;
			public List<ISymbol> ReferencedSymbols => _ReferencedSymbols ?? (_ReferencedSymbols = new List<ISymbol>());

			public void ShowContextMenu(RoutedEventArgs args) {
				if (ContextMenu == null) {
					var m = new CSharpSymbolContextMenu(Symbol, Node, Bar._SemanticContext);
					m.AddNodeCommands();
					var s = Symbol;
					if (s != null) {
						m.Items.Add(new Separator());
						m.AddAnalysisCommands();
						m.AddCopyAndSearchSymbolCommands();
						m.AddTitleItem(Node.GetDeclarationSignature());
					}
					ContextMenu = m;
				}
				ContextMenu.IsOpen = true;
			}

			async void HandleClick(object sender, RoutedEventArgs e) {
				SyncHelper.CancelAndDispose(ref Bar._cancellationSource, true);
				if (_Menu != null && Bar._SymbolList == _Menu && _Menu.IsVisible) {
					Bar.HideMenu();
					return;
				}
				if (Node.IsKind(SyntaxKind.RegionDirectiveTrivia) && (Node.FirstAncestorOrSelf<MemberDeclarationSyntax>()?.Span.Contains(Node.Span)) != true
					|| Node.Kind().IsNonDelegateTypeDeclaration()) {
					// displays member list for type declarations or regions outside of member declaration
					var ct = Bar._cancellationSource.GetToken();
					try {
						await CreateMenuForTypeSymbolNodeAsync(ct);
						await TH.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

						_FilterBox.UpdateNumbers((Symbol as ITypeSymbol)?.FindMembers().Select(s => new SymbolItem(s, null, false)) ?? Enumerable.Empty<SymbolItem>());
						var footer = (TextBlock)_Menu.Footer;
						if (_PartialCount > 1) {
							footer.Append(ThemeHelper.GetImage(IconIds.PartialDocumentCount))
								.Append(_PartialCount);
						}
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.LineOfCode)) {
							footer.Append(ThemeHelper.GetImage(IconIds.LineOfCode))
								.Append(Node.GetLineSpan().Length + 1);
						}
						Bar.ShowMenu(this, _Menu);
						_FilterBox?.FocusFilterBox();
					}
					catch (OperationCanceledException) {
						// ignore
					}
					return;
				}

				var span = Node.FullSpan;
				if (span.Contains(Bar._SemanticContext.Position) && Node.SyntaxTree.FilePath == Bar._SemanticContext.Document.FilePath
					|| Node.IsKind(SyntaxKind.RegionDirectiveTrivia)) {
					// Hack: since SelectNode will move the cursor to the end of the span--the beginning of next node,
					//    it will make next node selected, which is undesired in most cases
					Bar.View.Selection.SelectionChanged -= Bar.Update;
					Bar.View.SelectNode(Node, Keyboard.Modifiers != ModifierKeys.Control);
					Bar.View.Selection.SelectionChanged += Bar.Update;
				}
				else {
					Node.GetIdentifierToken().GetLocation().GoToSource();
				}
			}

			async Task CreateMenuForTypeSymbolNodeAsync(CancellationToken cancellationToken) {
				if (_Menu != null) {
					((TextBlock)_Menu.Footer).Clear();
					Controls.DragDropHelper.SetScrollOnDragDrop(_Menu, false);
					await RefreshItemsAsync(Node, cancellationToken);
					return;
				}
				_Menu = new SymbolList(Bar._SemanticContext) {
					Container = Bar.ListContainer,
					ContainerType = SymbolListType.NodeList,
					ExtIconProvider = s => GetExtIcons(s.SyntaxNode),
					Owner = this
				};
				Controls.DragDropHelper.SetScrollOnDragDrop(_Menu, true);
				_Menu.Header = new WrapPanel {
					Orientation = Orientation.Horizontal,
					Children = {
							new ThemedButton(new ThemedMenuText(Node.GetDeclarationSignature(), true)
									.SetGlyph(ThemeHelper.GetImage(Node.GetImageId())), null,
									() => Bar._SemanticContext.RelocateDeclarationNode(Node).GetLocation().GoToSource()) {
								BorderThickness = WpfHelper.TinyMargin,
								Margin = WpfHelper.SmallHorizontalMargin,
								Padding = WpfHelper.SmallHorizontalMargin,
							},
							(_FilterBox = new SymbolFilterBox(_Menu)),
						}
				};
				_Menu.Footer = new TextBlock { Margin = WpfHelper.MenuItemMargin }
					.ReferenceProperty(TextBlock.ForegroundProperty, EnvironmentColors.SystemGrayTextBrushKey);
				Bar.SetupSymbolListMenu(_Menu);
				await AddItemsAsync(Node, cancellationToken);
				if (_Menu.Symbols.Count > 100) {
					_Menu.EnableVirtualMode = true;
				}
			}

			async Task AddItemsAsync(SyntaxNode node, CancellationToken cancellationToken) {
				AddMemberDeclarations(node, false, true);
				if (node.IsKind(SyntaxKind.RegionDirectiveTrivia)) {
					return;
				}
				var externals = (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.PartialClassMember)
					&& (node as BaseTypeDeclarationSyntax).Modifiers.Any(SyntaxKind.PartialKeyword) ? MemberListOptions.ShowPartial : 0)
					| (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.BaseClassMember) && (node.IsKind(SyntaxKind.ClassDeclaration) || node.IsKind(CodeAnalysisHelper.RecordDeclaration)) ? MemberListOptions.ShowBase : 0);
				ISymbol symbol;
				if (externals != 0) {
					await Bar._SemanticContext.UpdateAsync(cancellationToken).ConfigureAwait(true);
					symbol = await Bar._SemanticContext.GetSymbolAsync(node, cancellationToken).ConfigureAwait(true);
					if (symbol == null) {
						return;
					}
					if (externals.MatchFlags(MemberListOptions.ShowPartial)) {
						await AddPartialTypeDeclarationsAsync(node as BaseTypeDeclarationSyntax, symbol, cancellationToken);
					}
					if (externals.MatchFlags(MemberListOptions.ShowBase) && symbol.Kind == SymbolKind.NamedType) {
						await AddBaseTypeDeclarationsAsync(symbol as INamedTypeSymbol, cancellationToken);
					}
				}
			}

			async Task RefreshItemsAsync(SyntaxNode node, CancellationToken cancellationToken) {
				var sm = Bar._SemanticContext.SemanticModel;
				await Bar._SemanticContext.UpdateAsync(cancellationToken).ConfigureAwait(true);
				if (sm != Bar._SemanticContext.SemanticModel) {
					_Menu.ClearSymbols();
					_Symbol = null;
					Node = Bar._SemanticContext.RelocateDeclarationNode(Node);
					await AddItemsAsync(Node, cancellationToken);
					_Menu.RefreshItemsSource(true);
					return;
				}
				// select node item which contains caret
				var pos = Bar.View.GetCaretPosition();
				foreach (var item in _Menu.Symbols) {
					if (item.Usage != SymbolUsageKind.Container) {
						if (item.IsExternal || cancellationToken.IsCancellationRequested
							|| item.SelectIfContainsPosition(pos)) {
							break;
						}
					}
				}
			}
			async Task AddPartialTypeDeclarationsAsync(BaseTypeDeclarationSyntax node, ISymbol symbol, CancellationToken cancellationToken) {
				var current = node.SyntaxTree;
				int c = 1;
				foreach (var item in symbol.DeclaringSyntaxReferences) {
					if (item.SyntaxTree == current || String.Equals(item.SyntaxTree.FilePath, current.FilePath, StringComparison.OrdinalIgnoreCase)) {
						continue;
					}
					await AddExternalNodesAsync(item, null, true, cancellationToken);
					++c;
				}
				_PartialCount = c;
			}
			async Task AddBaseTypeDeclarationsAsync(INamedTypeSymbol symbol, CancellationToken cancellationToken) {
				while ((symbol = symbol.BaseType) != null && symbol.HasSource()) {
					foreach (var item in symbol.DeclaringSyntaxReferences) {
						await AddExternalNodesAsync(item, symbol.GetTypeName(), false, cancellationToken);
					}
				}
			}

			async Task AddExternalNodesAsync(SyntaxReference item, string textOverride, bool includeDirectives, CancellationToken cancellationToken) {
				var externalNode = await item.GetSyntaxAsync(cancellationToken);
				var i = _Menu.Add(externalNode);
				i.Location = item.SyntaxTree.GetLocation(item.Span);
				i.Content.Text = textOverride ?? System.IO.Path.GetFileName(item.SyntaxTree.FilePath);
				i.Usage = SymbolUsageKind.Container;
				AddMemberDeclarations(externalNode, true, includeDirectives);
			}

			void AddMemberDeclarations(SyntaxNode node, bool isExternal, bool includeDirectives) {
				const byte UNDEFINED = 0xFF, TRUE = 1, FALSE = 0;
				IEnumerable<SyntaxNode> scope;
				if (node.IsKind(SyntaxKind.RegionDirectiveTrivia)) {
					var span = node.GetSematicSpan(true).ToTextSpan();
					scope = node.FirstAncestorOrSelf<SyntaxNode>(n => n.Span.Contains(span), true).ChildNodes().Where(n => span.Contains(n.SpanStart));
					includeDirectives = false;
				}
				else {
					scope = node.ChildNodes();
				}
				var directives = includeDirectives && Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.Region)
					? node.GetDirectives(d => d.IsKind(SyntaxKind.RegionDirectiveTrivia) || d.IsKind(SyntaxKind.EndRegionDirectiveTrivia))
					: null;
				byte regionJustStart = UNDEFINED; // undefined, prevent #endregion show up on top of menu items
				bool selected = false;
				int pos = Bar.View.GetCaretPosition();
				SyntaxNode lastNode = null;
				foreach (var child in scope) {
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
						else if (selected == false && i.SelectIfContainsPosition(pos)) {
							selected = true;
						}
						ShowNodeValue(i);
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.ParameterList)) {
							AddParameterList(i.Content, child);
						}
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.MemberType)) {
							AddReturnType(i, child);
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
					if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.MemberType)) {
						AddReturnType(i, item);
					}
				}
			}

			static StackPanel GetExtIcons(SyntaxNode node) {
				switch (node) {
					case BaseMethodDeclarationSyntax m:
						return GetMethodExtIcons(m);
					case BasePropertyDeclarationSyntax p:
						return GetPropertyExtIcons(p);
					case BaseFieldDeclarationSyntax f:
						return GetFieldExtIcons(f);
					case VariableDeclaratorSyntax v:
						return GetExtIcons(node.Parent.Parent);
					case BaseTypeDeclarationSyntax c:
						return GetTypeExtIcons(c);
				}
				return null;

				StackPanel GetMethodExtIcons(BaseMethodDeclarationSyntax m) {
					StackPanel icons = null;
					var isExt = false;
					if (m.ParameterList.Parameters.FirstOrDefault()?.Modifiers.Any(SyntaxKind.ThisKeyword) == true) {
						AddIcon(ref icons, IconIds.ExtensionMethod);
						isExt = true;
					}
					foreach (var modifier in m.Modifiers) {
						switch (modifier.Kind()) {
							case SyntaxKind.AsyncKeyword: AddIcon(ref icons, IconIds.AsyncMember); break;
							case SyntaxKind.AbstractKeyword: AddIcon(ref icons, IconIds.AbstractMember); break;
							case SyntaxKind.StaticKeyword:
								if (isExt == false) {
									AddIcon(ref icons, IconIds.StaticMember);
								}
								break;
							case SyntaxKind.UnsafeKeyword: AddIcon(ref icons, IconIds.Unsafe); break;
							case SyntaxKind.SealedKeyword: AddIcon(ref icons, IconIds.SealedMethod); break;
							case SyntaxKind.OverrideKeyword: AddIcon(ref icons, IconIds.OverrideMethod); break;
							case SyntaxKind.ReadOnlyKeyword: AddIcon(ref icons, IconIds.ReadonlyMethod); break;
						}
					}
					return icons;
				}

				StackPanel GetPropertyExtIcons(BasePropertyDeclarationSyntax p) {
					StackPanel icons = null;
					foreach (var modifier in p.Modifiers) {
						switch (modifier.Kind()) {
							case SyntaxKind.StaticKeyword: AddIcon(ref icons, IconIds.StaticMember); break;
							case SyntaxKind.AbstractKeyword: AddIcon(ref icons, IconIds.AbstractMember); break;
							case SyntaxKind.SealedKeyword:
								AddIcon(ref icons, p.IsKind(SyntaxKind.EventDeclaration) ? IconIds.SealedEvent : IconIds.SealedProperty);
								break;
							case SyntaxKind.OverrideKeyword:
								AddIcon(ref icons, p.IsKind(SyntaxKind.EventDeclaration) ? IconIds.OverrideEvent : IconIds.OverrideProperty);
								break;
							case SyntaxKind.ReadOnlyKeyword: AddIcon(ref icons, IconIds.ReadonlyMethod); break;
							case SyntaxKind.RefKeyword: AddIcon(ref icons, IconIds.RefMember); break;
						}
					}
					if (p.Type is RefTypeSyntax r) {
						AddIcon(ref icons, IconIds.RefMember);
						if (r.ReadOnlyKeyword.Parent != null) {
							AddIcon(ref icons, IconIds.ReadonlyProperty);
						}
					}
					if (p.AccessorList != null) {
						var a = p.AccessorList.Accessors;
						if (a.Count == 2) {
							if (a.Any(i => i.RawKind == (int)CodeAnalysisHelper.InitAccessorDeclaration)) {
								AddIcon(ref icons, IconIds.InitonlyProperty);
							}
							else if (a[0].Body == null && a[0].ExpressionBody == null && a[1].Body == null && a[1].ExpressionBody == null) {
								AddIcon(ref icons, IconIds.AutoProperty);
								return icons;
							}
						}
						else if (a.Count == 1) {
							if (a[0].Body == null && a[0].ExpressionBody == null) {
								AddIcon(ref icons, IconIds.ReadonlyProperty);
								return icons;
							}
						}
						if (a.Any(i => i.Keyword.IsKind(SyntaxKind.GetKeyword) && i.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))) {
							AddIcon(ref icons, IconIds.ReadonlyMethod);
						}
					}
					return icons;
				}

				StackPanel GetFieldExtIcons(BaseFieldDeclarationSyntax f) {
					StackPanel icons = null;
					foreach (var modifier in f.Modifiers) {
						switch (modifier.Kind()) {
							case SyntaxKind.ReadOnlyKeyword: AddIcon(ref icons, IconIds.ReadonlyField); break;
							case SyntaxKind.VolatileKeyword: AddIcon(ref icons, IconIds.VolatileField); break;
							case SyntaxKind.StaticKeyword: AddIcon(ref icons, IconIds.StaticMember); break;
						}
					}
					return icons;
				}

				StackPanel GetTypeExtIcons(BaseTypeDeclarationSyntax c) {
					StackPanel icons = null;
					foreach (var modifier in c.Modifiers) {
						switch (modifier.Kind()) {
							case SyntaxKind.StaticKeyword: AddIcon(ref icons, IconIds.StaticMember); break;
							case SyntaxKind.AbstractKeyword: AddIcon(ref icons, IconIds.AbstractClass); break;
							case SyntaxKind.SealedKeyword: AddIcon(ref icons, IconIds.SealedClass); break;
							case SyntaxKind.ReadOnlyKeyword: AddIcon(ref icons, IconIds.ReadonlyType); break;
						}
					}
					return icons;
				}

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
						ShowVariableValue(item);
						break;
					case SyntaxKind.EnumMemberDeclaration:
						ShowEnumMemberValue(item);
						break;
					case SyntaxKind.PropertyDeclaration:
						ShowPropertyValue(item);
						break;
				}

				void ShowVariableValue(SymbolItem fieldItem) {
					var vi = ((VariableDeclaratorSyntax)fieldItem.SyntaxNode).Initializer;
					if (vi != null) {
						var v = vi.Value?.ToString();
						if (v != null) {
							if (vi.Value.IsKind(CodeAnalysisHelper.ImplicitObjectCreationExpression)) {
								v = "new " + (fieldItem.SyntaxNode.Parent as VariableDeclarationSyntax).Type.ToString() + vi.Value.GetImplicitObjectCreationArgumentList().ToString();
							}
							fieldItem.Hint = ShowInitializerIndicator() + v;
						}
					}
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
						propertyItem.Hint = ShowInitializerIndicator() + (p.Initializer.Value.IsKind(CodeAnalysisHelper.ImplicitObjectCreationExpression) ? "new " + p.Type.ToString() + p.Initializer.Value.GetImplicitObjectCreationArgumentList().ToString() : p.Initializer.Value.ToString());
					}
					else if (p.ExpressionBody != null) {
						propertyItem.Hint = p.ExpressionBody.ToString();
					}
					else //if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.AutoPropertyAnnotation)) {
					//	var a = p.AccessorList.Accessors;
					//	if (a.Count == 2) {
					//		if (a.Any(i => i.RawKind == (int)CodeAnalysisHelper.InitAccessorDeclaration)) {
					//			propertyItem.Hint = "{init}";
					//		}
					//		else if (a[0].Body == null && a[0].ExpressionBody == null && a[1].Body == null && a[1].ExpressionBody == null) {
					//			propertyItem.Hint = "{;;}";
					//		}
					//	}
					//	else if (a.Count == 1) {
					//		if (a[0].Body == null && a[0].ExpressionBody == null) {
					//			propertyItem.Hint = "{;}";
					//		}
					//	}
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.MemberType)) {
							propertyItem.Hint += p.Type.ToString();
						}
					// }
				}

				string ShowInitializerIndicator() {
					return Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.MemberType) ? "= " : null;
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
						var tip = ToolTipFactory.CreateToolTip(Symbol, true, Bar._SemanticContext);
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.LineOfCode)) {
							tip.AddTextBlock().Append(R.T_LineOfCode + (Node.GetLineSpan().Length + 1));
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

			public override void Dispose() {
				if (Node != null) {
					if (_Menu != null) {
						Bar.DisposeSymbolList(_Menu);
						_Menu = null;
					}
					base.Dispose();
					Click -= HandleClick;
					_Symbol = null;
					_ReferencedSymbols = null;
					_FilterBox = null;
					if (ContextMenu is IDisposable d) {
						d.Dispose();
						ContextMenu = null;
					}
					DataContext = null;
				}
			}
		}

		abstract class BarItem : ThemedImageButton, IDisposable
		{
			public BarItem(CSharpBar bar, int imageId, TextBlock content) : base(imageId, content) {
				Bar = bar;
				this.ReferenceCrispImageBackground(EnvironmentColors.MainWindowActiveCaptionColorKey);
				SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextActiveKey);
			}

			protected CSharpBar Bar { get; private set; }
			public SyntaxNode Node { get; protected set; }
			public abstract BarItemType ItemType { get; }
			public virtual void Dispose() {
				Node = null;
				Bar = null;
			}
		}

		sealed class SymbolNodeItem : ThemedImageButton
		{
			CSharpBar _Bar;
			ISymbol _Symbol;

			public SymbolNodeItem(CSharpBar bar, ISymbol symbol)
				: base (IconIds.GoToDefinition, new ThemedMenuText(symbol.GetOriginalName())) {
				_Bar = bar;
				_Symbol = symbol;
				Opacity = 0.8;
				Unloaded += SymbolNodeItem_Unloaded;
			}

			void SymbolNodeItem_Unloaded(object sender, RoutedEventArgs e) {
				Unloaded -= SymbolNodeItem_Unloaded;
				_Bar = null;
				_Symbol = null;
			}

			protected override void OnClick() {
				base.OnClick();
				_Symbol.GoToSource();
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

		interface ISymbolContainer
		{
			ISymbol Symbol { get; }
		}
		enum BarItemType
		{
			Root, GlobalNamespace, Namespace, Node
		}
		enum MemberListOptions
		{
			None,
			ShowPartial,
			ShowBase
		}
	}
}
