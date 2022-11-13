using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using AppHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Codist.Refactorings
{
	abstract class ReplaceNodes
	{
		public static readonly ReplaceNodes DeleteCondition = new DeleteConditionRefactoring();
		public static readonly ReplaceNodes RemoveContainingStatement = new RemoveContainerRefactoring();
		public static readonly ReplaceNodes SwapOperands = new SwapOperandsRefactoring();
		public static readonly ReplaceNodes NestCondition = new NestConditionRefactoring();
		public static readonly ReplaceNodes MergeCondition = new MergeConditionRefactoring();

		public abstract bool AcceptNode(SyntaxNode node);
		public abstract void Refactor(SemanticContext ctx);

		static void Replace(SemanticContext context, SyntaxNode oldNode, IEnumerable<SyntaxNode> newNodes) {
			var view = context.View;
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
			newNodes = root.Format(ann, context.Workspace)
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

		sealed class DeleteConditionRefactoring : ReplaceNodes
		{
			public override bool AcceptNode(SyntaxNode node) {
				return node.IsKind(SyntaxKind.IfStatement) && node.Parent.IsKind(SyntaxKind.ElseClause) == false;
			}

			public override void Refactor(SemanticContext ctx) {
				var ifs = ((IfStatementSyntax)ctx.Node).Statement;
				Replace(ctx, ctx.Node, ifs is BlockSyntax b ? b.Statements : new SyntaxList<StatementSyntax>(ifs));
			}
		}

		sealed class RemoveContainerRefactoring : ReplaceNodes
		{
			public override bool AcceptNode(SyntaxNode node) {
				node = node.Parent.FirstAncestorOrSelf<BlockSyntax>()?.Parent;
				return node != null && CanBeRemoved(node);
			}

			static bool CanBeRemoved(SyntaxNode node) {
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

			public override void Refactor(SemanticContext ctx) {
				StatementSyntax s = ctx.Node.Parent.FirstAncestorOrSelf<BlockSyntax>();
				if (s == null) {
					return;
				}
				if (s.Parent.IsKind(SyntaxKind.ElseClause)) {
					var oldIf = s.FirstAncestorOrSelf<ElseClauseSyntax>().Parent.FirstAncestorOrSelf<IfStatementSyntax>();
					var newIf = oldIf.WithElse(null);
					var targets = ((BlockSyntax)s).Statements;
					Replace(ctx, oldIf, targets.Insert(0, newIf));
				}
				else {
					var targets = s is BlockSyntax b ? b.Statements : (IEnumerable<SyntaxNode>)ctx.Compilation.GetStatements(ctx.View.FirstSelectionSpan().ToTextSpan());
					while (s.IsKind(SyntaxKind.Block)) {
						s = s.Parent.FirstAncestorOrSelf<StatementSyntax>();
					}
					if (s != null && targets != null) {
						Replace(ctx, s, targets);
					}
				}
			}
		}

		sealed class SwapOperandsRefactoring : ReplaceNodes
		{
			public override bool AcceptNode(SyntaxNode node) {
				switch (node.Kind()) {
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

			public override void Refactor(SemanticContext ctx) {
				var node = ctx.NodeIncludeTrivia as BinaryExpressionSyntax;
				ExpressionSyntax right = node.Right, left = node.Left;
				if (left == null || right == null) {
					return;
				}

				#region Swap operands besides selected operator
				if (Keyboard.Modifiers.MatchFlags(ModifierKeys.Shift) == false) {
					BinaryExpressionSyntax temp;
					if ((temp = left as BinaryExpressionSyntax) != null
						&& temp.RawKind == node.RawKind
						&& temp.Right != null) {
						left = temp.Right;
						right = temp.Update(temp.Left, temp.OperatorToken, right);
					}
					else if ((temp = right as BinaryExpressionSyntax) != null
						&& temp.RawKind == node.RawKind
						&& temp.Left != null) {
						left = temp.Update(left, temp.OperatorToken, temp.Right);
						right = temp.Left;
					}
				}
				#endregion

				var newNode = node.Update(right.WithTrailingTrivia(left.GetTrailingTrivia()),
					node.OperatorToken,
					right.HasTrailingTrivia && right.GetTrailingTrivia().Last().IsKind(SyntaxKind.EndOfLineTrivia)
						? left.WithLeadingTrivia(right.GetLeadingTrivia())
						: left.WithoutTrailingTrivia());
				Replace(ctx, node, new[] { newNode });
			}
		}

		sealed class NestConditionRefactoring : ReplaceNodes
		{
			public override bool AcceptNode(SyntaxNode node) {
				return GetParentConditionalStatement(node) != null;
			}

			public override void Refactor(SemanticContext ctx) {
				var node = ctx.NodeIncludeTrivia as BinaryExpressionSyntax;
				var s = GetParentConditionalStatement(node);
				if (s == null) {
					return;
				}
				ExpressionSyntax right = node.Right, left = node.Left;
				while ((node = node.Parent as BinaryExpressionSyntax) != null) {
					right = node.Update(right, node.OperatorToken, node.Right);
				}

				if (s is IfStatementSyntax ifs) {
					var newIf = ifs.WithCondition(left.WithoutTrailingTrivia())
						.WithStatement(SF.Block(SF.IfStatement(right, ifs.Statement)).Format(ctx.View.TextBuffer.GetWorkspace()));
					Replace(ctx, ifs, new[] { newIf });
				}
				else if (s is WhileStatementSyntax ws) {
					var newWhile = ws.WithCondition(left.WithoutTrailingTrivia())
						.WithStatement(SF.Block(SF.IfStatement(right, ws.Statement)).Format(ctx.View.TextBuffer.GetWorkspace()));
					Replace(ctx, ws, new[] { newWhile });
				}
			}

			static StatementSyntax GetParentConditionalStatement(SyntaxNode node) {
				while (node.IsKind(SyntaxKind.LogicalAndExpression)) {
					node = node.Parent;
					if (node is IfStatementSyntax ifs) {
						return ifs;
					}
				}
				return null;
			}
		}

		sealed class MergeConditionRefactoring : ReplaceNodes
		{
			public override bool AcceptNode(SyntaxNode node) {
				return GetParentConditionalStatement(node) != null;
			}

			public override void Refactor(SemanticContext ctx) {
				var ifs = ctx.Node as IfStatementSyntax;
				var s = GetParentConditionalStatement(ctx.Node);
				if (s == null) {
					return;
				}
				if (ifs.Statement is BlockSyntax b) {
					b = SF.Block(b.Statements);
				}
				else {
					b = SF.Block(ifs.Statement);
				}

				if (s is IfStatementSyntax newIf) {
					newIf = newIf.WithCondition(SF.BinaryExpression(SyntaxKind.LogicalAndExpression, newIf.Condition, ifs.Condition))
						.WithStatement(b);
					Replace(ctx, s, new[] { newIf });
				}
				else if (s is WhileStatementSyntax newWhile) {
					newWhile = newWhile.WithCondition(SF.BinaryExpression(SyntaxKind.LogicalAndExpression, newWhile.Condition, ifs.Condition))
						.WithStatement(b);
					Replace(ctx, s, new[] { newWhile });
				}
			}

			static StatementSyntax GetParentConditionalStatement(SyntaxNode node) {
				var ifs = node as IfStatementSyntax;
				if (ifs == null || ifs.Else != null) {
					return null;
				}
				node = node.Parent;
				if (node.IsKind(SyntaxKind.Block)) {
					var block = (BlockSyntax)node;
					if (block.Statements.Count > 1) {
						return null;
					}
					node = node.Parent;
				}
				if (node.IsKind(SyntaxKind.IfStatement) || node.IsKind(SyntaxKind.WhileStatement)) {
					return node as StatementSyntax;
				}
				return null;
			}
		}
	}
}
