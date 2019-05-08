using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Microsoft.VisualStudio.Text.Editor;
using Task = System.Threading.Tasks.Task;

namespace Codist.NaviBar
{
	public sealed class CSharpBar : Menu
	{
		internal const string SyntaxNodeRange = nameof(SyntaxNodeRange);
		readonly IWpfTextView _View;
		readonly IAdornmentLayer _SyntaxNodeRangeAdornment;
		readonly SemanticContext _SemanticContext;
		readonly ExternalAdornment _SymbolListContainer;

		CancellationTokenSource _cancellationSource = new CancellationTokenSource();
		RootItem _RootItem;
		NodeItem _MouseHoverItem;
		SymbolList _SymbolList;

		public CSharpBar(IWpfTextView textView) {
			_View = textView;
			_SyntaxNodeRangeAdornment = _View.GetAdornmentLayer(SyntaxNodeRange);
			_SymbolListContainer = new ExternalAdornment(_View);
			_SemanticContext = textView.Properties.GetOrCreateSingletonProperty(() => new SemanticContext(textView));
			this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
			textView.Properties.AddProperty(nameof(NaviBar), this);
			Name = nameof(CSharpBar);
			Resources = SharedDictionaryManager.Menu;
			SetResourceReference(BackgroundProperty, VsBrushes.CommandBarMenuBackgroundGradientKey);
			SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextInactiveKey);
			Items.Add(_RootItem = new RootItem(this));
			_View.Selection.SelectionChanged += Update;
			_View.Closed += ViewClosed;
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
				var span = item.Node.Span.CreateSnapshotSpan(_View.TextSnapshot);
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

