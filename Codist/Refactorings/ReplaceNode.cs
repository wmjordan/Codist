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
		public static readonly ReplaceNode AsToCast = new AsToCastRefactoring();
		public static readonly ReplaceNode DeleteCondition = new DeleteConditionRefactoring();
		public static readonly ReplaceNode RemoveContainingStatement = new RemoveContainerRefactoring();
		public static readonly ReplaceNode SwapOperands = new SwapOperandsRefactoring();
		public static readonly ReplaceNode NestCondition = new NestConditionRefactoring();
		public static readonly ReplaceNode MergeCondition = new MergeConditionRefactoring();
		public static readonly ReplaceNode IfToConditional = new IfToConditionalRefactoring();
		public static readonly ReplaceNode ConditionalToIf = new ConditionalToIfRefactoring();
		public static readonly ReplaceNode While = new WhileRefactoring();
		public static readonly ReplaceNode MultiLineList = new MultiLineListRefactoring();
		public static readonly ReplaceNode MultiLineExpression = new MultiLineExpressionRefactoring();

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
					case SyntaxKind.ForEachVariableStatement:
						return ((ForEachVariableStatementSyntax)node).Statement.IsKind(SyntaxKind.Block) == false;
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
					case SyntaxKind.ForEachVariableStatement:
						statement = ((ForEachVariableStatementSyntax)node).Statement; break;
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
					yield return Replace(statement.Parent, statement.Parent.ReplaceNode(statement, SF.Block(statement)).AnnotateReformatAndSelect());
				}
			}
		}

		sealed class AsToCastRefactoring : ReplaceNode
		{
			string _Title;
			public override int IconId => IconIds.AsToCast;
			public override string Title => _Title;

			public override bool Accept(RefactoringContext ctx) {
				switch (ctx.Node.RawKind) {
					case (int)SyntaxKind.AsExpression:
						_Title = R.CMD_AsToCast;
						return true;
					case (int)SyntaxKind.CastExpression:
						_Title = R.CMD_CastToAs;
						return true;
				}
				return false;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				if (ctx.Node is BinaryExpressionSyntax exp) {
					yield return Replace(exp, SF.CastExpression(exp.Right.WithoutTrailingTrivia() as TypeSyntax, exp.Left).WithTriviaFrom(exp).AnnotateReformatAndSelect());
				}
				else if (ctx.Node is CastExpressionSyntax ce) {
					yield return Replace(ce, SF.BinaryExpression(SyntaxKind.AsExpression, ce.Expression.WithoutTrailingTrivia(), ce.Type).WithTriviaFrom(ce.Expression).AnnotateReformatAndSelect());
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
				yield return ifs is BlockSyntax b
					? Replace(ctx.Node, b.Statements.AttachAnnotation(CodeFormatHelper.Reformat, CodeFormatHelper.Select))
					: Replace(ctx.Node, ifs.AnnotateReformatAndSelect());
			}
		}

		sealed class RemoveContainerRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.Delete;
			public override string Title => R.CMD_DeleteContainingBlock;

			public override bool Accept(RefactoringContext ctx) {
				var node = ctx.NodeIncludeTrivia;
				var s = node.GetContainingStatement();
				return s != null
					&& s.SpanStart == node.SpanStart
					&& GetRemovableAncestor(s) != null;
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
						yield return Replace(ifs.Parent, (keep.Count > 1 || statement.Parent.IsKind(SyntaxKind.Block)
							? SF.ElseClause(SF.Block(keep))
							: SF.ElseClause(keep[0])).AnnotateReformatAndSelect());
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
					case SyntaxKind.LogicalAndExpression:
					case SyntaxKind.LogicalOrExpression:
					case SyntaxKind.BitwiseAndExpression:
					case SyntaxKind.BitwiseOrExpression:
					case SyntaxKind.ExclusiveOrExpression:
					case SyntaxKind.EqualsExpression:
					case SyntaxKind.NotEqualsExpression:
					case SyntaxKind.LessThanExpression:
					case SyntaxKind.LessThanOrEqualExpression:
					case SyntaxKind.GreaterThanExpression:
					case SyntaxKind.GreaterThanOrEqualExpression:
					case SyntaxKind.AddExpression:
					case SyntaxKind.SubtractExpression:
					case SyntaxKind.MultiplyExpression:
					case SyntaxKind.DivideExpression:
					case SyntaxKind.ModuloExpression:
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
				return node.Kind().IsAny(SyntaxKind.IfStatement, SyntaxKind.WhileStatement) ? node as StatementSyntax : null;
			}
		}

		sealed class IfToConditionalRefactoring : ReplaceNode
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
				var (indent, newLine) = ctx.GetIndentAndNewLine(ifStatement.SpanStart);
				switch (statement.Kind()) {
					case SyntaxKind.ReturnStatement:
						newNode = SF.ReturnStatement(
							MakeConditionalExpression(ifStatement.Condition.WithLeadingTrivia(SF.Space),
								(statement as ReturnStatementSyntax).Expression,
								(elseStatement as ReturnStatementSyntax).Expression,
								indent, newLine)
							);
						break;
					case SyntaxKind.ExpressionStatement:
						var assignment = (AssignmentExpressionSyntax)((ExpressionStatementSyntax)statement).Expression;
						newNode = SF.ExpressionStatement(
							SF.AssignmentExpression(assignment.Kind(),
								assignment.Left,
								MakeConditionalExpression(ifStatement.Condition,
									assignment.Right,
									((AssignmentExpressionSyntax)((ExpressionStatementSyntax)elseStatement).Expression).Right,
								indent, newLine))
							);
						break;
					case SyntaxKind.YieldReturnStatement:
						newNode = SF.YieldStatement(SyntaxKind.YieldReturnStatement,
							MakeConditionalExpression(ifStatement.Condition,
								(statement as YieldStatementSyntax).Expression,
								(elseStatement as YieldStatementSyntax).Expression,
								indent, newLine));
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

			static ConditionalExpressionSyntax MakeConditionalExpression(ExpressionSyntax condition, ExpressionSyntax whenTrue, ExpressionSyntax whenFalse, SyntaxTriviaList indent, SyntaxTrivia newLine) {
				return SF.ConditionalExpression(condition.WithTrailingTrivia(newLine),
					SF.Token(SyntaxKind.QuestionToken).WithLeadingTrivia(indent).WithTrailingTrivia(SF.Space),
					whenTrue.WithTrailingTrivia(newLine),
					SF.Token(SyntaxKind.ColonToken).WithLeadingTrivia(indent).WithTrailingTrivia(SF.Space),
					whenFalse);
			}
		}

		sealed class ConditionalToIfRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.SplitCondition;
			public override string Title => R.CMD_ConditionalToIfElse;

			public override bool Accept(RefactoringContext ctx) {
				var node = ctx.NodeIncludeTrivia;
				return node.IsKind(SyntaxKind.ConditionalExpression)
					&& (node.Parent is StatementSyntax || node.Parent.IsKind(SyntaxKind.SimpleAssignmentExpression) && node.Parent.Parent.IsKind(SyntaxKind.ExpressionStatement));
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

		sealed class WhileRefactoring : ReplaceNode
		{
			int _Icon;
			string _Title;

			public override int IconId => _Icon;
			public override string Title => _Title;

			public override bool Accept(RefactoringContext ctx) {
				switch (ctx.Node.RawKind) {
					case (int)SyntaxKind.WhileStatement:
						_Icon = IconIds.DoWhile;
						_Title = R.CMD_WhileToDo;
						return true;
					case (int)SyntaxKind.DoStatement:
						_Icon = IconIds.While;
						_Title = R.CMD_DoToWhile;
						return true;
				}
				return false;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var node = ctx.Node;
				if (node is WhileStatementSyntax ws) {
					yield return Replace(node, SF.DoStatement(ws.Statement, ws.Condition).WithTriviaFrom(ws).AnnotateReformatAndSelect());
				}
				else if (node is DoStatementSyntax ds) {
					yield return Replace(node, SF.WhileStatement(ds.Condition, ds.Statement).WithTriviaFrom(ds).AnnotateReformatAndSelect());
				}
			}
		}

		sealed class MultiLineExpressionRefactoring : ReplaceNode
		{
			string _Title;
			public override int IconId => IconIds.MultiLine;
			public override string Title => _Title;

			public override bool Accept(RefactoringContext ctx) {
				var node = ctx.NodeIncludeTrivia;
				var nodeKind = node.Kind();
				switch (nodeKind) {
					case SyntaxKind.LogicalAndExpression: _Title = R.CMD_LogicalAndOnMultiLines; break;
					case SyntaxKind.AddExpression:
					case SyntaxKind.SubtractExpression: _Title = R.CMD_OperandsOnMultiLines; break;
					case SyntaxKind.LogicalOrExpression: _Title = R.CMD_LogicalOrOnMultiLines; break;
					case SyntaxKind.CoalesceExpression: _Title = R.CMD_CoalesceOnMultiLines; break;
					case SyntaxKind.ConditionalExpression:
						_Title = R.CMD_ConditionalOnMultiLines;
						return node.IsMultiLine(false) == false;
					default: return false;
				}
				SyntaxNode p = node.Parent;
				if (nodeKind.IsAny(SyntaxKind.AddExpression, SyntaxKind.SubtractExpression)) {
					while (p.Kind().IsAny(SyntaxKind.AddExpression, SyntaxKind.SubtractExpression)) {
						node = p;
						p = p.Parent;
					}
				}
				else {
					while (p.IsKind(nodeKind)) {
						node = p;
						p = p.Parent;
					}
				}
				return (p is StatementSyntax || p.IsKind(SyntaxKind.EqualsValueClause))
					&& node.IsMultiLine(false) == false;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var node = ctx.NodeIncludeTrivia;
				var nodeKind = node.Kind();
				var (indent, newLine) = ctx.GetIndentAndNewLine(node);
				BinaryExpressionSyntax newExp = null;
				SyntaxToken token;
				if (nodeKind == SyntaxKind.LogicalAndExpression) {
					ReformatLogicalExpressions(ref node, ref newExp, newLine, indent, nodeKind);
				}
				else if (nodeKind.IsAny(SyntaxKind.AddExpression, SyntaxKind.SubtractExpression)) {
					ReformatLogicalExpressions(ref node, ref newExp, newLine, indent, nodeKind);
				}
				else if (nodeKind == SyntaxKind.LogicalOrExpression) {
					ReformatLogicalExpressions(ref node, ref newExp, newLine, indent, nodeKind);
				}
				else if (nodeKind == SyntaxKind.CoalesceExpression) {
					token = CreateTokenWithTrivia(indent, SyntaxKind.QuestionQuestionToken);
					ReformatCoalesceExpression(ref node, ref newExp, newLine, token, nodeKind);
				}
				else if (nodeKind == SyntaxKind.ConditionalExpression) {
					yield return ReformatConditionalExpression((ConditionalExpressionSyntax)node, indent, newLine);
					yield break;
				}
				else {
					yield break;
				}
				yield return Replace(node, newExp.AnnotateSelect());
			}

			static SyntaxToken CreateTokenWithTrivia(SyntaxTriviaList indent, SyntaxKind syntaxKind) {
				return SF.Token(syntaxKind).WithLeadingTrivia(indent).WithTrailingTrivia(SF.Space);
			}

			static void ReformatLogicalExpressions(ref SyntaxNode node, ref BinaryExpressionSyntax newExp, SyntaxTrivia newLine, SyntaxTriviaList indent, SyntaxKind nodeKind) {
				var exp = (BinaryExpressionSyntax)node;
				if (nodeKind.IsAny(SyntaxKind.AddExpression, SyntaxKind.SubtractExpression)) {
					while (exp.Left.Kind().IsAny(SyntaxKind.AddExpression, SyntaxKind.SubtractExpression)) {
						exp = (BinaryExpressionSyntax)exp.Left;
					}
				}
				else {
					while (exp.Left.IsKind(nodeKind)) {
						exp = (BinaryExpressionSyntax)exp.Left;
					}
				}
				do {
					node = exp;
					newExp = exp.Update(((ExpressionSyntax)newExp ?? exp.Left).WithTrailingTrivia(newLine),
						exp.OperatorToken.WithLeadingTrivia(indent).WithTrailingTrivia(SF.Space),
						exp.Right);
					exp = exp.Parent as BinaryExpressionSyntax;
				} while (exp != null);
			}

			static void ReformatCoalesceExpression(ref SyntaxNode node, ref BinaryExpressionSyntax newExp, SyntaxTrivia newLine, SyntaxToken token, SyntaxKind nodeKind) {
				var exp = (BinaryExpressionSyntax)node;
				while (exp.Right.IsKind(nodeKind)) {
					exp = (BinaryExpressionSyntax)exp.Right;
				}
				do {
					node = exp;
					newExp = exp.Update(exp.Left.WithTrailingTrivia(newLine),
						token,
						(ExpressionSyntax)newExp ?? exp.Right);
					exp = exp.Parent as BinaryExpressionSyntax;
				} while (exp != null);
			}

			static RefactoringAction ReformatConditionalExpression(ConditionalExpressionSyntax node, SyntaxTriviaList indent, SyntaxTrivia newLine) {
				var newNode = node.Update(node.Condition.WithTrailingTrivia(newLine),
					node.QuestionToken.WithLeadingTrivia(indent).WithTrailingTrivia(SF.Space),
					node.WhenTrue.WithTrailingTrivia(newLine),
					node.ColonToken.WithLeadingTrivia(indent).WithTrailingTrivia(SF.Space),
					node.WhenFalse);
				return Replace(node, newNode.AnnotateSelect());
			}
		}

		sealed class MultiLineListRefactoring : ReplaceNode
		{
			string _Title;
			public override int IconId => IconIds.MultiLineList;
			public override string Title => _Title;

			public override bool Accept(RefactoringContext ctx) {
				var node = ctx.Node;
				switch (node.Kind()) {
					case SyntaxKind.ArgumentList:
						if (((ArgumentListSyntax)node).Arguments.Count > 1 && node.IsMultiLine(false) == false) {
							_Title = R.CMD_ArgumentsOnMultiLine;
							return true;
						}
						break;
					case SyntaxKind.ParameterList:
						if (((ParameterListSyntax)node).Parameters.Count > 1 && node.IsMultiLine(false) == false) {
							_Title = R.CMD_ParametersOnMultiLine;
							return true;
						}
						break;
					case SyntaxKind.ArrayInitializerExpression:
					case SyntaxKind.CollectionInitializerExpression:
					case SyntaxKind.ObjectInitializerExpression:
						if (((InitializerExpressionSyntax)node).Expressions.Count > 1 && node.IsMultiLine(false) == false) {
							_Title = R.CMD_ExpressionsOnMultiLine;
							return true;
						}
						break;
					case SyntaxKind.VariableDeclaration:
						if (((VariableDeclarationSyntax)node).Variables.Count > 1 && node.IsMultiLine(false) == false) {
							_Title = R.CMD_DeclarationsOnMultiLine;
							return true;
						}
						break;
				}
				return false;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var node = ctx.Node;
				var (indent, newLine) = ctx.GetIndentAndNewLine(node.SpanStart);
				SyntaxNode newNode = null;
				if (node is ArgumentListSyntax al) {
					newNode = al.WithArguments(MakeMultiLine(al.Arguments, indent, newLine));
				}
				else if (node is ParameterListSyntax pl) {
					newNode = pl.WithParameters(MakeMultiLine(pl.Parameters, indent, newLine));
				}
				else if (node is InitializerExpressionSyntax ie) {
					newNode = MakeMultiLine(ie, indent, newLine);
				}
				else if (node is VariableDeclarationSyntax va) {
					newNode = va.WithVariables(MakeMultiLine(va.Variables, indent, newLine));
				}
				if (newNode != null) {
					yield return Replace(node, newNode.AnnotateSelect());
				}
			}

			static SeparatedSyntaxList<T> MakeMultiLine<T>(SeparatedSyntaxList<T> list, SyntaxTriviaList indent, SyntaxTrivia newLine) where T : SyntaxNode {
				var l = new T[list.Count];
				for (int i = 0; i < l.Length; i++) {
					l[i] = i > 0 ? list[i].WithLeadingTrivia(indent) : list[i];
				}
				return SF.SeparatedList(l,
					Enumerable.Repeat(SF.Token(SyntaxKind.CommaToken).WithTrailingTrivia(newLine), l.Length - 1));
			}

			static InitializerExpressionSyntax MakeMultiLine(InitializerExpressionSyntax initializer, SyntaxTriviaList indent, SyntaxTrivia newLine) {
				var list = initializer.Expressions;
				var l = new ExpressionSyntax[list.Count];
				for (int i = 0; i < l.Length; i++) {
					l[i] = list[i].WithLeadingTrivia(indent);
				}
				l[l.Length - 1] = l[l.Length - 1].WithTrailingTrivia(newLine);
				return initializer.Update(initializer.OpenBraceToken.WithTrailingTrivia(newLine),
					SF.SeparatedList(l, Enumerable.Repeat(SF.Token(SyntaxKind.CommaToken).WithTrailingTrivia(newLine), l.Length - 1)),
					initializer.CloseBraceToken.WithLeadingTrivia(indent.RemoveAt(indent.Count - 1))
				);
			}
		}
	}
}
