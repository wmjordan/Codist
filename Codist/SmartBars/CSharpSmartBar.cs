﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using CLR;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using TH = Microsoft.VisualStudio.Shell.ThreadHelper;
using R = Codist.Properties.Resources;

namespace Codist.SmartBars
{
	//todo Make commands async and cancellable
	/// <summary>
	/// An extended <see cref="SmartBar"/> for C# content type.
	/// </summary>
	sealed class CSharpSmartBar : SmartBar
	{
		static readonly Taggers.HighlightClassifications __HighlightClassifications = Taggers.HighlightClassifications.Instance;
		static readonly string[] __UnitTestingNamespace = new[] { "UnitTesting", "TestTools", "VisualStudio", "Microsoft" };
		SemanticContext _Context;
		TextViewOverlay _SymbolListContainer;
		ISymbol _Symbol;

		public CSharpSmartBar(IWpfTextView view, Microsoft.VisualStudio.Text.Operations.ITextSearchService2 textSearchService) : base(view, textSearchService) {
			TH.ThrowIfNotOnUIThread();
			_Context = SemanticContext.GetOrCreateSingletonInstance(view);
			_SymbolListContainer = TextViewOverlay.GetOrCreate(view);
			view.Closed += View_Closed;
		}

		protected override BarType Type => BarType.CSharp;

		ToolBar MyToolBar => ToolBar2;

		protected override async Task AddCommandsAsync(CancellationToken cancellationToken) {
			SemanticContext ctx;
			if (await UpdateAsync()
				&& cancellationToken.IsCancellationRequested == false
				&& (ctx = _Context)?.NodeIncludeTrivia != null) {
				await AddContextualCommandsAsync(ctx, cancellationToken);
			}
		}

		static CommandItem CreateCommandMenu(int imageId, string title, string emptyMenuTitle, Action<CommandContext, MenuItem> itemPopulator) {
			return new CommandItem(imageId, title, ctrl => ctrl.StaysOpenOnClick = true, ctx => {
				var menuItem = ctx.Sender as ThemedMenuItem;
				if (menuItem.Items.Count > 0 || menuItem.SubMenuHeader != null) {
					return;
				}
				ctx.KeepToolBarOnClick = true;
				itemPopulator(ctx, menuItem);
				if (menuItem.Items.Count == 0) {
					menuItem.Items.Add(new ThemedMenuItem { Header = emptyMenuTitle, IsEnabled = false });
				}
				else {
					CreateItemsFilter(menuItem);
				}
				menuItem.IsSubmenuOpen = true;
			});
		}

		static void CreateItemsFilter(ThemedMenuItem menuItem) {
			menuItem.SubMenuHeader = new StackPanel {
				Margin = WpfHelper.TopItemMargin,
				Children = {
					new SymbolFilterBox(new MenuItemFilter(menuItem.Items)),
					new Separator()
				}
			};
		}

