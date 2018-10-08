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

namespace Codist.CodeBar
{
	public sealed class CSharpNaviBar : Menu
	{
		readonly IWpfTextView _View;
		readonly SemanticContext _SemanticContext;
		CancellationTokenSource _cancellationSource = new CancellationTokenSource();

		public CSharpNaviBar(IWpfTextView textView) {
			_View = textView;
			_View.Selection.SelectionChanged += Update;
			_SemanticContext = new SemanticContext(textView);
			ImageThemingUtilities.SetImageBackgroundColor(this, ThemeHelper.TitleBackgroundColor);
			Resources = SharedDictionaryManager.Menu;
			SetResourceReference(BackgroundProperty, VsBrushes.CommandBarMenuBackgroundGradientKey);
			SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextInactiveKey);
			Items.Add(new RootItem(this));
			Update(this, EventArgs.Empty);
		}

		async void Update(object sender, EventArgs e) {
			_cancellationSource.Cancel();
			_cancellationSource = new CancellationTokenSource();
			var token = _cancellationSource.Token;
			try {
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
			catch (TaskCanceledException) {
				// ignore
			}
		}

		async Task<IEnumerable<SyntaxNode>> UpdateModelAsync(CancellationToken token) {
			if (await _SemanticContext.UpdateAsync(_View.Selection.Start.Position, false, token) == false) {
				return Array.Empty<SyntaxNode>();
			}
			var node = _SemanticContext.Node;
			var nodes = new List<SyntaxNode>(5);
			while (node != null) {
				if (node.FullSpan.Contains(_View.Selection, false)
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
			var t = new Controls.ThemedTipText()
				.Append(title, child.FullSpan.Contains(_SemanticContext.Position));
			if (child is BaseMethodDeclarationSyntax) {
				t.Append(GetParameterListSignature((child as BaseMethodDeclarationSyntax).ParameterList), new System.Windows.Media.SolidColorBrush(EnvironmentColors.SystemGrayTextColorKey.ToThemedWpfColor()));
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

		sealed class MemberFinderBox : Controls.ThemedTextBox
		{
			readonly ItemCollection _Items;
			readonly CSharpNaviBar _Bar;
			readonly int _FilterOffset;

			public MemberFinderBox(ItemCollection items, int filterOffset, CSharpNaviBar bar) {
				_Bar = bar;
				_FilterOffset = filterOffset;
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
		sealed class FilterBox : Controls.ThemedTextBox
		{
			readonly ItemCollection _Items;
			readonly int _FilterOffset;

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
			readonly CSharpNaviBar _Bar;
			public RootItem(CSharpNaviBar bar) {
				_Bar = bar;
				Icon = ThemeHelper.GetImage(KnownImageIds.CSProjectNode);
				ImageThemingUtilities.SetImageBackgroundColor(this, ThemeHelper.TitleBackgroundColor);
				SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextActiveKey);
				Header = new Controls.ThemedTipText("//");
				Items.Add(new MenuItem {
					Icon = ThemeHelper.GetImage(KnownImageIds.FindSymbol),
					MinWidth = 150,
					Header = new StackPanel {
						Children = {
							new Controls.ThemedTipText("Find symbol in document:"),
							new MemberFinderBox(Items, 0, _Bar),
						}
					}
				});
			}
		}
		sealed class NaviItem : MenuItem
		{
			readonly SyntaxNode _Node;
			readonly Action<NaviItem> _ClickHandler;
			readonly CSharpNaviBar _Bar;

			public NaviItem(CSharpNaviBar bar, SyntaxNode node) : this(bar, node, null, null) {}
			public NaviItem(CSharpNaviBar bar, SyntaxNode node, Action<NaviItem> initializer, Action<NaviItem> clickHandler) {
				_Node = node;
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
				ImageThemingUtilities.SetImageBackgroundColor(this, ThemeHelper.TitleBackgroundColor);
				SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextActiveKey);
				ToolTipOpening += NaviItem_ToolTipOpening;
			}

			void NaviItem_ToolTipOpening(object sender, ToolTipEventArgs e) {
				var symbol = _Bar._SemanticContext.SemanticModel.GetDeclaredSymbol(_Node, _Bar._cancellationSource.Token);
				ToolTip = symbol != null
					? ToolTipFactory.CreateToolTip(symbol, _Bar._SemanticContext.SemanticModel.Compilation)
					: (object)_Node.GetSyntaxBrief();
				ToolTipOpening -= NaviItem_ToolTipOpening;
			}

			protected override void OnClick() {
				if (_ClickHandler != null) {
					_ClickHandler(this);
				}
				else if (HasItems == false) {
					AddItems(Items, _Node);
					IsSubmenuOpen = true;
				}
				base.OnClick();
			}

			public void GoToLocation() {
				_Node.GetLocation().GoToSource();
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
					Header = new Controls.ThemedTipText(title, node.IsTypeDeclaration() || node.IsKind(SyntaxKind.NamespaceDeclaration) || node.IsKind(SyntaxKind.CompilationUnit));
				}
				return this;
			}

			void AddItems(ItemCollection items, SyntaxNode node) {
				switch (node.Kind()) {
					case SyntaxKind.NamespaceDeclaration:
						Items.Add(new NaviItem(_Bar, node, null, i => i.GoToLocation()));
						Items.Add(new Separator());
						AddTypeDeclarations(node);
						break;
					case SyntaxKind.ClassDeclaration:
					case SyntaxKind.StructDeclaration:
					case SyntaxKind.InterfaceDeclaration:
					case SyntaxKind.EnumDeclaration:
						Items.Add(new NaviItem(_Bar, node, null, i => i.GoToLocation()));
						Items.Add(new MenuItem {
							Icon = ThemeHelper.GetImage(KnownImageIds.Filter),
							MinWidth = 150,
							Header = new FilterBox(Items, 2)
						});
						Items.Add(new Separator());
						AddMemberDeclarations(node);
						break;
					default:
						SelectOrGoToSource(node);
						break;
				}
			}

			void SelectOrGoToSource(SyntaxNode node) {
				var span = node.FullSpan;
				if (span.Contains(_Bar._SemanticContext.Position)) {
					if (_Bar._View.TextSnapshot.Length > span.End) {
						_Bar._View.Selection.Select(new Microsoft.VisualStudio.Text.SnapshotSpan(_Bar._View.TextSnapshot, span.Start, span.Length), false);
					}
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
					Items.Add(new NaviItem(_Bar, child, i => i.Header = i._Bar.GetSignature(i._Node), i => i.SelectOrGoToSource(i._Node)));
					AddTypeDeclarations(child);
				}
			}

			void AddMemberDeclarations(SyntaxNode node) {
				foreach (var child in node.ChildNodes()) {
					if (child.IsMemberDeclaration() == false && child.IsTypeDeclaration() == false) {
						continue;
					}
					if (child.IsKind(SyntaxKind.FieldDeclaration)) {
						AddVariables((child as FieldDeclarationSyntax).Declaration.Variables);
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
