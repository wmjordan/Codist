using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using AppHelpers;
using System.Windows.Input;
using Microsoft.CodeAnalysis.FindSymbols;
using System.Windows;

namespace Codist.SmartBars
{
	//todo Make commands async and cancellable
	/// <summary>
	/// An extended <see cref="SmartBar"/> for C# content type.
	/// </summary>
	sealed class CSharpSmartBar : SmartBar {
		CompilationUnitSyntax _Compilation;
		Document _Document;
		SyntaxNode _Node;
		SemanticModel _SemanticModel;
		SyntaxToken _Token;
		SyntaxTrivia _Trivia;
		public CSharpSmartBar(IWpfTextView view) : base(view) {
		}

		ToolBar MyToolBar => ToolBar2;

		protected override void AddCommands() {
			if (UpdateSemanticModel()) {
				AddContextualCommands();
			}
			//MyToolBar.Items.Add(new Separator());
			base.AddCommands();
		}

		static CommandItem CreateCommandMenu(string title, int imageId, ISymbol symbol, string emptyMenuTitle, Action<MenuItem, ISymbol> itemPopulator) {
			return new CommandItem(title, imageId, ctrl => (ctrl as MenuItem).StaysOpenOnClick = true, ctx => {
				var menuItem = ctx.Sender as MenuItem;
				if (menuItem.Items.Count > 0) {
					return;
				}
				ctx.KeepToolbarOnClick = true;
				itemPopulator(menuItem, symbol);
				if (menuItem.Items.Count == 0) {
					menuItem.Items.Add(new MenuItem { Header = emptyMenuTitle, IsEnabled = false });
				}
				else {
					CreateItemsFilter(menuItem);
				}
				menuItem.IsSubmenuOpen = true;
			});
		}

		static void CreateItemsFilter(MenuItem menuItem) {
			TextBox filterBox;
			menuItem.Items.Insert(0, new MenuItem {
				Icon = ThemeHelper.GetImage(KnownImageIds.Filter),
				Header = filterBox = new TextBox {
					Width = 150,
					HorizontalAlignment = HorizontalAlignment.Stretch,
					BorderThickness = new Thickness(0, 0, 0, 1),
				},
				StaysOpenOnClick = true
			});
			filterBox.TextChanged += (s, args) => {
				var t = (s as TextBox).Text;
				foreach (MenuItem item in menuItem.Items) {
					var b = item.Header as TextBlock;
					if (b == null) {
						continue;
					}
					if (b.GetText().IndexOf(t, StringComparison.OrdinalIgnoreCase) == -1) {
						if (item.HasItems) {
							foreach (MenuItem sub in item.Items) {
								b = sub.Header as TextBlock;
								if (b == null) {
									continue;
								}
								if (b.GetText().IndexOf(t, StringComparison.OrdinalIgnoreCase) != -1) {
									item.Visibility = Visibility.Visible;
									goto NEXT;
								}
							}
						}
						item.Visibility = Visibility.Collapsed;
					}
					else {
						item.Visibility = Visibility.Visible;
					}
					NEXT:;
				}
			};
		}

		void AddCommand(int moniker, string tooltip, Action<ITextEdit> editCommand) {
			AddCommand(MyToolBar, moniker, tooltip, ctx => {
				// updates the semantic model before executing the command,
				// for it could be modified by external editor commands or duplicated document windows
				if (UpdateSemanticModel()) {
					using (var edit = ctx.View.TextSnapshot.TextBuffer.CreateEdit()) {
						editCommand(edit);
						if (edit.HasEffectiveChanges) {
							edit.Apply();
						}
					}
				}
			});
		}

