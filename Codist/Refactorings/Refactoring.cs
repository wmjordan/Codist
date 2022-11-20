using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Codist.Refactorings
{
	abstract partial class ReplaceNode : IRefactoring
	{
		public abstract int IconId { get; }
		public abstract string Title { get; }

		public virtual bool Accept(RefactoringContext ctx) {
			return ctx.SelectedStatementInfo.Statements != null;
		}

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

		public void Refactor(SemanticContext context) {
			var ctx = new RefactoringContext(context) {
				Refactoring = this
			};
			context.View.Edit(
				ctx,
				(v, p, edit) => {
					var root = p.SemanticContext.SemanticModel.SyntaxTree.GetRoot();
					var actions = p.Actions = ((ReplaceNode)p.Refactoring).Refactor(p).ToArray();
					foreach (var action in actions) {
						switch (action.ActionType) {
							case ActionType.Replace:
								root = action.Original.Count == 1
									? action.Insert.Count > 1 // bug in Roslyn requires this workaround
										? root.ReplaceNode(action.FirstOriginal, action.Insert)
										: root.ReplaceNode(action.FirstOriginal, action.Insert[0])
									: root.InsertNodesBefore(action.FirstOriginal, action.Insert)
										.RemoveNodes(action.Original, SyntaxRemoveOptions.KeepNoTrivia);
								break;
							case ActionType.InsertBefore:
								root = root.InsertNodesBefore(action.FirstOriginal, action.Insert);
								break;
							case ActionType.InsertAfter:
								root = root.InsertNodesAfter(action.FirstOriginal, action.Insert);
								break;
							case ActionType.Remove:
								root = root.RemoveNodes(action.Original, SyntaxRemoveOptions.KeepNoTrivia);
								break;
						}
					}
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
			foreach (var action in ctx.Actions) {
				if (action.ActionType == ActionType.Remove) {
					continue;
				}
				var inserted = ctx.NewRoot.GetAnnotatedNodes(action.Annotation).FirstOrDefault();
				if (inserted != null) {
					var selSpan = inserted.GetAnnotatedNodes(CodeFormatHelper.Select).FirstOrDefault()?.Span ?? default;
					if (selSpan.Length != 0) {
						context.View.SelectSpan(action.FirstOriginal.FullSpan.Start + (selSpan.Start - inserted.FullSpan.Start), selSpan.Length, 1);
						return;
					}
				}
			}
		}
	}
}
