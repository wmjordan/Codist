﻿using System;
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
using Task = System.Threading.Tasks.Task;
using TH = Microsoft.VisualStudio.Shell.ThreadHelper;

namespace Codist.NaviBar
{
	public sealed partial class CSharpBar : NaviBar
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

		abstract class BarItem : ThemedImageButton, IDisposable
		{
			protected BarItem(CSharpBar bar, int imageId, TextBlock content) : base(imageId, content) {
				Bar = bar;
				this.ReferenceCrispImageBackground(EnvironmentColors.MainWindowActiveCaptionColorKey)
					.ReferenceProperty(ForegroundProperty, VsBrushes.CommandBarTextActiveKey);
			}

			protected CSharpBar Bar { get; private set; }
			public SyntaxNode Node { get; protected set; }
			public abstract BarItemType ItemType { get; }
			public override void Dispose() {
				base.Dispose();
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
