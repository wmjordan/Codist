using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CLR;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;
using Task = System.Threading.Tasks.Task;

namespace Codist.NaviBar
{
	public sealed partial class CSharpBar
	{
		sealed class RootItem : BarItem, IContextMenuHost
		{
			MemberFinderBox _FinderBox;
			SearchScopeBox _ScopeBox;
			TextBlock _Note;
			SymbolList _Menu;
			IReadOnlyCollection<ISymbol> _IncrementalSearchContainer;
			string _PreviousSearchKeywords;

			public RootItem(CSharpBar bar) : base(bar, IconIds.Search, new ThemedToolBarText()) {
				_Menu = new SymbolList(bar._SemanticContext) {
					Container = Bar.ViewOverlay,
					ContainerType = SymbolListType.NodeList,
					Header = new StackPanel {
						Margin = WpfHelper.MenuItemMargin,
						Children = {
							new Separator { Tag = new ThemedMenuText(R.CMD_SearchDeclaration) },
							new StackPanel {
								Orientation = Orientation.Horizontal,
								Children = {
									VsImageHelper.GetImage(IconIds.Search).WrapMargin(WpfHelper.GlyphMargin),
									(_FinderBox = new MemberFinderBox() { MinWidth = 150, ToolTip = new ThemedToolTip(R.CMD_SearchDeclaration, R.T_SearchMemberTip) }),
									(_ScopeBox = new SearchScopeBox {
										Contents = {
											new ThemedButton(IconIds.ClearFilter, R.CMD_ClearFilter, ClearFilter) { MinHeight = 10 }.ClearSpacing()
										}
									}),
								}
							},
						}
					},
					Footer = _Note = new TextBlock { Margin = WpfHelper.MenuItemMargin }
						.ReferenceProperty(TextBlock.ForegroundProperty, EnvironmentColors.SystemGrayTextBrushKey),
					Owner = this
				};
				Controls.DragDropHelper.SetScrollOnDragDrop(_Menu, true);
				Bar.SetupSymbolListMenu(_Menu);
				_FinderBox.PreviewKeyDown += ChangeSearchScope;
				_FinderBox.TextChanged += SearchCriteriaChanged;
				_FinderBox.IsVisibleChanged += FinderBox_IsVisibleChanged;
				_ScopeBox.FilterChanged += SearchCriteriaChanged;
				_ScopeBox.FilterChanged += ScopeBox_FilterChanged;

				this.SetLazyToolTip(() => new CommandToolTip(IconIds.Search, R.CMD_SearchDeclaration, new TextBlock { TextWrapping = TextWrapping.Wrap }.Append(R.CMDT_SearchDeclaration)));
				this.SetTipPlacementBottom();
			}

			public override BarItemType ItemType => BarItemType.Root;
			public string FilterText => _FinderBox.Text;

			public void ClearSymbolList() {
				_Menu.NeedsRefresh = true;
			}
			internal void SetText(string text) {
				((TextBlock)Header).Text = text;
			}

			protected override void OnClick() {
				base.OnClick();
				if (Bar._SymbolList == _Menu && _Menu.IsVisible) {
					Bar.HideMenu();
					return;
				}
				ShowNamespaceAndTypeMenu((int)ScopeType.Undefined);
			}

			internal void ShowNamespaceAndTypeMenu(int parameter) {
				if (_Menu.NeedsRefresh) {
					_Menu.NeedsRefresh = false;
					_Menu.ClearSymbols();
					_Menu.ItemsSource = null;
				}
				if (Bar._SemanticContext.IsReady == false) {
					return;
				}
				PopulateTypes();
				_Note.Clear();
				if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.LineOfCode)) {
					  _Note.AddImage(IconIds.LineOfCode)
						.Append(Bar.View.TextSnapshot.LineCount);
				}
				Bar.ShowMenu(this, _Menu);
				switch ((ScopeType)parameter) {
					case ScopeType.ActiveDocument: _ScopeBox.Filter = ScopeType.ActiveDocument; break;
					case ScopeType.ActiveProject:
						_ScopeBox.Filter = ScopeType.ActiveProject;
						if (String.IsNullOrWhiteSpace(__ProjectWideSearchExpression) == false
							&& _FinderBox.Text != __ProjectWideSearchExpression) {
							SetAndSelectFinderText();
						}
						break;
				}
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
				int pos = Bar.View.GetCaretPosition();
				var symbols = _Menu.Symbols;
				for (int i = symbols.Count - 1; i >= 0; i--) {
					if (symbols[i].SelectIfContainsPosition(pos)) {
						return;
					}
				}
			}

			void AddNamespaceAndTypes() {
				foreach (var node in Bar._SemanticContext.Compilation.ChildNodes()) {
					if (node.Kind().IsTypeOrNamespaceDeclaration()) {
						_Menu.Add(node);
						AddTypeDeclarations(node);
					}
				}
				MarkEnclosingType();
			}

