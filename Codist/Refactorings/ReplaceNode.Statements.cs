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
		public static readonly ReplaceNode WrapInIf = new WrapInIfRefactoring();
		public static readonly ReplaceNode WrapInTryCatch = new WrapInTryCatchRefactoring();
		public static readonly ReplaceNode WrapInElse = new WrapInElseRefactoring();
		public static readonly ReplaceNode WrapInRegion = new WrapInRegionRefactoring();
		public static readonly ReplaceNode MergeToConditional = new MergeToConditionalRefactoring();

		abstract class ReplaceStatements : ReplaceNode
		{
			public override bool Accept(RefactoringContext ctx) {
				return ctx.SelectedStatementInfo.Items != null;
			}
		}

		sealed class WrapInIfRefactoring : ReplaceStatements
		{
			public override int IconId => IconIds.If;
			public override string Title => R.CMD_WrapInIf;

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				yield return Replace(ctx.SelectedStatementInfo.Items, SF.IfStatement(
					SF.LiteralExpression(SyntaxKind.TrueLiteralExpression).AnnotateSelect(),
					SF.Block(ctx.SelectedStatementInfo.Items)).WithAdditionalAnnotations(CodeFormatHelper.Reformat));
			}
		}

		sealed class MergeToConditionalRefactoring : ReplaceStatements
		{
			public override int IconId => IconIds.MergeCondition;
			public override string Title => R.CMD_MergeStatementsToConditional;

			public override bool Accept(RefactoringContext ctx) {
				StatementSyntax ifStatement, otherStatement;
				List<StatementSyntax> s;
				return base.Accept(ctx)
					&& (s = ctx.SelectedStatementInfo.Items).Count == 2
					&& s[0] is IfStatementSyntax ifs
					&& ifs.Else == null
					&& ifs.Statement != null
					&& ((ifStatement = ifs.Statement.GetSingleStatement()) != null)
					&& (otherStatement = s[1]).IsKind(ifStatement.Kind())
					&& MayBeMerged(ifStatement, otherStatement);
			}

			static bool MayBeMerged(StatementSyntax statement, StatementSyntax other) {
				switch (statement.Kind()) {
					case SyntaxKind.ReturnStatement:
						return ((ReturnStatementSyntax)statement).Expression != null;
					case SyntaxKind.YieldReturnStatement:
						return true;
					case SyntaxKind.ExpressionStatement:
						return statement.IsAssignedToSameTarget(other);
				}
				return false;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var s = ctx.SelectedStatementInfo.Items;
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
					case SyntaxKind.ExpressionStatement:
						var assignment = (AssignmentExpressionSyntax)((ExpressionStatementSyntax)first).Expression;
						newStatement = SF.ExpressionStatement(
							SF.AssignmentExpression(assignment.Kind(),
								assignment.Left,
								assignment.OperatorToken.WithTrailingTrivia(SF.Space),
								MakeConditional(ctx, ifs,
									assignment.Right,
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

		sealed class WrapInElseRefactoring : ReplaceStatements
		{
			public override int IconId => IconIds.Else;
			public override string Title => R.CMD_WrapInElse;

			public override bool Accept(RefactoringContext ctx) {
				return base.Accept(ctx)
					&& ctx.SelectedStatementInfo.Preceding is IfStatementSyntax ifs
					&& ifs.Else == null;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var ifs = ctx.SelectedStatementInfo.Preceding as IfStatementSyntax;
				yield return Replace(ifs, ifs.WithElse(SF.ElseClause(ctx.SelectedStatementInfo.Items.Count > 0 || ifs.Statement.IsKind(SyntaxKind.Block)
					? SF.Block(ctx.SelectedStatementInfo.Items)
					: ctx.SelectedStatementInfo.Items[0])).AnnotateReformatAndSelect());
				yield return Remove(ctx.SelectedStatementInfo.Items);
			}
		}

		sealed class WrapInTryCatchRefactoring : ReplaceStatements
		{
			public override int IconId => IconIds.TryCatch;
			public override string Title => R.CMD_WrapInTryCatch;

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				yield return Replace(ctx.SelectedStatementInfo.Items, SF.TryStatement(SF.Block(ctx.SelectedStatementInfo.Items),
					new SyntaxList<CatchClauseSyntax>(
						SF.CatchClause(SF.Token(SyntaxKind.CatchKeyword), SF.CatchDeclaration(SF.IdentifierName("Exception").AnnotateSelect(), SF.Identifier("ex")),
						null,
						SF.Block())),
					null).WithAdditionalAnnotations(CodeFormatHelper.Reformat));
			}
		}

		sealed class WrapInRegionRefactoring : ReplaceStatements
		{
			public override int IconId => IconIds.SurroundWith;
			public override string Title => R.CMD_SurroundWithRegion;

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var statements = ctx.SelectedStatementInfo.Items;
				var first = statements[0];
				var last = statements[statements.Count - 1];
				var (indent, newLine) = ctx.GetIndentAndNewLine(first.SpanStart, 0);
				const string REGION_NAME = "RegionName";
				if (first == last) {
					yield return Replace(first,
						first.WithLeadingTrivia(first.GetLeadingTrivia().Insert(0, GetRegionLeadingTrivia(REGION_NAME, indent, newLine)))
							.WithTrailingTrivia(first.GetTrailingTrivia().Add(GetRegionTrailingTrivia(indent)).Add(newLine))
						);
				}
				else {
					yield return Replace(first,
						first.WithLeadingTrivia(first.GetLeadingTrivia().Insert(0, GetRegionLeadingTrivia(REGION_NAME, indent, newLine)))
						);
					yield return Replace(last,
						last.WithTrailingTrivia(last.GetTrailingTrivia().Add(GetRegionTrailingTrivia(indent)).Add(newLine))
						);
				}
			}

			static SyntaxTrivia GetRegionLeadingTrivia(string regionName, SyntaxTriviaList indent, SyntaxTrivia newLine) {
				return SF.Trivia(
					SF.RegionDirectiveTrivia(true).WithLeadingTrivia(indent)
						.WithEndOfDirectiveToken(
							SF.Token(
								SF.TriviaList(SF.Space, SF.PreprocessingMessage(regionName).WithAdditionalAnnotations(CodeFormatHelper.Select)),
								SyntaxKind.EndOfDirectiveToken,
								SF.TriviaList(newLine))));
			}

			static SyntaxTrivia GetRegionTrailingTrivia(SyntaxTriviaList indent) {
				return SF.Trivia(SF.EndRegionDirectiveTrivia(true).WithLeadingTrivia(indent));
			}
		}
	}
}