		void AddContextualCommands() {
			if (_Node == null) {
				return;
			}

			// anti-pattern for a small margin of performance
			bool isDesignMode = CodistPackage.DebuggerStatus == DebuggerStatus.Design;
			if (isDesignMode && _Node is XmlTextSyntax) {
				AddCommand(KnownImageIds.MarkupTag, "Tag XML Doc with <c>", edit => {
					foreach (var item in View.Selection.SelectedSpans) {
						edit.Replace(item, "<c>" + item.GetText() + "</c>");
					}
				});
				AddCommand(KnownImageIds.GoToNext, "Tag XML Doc with <see>", edit => {
					foreach (var item in View.Selection.SelectedSpans) {
						var t = item.GetText();
						edit.Replace(item, (SyntaxFacts.GetKeywordKind(t) != SyntaxKind.None ? "<see langword=\"" : "<see cref=\"") + t + "\"/>");
					}
				});
				AddCommand(KnownImageIds.ParagraphHardReturn, "Tag XML Doc with <para>", edit => {
					foreach (var item in View.Selection.SelectedSpans) {
						edit.Replace(item, "<para>" + item.GetText() + "</para>");
					}
				});
			}
			else if (_Trivia.RawKind == 0) {
				if (_Token.Span.Contains(View.Selection, true)
					&& _Token.Kind() == SyntaxKind.IdentifierToken
					&& (_Node is TypeSyntax || _Node is MemberDeclarationSyntax || _Node is VariableDeclaratorSyntax || _Node is ParameterSyntax)) {
					// selection is within a symbol
					if (_Node is IdentifierNameSyntax) {
						AddEditorCommand(MyToolBar, KnownImageIds.GoToDefinition, "Edit.GoToDefinition", "Go to definition");
					}
					AddCommands(MyToolBar, KnownImageIds.ReferencedDimension, "Find references", GetReferenceCommands);

					if (isDesignMode) {
						AddCommand(MyToolBar, KnownImageIds.Rename, "Rename symbol", ctx => {
							TextEditorHelper.ExecuteEditorCommand("Refactor.Rename");
							ctx.KeepToolbarOnClick = true;
						});
						if (_Node is ParameterSyntax && _Node.Parent is ParameterListSyntax) {
							AddEditorCommand(MyToolBar, KnownImageIds.ReorderParameters, "Refactor.ReorderParameters", "Reorder parameters");
						}
					}
				}
				else if (_Token.RawKind >= (int)SyntaxKind.NumericLiteralToken && _Token.RawKind <= (int)SyntaxKind.StringLiteralToken) {
					AddEditorCommand(MyToolBar, KnownImageIds.ReferencedDimension, "Edit.FindAllReferences", "Find all references");
				}
				if (isDesignMode) {
					if (_Node.IsKind(SyntaxKind.VariableDeclarator)) {
						if (_Node?.Parent?.Parent is MemberDeclarationSyntax) {
							AddCommand(MyToolBar, KnownImageIds.AddComment, "Insert comment", ctx => {
								TextEditorHelper.ExecuteEditorCommand("Edit.InsertComment");
								ctx.View.Selection.Clear();
							});
						}
					}
					else if (_Node.IsDeclaration()) {
						if (_Node is TypeDeclarationSyntax || _Node is MemberDeclarationSyntax || _Node is ParameterListSyntax) {
							AddCommand(MyToolBar, KnownImageIds.AddComment, "Insert comment", ctx => {
								TextEditorHelper.ExecuteEditorCommand("Edit.InsertComment");
								ctx.View.Selection.Clear();
							});
						}
					}
					else {
						AddEditorCommand(MyToolBar, KnownImageIds.ExtractMethod, "Refactor.ExtractMethod", "Extract Method");
					}
				}
			}
			if (CodistPackage.DebuggerStatus != DebuggerStatus.Running) {
				if (_Trivia.IsLineComment()) {
					AddEditorCommand(MyToolBar, KnownImageIds.UncommentCode, "Edit.UncommentSelection", "Uncomment selection");
				}
				else {
					AddCommand(MyToolBar, KnownImageIds.CommentCode, "Comment selection\nRight click: Comment line", ctx => {
						if (ctx.RightClick) {
							ctx.View.ExpandSelectionToLine();
						}
						TextEditorHelper.ExecuteEditorCommand("Edit.CommentSelection");
					});
				}
			}
			if (isDesignMode == false) {
				AddCommands(MyToolBar, KnownImageIds.BreakpointEnabled, "Debugger", GetDebugCommands);
			}
			AddCommands(MyToolBar, KnownImageIds.SelectFrame, "Expand selection\nRight click: Duplicate\nCtrl click item: Copy\nShift click item: Exclude whitespaces and comments", GetExpandSelectionCommands);
		}

