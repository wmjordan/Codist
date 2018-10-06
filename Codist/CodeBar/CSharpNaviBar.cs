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
	sealed class CSharpNaviBar : Menu
	{
		readonly IWpfTextView _View;
		readonly CancellationTokenSource _cancellationSource = new CancellationTokenSource();
		readonly SemanticContext _SemanticContext;

		public CSharpNaviBar(IWpfTextView textView) {
			_View = textView;
			_View.Selection.SelectionChanged += Update;
			_SemanticContext = new SemanticContext(textView);
			ImageThemingUtilities.SetImageBackgroundColor(this, ThemeHelper.TitleBackgroundColor);
			Resources = SharedDictionaryManager.Menu;
			SetResourceReference(BackgroundProperty, VsBrushes.CommandBarMenuBackgroundGradientKey);
			SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextInactiveKey);
			Update(this, EventArgs.Empty);
		}

		async void Update(object sender, EventArgs e) {
			var token = _cancellationSource.Token;
			try {
				var nodes = await UpdateModelAsync(token);
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
				Items.Clear();
				foreach (var item in nodes) {
					var n = new NaviItem(this, item);
					if (item.IsKind(SyntaxKind.NamespaceDeclaration) || item.IsTypeDeclaration()) {
						(n.Header as TextBlock).FontWeight = FontWeights.Bold;
					}
					Items.Add(n);
				}
			}
			catch (TaskCanceledException) {
				// ignore
			}
		}

		async Task<IEnumerable<SyntaxNode>> UpdateModelAsync(CancellationToken token) {
			if (await _SemanticContext.UpdateAsync(_View.Selection.Start.Position, token) == false) {
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

		sealed class FilterBox : TextBox
		{
			readonly ItemCollection _Items;
			readonly int _FilterOffset;

			public FilterBox(ItemCollection items, int filterOffset) {
				_Items = items;
				_FilterOffset = filterOffset;
				BorderThickness = new Thickness(0, 0, 0, 1);
				this.SetStyleResourceProperty(VsResourceKeys.TextBoxStyleKey);
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
				ToolTipOpening += NaviItem_ToolTipOpening;
			}

			void NaviItem_ToolTipOpening(object sender, ToolTipEventArgs e) {
				var symbol = _Bar._SemanticContext.SemanticModel.GetDeclaredSymbol(_Node, _Bar._cancellationSource.Token);
				ToolTip = symbol != null
					? ToolTipFactory.CreateToolTip(symbol, _Bar._SemanticContext.SemanticModel.Compilation)
					: null;
				ToolTipOpening -= NaviItem_ToolTipOpening;
			}

			NaviItem SetHeader(SyntaxNode node) {
				var title = node.GetDeclarationSignature();
				if (title != null) {
					if (node.IsTypeDeclaration()) {
						var p = node.Parent;
						if (p.IsTypeDeclaration()) {
							title = "..." + title;
						}
					}
					Header = new Controls.ThemedTipText(title);
				}
				return this;
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

			void AddItems(ItemCollection items, SyntaxNode node) {
				switch (node.Kind()) {
					case SyntaxKind.NamespaceDeclaration:
						Items.Add(new NaviItem(_Bar, node, null, i => i._Node.GetLocation().GoToSource()));
						Items.Add(new Separator());
						AddTypeDeclarations(node);
						break;
					case SyntaxKind.ClassDeclaration:
					case SyntaxKind.StructDeclaration:
					case SyntaxKind.InterfaceDeclaration:
					case SyntaxKind.EnumDeclaration:
						Items.Add(new NaviItem(_Bar, node, null, i => i._Node.GetLocation().GoToSource()));
						Items.Add(new Separator());
						Items.Add(new MenuItem {
							Icon = ThemeHelper.GetImage(KnownImageIds.Filter),
							MinWidth = 150,
							Header = new FilterBox(Items, 2)
						});
						AddMemberDeclarations(node);
						break;
					default:
						if (node.Span.Contains(_Bar._SemanticContext.Position)) {
							if (_Bar._View.TextSnapshot.Length > node.Span.End) {
								_Bar._View.Selection.Select(new Microsoft.VisualStudio.Text.SnapshotSpan(_Bar._View.TextSnapshot, node.Span.Start, node.Span.Length), false);
							}
						}
						else {
							node.GetLocation().GoToSource();
						}
						break;
				}
			}

			void AddTypeDeclarations(SyntaxNode node) {
				foreach(var child in node.ChildNodes()) {
					if (child.IsTypeDeclaration() == false) {
						continue;
					}
					Items.Add(new NaviItem(_Bar, child, null, i => i._Node.GetLocation().GoToSource()));
					AddTypeDeclarations(child);
				}
			}

			void AddMemberDeclarations(SyntaxNode node) {
				foreach (var child in node.ChildNodes()) {
					if (child.IsMemberDeclaration() == false && child.IsTypeDeclaration() == false) {
						continue;
					}
					Items.Add(new NaviItem(_Bar, child, i => i.Header = GetSignature(child), i => i._Node.GetLocation().GoToSource()));
				}
			}

			TextBlock GetSignature(SyntaxNode child) {
				var t = new Controls.ThemedTipText()
					.Append(child.GetDeclarationSignature(), child.FullSpan.Contains(_Bar._SemanticContext.Position));
				if (child.IsKind(SyntaxKind.MethodDeclaration)) {
					t.Append((child as MethodDeclarationSyntax).ParameterList.ToString());
				}
				return t;
			}
		}
	}
}
