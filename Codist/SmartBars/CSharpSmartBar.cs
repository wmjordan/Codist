using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using AppHelpers;
using System.Windows.Input;
using Microsoft.CodeAnalysis.FindSymbols;

namespace Codist.SmartBars
{
	/// <summary>
	/// An extended <see cref="SmartBar"/> for C# content type.
	/// </summary>
	sealed class CSharpSmartBar : SmartBar {
		SemanticModel _SemanticModel;
		CompilationUnitSyntax _Compilation;
		SyntaxToken _Token;
		SyntaxTrivia _Trivia;
		SyntaxNode _Node;

		public CSharpSmartBar(IWpfTextView view) : base(view) {
		}

		ToolBar MyToolBar => ToolBar2;

		protected override void AddCommands() {
			UpdateSemanticModel();
			AddCommandsForNode();
			//MyToolBar.Items.Add(new Separator());
			base.AddCommands();
		}

		void AddCommandsForNode() {
			if (_Node == null) {
				return;
			}
			// anti-pattern for a small margin of performance
			if (CodistPackage.DebuggerStatus == DebuggerStatus.Design && _Node is XmlTextSyntax) {
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
					if (_Node is IdentifierNameSyntax) {
						AddEditorCommand(MyToolBar, KnownImageIds.GoToDefinition, "Edit.GoToDefinition", "Go to definition");
					}
					AddCommands(MyToolBar, KnownImageIds.ReferencedDimension, "Find references", GetReferenceCommands);

					if (CodistPackage.DebuggerStatus == DebuggerStatus.Design) {
						AddEditorCommand(MyToolBar, KnownImageIds.Rename, "Refactor.Rename", "Rename symbol");
						if (_Node is ParameterSyntax && _Node.Parent is ParameterListSyntax) {
							AddEditorCommand(MyToolBar, KnownImageIds.ReorderParameters, "Refactor.ReorderParameters", "Reorder parameters");
						}
					}
				}
				else if (_Token.RawKind >= (int)SyntaxKind.StringLiteralToken && _Token.RawKind <= (int)SyntaxKind.NumericLiteralToken) {
					AddEditorCommand(MyToolBar, KnownImageIds.ReferencedDimension, "Edit.FindAllReferences", "Find all references");
				}
				if (CodistPackage.DebuggerStatus == DebuggerStatus.Design) {
					if (_Node.IsDeclaration()) {
						if (_Node is TypeDeclarationSyntax || _Node is MemberDeclarationSyntax || _Node is ParameterListSyntax) {
							AddEditorCommand(MyToolBar, KnownImageIds.AddComment, "Edit.InsertComment", "Insert comment");
						}
					}
					else {
						AddEditorCommand(MyToolBar, KnownImageIds.ExtractMethod, "Refactor.ExtractMethod", "Extract Method");
					}
				}
			}
			if (CodistPackage.DebuggerStatus == DebuggerStatus.Design) {
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
			AddCommands(MyToolBar, KnownImageIds.SelectFrame, "Expand selection\nRight click: Duplicate\nCtrl click item: Copy\nShift click item: Exclude whitespaces and comments", GetExpandSelectionCommands);
		}