		void FindCallers(MenuItem menuItem, ISymbol source) {
			var callers = SymbolFinder.FindCallersAsync(source, View.TextBuffer.GetWorkspace().CurrentSolution).Result.ToArray();
			Array.Sort(callers, (a, b) => {
				var s = a.CallingSymbol.ContainingType.Name.CompareTo(b.CallingSymbol.ContainingType.Name);
				return s != 0 ? s : a.CallingSymbol.Name.CompareTo(b.CallingSymbol.Name);
			});
			if (callers.Length < 10) {
				foreach (var caller in callers) {
					var s = caller.CallingSymbol;
					menuItem.Items.Add(new SymbolMenuItem(this, s, caller.Locations) {
						Header = new TextBlock().Append(s.ContainingType.Name + ".", System.Windows.Media.Brushes.Gray).Append(s.Name)
					});
				}
			}
			else {
				SymbolMenuItem subMenu = null;
				INamedTypeSymbol typeSymbol = null;
				foreach (var caller in callers) {
					var s = caller.CallingSymbol;
					if (typeSymbol == null || typeSymbol != s.ContainingType) {
						typeSymbol = s.ContainingType;
						subMenu = new SymbolMenuItem(this, typeSymbol, null);
						menuItem.Items.Add(subMenu);
					}
					subMenu.Items.Add(new SymbolMenuItem(this, s, caller.Locations));
				}
			}
		}

		void FindDerivedClasses(MenuItem menuItem, ISymbol symbol) {
			var classes = SymbolFinder.FindDerivedClassesAsync(symbol as INamedTypeSymbol, View.TextBuffer.GetWorkspace().CurrentSolution).Result.ToArray();
			Array.Sort(classes, (a, b) => a.Name.CompareTo(b.Name));
			foreach (var derived in classes) {
				var item = new SymbolMenuItem(this, derived, derived.Locations);
				if (derived.GetSourceLocations().Length == 0) {
					(item.Header as TextBlock).Foreground = System.Windows.Media.Brushes.Gray;
				}
				menuItem.Items.Add(item);
			}
		}

		void FindImplementations(MenuItem menuItem, ISymbol symbol) {
			if (symbol.Kind == SymbolKind.NamedType) {
				foreach (var impl in SymbolFinder.FindImplementationsAsync(symbol, View.TextBuffer.GetWorkspace().CurrentSolution).Result) {
					menuItem.Items.Add(new SymbolMenuItem(this, impl, impl.Locations));
				}
			}
			else {
				foreach (var impl in SymbolFinder.FindImplementationsAsync(symbol, View.TextBuffer.GetWorkspace().CurrentSolution).Result) {
					menuItem.Items.Add(new SymbolMenuItem(this, impl.ContainingSymbol, impl.Locations));
				}
			}
		}

		void FindInstanceAsParameter(MenuItem menuItem, ISymbol source) {
			var members = (source as ITypeSymbol).FindInstanceAsParameter(_Document.Project);
			SortAndGroupSymbolByClass(menuItem, members);
		}

		void FindInstanceProducer(MenuItem menuItem, ISymbol source) {
			var members = (source as ITypeSymbol).FindSymbolInstanceProducer(_Document.Project);
			SortAndGroupSymbolByClass(menuItem, members);
		}

		void FindMembers(MenuItem menuItem, ISymbol symbol) {
			var members = (symbol as INamedTypeSymbol).GetMembers().RemoveAll(m => m.CanBeReferencedByName == false).Sort(Comparer<ISymbol>.Create((a, b) => {
				int s;
				if ((s = b.DeclaredAccessibility - a.DeclaredAccessibility) != 0 // sort by visibility first
					|| (s = a.Kind - b.Kind) != 0) { // then by member kind
					return s;
				}
				return a.Name.CompareTo(b.Name);
			}));
			foreach (var item in members) {
				menuItem.Items.Add(new SymbolMenuItem(this, item, item.Locations));
			}
		}

