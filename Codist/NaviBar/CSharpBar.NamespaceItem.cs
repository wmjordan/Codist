using System;
using System.Diagnostics.CodeAnalysis;
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
			NamespaceNode _Node;
			bool _Disposed;

			public NamespaceItem(CSharpBar bar, NamespaceNode node) : base(bar, IconIds.Namespace, new ThemedToolBarText()) {
				_Node = node;
				((TextBlock)Header).Text = node.Name;
				Click += HandleClick;
				this.SetLazyToolTip(() => new CommandToolTip(IconIds.Namespace, R.CMD_SearchWithinNamespace, new TextBlock { TextWrapping = TextWrapping.Wrap }.Append(R.CMDT_SearchWithinNamespace)));
				this.SetTipPlacementBottom();
			}

			public override BarItemType ItemType => BarItemType.Namespace;
			public bool IsSymbolNode { get; }
			public ISymbol Symbol => _Node.GetSymbol(Bar._SemanticContext);

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void HandleClick(object sender, RoutedEventArgs e) {
				SyncHelper.CancelAndDispose(ref Bar._CancellationSource, true);
				if (_Menu != null && Bar._SymbolList == _Menu && _Menu.IsVisible) {
					Bar.HideMenu();
					return;
				}
				var ct = Bar._CancellationSource.GetToken();
				try {
					await CreateMenuForNamespaceNodeAsync(ct);
					await SyncHelper.SwitchToMainThreadAsync(ct);
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
					Container = Bar.ViewOverlay,
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
				var items = await Bar._SemanticContext.GetNamespacesAndTypesAsync(Symbol as INamespaceSymbol, cancellationToken).ConfigureAwait(false);
				await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
				_Menu.AddNamespaceItems(items, Bar.GetChildSymbolOnNaviBar(this));
			}

			void FilterChanged(object sender, SymbolFilterBox.FilterEventArgs e) {
				if (e.FilterText.Length == 0) {
					SelectChild();
				}
			}

			async Task RefreshItemsAsync(CancellationToken cancellationToken) {
				var ctx = Bar._SemanticContext;
				var sm = ctx.SemanticModel;
				await ctx.UpdateAsync(cancellationToken).ConfigureAwait(false);
				if (sm != ctx.SemanticModel) {
					_Menu.ClearSymbols();
					var items = await ctx.GetNamespacesAndTypesAsync(Symbol as INamespaceSymbol, cancellationToken).ConfigureAwait(false);
					await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
					_Menu.AddNamespaceItems(items, Bar.GetChildSymbolOnNaviBar(this));
					_Menu.RefreshItemsSource(true);
				}
				else {
					SelectChild();
				}
			}

			void SelectChild() {
				var child = Bar.GetChildSymbolOnNaviBar(this);
				if (child != null && _Menu.HasItems) {
					var c = CodeAnalysisHelper.GetSpecificSymbolComparer(child);
					foreach (var item in _Menu.Symbols) {
						var s = item.Symbol;
						if (c(s)) {
							_Menu.SelectedItem = s;
							break;
						}
					}
				}
			}

			void IContextMenuHost.ShowContextMenu(RoutedEventArgs args) {
				if (ContextMenu == null) {
					var s = Symbol;
					var m = new CSharpSymbolContextMenu(s, null, Bar._SemanticContext);
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
				if (_Disposed == false) {
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
					if (ContextMenu is IDisposable d) {
						d.Dispose();
						ContextMenu = null;
					}
					DataContext = null;
					_Node = null;
					_Disposed = true;
				}
			}
		}
	}
}
