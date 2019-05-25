using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AppHelpers;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.SmartBars
{
	//todo Make commands async and cancellable
	/// <summary>
	/// An extended <see cref="SmartBar"/> for C# content type.
	/// </summary>
	sealed class CSharpSmartBar : SmartBar {
		static readonly Classifiers.HighlightClassifications __HighlightClassifications = new Classifiers.HighlightClassifications(ServicesHelper.Instance.ClassificationTypeRegistry);
		readonly SemanticContext _Context;
		readonly bool _IsVsProject;
		readonly ExternalAdornment _SymbolListContainer;
		ISymbol _Symbol;
		SymbolList _SymbolList;

		public CSharpSmartBar(IWpfTextView view, Microsoft.VisualStudio.Text.Operations.ITextSearchService2 textSearchService) : base(view, textSearchService) {
			ThreadHelper.ThrowIfNotOnUIThread();
			_Context = SemanticContext.GetOrCreateSingetonInstance(view);
			_SymbolListContainer = new ExternalAdornment(view);
			View.Selection.SelectionChanged += ViewSeletionChanged;
			var extenders = CodistPackage.DTE.ActiveDocument?.ProjectItem?.ContainingProject?.ExtenderNames as string[];
			if (extenders != null) {
				_IsVsProject = Array.IndexOf(extenders, "VsixProjectExtender") != -1;
			}
		}

		ToolBar MyToolBar => ToolBar2;

		protected override void AddCommands(CancellationToken cancellationToken) {
			if (UpdateSemanticModel() && _Context.NodeIncludeTrivia != null) {
				AddContextualCommands(cancellationToken);
			}
			//MyToolBar.Items.Add(new Separator());
			base.AddCommands(cancellationToken);
		}

		void ViewSeletionChanged(object sender, EventArgs e) {
			HideMenu();
		}

		void HideMenu() {
			if (_SymbolList != null) {
				_SymbolListContainer.Clear();
				_SymbolList.SelectedItem = null;
				_SymbolList = null;
			}
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
					new MemberFilterBox(new MenuItemFilter(menuItem.Items)),
					new Separator()
				}
			};
		}

		void AddContextualCommands(CancellationToken cancellationToken) {
			// anti-pattern for a small margin of performance
			bool isDesignMode = CodistPackage.DebuggerStatus == DebuggerStatus.Design;
			var isReadOnly = _Context.View.IsCaretInReadOnlyRegion();
			var node = _Context.NodeIncludeTrivia;
			if (isDesignMode && isReadOnly == false && node is XmlTextSyntax) {
				AddXmlDocCommands();
				return;
			}
			var trivia = _Context.GetNodeTrivia();
			if (trivia.RawKind == 0) {
				var token = _Context.Token;
				if (token.Span.Contains(View.Selection, true)
					&& token.Kind() == SyntaxKind.IdentifierToken
					&& (node.IsDeclaration() || node is TypeSyntax || node is ParameterSyntax || node.IsKind(SyntaxKind.VariableDeclarator) || node.IsKind(SyntaxKind.ForEachStatement))) {
					// selection is within a symbol
					_Symbol = ThreadHelper.JoinableTaskFactory.Run(() => _Context.GetSymbolAsync(cancellationToken));
					if (_Symbol != null) {
						if (node.IsKind(SyntaxKind.IdentifierName) || node.IsKind(SyntaxKind.GenericName)) {
							AddEditorCommand(MyToolBar, KnownImageIds.GoToDefinition, "Edit.GoToDefinition", "Go to definition\nRight click: Peek definition", "Edit.PeekDefinition");
						}
						AddCommands(MyToolBar, KnownImageIds.ReferencedDimension, "Analyze symbol...", GetReferenceCommandsAsync);
						if (Classifiers.SymbolMarkManager.CanBookmark(_Symbol)) {
							AddCommands(MyToolBar, KnownImageIds.FlagGroup, "Mark symbol...", null, GetMarkerCommands);
						}

						if (/*isDesignMode && */isReadOnly == false) {
							AddRefactorCommands(node);
						}
					}
				}
				else if (token.RawKind >= (int)SyntaxKind.NumericLiteralToken && token.RawKind <= (int)SyntaxKind.StringLiteralToken) {
					AddEditorCommand(MyToolBar, KnownImageIds.ReferencedDimension, "Edit.FindAllReferences", "Find all references");
				}
				else if (isReadOnly == false && (token.IsKind(SyntaxKind.TrueKeyword) || token.IsKind(SyntaxKind.FalseKeyword))) {
					AddCommand(MyToolBar, KnownImageIds.ToggleButton, "Toggle value", ctx => {
						Replace(ctx, v => v == "true" ? "false" : "true", true);
					});
				}
				else if (node.IsRegionalDirective()) {
					AddDirectiveCommands();
				}
				if (/*isDesignMode && */isReadOnly == false) {
					if (node.IsKind(SyntaxKind.VariableDeclarator)) {
						if (node?.Parent?.Parent is MemberDeclarationSyntax) {
							AddCommand(MyToolBar, KnownImageIds.AddComment, "Insert comment", ctx => {
								TextEditorHelper.ExecuteEditorCommand("Edit.InsertComment");
								ctx.View.Selection.Clear();
							});
						}
					}
					else if (node.IsDeclaration()) {
						if (node is TypeDeclarationSyntax || node is MemberDeclarationSyntax || node is ParameterListSyntax) {
							AddCommand(MyToolBar, KnownImageIds.AddComment, "Insert comment", ctx => {
								TextEditorHelper.ExecuteEditorCommand("Edit.InsertComment");
								ctx.View.Selection.Clear();
							});
						}
					}
					else if (IsInvertableOperation(node.Kind())) {
						AddCommand(MyToolBar, KnownImageIds.Operator, "Invert operator", InvertOperator);
					}
					else if (isDesignMode) {
						AddEditorCommand(MyToolBar, KnownImageIds.ExtractMethod, "Refactor.ExtractMethod", "Extract Method");
					}
				}
			}
			if (CodistPackage.DebuggerStatus != DebuggerStatus.Running && isReadOnly == false) {
				AddCommentCommands();
			}
			if (isDesignMode == false) {
				AddCommands(MyToolBar, KnownImageIds.BreakpointEnabled, "Debugger...\nLeft click: Toggle breakpoint\nRight click: Debugger menu...", ctx => TextEditorHelper.ExecuteEditorCommand("Debug.ToggleBreakpoint"), ctx => DebugCommands);
			}
			AddCommands(MyToolBar, KnownImageIds.SelectFrame, "Expand selection...\nRight click: Duplicate...\nCtrl click item: Copy\nShift click item: Exclude whitespaces and comments", null, GetExpandSelectionCommands);
		}

		void AddDirectiveCommands() {
			AddCommand(MyToolBar, KnownImageIds.BlockSelection, "Select directive region", ctx => {
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
			AddCommand(MyToolBar, KnownImageIds.CommentCode, "Comment selection\nRight click: Comment line", ctx => {
				if (ctx.RightClick) {
					ctx.View.ExpandSelectionToLine();
				}
				TextEditorHelper.ExecuteEditorCommand("Edit.CommentSelection");
			});
			if (View.TryGetFirstSelectionSpan(out var ss) && ss.Length < 0x2000) {
				foreach (var t in _Context.Compilation.DescendantTrivia(ss.ToTextSpan())) {
					if (t.IsKind(SyntaxKind.SingleLineCommentTrivia)) {
						AddEditorCommand(MyToolBar, KnownImageIds.UncommentCode, "Edit.UncommentSelection", "Uncomment selection");
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
				AddEditorCommand(MyToolBar, KnownImageIds.UncommentCode, "Edit.UncommentSelection", "Uncomment selection");
			}
		}

		void AddRefactorCommands(SyntaxNode node) {
			if (_Symbol.ContainingAssembly.GetSourceType() != AssemblySource.Metadata) {
				AddCommand(MyToolBar, KnownImageIds.Rename, "Rename symbol", ctx => {
					ctx.KeepToolBar(false);
					TextEditorHelper.ExecuteEditorCommand("Refactor.Rename");
				});
			}
			if (node is ParameterSyntax && node.Parent is ParameterListSyntax) {
				AddEditorCommand(MyToolBar, KnownImageIds.ReorderParameters, "Refactor.ReorderParameters", "Reorder parameters");
			}
			if (node.IsKind(SyntaxKind.ClassDeclaration) || node.IsKind(SyntaxKind.StructDeclaration)) {
				AddEditorCommand(MyToolBar, KnownImageIds.ExtractInterface, "Refactor.ExtractInterface", "Extract interface");
			}
		}

		void AddXmlDocCommands() {
			AddCommand(MyToolBar, KnownImageIds.MarkupTag, "Tag XML Doc with <c>", ctx => {
				SurroundWith(ctx, "<c>", "</c>", true);
			});
			AddCommand(MyToolBar, KnownImageIds.GoToNext, "Tag XML Doc with <see> or <paramref>", ctx => {
				// updates the semantic model before executing the command,
				// for it could be modified by external editor commands or duplicated document windows
				if (UpdateSemanticModel() == false) {
					return;
				}
				ctx.View.Edit((view, edit) => {
					foreach (var item in view.Selection.SelectedSpans) {
						var t = item.GetText();
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
				});
			});
			AddCommand(MyToolBar, KnownImageIds.ParagraphHardReturn, "Tag XML Doc with <para>", ctx => {
				SurroundWith(ctx, "<para>", "</para>", false);
			});
			AddCommand(MyToolBar, KnownImageIds.Bold, "Tag XML Doc with HTML <b>", ctx => {
				SurroundWith(ctx, "<b>", "</b>", true);
			});
			AddCommand(MyToolBar, KnownImageIds.Italic, "Tag XML Doc with HTML <i>", ctx => {
				SurroundWith(ctx, "<i>", "</i>", true);
			});
			AddCommand(MyToolBar, KnownImageIds.Underline, "Tag XML Doc with HTML <u>", ctx => {
				SurroundWith(ctx, "<u>", "</u>", true);
			});
			AddCommand(MyToolBar, KnownImageIds.CommentCode, "Comment selection\nRight click: Comment line", ctx => {
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
			r.Add(new CommandItem(KnownImageIds.Flag, "Mark " + symbol.Name, AddHighlightMenuItems, null));
			if (Classifiers.SymbolMarkManager.Contains(symbol)) {
				r.Add(new CommandItem(KnownImageIds.FlagOutline, "Unmark " + symbol.Name, ctx => {
					UpdateSemanticModel();
					if (_Symbol != null && Classifiers.SymbolMarkManager.Remove(_Symbol)) {
						Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
						return;
					}
				}));
			}
			else if (Classifiers.SymbolMarkManager.HasBookmark) {
				r.Add(CreateCommandMenu(KnownImageIds.FlagOutline, "Unmark symbol...", symbol, "No symbol marked", (ctx, m, s) => {
					foreach (var item in Classifiers.SymbolMarkManager.MarkedSymbols) {
						m.Items.Add(new CommandMenuItem(this, new CommandItem(item.ImageId, item.DisplayString, _ => {
							Classifiers.SymbolMarkManager.Remove(item);
							Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
						})));
					}
				}));
			}
			return r;
		}

		void AddHighlightMenuItems(MenuItem menuItem) {
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(KnownImageIds.Flag, "Highlight 1", item => item.Tag = __HighlightClassifications.Highlight1, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(KnownImageIds.Flag, "Highlight 2", item => item.Tag = __HighlightClassifications.Highlight2, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(KnownImageIds.Flag, "Highlight 3", item => item.Tag = __HighlightClassifications.Highlight3, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(KnownImageIds.Flag, "Highlight 4", item => item.Tag = __HighlightClassifications.Highlight4, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(KnownImageIds.Flag, "Highlight 5", item => item.Tag = __HighlightClassifications.Highlight5, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(KnownImageIds.Flag, "Highlight 6", item => item.Tag = __HighlightClassifications.Highlight6, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(KnownImageIds.Flag, "Highlight 7", item => item.Tag = __HighlightClassifications.Highlight7, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(KnownImageIds.Flag, "Highlight 8", item => item.Tag = __HighlightClassifications.Highlight8, SetSymbolMark)));
			menuItem.Items.Add(new CommandMenuItem(this, new CommandItem(KnownImageIds.Flag, "Highlight 9", item => item.Tag = __HighlightClassifications.Highlight9, SetSymbolMark)));
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
			Classifiers.SymbolMarkManager.Update(_Symbol, context.Sender.Tag as Microsoft.VisualStudio.Text.Classification.IClassificationType);
			Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
		}

		void FindCallers(CommandContext context, ISymbol source) {
			var doc = _Context.Document;
			var docs = System.Collections.Immutable.ImmutableHashSet.CreateRange(doc.Project.GetRelatedProjectDocuments());
			List<SymbolCallerInfo> callers;
			switch (source.Kind) {
				case SymbolKind.Method:
				case SymbolKind.Property:
				case SymbolKind.Event:
					callers = ThreadHelper.JoinableTaskFactory.Run(() => SymbolFinder.FindCallersAsync(source, doc.Project.Solution, docs, context.CancellationToken)).ToList();
					break;
				case SymbolKind.NamedType:
					var tempResults = new HashSet<SymbolCallerInfo>(SymbolCallerInfoComparer.Instance);
					ThreadHelper.JoinableTaskFactory.Run(async () => {
						foreach (var item in (source as INamedTypeSymbol).InstanceConstructors) {
							foreach (var c in await SymbolFinder.FindCallersAsync(item, doc.Project.Solution, docs, context.CancellationToken)) {
								tempResults.Add(c);
							}
						}
					});
					(callers = new List<SymbolCallerInfo>(tempResults.Count)).AddRange(tempResults);
					break;
				default: return;
			}
			callers.Sort((a, b) => CodeAnalysisHelper.CompareSymbol(a.CallingSymbol, b.CallingSymbol));
			var m = new SymbolMenu(this);
			m.Title.SetGlyph(ThemeHelper.GetImage(source.GetImageId()))
				.Append(source.ToDisplayString(WpfHelper.MemberNameFormat), true)
				.Append(" callers");
			var containerType = source.ContainingType;
			foreach (var caller in callers) {
				var s = caller.CallingSymbol;
				var i = m.Menu.Add(s, false);
				i.Location = caller.Locations.FirstOrDefault();
				if (s.ContainingType != containerType) {
					i.Hint = s.ContainingType.ToDisplayString(WpfHelper.MemberNameFormat);
				}
			}
			m.Show();
		}

		void FindDerivedClasses(CommandContext context, ISymbol symbol) {
			var classes = ThreadHelper.JoinableTaskFactory.Run(() => SymbolFinder.FindDerivedClassesAsync(symbol as INamedTypeSymbol, _Context.Document.Project.Solution, null, context.CancellationToken)).Cast<ISymbol>().ToList();
			classes.Sort((a, b) => a.Name.CompareTo(b.Name));
			ShowSymbolMenuForResult(symbol, classes, " derived classes", false);
		}

		void FindImplementations(CommandContext context, ISymbol symbol) {
			var implementations = new List<ISymbol>(ThreadHelper.JoinableTaskFactory.Run(() => SymbolFinder.FindImplementationsAsync(symbol, _Context.Document.Project.Solution, null, context.CancellationToken)));
			implementations.Sort((a, b) => a.Name.CompareTo(b.Name));
			var m = new SymbolMenu(this);
			if (symbol.Kind == SymbolKind.NamedType) {
				foreach (var impl in implementations) {
					m.Menu.Add(impl, false);
				}
			}
			else {
				foreach (var impl in implementations) {
					m.Menu.Add(impl, impl.ContainingSymbol);
				}
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(WpfHelper.MemberNameFormat), true)
				.Append(" implementations: ")
				.Append(implementations.Count.ToString());
			m.Show();
		}

		void FindInstanceAsParameter(CommandContext context, ISymbol source) {
			ThreadHelper.JoinableTaskFactory.Run(async () => {
				var members = await (source as ITypeSymbol).FindInstanceAsParameterAsync(_Context.Document.Project, context.CancellationToken);
				ShowSymbolMenuForResult(source, members, " as parameter", true);
			});
		}

		void FindInstanceProducer(CommandContext context, ISymbol source) {
			ThreadHelper.JoinableTaskFactory.Run(async () => {
				var members = await (source as ITypeSymbol).FindSymbolInstanceProducerAsync(_Context.Document.Project, context.CancellationToken);
				ShowSymbolMenuForResult(source, members, " producers", true);
			});
		}

		void FindExtensionMethods(CommandContext context, ISymbol source) {
			ThreadHelper.JoinableTaskFactory.Run(async () => {
				var members = await (source as ITypeSymbol).FindExtensionMethodsAsync(_Context.Document.Project, context.CancellationToken);
				ShowSymbolMenuForResult(source, members, " extensions", true);
			});
		}

		void ShowSymbolMenuForResult(ISymbol source, List<ISymbol> members, string suffix, bool groupByType) {
			members.Sort(CodeAnalysisHelper.CompareSymbol);
			var m = new SymbolMenu(this);
			m.Title.SetGlyph(ThemeHelper.GetImage(source.GetImageId()))
				.Append(source.ToDisplayString(WpfHelper.MemberNameFormat), true)
				.Append(suffix);
			ITypeSymbol containingType = null;
			foreach (var item in members) {
				if (groupByType && item.ContainingType != containingType) {
					m.Menu.Add((containingType = item.ContainingType), false)
						.Type = SymbolItemType.Container;
				}
				m.Menu.Add(item, false);
			}
			m.Show();
		}

		void FindMembers(ISymbol symbol) {
			var m = new SymbolMenu(this, symbol.Kind == SymbolKind.Namespace ? SymbolListType.TypeList : SymbolListType.None);
			var (count, inherited) = m.Menu.AddSymbolMembers(symbol, _IsVsProject);
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(WpfHelper.MemberNameFormat), true)
				.Append(" members: ")
				.Append(count + " (" + inherited + " inherited)");
			m.Show();
		}

		void FindOverrides(CommandContext context, ISymbol symbol) {
			var m = new SymbolMenu(this);
			int c = 0;
			foreach (var ov in ThreadHelper.JoinableTaskFactory.Run(() => SymbolFinder.FindOverridesAsync(symbol, _Context.Document.Project.Solution, null, context.CancellationToken))) {
				m.Menu.Add(ov, ov.ContainingType);
				++c;
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(WpfHelper.MemberNameFormat), true)
				.Append(" overrides: ")
				.Append(c.ToString());
			m.Show();
		}

		void FindSymbolWithName(CommandContext ctx, ISymbol source) {
			var result = _Context.SemanticModel.Compilation.FindDeclarationMatchName(source.Name, Keyboard.Modifiers == ModifierKeys.Control, true, ctx.CancellationToken);
			ShowSymbolMenuForResult(source, new List<ISymbol>(result), " name alike", true);
		}

		List<CommandItem> GetExpandSelectionCommands(CommandContext ctx) {
			var r = new List<CommandItem>();
			var duplicate = ctx.RightClick;
			var node = _Context.NodeIncludeTrivia;
			while (node != null) {
				if (node.FullSpan.Contains(ctx.View.Selection, false)
					&& (node.IsSyntaxBlock() || node.IsDeclaration() || node.IsKind(SyntaxKind.VariableDeclarator))
					&& node.IsKind(SyntaxKind.VariableDeclaration) == false) {
					var n = node;
					r.Add(new CommandItem(CodeAnalysisHelper.GetImageId(n), (duplicate ? "Duplicate " : "Select ") + n.GetSyntaxBrief() + " " + n.GetDeclarationSignature(), ctx2 => {
						ctx2.View.SelectNode(n, Keyboard.Modifiers == ModifierKeys.Shift ^ Config.Instance.SmartBarOptions.MatchFlags(SmartBarOptions.ExpansionIncludeTrivia) || n.Span.Contains(ctx2.View.Selection, false) == false);
						if (Keyboard.Modifiers == ModifierKeys.Control) {
							TextEditorHelper.ExecuteEditorCommand("Edit.Copy");
						}
						if (duplicate) {
							TextEditorHelper.ExecuteEditorCommand("Edit.Duplicate");
						}
					}));
				}
				node = node.Parent;
			}
			r.Add(new CommandItem(KnownImageIds.SelectAll, "Select All", ctrl => ctrl.ToolTip = "Select all text", ctx2 => TextEditorHelper.ExecuteEditorCommand("Edit.SelectAll")));
			return r;
		}

		async Task<IEnumerable<CommandItem>> GetReferenceCommandsAsync(CommandContext ctx) {
			var r = new List<CommandItem>();
			var symbol = await SymbolFinder.FindSymbolAtPositionAsync(_Context.Document, View.GetCaretPosition(), ctx.CancellationToken);
			if (symbol == null) {
				return r;
			}
			symbol = symbol.GetAliasTarget();
			switch (symbol.Kind) {
				case SymbolKind.Method:
				case SymbolKind.Property:
				case SymbolKind.Event:
					r.Add(new CommandItem(KnownImageIds.ShowCallerGraph, "Find Callers...", s => FindCallers(s, symbol)));
					if (symbol.MayHaveOverride()) {
						r.Add(new CommandItem(KnownImageIds.OverloadBehavior, "Find Overrides...", c => FindOverrides(c, symbol)));
					}
					var st = symbol.ContainingType;
					if (st != null && st.TypeKind == TypeKind.Interface) {
						r.Add(new CommandItem(KnownImageIds.ImplementInterface, "Find Implementations...", c => FindImplementations(c, symbol)));
					}
					if (symbol.Kind != SymbolKind.Event) {
						CreateCommandsForReturnTypeCommand(symbol, r);
					}
					if (symbol.Kind == SymbolKind.Method
						&& (symbol as IMethodSymbol).MethodKind == MethodKind.Constructor
						&& st.SpecialType == SpecialType.None) {
						CreateInstanceCommandsForType(st, r);
					}
					break;
				case SymbolKind.Field:
				case SymbolKind.Local:
				case SymbolKind.Parameter:
					CreateCommandsForReturnTypeCommand(symbol, r);
					break;
				case SymbolKind.NamedType:
					CreateCommandForNamedType(symbol as INamedTypeSymbol, r);
					break;
				case SymbolKind.Namespace:
					r.Add(new CommandItem(KnownImageIds.ListMembers, "Find Members...", s => FindMembers(symbol)));
					break;
			}
			if (_Context.Node.IsDeclaration() && symbol.Kind != SymbolKind.Namespace) {
				r.Add(new CommandItem(KnownImageIds.ShowReferencedElements, "Find Referenced Symbols...", c => FindReferencedSymbols(c, symbol)));
			}
			//r.Add(CreateCommandMenu("Find references...", KnownImageIds.ReferencedDimension, symbol, "No reference found", FindReferences));
			r.Add(new CommandItem(KnownImageIds.ReferencedDimension, "Find All References", _ => TextEditorHelper.ExecuteEditorCommand("Edit.FindAllReferences")));
			r.Add(new CommandItem(KnownImageIds.FindSymbol, "Find Symbol with Name " + symbol.Name + "...", c => FindSymbolWithName(c, symbol)));
			r.Add(new CommandItem(KnownImageIds.ListMembers, "Go to Member", _ => TextEditorHelper.ExecuteEditorCommand("Edit.GoToMember")));
			r.Add(new CommandItem(KnownImageIds.Type, "Go to Type", _ => TextEditorHelper.ExecuteEditorCommand("Edit.GoToType")));
			r.Add(new CommandItem(KnownImageIds.FindSymbol, "Go to Symbol", _ => TextEditorHelper.ExecuteEditorCommand("Edit.GoToSymbol")));
			return r;
		}

		void FindReferencedSymbols(CommandContext context, ISymbol symbol) {
			var m = new SymbolMenu(this);
			var c = 0;
			foreach (var item in _Context.Node.FindReferencingSymbols(_Context.SemanticModel, true)) {
				var member = item.Key;
				var i = m.Menu.Add(member, true);
				if (item.Value > 1) {
					i.Hint = "* " + item.Value.ToString();
				}
				++c;
			}
			m.Title.SetGlyph(ThemeHelper.GetImage(symbol.GetImageId()))
				.Append(symbol.ToDisplayString(WpfHelper.MemberNameFormat), true)
				.Append(" referenced members: ")
				.Append(c.ToString());
			m.Show();
		}

		void CreateCommandForNamedType(INamedTypeSymbol t, List<CommandItem> r) {
			if (t.TypeKind == TypeKind.Class || t.TypeKind == TypeKind.Struct) {
				var ctor = _Context.NodeIncludeTrivia.GetObjectCreationNode();
				if (ctor != null) {
					var symbol = _Context.SemanticModel.GetSymbolOrFirstCandidate(ctor);
					if (symbol != null) {
						r.Add(new CommandItem(KnownImageIds.ShowCallerGraph, "Find Callers...", ctx => FindCallers(ctx, symbol)));
					}
				}
				else if (t.InstanceConstructors.Length > 0) {
					r.Add(new CommandItem(KnownImageIds.ShowCallerGraph, "Find Constructor Callers...", ctx => FindCallers(ctx, t)));
				}
			}
			r.Add(new CommandItem(KnownImageIds.ListMembers, "Find Members...", ctx => FindMembers(t)));
			if (t.IsStatic) {
				return;
			}
			if (t.IsSealed == false) {
				if (t.TypeKind == TypeKind.Class) {
					r.Add(new CommandItem(KnownImageIds.NewClass, "Find Derived Classes...", c => FindDerivedClasses(c, t)));
				}
				else if (t.TypeKind == TypeKind.Interface) {
					r.Add(new CommandItem(KnownImageIds.ImplementInterface, "Find Implementations...", c => FindImplementations(c, t)));
				}
			}
			r.Add(new CommandItem(KnownImageIds.ExtensionMethod, "Find Extensions...", ctx => FindExtensionMethods(ctx, t)));
			if (t.SpecialType == SpecialType.None) {
				CreateInstanceCommandsForType(t, r);
			}
		}

		void CreateInstanceCommandsForType(INamedTypeSymbol t, List<CommandItem> r) {
			r.Add(new CommandItem(KnownImageIds.NewItem, "Find Instance Producer...", ctx => FindInstanceProducer(ctx, t)));
			r.Add(new CommandItem(KnownImageIds.Parameter, "Find Instance as Parameter...", ctx => FindInstanceAsParameter(ctx, t)));
		}

		void CreateCommandsForReturnTypeCommand(ISymbol symbol, List<CommandItem> list) {
			var type = symbol.GetReturnType();
			if (type != null && type.SpecialType == SpecialType.None) {
				list.Add(new CommandItem(KnownImageIds.ListMembers, "Find Members of " + type.Name + type.GetParameterString() + "...", s => FindMembers(type)));
				if (type.IsStatic == false) {
					list.Add(new CommandItem(KnownImageIds.ExtensionMethod, "Find Extensions for " + type.Name + type.GetParameterString() + "...", ctx => FindExtensionMethods(ctx, type)));
				}
				if (type.ContainingAssembly.GetSourceType() != AssemblySource.Metadata) {
					list.Add(new CommandItem(KnownImageIds.GoToDeclaration, "Go to " + type.Name + type.GetParameterString(), _ => type.GoToSource()));
				}
			}
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
			return ThreadHelper.JoinableTaskFactory.Run(() => _Context.UpdateAsync(View.Selection.Start.Position, default));
		}
		sealed class SymbolCallerInfoComparer : IEqualityComparer<SymbolCallerInfo>
		{
			internal static readonly SymbolCallerInfoComparer Instance = new SymbolCallerInfoComparer();

			public bool Equals(SymbolCallerInfo x, SymbolCallerInfo y) {
				return x.CallingSymbol == y.CallingSymbol;
			}

			public int GetHashCode(SymbolCallerInfo obj) {
				return obj.CallingSymbol.GetHashCode();
			}
		}

		sealed class SymbolMenu
		{
			readonly CSharpSmartBar _Bar;

			public SymbolList Menu { get; }
			public ThemedMenuText Title { get; }
			public MemberFilterBox FilterBox { get; }

			public SymbolMenu(CSharpSmartBar bar) : this(bar, SymbolListType.None) { }
			public SymbolMenu(CSharpSmartBar bar, SymbolListType listType) {
				Menu = new SymbolList(bar._Context) {
					Container = bar._SymbolListContainer,
					ContainerType = listType
				};
				Menu.Header = new StackPanel {
					Margin = WpfHelper.MenuItemMargin,
					Children = {
						(Title = new ThemedMenuText { TextAlignment = TextAlignment.Center, Padding = WpfHelper.SmallMargin }),
						(FilterBox = new MemberFilterBox(Menu)),
						new Separator()
					}
				};
				_Bar = bar;
			}
			public void Show() {
				SetupSymbolListMenu(Menu);
				ShowMenu(Menu);
				UpdateNumbers();
				FilterBox.FocusTextBox();
			}
			void UpdateNumbers() {
				FilterBox.UpdateNumbers(Menu.Symbols.Select(i => i.Symbol));
			}

			void MenuItemSelect(object sender, MouseButtonEventArgs e) {
				var menu = sender as SymbolList;
				if (menu.SelectedIndex == -1 || (e.OriginalSource as DependencyObject)?.GetParent<ListBoxItem>() == null) {
					return;
				}
				_Bar.View.VisualElement.Focus();
				(menu.SelectedItem as SymbolItem)?.GoToSource();
			}

			void SetupSymbolListMenu(SymbolList list) {
				list.ReferenceCrispImageBackground(EnvironmentColors.MainWindowActiveCaptionColorKey);
				list.MouseLeftButtonUp += MenuItemSelect;
				if (list.Symbols.Count > 100) {
					ScrollViewer.SetCanContentScroll(list, true);
				}
			}

			void ShowMenu(SymbolList menu) {
				if (_Bar._SymbolList != menu) {
					_Bar._SymbolListContainer.Children.Clear();
					_Bar._SymbolListContainer.Add(menu);
					_Bar._SymbolList = menu;
				}
				menu.ItemsControlMaxHeight = _Bar.View.ViewportHeight / 2;
				menu.RefreshItemsSource();
				menu.ScrollToSelectedItem();
				menu.PreviewKeyUp -= OnMenuKeyUp;
				menu.PreviewKeyUp += OnMenuKeyUp;

				var p = Mouse.GetPosition(_Bar._SymbolListContainer);
				Canvas.SetLeft(menu, p.X);
				Canvas.SetTop(menu, p.Y);
				//var point = visual.TransformToVisual(View.VisualElement).Transform(new Point());
				//Canvas.SetLeft(menu, point.X + visual.RenderSize.Width);
				//Canvas.SetTop(menu, point.Y);
			}
			void OnMenuKeyUp(object sender, KeyEventArgs e) {
				if (e.Key == Key.Escape) {
					_Bar.HideMenu();
					e.Handled = true;
				}
			}
		}
	}
}