		void FindOverrides(MenuItem menuItem, ISymbol symbol) {
			foreach (var ov in SymbolFinder.FindOverridesAsync(symbol, View.TextBuffer.GetWorkspace().CurrentSolution).Result) {
				menuItem.Items.Add(new SymbolMenuItem(this, ov, ov.ContainingType.Name, ov.Locations));
			}
		}

		void FindReferences(MenuItem menuItem, ISymbol source) {
			var refs = SymbolFinder.FindReferencesAsync(source, View.TextBuffer.GetWorkspace().CurrentSolution).Result.ToArray();
			Array.Sort(refs, (a, b) => {
				int s;
				return 0 != (s = a.Definition.ContainingType.Name.CompareTo(b.Definition.ContainingType.Name)) ? s :
					0 != (s = b.Definition.DeclaredAccessibility - a.Definition.DeclaredAccessibility) ? s
					: a.Definition.Name.CompareTo(b.Definition.Name);
			});
			if (refs.Length < 10) {
				foreach (var item in refs) {
					menuItem.Items.Add(new SymbolMenuItem(this, item.Definition, item.Definition.ContainingType?.Name + "." + item.Definition.Name, null));
				}
			}
			else {
				SymbolMenuItem subMenu = null;
				INamedTypeSymbol typeSymbol = null;
				foreach (var item in refs) {
					if (typeSymbol == null || typeSymbol != item.Definition.ContainingType) {
						typeSymbol = item.Definition.ContainingType;
						subMenu = new SymbolMenuItem(this, typeSymbol, null);
						menuItem.Items.Add(subMenu);
					}
					subMenu.Items.Add(new SymbolMenuItem(this, item.Definition, null));
				}
			}
		}

		void FindSimilarSymbols(MenuItem menuItem, ISymbol symbol) {
			foreach (var project in View.TextBuffer.GetWorkspace().CurrentSolution.Projects) {
				foreach (var ss in SymbolFinder.FindSimilarSymbols(symbol, project.GetCompilationAsync().Result)) {
					menuItem.Items.Add(new SymbolMenuItem(this, ss, ss.Locations));
				}
			}
		}

		CommandItem[] GetDebugCommands(CommandContext ctx) {
			return new CommandItem[] {
				new CommandItem("Toggle breakpoint", KnownImageIds.BreakpointEnabled, c => c.ToolTip = "Toggle breakpoint", c => TextEditorHelper.ExecuteEditorCommand("Debug.ToggleBreakpoint")),
				new CommandItem("Delete breakpoints", KnownImageIds.DeleteBreakpoint, c => c.ToolTip = "Delete all breakpoints", c => TextEditorHelper.ExecuteEditorCommand("Debug.DeleteAllBreakpoints"))
			};
		}