		void HighlightNodeRanges(SyntaxNode node, Microsoft.VisualStudio.Text.SnapshotSpan span) {
			_SyntaxNodeRangeAdornment.AddAdornment(span, null, new GeometryAdornment(ThemeHelper.MenuHoverBackgroundColor, _View.TextViewLines.GetMarkerGeometry(span), 3));
			var p = _View.Caret.Position.BufferPosition;
			if (span.Contains(p) == false) {
				return;
			}
			var n = _SemanticContext.GetNode(p, false, false);
			while (n != null && node.Contains(n)) {
				if (n.Span.Start != span.Start
					&& n.Span.Length != span.Length
					&& n.IsKind(SyntaxKind.Block) == false) {
					span = n.Span.CreateSnapshotSpan(_View.TextSnapshot);
					_SyntaxNodeRangeAdornment.AddAdornment(span, null, new GeometryAdornment(ThemeHelper.MenuHoverBackgroundColor, _View.TextViewLines.GetMarkerGeometry(span), n.IsSyntaxBlock() || n.IsDeclaration() ? 1 : 0));
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

		async void Update(object sender, EventArgs e) {
			HideMenu();
			CancellationHelper.CancelAndDispose(ref _cancellationSource, true);
			var cs = _cancellationSource;
			if (cs != null) {
				try {
					await Update(cs.Token);
				}
				catch (OperationCanceledException) {
					// ignore
				}
			}
			async Task Update(CancellationToken token) {
				var nodes = await UpdateModelAsync(token);
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
				var c = Math.Min(Items.Count, nodes.Count);
				int i, i2;
				for (i = 1, i2 = 0; i < c && i2 < c; i2++) {
					var n = nodes[i2];
					if (token.IsCancellationRequested) {
						return;
					}
					var n2 = (Items[i] as NodeItem).Node;
					if (n2 == n) {
						// keep the NaviItem if node is not updated
						++i;
						continue;
					}
					if (n.IsKind(SyntaxKind.NamespaceDeclaration)) {
						continue;
					}
					break;
				}
				if ((i == 1 || i2 < nodes.Count && nodes[i2].IsTypeOrNamespaceDeclaration()) && _RootItem.FilterText.Length == 0) {
					// clear type and namespace menu items if a type is changed
					_RootItem.ClearSymbolList();
				}
				c = Items.Count;
				while (c > i) {
					Items.RemoveAt(--c);
				}
				c = nodes.Count;
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
					bool highlight = node.IsMemberDeclaration();
					var newItem = new NodeItem(this, node);
					if (highlight) {
						newItem.FontWeight = FontWeights.Bold;
						newItem.IsChecked = true;
					}
					Items.Add(newItem);
					++i2;
				}
			}
		}

		void ViewClosed(object sender, EventArgs e) {
			_View.Selection.SelectionChanged -= Update;
			CancellationHelper.CancelAndDispose(ref _cancellationSource, false);
			_View.Closed -= ViewClosed;
		}

		#region Menu handler
		internal void ShowRootItemMenu() {
			_RootItem.ShowNamespaceAndTypeMenu();
		}
		internal void ShowActiveClassMenu() {
			for (int i = Items.Count - 1; i >= 0; i--) {
				var item = Items[i] as NodeItem;
				if (item != null && item.Node.IsTypeDeclaration()) {
					item.PerformClick();
					return;
				}
			}
		}
		void SetupSymbolListMenu(SymbolList list) {
			list.ReferenceCrispImageBackground(EnvironmentColors.MainWindowActiveCaptionColorKey);
			list.MouseLeftButtonUp += MenuItemSelect;
		}

		void ShowMenu(Visual visual, SymbolList menu) {
			if (_SymbolList != menu) {
				_SymbolListContainer.Children.Clear();
				_SymbolListContainer.Add(menu);
				_SymbolList = menu;
			}
			menu.ItemsControlMaxHeight = _View.ViewportHeight / 2;
			menu.RefreshSymbols();
			menu.ScrollToSelectedItem();
			menu.PreviewKeyUp -= OnMenuKeyUp;
			menu.PreviewKeyUp += OnMenuKeyUp;
			Canvas.SetLeft(menu, visual.TransformToVisual(_View.VisualElement).Transform(new Point()).X);
			Canvas.SetTop(menu, 0);
		}

		void OnMenuKeyUp(object sender, KeyEventArgs e) {
			if (e.Key == Key.Escape) {
				HideMenu();
				e.Handled = true;
			}
		}

		void HideMenu() {
			if (_SymbolList != null) {
				_SymbolListContainer.Clear();
				_SymbolList.SelectedItem = null;
				_SymbolList = null;
			}
		}

		void MenuItemSelect(object sender, MouseButtonEventArgs e) {
			var menu = sender as SymbolList;
			if (menu.SelectedIndex == -1) {
				return;
			}
			_View.VisualElement.Focus();
			(menu.SelectedItem as SymbolItem)?.GoToSource();
		}
		#endregion

		void SetHeader(ThemedMenuText t, SyntaxNode node, bool includeContainer, bool highlight, bool includeParameterList) {
			var title = node.GetDeclarationSignature();
			if (title == null) {
				return;
			}
			if (node.IsStructuredTrivia && Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.StripRegionNonLetter)) {
				title = TrimNonLetterOrDigitCharacters(title);
			}
			if (includeContainer == false && node.IsTypeDeclaration()) {
				var p = node.Parent;
				while (p.IsTypeDeclaration()) {
					title = "..." + title;
					p = p.Parent;
				}
			}
			if (includeContainer) {
				var p = node.Parent;
				if (node is VariableDeclaratorSyntax) {
					p = p.Parent.Parent;
				}
				if (p is BaseTypeDeclarationSyntax) {
					t.Append((p as BaseTypeDeclarationSyntax).Identifier.ValueText + ".", ThemeHelper.SystemGrayTextBrush);
				}
			}
			t.Append(title, highlight);
			if (includeParameterList && Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.ParameterList)) {
				AddParameterList(t, node);
			}
		}

		static void AddParameterList(ThemedMenuText t, SyntaxNode node) {
			var useParamName = Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.ParameterListShowParamName);
			if (node is BaseMethodDeclarationSyntax) {
				t.Append((node as BaseMethodDeclarationSyntax).ParameterList.GetParameterListSignature(useParamName), ThemeHelper.SystemGrayTextBrush);
			}
			else if (node.IsKind(SyntaxKind.DelegateDeclaration)) {
				t.Append((node as DelegateDeclarationSyntax).ParameterList.GetParameterListSignature(useParamName));
			}
			else if (node is OperatorDeclarationSyntax) {
				t.Append((node as OperatorDeclarationSyntax).ParameterList.GetParameterListSignature(useParamName));
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


		async Task<List<SyntaxNode>> UpdateModelAsync(CancellationToken token) {
			var start = _View.Selection.Start.Position;
			if (await _SemanticContext.UpdateAsync(start, token) == false) {
				return new List<SyntaxNode>();
			}
			return _SemanticContext.GetContainingNodes(start, Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.SyntaxDetail), Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.RegionOnBar));
		}

