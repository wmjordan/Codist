using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Codist.Controls;

namespace Codist.Refactorings
{
	abstract partial class ReplaceNode : IRefactoring
	{
		public abstract int IconId { get; }
		public abstract string Title { get; }

		public abstract bool Accept(RefactoringContext ctx);

		public abstract IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx);

		public static RefactoringAction Remove(SyntaxNode delete) {
			return new RefactoringAction(ActionType.Remove, new List<SyntaxNode> { delete }, default);
		}
		public static RefactoringAction Remove(IEnumerable<SyntaxNode> delete) {
			return new RefactoringAction(ActionType.Remove, new List<SyntaxNode>(delete), default);
		}
		public static RefactoringAction Replace(IEnumerable<SyntaxNode> delete, SyntaxNode insert) {
			return new RefactoringAction(ActionType.Replace, new List<SyntaxNode>(delete), new List<SyntaxNode> { insert });
		}
		public static RefactoringAction Replace(IEnumerable<SyntaxNode> inserts) {
			return new RefactoringAction(ActionType.Replace, default, new List<SyntaxNode>(inserts));
		}
		public static RefactoringAction Replace(SyntaxNode oldNode, SyntaxNode insert) {
			return new RefactoringAction(ActionType.Replace, new List<SyntaxNode> { oldNode }, new List<SyntaxNode> { insert });
		}
		public static RefactoringAction Replace(SyntaxNode oldNode, IEnumerable<SyntaxNode> insert) {
			return new RefactoringAction(ActionType.Replace, new List<SyntaxNode> { oldNode }, new List<SyntaxNode> (insert));
		}
		public static RefactoringAction InsertBefore(SyntaxNode oldNode, SyntaxNode insert) {
			return new RefactoringAction(ActionType.InsertBefore, new List<SyntaxNode> { oldNode }, new List<SyntaxNode> { insert });
		}
		public static RefactoringAction InsertAfter(SyntaxNode oldNode, SyntaxNode insert) {
			return new RefactoringAction(ActionType.InsertAfter, new List<SyntaxNode> { oldNode }, new List<SyntaxNode> { insert });
		}
		public static RefactoringAction InsertAfter(SyntaxNode oldNode, IEnumerable<SyntaxNode> insert) {
			return new RefactoringAction(ActionType.InsertAfter, new List<SyntaxNode> { oldNode }, new List<SyntaxNode> (insert));
		}

		public void Refactor(SemanticContext context) {
			try {
				RefactorInternal(context);
			}
			catch (Exception ex) when (System.Diagnostics.Debugger.IsAttached == false) {
				MessageWindow.Error(ex, Title, null, this);
			}
		}

		private void RefactorInternal(SemanticContext context) {
			var ctx = new RefactoringContext(context) {
				Refactoring = this
			};
			context.View.Edit(
				ctx,
				(v, p, edit) => {
					var root = p.SemanticContext.SemanticModel.SyntaxTree.GetRoot();
					var r = (ReplaceNode)p.Refactoring;
					if (r.Accept(p) == false) {
						return;
					}
					var actions = p.Actions = r.Refactor(p).ToArray();
					if (actions.Length == 0) {
						return;
					}
					root = actions.Length > 1
						? ChangeDocumentWithActions(p.SemanticContext, actions)
						: ChangeDocumentWithAction(root, actions[0]);
					p.NewRoot = root = root.Format(CodeFormatHelper.Reformat, p.SemanticContext.Workspace);
					foreach (var action in actions) {
						switch (action.ActionType) {
							case ActionType.Replace:
								edit.Replace(action.OriginalSpan.ToSpan(), action.Insert.Count > 0 ? action.GetInsertionString(root) : String.Empty);
								break;
							case ActionType.InsertBefore:
								edit.Insert(action.FirstOriginal.FullSpan.Start, action.GetInsertionString(root));
								break;
							case ActionType.InsertAfter:
								edit.Insert(action.FirstOriginal.FullSpan.End, action.GetInsertionString(root));
								break;
							case ActionType.Remove:
								edit.Delete(action.OriginalSpan.ToSpan());
								continue;
						}
					}
				}
			);
			var ms = context.View.GetMultiSelectionBroker();
			var ss = ms.CurrentSnapshot;
			using (var b = ms.BeginBatchOperation()) {
				foreach (var action in ctx.Actions) {
					if (action.ActionType == ActionType.Remove) {
						continue;
					}
					var inserted = ctx.NewRoot.GetAnnotatedNodes(action.Annotation).FirstOrDefault();
					if (inserted == null || inserted.ContainsAnnotations == false) {
						continue;
					}
					var s = action.FirstOriginal.FullSpan.Start;
					var c = new Chain<TextSpan>();
					foreach (var item in inserted.GetAnnotatedNodesAndTokens(CodeFormatHelper.Select)) {
						c.Add(item.Span);
					}
					foreach (var item in inserted.GetAnnotatedTrivia(CodeFormatHelper.Select)) {
						c.Add(item.Span);
					}
					ms.AddSelectionRange(c.Select(i => new Selection(new SnapshotSpan(ss, i.ToSpan()))));
				}
				ms.TryRemoveSelection(ms.PrimarySelection);
			}
			context.View.Caret.EnsureVisible();
		}

		static SyntaxNode ChangeDocumentWithActions(SemanticContext context, RefactoringAction[] actions) {
			var editor = new SyntaxEditor(context.Compilation, context.Workspace);
			foreach (var action in actions) {
				switch (action.ActionType) {
					case ActionType.Replace:
						if (action.Original.Count == 1) {
							if (action.Insert.Count > 1) {
								ReplaceNodes(editor, action.FirstOriginal, action.Insert);
							}
							else {
								editor.ReplaceNode(action.FirstOriginal, action.Insert[0]);
							}
						}
						else {
							editor.InsertBefore(action.FirstOriginal, action.Insert);
							RemoveNodes(editor, action.Original);
						}
						break;
					case ActionType.InsertBefore:
						editor.InsertBefore(action.FirstOriginal, action.Insert);
						break;
					case ActionType.InsertAfter:
						editor.InsertAfter(action.FirstOriginal, action.Insert);
						break;
					case ActionType.Remove:
						RemoveNodes(editor, action.Original);
						break;
				}
			}
			return editor.GetChangedRoot();
		}

		static SyntaxNode ChangeDocumentWithAction(SyntaxNode root, RefactoringAction action) {
			switch (action.ActionType) {
				case ActionType.Replace:
					return action.Insert.Count == 1
						? root.ReplaceNode(action.FirstOriginal, action.Insert[0])
						: root.ReplaceNode(action.FirstOriginal, action.Insert);
					// no need to remove old nodes since we won't use them later
					// root.RemoveNodes(action.Original, SyntaxRemoveOptions.KeepNoTrivia);
				case ActionType.InsertBefore:
					return root.InsertNodesBefore(action.FirstOriginal, action.Insert);
				case ActionType.InsertAfter:
					return root.InsertNodesAfter(action.FirstOriginal, action.Insert);
				case ActionType.Remove:
					return root.RemoveNodes(action.Original, SyntaxRemoveOptions.KeepNoTrivia);
			}
			return root;
		}

		static void RemoveNodes(SyntaxEditor editor, IEnumerable<SyntaxNode> nodes) {
			foreach (var item in nodes) {
				editor.RemoveNode(item);
			}
		}

		static void ReplaceNodes(SyntaxEditor editor, SyntaxNode original, IList<SyntaxNode> nodes) {
			editor.InsertAfter(original, nodes);
			editor.RemoveNode(original);
		}
	}
}
