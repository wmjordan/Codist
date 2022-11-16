using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using AppHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using R = Codist.Properties.Resources;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Codist.Refactorings
{
	abstract class ReplaceNodes : IRefactoring<SyntaxNode>
	{
		public static readonly ReplaceNodes DeleteCondition = new DeleteConditionRefactoring();
		public static readonly ReplaceNodes RemoveContainingStatement = new RemoveContainerRefactoring();
		public static readonly ReplaceNodes SwapOperands = new SwapOperandsRefactoring();
		public static readonly ReplaceNodes NestCondition = new NestConditionRefactoring();
		public static readonly ReplaceNodes MergeCondition = new MergeConditionRefactoring();
		public static readonly ReplaceNodes ChangeIfToConditional = new ChangeIfToConditionalRefactoring();
		public static readonly ReplaceNodes ChangeConditionalToIf = new ChangeConditionalToIfRefactoring();
		public static readonly ReplaceNodes MultiLineConditional = new MultilineConditionalRefactoring();

		public abstract int IconId { get; }
		public abstract string Title { get; }

		public abstract bool Accept(SyntaxNode node);
		public abstract void Refactor(SemanticContext ctx);

		static void Replace(SemanticContext context, SyntaxNode oldNode, SyntaxNode newNode) {
			Replace(context, oldNode, new[] { newNode });
		}
		static void Replace(SemanticContext context, SyntaxNode oldNode, IEnumerable<SyntaxNode> newNodes) {
			var view = context.View;
			var start = view.Selection.StreamSelectionSpan.Start.Position;
			var ann = new SyntaxAnnotation();
			List<SyntaxNode> nodes = new List<SyntaxNode>();
			foreach (var item in newNodes) {
				nodes.Add(item.WithAdditionalAnnotations(ann));
			}
			var root = oldNode.SyntaxTree.GetRoot();
			root = (nodes.Count > 1
					? root.ReplaceNode(oldNode, nodes)
					: root.ReplaceNode(oldNode, nodes[0]))
				.Format(CodeFormatHelper.Reformat, context.Workspace);
			newNodes = root.GetAnnotatedNodes(ann);
			var select = root.GetAnnotatedNodes(CodeFormatHelper.Select).FirstOrDefault();
			view.Edit(
				(rep: String.Concat(newNodes.Select(i => i.ToFullString())), sel: oldNode.FullSpan.ToSpan()),
				(v, p, edit) => edit.Replace(p.sel, p.rep)
			);
			view.Caret.MoveTo(new SnapshotPoint(view.TextSnapshot, newNodes.First().SpanStart));
			if (select != null) {
				view.Selection.Select(new SnapshotSpan(view.TextSnapshot, select.Span.ToSpan()), false);
			}
		}

		sealed class DeleteConditionRefactoring : ReplaceNodes
		{
			public override int IconId => IconIds.DeleteCondition;
			public override string Title => R.CMD_DeleteCondition;

			public override bool Accept(SyntaxNode node) {
				return node.IsKind(SyntaxKind.IfStatement) && node.Parent.IsKind(SyntaxKind.ElseClause) == false;
			}

			public override void Refactor(SemanticContext ctx) {
				var ifs = ((IfStatementSyntax)ctx.Node).Statement;
				Replace(ctx,
					ctx.Node,
					ifs is BlockSyntax b
						? b.Statements.AttachAnnotation(CodeFormatHelper.Reformat, CodeFormatHelper.Select)
						: new SyntaxList<StatementSyntax>(ifs.AnnotateReformatAndSelect())
					);
			}
		}

		sealed class RemoveContainerRefactoring : ReplaceNodes
		{
			public override int IconId => IconIds.Delete;
			public override string Title => R.CMD_DeleteContainingBlock;

			public override bool Accept(SyntaxNode node) {
				var s = node.GetContainingStatement();
				return s != null && s.SpanStart == node.SpanStart && GetRemovableAncestor(s) != null;
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
					case SyntaxKind.IfStatement:
						return true;
					case SyntaxKind.ElseClause:
						return ((ElseClauseSyntax)node).Statement?.Kind() != SyntaxKind.IfStatement;
				}
				return false;
			}

			public override void Refactor(SemanticContext ctx) {
				var statement = ctx.Node.GetContainingStatement();
				var remove = GetRemovableAncestor(statement);
				if (remove == null) {
					return;
				}
				SyntaxList<StatementSyntax> keep;
				if (statement.Parent is BlockSyntax b) {
					keep = b.Statements;
				}
				else {
					keep = new SyntaxList<StatementSyntax>(statement);
				}
				if (remove.IsKind(SyntaxKind.ElseClause)) {
					var ifs = remove.Parent as IfStatementSyntax;
					if (ifs.Parent.IsKind(SyntaxKind.ElseClause)) {
						Replace(ctx, ifs.Parent, (keep.Count > 1 || statement.Parent.IsKind(SyntaxKind.Block) ? SF.ElseClause(SF.Block(keep)) : SF.ElseClause(keep[0])).AnnotateReformatAndSelect());
						return;
					}
					else {
						keep = keep.Insert(0, ifs.WithElse(null));
					}
					remove = ifs;
				}
				keep = keep.AttachAnnotation(CodeFormatHelper.Reformat, CodeFormatHelper.Select);
				Replace(ctx, remove, keep);
			}

			static SyntaxNode GetRemovableAncestor(SyntaxNode node) {
				if (node == null) {
					return null;
				}
				do {
					if (CanBeRemoved(node = node.Parent)) {
						return node;
					}
				} while (node is StatementSyntax);
				return null;
			}
		}

		sealed class SwapOperandsRefactoring : ReplaceNodes
		{
			public override int IconId => IconIds.SwapOperands;
			public override string Title => R.CMD_SwapOperands;

			public override bool Accept(SyntaxNode node) {
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
				Replace(ctx, node, newNode.AnnotateReformatAndSelect());
			}
		}

		sealed class NestConditionRefactoring : ReplaceNodes
		{
			public override int IconId => IconIds.NestCondition;
			public override string Title => R.CMD_SplitToNested;

			public override bool Accept(SyntaxNode node) {
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
					Replace(ctx, ifs, new[] { newIf.AnnotateReformatAndSelect() });
				}
				else if (s is WhileStatementSyntax ws) {
					var newWhile = ws.WithCondition(left.WithoutTrailingTrivia())
						.WithStatement(SF.Block(SF.IfStatement(right, ws.Statement)).Format(ctx.View.TextBuffer.GetWorkspace()));
					Replace(ctx, ws, new[] { newWhile.AnnotateReformatAndSelect() });
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
			string _NodeKind;

			public override int IconId => IconIds.MergeCondition;
			public override string Title => R.CMD_MergeWithParent.Replace("NODE", _NodeKind);

			public override bool Accept(SyntaxNode node) {
				if (GetParentConditionalStatement(node) != null) {
					_NodeKind = node.IsKind(SyntaxKind.IfStatement) ? "if" : "while";
					return true;
				}
				return false;
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
					Replace(ctx, s, new[] { newIf.AnnotateReformatAndSelect() });
				}
				else if (s is WhileStatementSyntax newWhile) {
					newWhile = newWhile.WithCondition(SF.BinaryExpression(SyntaxKind.LogicalAndExpression, newWhile.Condition, ifs.Condition))
						.WithStatement(b);
					Replace(ctx, s, new[] { newWhile.AnnotateReformatAndSelect() });
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
				return node.IsKind(SyntaxKind.IfStatement) || node.IsKind(SyntaxKind.WhileStatement) ? node as StatementSyntax : null;
			}
		}

		sealed class ChangeIfToConditionalRefactoring : ReplaceNodes
		{
			public override int IconId => IconIds.MergeCondition;
			public override string Title => R.CMD_IfElseToConditional;

			public override bool Accept(SyntaxNode node) {
				return GetConditionalStatement(node).ifStatement != null;
			}

			public override void Refactor(SemanticContext ctx) {
				var (ifStatement, statement, elseStatement) = GetConditionalStatement(ctx.Node);
				if (ifStatement == null) {
					return;
				}
				SyntaxNode newNode;
				switch (statement.Kind()) {
					case SyntaxKind.ReturnStatement:
						newNode = SF.ReturnStatement(
							SF.ConditionalExpression(ifStatement.Condition.WithLeadingTrivia(SF.Space),
								(statement as ReturnStatementSyntax).Expression,
								(elseStatement as ReturnStatementSyntax).Expression)
							);
						break;
					case SyntaxKind.ExpressionStatement:
						var assignee = ((AssignmentExpressionSyntax)((ExpressionStatementSyntax)statement).Expression).Left;
						newNode = SF.ExpressionStatement(
							SF.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
								assignee,
								SF.ConditionalExpression(ifStatement.Condition,
									((AssignmentExpressionSyntax)((ExpressionStatementSyntax)statement).Expression).Right,
									((AssignmentExpressionSyntax)((ExpressionStatementSyntax)elseStatement).Expression).Right))
							);
						break;
					case SyntaxKind.YieldReturnStatement:
						newNode = SF.YieldStatement(SyntaxKind.YieldReturnStatement,
							SF.ConditionalExpression(ifStatement.Condition,
								(statement as YieldStatementSyntax).Expression,
								(elseStatement as YieldStatementSyntax).Expression));
						break;
					default:
						return;
				}
				Replace(ctx, ifStatement, newNode.AnnotateReformatAndSelect());
			}

			static (IfStatementSyntax ifStatement, StatementSyntax statement, StatementSyntax elseStatement) GetConditionalStatement(SyntaxNode node) {
				StatementSyntax ss, es;
				SyntaxKind k;
				if (node is IfStatementSyntax ifs
					&& ifs.Else != null
					&& (ss = ifs.Statement) != null
					&& (ss = GetSingleStatement(ss)) != null
					&& (es = ifs.Else.Statement) != null
					&& (es = GetSingleStatement(es)) != null
					&& es.IsKind(k = ss.Kind())
					&& (k == SyntaxKind.ReturnStatement
						|| k == SyntaxKind.YieldReturnStatement
						|| (k == SyntaxKind.ExpressionStatement
							&& ss is ExpressionStatementSyntax e
							&& e.Expression is AssignmentExpressionSyntax a
							&& es is ExpressionStatementSyntax ee
							&& ee.Expression is AssignmentExpressionSyntax ea
							&& a.Left.ToString() == ea.Left.ToString()))) {
					return (ifs, ss, es);
				}
				return default;
			}

			static StatementSyntax GetSingleStatement(StatementSyntax statement) {
				return statement is BlockSyntax b
					? (b.Statements.Count == 1 ? b.Statements[0] : null)
					: statement;
			}
		}

		sealed class ChangeConditionalToIfRefactoring : ReplaceNodes
		{
			public override int IconId => IconIds.SplitCondition;
			public override string Title => R.CMD_ConditionalToIfElse;

			public override bool Accept(SyntaxNode node) {
				return node.IsKind(SyntaxKind.ConditionalExpression) && node.Parent is StatementSyntax;
			}

			public override void Refactor(SemanticContext ctx) {
				var node = ctx.NodeIncludeTrivia as ConditionalExpressionSyntax;
				SyntaxNode newNode;
				StatementSyntax whenTrue, whenFalse;
				if (node.Parent is ReturnStatementSyntax r) {
					whenTrue = SF.ReturnStatement(node.WhenTrue);
					whenFalse = SF.ReturnStatement(node.WhenFalse);
				}
				else if (node.Parent is ExpressionStatementSyntax es
					&& es.Expression is AssignmentExpressionSyntax a
					&& a.IsKind(SyntaxKind.SimpleAssignmentExpression)) {
					whenTrue = SF.ExpressionStatement(SF.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, a.Left, node.WhenTrue));
					whenFalse = SF.ExpressionStatement(SF.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, a.Left, node.WhenFalse));
				}
				else if (node.Parent is YieldStatementSyntax) {
					whenTrue = SF.YieldStatement(SyntaxKind.YieldReturnStatement, node.WhenTrue);
					whenFalse = SF.YieldStatement(SyntaxKind.YieldReturnStatement, node.WhenFalse);
				}
				else {
					return;
				}
				newNode = SF.IfStatement(node.Condition.WithoutTrailingTrivia(),
					SF.Block(whenTrue),
					SF.ElseClause(SF.Block(whenFalse))
					);
				Replace(ctx, node.Parent, newNode.AnnotateReformatAndSelect());
			}
		}

		sealed class MultilineConditionalRefactoring : ReplaceNodes
		{
			public override int IconId => IconIds.MultiLine;
			public override string Title => R.CMD_ConditionalOnMultiLines;

			public override bool Accept(SyntaxNode node) {
				return node.IsKind(SyntaxKind.ConditionalExpression)
					&& node.IsMultiLine(false) == false;
			}

			public override void Refactor(SemanticContext ctx) {
				var conditional = ctx.NodeIncludeTrivia as ConditionalExpressionSyntax;
				if (conditional == null) {
					return;
				}
				// it is frustrating that the C# formatter removes trivias in ?: expressions
				var options = ctx.Workspace.Options;
				var indent = conditional.GetContainingStatement()
					?.GetLeadingTrivia()
					.Where(t => t.IsKind(SyntaxKind.WhitespaceTrivia))
					.Concat(new SyntaxTriviaList(SF.Whitespace(options.GetIndentString())))
					.ToSyntaxTriviaList();
				var newLine = SF.Whitespace(options.GetNewLineString());
				var newNode = conditional.Update(conditional.Condition.WithTrailingTrivia(newLine),
					conditional.QuestionToken.WithLeadingTrivia(indent).WithTrailingTrivia(SF.Space),
					conditional.WhenTrue.WithTrailingTrivia(newLine),
					conditional.ColonToken.WithLeadingTrivia(indent).WithTrailingTrivia(SF.Space),
					conditional.WhenFalse);
				Replace(ctx, conditional, newNode.AnnotateSelect());
			}
		}
	}
}
