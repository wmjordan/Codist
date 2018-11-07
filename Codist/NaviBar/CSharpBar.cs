using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Task = System.Threading.Tasks.Task;
using AppHelpers;

namespace Codist.NaviBar
{
	public sealed class CSharpBar : Menu
	{
		readonly IWpfTextView _View;
		readonly IAdornmentLayer _Adornment;
		readonly SemanticContext _SemanticContext;
		CancellationTokenSource _cancellationSource = new CancellationTokenSource();
		NaviItem _MouseHoverItem;
		static MemberFilterOptions _MemberFilterOptions;

		public CSharpBar(IWpfTextView textView) {
			_View = textView;
			_Adornment = _View.GetAdornmentLayer(nameof(CSharpBar));
			_SemanticContext = textView.Properties.GetOrCreateSingletonProperty(() => new SemanticContext(textView));
			this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
			Name = nameof(CSharpBar);
			Resources = SharedDictionaryManager.Menu;
			SetResourceReference(BackgroundProperty, VsBrushes.CommandBarMenuBackgroundGradientKey);
			SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextInactiveKey);
			Items.Add(new RootItem(this));
			_View.Selection.SelectionChanged += Update;
			_View.Closed += ViewClosed;
			Update(this, EventArgs.Empty);
			// todo update icons if theme changed
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
				var item = Items[i] as NaviItem;
				if (item == null || item.Contains(e.GetPosition(item)) == false) {
					continue;
				}

