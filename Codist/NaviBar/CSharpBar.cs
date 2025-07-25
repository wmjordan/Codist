﻿using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CLR;
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

namespace Codist.NaviBar
{
	public sealed partial class CSharpBar : NaviBar
	{
		internal const string SyntaxNodeRange = nameof(SyntaxNodeRange);
		static string __ProjectWideSearchExpression;

		SemanticContext _SemanticContext;

		CancellationTokenSource _CancellationSource = new CancellationTokenSource();
		RootItem _RootItem;
		GlobalNamespaceItem _GlobalNamespaceItem;
		NodeItem _MouseHoverItem;
		SymbolList _SymbolList;
		ThemedImageButton _ActiveItem;
		ITextBuffer _Buffer;

		public CSharpBar(IWpfTextView view) : base(view) {
			Name = nameof(CSharpBar);
			BindView();
			Items.Add(_RootItem = new RootItem(this));
			Items.Add(_GlobalNamespaceItem = new GlobalNamespaceItem(this));
			Update(this, EventArgs.Empty);
			view.Closed += View_Closed;
			LayoutUpdated += CSharpBar_LayoutUpdated;
		}

		void CSharpBar_LayoutUpdated(object sender, EventArgs e) {
			AdjustItems();
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
				ViewOverlay.ClearRangeAdornments();
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
			ViewOverlay.ClearRangeAdornments();
			_MouseHoverItem = null;
		}

		internal protected override void BindView() {
			if (_SemanticContext != null) {
				UnbindView();
			}
			_Buffer = View.TextBuffer;
			View.TextBuffer.ChangedLowPriority += TextBuffer_Changed;
			View.Caret.PositionChanged += Update;
			ViewOverlay.ChildRemoved += ViewOverlay_MenuRemoved;
			Config.RegisterUpdateHandler(UpdateCSharpNaviBarConfig);
			SyncHelper.CancelAndDispose(ref _CancellationSource, true);
			_SemanticContext = SemanticContext.GetOrCreateSingletonInstance(View);
		}

		protected override void UnbindView() {
			View.Caret.PositionChanged -= Update;
			View.TextBuffer.ChangedLowPriority -= TextBuffer_Changed;
			ViewOverlay.ChildRemoved -= ViewOverlay_MenuRemoved;
			Config.UnregisterUpdateHandler(UpdateCSharpNaviBarConfig);
		}

		void HighlightNodeRanges(SyntaxNode node, SnapshotSpan span) {
			ViewOverlay.AddRangeAdornment(span, ThemeCache.MenuHoverBackgroundColor, 3);
			var p = View.Caret.Position.BufferPosition;
			if (span.Contains(p) == false) {
				return;
			}
			var n = _SemanticContext.GetNode(p, false, false);
			while (n != null && node.Contains(n)) {
				var nodeSpan = n.Span;
				if (nodeSpan.Start != span.Start
					&& nodeSpan.Length != span.Length) {
					var nodeKind = n.Kind();
					if (nodeKind != SyntaxKind.Block) {
						span = nodeSpan.CreateSnapshotSpan(View.TextSnapshot);
						ViewOverlay.AddRangeAdornment(span, ThemeCache.MenuHoverBackgroundColor, nodeKind.IsSyntaxBlock() || nodeKind.IsDeclaration() ? 1 : 0);
					}
				}
				n = n.Parent;
			}
		}

		protected override void OnMouseLeave(MouseEventArgs e) {
			base.OnMouseLeave(e);
			ViewOverlay?.ClearRangeAdornments();
			_MouseHoverItem = null;
		}

		void TextBuffer_Changed(object sender, TextContentChangedEventArgs e) {
			HideMenu();
			_RootItem.ClearSymbolList();
		}

