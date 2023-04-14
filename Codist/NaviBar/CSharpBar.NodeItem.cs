using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AppHelpers;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;
using Task = System.Threading.Tasks.Task;
using TH = Microsoft.VisualStudio.Shell.ThreadHelper;

namespace Codist.NaviBar
{
	public sealed partial class CSharpBar
	{
		sealed class NodeItem : BarItem, ISymbolFilter, IContextMenuHost, ISymbolContainer
		{
			readonly int _ImageId;
			SymbolList _Menu;
			SymbolFilterBox _FilterBox;
			int _PartialCount;
			ISymbol _Symbol;
			List<ISymbol> _ReferencedSymbols;

			public NodeItem(CSharpBar bar, SyntaxNode node)
				: base (bar, node.GetImageId(), new ThemedMenuText(node.GetDeclarationSignature() ?? String.Empty)) {
				_ImageId = node.GetImageId();
				Node = node;
				Click += HandleClick;
				this.UseDummyToolTip();
			}

			public override BarItemType ItemType => BarItemType.Node;
			public SyntaxNode Node { get; private set; }
			public bool IsSymbolNode => false;
			public ISymbol Symbol => _Symbol ?? (_Symbol = SyncHelper.RunSync(() => Bar._SemanticContext.GetSymbolAsync(Node, Bar._CancellationSource.GetToken())));
			public bool HasReferencedSymbols => _ReferencedSymbols != null && _ReferencedSymbols.Count > 0;
			public List<ISymbol> ReferencedSymbols => _ReferencedSymbols ?? (_ReferencedSymbols = new List<ISymbol>());

			public void ShowContextMenu(RoutedEventArgs args) {
				if (ContextMenu == null) {
					var s = Symbol;
					var m = new CSharpSymbolContextMenu(s, Node, Bar._SemanticContext);
					m.AddNodeCommands();
					if (s != null) {
						m.AddUnitTestCommands();
						m.Items.Add(new Separator());
						m.AddAnalysisCommands();
						m.AddCopyAndSearchSymbolCommands();
						m.AddTitleItem(Node.GetDeclarationSignature());
					}
					ContextMenu = m;
				}
				ContextMenu.IsOpen = true;
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			[SuppressMessage("Performance", "U2U1009:Async or iterator methods should avoid state machine generation for early exits (throws or synchronous returns)", Justification = Suppression.EventHandler)]
			async void HandleClick(object sender, RoutedEventArgs e) {
				SyncHelper.CancelAndDispose(ref Bar._CancellationSource, true);
				if (_Menu != null && Bar._SymbolList == _Menu && _Menu.IsVisible) {
					Bar.HideMenu();
					return;
				}
				if (Node.IsKind(SyntaxKind.RegionDirectiveTrivia)
						&& (Node.FirstAncestorOrSelf<MemberDeclarationSyntax>()?.Span.Contains(Node.Span)) != true
					|| Node.Kind().IsNonDelegateTypeDeclaration()) {
					if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.CtrlGoToSource)
						&& Keyboard.Modifiers.MatchFlags(ModifierKeys.Control)) {
						Node.GetReference().GoToSource();
						return;
					}
					// displays member list for type declarations or regions outside of member declaration
					var ct = Bar._CancellationSource.GetToken();
					try {
						await CreateMenuForTypeSymbolNodeAsync(ct);
						await TH.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

						_FilterBox.UpdateNumbers((Symbol as ITypeSymbol)?.GetMembers().Select(s => new SymbolItem(s, null, false)) ?? Enumerable.Empty<SymbolItem>());
						var footer = (TextBlock)_Menu.Footer;
						if (_PartialCount > 1) {
							footer.AddImage(IconIds.PartialDocumentCount)
								.Append(_PartialCount);
						}
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.LineOfCode)) {
							footer.AddImage(IconIds.LineOfCode)
								.Append(Node.GetLineSpan().Length + 1);
						}
						Bar.ShowMenu(this, _Menu);
						_FilterBox?.FocusFilterBox();
					}
					catch (OperationCanceledException) {
						// ignore
					}
					return;
				}