				_MouseHoverItem = item;
				if (_Adornment.IsEmpty == false) {
					_Adornment.RemoveAllAdornments();
				}
				var span = item.Node.Span.CreateSnapshotSpan(_View.TextSnapshot);
				if (span.Length > 0) {
					try {
						_Adornment.AddAdornment(span, null, new GeometryAdornment(ThemeHelper.TitleBackgroundColor, _View.TextViewLines.GetMarkerGeometry(span)));
					}
					catch (ObjectDisposedException) {
						// ignore
						_MouseHoverItem = null;
					}
				}
				return;
			}
			if (_Adornment.IsEmpty == false) {
				_Adornment.RemoveAllAdornments();
				_MouseHoverItem = null;
			}
		}
		protected override void OnMouseLeave(MouseEventArgs e) {
			base.OnMouseLeave(e);
			if (_Adornment.IsEmpty == false) {
				_Adornment.RemoveAllAdornments();
				_MouseHoverItem = null;
			}
		}

		async void Update(object sender, EventArgs e) {
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
				var c = Math.Min(Items.Count - 1, nodes.Count);
				int i;
				for (i = 0; i < c; i++) {
					var n = nodes[i];
					if ((Items[i + 1] as NaviItem).Node != n) {
						// keep the NaviItem if node is not updated
						break;
					}
					if (token.IsCancellationRequested) {
						return;
					}
				}
				c = Items.Count;
				while (--c > i) {
					Items.RemoveAt(c);
				}
				c = nodes.Count;
				while (i < c) {
					if (token.IsCancellationRequested) {
						return;
					}
					Items.Add(new NaviItem(this, nodes[i], true, false));
					++i;
				}
			}
		}

		void ViewClosed(object sender, EventArgs e) {
			_View.Selection.SelectionChanged -= Update;
			CancellationHelper.CancelAndDispose(ref _cancellationSource, false);
			_View.Closed -= ViewClosed;
		}

		async Task<List<SyntaxNode>> UpdateModelAsync(CancellationToken token) {
			if (await _SemanticContext.UpdateAsync(_View.Selection.Start.Position, token) == false) {
				return new List<SyntaxNode>();
			}
			var node = _SemanticContext.Node;
			var nodes = new List<SyntaxNode>(5);
			var detail = Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.SyntaxDetail);
			while (node != null) {
				if (node.FullSpan.Contains(_View.Selection, true)
					&& node.IsKind(SyntaxKind.VariableDeclaration) == false
					&& (detail && node.IsSyntaxBlock() || node.IsDeclaration() || node.IsKind(SyntaxKind.Attribute))) {
					nodes.Add(node);
				}
				node = node.Parent;
			}
			nodes.Reverse();
			return nodes;
		}

		static string GetParameterListSignature(ParameterListSyntax parameters) {
			if (parameters.Parameters.Count == 0) {
				return "()";
			}
			using (var r = Microsoft.VisualStudio.Utilities.ReusableStringBuilder.AcquireDefault(30)) {
				var sb = r.Resource;
				sb.Append('(');
				foreach (var item in parameters.Parameters) {
					if (sb.Length > 1) {
						sb.Append(',');
					}
					sb.Append(item.Type.ToString());
				}
				sb.Append(')');
				return sb.ToString();
			}
		}

		[Flags]
		enum MemberFilterOptions
		{
			None,
			Public = 1,
			Private = 1 << 1,
			Internal = 1 << 2,
			Field = 1 << 3,
			Property = 1 << 4,
			Method = 1 << 5,
			Delegate = 1 << 6,
			All = Public | Private | Internal | Field | Property | Method | Delegate
		}

		sealed class RootItem : ThemedMenuItem
		{
			readonly CSharpBar _Bar;
			readonly MemberFinderBox _FinderBox;
			//todo update image when theme changed
			public RootItem(CSharpBar bar) {
				_Bar = bar;
				Icon = ThemeHelper.GetImage(KnownImageIds.CSProjectNode);
				this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
				SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextActiveKey);
				Header = new ThemedTipText("//");
				SubMenuHeader = new StackPanel {
					Margin = WpfHelper.MenuItemMargin,
					Children = {
						new StackPanel {
							Children = {
								ThemeHelper.GetImage(KnownImageIds.FindSymbol).WrapMargin(WpfHelper.GlyphMargin),
								(_FinderBox = new MemberFinderBox(Items) { MinWidth = 150 }),
							},
							Orientation = Orientation.Horizontal
						},
					}
				};
				_FinderBox.TextChanged += MemberFinderBox_TextChanged;
			}

			private void MemberFinderBox_TextChanged(object sender, TextChangedEventArgs e) {
				ClearItems();
				CancellationHelper.CancelAndDispose(ref _Bar._cancellationSource, true);
				var s = _FinderBox.Text;
				if (s.Length == 0) {
					return;
				}
				try {
					var cancellationToken = _Bar._cancellationSource.GetToken();
					var members = _Bar._SemanticContext.Compilation.GetDecendantDeclarations(cancellationToken);
					foreach (var item in members) {
						if (item.GetDeclarationSignature().IndexOf(s, StringComparison.OrdinalIgnoreCase) != -1) {
							Items.Add(new NaviItem(_Bar, item, i => i.SetHeader(true), i => i.GoToLocation()));
						}
					}
					//if (HasExplicitItems) {
					//	Items.Add(new Separator());
					//}
					//if (s.Length < 2) {
					//	return;
					//}
					//await FindDeclarationsAsync(s, cancellationToken);
				}
				catch (OperationCanceledException) {
					// ignores cancellation
				}
				catch (ObjectDisposedException) { }
			}
			async Task FindDeclarationsAsync(string symbolName, CancellationToken token) {
				var result = new SortedSet<ISymbol>(Comparer<ISymbol>.Create((x, y) => x.Name.Length - y.Name.Length));
				int maxNameLength = 0;
				foreach (var symbol in await Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindSourceDeclarationsAsync(_Bar._SemanticContext.Document.Project, name => name.IndexOf(symbolName, StringComparison.OrdinalIgnoreCase) != -1, token)) {
					if (result.Count < 50) {
						result.Add(symbol);
					}
					else {
						maxNameLength = result.Max.Name.Length;
						if (symbol.Name.Length < maxNameLength) {
							result.Remove(result.Max);
							result.Add(symbol);
						}
					}
				}
				foreach (var item in result) {
					Items.Add(new SymbolItem(item));
				}
			}

			sealed class MemberFinderBox : ThemedTextBox
			{
				readonly ItemCollection _Items;

				public MemberFinderBox(ItemCollection items) {
					_Items = items;
					PreviewKeyUp += ControlMenuSelection;
				}

				void ControlMenuSelection(object sender, KeyEventArgs e) {
					if (e.Key == Key.Enter && _Items.Count > 1) {
						foreach (var item in _Items) {
							var nav = item as NaviItem;
							if (nav != null) {
								nav.RaiseEvent(new RoutedEventArgs(ClickEvent));
								return;
							}
						}
					}
				}
			}
		}
		sealed class NaviItem : ThemedMenuItem
		{
			readonly CSharpBar _Bar;
			bool _NodeIsExternal, _ShowNodeDetail;

			public NaviItem(CSharpBar bar, SyntaxNode node) : this (bar, node, false, false) { }
			public NaviItem(CSharpBar bar, SyntaxNode node, bool highlightTypes, bool includeParameterList) : this(bar, node, null, null) {
				SetHeader(node, false, highlightTypes, includeParameterList);
			}
			public NaviItem(CSharpBar bar, SyntaxNode node, Action<NaviItem> initializer, Action<NaviItem> clickHandler) {
				Node = node;
				ClickHandler = clickHandler;
				_Bar = bar;

				Icon = ThemeHelper.GetImage(node.GetImageId());
				initializer?.Invoke(this);
				this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
				SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextActiveKey);
				if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.SymbolToolTip)) {
					ToolTip = String.Empty;
					ToolTipOpening += NaviItem_ToolTipOpening;
				}
				Click += NaviItem_Click;
			}

			public bool NodeIsExternal {
				get => _NodeIsExternal;
				internal set {
					_NodeIsExternal = value;
					Opacity = value ? 0.7 : 1;
				}
			}
			public bool ShowNodeDetail {
				get => _ShowNodeDetail;
				internal set {
					_ShowNodeDetail = value;
					if (value && Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.FieldValue)) {
						if (Node.IsKind(SyntaxKind.VariableDeclarator)) {
							InputGestureText = (Node as VariableDeclaratorSyntax).Initializer?.Value?.ToString();
						}
						else if (Node.IsKind(SyntaxKind.EnumMemberDeclaration)) {
							InputGestureText = (Node as EnumMemberDeclarationSyntax).EqualsValue?.Value?.ToString();
						}
					}
					else {
						InputGestureText = null;
					}
				}
			}
			internal SyntaxNode Node { get; }

			private Action<NaviItem> ClickHandler { get; set; }

			async void NaviItem_Click(object sender, RoutedEventArgs e) {
				if (ClickHandler != null) {
					ClickHandler(this);
				}
				else if (HasItems == false) {
					await AddItemsAsync(Items, Node);
					if (HasExplicitItems) {
						IsSubmenuOpen = true;
						SubmenuOpened += NaviItem_SubmenuOpened;
					}
				}
			}

			async void NaviItem_SubmenuOpened(object sender, RoutedEventArgs e) {
				await RefreshItemsAsync(Items, Node);
			}

			async void NaviItem_ToolTipOpening(object sender, ToolTipEventArgs e) {
				// todo: handle updated syntax node for RootItem
				var symbol = await _Bar._SemanticContext.GetSymbolAsync(Node, _Bar._cancellationSource.GetToken());
				ToolTip = symbol != null
					? ToolTipFactory.CreateToolTip(symbol, _Bar._SemanticContext.SemanticModel.Compilation)
					: (object)Node.GetSyntaxBrief();
				this.SetTipOptions();
				if (Parent != _Bar) {
					ToolTipService.SetPlacement(this, System.Windows.Controls.Primitives.PlacementMode.Right);
				}
				ToolTipOpening -= NaviItem_ToolTipOpening;
			}

			#region Item methods
			public void SetHeader(bool includeContainer) {
				SetHeader(Node, includeContainer, true, true);
			}
			NaviItem SetHeader(SyntaxNode node, bool includeContainer, bool highlightTypes, bool includeParameterList) {
				var title = node.GetDeclarationSignature();
				if (title == null) {
					return this;
				}
				if (includeContainer == false && node.IsTypeDeclaration()) {
					var p = node.Parent;
					while (p.IsTypeDeclaration()) {
						title = "..." + title;
						p = p.Parent;
					}
				}
				//if (title.Length > 32) {
				//	title = title.Substring(0, 32) + "...";
				//}
				var t = new ThemedTipText();
				if (includeContainer) {
					var p = node.Parent;
					if (node is VariableDeclaratorSyntax) {
						p = p.Parent.Parent;
					}
					else if (node is EnumMemberDeclarationSyntax) {
						p = p.Parent;
					}
					if (p is TypeDeclarationSyntax) {
						t.Append((p as TypeDeclarationSyntax).Identifier.ValueText + ".", ThemeHelper.SystemGrayTextBrush);
					}
				}
				t.Append(title, highlightTypes && (node.IsTypeDeclaration() || node.IsKind(SyntaxKind.NamespaceDeclaration) || node.IsKind(SyntaxKind.CompilationUnit)));
				if (includeParameterList && Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.ParameterList)) {
					if (node is BaseMethodDeclarationSyntax) {
						t.Append(GetParameterListSignature((node as BaseMethodDeclarationSyntax).ParameterList), ThemeHelper.SystemGrayTextBrush);
					}
					else if (node.IsKind(SyntaxKind.DelegateDeclaration)) {
						t.Append(GetParameterListSignature((node as DelegateDeclarationSyntax).ParameterList));
					}
					else if (node is OperatorDeclarationSyntax) {
						t.Append(GetParameterListSignature((node as OperatorDeclarationSyntax).ParameterList));
					}
				}
				Header = t;
				return this;
			}

			NaviItem MarkEnclosingItem(int position) {
				if (NodeIsExternal) {
					return this;
				}
				if (Node.FullSpan.Contains(position)) {
					if (Background == Brushes.Transparent) {
						Background = ThemeHelper.GetWpfBrush(Microsoft.VisualStudio.PlatformUI.EnvironmentColors.CommandBarMenuItemMouseOverBrushKey);
					}
				}
				else {
					if (Background != Brushes.Transparent) {
						Background = Brushes.Transparent;
					}
				}
				return this;
			}

			async Task AddItemsAsync(ItemCollection items, SyntaxNode node) {
				switch (node.Kind()) {
					case SyntaxKind.NamespaceDeclaration:
						SubMenuMaxHeight = _Bar._View.ViewportHeight / 2;
						SubMenuHeader = new StackPanel {
							Children = {
								new NaviItem(_Bar, node) { ClickHandler = i => i.GoToLocation() },
								new Separator()
							}
						};
						await AddTypeDeclarationsAsync(node);
						break;
					case SyntaxKind.ClassDeclaration:
					case SyntaxKind.StructDeclaration:
					case SyntaxKind.InterfaceDeclaration:
					case SyntaxKind.EnumDeclaration:
						SubMenuMaxHeight = _Bar._View.ViewportHeight / 2;
						SubMenuHeader = new StackPanel {
							Children = {
								new NaviItem(_Bar, node) { ClickHandler = i => i.GoToLocation() },
								new StackPanel {
									Margin = WpfHelper.MenuItemMargin,
									Children = {
										ThemeHelper.GetImage(KnownImageIds.Filter).WrapMargin(WpfHelper.GlyphMargin),
										new FilterBox(Items) { MinWidth = 150 }
									},
									Orientation = Orientation.Horizontal
								},
								new Separator()
							}
						};
						AddMemberDeclarations(node, false);
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.PartialClassMember)
							&& (node as BaseTypeDeclarationSyntax).Modifiers.Any(SyntaxKind.PartialKeyword)) {
							await AddPartialTypeDeclarationsAsync(node as BaseTypeDeclarationSyntax);
						}
						break;
					default:
						SelectOrGoToSource(node);
						break;
				}
			}

			async Task RefreshItemsAsync(ItemCollection items, SyntaxNode node) {
				var sm = _Bar._SemanticContext.SemanticModel;
				var ct = _Bar._cancellationSource.GetToken();
				await _Bar._SemanticContext.UpdateAsync(ct);
				if (sm != _Bar._SemanticContext.SemanticModel) {
					ClearItems();
					await AddItemsAsync(items, node);
					return;
				}
				var pos = _Bar._SemanticContext.Position;
				foreach (var item in items) {
					var n = item as NaviItem;
					if (n == null) {
						continue;
					}
					if (n.NodeIsExternal || ct.IsCancellationRequested) {
						break;
					}
					n.MarkEnclosingItem(pos);
				}
			}
			#endregion

			#region Helper methods
			public void GoToLocation() {
				_Bar._SemanticContext.RelocateDeclarationNode(Node)?.GetLocation().GoToSource();
			}

			void SelectOrGoToSource() {
				SelectOrGoToSource(Node);
			}
			void SelectOrGoToSource(SyntaxNode node) {
				node = _Bar._SemanticContext.RelocateDeclarationNode(node) ?? node;
				var span = node.FullSpan;
				if (span.Contains(_Bar._SemanticContext.Position)) {
					_Bar._View.SelectNode(node, Keyboard.Modifiers != ModifierKeys.Control);
				}
				else {
					node.GetLocation().GoToSource();
				}
			}
			#endregion

			#region Drop down menu items
			async Task AddPartialTypeDeclarationsAsync(BaseTypeDeclarationSyntax node) {
				var ct = _Bar._cancellationSource.GetToken();
				await _Bar._SemanticContext.UpdateAsync(ct);
				var symbol = await _Bar._SemanticContext.GetSymbolAsync(node, ct);
				if (symbol == null) {
					return;
				}
				var current = node.SyntaxTree;
				foreach (var item in symbol.DeclaringSyntaxReferences) {
					if (item.SyntaxTree == current) {
						continue;
					}
					var partial = await item.GetSyntaxAsync(ct);
					Items.Add(new NaviItem(_Bar, partial, i => {
						i.Header = new ThemedTipText(System.IO.Path.GetFileName(item.SyntaxTree.FilePath), true) {
							TextAlignment = TextAlignment.Center
						}; },
						i => i.GoToLocation()) { Background = ThemeHelper.TitleBackgroundBrush.Alpha(0.8) });
					AddMemberDeclarations(partial, true);
				}
			}
			async Task AddTypeDeclarationsAsync(SyntaxNode node) {
				int pos = _Bar._View.GetCaretPosition();
				foreach(var child in node.ChildNodes()) {
					if (child.IsTypeDeclaration() == false) {
						if (child.IsKind(SyntaxKind.NamespaceDeclaration)) {
							Items.Add(new NaviItem(_Bar, child) { ClickHandler = i => i.GoToLocation() }.MarkEnclosingItem(pos));
						}
						continue;
					}
					Items.Add(new NaviItem(_Bar, child) { ClickHandler = i => i.GoToLocation() }.MarkEnclosingItem(pos));
					await AddTypeDeclarationsAsync(child);
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
									var item = new NaviItem(_Bar, d, false, false) {
										Background = ThemeHelper.TitleBackgroundBrush.Alpha(0.5),
										NodeIsExternal = isExternal
									};
									//(item.Header as TextBlock).TextAlignment = TextAlignment.Center;
									(item.Header as TextBlock).FontWeight = FontWeights.Bold;
									Items.Add(item);
									regionJustStart = TRUE;
								}
								else if (d.IsKind(SyntaxKind.EndRegionDirectiveTrivia)) {
									// don't show #endregion if preceeding item is #region
									if (regionJustStart == FALSE) {
										Items.Add(new Separator {
											Tag = new ThemedTipText {
												HorizontalAlignment = HorizontalAlignment.Right,
												Foreground = ThemeHelper.SystemGrayTextBrush,
												//Margin = new Thickness(0, 0, 17, 0)
											}
											.Append("#endregion ")
											.Append(d.GetDeclarationSignature())
										});
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
						AddVariables((child as BaseFieldDeclarationSyntax).Declaration.Variables, isExternal);
					}
					else {
						Items.Add(new NaviItem(_Bar, child, false, true) {
							NodeIsExternal = isExternal,
							ClickHandler = i => i.SelectOrGoToSource(),
							ShowNodeDetail = child.IsKind(SyntaxKind.EnumMemberDeclaration)
						}.MarkEnclosingItem(pos));
					}
					// a member is added between #region and #endregion
					regionJustStart = FALSE;
				}
				if (directives != null) {
					foreach (var item in directives) {
						if (item.IsKind(SyntaxKind.RegionDirectiveTrivia)) {
							Items.Add(new NaviItem(_Bar, item, false, false) { NodeIsExternal = isExternal });
						}
					}
				}
			}

			void AddVariables(SeparatedSyntaxList<VariableDeclaratorSyntax> fields, bool isExternal) {
				foreach (var item in fields) {
					Items.Add(new NaviItem(_Bar, item) { NodeIsExternal = isExternal, ShowNodeDetail = true });
				}
			}
			#endregion

			sealed class FilterBox : ThemedTextBox
			{
				readonly ItemCollection _Items;
				readonly int _FilterOffset;

				public FilterBox(ItemCollection items) {
					_Items = items;
					_FilterOffset = 0;
				}

				public FilterBox(ItemCollection items, int filterOffset) {
					_Items = items;
					_FilterOffset = filterOffset;
				}

				protected override void OnTextChanged(TextChangedEventArgs e) {
					base.OnTextChanged(e);
					var s = Text;
					if (s.Length == 0) {
						for (int i = _Items.Count - 1; i > _FilterOffset; i--) {
							(_Items[i] as UIElement).Visibility = Visibility.Visible;
						}
						return;
					}
					for (int i = _Items.Count - 1; i > _FilterOffset; i--) {
						var item = _Items[i] as MenuItem;
						if (item == null) {
							(_Items[i] as UIElement).Visibility = Visibility.Collapsed;
							continue;
						}
						var t = (item.Header as TextBlock)?.GetText();
						if (t == null) {
							continue;
						}
						item.Visibility = t.IndexOf(s, StringComparison.OrdinalIgnoreCase) != -1
							? Visibility.Visible
							: Visibility.Collapsed;
					}
				}
			}
		}

		sealed class GeometryAdornment : UIElement
		{
			readonly DrawingVisual _child;

			public GeometryAdornment(Color color, Geometry geometry) {
				_child = new DrawingVisual();
				using (var context = _child.RenderOpen()) {
					context.DrawGeometry(new SolidColorBrush(color.Alpha(192)), new Pen(new SolidColorBrush(color), 1), geometry);
					context.Close();
				}
				AddVisualChild(_child);
			}

			protected override int VisualChildrenCount => 1;

			protected override Visual GetVisualChild(int index) {
				return _child;
			}
		}

		sealed class SymbolItem : ThemedMenuItem
		{
			public SymbolItem(ISymbol symbol) {
				Icon = ThemeHelper.GetImage(symbol.GetImageId());
				Header = new ThemedTipText().Append(symbol.ContainingType != null ? symbol.ContainingType.Name + symbol.ContainingType.GetParameterString() + "." : String.Empty, ThemeHelper.SystemGrayTextBrush).Append(symbol.Name).Append(symbol.GetParameterString(), ThemeHelper.SystemGrayTextBrush);
				Symbol = symbol;
			}
			public ISymbol Symbol { get; }

			protected override void OnClick() {
				base.OnClick();
				Symbol?.GoToSource();
			}
		}
	}
}
