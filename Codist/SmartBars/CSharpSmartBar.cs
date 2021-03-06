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
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
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
		readonly SemanticContext _Context;
		readonly ExternalAdornment _SymbolListContainer;
		ISymbol _Symbol;

		public CSharpSmartBar(IWpfTextView view, Microsoft.VisualStudio.Text.Operations.ITextSearchService2 textSearchService) : base(view, textSearchService) {
			ThreadHelper.ThrowIfNotOnUIThread();
			_Context = SemanticContext.GetOrCreateSingetonInstance(view);
			_SymbolListContainer = ExternalAdornment.GetOrCreate(view);
		}

		ToolBar MyToolBar => ToolBar2;

		protected override void AddCommands(CancellationToken cancellationToken) {
			if (UpdateSemanticModel() && _Context.NodeIncludeTrivia != null) {
				AddContextualCommands(cancellationToken);
			}
			//MyToolBar.Items.Add(new Separator());
			base.AddCommands(cancellationToken);
		}

		static CommandItem CreateCommandMenu(int imageId, string title, ISymbol symbol, string emptyMenuTitle, Action<CommandContext, MenuItem, ISymbol> itemPopulator) {
			return new CommandItem(imageId, title, ctrl => (ctrl as MenuItem).StaysOpenOnClick = true, ctx => {
				var menuItem = ctx.Sender as ThemedMenuItem;
				if (menuItem.Items.Count > 0 || menuItem.SubMenuHeader != null) {
					return;
				}
				ctx.KeepToolBarOnClick = true;
				itemPopulator(ctx, menuItem, symbol);
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
			// anti-pattern for a small margin of performance
			bool isDesignMode = CodistPackage.DebuggerStatus == DebuggerStatus.Design;
			var isReadOnly = _Context.View.IsCaretInReadOnlyRegion();
			var node = _Context.NodeIncludeTrivia;
			if (isDesignMode && isReadOnly == false && (node is XmlTextSyntax)) {
				AddXmlDocCommands();
				return;
			}
			var trivia = _Context.GetNodeTrivia();
			var nodeKind = node.Kind();
			if (trivia.RawKind == 0) {
				var token = _Context.Token;
				if (token.Span.Contains(View.Selection, true)
					&& (token.Kind() == SyntaxKind.IdentifierToken || token.RawKind == (int)SyntaxKind.SetKeyword || token.RawKind == (int)SyntaxKind.GetKeyword || token.RawKind == (int)CodeAnalysisHelper.InitKeyword)) {
					if (nodeKind.IsDeclaration() || node is TypeSyntax || node is ParameterSyntax || nodeKind == SyntaxKind.VariableDeclarator || nodeKind == SyntaxKind.ForEachStatement || nodeKind == SyntaxKind.SingleVariableDesignation || node is AccessorDeclarationSyntax) {
						// selection is within a symbol
						_Symbol = SyncHelper.RunSync(() => _Context.GetSymbolAsync(cancellationToken));
						if (_Symbol != null) {
							AddSymbolCommands(isReadOnly, node);
						}
					}
					else if (nodeKind == SyntaxKind.TypeParameter) {
						_Symbol = SyncHelper.RunSync(() => _Context.GetSymbolAsync(cancellationToken));
						if (_Symbol != null && isReadOnly == false) {
							AddRenameCommand(node);
						}
					}
				}
				else if (token.RawKind >= (int)SyntaxKind.NumericLiteralToken && token.RawKind <= (int)SyntaxKind.StringLiteralToken) {
					AddEditorCommand(MyToolBar, IconIds.FindReference, "Edit.FindAllReferences", R.CMD_FindAllReferences);
				}
				else if (nodeKind.IsRegionalDirective()) {
					AddDirectiveCommands();
				}
				if (isReadOnly == false) {
					if (token.IsKind(SyntaxKind.TrueKeyword) || token.IsKind(SyntaxKind.FalseKeyword)) {
						AddCommand(MyToolBar, IconIds.ToggleValue, R.CMD_ToggleValue, ctx => Replace(ctx, v => v == "true" ? "false" : "true", true));
					}
					else if (token.IsKind(SyntaxKind.ExplicitKeyword) || token.IsKind(SyntaxKind.ImplicitKeyword)) {
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
					else if (IsInvertableOperation(nodeKind)) {
						AddCommand(MyToolBar, IconIds.InvertOperator, R.CMD_InvertOperator, InvertOperator);
					}
					else if (isDesignMode && nodeKind != SyntaxKind.TypeParameter) {
						AddEditorCommand(MyToolBar, IconIds.ExtractMethod, "Refactor.ExtractMethod", R.CMD_ExtractMethod);
					}
				}
			}
			if (CodistPackage.DebuggerStatus != DebuggerStatus.Running && isReadOnly == false) {
				AddCommentCommands();
			}
			if (isDesignMode == false) {
				AddCommands(MyToolBar, IconIds.ToggleBreakpoint, R.CMD_Debugger, ctx => TextEditorHelper.ExecuteEditorCommand("Debug.ToggleBreakpoint"), ctx => DebugCommands);
			}
			//AddCommands(MyToolBar, KnownImageIds.SelectFrame, "Expand selection...\nRight click: Duplicate...\nCtrl click item: Copy\nShift click item: Exclude whitespaces and comments", null, GetExpandSelectionCommands);
		}

		void AddSymbolCommands(bool isReadOnly, SyntaxNode node) {
			if (node.IsKind(SyntaxKind.IdentifierName) || node.IsKind(SyntaxKind.GenericName)) {
				AddEditorCommand(MyToolBar, IconIds.GoToDefinition, "Edit.GoToDefinition", R.CMD_GoToDefinitionPeek, "Edit.PeekDefinition");
			}
			AddCommand(MyToolBar, IconIds.FindReference, R.CMD_AnalyzeSymbol, ctx => {
				ctx.KeepToolBar(false);
				if (UpdateSemanticModel() == false) {
					return;
				}
				var m = new CSharpSymbolContextMenu(_Context) {
					Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
					PlacementTarget = ctx.Sender,
					Symbol = _Symbol,
					SyntaxNode = node
				};
				m.ItemClicked += (s, args) => HideToolBar();
				m.AddAnalysisCommands();
				m.AddFindAllReferencesCommand();
				m.AddGoToAnyCommands();
				ctx.Sender.ContextMenu = m;
				m.IsOpen = true;
			});
			if (Config.Instance.Features.MatchFlags(Features.SyntaxHighlight) && Taggers.SymbolMarkManager.CanBookmark(_Symbol)) {
				AddCommands(MyToolBar, IconIds.Marks, R.CMD_MarkSymbol, null, GetMarkerCommands);
			}

			if (/*isDesignMode && */isReadOnly == false) {
				AddRefactorCommands(node);
			}
		}

		void AddDirectiveCommands() {
			AddCommand(MyToolBar, IconIds.SelectCode, R.CMD_SelectDirectiveRegion, ctx => {
				var a = _Context.NodeIncludeTrivia as DirectiveTriviaSyntax;
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
			});
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

		void AddRenameCommand(SyntaxNode node) {
			if (_Symbol.ContainingAssembly.GetSourceType() != AssemblySource.Metadata) {
				AddCommand(MyToolBar, IconIds.Rename, R.CMD_RenameSymbol, ctx => {
					ctx.KeepToolBar(false);
					TextEditorHelper.ExecuteEditorCommand("Refactor.Rename");
				});
			}
		}

		void AddRefactorCommands(SyntaxNode node) {
			AddRenameCommand(node);
			if (node is ParameterSyntax && node.Parent is ParameterListSyntax) {
				AddEditorCommand(MyToolBar, IconIds.ReorderParameters, "Refactor.ReorderParameters", R.CMD_ReorderParameters);
			}
			else if ((node.IsKind(SyntaxKind.ClassDeclaration) || node.IsKind(SyntaxKind.StructDeclaration) || node.IsKind(CodeAnalysisHelper.RecordDeclaration)) && (node as TypeDeclarationSyntax).Modifiers.Any(SyntaxKind.StaticKeyword) == false) {
				AddEditorCommand(MyToolBar, IconIds.ExtractInterface, "Refactor.ExtractInterface", R.CMD_ExtractInterface);
			}
			else if (node.IsKind(SyntaxKind.VariableDeclarator) && node.Parent?.Parent.IsKind(SyntaxKind.FieldDeclaration) == true) {
				AddEditorCommand(MyToolBar, IconIds.EncapsulateField, "Refactor.EncapsulateField", R.CMD_EncapsulateField);
			}
		}

		void AddXmlDocCommands() {
			AddCommand(MyToolBar, IconIds.TagCode, R.CMD_TagXmlDocC, ctx => {
				WrapWith(ctx, "<c>", "</c>", true);
			});
			AddCommand(MyToolBar, IconIds.TagXmlDocSee, R.CMD_TagXmlDocSee, ctx => {
				// updates the semantic model before executing the command,
				// for it could be modified by external editor commands or duplicated document windows
				if (UpdateSemanticModel() == false) {
					return;
				}
				ctx.View.Edit((view, edit) => {
					ctx.KeepToolBar(true);
					string t = null;
					foreach (var item in view.Selection.SelectedSpans) {
						t = item.GetText();
						var d = _Context.GetNode(item.Start, false, false).GetAncestorOrSelfDeclaration();
						if (d != null) {
							if (((d as BaseMethodDeclarationSyntax)?.ParameterList
								?? (d as DelegateDeclarationSyntax)?.ParameterList)
									?.Parameters.Any(p => p.Identifier.Text == t) == true) {
								edit.Replace(item, "<paramref name=\"" + t + "\"/>");
								continue;
							}
							if (d.FindTypeParameter(t) != null) {
								edit.Replace(item, "<typeparamref name=\"" + t + "\"/>");
								continue;
							}
						}
						edit.Replace(item, (SyntaxFacts.GetKeywordKind(t) != SyntaxKind.None ? "<see langword=\"" : "<see cref=\"") + t + "\"/>");
					}
					if (t != null && Keyboard.Modifiers.MatchFlags(ModifierKeys.Control | ModifierKeys.Shift)
						&& FindNext(ctx, t) == false) {
						ctx.HideToolBar();
					}
				});
			});
			AddCommand(MyToolBar, IconIds.TagXmlDocPara, R.CMD_TagXmlDocPara, ctx => {
				WrapWith(ctx, "<para>", "</para>", false);
			});
			AddCommand(MyToolBar, IconIds.TagBold, R.CMD_TagXmlDocB, ctx => {
				WrapWith(ctx, "<b>", "</b>", true);
			});
			AddCommand(MyToolBar, IconIds.TagItalic, R.CMD_TagXmlDocI, ctx => {
				WrapWith(ctx, "<i>", "</i>", true);
			});
			AddCommand(MyToolBar, IconIds.TagUnderline, R.CMD_TagXmlDocU, ctx => {
				WrapWith(ctx, "<u>", "</u>", true);
			});
			AddCommand(MyToolBar, IconIds.Comment, R.CMD_CommentSelection, ctx => {
				if (ctx.RightClick) {
					ctx.View.ExpandSelectionToLine();
				}
				TextEditorHelper.ExecuteEditorCommand("Edit.CommentSelection");
			});
		}

		List<CommandItem> GetMarkerCommands(CommandContext arg) {
			var r = new List<CommandItem>(3);
			var symbol = _Symbol;
			if (symbol.Kind == SymbolKind.Method) {
				var ctor = symbol as IMethodSymbol;
				if (ctor != null && ctor.MethodKind == MethodKind.Constructor) {
					symbol = ctor.ContainingType;
				}
			}
			r.Add(new CommandItem(IconIds.MarkSymbol, R.CMD_Mark.Replace("<NAME>", symbol.Name), AddHighlightMenuItems, null));
			if (Taggers.SymbolMarkManager.Contains(symbol)) {
				r.Add(new CommandItem(IconIds.UnmarkSymbol, R.CMD_Unmark.Replace("<NAME>", symbol.Name), ctx => {
					UpdateSemanticModel();
					if (_Symbol != null && Taggers.SymbolMarkManager.Remove(_Symbol)) {
						Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
						return;
					}
				}));
			}
			else if (Taggers.SymbolMarkManager.HasBookmark) {
				r.Add(CreateCommandMenu(IconIds.UnmarkSymbol, R.CMD_UnmarkSymbol, symbol, "No symbol marked", (ctx, m, s) => {
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
			if (_Symbol.Kind == SymbolKind.Method) {
				var ctor = _Symbol as IMethodSymbol;
				if (ctor != null && ctor.MethodKind == MethodKind.Constructor) {
					_Symbol = ctor.ContainingType;
				}
			}
			Taggers.SymbolMarkManager.Update(_Symbol, context.Sender.Tag as ClassificationTag);
			Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
		}

		List<CommandItem> GetExpandSelectionCommands(CommandContext ctx) {
			var r = new List<CommandItem>();
			var duplicate = ctx.RightClick;
			var node = _Context.NodeIncludeTrivia;
			while (node != null) {
				if (node.FullSpan.Contains(ctx.View.Selection, false)) {
					var nodeKind = node.Kind();
					if ((nodeKind.IsSyntaxBlock() || nodeKind.IsDeclaration() || nodeKind ==SyntaxKind.VariableDeclarator)
					&& nodeKind != SyntaxKind.VariableDeclaration) {
						var n = node;
						r.Add(new CommandItem(CodeAnalysisHelper.GetImageId(n), (duplicate ? "Duplicate " : "Select ") + nodeKind.GetSyntaxBrief() + " " + n.GetDeclarationSignature(), ctx2 => {
							ctx2.View.SelectNode(n, Keyboard.Modifiers == ModifierKeys.Shift ^ Config.Instance.SmartBarOptions.MatchFlags(SmartBarOptions.ExpansionIncludeTrivia) || n.Span.Contains(ctx2.View.Selection, false) == false);
							if (Keyboard.Modifiers == ModifierKeys.Control) {
								TextEditorHelper.ExecuteEditorCommand("Edit.Copy");
							}
							if (duplicate) {
								TextEditorHelper.ExecuteEditorCommand("Edit.Duplicate");
							}
						}));
					}
				}
				node = node.Parent;
			}
			r.Add(new CommandItem(IconIds.SelectAll, R.CMD_SelectAll, ctrl => ctrl.ToolTip = R.CMDT_SelectAll, ctx2 => TextEditorHelper.ExecuteEditorCommand("Edit.SelectAll")));
			return r;
		}

		static bool IsInvertableOperation(SyntaxKind kind) {
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

		void InvertOperator(CommandContext ctx) {
			Replace(ctx, input => {
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
			}, true);
		}

		bool UpdateSemanticModel() {
			return SyncHelper.RunSync(() => _Context.UpdateAsync(View.Selection.Start.Position, default));
		}
	}
}
