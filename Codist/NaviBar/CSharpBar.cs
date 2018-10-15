using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using System.Windows;
using Codist.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Task = System.Threading.Tasks.Task;

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
			Resources = SharedDictionaryManager.Menu;
			SetResourceReference(BackgroundProperty, VsBrushes.CommandBarMenuBackgroundGradientKey);
			SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextInactiveKey);
			Items.Add(new RootItem(this));
			_View.Selection.SelectionChanged += Update;
			_View.Closed += ViewClosed;
			Update(this, EventArgs.Empty);
		}

		protected override void OnMouseMove(MouseEventArgs e) {
			base.OnMouseMove(e);
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
				for (int i = Items.Count - 1; i > 0; i--) {
					Items.RemoveAt(i);
				}
				foreach (var item in nodes) {
					if (token.IsCancellationRequested) {
						return;
					}
					Items.Add(new NaviItem(this, item));
				}
			}
		}

		void ViewClosed(object sender, EventArgs e) {
			_View.Selection.SelectionChanged -= Update;
			CancellationHelper.CancelAndDispose(ref _cancellationSource, false);
			_View.Closed -= ViewClosed;
		}

		async Task<IEnumerable<SyntaxNode>> UpdateModelAsync(CancellationToken token) {
			if (await _SemanticContext.UpdateAsync(_View.Selection.Start.Position, token) == false) {
				return Array.Empty<SyntaxNode>();
			}
			var node = _SemanticContext.Node;
			var nodes = new List<SyntaxNode>(5);
			while (node != null) {
				if (node.FullSpan.Contains(_View.Selection, true)
					&& node.IsKind(SyntaxKind.VariableDeclaration) == false
					&& (node.IsSyntaxBlock() || node.IsDeclaration())) {
					nodes.Add(node);
				}
				node = node.Parent;
			}
			nodes.Reverse();
			return nodes;
		}

		TextBlock GetSignature(SyntaxNode child) {
			var title = child.GetDeclarationSignature();
			if (child.IsTypeDeclaration()) {
				var p = child.Parent;
				while (p.IsTypeDeclaration()) {
					title = "..." + title;
					p = p.Parent;
				}
			}
			var t = new ThemedTipText()
				.Append(title, child.FullSpan.Contains(_SemanticContext.Position));
			if (child is BaseMethodDeclarationSyntax) {
				t.Append(GetParameterListSignature((child as BaseMethodDeclarationSyntax).ParameterList), new SolidColorBrush(EnvironmentColors.SystemGrayTextColorKey.GetWpfColor()));
			}
			else if (child.IsKind(SyntaxKind.DelegateDeclaration)) {
				t.Append(" " + GetParameterListSignature((child as DelegateDeclarationSyntax).ParameterList));
			}
			else if (child is OperatorDeclarationSyntax) {
				t.Append(" " + GetParameterListSignature((child as OperatorDeclarationSyntax).ParameterList));
			}
			return t;
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

		static void AddItemPlaceHolder(MenuItem item) {
			item.Items.Add(new MenuItem { Visibility = Visibility.Collapsed });
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

		sealed class MemberFinderBox : ThemedTextBox
		{
			readonly ItemCollection _Items;
			readonly CSharpBar _Bar;
			readonly int _FilterOffset = 0;

			public MemberFinderBox(ItemCollection items, CSharpBar bar) {
				_Bar = bar;
				_Items = items;
			}
			protected override void OnTextChanged(TextChangedEventArgs e) {
				base.OnTextChanged(e);
				for (int i = _Items.Count - 1; i > _FilterOffset; i--) {
					_Items.RemoveAt(i);
				}
				var s = Text;
				if (s.Length == 0) {
					return;
				}
				var members = _Bar._SemanticContext.Compilation.GetDecendantDeclarations();
				foreach (var item in members) {
					if (item.GetDeclarationSignature().IndexOf(s, StringComparison.OrdinalIgnoreCase) != -1) {
						_Items.Add(new NaviItem(_Bar, item, i => i.Header = _Bar.GetSignature(item), i => i.GoToLocation()));
					}
				}
			}
		}
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
						(_Items[i] as MenuItem).Visibility = Visibility.Visible;
					}
					return;
				}
				for (int i = _Items.Count - 1; i > _FilterOffset; i--) {
					var item = _Items[i] as MenuItem;
					var t = ((_Items[i] as MenuItem)?.Header as TextBlock)?.GetText();
					if (t == null) {
						continue;
					}
					item.Visibility = t.IndexOf(s, StringComparison.OrdinalIgnoreCase) != -1
						? Visibility.Visible
						: Visibility.Collapsed;
				}
			}
		}
		sealed class RootItem : MenuItem
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
				Tag = new StackPanel {
					Margin = WpfHelper.MenuItemMargin,
					Children = {
						new StackPanel {
							Children = {
								ThemeHelper.GetImage(KnownImageIds.FindSymbol).WrapMargin(WpfHelper.GlyphMargin),
								(_FinderBox = new MemberFinderBox(Items, _Bar) { MinWidth = 150 }),
							},
							Orientation = Orientation.Horizontal
						},
						//new StackPanel {
						//	Children = {
						//		new ThemedTipText("Goto: "),
						//		new ThemedButton(KnownImageIds.NextError, "Go to next error", () => TextEditorHelper.ExecuteEditorCommand("View.NextError")),
						//		new ThemedButton(KnownImageIds.Task, "Go to next task", () => TextEditorHelper.ExecuteEditorCommand("View.NextTask")),
						//	},
						//	Orientation = Orientation.Horizontal
						//}
					}
				};
				AddItemPlaceHolder(this);
			}
		}
		sealed class NaviItem : MenuItem
		{
			readonly Action<NaviItem> _ClickHandler;
			readonly CSharpBar _Bar;

			public NaviItem(CSharpBar bar, SyntaxNode node) : this(bar, node, null, null) {}
			public NaviItem(CSharpBar bar, SyntaxNode node, Action<NaviItem> initializer, Action<NaviItem> clickHandler) {
				Node = node;
				_ClickHandler = clickHandler;
				_Bar = bar;

				Icon = ThemeHelper.GetImage(node.GetImageId());
				if (initializer == null) {
					SetHeader(node);
				}
				else {
					initializer(this);
				}
				ToolTip = String.Empty;
				this.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
				SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextActiveKey);
				ToolTipOpening += NaviItem_ToolTipOpening;
			}

			internal SyntaxNode Node { get; }

			void NaviItem_ToolTipOpening(object sender, ToolTipEventArgs e) {
				var cs = _Bar._cancellationSource;
				if (cs == null) {
					return;
				}
				var symbol = _Bar._SemanticContext.GetSymbol(Node, cs.Token);
				ToolTip = symbol != null
					? ToolTipFactory.CreateToolTip(symbol, _Bar._SemanticContext.SemanticModel.Compilation)
					: (object)Node.GetSyntaxBrief();
				this.SetTipOptions();
				ToolTipOpening -= NaviItem_ToolTipOpening;
			}

			protected override void OnClick() {
				base.OnClick();
				if (_ClickHandler != null) {
					_ClickHandler(this);
				}
				else if (HasItems == false) {
					AddItems(Items, Node);
					IsSubmenuOpen = true;
				}
			}

			public void GoToLocation() {
				Node.GetLocation().GoToSource();
			}

			NaviItem SetHeader(SyntaxNode node) {
				var title = node.GetDeclarationSignature();
				if (title != null) {
					if (node.IsTypeDeclaration()) {
						var p = node.Parent;
						while (p.IsTypeDeclaration()) {
							title = "..." + title;
							p = p.Parent;
						}
					}
					if (title.Length > 32) {
						title = title.Substring(0, 32) + "...";
					}
					Header = new ThemedTipText(title, node.IsTypeDeclaration() || node.IsKind(SyntaxKind.NamespaceDeclaration) || node.IsKind(SyntaxKind.CompilationUnit));
				}
				return this;
			}

			void AddItems(ItemCollection items, SyntaxNode node) {
				switch (node.Kind()) {
					case SyntaxKind.NamespaceDeclaration:
						MaxHeight = _Bar._View.ViewportHeight / 2;
						Tag = new StackPanel {
							Children = {
								new NaviItem(_Bar, node, null, i => i.SelectOrGoToSource()),
								new Separator()
							}
						};
						AddTypeDeclarations(node);
						break;
					case SyntaxKind.ClassDeclaration:
					case SyntaxKind.StructDeclaration:
					case SyntaxKind.InterfaceDeclaration:
					case SyntaxKind.EnumDeclaration:
						MaxHeight = _Bar._View.ViewportHeight / 2;
						Tag = new StackPanel {
							Children = {
								new NaviItem(_Bar, node, null, i => i.SelectOrGoToSource()),
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
						AddItemPlaceHolder(this);
						AddMemberDeclarations(node);
						break;
					default:
						SelectOrGoToSource(node);
						break;
				}
			}

			void SelectOrGoToSource() {
				SelectOrGoToSource(Node);
			}
			void SelectOrGoToSource(SyntaxNode node) {
				var span = node.FullSpan;
				if (span.Contains(_Bar._SemanticContext.Position)) {
					_Bar._View.SelectNode(node, Keyboard.Modifiers != ModifierKeys.Control);
				}
				else {
					node.GetLocation().GoToSource();
				}
			}

			void AddTypeDeclarations(SyntaxNode node) {
				foreach(var child in node.ChildNodes()) {
					if (child.IsTypeDeclaration() == false) {
						continue;
					}
					Items.Add(new NaviItem(_Bar, child, i => i.Header = i._Bar.GetSignature(i.Node), i => i.SelectOrGoToSource(i.Node)));
					AddTypeDeclarations(child);
				}
			}

			void AddMemberDeclarations(SyntaxNode node) {
				foreach (var child in node.ChildNodes()) {
					if (child.IsMemberDeclaration() == false && child.IsTypeDeclaration() == false) {
						continue;
					}
					if (child.IsKind(SyntaxKind.FieldDeclaration) || child.IsKind(SyntaxKind.EventFieldDeclaration)) {
						AddVariables((child as BaseFieldDeclarationSyntax).Declaration.Variables);
					}
					else {
						Items.Add(new NaviItem(_Bar, child, i => i.Header = _Bar.GetSignature(child), i => i.GoToLocation()));
					}
				}
			}

			void AddVariables(SeparatedSyntaxList<VariableDeclaratorSyntax> fields) {
				foreach (var item in fields) {
					Items.Add(new NaviItem(_Bar, item));
				}
			}

		}
	}
}
