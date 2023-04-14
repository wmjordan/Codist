using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Codist.Controls;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;
using Task = System.Threading.Tasks.Task;
using TH = Microsoft.VisualStudio.Shell.ThreadHelper;

namespace Codist.NaviBar
{
	public sealed partial class CSharpBar
	{
		sealed class GlobalNamespaceItem : BarItem
		{
			SymbolList _Menu;
			SymbolFilterBox _FilterBox;

			public GlobalNamespaceItem(CSharpBar bar) : base(bar, IconIds.GlobalNamespace, new ThemedToolBarText()) {
				Click += HandleClick;
				this.SetLazyToolTip(() => new CommandToolTip(IconIds.GlobalNamespace, R.CMD_GlobalNamespace, new TextBlock { TextWrapping = TextWrapping.Wrap }.Append(R.CMDT_SearchWithinGlobalNamespace)));
				this.SetTipPlacementBottom();
			}

			public override BarItemType ItemType => BarItemType.GlobalNamespace;

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void HandleClick(object sender, RoutedEventArgs e) {
				SyncHelper.CancelAndDispose(ref Bar._CancellationSource, true);
				if (_Menu != null && Bar._SymbolList == _Menu && _Menu.IsVisible) {
					Bar.HideMenu();
					return;
				}
				var ct = Bar._CancellationSource.GetToken();
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
				_Menu.Header = _FilterBox = new SymbolFilterBox(_Menu) { HorizontalAlignment = HorizontalAlignment.Right };
				_FilterBox.FilterChanged += FilterChanged;
				_Menu.Footer = new TextBlock { Margin = WpfHelper.MenuItemMargin }
					.ReferenceProperty(TextBlock.ForegroundProperty, EnvironmentColors.SystemGrayTextBrushKey);
				Bar.SetupSymbolListMenu(_Menu);
				await Bar._SemanticContext.UpdateAsync(cancellationToken).ConfigureAwait(true);
				var d = Bar._SemanticContext.Document;
				if (d != null) {
					var items = await Bar._SemanticContext.GetNamespacesAndTypesAsync((await d.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false)).GlobalNamespace, cancellationToken).ConfigureAwait(false);
					await TH.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
					_Menu.AddNamespaceItems(items, Bar.GetChildSymbolOnNaviBar(this));
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
					_Menu.AddNamespaceItems(items, Bar.GetChildSymbolOnNaviBar(this));
				}
				_Menu.RefreshItemsSource(true);
			}

			void SelectChild(CancellationToken cancellationToken) {
				var child = Bar.GetChildSymbolOnNaviBar(this);
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
	}
}