		void AddCommand(int moniker, string tooltip, Action<ITextEdit> editCommand) {
			AddCommand(MyToolBar, moniker, tooltip, ctx => {
				// before executing the command, updates the semantic model,
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

		List<CommandItem> GetReferenceCommands(CommandContext ctx) {
			var r = new List<CommandItem>();
			var node = _Node;
			var symbol = _SemanticModel.GetSymbolInfo(node).Symbol ?? _SemanticModel.GetDeclaredSymbol(_Node);
			if (symbol == null) {
				return r;
			}

			switch (symbol.Kind) {
				case SymbolKind.Method:
					r.Add(CreateFindCallersCommand(symbol));
					if (symbol.MayHaveOverride()) {
						r.Add(CreateFindOverridesCommand(symbol));
					}
					break;
				case SymbolKind.Property:
					r.Add(CreateFindCallersCommand(symbol));
					if (symbol.MayHaveOverride()) {
						r.Add(CreateFindOverridesCommand(symbol));
					}
					break;
				case SymbolKind.Event:
					r.Add(CreateFindCallersCommand(symbol));
					if (symbol.MayHaveOverride()) {
						r.Add(CreateFindOverridesCommand(symbol));
					}
					break;
				case SymbolKind.Field:
				case SymbolKind.Local:
					break;
				case SymbolKind.NamedType:
					var t = symbol as INamedTypeSymbol;
					if (t.IsStatic || t.IsSealed) {
						break;
					}
					if (t.TypeKind == TypeKind.Class) {
						r.Add(CreateFindDerivedClassesCommand(symbol));
					}
					else if (t.TypeKind == TypeKind.Interface) {
						r.Add(CreateFindImplementationsCommand(symbol));
					}
					break;
			}
			//r.Add(CreateFindReferencesCommand(symbol));
			r.Add(new CommandItem("Find all references", KnownImageIds.ReferencedDimension, null, _ => TextEditorHelper.ExecuteEditorCommand("Edit.FindAllReferences")));
			return r;
		}

		CommandItem CreateFindCallersCommand(ISymbol symbol) {
			return new CommandItem("Find callers...", KnownImageIds.ShowCallerGraph, ctrl => (ctrl as MenuItem).StaysOpenOnClick = true, ctx => {
				var menuItem = ctx.Sender as MenuItem;
				if (menuItem.Items.Count > 0) {
					return;
				}
				ctx.KeepToolbarOnClick = true;
				var callers = new List<SymbolCallerInfo>(SymbolFinder.FindCallersAsync(symbol, View.TextBuffer.GetWorkspace().CurrentSolution).Result);
				callers.Sort((a, b) => {
					var s = a.CallingSymbol.ContainingType.Name.CompareTo(b.CallingSymbol.ContainingType.Name);
					return s != 0 ? s : a.CallingSymbol.Name.CompareTo(b.CallingSymbol.Name);
				});
				if (callers.Count < 10) {
					foreach (var caller in callers) {
						var s = caller.CallingSymbol;
						var item = ToMenuItem(new CommandItem(s, s.Name, caller.Locations));
						item.Header = new TextBlock().AddText(s.ContainingType.Name + ".", System.Windows.Media.Brushes.Gray).AddText(s.Name);
						item.ToolTip = s.ToDisplayString() + "\nnamespace: " + s.ContainingNamespace?.ToDisplayString();
						menuItem.Items.Add(item);
					}
				}
				else {
					MenuItem container = null;
					INamedTypeSymbol typeSymbol = null;
					foreach (var caller in callers) {
						var s = caller.CallingSymbol;
						if (typeSymbol == null || typeSymbol != s.ContainingType) {
							typeSymbol = s.ContainingType;
							container = new MenuItem {
								Header = new TextBlock() { Text = typeSymbol.Name },
								Icon = new Image { Source = CodistPackage.GetImage(typeSymbol.GetImageId()) },
								ToolTip = typeSymbol.ToDisplayString() + "\nnamespace: " + typeSymbol.ContainingNamespace?.ToDisplayString()
							};
							menuItem.Items.Add(container);
						}
						var item = ToMenuItem(new CommandItem(s, s.Name, caller.Locations));
						item.ToolTip = s.ToDisplayString() + "\nnamespace: " + s.ContainingNamespace?.ToDisplayString();
						container.Items.Add(item);
					}
				}
				if (menuItem.Items.Count == 0) {
					menuItem.Items.Add(new MenuItem { Header = "No caller found", IsEnabled = false });
				}
				menuItem.IsSubmenuOpen = true;
			});
		}

		//todo group references to class and symbol
		CommandItem CreateFindReferencesCommand(ISymbol symbol) {
			return new CommandItem("Find references...", KnownImageIds.ReferencedDimension, ctrl => (ctrl as MenuItem).StaysOpenOnClick = true, ctx => {
				var menuItem = ctx.Sender as MenuItem;
				if (menuItem.Items.Count > 0) {
					return;
				}
				ctx.KeepToolbarOnClick = true;
				var refs = new List<ReferencedSymbol>(SymbolFinder.FindReferencesAsync(symbol, View.TextBuffer.GetWorkspace().CurrentSolution).Result);
				refs.Sort((a, b) => {
					var s = a.Definition.ContainingType.Name.CompareTo(b.Definition.ContainingType.Name);
					return s != 0 ? s : a.Definition.Name.CompareTo(b.Definition.Name);
				});
				if (refs.Count < 10) {
					foreach (var item in refs) {
						menuItem.Items.Add(ToMenuItem(new CommandItem(item.Definition, item.Definition.ContainingType.Name + "." + item.Definition.Name, null)));
					}
				}
				else {
					MenuItem container = null;
					INamedTypeSymbol typeSymbol = null;
					foreach (var item in refs) {
						if (typeSymbol == null || typeSymbol != item.Definition.ContainingType) {
							typeSymbol = item.Definition.ContainingType;
							container = new MenuItem {
								Header = new TextBlock() { Text = typeSymbol.Name },
								Icon = new Image { Source = CodistPackage.GetImage(typeSymbol.GetImageId()) }
							};
							menuItem.Items.Add(container);
						}
						container.Items.Add(ToMenuItem(new CommandItem(item.Definition, item.Definition.Name, null)));
					}
				}
				if (menuItem.Items.Count == 0) {
					menuItem.Items.Add(new MenuItem { Header = "No reference found", IsEnabled = false });
				}
				menuItem.IsSubmenuOpen = true;
			});
		}

		CommandItem CreateFindOverridesCommand(ISymbol symbol) {
			return new CommandItem("Find overrides...", KnownImageIds.OverloadBehavior, ctrl => (ctrl as MenuItem).StaysOpenOnClick = true, ctx => {
				var menuItem = ctx.Sender as MenuItem;
				if (menuItem.Items.Count > 0) {
					return;
				}
				ctx.KeepToolbarOnClick = true;
				foreach (var ov in SymbolFinder.FindOverridesAsync(symbol, View.TextBuffer.GetWorkspace().CurrentSolution).Result) {
					menuItem.Items.Add(ToMenuItem(new CommandItem(ov, ov.ContainingType.Name, ov.Locations)));
				}
				if (menuItem.Items.Count == 0) {
					menuItem.Items.Add(new MenuItem { Header = "No overrider found", IsEnabled = false });
				}
				menuItem.IsSubmenuOpen = true;
			});
		}

		CommandItem CreateFindDerivedClassesCommand(ISymbol symbol) {
			return new CommandItem("Find derived classes...", KnownImageIds.NewClass, ctrl => (ctrl as MenuItem).StaysOpenOnClick = true, ctx => {
				var menuItem = ctx.Sender as MenuItem;
				if (menuItem.Items.Count > 0) {
					return;
				}
				ctx.KeepToolbarOnClick = true;
				foreach (var derived in SymbolFinder.FindDerivedClassesAsync(symbol as INamedTypeSymbol, View.TextBuffer.GetWorkspace().CurrentSolution).Result) {
					var item = ToMenuItem(new CommandItem(derived, derived.Name, derived.Locations));
					item.ToolTip = "namespace: " + derived.ContainingNamespace?.ToDisplayString();
					if (derived.GetSourceLocations().Length == 0) {
						(item.Header as TextBlock).Foreground = System.Windows.Media.Brushes.Gray;
					}
					menuItem.Items.Add(item);
				}
				if (menuItem.Items.Count == 0) {
					menuItem.Items.Add(new MenuItem { Header = "No derived class found", IsEnabled = false });
				}
				menuItem.IsSubmenuOpen = true;
			});
		}

		CommandItem CreateFindImplementationsCommand(ISymbol symbol) {
			return new CommandItem("Find implementations...", KnownImageIds.ImplementInterface, ctrl => (ctrl as MenuItem).StaysOpenOnClick = true, ctx => {
				var menuItem = ctx.Sender as MenuItem;
				if (menuItem.Items.Count > 0) {
					return;
				}
				ctx.KeepToolbarOnClick = true;
				foreach (var impl in SymbolFinder.FindImplementationsAsync(symbol as INamedTypeSymbol, View.TextBuffer.GetWorkspace().CurrentSolution).Result) {
					menuItem.Items.Add(ToMenuItem(new CommandItem(impl, impl.Name, null)));
				}
				if (menuItem.Items.Count == 0) {
					menuItem.Items.Add(new MenuItem { Header = "No implementation found", IsEnabled = false });
				}
				menuItem.IsSubmenuOpen = true;
			});
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

		bool UpdateSemanticModel() {
			Document document = View.TextBuffer.GetWorkspace().GetDocument(View.Selection.SelectedSpans[0]);
			_SemanticModel = document.GetSemanticModelAsync().Result;
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
	}
}