		async Task AddContextualCommandsAsync(SemanticContext ctx, CancellationToken cancellationToken) {
			var node = ctx.NodeIncludeTrivia;
			var nodeKind = node.Kind();
			if (IsReadOnly == false && nodeKind == SyntaxKind.XmlText) {
				AddXmlDocCommands();
				return;
			}
			var trivia = ctx.NodeTrivia;
			if (trivia.RawKind == 0) {
				var token = ctx.Token;
				var tokenKind = token.Kind();
				if (token.Span.Contains(View.Selection, true)) {
					switch (tokenKind) {
						case SyntaxKind.IdentifierToken:
						case SyntaxKind.SetKeyword:
						case SyntaxKind.GetKeyword:
						case CodeAnalysisHelper.InitKeyword:
							if (nodeKind.IsDeclaration()
								|| nodeKind.CeqAny(SyntaxKind.PredefinedType, SyntaxKind.Parameter, SyntaxKind.VariableDeclarator, SyntaxKind.ForEachStatement, SyntaxKind.SingleVariableDesignation)
								|| node is TypeSyntax
								|| node is AccessorDeclarationSyntax) {
								// selection is within a symbol
								_Symbol = await ctx.GetSymbolAsync(cancellationToken);
								if (_Symbol != null) {
									AddSymbolCommands(nodeKind);
								}

								if (IsReadOnly == false) {
									if (_Symbol?.IsPublicConcreteInstance() == true) {
										if (_Symbol.Kind == SymbolKind.Method) {
											if (_Symbol.GetContainingTypes().All(t => t.IsPublicConcreteInstance())
												&& ((IMethodSymbol)_Symbol).ReturnsVoid
												&& _Symbol.GetAttributes().Any(a => a.AttributeClass.MatchTypeName("TestMethodAttribute", __UnitTestingNamespace))) {
												AddEditorCommand(MyToolBar, IconIds.DebugTest, "TestExplorer.DebugAllTestsInContext", R.CMD_DebugTestMethod, "TestExplorer.RunAllTestsInContext");
											}
										}
										else if (nodeKind == SyntaxKind.ClassDeclaration) {
											if (_Symbol.GetAttributes().Any(a => a.AttributeClass.MatchTypeName("TestClassAttribute", __UnitTestingNamespace))) {
												AddEditorCommand(MyToolBar, IconIds.DebugTest, "TestExplorer.DebugAllTestsInContext", R.CMD_DebugTestClass, "TestExplorer.RunAllTestsInContext");
											}
										}
									}
									AddRefactorCommands(node);
								}
							}
							else if (nodeKind == SyntaxKind.TypeParameter) {
								_Symbol = SyncHelper.RunSync(() => ctx.GetSymbolAsync(cancellationToken));
								if (_Symbol != null && IsReadOnly == false) {
									AddRenameCommand();
								}
							}
							if (_Symbol != null) {
								if (IsReadOnly == false) {
									AddCommand(MyToolBar, IconIds.SelectAll, R.CMD_SelectSymbolInDocument, SelectSymbolOccurrencesAsync);
								}
								if (Config.Instance.Features.MatchFlags(Features.SyntaxHighlight)
									&& Taggers.SymbolMarkManager.CanBookmark(_Symbol)) {
									AddCommands(MyToolBar, IconIds.Marks, R.CMD_MarkSymbol, null, GetMarkerCommands);
								}
							}
							break;
						default:
							if (tokenKind.IsPredefinedSystemType()) {
								goto case SyntaxKind.IdentifierToken;
							}
							if (View.Selection.StreamSelectionSpan.Length < 4
								&& (tokenKind.IsBetween(SyntaxKind.TildeToken, SyntaxKind.PercentEqualsToken)
									|| tokenKind.CeqAny(SyntaxKind.IsKeyword, SyntaxKind.AsKeyword))
								&& SelectionIs<SyntaxNode>()) {
								AddCommand(MyToolBar, IconIds.SelectBlock, R.CMD_SelectBlock, SelectNodeAsKind<SyntaxNode>);
							}
							else if (tokenKind.IsBetween(SyntaxKind.NumericLiteralToken, SyntaxKind.StringLiteralToken)
								|| tokenKind.CeqAny(CodeAnalysisHelper.SingleLineRawStringLiteralToken, CodeAnalysisHelper.MultiLineRawStringLiteralToken)) {
								if (token.Span.Length > View.Selection.StreamSelectionSpan.Length) {
									AddCommand(MyToolBar, IconIds.SelectText, R.CMD_SelectBlock, SelectNodeAsKind<SyntaxNode>);
								}
								AddEditorCommand(MyToolBar, IconIds.FindReference, "Edit.FindAllReferences", R.CMD_FindAllReferences);
							}
							else if (tokenKind.CeqAny(SyntaxKind.InterpolatedStringStartToken, SyntaxKind.InterpolatedStringEndToken, SyntaxKind.InterpolatedStringTextToken, SyntaxKind.InterpolatedStringToken)) {
								AddCommand(MyToolBar, IconIds.SelectText, R.CMD_SelectBlock, SelectNodeAsKind<InterpolatedStringExpressionSyntax>);
							}
							break;
					}
				}
				else if (nodeKind.IsRegionalDirective()) {
					AddDirectiveCommands();
				}
				if (IsReadOnly == false) {
					if (tokenKind.CeqAny(SyntaxKind.TrueKeyword, SyntaxKind.FalseKeyword)) {
						AddCommand(MyToolBar, IconIds.ToggleValue, R.CMD_ToggleValue, ctx => Replace(ctx, v => v == "true" ? "false" : "true", true));
					}
					else if (tokenKind.CeqAny(SyntaxKind.ExplicitKeyword, SyntaxKind.ImplicitKeyword)) {
						AddCommand(MyToolBar, IconIds.ToggleValue, R.CMD_ToggleOperator, ctx => Replace(ctx, v => v == "implicit" ? "explicit" : "implicit", true));
					}
					if (nodeKind == SyntaxKind.VariableDeclarator) {
						if (node?.Parent?.Parent is MemberDeclarationSyntax) {
							AddCommand(MyToolBar, IconIds.AddXmlDoc, R.CMD_InsertComment, ctx => {
								TextEditorHelper.ExecuteEditorCommand("Edit.InsertComment");
								ctx.View.Selection.Clear();
							});
						}
					}
					else if (nodeKind.IsDeclaration()) {
						if (node is TypeDeclarationSyntax || node is MemberDeclarationSyntax || node is ParameterListSyntax) {
							AddCommand(MyToolBar, IconIds.AddXmlDoc, R.CMD_InsertComment, ctx => {
								TextEditorHelper.ExecuteEditorCommand("Edit.InsertComment");
								ctx.View.Selection.Clear();
							});
						}
					}
				}
			}
			if (IsReadOnly == false) {
				var refactoringContext = new Refactorings.RefactoringContext(ctx);
				if (refactoringContext.SelectedStatementInfo.Items != null) {
					AddEditorCommand(MyToolBar, IconIds.ExtractMethod, "Refactor.ExtractMethod", R.CMD_ExtractMethod);
				}
				if (refactoringContext.AcceptAny(Refactorings.All.Refactorings) && View.TextBuffer.ContentType.IsOfType(Constants.CodeTypes.Projection) == false) {
					AddCommand(MyToolBar, IconIds.Refactoring, R.CMD_RefactorSelection, ShowRefactorMenu);
				}
				AddCommentCommands();
				AddEditorCommand(MyToolBar, IconIds.QuickAction, "View.QuickActionsForPosition", R.CMD_QuickAction);
			}
		}