		List<CommandItem> GetExpandSelectionCommands(CommandContext ctx) {
			var r = new List<CommandItem>();
			var duplicate = ctx.RightClick;
			var node = _Node;
			while (node != null) {
				if (node.FullSpan.Contains(ctx.View.Selection, false)
					&& (node.IsSyntaxBlock() || node.IsDeclaration())) {
					var n = node;
					r.Add(new CommandItem((duplicate ? "Duplicate " : "Select ") + n.GetSyntaxBrief() + " " + n.GetDeclarationSignature(), CodeAnalysisHelper.GetImageId(n), null, ctx2 => {
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
			r.Add(new CommandItem("Select all", KnownImageIds.SelectAll, ctrl => ctrl.ToolTip = "Select all text", ctx2 => TextEditorHelper.ExecuteEditorCommand("Edit.SelectAll")));
			return r;
		}

		List<CommandItem> GetReferenceCommands(CommandContext ctx) {
			var r = new List<CommandItem>();
			var symbol = SymbolFinder.FindSymbolAtPositionAsync(_Document, View.Caret.Position.BufferPosition).Result;
			if (symbol == null) {
				return r;
			}

			switch (symbol.Kind) {
				case SymbolKind.Method:
					if ((symbol as IMethodSymbol).MethodKind == MethodKind.Constructor) {
						r.Add(CreateCommandMenu("Find members...", KnownImageIds.ListMembers, symbol.ContainingType, "No member was found", FindMembers));
					}
					goto case SymbolKind.Property;
				case SymbolKind.Property:
				case SymbolKind.Event:
					r.Add(CreateCommandMenu("Find callers...", KnownImageIds.ShowCallerGraph, symbol, "No caller was found", FindCallers));
					if (symbol.MayHaveOverride()) {
						r.Add(CreateCommandMenu("Find overrides...", KnownImageIds.OverloadBehavior, symbol, "No override was found", FindOverrides));
					}
					var st = symbol.ContainingType as INamedTypeSymbol;
					if (st != null && st.TypeKind == TypeKind.Interface) {
						r.Add(CreateCommandMenu("Find implementations...", KnownImageIds.ImplementInterface, symbol, "No implementation was found", FindImplementations));
					}
					//r.Add(CreateCommandMenu("Find similar...", KnownImageIds.DropShadow, symbol, "No similar symbol was found", FindSimilarSymbols));
					break;
				case SymbolKind.Field:
				case SymbolKind.Local:
					break;
				case SymbolKind.NamedType:
					var t = symbol as INamedTypeSymbol;
					if (t.TypeKind == TypeKind.Class || t.TypeKind == TypeKind.Struct) {
						var ctor = _Node.GetObjectCreationNode();
						if (ctor != null) {
							var s = _SemanticModel.GetSymbolOrFirstCandidate(ctor);
							if (s != null) {
								r.Add(CreateCommandMenu("Find callers...", KnownImageIds.ShowCallerGraph, s, "No caller was found", FindCallers));
							}
						}
					}
					r.Add(CreateCommandMenu("Find members...", KnownImageIds.ListMembers, t, "No member was found", FindMembers));
					if (t.IsStatic || t.SpecialType != SpecialType.None) {
						break;
					}
					r.Add(CreateCommandMenu("Find instance producer...", KnownImageIds.NewItem, t, "No instance creator was found", FindInstanceProducer));
					r.Add(CreateCommandMenu("Find instance as parameter...", KnownImageIds.Parameter, t, "No instance as parameter was found", FindInstanceAsParameter));
					if (t.IsSealed) {
						break;
					}
					if (t.TypeKind == TypeKind.Class) {
						r.Add(CreateCommandMenu("Find derived classes...", KnownImageIds.NewClass, symbol, "No derived class was found", FindDerivedClasses));
					}
					else if (t.TypeKind == TypeKind.Interface) {
						r.Add(CreateCommandMenu("Find implementations...", KnownImageIds.ImplementInterface, symbol, "No implementation was found", FindImplementations));
					}
					break;
			}
			//r.Add(CreateCommandMenu("Find references...", KnownImageIds.ReferencedDimension, symbol, "No reference found", FindReferences));
			r.Add(new CommandItem("Find all references", KnownImageIds.ReferencedDimension, null, _ => TextEditorHelper.ExecuteEditorCommand("Edit.FindAllReferences")));
			return r;
		}
		void SortAndGroupSymbolByClass(MenuItem menuItem, List<ISymbol> members) {
			members.Sort((a, b) => {
				var s = a.ContainingType.Name.CompareTo(b.ContainingType.Name);
				return s != 0 ? s : a.Name.CompareTo(b.Name);
			});
			if (members.Count < 10) {
				foreach (var member in members) {
					menuItem.Items.Add(new SymbolMenuItem(this, member, member.Locations) {
						Header = new TextBlock().Append(member.ContainingType.Name + ".", System.Windows.Media.Brushes.Gray).Append(member.Name)
					});
				}
			}
			else {
				SymbolMenuItem subMenu = null;
				INamedTypeSymbol typeSymbol = null;
				foreach (var member in members) {
					if (typeSymbol == null || typeSymbol != member.ContainingType) {
						typeSymbol = member.ContainingType;
						subMenu = new SymbolMenuItem(this, typeSymbol, null);
						menuItem.Items.Add(subMenu);
					}
					subMenu.Items.Add(new SymbolMenuItem(this, member, member.Locations));
				}
			}
		}

		bool UpdateSemanticModel() {
			_Document = View.TextSnapshot.GetOpenDocumentInCurrentContextWithChanges();
			_SemanticModel = _Document.GetSemanticModelAsync().Result;
			_Compilation = _SemanticModel.SyntaxTree.GetCompilationUnitRoot();
			int pos = View.Selection.Start.Position;
			try {
				_Token = _Compilation.FindToken(pos, true);
			}
			catch (ArgumentOutOfRangeException) {
				_Node = null;
				_Token = default(SyntaxToken);
				_Trivia = default(SyntaxTrivia);
				return false;
			}
			_Trivia = _Token.HasLeadingTrivia && _Token.LeadingTrivia.Span.Contains(pos) ? _Token.LeadingTrivia.FirstOrDefault(i => i.Span.Contains(pos))
			   : _Token.HasTrailingTrivia && _Token.TrailingTrivia.Span.Contains(pos) ? _Token.TrailingTrivia.FirstOrDefault(i => i.Span.Contains(pos))
			   : default(SyntaxTrivia);
			_Node = _Compilation.FindNode(_Token.Span, true, true);
			return true;
		}

		sealed class SymbolMenuItem : CommandMenuItem
		{
			static readonly SymbolFormatter __Formatter = new SymbolFormatter();
			static readonly SymbolDisplayFormat __MemberNameFormat = new SymbolDisplayFormat(
				typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
				parameterOptions: SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeOptionalBrackets,
				genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
				miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

			public SymbolMenuItem(SmartBar bar, ISymbol symbol, IEnumerable<Location> locations) : this(bar, symbol, symbol.ToDisplayString(__MemberNameFormat), locations) { }
			public SymbolMenuItem(SmartBar bar, ISymbol symbol, string alias, IEnumerable<Location> locations) : base(bar, new CommandItem(symbol, alias)) {
				Locations = locations;
				Symbol = symbol;
				//todo compatible with symbols having more than 1 locations
				if (locations != null && locations.Any(l => l.SourceTree?.FilePath != null)) {
					Click += GotoLocation;
				}
				if (Symbol != null) {
					ToolTip = "";
					ToolTipOpening += ShowToolTip;
				}
			}
			public IEnumerable<Location> Locations { get; }
			public ISymbol Symbol { get; }
			void GotoLocation(object sender, RoutedEventArgs args) {
				var loc = Locations.FirstOrDefault();
				if (loc != null) {
					var p = loc.GetLineSpan();
					CodistPackage.DTE.OpenFile(loc.SourceTree.FilePath, p.StartLinePosition.Line + 1, p.StartLinePosition.Character + 1);
				}
			}
			void ShowToolTip(object sender, ToolTipEventArgs args) {
				var tip = new TextBlock()
					.Append(Symbol.GetAccessibility() + Symbol.GetAbstractionModifier() + Symbol.GetSymbolKindName() + " ")
					.Append(Symbol.GetSignatureString(), true);
				ITypeSymbol t = Symbol.ContainingType;
				if (t != null) {
					tip.Append("\n" + t.GetSymbolKindName() + ": ")
						.Append(t.ToDisplayString(__MemberNameFormat));
				}
				t = Symbol.GetReturnType();
				if (t != null) {
					tip.Append("\nreturn value: " + t.ToDisplayString(__MemberNameFormat));
				}
				tip.Append("\nnamespace: " + Symbol.ContainingNamespace?.ToString())
					.Append("\nassembly: " + Symbol.GetAssemblyModuleName());
				var f = Symbol as IFieldSymbol;
				if (f != null && f.IsConst) {
					tip.Append("\nconst: " + f.ConstantValue.ToString());
				}
				var doc = Symbol.GetXmlDocForSymbol();
				if (doc != null) {
					new XmlDocRenderer((SmartBar as CSharpSmartBar)._SemanticModel.Compilation, __Formatter).Render(doc, tip.Append("\n\n").Inlines);
					tip.MaxWidth = Config.Instance.QuickInfoMaxWidth;
				}
				tip.TextWrapping = TextWrapping.Wrap;
				ToolTip = tip;
				ToolTipOpening -= ShowToolTip;
			}
		}
	}
}