			void AddTypeDeclarations(SyntaxNode node) {
				foreach (var child in node.ChildNodes()) {
					if (child.Kind().IsTypeOrNamespaceDeclaration()) {
						var i = _Menu.Add(child);
						string prefix = null;
						var p = child.Parent;
						while (p.Kind().IsTypeDeclaration()) {
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

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void SearchCriteriaChanged(object sender, EventArgs e) {
				SyncHelper.CancelAndDispose(ref Bar._CancellationSource, true);
				var ct = Bar._CancellationSource.GetToken();
				try {
					await SyncHelper.SwitchToMainThreadAsync(ct);
					_Menu.ItemsSource = null;
					_Menu.ClearSymbols();
					var s = _FinderBox.Text.Trim();
					if (s.Length == 0) {
						_Menu.ContainerType = SymbolListType.NodeList;
						ShowNamespaceAndTypeMenu((int)ScopeType.Undefined);
						_IncrementalSearchContainer = null;
						_PreviousSearchKeywords = null;
						if (String.IsNullOrWhiteSpace(__ProjectWideSearchExpression) == false && sender == _ScopeBox) {
							SetAndSelectFinderText();
						}
						else if (_ScopeBox.Filter != ScopeType.ActiveDocument) {
							__ProjectWideSearchExpression = String.Empty;
						}
						return;
					}
					_Menu.ContainerType = SymbolListType.None;
					switch (_ScopeBox.Filter) {
						case ScopeType.ActiveDocument:
							FindInDocument(s, ct);
							break;
						case ScopeType.ActiveProject:
							__ProjectWideSearchExpression = s;
							await FindInProjectAsync(s, ct);
							break;
					}
					await SyncHelper.SwitchToMainThreadAsync(ct);
					_Menu.RefreshItemsSource();
					_Menu.UpdateLayout();
				}
				catch (OperationCanceledException) {
					// ignores cancellation
				}
				catch (ObjectDisposedException) { }
			}
			void SetAndSelectFinderText() {
				_FinderBox.Text = __ProjectWideSearchExpression;
				_FinderBox.CaretIndex = _FinderBox.Text.Length;
				_FinderBox.SelectAll();
			}
			void ChangeSearchScope(object sender, KeyEventArgs e) {
				if (!UIHelper.IsCtrlDown && !UIHelper.IsShiftDown) {
					if (e.Key.CeqAny(Key.OemPlus, Key.Add)) {
						_ScopeBox.Filter = ScopeType.ActiveProject;
						e.Handled = true;
					}
					else if (e.Key.CeqAny(Key.OemMinus, Key.Subtract)) {
						_ScopeBox.Filter = ScopeType.ActiveDocument;
						e.Handled = true;
					}
				}
			}
			void FindInDocument(string text, CancellationToken token) {
				var filter = CodeAnalysisHelper.CreateNameFilter(text, false, Char.IsUpper(text[0]));
				foreach (var item in Bar._SemanticContext.Compilation.GetDescendantDeclarations(token)) {
					if (filter(item.GetDeclarationSignature())) {
						var i = _Menu.Add(item);
						i.Content = SetHeader(item, true, false, true);
					}
				}
			}
			async Task FindInProjectAsync(string text, CancellationToken token) {
				await FindDeclarationsAsync(text, token);
			}

			async Task FindDeclarationsAsync(string symbolName, CancellationToken token) {
				const int MaxResultLimit = 500;
				IReadOnlyCollection<ISymbol> result;
				if (_PreviousSearchKeywords != null
					&& symbolName.StartsWith(_PreviousSearchKeywords)
					&& _IncrementalSearchContainer?.Count < MaxResultLimit) {
					var filter = CodeAnalysisHelper.CreateNameFilter(symbolName, false, Char.IsUpper(symbolName[0]));
					result = _IncrementalSearchContainer.Where(i => filter(i.Name)).ToList();
				}
				else {
					// todo find async, sort later, incrementally
					_IncrementalSearchContainer = result = await Bar._SemanticContext.Document.Project.FindDeclarationsAsync(symbolName, MaxResultLimit, false, Char.IsUpper(symbolName[0]), token).ConfigureAwait(false);
					_PreviousSearchKeywords = symbolName;
				}
				int c = 0;
				foreach (var item in result) {
					if (token.IsCancellationRequested || ++c > 50) {
						break;
					}
					_Menu.Add(item, true);
				}
			}

			void ScopeBox_FilterChanged(object sender, EventArgs e) {
				_FinderBox.Focus();
			}

			void FinderBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args) {
				if ((bool)args.NewValue == false) {
					_IncrementalSearchContainer = null;
					_PreviousSearchKeywords = null;
				}
				else if (_ScopeBox.Filter != ScopeType.ActiveDocument
					&& _FinderBox.Text != __ProjectWideSearchExpression) {
					SetAndSelectFinderText();
				}
			}

			void IContextMenuHost.ShowContextMenu(RoutedEventArgs args) {
				ShowNamespaceAndTypeMenu((int)ScopeType.Undefined);
			}

			public override void Dispose() {
				if (_FinderBox != null) {
					if (_Menu != null) {
						Bar.DisposeSymbolList(_Menu);
						_Menu = null;
					}
					base.Dispose();
					_IncrementalSearchContainer = null;
					_FinderBox.PreviewKeyDown -= ChangeSearchScope;
					_FinderBox.TextChanged -= SearchCriteriaChanged;
					_FinderBox.IsVisibleChanged -= FinderBox_IsVisibleChanged;
					_FinderBox = null;
					_ScopeBox.FilterChanged -= SearchCriteriaChanged;
					_ScopeBox.FilterChanged -= ScopeBox_FilterChanged;
					_ScopeBox = null;
					_Note = null;
					DataContext = null;
				}
			}

			sealed class MemberFinderBox : ThemedTextBox
			{
				public MemberFinderBox() {
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
	}
}