		void ShowRefactorMenu(CommandContext ctx) {
			SemanticContext c;
			ctx.KeepToolBar(false);
			if (UpdateSemanticModel() == false || (c = _Context) is null) {
				return;
			}
			var m = new CSharpSymbolContextMenu(null, null, c) {
				Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
				PlacementTarget = ctx.Sender,
			};
			m.SetValue(TextBlock.ForegroundProperty, ThemeCache.MenuTextBrush);
			var rc = new Refactorings.RefactoringContext(c);
			AddRefactoringCommands(m, Refactorings.All.Refactorings, rc);
			ctx.Sender.ContextMenu = m;
			m.CommandExecuted += HideSmartBar;
			m.IsOpen = true;
		}

		void AddRefactoringCommands(ContextMenu menu, Refactorings.IRefactoring[] refactorings, Refactorings.RefactoringContext ctx) {
			foreach (var item in refactorings) {
				if (item.Accept(ctx)) {
					menu.Items.Add(new CommandMenuItem(this, new CommandItem(item.IconId, item.Title, c => item.Refactor(((CSharpSmartBar)c.Bar)._Context))));
				}
			}
		}

		void AddSymbolCommands(SyntaxKind nodeKind) {
			if (nodeKind.CeqAny(SyntaxKind.IdentifierName, SyntaxKind.GenericName)) {
				AddEditorCommand(MyToolBar, IconIds.GoToDefinition, "Edit.GoToDefinition", R.CMD_GoToDefinitionPeek, "Edit.PeekDefinition");
			}
			AddCommand(MyToolBar, IconIds.SymbolAnalysis, R.CMD_AnalyzeSymbol, ShowSymbolContextMenu);
		}