		[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
		async void Update(object sender, EventArgs e) {
			HideMenu();
			if (_CancellationSource != null) {
				try {
					await UpdateAsync(SyncHelper.CancelAndRetainToken(ref _CancellationSource)).ConfigureAwait(false);
				}
				catch (OperationCanceledException) {
					// ignore
				}
			}
		}

		async Task UpdateAsync(CancellationToken token) {
			var nodes = await UpdateModelAndGetContainingNodesAsync(token);
			await SyncHelper.SwitchToMainThreadAsync(token);
			ItemCollection items = Items;
			int ic = items.Count, c = Math.Min(ic, nodes.Length);
			int i, i2;
			#region Remove outdated nodes on NaviBar
			const int FirstNodeIndex = 2; // exclude root and globalNs node
			for (i = FirstNodeIndex, i2 = 0; i < c && i2 < c; i2++) {
				if (token.IsCancellationRequested) {
					return;
				}
				CHECK_NODE:
				if (items[i] is BarItem ni) {
					switch (ni.ItemType) {
						case BarItemType.Node:
							if (((NodeItem)ni).Node == nodes[i2]) {
								// keep the item if corresponding node is not updated
								++i;
								continue;
							}
							break;
						case BarItemType.Namespace:
							i = FirstNodeIndex;
							break;
					}
				}
				break;
			}
			c = items.Count;
			if (i == FirstNodeIndex && _RootItem.FilterText.Length == 0) {
				// clear type and namespace menu items if a type is changed
				_RootItem.ClearSymbolList();
			}
			// remove outdated nodes
			while (c > i) {
				items.RemoveAndDisposeAt(--c);
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
						var header = newItem.Header;
						header.FontWeight = FontWeights.Bold;
						header.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.FileTabSelectedTextBrushKey);
						newItem.IsChecked = true;
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.ReferencingTypes)) {
							newItem.ReferencedSymbols.AddRange(node.FindRelatedTypes(_SemanticContext.SemanticModel, token).Take(5));
						}
					}
					items.Add(newItem);
				}
				++i2;
			}
			#endregion
			#region Add external referenced nodes in node range
			if (memberNode == null) {
				memberNode = items.GetFirst((Predicate<NodeItem>)(n => n.HasReferencedSymbols));
			}
			if (memberNode?.HasReferencedSymbols == true) {
				foreach (var doc in memberNode.ReferencedSymbols) {
					items.Add(new SymbolNodeItem(doc));
				}
			}
			#endregion
		}

		void AdjustItems() {
			var w = 0d;
			var items = Items;
			foreach (BarItem item in items) {
				w += item.DesiredSize.Width;
			}

			if (w <= this.RenderSize.Width) {
				return;
			}
			var l = items.Count;
			for (int i = l - 2; i > 1; i--) { // skip last node and home node
				if (items[i] is BarItem n) {
					if (n.IsChecked) {
						break;
					}
					if (n.ItemType > BarItemType.GlobalNamespace && n.IsHeaderVisible) {
						n.IsHeaderVisible = false;
						return;
					}
				}
			}
			for (int i = 2; i < l - 1; i++) {
				if (items[i] is BarItem n) {
					if (n.IsChecked) {
						break;
					}
					if (n.ItemType > BarItemType.GlobalNamespace && n.IsHeaderVisible) {
						n.IsHeaderVisible = false;
						return;
					}
				}
			}
		}

		void AddNamespaceNodes(SyntaxNode node, NameSyntax ns) {
			var name = ns.GetName();
			NamespaceNode nn = null;
			if (node.Parent.IsKind(SyntaxKind.NamespaceDeclaration) == false) {
				var nb = ImmutableArray.CreateBuilder<NameSyntax>();
				while (ns is QualifiedNameSyntax q) {
					ns = q.Left;
					nb.Add(ns);
				}
				for (var i = nb.Count - 1; i >= 0; i--) {
					Items.Add(new NamespaceItem(this, nn = new NamespaceNode(nb[i].GetName(), nn)));
				}
			}
			Items.Add(new NamespaceItem(this, new NamespaceNode(name, nn)));
		}

		async Task<ImmutableArray<SyntaxNode>> UpdateModelAndGetContainingNodesAsync(CancellationToken token) {
			var position = View.GetCaretPosition();
			var ctx = _SemanticContext;
			if (ctx == null
				|| await ctx.UpdateAsync(position, token).ConfigureAwait(false) == false
				|| ctx.Compilation == null) {
				return ImmutableArray<SyntaxNode>.Empty;
			}
			// if the caret is at the beginning of the document, move to the first type declaration
			if (position == 0) {
				var n = ctx.Compilation.GetDescendantDeclarations(token).FirstOrDefault();
				if (n != null) {
					position = new SnapshotPoint(position.Snapshot, n.SpanStart);
				}
			}
			await SyncHelper.SwitchToMainThreadAsync(token);
			return ctx.GetContainingNodes(position,
				Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.SyntaxDetail),
				Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.RegionOnBar));
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
				if (Items[i] is NodeItem item
					&& item.Node.Kind().IsTypeDeclaration()) {
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
				ViewOverlay.Remove(l);
				l = menu;
				ViewOverlay.Add(menu);
				if (_ActiveItem != null) {
					_ActiveItem.IsHighlighted = false;
				}
			}
			_ActiveItem = barItem;
			barItem.IsHighlighted = true;
			menu.ItemsControlMaxHeight = ViewOverlay.DisplayHeight / 2;
			menu.RefreshItemsSource();
			menu.ScrollToSelectedItem();
			menu.PreviewKeyUp -= OnMenuKeyUp;
			menu.PreviewKeyUp += OnMenuKeyUp;
			PositionMenu();
			if (ViewOverlay.Contains(menu) == false) {
				ViewOverlay.Add(menu);
			}
		}

		void PositionMenu() {
			if (_SymbolList != null && _ActiveItem != null) {
				var p = _ActiveItem.TransformToVisual(View.VisualElement).Transform(new Point());
				ViewOverlay.Position(_SymbolList, new Point(p.X, p.Y - 1 + _ActiveItem.ActualHeight), 30);
			}
		}

		void OnMenuKeyUp(object sender, KeyEventArgs e) {
			if (e.Key == Key.Escape) {
				HideMenu();
				e.Handled = true;
			}
			else if (e.Key == Key.Tab && _ActiveItem != null) {
				int i;
				if (UIHelper.IsShiftDown) {
					if ((i = Items.IndexOf(_ActiveItem)) > 0) {
						((ThemedImageButton)Items[i - 1]).PerformClick();
					}
				}
				else if ((i = Items.IndexOf(_ActiveItem)) < Items.Count - 1) {
					((ThemedImageButton)Items[i + 1]).PerformClick();
				}
				e.Handled = true;
			}
		}

		void HideMenu() {
			var l = _SymbolList;
			if (l != null) {
				ViewOverlay.Remove(l);
			}
		}

		void ViewOverlay_MenuRemoved(object sender, OverlayElementRemovedEventArgs e) {
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
			if (menu.SelectedItem is SymbolItem i) {
				GoToSourceAndHideMenuAsync(this, i);
			}

			async void GoToSourceAndHideMenuAsync(CSharpBar me, SymbolItem item) {
				try {
					if (await item.GoToSourceAsync()) {
						me.HideMenu();
					}
				}
				catch (OperationCanceledException) {
					// ignore
				}
				catch (Exception ex) {
					await SyncHelper.SwitchToMainThreadAsync();
					MessageWindow.Error(ex, R.T_ErrorNavigatingToSource, null, typeof(SymbolItem));
				}
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
					t.Append(bt.Identifier.ValueText + ".", ThemeCache.SystemGrayTextBrush);
				}
			}
			t.Append(title, highlight, false, SymbolFormatter.Instance.GetBrush(node));
			if (includeParameterList && Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.ParameterList)) {
				AddParameterList(t, node);
			}
			return t;
		}

		ISymbol GetChildSymbolOnNaviBar(BarItem item) {
			var p = Items.IndexOf(item);
			return p != -1 && p < Items.Count - 1 ? (Items[p + 1] as ISymbolContainer)?.Symbol : null;
		}

		static void AddParameterList(TextBlock text, SyntaxNode node) {
			BaseParameterListSyntax p;
			if (node is BaseMethodDeclarationSyntax bm) {
				p = bm.ParameterList;
			}
			else if (node.IsKind(SyntaxKind.DelegateDeclaration)) {
				p = ((DelegateDeclarationSyntax)node).ParameterList;
			}
			else if (node is OperatorDeclarationSyntax o) {
				p = o.ParameterList;
			}
			else if (node is IndexerDeclarationSyntax id) {
				p = id.ParameterList;
			}
            else {
				return;
			}
			if (p != null) {
				text.Append(p.GetParameterListSignature(Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.ParameterListShowParamName)), SymbolFormatter.SemiTransparent.PlainText);
			}
		}

		static void AddReturnType(SymbolItem item, SyntaxNode node) {
			if (String.IsNullOrEmpty(item.Hint)) {
				var t = node.GetMemberDeclarationType();
				if (t != null) {
					item.Hint = t.ToString();
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
			LayoutUpdated -= CSharpBar_LayoutUpdated;
			_SemanticContext = null;
			_Buffer = null;
			_CancellationSource.CancelAndDispose();
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
				ViewOverlay?.Remove(l);
				l.Dispose();
			}
		}

		abstract class BarItem : ThemedImageButton, IDisposable
		{
			protected BarItem(CSharpBar bar, int imageId, TextBlock content) : base(imageId, content) {
				Bar = bar;
				this.ReferenceCrispImageBackground(EnvironmentColors.MainWindowActiveCaptionColorKey)
					.ReferenceProperty(ForegroundProperty, VsBrushes.CommandBarTextActiveKey);
			}

			protected CSharpBar Bar { get; private set; }
			public abstract BarItemType ItemType { get; }
			public override void Dispose() {
				base.Dispose();
				Bar = null;
			}
		}

		sealed class SymbolNodeItem : ThemedImageButton
		{
			ISymbol _Symbol;

			public SymbolNodeItem(ISymbol symbol)
				: base (IconIds.GoToDefinition, new ThemedMenuText(symbol.GetOriginalName())) {
				_Symbol = symbol;
				Opacity = 0.8;
				Unloaded += SymbolNodeItem_Unloaded;
			}

			void SymbolNodeItem_Unloaded(object sender, RoutedEventArgs e) {
				Unloaded -= SymbolNodeItem_Unloaded;
				_Symbol = null;
			}

			protected override void OnClick() {
				base.OnClick();
				_Symbol.GoToSource();
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
		[Flags]
		enum MemberListOptions
		{
			None,
			ShowPartial = 1,
			ShowBase = 1 << 1
		}

		sealed class NamespaceNode
		{
			public readonly string Name;
			public readonly NamespaceNode Parent;

			public NamespaceNode(string name, NamespaceNode parent) {
				Name = name;
				Parent = parent;
			}

			public INamespaceSymbol GetSymbol(SemanticContext context) {
				return Parent != null
					? FindSymbolFromParent(Parent.GetSymbol(context))
					: FindSymbolFromParent(context.SemanticModel.Compilation.GlobalNamespace);
			}

			INamespaceSymbol FindSymbolFromParent(INamespaceSymbol p) {
				foreach (var n in p.ConstituentNamespaces) {
					foreach (var item in n.GetNamespaceMembers()) {
						if (item.Name == Name) {
							return item;
						}
					}
				}
				return null;
			}
		}
	}
}
