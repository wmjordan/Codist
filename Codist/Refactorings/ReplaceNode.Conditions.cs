using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using R = Codist.Properties.Resources;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Codist.Refactorings
{
	abstract partial class ReplaceNode
	{
		public static readonly ReplaceNode DeleteCondition = new DeleteConditionRefactoring();
		public static readonly ReplaceNode NestCondition = new NestConditionRefactoring();
		public static readonly ReplaceNode MergeCondition = new MergeConditionRefactoring();
		public static readonly ReplaceNode IfToConditional = new IfToConditionalRefactoring();
		public static readonly ReplaceNode ConditionalToIf = new ConditionalToIfRefactoring();
		public static readonly ReplaceNode SwapConditionResults = new SwapConditionResultsRefactoring();
		public static readonly ReplaceNode While = new WhileRefactoring();

		sealed class DeleteConditionRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.DeleteCondition;
			public override string Title => R.CMD_DeleteCondition;

			public override bool Accept(RefactoringContext ctx) {
				return ctx.Node is IfStatementSyntax ifs
					&& ctx.SemanticContext.SemanticModel.AnalyzeDataFlow(ifs.Condition).VariablesDeclared.Length == 0
					&& (ctx.SelectedStatementInfo.Items == null || ctx.SelectedStatementInfo.Items.Count == 1);
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var node = ctx.Node;
				yield return ((IfStatementSyntax)node).Statement is BlockSyntax b
					? node.Parent.IsKind(SyntaxKind.ElseClause)
						? Replace((ElseClauseSyntax)node.Parent, SF.ElseClause(SF.Block(b.Statements)).AnnotateReformatAndSelect())
						: Replace(node, b.Statements.AttachAnnotation(CodeFormatHelper.Reformat, CodeFormatHelper.Select))
					: Replace(node, ((IfStatementSyntax)node).Statement.AnnotateReformatAndSelect());
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
					if (node.Kind().IsAny(SyntaxKind.IfStatement, SyntaxKind.WhileStatement)) {
						return (StatementSyntax)node;
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
				SyntaxNode node;
				if (ctx.NodeIncludeTrivia is IfStatementSyntax ifs
					&& (node = GetParentConditional(ifs)) != null
					&& node.SyntaxTree.GetText().Lines.GetLineFromPosition(node.SpanStart)
						.SpanIncludingLineBreak.Contains(ifs.FullSpan.Start) == false) {
					_NodeKind = node.IsKind(SyntaxKind.IfStatement) ? "if"
						: node.IsKind(SyntaxKind.ElseClause) ? "else"
						: "while";
					return true;
				}
				return false;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var ifs = ctx.Node as IfStatementSyntax;
				var s = GetParentConditional(ifs);
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
				else if (s is ElseClauseSyntax newElse) {
					newElse = SF.ElseClause(newElse.ElseKeyword.WithTrailingTrivia(), ifs);
					yield return Replace(s, newElse.AnnotateReformatAndSelect());
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

			static SyntaxNode GetParentConditional(IfStatementSyntax ifs) {
				var node = ifs.Parent;
				if (node.IsKind(SyntaxKind.Block)) {
					var block = (BlockSyntax)node;
					if (block.Statements.Count > 1) {
						return null;
					}
					node = node.Parent;
				}
				return node.Kind().IsAny(SyntaxKind.IfStatement, SyntaxKind.WhileStatement)
					? (ifs.Else == null ? node : null)
					: (node.IsKind(SyntaxKind.ElseClause) ? node : null);
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
				if (node.IsKind(SyntaxKind.ConditionalExpression)) {
					SyntaxNode p = node.Parent;
					if (p is StatementSyntax) {
						return true;
					}
					if (p.IsKind(SyntaxKind.SimpleAssignmentExpression)) {
						return p.Parent.IsKind(SyntaxKind.ExpressionStatement);
					}
					if (p.IsKind(SyntaxKind.EqualsValueClause)) {
						p = p.Parent;
						return p.IsKind(SyntaxKind.VariableDeclarator)
							&& p.Parent is VariableDeclarationSyntax v
							&& v.Variables.Count == 1
							&& v.Parent.IsKind(SyntaxKind.LocalDeclarationStatement);
					}
				}
				return false;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var condition = ctx.NodeIncludeTrivia as ConditionalExpressionSyntax;
				var node = condition.Parent;
				SyntaxNode newNode;
				StatementSyntax whenTrue, whenFalse;
				if (node is ReturnStatementSyntax r) {
					whenTrue = SF.ReturnStatement(condition.WhenTrue);
					whenFalse = SF.ReturnStatement(condition.WhenFalse);
				}
				else if (node is AssignmentExpressionSyntax ae
					&& ae.IsKind(SyntaxKind.SimpleAssignmentExpression)
					&& ae.Parent is ExpressionStatementSyntax es) {
					node = es;
					whenTrue = SF.ExpressionStatement(SF.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, ae.Left, condition.WhenTrue));
					whenFalse = SF.ExpressionStatement(SF.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, ae.Left, condition.WhenFalse));
				}
				else if (node is YieldStatementSyntax) {
					whenTrue = SF.YieldStatement(SyntaxKind.YieldReturnStatement, condition.WhenTrue);
					whenFalse = SF.YieldStatement(SyntaxKind.YieldReturnStatement, condition.WhenFalse);
				}
				else if (node.IsKind(SyntaxKind.EqualsValueClause)
					&& node.Parent is VariableDeclaratorSyntax v) {
					whenTrue = SF.ExpressionStatement(SF.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SF.IdentifierName(v.Identifier), condition.WhenTrue));
					whenFalse = SF.ExpressionStatement(SF.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SF.IdentifierName(v.Identifier), condition.WhenFalse));
					if (v.Parent is VariableDeclarationSyntax d) {
						var tn = d.Type;
						if (tn.IsVar) {
							tn = SF.ParseTypeName(ctx.SemanticContext.SemanticModel.GetSymbol(d.Type)
								?.ToMinimalDisplayString(ctx.SemanticContext.SemanticModel, d.Type.SpanStart)
								?? "var");
						}
						node = d.Parent;
						yield return Replace(node, new SyntaxNode[] {
							SF.LocalDeclarationStatement(
								SF.VariableDeclaration(tn, SF.SeparatedList<VariableDeclaratorSyntax>(new[] {
									SF.VariableDeclarator(v.Identifier.WithoutTrivia())
								}))
								).WithTriviaFrom(node),
							SF.IfStatement(condition.Condition.WithoutTrailingTrivia(),
								SF.Block(whenTrue),
								SF.ElseClause(SF.Block(whenFalse))
								).AnnotateReformatAndSelect()
						});
					}
					yield break;
				}
				else {
					yield break;
				}
				newNode = SF.IfStatement(condition.Condition.WithoutTrailingTrivia(),
					SF.Block(whenTrue),
					SF.ElseClause(SF.Block(whenFalse))
					);
				yield return Replace(node, newNode.AnnotateReformatAndSelect());
			}
		}

		sealed class SwapConditionResultsRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.SwapOperands;
			public override string Title => R.CMD_SwapConditionResults;

			public override bool Accept(RefactoringContext ctx) {
				return ctx.Node.IsKind(SyntaxKind.ConditionalExpression);
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				if (ctx.Node is ConditionalExpressionSyntax node) {
					yield return Replace(node,
						node.Update(node.Condition,
							node.QuestionToken,
							node.WhenFalse.WithTriviaFrom(node.WhenTrue),
							node.ColonToken,
							node.WhenTrue.WithTriviaFrom(node.WhenFalse)).AnnotateSelect());
				}
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
	}
}