		async Task SelectSymbolOccurrencesAsync(CommandContext ctx) {
			SemanticContext c;
			ctx.KeepToolBar(false);
			if (await UpdateAsync() == false || (c = _Context) is null) {
				return;
			}
			var selections = ctx.View.GetMultiSelectionBroker();
			var symbol = _Symbol;
			Span selectionOffsetSpan = default;
			var tokenSpan = _Context.Token.Span.ToSpan();
			var tokenLength = tokenSpan.Length;
			if (selections.HasMultipleSelections == false) {
				// if only part of the symbol token is selected,
				// select that part in other occurrences too
				selectionOffsetSpan = selections.PrimarySelection.Extent.SnapshotSpan.Span;
				if (tokenSpan.Contains(selectionOffsetSpan)) {
					selectionOffsetSpan = new Span(selectionOffsetSpan.Start - tokenSpan.Start, selectionOffsetSpan.Length);
				}
			}
			await SelectSymbolDefinitionAndReferencesAsync(c, selections, symbol, tokenLength, selectionOffsetSpan);
			#region Select others with the same name
			switch (symbol.Kind) {
				case SymbolKind.NamedType:
					if (symbol is INamedTypeSymbol t && t.TypeKind == TypeKind.Class) {
						foreach (var tm in t.GetMembers()) {
							if (tm.Kind == SymbolKind.Method
								&& tm.IsImplicitlyDeclared == false
								&& IsTypeNamedMethod((IMethodSymbol)tm)) {
								await SelectSymbolDefinitionAndReferencesAsync(c, selections, tm, tokenLength, selectionOffsetSpan);
							}
						}
					}
					break;
				case SymbolKind.Method:
					if (symbol is IMethodSymbol m) {
						if (IsTypeNamedMethod(m)) {
							await SelectSymbolDefinitionAndReferencesAsync(c, selections, symbol = symbol.ContainingType, tokenLength, selectionOffsetSpan);
							goto case SymbolKind.NamedType;
						}
					}
					break;
			}
			#endregion

			bool IsTypeNamedMethod(IMethodSymbol m) {
				return m.MethodKind.CeqAny(MethodKind.Constructor, MethodKind.StaticConstructor, MethodKind.Destructor);
			}

			async Task SelectSymbolDefinitionAndReferencesAsync(SemanticContext ctx, IMultiSelectionBroker msb, ISymbol s, int len, Span offsetSpan) {
				msb.AddSelections(OffsetLocations(await s.FindOccurrencesInDocumentAsync(ctx.Document, ctx.Compilation.SyntaxTree, default), len, offsetSpan));
			}

			IEnumerable<Span> OffsetLocations(IEnumerable<Location> locations, int len, Span off) {
				foreach (var location in locations) {
					yield return Offset(location.SourceSpan, len, off);
				}
			}

			Span Offset(TextSpan sourceSpan, int len, Span offsetSpan) {
				return sourceSpan.Length == len && offsetSpan.Length != 0
					? new Span(sourceSpan.Start + offsetSpan.Start, offsetSpan.Length)
					: sourceSpan.ToSpan();
			}
		}

		void ShowSymbolContextMenu(CommandContext ctx) {
			ctx.KeepToolBar(false);
			if (UpdateSemanticModel() == false) {
				return;
			}
			var m = new CSharpSymbolContextMenu(_Symbol, _Context.NodeIncludeTrivia, _Context) {
				Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
				PlacementTarget = ctx.Sender
			};
			m.AddAnalysisCommands();
			m.AddFindAllReferencesCommand();
			m.AddGoToAnyCommands();
			ctx.Sender.ContextMenu = m;
			m.CommandExecuted += HideSmartBar;
			m.IsOpen = true;
		}

