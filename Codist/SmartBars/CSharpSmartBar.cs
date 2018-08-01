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

		public CSharpSmartBar(IWpfTextView view) : base(view, 16) {
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
			if (CodistPackage.DebuggerStatus == DebuggerStatus.Design && _Node is XmlTextSyntax) {
				AddCommand(KnownMonikers.MarkupTag, "Tag XML Doc with <c>", edit => {
					foreach (var item in View.Selection.SelectedSpans) {
						edit.Replace(item, "<c>" + item.GetText() + "</c>");
					}
				});
				AddCommand(KnownMonikers.GoToNext, "Tag XML Doc with <see>", edit => {
					foreach (var item in View.Selection.SelectedSpans) {
						var t = item.GetText();
						edit.Replace(item, (SyntaxFacts.GetKeywordKind(t) != SyntaxKind.None ? "<see langword=\"" : "<see cref=\"") + t + "\"/>");
					}
				});
				AddCommand(KnownMonikers.ParagraphHardReturn, "Tag XML Doc with <para>", edit => {
					foreach (var item in View.Selection.SelectedSpans) {
						edit.Replace(item, "<para>" + item.GetText() + "</para>");
					}
				});
			}
			else if (_Trivia.RawKind == 0) {
				if (_Token.Span.Contains(View.Selection, true)
					&& (_Node is TypeSyntax || _Node is MemberDeclarationSyntax || _Node is VariableDeclaratorSyntax || _Node is ParameterSyntax)
					&& _Token.Kind() == SyntaxKind.IdentifierToken) {
					if (_Node is IdentifierNameSyntax) {
						AddEditorCommand(MyToolBar, KnownMonikers.GoToDefinition, "Edit.GoToDefinition", "Go to definition");
					}
					AddEditorCommand(MyToolBar, KnownMonikers.ReferencedDimension, "Edit.FindAllReferences", "Find all references");
					if (CodistPackage.DebuggerStatus == DebuggerStatus.Design) {
						AddEditorCommand(MyToolBar, KnownMonikers.Rename, "Refactor.Rename", "Rename symbol");
						if (_Node is ParameterSyntax && _Node.Parent is ParameterListSyntax) {
							AddEditorCommand(MyToolBar, KnownMonikers.ReorderParameters, "Refactor.ReorderParameters", "Reorder parameters");
						}
					}
				}
				else if (_Token.Kind() == SyntaxKind.StringLiteralToken) {
					AddEditorCommand(MyToolBar, KnownMonikers.ReferencedDimension, "Edit.FindAllReferences", "Find all references");
				}
				if (CodistPackage.DebuggerStatus == DebuggerStatus.Design) {
					if (_Node.IsDeclaration()) {
						if (_Node is TypeDeclarationSyntax || _Node is MemberDeclarationSyntax || _Node is ParameterListSyntax) {
							AddEditorCommand(MyToolBar, KnownMonikers.AddComment, "Edit.InsertComment", "Insert comment");
						}
					}
					else {
						AddEditorCommand(MyToolBar, KnownMonikers.ExtractMethod, "Refactor.ExtractMethod", "Extract Method");
					}
				}
			}
			if (CodistPackage.DebuggerStatus == DebuggerStatus.Design) {
				if (_Trivia.IsLineComment()) {
					AddEditorCommand(MyToolBar, KnownMonikers.UncommentCode, "Edit.UncommentSelection", "Uncomment selection");
				}
				else {
					AddCommand(MyToolBar, KnownMonikers.CommentCode, "Comment selection\nRight click: Comment line", ctx => {
						if (ctx.RightClick) {
							View.ExpandSelectionToLine();
						}
						TextEditorHelper.ExecuteEditorCommand("Edit.CommentSelection");
					});
				}
			}
			AddCommands(MyToolBar, KnownMonikers.SelectFrame, "Expand selection\nRight click: Duplicate\nCtrl click item: Copy", (ctx) => {
				var r = new List<CommandItem>();
				var duplicate = ctx.RightClick;
				var node = _Node;
				while (node != null) {
					if (node.FullSpan.Contains(View.Selection, false)
						&& (node.IsSyntaxBlock() || node.IsDeclaration())) {
						var n = node;
						r.Add(new CommandItem((duplicate ? "Duplicate " : "Select ") + n.GetSyntaxBrief() + " " + n.GetDeclarationSignature(), CodeAnalysisHelper.GetSyntaxMoniker(n), null, _ => {
							View.SelectNode(n, Keyboard.Modifiers == ModifierKeys.Shift ^ Config.Instance.SmartBarOptions.MatchFlags(SmartBarOptions.ExpansionIncludeTrivia) || n.Span.Contains(View.Selection, false) == false);
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
				return r;
			});
		}

		void AddCommand(ImageMoniker moniker, string tooltip, Action<ITextEdit> editCommand) {
			AddCommand(MyToolBar, moniker, tooltip, (ctx) => {
				if (UpdateSemanticModel()) {
					using (var edit = View.TextSnapshot.TextBuffer.CreateEdit()) {
						editCommand(edit);
						if (edit.HasEffectiveChanges) {
							edit.Apply();
						}
					}
				}
			});
		}

		bool UpdateSemanticModel() {
			var workspace = View.TextBuffer.GetWorkspace();
			_SemanticModel = workspace.GetDocument(View.Selection.SelectedSpans[0]).GetSemanticModelAsync().Result;
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
