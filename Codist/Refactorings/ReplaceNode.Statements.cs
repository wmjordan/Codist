using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using R = Codist.Properties.Resources;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Codist.Refactorings
{
	abstract partial class ReplaceNode
	{
		public static readonly ReplaceNode WrapInIf = new WrapInIfRefactoring();
		public static readonly ReplaceNode WrapInTryCatch = new WrapInTryCatchRefactoring();
		public static readonly ReplaceNode WrapInElse = new WrapInElseRefactoring();
		public static readonly ReplaceNode MergeToConditional = new MergeToConditionalRefactoring();

		sealed class WrapInIfRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.If;
			public override string Title => R.CMD_WrapInIf;

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				yield return Replace(ctx.SelectedStatementInfo.Statements, SF.IfStatement(
					SF.LiteralExpression(SyntaxKind.TrueLiteralExpression).AnnotateSelect(),
					SF.Block(ctx.SelectedStatementInfo.Statements)).WithAdditionalAnnotations(CodeFormatHelper.Reformat));
			}
		}

		sealed class MergeToConditionalRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.MergeCondition;
			public override string Title => R.CMD_MergeStatementsToConditional;

			public override bool Accept(RefactoringContext ctx) {
				StatementSyntax ifStatement, otherStatement;
				var s = ctx.SelectedStatementInfo.Statements;
				return s?.Count == 2
					&& s[0] is IfStatementSyntax ifs
					&& ifs.Else == null
					&& ifs.Statement != null
					&& ((ifStatement = ifs.Statement.GetSingleStatement()) != null)
					&& IsWrappable(ifStatement)
					&& (otherStatement = s[1]).IsKind(ifStatement.Kind())
					&& IsWrappable(otherStatement)
					&& (ifStatement.IsKind(SyntaxKind.SimpleAssignmentExpression) == false || ifStatement.IsAssignedToSameTarget(otherStatement));
			}

			static bool IsWrappable(StatementSyntax statement) {
				switch (statement.Kind()) {
					case SyntaxKind.ReturnStatement:
						return ((ReturnStatementSyntax)statement).Expression != null;
					case SyntaxKind.YieldReturnStatement:
					case SyntaxKind.SimpleAssignmentExpression:
						return true;
				}
				return false;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var s = ctx.SelectedStatementInfo.Statements;
				var ifs = s[0] as IfStatementSyntax;
				var first = ifs.Statement.GetSingleStatement();
				var other = s[1].GetSingleStatement();
				StatementSyntax newStatement;
				switch (other.Kind()) {
					case SyntaxKind.ReturnStatement:
						newStatement = SF.ReturnStatement(
							SF.Token(SyntaxKind.ReturnKeyword).WithTrailingTrivia(SF.Space),
							MakeConditional(ctx, ifs,
								(first as ReturnStatementSyntax).Expression,
								(other as ReturnStatementSyntax).Expression),
							SF.Token(SyntaxKind.SemicolonToken)
							);
						break;
					case SyntaxKind.SimpleAssignmentExpression:
						var assignee = ((AssignmentExpressionSyntax)((ExpressionStatementSyntax)first).Expression).Left;
						newStatement = SF.ExpressionStatement(
							SF.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
								assignee,
								MakeConditional(ctx, ifs,
									((AssignmentExpressionSyntax)((ExpressionStatementSyntax)first).Expression).Right,
									((AssignmentExpressionSyntax)((ExpressionStatementSyntax)other).Expression).Right))
							);
						break;
					case SyntaxKind.YieldReturnStatement:
						newStatement = SF.YieldStatement(SyntaxKind.YieldReturnStatement,
							MakeConditional(ctx, ifs,
								(first as YieldStatementSyntax).Expression,
								(other as YieldStatementSyntax).Expression));
						break;
					default:
						yield break;
				}
				yield return Replace(ifs,
					newStatement.WithLeadingTrivia(ifs.GetLeadingTrivia())
						.WithTrailingTrivia(other.GetTrailingTrivia())
						.AnnotateSelect()
					);
				yield return Remove(other);
			}

			static ConditionalExpressionSyntax MakeConditional(RefactoringContext ctx, IfStatementSyntax ifStatement, ExpressionSyntax whenTrue, ExpressionSyntax whenFalse) {
				var (indent, newLine) = ctx.GetIndentAndNewLine(ifStatement.SpanStart);
				return SF.ConditionalExpression(ifStatement.Condition.WithTrailingTrivia(newLine),
					SF.Token(SyntaxKind.QuestionToken).WithLeadingTrivia(indent).WithTrailingTrivia(SF.Space),
					whenTrue.WithTrailingTrivia(newLine),
					SF.Token(SyntaxKind.ColonToken).WithLeadingTrivia(indent).WithTrailingTrivia(SF.Space),
					whenFalse);
			}
		}

		sealed class WrapInElseRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.Else;
			public override string Title => R.CMD_WrapInElse;

			public override bool Accept(RefactoringContext ctx) {
				var p = ctx.SelectedStatementInfo.Preceding;
				return p is IfStatementSyntax ifs && ifs.Else == null;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var ifs = ctx.SelectedStatementInfo.Preceding as IfStatementSyntax;
				yield return Replace(ifs, ifs.WithElse(SF.ElseClause(ctx.SelectedStatementInfo.Statements.Count > 0 || ifs.Statement.IsKind(SyntaxKind.Block)
					? SF.Block(ctx.SelectedStatementInfo.Statements)
					: ctx.SelectedStatementInfo.Statements[0])).AnnotateReformatAndSelect());
				yield return Remove(ctx.SelectedStatementInfo.Statements);
			}
		}

		sealed class WrapInTryCatchRefactoring : ReplaceNode
		{
			public override int IconId => IconIds.TryCatch;
			public override string Title => R.CMD_WrapInTryCatch;

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				yield return Replace(ctx.SelectedStatementInfo.Statements, SF.TryStatement(SF.Block(ctx.SelectedStatementInfo.Statements),
					new SyntaxList<CatchClauseSyntax>(
						SF.CatchClause(SF.Token(SyntaxKind.CatchKeyword), SF.CatchDeclaration(SF.IdentifierName("Exception").AnnotateSelect(), SF.Identifier("ex")),
						null,
						SF.Block())),
					null).WithAdditionalAnnotations(CodeFormatHelper.Reformat));
			}
		}
	}
}