		void AddDirectiveCommands() {
			AddCommand(MyToolBar, IconIds.SelectCode, R.CMD_SelectDirectiveRegion, SelectDirectiveRegion);
		}

		void SelectDirectiveRegion(CommandContext ctx) {
			var a = _Context.NodeIncludeTrivia as DirectiveTriviaSyntax ?? default;
			if (a == null) {
				return;
			}
			DirectiveTriviaSyntax b;
			if (a.IsKind(SyntaxKind.EndRegionDirectiveTrivia) || a.IsKind(SyntaxKind.EndIfDirectiveTrivia)) {
				b = a;
				a = b.GetPreviousDirective();
				if (a == null) {
					return;
				}
			}
			else {
				b = a.GetNextDirective();
			}
			ctx.View.SelectSpan(new SnapshotSpan(ctx.View.TextSnapshot, Span.FromBounds(a.FullSpan.Start, b.FullSpan.End)));
		}

		bool SelectionIs<TNode>() where TNode : SyntaxNode {
			var s = View.Selection.SelectedSpans.FirstOrDefault().ToTextSpan();
			foreach (var item in _Context.NodeIncludeTrivia.AncestorsAndSelf()) {
				TextSpan span;
				if (item is TNode && (span = item.Span).Contains(s) && span != s) {
					return true;
				}
			}
			return false;
		}

		void SelectNodeAsKind<TNode>(CommandContext ctx) where TNode : SyntaxNode {
			var s = View.Selection.SelectedSpans.FirstOrDefault().ToTextSpan();
			foreach (var item in _Context.NodeIncludeTrivia.AncestorsAndSelf()) {
				TextSpan span;
				if (item is TNode && (span = item.Span).Contains(s) && span != s) {
					ctx.KeepToolBar(false);
					item.SelectNode(false);
					ctx.KeepToolBar(true);
					return;
				}
			}
		}

		void AddCommentCommands() {
			AddCommentCommand(MyToolBar);
			if (View.TryGetFirstSelectionSpan(out var ss) && ss.Length < 0x2000) {
				foreach (var t in _Context.Compilation.DescendantTrivia(ss.ToTextSpan())) {
					if (t.IsKind(SyntaxKind.SingleLineCommentTrivia)) {
						AddEditorCommand(MyToolBar, IconIds.Uncomment, "Edit.UncommentSelection", R.CMD_UncommentSelection);
						return;
					}
				}
			}
			if (_Context.GetLineComment().RawKind != 0) {
				AddEditorCommand(MyToolBar, IconIds.Uncomment, "Edit.UncommentSelection", R.CMD_UncommentSelection);
			}
		}

		void AddRenameCommand() {
			if (_Symbol != null && _Symbol.ContainingAssembly.GetSourceType() != AssemblySource.Metadata) {
				AddCommand(MyToolBar, IconIds.Rename, R.CMD_RenameSymbol, ctx => {
					ctx.KeepToolBar(false);
					TextEditorHelper.ExecuteEditorCommand("Refactor.Rename");
				});
			}
		}

