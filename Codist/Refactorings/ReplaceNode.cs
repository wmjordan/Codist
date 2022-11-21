using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using AppHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using R = Codist.Properties.Resources;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Codist.Refactorings
{
	abstract partial class ReplaceNode
	{
		public static readonly ReplaceNode AddBraces = new AddBracesRefactoring();
		public static readonly ReplaceNode DeleteCondition = new DeleteConditionRefactoring();
		public static readonly ReplaceNode RemoveContainingStatement = new RemoveContainerRefactoring();
		public static readonly ReplaceNode SwapOperands = new SwapOperandsRefactoring();
		public static readonly ReplaceNode NestCondition = new NestConditionRefactoring();
		public static readonly ReplaceNode MergeCondition = new MergeConditionRefactoring();
		public static readonly ReplaceNode ChangeIfToConditional = new ChangeIfToConditionalRefactoring();
		public static readonly ReplaceNode ChangeConditionalToIf = new ChangeConditionalToIfRefactoring();
		public static readonly ReplaceNode MultiLineConditional = new MultiLineConditionalRefactoring();

		sealed class AddBracesRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.AddBraces;
			public override string Title => R.CMD_AddBraces;

			public override bool Accept(RefactoringContext ctx) {
				var node = ctx.NodeIncludeTrivia;
				switch (node.Kind()) {
					case SyntaxKind.IfStatement:
						return ((IfStatementSyntax)node).Statement.IsKind(SyntaxKind.Block) == false;
					case SyntaxKind.ForEachStatement:
						return ((ForEachStatementSyntax)node).Statement.IsKind(SyntaxKind.Block) == false;
					case SyntaxKind.ForStatement:
						return ((ForStatementSyntax)node).Statement.IsKind(SyntaxKind.Block) == false;
					case SyntaxKind.WhileStatement:
						return ((WhileStatementSyntax)node).Statement.IsKind(SyntaxKind.Block) == false;
					case SyntaxKind.UsingStatement:
						return ((UsingStatementSyntax)node).Statement.IsKind(SyntaxKind.Block) == false;
					case SyntaxKind.LockStatement:
						return ((LockStatementSyntax)node).Statement.IsKind(SyntaxKind.Block) == false;
					case SyntaxKind.ElseClause:
						return ((ElseClauseSyntax)node).Statement.IsKind(SyntaxKind.Block) == false;
					case SyntaxKind.FixedStatement:
						return ((FixedStatementSyntax)node).Statement.IsKind(SyntaxKind.Block) == false;
					case SyntaxKind.CaseSwitchLabel:
						node = node.Parent;
						goto case SyntaxKind.SwitchSection;
					case SyntaxKind.SwitchSection:
						var statements = ((SwitchSectionSyntax)node).Statements;
						return statements.Count > 1 || statements[0].IsKind(SyntaxKind.Block) == false;
					default: return false;
				}
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var node = ctx.Node;
				StatementSyntax statement;
				switch (node.Kind()) {
					case SyntaxKind.IfStatement:
						statement = ((IfStatementSyntax)node).Statement; break;
					case SyntaxKind.ForEachStatement:
						statement = ((ForEachStatementSyntax)node).Statement; break;
					case SyntaxKind.ForStatement:
						statement = ((ForStatementSyntax)node).Statement; break;
					case SyntaxKind.WhileStatement:
						statement = ((WhileStatementSyntax)node).Statement; break;
					case SyntaxKind.UsingStatement:
						statement = ((UsingStatementSyntax)node).Statement; break;
					case SyntaxKind.LockStatement:
						statement = ((LockStatementSyntax)node).Statement; break;
					case SyntaxKind.FixedStatement:
						statement = ((FixedStatementSyntax)node).Statement; break;
					case SyntaxKind.ElseClause:
						var oldElse = (ElseClauseSyntax)node;
						var newElse = oldElse.WithStatement(SF.Block(oldElse.Statement)).AnnotateReformatAndSelect();
						yield return Replace(oldElse, newElse);
						yield break;
					case SyntaxKind.CaseSwitchLabel:
						node = node.Parent;
						goto case SyntaxKind.SwitchSection;
					case SyntaxKind.SwitchSection:
						var oldSection = (SwitchSectionSyntax)node;
						var newSection = oldSection.WithStatements(SF.SingletonList((StatementSyntax)SF.Block(oldSection.Statements))).AnnotateReformatAndSelect();
						yield return Replace(oldSection, newSection);
						yield break;
					default: yield break;
				}
				if (statement != null) {
					yield return Replace(statement, SF.Block(statement).AnnotateReformatAndSelect());
				}
			}
		}

		sealed class DeleteConditionRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.DeleteCondition;
			public override string Title => R.CMD_DeleteCondition;

			public override bool Accept(RefactoringContext ctx) {
				var node = ctx.NodeIncludeTrivia;
				return node.IsKind(SyntaxKind.IfStatement) && node.Parent.IsKind(SyntaxKind.ElseClause) == false && (ctx.SelectedStatementInfo.Statements == null || ctx.SelectedStatementInfo.Statements.Count == 1);
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var ifs = ((IfStatementSyntax)ctx.Node).Statement;
				if (ifs is BlockSyntax b) {
					yield return Replace(ctx.Node, b.Statements.AttachAnnotation(CodeFormatHelper.Reformat, CodeFormatHelper.Select));
				}
				else {
					yield return Replace(ctx.Node, ifs.AnnotateReformatAndSelect());
				}
			}
		}

		sealed class RemoveContainerRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.Delete;
			public override string Title => R.CMD_DeleteContainingBlock;

			public override bool Accept(RefactoringContext ctx) {
				var node = ctx.NodeIncludeTrivia;
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

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var statement = ctx.Node.GetContainingStatement();
				var remove = GetRemovableAncestor(statement);
				if (remove == null) {
					yield break;
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
						yield return Replace(ifs.Parent, (keep.Count > 1 || statement.Parent.IsKind(SyntaxKind.Block) ? SF.ElseClause(SF.Block(keep)) : SF.ElseClause(keep[0])).AnnotateReformatAndSelect());
						yield break;
					}
					else {
						keep = keep.Insert(0, ifs.WithElse(null));
					}
					remove = ifs;
				}
				keep = keep.AttachAnnotation(CodeFormatHelper.Reformat, CodeFormatHelper.Select);
				yield return Replace(remove, keep);
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

		sealed class SwapOperandsRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.SwapOperands;
			public override string Title => R.CMD_SwapOperands;

			public override bool Accept(RefactoringContext ctx) {
				switch (ctx.NodeIncludeTrivia.Kind()) {
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

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var node = ctx.NodeIncludeTrivia as BinaryExpressionSyntax;
				ExpressionSyntax right = node.Right, left = node.Left;
				if (left == null || right == null) {
					yield break;
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
				yield return Replace(node, newNode.AnnotateReformatAndSelect());
			}
		}

		sealed class NestConditionRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.NestCondition;
			public override string Title => R.CMD_SplitToNested;

			public override bool Accept(RefactoringContext ctx) {
				return GetParentConditionalStatement(ctx.NodeIncludeTrivia) != null;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var node = ctx.NodeIncludeTrivia as BinaryExpressionSyntax;
				var s = GetParentConditionalStatement(node);
				if (s == null) {
					yield break;
				}
				ExpressionSyntax right = node.Right, left = node.Left;
				while ((node = node.Parent as BinaryExpressionSyntax) != null) {
					right = node.Update(right, node.OperatorToken, node.Right);
				}

				if (s is IfStatementSyntax ifs) {
					var newIf = ifs.WithCondition(left.WithoutTrailingTrivia())
						.WithStatement(SF.Block(SF.IfStatement(right, ifs.Statement)).Format(ctx.SemanticContext.Workspace));
					yield return Replace(ifs, newIf.AnnotateReformatAndSelect());
				}
				else if (s is WhileStatementSyntax ws) {
					var newWhile = ws.WithCondition(left.WithoutTrailingTrivia())
						.WithStatement(SF.Block(SF.IfStatement(right, ws.Statement)).Format(ctx.SemanticContext.Workspace));
					yield return Replace(ws, newWhile.AnnotateReformatAndSelect());
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

		sealed class MergeConditionRefactoring : ReplaceNode
		{
			string _NodeKind;

			public override int IconId => IconIds.MergeCondition;
			public override string Title => R.CMD_MergeWithParent.Replace("NODE", _NodeKind);

			public override bool Accept(RefactoringContext ctx) {
				var node = ctx.NodeIncludeTrivia;
				if (GetParentConditionalStatement(node) != null) {
					_NodeKind = node.IsKind(SyntaxKind.IfStatement) ? "if" : "while";
					return true;
				}
				return false;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var ifs = ctx.Node as IfStatementSyntax;
				var s = GetParentConditionalStatement(ctx.Node);
				if (s == null) {
					yield break;
				}
				if (ifs.Statement is BlockSyntax b) {
					b = SF.Block(b.Statements);
				}
				else {
					b = SF.Block(ifs.Statement);
				}

				if (s is IfStatementSyntax newIf) {
					newIf = newIf.WithCondition(SF.BinaryExpression(SyntaxKind.LogicalAndExpression, ParenthesizeLogicalOrExpression(newIf.Condition), ParenthesizeLogicalOrExpression(ifs.Condition)))
						.WithStatement(b);
					yield return Replace(s, newIf.AnnotateReformatAndSelect());
				}
				else if (s is WhileStatementSyntax newWhile) {
					newWhile = newWhile.WithCondition(SF.BinaryExpression(SyntaxKind.LogicalAndExpression, ParenthesizeLogicalOrExpression(newWhile.Condition), ParenthesizeLogicalOrExpression(ifs.Condition)))
						.WithStatement(b);
					yield return Replace(s, newWhile.AnnotateReformatAndSelect());
				}
			}

			static ExpressionSyntax ParenthesizeLogicalOrExpression(ExpressionSyntax expression) {
				return expression is BinaryExpressionSyntax b && b.IsKind(SyntaxKind.LogicalOrExpression)
					? SF.ParenthesizedExpression(expression)
					: expression;
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

		sealed class ChangeIfToConditionalRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.MergeCondition;
			public override string Title => R.CMD_IfElseToConditional;

			public override bool Accept(RefactoringContext ctx) {
				return GetConditionalStatement(ctx.NodeIncludeTrivia).ifStatement != null;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var (ifStatement, statement, elseStatement) = GetConditionalStatement(ctx.Node);
				if (ifStatement == null) {
					yield break;
				}
				StatementSyntax newNode;
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
						yield break;
				}
				yield return Replace(ifStatement, newNode.AnnotateReformatAndSelect());
			}

			static (IfStatementSyntax ifStatement, StatementSyntax statement, StatementSyntax elseStatement) GetConditionalStatement(SyntaxNode node) {
				StatementSyntax ss, es;
				SyntaxKind k;
				return node is IfStatementSyntax ifs
					&& ifs.Else != null
					&& (ss = ifs.Statement) != null
					&& (ss = GetSingleStatement(ss)) != null
					&& (es = ifs.Else.Statement) != null
					&& (es = GetSingleStatement(es)) != null
					&& es.IsKind(k = ss.Kind())
					&& (k == SyntaxKind.ReturnStatement
						|| k == SyntaxKind.YieldReturnStatement
						|| k == SyntaxKind.ExpressionStatement && ss.IsAssignedToSameTarget(es))
					? (ifs, ss, es)
					: default;
			}

			static StatementSyntax GetSingleStatement(StatementSyntax statement) {
				return statement is BlockSyntax b
					? (b.Statements.Count == 1 ? b.Statements[0] : null)
					: statement;
			}
		}

		sealed class ChangeConditionalToIfRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.SplitCondition;
			public override string Title => R.CMD_ConditionalToIfElse;

			public override bool Accept(RefactoringContext ctx) {
				var node = ctx.NodeIncludeTrivia;
				return node.IsKind(SyntaxKind.ConditionalExpression) && node.Parent is StatementSyntax;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
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
					yield break;
				}
				newNode = SF.IfStatement(node.Condition.WithoutTrailingTrivia(),
					SF.Block(whenTrue),
					SF.ElseClause(SF.Block(whenFalse))
					);
				yield return Replace(node.Parent, newNode.AnnotateReformatAndSelect());
			}
		}

		sealed class MultiLineConditionalRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.MultiLine;
			public override string Title => R.CMD_ConditionalOnMultiLines;

			public override bool Accept(RefactoringContext ctx) {
				var node = ctx.NodeIncludeTrivia;
				return node.IsKind(SyntaxKind.ConditionalExpression)
					&& node.IsMultiLine(false) == false;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var conditional = ctx.NodeIncludeTrivia as ConditionalExpressionSyntax;
				if (conditional == null) {
					yield break;
				}
				// it is frustrating that the C# formatter removes trivias in ?: expressions
				var options = ctx.WorkspaceOptions;
				var indent = conditional.GetContainingStatement()
					.GetPrecedingWhitespace()
					.Add(SF.Whitespace(options.GetIndentString()));
				var newLine = SF.Whitespace(options.GetNewLineString());
				var newNode = conditional.Update(conditional.Condition.WithTrailingTrivia(newLine),
					conditional.QuestionToken.WithLeadingTrivia(indent).WithTrailingTrivia(SF.Space),
					conditional.WhenTrue.WithTrailingTrivia(newLine),
					conditional.ColonToken.WithLeadingTrivia(indent).WithTrailingTrivia(SF.Space),
					conditional.WhenFalse);
				yield return Replace(conditional, newNode.AnnotateSelect());
			}
		}
	}
}