				var span = Node.FullSpan;
				if (span.Contains(Bar._SemanticContext.Position) && Node.SyntaxTree.FilePath == Bar._SemanticContext.Document.FilePath
					|| Node.IsKind(SyntaxKind.RegionDirectiveTrivia)) {
					// Hack: since SelectNode will move the cursor to the end of the span--the beginning of next node,
					//    it will make next node selected, which is undesired in most cases
					Bar.View.Selection.SelectionChanged -= Bar.Update;
					Bar.View.SelectNode(Node, Keyboard.Modifiers != ModifierKeys.Control);
					Bar.View.Selection.SelectionChanged += Bar.Update;
				}
				else {
					Node.GetIdentifierToken().GetLocation().GoToSource();
				}
			}

			async Task CreateMenuForTypeSymbolNodeAsync(CancellationToken cancellationToken) {
				if (_Menu != null) {
					((TextBlock)_Menu.Footer).Clear();
					Controls.DragDropHelper.SetScrollOnDragDrop(_Menu, false);
					await RefreshItemsAsync(Node, cancellationToken);
					return;
				}
				_Menu = new SymbolList(Bar._SemanticContext) {
					Container = Bar.ListContainer,
					ContainerType = SymbolListType.NodeList,
					ExtIconProvider = s => GetExtIcons(s.SyntaxNode),
					Owner = this
				};
				Controls.DragDropHelper.SetScrollOnDragDrop(_Menu, true);
				_Menu.Header = _FilterBox = new SymbolFilterBox(_Menu) { HorizontalAlignment = HorizontalAlignment.Right };
				_Menu.Footer = new TextBlock { Margin = WpfHelper.MenuItemMargin }
					.ReferenceProperty(TextBlock.ForegroundProperty, EnvironmentColors.SystemGrayTextBrushKey);
				Bar.SetupSymbolListMenu(_Menu);
				await AddItemsAsync(Node, cancellationToken);
				if (_Menu.Symbols.Count > 100) {
					_Menu.EnableVirtualMode = true;
				}
			}

			async Task RefreshItemsAsync(SyntaxNode node, CancellationToken cancellationToken) {
				var sm = Bar._SemanticContext.SemanticModel;
				await Bar._SemanticContext.UpdateAsync(cancellationToken).ConfigureAwait(true);
				if (sm != Bar._SemanticContext.SemanticModel) {
					_Menu.ClearSymbols();
					_Symbol = null;
					Node = Bar._SemanticContext.RelocateDeclarationNode(Node);
					await AddItemsAsync(Node, cancellationToken);
					_Menu.RefreshItemsSource(true);
					return;
				}
				// select node item which contains caret
				var pos = Bar.View.GetCaretPosition();
				foreach (var item in _Menu.Symbols) {
					if (item.Usage != SymbolUsageKind.Container) {
						if (item.IsExternal || cancellationToken.IsCancellationRequested
							|| item.SelectIfContainsPosition(pos)) {
							break;
						}
					}
				}
			}

			Task AddItemsAsync(SyntaxNode node, CancellationToken cancellationToken) {
				AddMemberDeclarations(node, false, true);
				if (node.IsKind(SyntaxKind.RegionDirectiveTrivia)) {
					return Task.CompletedTask;
				}
				var externals = (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.PartialClassMember)
					&& (node as BaseTypeDeclarationSyntax).Modifiers.Any(SyntaxKind.PartialKeyword) ? MemberListOptions.ShowPartial : 0)
					| (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.BaseClassMember) && (node.IsKind(SyntaxKind.ClassDeclaration) || node.IsKind(CodeAnalysisHelper.RecordDeclaration)) ? MemberListOptions.ShowBase : 0);
				return externals == 0 ? Task.CompletedTask : AddExternalItemsAsync(node, externals, cancellationToken);
			}

			async Task AddExternalItemsAsync(SyntaxNode node, MemberListOptions externals, CancellationToken cancellationToken) {
				await Bar._SemanticContext.UpdateAsync(cancellationToken).ConfigureAwait(true);
				var symbol = await Bar._SemanticContext.GetSymbolAsync(node, cancellationToken).ConfigureAwait(true);
				if (symbol == null) {
					return;
				}
				if (externals.MatchFlags(MemberListOptions.ShowPartial)) {
					await AddPartialTypeDeclarationsAsync(node as BaseTypeDeclarationSyntax, symbol, cancellationToken);
				}
				if (externals.MatchFlags(MemberListOptions.ShowBase) && symbol.Kind == SymbolKind.NamedType) {
					await AddBaseTypeDeclarationsAsync(symbol as INamedTypeSymbol, cancellationToken);
				}
			}

			async Task AddPartialTypeDeclarationsAsync(BaseTypeDeclarationSyntax node, ISymbol symbol, CancellationToken cancellationToken) {
				var current = node.SyntaxTree;
				int c = 1;
				foreach (var item in symbol.DeclaringSyntaxReferences) {
					if (item.SyntaxTree == current || String.Equals(item.SyntaxTree.FilePath, current.FilePath, StringComparison.OrdinalIgnoreCase)) {
						continue;
					}
					await AddExternalNodesAsync(item, null, true, cancellationToken);
					++c;
				}
				_PartialCount = c;
			}

			async Task AddBaseTypeDeclarationsAsync(INamedTypeSymbol symbol, CancellationToken cancellationToken) {
				while ((symbol = symbol.BaseType) != null && symbol.HasSource()) {
					foreach (var item in symbol.DeclaringSyntaxReferences) {
						await AddExternalNodesAsync(item, symbol.GetTypeName(), false, cancellationToken);
					}
				}
			}

			async Task AddExternalNodesAsync(SyntaxReference item, string textOverride, bool includeDirectives, CancellationToken cancellationToken) {
				var externalNode = await item.GetSyntaxAsync(cancellationToken);
				var i = _Menu.Add(externalNode);
				i.Location = item.SyntaxTree.GetLocation(item.Span);
				i.Content.Text = textOverride ?? System.IO.Path.GetFileName(item.SyntaxTree.FilePath);
				i.Usage = SymbolUsageKind.Container;
				AddMemberDeclarations(externalNode, true, includeDirectives);
			}

			void AddMemberDeclarations(SyntaxNode node, bool isExternal, bool includeDirectives) {
				const byte UNDEFINED = 0xFF, TRUE = 1, FALSE = 0;
				IEnumerable<SyntaxNode> scope;
				if (node.IsKind(SyntaxKind.RegionDirectiveTrivia)) {
					var span = node.GetSematicSpan(true).ToTextSpan();
					scope = node.FirstAncestorOrSelf<SyntaxNode>(n => n.Span.Contains(span), true).ChildNodes().Where(n => span.Contains(n.SpanStart));
					includeDirectives = false;
				}
				else {
					scope = node.ChildNodes();
				}
				var directives = includeDirectives && Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.Region)
					? node.GetDirectives(d => d.IsKind(SyntaxKind.RegionDirectiveTrivia) || d.IsKind(SyntaxKind.EndRegionDirectiveTrivia))
					: null;
				byte regionJustStart = UNDEFINED; // undefined, prevent #endregion show up on top of menu items
				bool selected = false;
				int pos = Bar.View.GetCaretPosition();
				SyntaxNode lastNode = null;
				foreach (var child in scope) {
					var childKind = child.Kind();
					if (childKind.IsMemberDeclaration() == false && childKind.IsTypeDeclaration() == false) {
						continue;
					}
					if (directives != null) {
						for (var i = 0; i < directives.Count; i++) {
							var d = directives[i];
							if (d.SpanStart < child.SpanStart) {
								if (d.IsKind(SyntaxKind.RegionDirectiveTrivia)) {
									if (lastNode == null || lastNode.Span.Contains(d.SpanStart) == false) {
										AddStartRegion(d, isExternal);
									}
									regionJustStart = TRUE;
								}
								else if (d.IsKind(SyntaxKind.EndRegionDirectiveTrivia)) {
									// don't show #endregion if preceeding item is #region
									if (regionJustStart == FALSE) {
										if (lastNode == null || lastNode.Span.Contains(d.SpanStart) == false) {
											var item = new SymbolItem(_Menu);
											_Menu.Add(item);
											item.Content
												.Append("#endregion ").Append(d.GetDeclarationSignature())
												.Foreground = ThemeHelper.SystemGrayTextBrush;
										}
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
					if (childKind == SyntaxKind.FieldDeclaration || childKind == SyntaxKind.EventFieldDeclaration) {
						AddVariables(((BaseFieldDeclarationSyntax)child).Declaration.Variables, isExternal, pos);
					}
					else {
						var i = _Menu.Add(child);
						if (isExternal) {
							i.Usage = SymbolUsageKind.External;
						}
						else if (selected == false && i.SelectIfContainsPosition(pos)) {
							selected = true;
						}
						ShowNodeValue(i);
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.ParameterList)) {
							AddParameterList(i.Content, child);
						}
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.MemberType)) {
							AddReturnType(i, child);
						}
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.RegionInMember) == false) {
							lastNode = child;
						}
					}
					// a member is added between #region and #endregion
					regionJustStart = FALSE;
				}
				if (directives != null) {
					foreach (var item in directives) {
						if (item.IsKind(SyntaxKind.RegionDirectiveTrivia)
							&& (lastNode == null || lastNode.Span.Contains(item.SpanStart) == false)) {
							AddStartRegion(item, isExternal);
						}
					}
				}
			}

			void AddStartRegion(DirectiveTriviaSyntax d, bool isExternal) {
				var item = _Menu.Add(d);
				item.Hint = "#region";
				item.Content = SetHeader(d, false, false, false);
				if (isExternal) {
					item.Usage = SymbolUsageKind.External;
				}
			}

			void AddVariables(SeparatedSyntaxList<VariableDeclaratorSyntax> fields, bool isExternal, int pos) {
				foreach (var item in fields) {
					var i = _Menu.Add(item);
					if (isExternal) {
						i.Usage = SymbolUsageKind.External;
					}
					i.SelectIfContainsPosition(pos);
					ShowNodeValue(i);
					if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.MemberType)) {
						AddReturnType(i, item);
					}
				}
			}

			static StackPanel GetExtIcons(SyntaxNode node) {
				switch (node) {
					case BaseMethodDeclarationSyntax m:
						return GetMethodExtIcons(m);
					case BasePropertyDeclarationSyntax p:
						return GetPropertyExtIcons(p);
					case BaseFieldDeclarationSyntax f:
						return GetFieldExtIcons(f);
					case VariableDeclaratorSyntax v:
						return GetExtIcons(node.Parent.Parent);
					case BaseTypeDeclarationSyntax c:
						return GetTypeExtIcons(c);
				}
				return null;

				StackPanel GetMethodExtIcons(BaseMethodDeclarationSyntax m) {
					StackPanel icons = null;
					bool isExt = false, isStatic = false;
					if (m.ParameterList.Parameters.FirstOrDefault()?.Modifiers.Any(SyntaxKind.ThisKeyword) == true) {
						AddIcon(ref icons, IconIds.ExtensionMethod);
						isExt = true;
					}
					foreach (var modifier in m.Modifiers) {
						switch (modifier.Kind()) {
							case SyntaxKind.AsyncKeyword: AddIcon(ref icons, IconIds.AsyncMember); break;
							case SyntaxKind.AbstractKeyword: AddIcon(ref icons, IconIds.AbstractMember); break;
							case SyntaxKind.StaticKeyword:
								if (isExt == false) {
									AddIcon(ref icons, IconIds.StaticMember);
									isStatic = true;
								}
								break;
							case SyntaxKind.UnsafeKeyword: AddIcon(ref icons, IconIds.Unsafe); break;
							case SyntaxKind.SealedKeyword: AddIcon(ref icons, IconIds.SealedMethod); break;
							case SyntaxKind.OverrideKeyword: AddIcon(ref icons, IconIds.OverrideMethod); break;
							case SyntaxKind.ReadOnlyKeyword: AddIcon(ref icons, IconIds.ReadonlyMethod); break;
						}
					}
					if (isStatic == false && isExt == false
						&& m.Parent.IsKind(SyntaxKind.InterfaceDeclaration)
						&& (m.Body != null || m.ExpressionBody != null)) {
						AddIcon(ref icons, IconIds.DefaultInterfaceImplementation);
					}
					return icons;
				}

				StackPanel GetPropertyExtIcons(BasePropertyDeclarationSyntax p) {
					StackPanel icons = null;
					foreach (var modifier in p.Modifiers) {
						switch (modifier.Kind()) {
							case SyntaxKind.StaticKeyword: AddIcon(ref icons, IconIds.StaticMember); break;
							case SyntaxKind.AbstractKeyword: AddIcon(ref icons, IconIds.AbstractMember); break;
							case SyntaxKind.SealedKeyword:
								AddIcon(ref icons, p.IsKind(SyntaxKind.EventDeclaration) ? IconIds.SealedEvent : IconIds.SealedProperty);
								break;
							case SyntaxKind.OverrideKeyword:
								AddIcon(ref icons, p.IsKind(SyntaxKind.EventDeclaration) ? IconIds.OverrideEvent : IconIds.OverrideProperty);
								break;
							case SyntaxKind.ReadOnlyKeyword: AddIcon(ref icons, IconIds.ReadonlyMethod); break;
							case SyntaxKind.RefKeyword: AddIcon(ref icons, IconIds.RefMember); break;
							case CodeAnalysisHelper.RequiredKeyword: AddIcon(ref icons, IconIds.RequiredMember); break;
						}
					}
					if (p.Type is RefTypeSyntax r) {
						AddIcon(ref icons, IconIds.RefMember);
						if (r.ReadOnlyKeyword.Parent != null) {
							AddIcon(ref icons, IconIds.ReadonlyProperty);
						}
					}
					if (p.AccessorList != null) {
						var a = p.AccessorList.Accessors;
						if (a.Count == 2) {
							if (a.Any(i => i.RawKind == (int)CodeAnalysisHelper.InitAccessorDeclaration)) {
								AddIcon(ref icons, IconIds.InitonlyProperty);
							}
							else if (a[0].Body == null && a[0].ExpressionBody == null && a[1].Body == null && a[1].ExpressionBody == null) {
								AddIcon(ref icons, IconIds.AutoProperty);
								return icons;
							}
						}
						else if (a.Count == 1) {
							if (a[0].Body == null && a[0].ExpressionBody == null) {
								AddIcon(ref icons, IconIds.ReadonlyProperty);
								return icons;
							}
						}
						if (a.Any(i => i.Keyword.IsKind(SyntaxKind.GetKeyword) && i.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))) {
							AddIcon(ref icons, IconIds.ReadonlyMethod);
						}
					}
					return icons;
				}

				StackPanel GetFieldExtIcons(BaseFieldDeclarationSyntax f) {
					StackPanel icons = null;
					foreach (var modifier in f.Modifiers) {
						switch (modifier.Kind()) {
							case SyntaxKind.ReadOnlyKeyword: AddIcon(ref icons, IconIds.ReadonlyField); break;
							case SyntaxKind.VolatileKeyword: AddIcon(ref icons, IconIds.VolatileField); break;
							case SyntaxKind.StaticKeyword: AddIcon(ref icons, IconIds.StaticMember); break;
						}
					}
					return icons;
				}

				StackPanel GetTypeExtIcons(BaseTypeDeclarationSyntax c) {
					StackPanel icons = null;
					foreach (var modifier in c.Modifiers) {
						switch (modifier.Kind()) {
							case SyntaxKind.StaticKeyword: AddIcon(ref icons, IconIds.StaticMember); break;
							case SyntaxKind.AbstractKeyword: AddIcon(ref icons, IconIds.AbstractClass); break;
							case SyntaxKind.SealedKeyword: AddIcon(ref icons, IconIds.SealedClass); break;
							case SyntaxKind.ReadOnlyKeyword: AddIcon(ref icons, IconIds.ReadonlyType); break;
						}
					}
					return icons;
				}

				void AddIcon(ref StackPanel container, int imageId) {
					if (container == null) {
						container = new StackPanel { Orientation = Orientation.Horizontal };
					}
					container.Children.Add(ThemeHelper.GetImage(imageId));
				}
			}

			static void ShowNodeValue(SymbolItem item) {
				if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.FieldValue) == false) {
					return;
				}
				switch (item.SyntaxNode.Kind()) {
					case SyntaxKind.VariableDeclarator:
						ShowVariableValue(item);
						break;
					case SyntaxKind.EnumMemberDeclaration:
						ShowEnumMemberValue(item);
						break;
					case SyntaxKind.PropertyDeclaration:
						ShowPropertyValue(item);
						break;
				}

				void ShowVariableValue(SymbolItem fieldItem) {
					var vi = ((VariableDeclaratorSyntax)fieldItem.SyntaxNode).Initializer;
					if (vi != null) {
						var v = vi.Value?.ToString();
						if (v != null) {
							if (vi.Value.IsKind(CodeAnalysisHelper.ImplicitObjectCreationExpression)) {
								v = "new " + (fieldItem.SyntaxNode.Parent as VariableDeclarationSyntax).Type.ToString() + vi.Value.GetImplicitObjectCreationArgumentList().ToString();
							}
							fieldItem.Hint = ShowInitializerIndicator() + (v.Length > 200 ? v.Substring(0, 200) : v);
						}
					}
				}

				void ShowEnumMemberValue(SymbolItem enumItem) {
					var v = ((EnumMemberDeclarationSyntax)enumItem.SyntaxNode).EqualsValue;
					if (v != null) {
						enumItem.Hint = v.Value?.ToString();
					}
					else {
						enumItem.SetSymbolToSyntaxNode();
						enumItem.Hint = ((IFieldSymbol)enumItem.Symbol).ConstantValue?.ToString();
					}
				}

				void ShowPropertyValue(SymbolItem propertyItem) {
					var p = (PropertyDeclarationSyntax)propertyItem.SyntaxNode;
					if (p.Initializer != null) {
						propertyItem.Hint = ShowInitializerIndicator() + (p.Initializer.Value.IsKind(CodeAnalysisHelper.ImplicitObjectCreationExpression) ? "new " + p.Type.ToString() + p.Initializer.Value.GetImplicitObjectCreationArgumentList().ToString() : p.Initializer.Value.ToString());
					}
					else if (p.ExpressionBody != null) {
						propertyItem.Hint = p.ExpressionBody.ToString();
					}
					else //if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.AutoPropertyAnnotation)) {
					//	var a = p.AccessorList.Accessors;
					//	if (a.Count == 2) {
					//		if (a.Any(i => i.RawKind == (int)CodeAnalysisHelper.InitAccessorDeclaration)) {
					//			propertyItem.Hint = "{init}";
					//		}
					//		else if (a[0].Body == null && a[0].ExpressionBody == null && a[1].Body == null && a[1].ExpressionBody == null) {
					//			propertyItem.Hint = "{;;}";
					//		}
					//	}
					//	else if (a.Count == 1) {
					//		if (a[0].Body == null && a[0].ExpressionBody == null) {
					//			propertyItem.Hint = "{;}";
					//		}
					//	}
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.MemberType)) {
							propertyItem.Hint += p.Type.ToString();
						}
					// }
				}

				string ShowInitializerIndicator() {
					return Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.MemberType) ? "= " : null;
				}
			}

			protected override void OnToolTipOpening(ToolTipEventArgs e) {
				base.OnToolTipOpening(e);
				if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.SymbolToolTip) == false) {
					ToolTip = null;
					return;
				}

				if (this.HasDummyToolTip()) {
					this.SetTipPlacementBottom();
					// todo: handle updated syntax node for RootItem
					var s = Symbol;
					if (s != null) {
						var tip = ToolTipHelper.CreateToolTip(s, true, Bar._SemanticContext);
						if (Config.Instance.NaviBarOptions.MatchFlags(NaviBarOptions.LineOfCode)) {
							tip.AddTextBlock().Append(R.T_LineOfCode + (Node.GetLineSpan().Length + 1));
						}
						ToolTip = tip;
					}
					else {
						ToolTip = Node.Kind().GetSyntaxBrief();
					}
					this.SetTipOptions();
				}
			}

			bool ISymbolFilter.Filter(int filterTypes) {
				return SymbolFilterBox.FilterByImageId((MemberFilterTypes)filterTypes, _ImageId);
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
					_ReferencedSymbols = null;
					_FilterBox = null;
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