		void AddRefactorCommands(SyntaxNode node) {
			AddRenameCommand();
			switch (node.Kind()) {
				case SyntaxKind.Parameter:
					if (node.Parent.IsKind(SyntaxKind.ParameterList)) {
						AddEditorCommand(MyToolBar, IconIds.ReorderParameters, "Refactor.ReorderParameters", R.CMD_ReorderParameters);
					}
					break;
				case SyntaxKind.IdentifierName:
				case SyntaxKind.PredefinedType:
					if (node.Parent.IsKind(SyntaxKind.Parameter) && node.Parent.Parent.IsKind(SyntaxKind.ParameterList)) {
						AddEditorCommand(MyToolBar, IconIds.ReorderParameters, "Refactor.ReorderParameters", R.CMD_ReorderParameters);
					}
					break;
				case SyntaxKind.ClassDeclaration:
				case SyntaxKind.StructDeclaration:
				case CodeAnalysisHelper.RecordDeclaration:
				case CodeAnalysisHelper.RecordStructDeclaration:
					if ((node as TypeDeclarationSyntax).Modifiers.Any(SyntaxKind.StaticKeyword) == false) {
						AddEditorCommand(MyToolBar, IconIds.ExtractInterface, "Refactor.ExtractInterface", R.CMD_ExtractInterface);
					}
					AddCommand(MyToolBar, IconIds.DeleteType, R.CMD_DeleteType, DeleteCurrentNode);
					break;
				case SyntaxKind.InterfaceDeclaration:
					AddCommand(MyToolBar, IconIds.DeleteType, R.CMD_DeleteInterface, DeleteCurrentNode);
					break;
				case SyntaxKind.ConstructorDeclaration:
				case SyntaxKind.DestructorDeclaration:
				case SyntaxKind.MethodDeclaration:
				case SyntaxKind.LocalFunctionStatement:
					AddCommand(MyToolBar, IconIds.DeleteMethod, R.CMD_DeleteMethod, DeleteCurrentNode);
					break;
				case SyntaxKind.PropertyDeclaration:
					AddCommand(MyToolBar, IconIds.DeleteProperty, R.CMD_DeleteProperty, DeleteCurrentNode);
					break;
				case SyntaxKind.EventDeclaration:
					AddCommand(MyToolBar, IconIds.DeleteEvent, R.CMD_DeleteEvent, DeleteCurrentNode);
					break;
				case SyntaxKind.EnumDeclaration:
					AddCommand(MyToolBar, IconIds.DeleteType, R.CMD_DeleteEnum, DeleteCurrentNode);
					break;
				case SyntaxKind.VariableDeclarator:
					if ((node = node.Parent?.Parent).IsAnyKind(SyntaxKind.FieldDeclaration, SyntaxKind.EventFieldDeclaration)) {
						if (node.IsKind(SyntaxKind.FieldDeclaration) && ((FieldDeclarationSyntax)node).Declaration.Variables.Count == 1) {
							AddEditorCommand(MyToolBar, IconIds.EncapsulateField, "Refactor.EncapsulateField", R.CMD_EncapsulateField);
							AddCommand(MyToolBar, IconIds.DeleteField, R.CMD_DeleteField, DeleteParentDeclaration);
						}
						else if (node.IsKind(SyntaxKind.EventFieldDeclaration) && ((EventFieldDeclarationSyntax)node).Declaration.Variables.Count == 1) {
							AddCommand(MyToolBar, IconIds.DeleteEvent, R.CMD_DeleteEvent, DeleteParentDeclaration);
						}
					}
					break;
			}
		}

		void DeleteCurrentNode(CommandContext ctx) {
			var node = _Context.Node;
			ctx.View.SelectNode(node, true);
			ctx.View.Edit(node, (view, n, edit) => edit.Delete(n.FullSpan.ToSpan()));
		}

		void DeleteParentDeclaration(CommandContext ctx) {
			var node = _Context.Node.Parent?.Parent;
			if (node != null) {
				ctx.View.SelectNode(node, true);
				ctx.View.Edit(node, (view, n, edit) => edit.Delete(n.FullSpan.ToSpan()));
			}
		}

