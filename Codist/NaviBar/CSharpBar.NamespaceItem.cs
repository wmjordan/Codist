using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;
using Task = System.Threading.Tasks.Task;
using TH = Microsoft.VisualStudio.Shell.ThreadHelper;

namespace Codist.NaviBar
{
	public sealed partial class CSharpBar
	{
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
				this.SetLazyToolTip(CreateToolTip);
				ToolTipService.SetPlacement(this, System.Windows.Controls.Primitives.PlacementMode.Bottom);
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
				_Menu.Header = _FilterBox = new SymbolFilterBox(_Menu) { HorizontalAlignment = HorizontalAlignment.Right };
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

			CommandToolTip CreateToolTip() {
				return new CommandToolTip(IconIds.Namespace, R.CMD_SearchWithinNamespace, new TextBlock { TextWrapping = TextWrapping.Wrap }.Append(R.CMDT_SearchWithinNamespace));
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
	}
}