		sealed class RootItem : ThemedMenuItem
		{
			readonly CSharpBar _Bar;
			readonly SymbolList _Menu;
			readonly MemberFinderBox _FinderBox;
			readonly SearchScopeBox _ScopeBox;

			public RootItem(CSharpBar bar) {
				_Bar = bar;
				Icon = ThemeHelper.GetImage(KnownImageIds.Namespace);
				this.ReferenceCrispImageBackground(EnvironmentColors.MainWindowActiveCaptionColorKey);
				SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextActiveKey);
				Header = new ThemedToolBarText();
				_Menu = new SymbolList {
					Container = _Bar._SymbolListContainer,
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
											new ThemedButton(KnownImageIds.StopFilter, "Clear filter", ClearFilter).ClearBorder().ClearMargin()
										}
									}),
								}
							},
						}
					},
				};
				_Bar.SetupSymbolListMenu(_Menu);
				_FinderBox.TextChanged += SearchCriteriaChanged;
				_ScopeBox.FilterChanged += SearchCriteriaChanged;
				_ScopeBox.FilterChanged += (s, args) => _FinderBox.Focus();
			}

			public string FilterText => _FinderBox.Text;

			public void ClearSymbolList() {
				_Menu.Symbols.Clear();
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
				PopulateTypes();
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
				int pos = _Bar._View.GetCaretPosition();
				for (int i = _Menu.Symbols.Count - 1; i >= 0; i--) {
					if (_Menu.Symbols[i].SelectIfContainsPosition(pos)) {
						return;
					}
				}
			}

			void AddNamespaceAndTypes() {
				foreach (var node in _Bar._SemanticContext.Compilation.ChildNodes()) {
					if (node.IsTypeOrNamespaceDeclaration()) {
						_Menu.Add(node, _Bar._SemanticContext);
						AddTypeDeclarations(node);
					}
				}
				MarkEnclosingType();
			}

			void AddTypeDeclarations(SyntaxNode node) {
				foreach (var child in node.ChildNodes()) {
					if (child.IsTypeOrNamespaceDeclaration()) {
						var i = _Menu.Add(child, _Bar._SemanticContext);
						String prefix = null;
						var p = child.Parent;
						while (p.IsTypeDeclaration()) {
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
				CancellationHelper.CancelAndDispose(ref _Bar._cancellationSource, true);
				_Menu.ItemsSource = null;
				_Menu.Symbols.Clear();
				var s = _FinderBox.Text;
				if (s.Length == 0) {
					AddNamespaceAndTypes();
					_Menu.RefreshSymbols();
					return;
				}
				try {
					switch (_ScopeBox.Filter) {
						case ScopeType.ActiveDocument: FindInDocument(s); break;
						case ScopeType.ActiveProject: FindInProject(s); break;
					}
					_Menu.RefreshSymbols();
					_Menu.UpdateLayout();
				}
				catch (OperationCanceledException) {
					// ignores cancellation
				}
				catch (ObjectDisposedException) { }
			}
			void FindInDocument(string text) {
				var cancellationToken = _Bar._cancellationSource.GetToken();
				var members = _Bar._SemanticContext.Compilation.GetDecendantDeclarations(cancellationToken);
				foreach (var item in members) {
					if (item.GetDeclarationSignature().IndexOf(text, StringComparison.OrdinalIgnoreCase) != -1) {
						var i = _Menu.Add(item, _Bar._SemanticContext);
						var c = new ThemedMenuText();
						_Bar.SetHeader(c, item, true, false, true);
						i.Content = c;
					}
				}
			}
			void FindInProject(string text) {
				ThreadHelper.JoinableTaskFactory.Run(() => FindDeclarationsAsync(text, _Bar._cancellationSource.GetToken()));
			}

			async Task FindDeclarationsAsync(string symbolName, CancellationToken token) {
				var result = await _Bar._SemanticContext.Document.Project.FindDeclarationsAsync(symbolName, 50, false, false, SymbolFilter.All, token).ConfigureAwait(false);
				foreach (var item in result) {
					if (token.IsCancellationRequested) {
						break;
					}
					//Items.Add(new SymbolItem(item, _Bar._SemanticContext));
					_Menu.Add(item, _Bar._SemanticContext, true);
				}
			}
			sealed class MemberFinderBox : ThemedTextBox
			{
				public MemberFinderBox() : base(true) {
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
		sealed class NodeItem : ThemedMenuItem, IMemberTypeFilter
		{
			readonly int _ImageId;
			readonly CSharpBar _Bar;
			SymbolList _Menu;
			MemberFilterBox _FilterBox;

			public NodeItem(CSharpBar bar, SyntaxNode node) {
				_Bar = bar;
				_ImageId = node.GetImageId();
				Header = new ThemedMenuText(node.GetDeclarationSignature() ?? String.Empty);
				Node = node;
				Icon = ThemeHelper.GetImage(_ImageId);
				this.ReferenceCrispImageBackground(EnvironmentColors.MainWindowActiveCaptionColorKey);
				SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextActiveKey);
				Click += HandleClick;
			}
			public SyntaxNode Node { get; }

			async void HandleClick(object sender, RoutedEventArgs e) {
				CancellationHelper.CancelAndDispose(ref _Bar._cancellationSource, true);
				if (_Menu != null && _Bar._SymbolList == _Menu) {
					_Bar.HideMenu();
					return;
				}
				if (Node.IsTypeDeclaration() == false) {
					var span = Node.FullSpan;
					if (span.Contains(_Bar._SemanticContext.Position) && Node.SyntaxTree.FilePath == _Bar._SemanticContext.Document.FilePath) {
						_Bar._View.SelectNode(Node, Keyboard.Modifiers != ModifierKeys.Control);
					}
					else {
						Node.GetIdentifierToken().GetLocation().GoToSource();
					}
					return;
				}
				if (_Menu == null) {
					_Menu = new SymbolList {
						Container = _Bar._SymbolListContainer
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
							(_FilterBox = new MemberFilterBox(_Menu)),
						}
					};
					_Bar.SetupSymbolListMenu(_Menu);
					await AddItemsAsync(Node, _Bar._cancellationSource.GetToken());
					if (_Menu.Symbols.Count > 100) {
						ScrollViewer.SetCanContentScroll(_Menu, true);
					}
				}
				else {
					await RefreshItemsAsync(Node, _Bar._cancellationSource.GetToken());
				}
				_Menu.ItemsControlMaxHeight = _Bar._View.ViewportHeight / 2;
				_Bar.ShowMenu(this, _Menu);
				_FilterBox?.FocusTextBox();
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
				await _Bar._SemanticContext.UpdateAsync(cancellationToken);
				if (sm != _Bar._SemanticContext.SemanticModel) {
					_Menu.Symbols.Clear();
					await AddItemsAsync(node, cancellationToken);
					return;
				}
				var pos = _Bar._SemanticContext.Position;
				foreach (var item in _Menu.Symbols) {
					if (item.Type == SymbolItemType.Container) {
						continue;
					}
					if (item.IsExternal || cancellationToken.IsCancellationRequested || item.SelectIfContainsPosition(pos)) {
						break;
					}
				}
			}
			async Task AddPartialTypeDeclarationsAsync(BaseTypeDeclarationSyntax node, CancellationToken cancellationToken) {
				await _Bar._SemanticContext.UpdateAsync(cancellationToken);
				var symbol = await _Bar._SemanticContext.GetSymbolAsync(node, cancellationToken);
				if (symbol == null) {
					return;
				}
				var current = node.SyntaxTree;
				foreach (var item in symbol.DeclaringSyntaxReferences) {
					if (item.SyntaxTree == current || String.Equals(item.SyntaxTree.FilePath, current.FilePath, StringComparison.OrdinalIgnoreCase)) {
						continue;
					}
					var partial = await item.GetSyntaxAsync(cancellationToken);
					var i = _Menu.Add(partial, _Bar._SemanticContext);
					i.Content.Text = System.IO.Path.GetFileName(item.SyntaxTree.FilePath);
					i.Type = SymbolItemType.Container;
					AddMemberDeclarations(partial, true);
				}
			}
			void AddMemberDeclarations(SyntaxNode node, bool isExternal) {
				const byte UNDEFINED = 0xFF, TRUE = 1, FALSE = 0;
				var directives = Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.Region)
					? node.GetDirectives(d => d.IsKind(SyntaxKind.RegionDirectiveTrivia) || d.IsKind(SyntaxKind.EndRegionDirectiveTrivia))
					: null;
				byte regionJustStart = UNDEFINED; // undefined, prevent #endregion show up on top of menu items
				int pos = _Bar._View.GetCaretPosition();
				foreach (var child in node.ChildNodes()) {
					if (child.IsMemberDeclaration() == false && child.IsTypeDeclaration() == false) {
						continue;
					}
					if (directives != null) {
						for (var i = 0; i < directives.Count; i++) {
							var d = directives[i];
							if (d.SpanStart < child.SpanStart) {
								if (d.IsKind(SyntaxKind.RegionDirectiveTrivia)) {
									var item = _Menu.Add(d, _Bar._SemanticContext);
									if (isExternal) {
										item.Type = SymbolItemType.External;
									}
									regionJustStart = TRUE;
								}
								else if (d.IsKind(SyntaxKind.EndRegionDirectiveTrivia)) {
									// don't show #endregion if preceeding item is #region
									if (regionJustStart == FALSE) {
										var item = new SymbolItem(_Menu);
										_Menu.Symbols.Add(item);
										item.Content.Append("#endregion ").Append(d.GetDeclarationSignature());
										item.Content.Foreground = ThemeHelper.SystemGrayTextBrush;
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
					if (child.IsKind(SyntaxKind.FieldDeclaration) || child.IsKind(SyntaxKind.EventFieldDeclaration)) {
						AddVariables((child as BaseFieldDeclarationSyntax).Declaration.Variables, isExternal, pos);
					}
					else {
						var i = _Menu.Add(child, _Bar._SemanticContext);
						if (isExternal) {
							i.Type = SymbolItemType.External;
						}
						i.SelectIfContainsPosition(pos);
						ShowNodeValue(i);
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.ParameterList)) {
							AddParameterList(i.Content, child);
						}
					}
					// a member is added between #region and #endregion
					regionJustStart = FALSE;
				}
				if (directives != null) {
					foreach (var item in directives) {
						if (item.IsKind(SyntaxKind.RegionDirectiveTrivia)) {
							var i = _Menu.Add(item, _Bar._SemanticContext);
							if (isExternal) {
								i.Type = SymbolItemType.External;
							}
						}
					}
				}
			}

			void AddVariables(SeparatedSyntaxList<VariableDeclaratorSyntax> fields, bool isExternal, int pos) {
				foreach (var item in fields) {
					var i = _Menu.Add(item, _Bar._SemanticContext);
					if (isExternal) {
						i.Type = SymbolItemType.External;
					}
					i.SelectIfContainsPosition(pos);
					ShowNodeValue(i);
				}
			}

			static void ShowNodeValue(SymbolItem item) {
				if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.FieldValue) == false) {
					return;
				}
				switch (item.SyntaxNode.Kind()) {
					case SyntaxKind.VariableDeclarator:
						item.Hint = (item.SyntaxNode as VariableDeclaratorSyntax).Initializer?.Value?.ToString();
						break;
					case SyntaxKind.EnumMemberDeclaration:
						item.Hint = (item.SyntaxNode as EnumMemberDeclarationSyntax).EqualsValue?.Value?.ToString();
						break;
					case SyntaxKind.PropertyDeclaration:
						var p = item.SyntaxNode as PropertyDeclarationSyntax;
						if (p.Initializer != null) {
							item.Hint = p.Initializer.Value.ToString();
						}
						else if (p.ExpressionBody != null) {
							item.Hint = p.ExpressionBody.ToString();
						}
						else if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.AutoPropertyAnnotation)) {
							var a = p.AccessorList.Accessors;
							if (a.Count == 2) {
								if (a[0].Body == null && a[0].ExpressionBody == null && a[1].Body == null && a[1].ExpressionBody == null) {
									item.Hint = "{;;}";
								}
							}
							else if (a.Count == 1) {
								if (a[0].Body == null && a[0].ExpressionBody == null) {
									item.Hint = "{;}";
								}
							}
						}
						break;
				}
			}

			bool IMemberTypeFilter.Filter(MemberFilterTypes filterTypes) {
				return MemberFilterBox.FilterByImageId(filterTypes, _ImageId);
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