		void AddXmlDocCommands() {
			AddCommand(MyToolBar, IconIds.TagCode, R.CMD_TagXmlDocC, ctx => WrapWith(ctx, "<c>", "</c>", true));
			AddCommand(MyToolBar, IconIds.TagXmlDocSee, R.CMD_TagXmlDocSee, WrapXmlDocSee);
			AddCommand(MyToolBar, IconIds.TagXmlDocPara, R.CMD_TagXmlDocPara, ctx => WrapWith(ctx, "<para>", "</para>", false));
			AddCommand(MyToolBar, IconIds.TagBold, R.CMD_TagXmlDocB, ctx => WrapWith(ctx, "<b>", "</b>", true));
			AddCommand(MyToolBar, IconIds.TagItalic, R.CMD_TagXmlDocI, ctx => WrapWith(ctx, "<i>", "</i>", true));
			AddCommand(MyToolBar, IconIds.TagUnderline, R.CMD_TagXmlDocU, ctx => WrapWith(ctx, "<u>", "</u>", true));
			AddCommand(MyToolBar, IconIds.TagHyperLink, R.CMD_TagXmlDocA, MakeUrl);
			AddCommentCommand(MyToolBar);
		}

		void WrapXmlDocSee(CommandContext ctx) {
			// updates the semantic model before executing the command,
			// for it could be modified by external editor commands or duplicated document windows
			if (UpdateSemanticModel() == false) {
				return;
			}
			ctx.View.Edit(new { me = this, ctx }, (view, arg, edit) => {
				arg.ctx.KeepToolBar(true);
				string t = null;
				foreach (var item in view.Selection.SelectedSpans) {
					t = item.GetText();
					var d = arg.me._Context.GetNode(item.Start, false, false).GetAncestorOrSelfDeclaration();
					if (d != null) {
						if (((d as BaseMethodDeclarationSyntax)?.ParameterList
							?? (d as DelegateDeclarationSyntax)?.ParameterList)
								?.Parameters.Any(p => p.Identifier.Text == t) == true) {
							edit.Replace(item, $"<paramref name=\"{t}\"/>");
							continue;
						}
						if (d.FindTypeParameter(t) != null) {
							edit.Replace(item, $"<typeparamref name=\"{t}\"/>");
							continue;
						}
						while ((d = d.Ancestors().FirstOrDefault() as TypeDeclarationSyntax) != null) {
							if (d.FindTypeParameter(t) != null) {
								edit.Replace(item, $"<typeparamref name=\"{t}\"/>");
								goto NEXT;
							}
						}
					}
					edit.Replace(item, (SyntaxFacts.GetKeywordKind(t) != SyntaxKind.None ? "<see langword=\"" : "<see cref=\"") + t + "\"/>");
					NEXT:;
				}
				if (t != null && ctx.ModifierKeys.MatchFlags(ModifierKeys.Control | ModifierKeys.Shift)
					&& FindNext(arg.ctx, t) == false) {
					arg.ctx.HideToolBar();
				}
			});
		}

		void MakeUrl(CommandContext ctx) {
			var v = ctx.View;
			var t = v.GetFirstSelectionText();
			if (t.StartsWith("http://", StringComparison.Ordinal) || t.StartsWith("https://", StringComparison.Ordinal)) {
				foreach (var s in WrapWith(ctx, "<a href=\"", "\">text</a>", false)) {
					if (s.Snapshot != null) {
						// select the "text"
						v.Selection.Select(new SnapshotSpan(s.Snapshot, s.End - 8, 4), false);
						v.Caret.MoveTo(s.End - 4);
						return;
					}
				}
			}
			else {
				foreach (var s in WrapWith(ctx, "<a href=\"url\">", "</a>", false)) {
					if (s.Snapshot != null) {
						// select the "url"
						v.Selection.Select(new SnapshotSpan(s.Snapshot, s.Start.Position + 9, 3), false);
						v.Caret.MoveTo(s.Start + 12);
						return;
					}
				}
			}
		}

