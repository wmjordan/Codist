﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using AppHelpers;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using R = Codist.Properties.Resources;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Formatting;

namespace Codist.SmartBars
{
	//todo Make commands async and cancellable
	/// <summary>
	/// An extended <see cref="SmartBar"/> for C# content type.
	/// </summary>
	sealed class CSharpSmartBar : SmartBar {
		static readonly Taggers.HighlightClassifications __HighlightClassifications = Taggers.HighlightClassifications.Instance;
		SemanticContext _Context;
		ExternalAdornment _SymbolListContainer;
		ISymbol _Symbol;

		public CSharpSmartBar(IWpfTextView view, Microsoft.VisualStudio.Text.Operations.ITextSearchService2 textSearchService) : base(view, textSearchService) {
			ThreadHelper.ThrowIfNotOnUIThread();
			_Context = SemanticContext.GetOrCreateSingetonInstance(view);
			_SymbolListContainer = ExternalAdornment.GetOrCreate(view);
			view.Closed += View_Closed;
		}

		ToolBar MyToolBar => ToolBar2;

		protected override void AddCommands(CancellationToken cancellationToken) {
			if (UpdateSemanticModel() && _Context.NodeIncludeTrivia != null) {
				AddContextualCommands(cancellationToken);
			}
			base.AddCommands(cancellationToken);
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

		void AddContextualCommands(CancellationToken cancellationToken) {
			var isReadOnly = _Context.View.IsCaretInReadOnlyRegion();
			var node = _Context.NodeIncludeTrivia;
			var nodeKind = node.Kind();
			if (isReadOnly == false && nodeKind == SyntaxKind.XmlText) {
				AddXmlDocCommands();
				return;
			}
			var trivia = _Context.GetNodeTrivia();
			if (trivia.RawKind == 0) {
				var token = _Context.Token;
				var tokenKind = token.Kind();
				if (token.Span.Contains(View.Selection, true)) {
					switch (tokenKind) {
						case SyntaxKind.IdentifierToken:
						case SyntaxKind.SetKeyword:
						case SyntaxKind.GetKeyword:
						case CodeAnalysisHelper.InitKeyword:
							if (nodeKind.IsDeclaration()
								|| nodeKind == SyntaxKind.PredefinedType
								|| node is TypeSyntax
								|| nodeKind == SyntaxKind.Parameter
								|| nodeKind == SyntaxKind.VariableDeclarator
								|| nodeKind == SyntaxKind.ForEachStatement
								|| nodeKind == SyntaxKind.SingleVariableDesignation
								|| node is AccessorDeclarationSyntax) {
								// selection is within a symbol
								_Symbol = SyncHelper.RunSync(() => _Context.GetSymbolAsync(cancellationToken));
								if (_Symbol != null) {
									AddSymbolCommands(nodeKind);
								}

								if (isReadOnly == false) {
									AddRefactorCommands(node);
								}
							}
							else if (nodeKind == SyntaxKind.TypeParameter) {
								_Symbol = SyncHelper.RunSync(() => _Context.GetSymbolAsync(cancellationToken));
								if (_Symbol != null && isReadOnly == false) {
									AddRenameCommand();
								}
							}
							break;
						default:
							if (tokenKind.IsPredefinedSystemType()) {
								goto case SyntaxKind.IdentifierToken;
							}
							if (View.Selection.StreamSelectionSpan.Length < 4
								&& (tokenKind >= SyntaxKind.TildeToken && tokenKind <= SyntaxKind.PercentEqualsToken
									|| tokenKind == SyntaxKind.IsKeyword
									|| tokenKind == SyntaxKind.AsKeyword)
								&& SelectionIs<SyntaxNode>()) {
								AddCommand(MyToolBar, IconIds.SelectBlock, R.CMD_SelectBlock, SelectNodeAsKind<SyntaxNode>);
							}
							else if (tokenKind >= SyntaxKind.NumericLiteralToken && tokenKind <= SyntaxKind.StringLiteralToken) {
								AddEditorCommand(MyToolBar, IconIds.FindReference, "Edit.FindAllReferences", R.CMD_FindAllReferences);
							}
							break;
					}
				}
				else if (nodeKind.IsRegionalDirective()) {
					AddDirectiveCommands();
				}
				if (isReadOnly == false) {
					if (tokenKind == SyntaxKind.TrueKeyword || tokenKind == SyntaxKind.FalseKeyword) {
						AddCommand(MyToolBar, IconIds.ToggleValue, R.CMD_ToggleValue, ctx => Replace(ctx, v => v == "true" ? "false" : "true", true));
					}
					else if (tokenKind == SyntaxKind.ExplicitKeyword || tokenKind == SyntaxKind.ImplicitKeyword) {
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
					else if (node is ExpressionSyntax) {
						if (InvertOperatorRefactoring.IsInvertableOperation(nodeKind)) {
							AddCommand(MyToolBar, IconIds.InvertOperator, R.CMD_InvertOperator, InvertOperator);
						}
						if (SwapOperandRefactoring.IsSwappableOperation(nodeKind)) {
							AddCommand(MyToolBar, IconIds.SwapOperands, R.CMD_SwapOperands, SwapOperand);
						}
					}
					else if (nodeKind == SyntaxKind.IfStatement && node.Parent.IsKind(SyntaxKind.ElseClause) == false) {
						AddCommand(MyToolBar, IconIds.DeleteCondition, R.CMD_DeleteCondition, RefactorToDeleteCondition);
					}
				}
			}
			if (isReadOnly == false) {
				var statements = _Context.Compilation.GetStatements(_Context.View.FirstSelectionSpan().ToTextSpan());
				if (statements.Length > 0) {
					AddEditorCommand(MyToolBar, IconIds.ExtractMethod, "Refactor.ExtractMethod", R.CMD_ExtractMethod);
					AddCommand(MyToolBar, IconIds.Refactoring, R.CMD_RefactorSelection, ShowRefactorMenu);
				}
				AddCommentCommands();
				AddEditorCommand(MyToolBar, IconIds.QuickAction, "View.QuickActionsForPosition", R.CMD_QuickAction);
			}
		}

		void ShowRefactorMenu(CommandContext ctx) {
			ctx.KeepToolBar(false);
			if (UpdateSemanticModel() == false) {
				return;
			}
			var m = new ContextMenu() {
				Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
				PlacementTarget = ctx.Sender,
				Resources = SharedDictionaryManager.ContextMenu
			};
			m.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.If, R.CMD_WrapInIf, RefactorToIfStatement)));
			m.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.TryCatch, R.CMD_WrapInTryCatch, RefactorToTryCatchStatement)));
			var block = _Context.Node.Parent.FirstAncestorOrSelf<BlockSyntax>();
			if (block != null) {
				if (RemoveContainingStatementRefactoring.CheckContainingNode(block.Parent)) {
					m.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.Delete, R.CMD_DeleteContainingBlock, RefactorToDeleteContainingBlock)));
				}
			}

			ctx.Sender.ContextMenu = m;
			m.Closed += Menu_Closed;
			m.IsOpen = true;
		}

		void RefactorToTryCatchStatement(CommandContext c) {
			ReplaceStatements(WrapStatementRefactoring.WrapInTryCatch);
		}

		void RefactorToIfStatement(CommandContext c) {
			ReplaceStatements(WrapStatementRefactoring.WrapInIf);
		}

		void RefactorToUsingStatement(CommandContext c) {
			ReplaceStatements(WrapStatementRefactoring.WrapInUsing);
		}

		void RefactorToDeleteCondition(CommandContext ctx) {
			var ifs = ((IfStatementSyntax)_Context.Node).Statement;
			ReplaceNodes(View, _Context.Node, ifs is BlockSyntax b ? b.Statements : new SyntaxList<StatementSyntax>(ifs));
		}

		void InvertOperator(CommandContext ctx) {
			Replace(ctx, InvertOperatorRefactoring.InvertOperator, true);
		}

		void SwapOperand(CommandContext ctx) {
			SwapOperandRefactoring.Refactor(_Context);
		}

		void ReplaceStatements(Func<ImmutableArray<StatementSyntax>, SyntaxAnnotation, SyntaxNode> refactor) {
			var view = View;
			var statements = _Context.Compilation.GetStatements(view.FirstSelectionSpan().ToTextSpan());
			var start = view.Selection.StreamSelectionSpan.Start.Position;
			SyntaxAnnotation annStatement = new SyntaxAnnotation(),
				annSelect = new SyntaxAnnotation();
			SyntaxNode statement = refactor(statements, annSelect)
				.WithAdditionalAnnotations(annStatement);
			var root = statements[0].SyntaxTree.GetRoot()
				.InsertNodesBefore(statements[0], new[] { statement })
				.RemoveNodes(statements, SyntaxRemoveOptions.KeepNoTrivia);
			statement = Formatter.Format(root, annStatement, Workspace.GetWorkspaceRegistration(view.TextBuffer.AsTextContainer()).Workspace)
				.GetAnnotatedNodes(annStatement)
				.First();
			view.Edit(
				(rep: statement.ToString(), sel: view.FirstSelectionSpan()),
				(v, p, edit) => edit.Replace(p.sel, p.rep)
			);
			var selSpan = statement.GetAnnotatedNodes(annSelect).First().Span;
			view.SelectSpan(start.Position + (selSpan.Start - statement.SpanStart), selSpan.Length, 1);
		}

		void RefactorToDeleteContainingBlock(CommandContext c) {
			RemoveContainingStatementRefactoring.Refactor(_Context);
		}

		static void ReplaceNodes(IWpfTextView view, SyntaxNode oldNode, IEnumerable<SyntaxNode> newNodes) {
			var start = view.Selection.StreamSelectionSpan.Start.Position;
			var ann = new SyntaxAnnotation();
			List<SyntaxNode> nodes = new List<SyntaxNode>();
			foreach (var item in newNodes) {
				nodes.Add(item.WithAdditionalAnnotations(ann));
			}
			var root = oldNode.SyntaxTree.GetRoot();
			root = nodes.Count > 1
				? root.ReplaceNode(oldNode, nodes)
				: root.ReplaceNode(oldNode, nodes[0]);
			newNodes = Formatter.Format(root, ann, Workspace.GetWorkspaceRegistration(view.TextBuffer.AsTextContainer()).Workspace)
				.GetAnnotatedNodes(ann);
			view.Edit(
				(rep: String.Concat(newNodes.Select(i => i.ToFullString())), sel: oldNode.FullSpan.ToSpan()),
				(v, p, edit) => edit.Replace(p.sel, p.rep)
			);
			view.Caret.MoveTo(new SnapshotPoint(view.TextSnapshot, newNodes.First().SpanStart));
			if (nodes.Count == 1) {
				view.Selection.Select(new SnapshotSpan(view.TextSnapshot, newNodes.First().Span.ToSpan()), false);
			}
		}

		void AddSymbolCommands(SyntaxKind nodeKind) {
			if (nodeKind == SyntaxKind.IdentifierName || nodeKind == SyntaxKind.GenericName) {
				AddEditorCommand(MyToolBar, IconIds.GoToDefinition, "Edit.GoToDefinition", R.CMD_GoToDefinitionPeek, "Edit.PeekDefinition");
			}
			AddCommand(MyToolBar, IconIds.FindReference, R.CMD_AnalyzeSymbol, ShowSymbolContextMenu);
			if (Config.Instance.Features.MatchFlags(Features.SyntaxHighlight)
				&& Taggers.SymbolMarkManager.CanBookmark(_Symbol)) {
				AddCommands(MyToolBar, IconIds.Marks, R.CMD_MarkSymbol, null, GetMarkerCommands);
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
			m.Closed += Menu_Closed;
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
				if (item is TNode && item.Span.Contains(s) && item.Span != s) {
					return true;
				}
			}
			return false;
		}

		void SelectNodeAsKind<TNode>(CommandContext ctx) where TNode : SyntaxNode {
			var s = View.Selection.SelectedSpans.FirstOrDefault().ToTextSpan();
			foreach (var item in _Context.NodeIncludeTrivia.AncestorsAndSelf()) {
				if (item is TNode && item.Span.Contains(s) && item.Span != s) {
					ctx.KeepToolBar(false);
					item.SelectNode(false);
					ctx.KeepToolBar(true);
					return;
				}
			}
		}

		void AddCommentCommands() {
			AddCommand(MyToolBar, IconIds.Comment, R.CMD_CommentSelection, ctx => {
				if (ctx.RightClick) {
					ctx.View.ExpandSelectionToLine();
				}
				TextEditorHelper.ExecuteEditorCommand("Edit.CommentSelection");
			});
			if (View.TryGetFirstSelectionSpan(out var ss) && ss.Length < 0x2000) {
				foreach (var t in _Context.Compilation.DescendantTrivia(ss.ToTextSpan())) {
					if (t.IsKind(SyntaxKind.SingleLineCommentTrivia)) {
						AddEditorCommand(MyToolBar, IconIds.Uncomment, "Edit.UncommentSelection", R.CMD_UncommentSelection);
						return;
					}
				}
			}
			var token = _Context.Token;
			var triviaList = token.HasLeadingTrivia ? token.LeadingTrivia : token.HasTrailingTrivia ? token.TrailingTrivia : default;
			var lineComment = new SyntaxTrivia();
			if (triviaList.Equals(SyntaxTriviaList.Empty) == false && triviaList.FullSpan.Contains(View.Selection.Start.Position)) {
				lineComment = triviaList.FirstOrDefault(i => i.IsLineComment());
			}
			if (lineComment.RawKind != 0) {
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
				case CodeAnalysisHelper.RecordStructDesclaration:
					if ((node as TypeDeclarationSyntax).Modifiers.Any(SyntaxKind.StaticKeyword) == false) {
						AddEditorCommand(MyToolBar, IconIds.ExtractInterface, "Refactor.ExtractInterface", R.CMD_ExtractInterface);
					}
					break;
				case SyntaxKind.MethodDeclaration:
					AddCommand(MyToolBar, IconIds.DeleteMethod, "Delete method", ctx => {
						ctx.View.SelectNode(node, true);
						ctx.View.Edit(node, (view, n, edit) => {
							edit.Delete(n.FullSpan.ToSpan());
						});
					});
					break;
				case SyntaxKind.VariableDeclarator:
					if (node.Parent?.Parent.IsKind(SyntaxKind.FieldDeclaration) == true) {
						AddEditorCommand(MyToolBar, IconIds.EncapsulateField, "Refactor.EncapsulateField", R.CMD_EncapsulateField);
					}
					break;
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
			AddCommand(MyToolBar, IconIds.Comment, R.CMD_CommentSelection, ctx => {
				if (ctx.RightClick) {
					ctx.View.ExpandSelectionToLine();
				}
				TextEditorHelper.ExecuteEditorCommand("Edit.CommentSelection");
			});
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
					}
					edit.Replace(item, (SyntaxFacts.GetKeywordKind(t) != SyntaxKind.None ? "<see langword=\"" : "<see cref=\"") + t + "\"/>");
				}
				if (t != null && Keyboard.Modifiers.MatchFlags(ModifierKeys.Control | ModifierKeys.Shift)
					&& FindNext(arg.ctx, t) == false) {
					arg.ctx.HideToolBar();
				}
			});
		}

		void MakeUrl(CommandContext ctx) {
			var t = ctx.View.GetFirstSelectionText();
			if (t.StartsWith("http://", StringComparison.Ordinal) || t.StartsWith("https://", StringComparison.Ordinal)) {
				var s = WrapWith(ctx, "<a href=\"", "\">text</a>", false);
				if (s.Snapshot != null) {
					// select the "text"
					ctx.View.Selection.Select(new SnapshotSpan(s.Snapshot, s.End - 8, 4), false);
					ctx.View.Caret.MoveTo(s.End - 4);
				}
			}
			else {
				var s = WrapWith(ctx, "<a href=\"url\">", "</a>", false);
				if (s.Snapshot != null) {
					// select the "url"
					ctx.View.Selection.Select(new SnapshotSpan(s.Snapshot, s.Start + 9, 3), false);
					ctx.View.Caret.MoveTo(s.Start + 12);
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

		void AddHighlightMenuItems(MenuItem menuItem) {
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 1", item => item.Tag = __HighlightClassifications.Highlight1, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 2", item => item.Tag = __HighlightClassifications.Highlight2, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 3", item => item.Tag = __HighlightClassifications.Highlight3, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 4", item => item.Tag = __HighlightClassifications.Highlight4, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 5", item => item.Tag = __HighlightClassifications.Highlight5, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 6", item => item.Tag = __HighlightClassifications.Highlight6, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 7", item => item.Tag = __HighlightClassifications.Highlight7, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 8", item => item.Tag = __HighlightClassifications.Highlight8, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(IconIds.MarkSymbol, R.CMD_Highlight + " 9", item => item.Tag = __HighlightClassifications.Highlight9, SetSymbolMark)));
		}

		void SetSymbolMark(CommandContext context) {
			if (_Symbol == null) {
				return;
			}
			if (_Symbol.Kind == SymbolKind.Method
				&& _Symbol is IMethodSymbol ctor && ctor.MethodKind == MethodKind.Constructor) {
				_Symbol = ctor.ContainingType;
			}
			Taggers.SymbolMarkManager.Update(_Symbol, context.Sender.Tag as ClassificationTag);
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

		System.Threading.Tasks.Task<bool> UpdateAsync() {
			return _Context.UpdateAsync(View.Selection.Start.Position, default);
		}

		void Menu_Closed(object sender, EventArgs args) {
			var m = sender as ContextMenu;
			m.PlacementTarget = null;
			m.Closed -= Menu_Closed;
			(m as CSharpSymbolContextMenu)?.Dispose();
			HideToolBar();
		}

		void View_Closed(object sender, EventArgs e) {
			(sender as IWpfTextView).Closed -= View_Closed;
			_Context = null;
			_SymbolListContainer = null;
			_Symbol = null;
		}

		static class WrapStatementRefactoring
		{
			public static IfStatementSyntax WrapInIf(ImmutableArray<StatementSyntax> s, SyntaxAnnotation a) {
				return SF.IfStatement(
					SF.LiteralExpression(SyntaxKind.TrueLiteralExpression).WithAdditionalAnnotations(a),
					SF.Block(s));
			}

			public static TryStatementSyntax WrapInTryCatch(ImmutableArray<StatementSyntax> s, SyntaxAnnotation a) {
				return SF.TryStatement(SF.Block(s),
					new SyntaxList<CatchClauseSyntax>(
						SF.CatchClause(SF.Token(SyntaxKind.CatchKeyword), SF.CatchDeclaration(SF.IdentifierName("Exception").WithAdditionalAnnotations(a), SF.Identifier("ex")),
						null,
						SF.Block())),
					null);
			}

			public static UsingStatementSyntax WrapInUsing(ImmutableArray<StatementSyntax> s, SyntaxAnnotation a) {
				return SF.UsingStatement(null, SF.IdentifierName("disposable").WithAdditionalAnnotations(a), SF.Block(s));
			}
		}

		static class SwapOperandRefactoring
		{
			public static bool IsSwappableOperation(SyntaxKind kind) {
				switch (kind) {
					case SyntaxKind.BitwiseAndExpression:
					case SyntaxKind.BitwiseOrExpression:
					case SyntaxKind.LogicalOrExpression:
					case SyntaxKind.LogicalAndExpression:
					case SyntaxKind.EqualsExpression:
					case SyntaxKind.NotEqualsExpression:
					case SyntaxKind.GreaterThanExpression:
					case SyntaxKind.GreaterThanOrEqualExpression:
					case SyntaxKind.LessThanExpression:
					case SyntaxKind.LessThanOrEqualExpression:
					case SyntaxKind.AddExpression:
					case SyntaxKind.SubtractExpression:
					case SyntaxKind.MultiplyExpression:
					case SyntaxKind.DivideExpression:
					case SyntaxKind.CoalesceExpression:
						return true;
				}
				return false;
			}

			public static void Refactor(SemanticContext ctx) {
				var node = ctx.Node as BinaryExpressionSyntax;
				var newNode = node.Update(node.Right, node.OperatorToken, node.Left);
				ReplaceNodes(ctx.View, node, new[] { newNode });
			}
		}

		static class InvertOperatorRefactoring
		{
			public static bool IsInvertableOperation(SyntaxKind kind) {
				switch (kind) {
					case SyntaxKind.BitwiseAndExpression:
					case SyntaxKind.BitwiseOrExpression:
					case SyntaxKind.LogicalOrExpression:
					case SyntaxKind.LogicalAndExpression:
					case SyntaxKind.EqualsExpression:
					case SyntaxKind.NotEqualsExpression:
					case SyntaxKind.GreaterThanExpression:
					case SyntaxKind.GreaterThanOrEqualExpression:
					case SyntaxKind.LessThanExpression:
					case SyntaxKind.LessThanOrEqualExpression:
					case SyntaxKind.PostDecrementExpression:
					case SyntaxKind.PostIncrementExpression:
					case SyntaxKind.PreIncrementExpression:
					case SyntaxKind.PreDecrementExpression:
					case SyntaxKind.AddExpression:
					case SyntaxKind.SubtractExpression:
					case SyntaxKind.MultiplyExpression:
					case SyntaxKind.DivideExpression:
					case SyntaxKind.UnaryPlusExpression:
					case SyntaxKind.UnaryMinusExpression:
					case SyntaxKind.LeftShiftExpression:
					case SyntaxKind.RightShiftExpression:
					case SyntaxKind.AddAssignmentExpression:
					case SyntaxKind.SubtractAssignmentExpression:
					case SyntaxKind.MultiplyAssignmentExpression:
					case SyntaxKind.DivideAssignmentExpression:
					case SyntaxKind.AndAssignmentExpression:
					case SyntaxKind.OrAssignmentExpression:
					case SyntaxKind.LeftShiftAssignmentExpression:
					case SyntaxKind.RightShiftAssignmentExpression:
						return true;
				}
				return false;
			}

			public static string InvertOperator(string input) {
				switch (input) {
					case "==": return "!=";
					case "!=": return "==";
					case "&&": return "||";
					case "||": return "&&";
					case "--": return "++";
					case "++": return "--";
					case "<": return ">=";
					case ">": return "<=";
					case "<=": return ">";
					case ">=": return "<";
					case "+": return "-";
					case "-": return "+";
					case "*": return "/";
					case "/": return "*";
					case "&": return "|";
					case "|": return "&";
					case "<<": return ">>";
					case ">>": return "<<";
					case "+=": return "-=";
					case "-=": return "+=";
					case "*=": return "/=";
					case "/=": return "*=";
					case "<<=": return ">>=";
					case ">>=": return "<<=";
					case "&=": return "|=";
					case "|=": return "&=";
				}
				return null;
			}
		}

		static class RemoveContainingStatementRefactoring
		{
			public static bool CheckContainingNode(SyntaxNode node) {
				switch (node.Kind()) {
					case SyntaxKind.ForEachStatement:
					case SyntaxKind.ForEachVariableStatement:
					case SyntaxKind.ForStatement:
					case SyntaxKind.UsingStatement:
					case SyntaxKind.WhileStatement:
					case SyntaxKind.DoStatement:
					case SyntaxKind.LockStatement:
					case SyntaxKind.FixedStatement:
					case SyntaxKind.UnsafeStatement:
					case SyntaxKind.TryStatement:
					case SyntaxKind.CheckedStatement:
					case SyntaxKind.UncheckedStatement:
						return true;
					case SyntaxKind.IfStatement:
						return ((IfStatementSyntax)node).IsTopmostIf();
					case SyntaxKind.ElseClause:
						return ((ElseClauseSyntax)node).Statement?.Kind() != SyntaxKind.IfStatement;
				}
				return false;
			}

			public static void Refactor(SemanticContext ctx) {
				StatementSyntax s = ctx.Node.Parent.FirstAncestorOrSelf<BlockSyntax>();
				if (s.Parent.IsKind(SyntaxKind.ElseClause)) {
					var oldIf = s.FirstAncestorOrSelf<ElseClauseSyntax>().Parent.FirstAncestorOrSelf<IfStatementSyntax>();
					var newIf = oldIf.WithElse(null);
					var targets = ((BlockSyntax)s).Statements;
					if (s != null) {
						ReplaceNodes(ctx.View, oldIf, targets.Insert(0, newIf));
					}
				}
				else {
					var targets = s is BlockSyntax b ? b.Statements : (IEnumerable<SyntaxNode>)ctx.Compilation.GetStatements(ctx.View.FirstSelectionSpan().ToTextSpan());
					while (s.IsKind(SyntaxKind.Block)) {
						s = s.Parent.FirstAncestorOrSelf<StatementSyntax>();
					}
					if (s != null) {
						ReplaceNodes(ctx.View, s, targets);
					}
				}
			}
		}
	}
}