		List<CommandItem> GetMarkerCommands(CommandContext arg) {
			var r = new List<CommandItem>(3);
			var symbol = _Symbol;
			if (symbol.Kind == SymbolKind.Method
				&& symbol is IMethodSymbol ctor && ctor.MethodKind == MethodKind.Constructor) {
				symbol = ctor.ContainingType;
			}
			r.Add(new CommandItem(IconIds.MarkSymbol, R.CMD_Mark.Replace("<NAME>", symbol.Name), AddHighlightMenuItems, null));
			if (Taggers.SymbolMarkManager.Contains(symbol)) {
				r.Add(new CommandItem(IconIds.UnmarkSymbol, R.CMD_Unmark.Replace("<NAME>", symbol.Name), UnmarkSymbolMark));
			}
			else if (Taggers.SymbolMarkManager.HasBookmark) {
				r.Add(CreateCommandMenu(IconIds.UnmarkSymbol, R.CMD_UnmarkSymbol, "No symbol marked", (ctx, m) => {
					foreach (var item in Taggers.SymbolMarkManager.MarkedSymbols) {
						m.Items.Add(new CommandMenuItem(this, new CommandItem(item.ImageId, item.DisplayString, _ => {
							Taggers.SymbolMarkManager.Remove(item);
							Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
						})));
					}
				}));
			}
			r.Add(new CommandItem(IconIds.ToggleBreakpoint, R.CMD_ToggleBreakpoint, _ => TextEditorHelper.ExecuteEditorCommand("Debug.ToggleBreakpoint")));
			r.Add(new CommandItem(IconIds.ToggleBookmark, R.CMD_ToggleBookmark, _ => TextEditorHelper.ExecuteEditorCommand("Edit.ToggleBookmark")));
			return r;
		}

		static readonly ExtensionProperty<MenuItem, ClassificationTag> __ClassificationTag = ExtensionProperty<MenuItem, ClassificationTag>.Register("ClassificationTag");
		void AddHighlightMenuItems(MenuItem menuItem) {
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 1", item => __ClassificationTag.Set(item, __HighlightClassifications.Highlight1), SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 2", item => __ClassificationTag.Set(item, __HighlightClassifications.Highlight2), SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 3", item => __ClassificationTag.Set(item, __HighlightClassifications.Highlight3), SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 4", item => __ClassificationTag.Set(item, __HighlightClassifications.Highlight4), SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 5", item => __ClassificationTag.Set(item, __HighlightClassifications.Highlight5), SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 6", item => __ClassificationTag.Set(item, __HighlightClassifications.Highlight6), SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 7", item => __ClassificationTag.Set(item, __HighlightClassifications.Highlight7), SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 8", item => __ClassificationTag.Set(item, __HighlightClassifications.Highlight8), SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 9", item => __ClassificationTag.Set(item, __HighlightClassifications.Highlight9), SetSymbolMark)));
		}

		void SetSymbolMark(CommandContext context) {
			if (_Symbol == null) {
				return;
			}
			if (_Symbol.Kind == SymbolKind.Method
				&& _Symbol is IMethodSymbol ctor && ctor.MethodKind == MethodKind.Constructor) {
				_Symbol = ctor.ContainingType;
			}
			Taggers.SymbolMarkManager.Update(_Symbol, __ClassificationTag.Get((MenuItem)context.Sender));
			Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
		}

		void UnmarkSymbolMark(CommandContext dummy) {
			UpdateSemanticModel();
			if (_Symbol != null && Taggers.SymbolMarkManager.Remove(_Symbol)) {
				Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
			}
		}

		bool UpdateSemanticModel() {
			return SyncHelper.RunSync(UpdateAsync);
		}

		Task<bool> UpdateAsync() {
			return _Context.UpdateAsync(View.Selection.Start.Position, default);
		}

		void HideSmartBar(object sender, EventArgs args) {
			var m = sender as CSharpSymbolContextMenu;
			m.PlacementTarget = null;
			m.CommandExecuted -= HideSmartBar;
			m.Dispose();
			HideToolBar();
		}

		void View_Closed(object sender, EventArgs e) {
			(sender as IWpfTextView).Closed -= View_Closed;
			_Context = null;
			_SymbolListContainer = null;
			_Symbol = null;
		}
	}
}
