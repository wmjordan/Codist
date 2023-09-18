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
		public static readonly ReplaceNode WrapInTryFinally = new WrapInTryFinallyRefactoring();
		public static readonly ReplaceNode WrapInElse = new WrapInElseRefactoring();
		public static readonly ReplaceNode WrapInUsing = new WrapInUsingRefactoring();
		public static readonly ReplaceNode MergeToConditional = new MergeToConditionalRefactoring();
		public static readonly ReplaceNode DeleteStatement = new DeleteStatementRefactoring();

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
				return Chain.Create(Replace(ctx.SelectedStatementInfo.Items, SF.IfStatement(
					SF.LiteralExpression(SyntaxKind.TrueLiteralExpression).AnnotateSelect(),
					SF.Block(ctx.SelectedStatementInfo.Items)).WithAdditionalAnnotations(CodeFormatHelper.Reformat)));
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
								((ReturnStatementSyntax)first).Expression,
								((ReturnStatementSyntax)other).Expression),
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
								((YieldStatementSyntax)first).Expression,
								((YieldStatementSyntax)other).Expression));
						break;
					default:
						return Enumerable.Empty<RefactoringAction>();
				}
				return Chain.Create(Replace(ifs,
					newStatement.WithLeadingTrivia(ifs.GetLeadingTrivia())
						.WithTrailingTrivia(other.GetTrailingTrivia())
						.AnnotateSelect()
					)).Add(Remove(other));
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
				return Chain.Create(Replace(ifs, ifs.WithElse(SF.ElseClause(ctx.SelectedStatementInfo.Items.Count > 0 || ifs.Statement.IsKind(SyntaxKind.Block)
					? SF.Block(ctx.SelectedStatementInfo.Items)
					: ctx.SelectedStatementInfo.Items[0])).AnnotateReformatAndSelect()))
					.Add(Remove(ctx.SelectedStatementInfo.Items));
			}
		}

		sealed class WrapInTryCatchRefactoring : ReplaceStatements
		{
			public override int IconId => IconIds.TryCatch;
			public override string Title => R.CMD_WrapInTryCatch;

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				return Chain.Create(Replace(ctx.SelectedStatementInfo.Items,
					SF.TryStatement(SF.Block(ctx.SelectedStatementInfo.Items),
						new SyntaxList<CatchClauseSyntax>(
							SF.CatchClause(SF.Token(SyntaxKind.CatchKeyword), SF.CatchDeclaration(SF.IdentifierName("Exception").AnnotateSelect(), SF.Identifier("ex")),
							null,
							SF.Block())),
						null).WithAdditionalAnnotations(CodeFormatHelper.Reformat)
					));
			}
		}

		sealed class WrapInTryFinallyRefactoring : ReplaceStatements
		{
			public override int IconId => IconIds.TryCatch;
			public override string Title => R.CMD_WrapInTryFinally;

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var statements = ctx.SelectedStatementInfo.Items;
				return Chain.Create(Replace(statements,
					SF.TryStatement(SF.Block(statements),
						default,
						SF.FinallyClause(SF.Block(
							SF.Token(SyntaxKind.OpenBraceToken),
							new SyntaxList<StatementSyntax>(SF.EmptyStatement().AnnotateSelect()),
							SF.Token(SyntaxKind.CloseBraceToken))
						)).WithAdditionalAnnotations(CodeFormatHelper.Reformat)
					));
			}
		}

		sealed class WrapInUsingRefactoring : ReplaceStatements
		{
			public override int IconId => IconIds.Using;
			public override string Title => R.CMD_WrapInUsing;

			public override bool Accept(RefactoringContext ctx) {
				StatementSyntax s;
				return base.Accept(ctx)
					&& ((s = ctx.SelectedStatementInfo.Items[0]) is LocalDeclarationStatementSyntax loc
							&& loc.Declaration.Variables.Count == 1
							&& (ctx.SemanticContext.SemanticModel.GetSymbol(loc.Declaration.Type) as ITypeSymbol)
								?.AllInterfaces.Any(i => i.IsDisposable()) == true
						|| s is ExpressionStatementSyntax exp
							&& exp.Expression is AssignmentExpressionSyntax a
							&& ctx.SemanticContext.SemanticModel.GetTypeInfo(a.Left).Type
								?.AllInterfaces.Any(i => i.IsDisposable()) == true);
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				var statements = ctx.SelectedStatementInfo.Items;
				var newBlock = SF.Block(ctx.SelectedStatementInfo.Items.Skip(1));
				if (statements[0] is LocalDeclarationStatementSyntax loc) {
					return Chain.Create(Replace(ctx.SelectedStatementInfo.Items,
						SF.UsingStatement(loc.Declaration.WithoutTrivia(), null, newBlock).AnnotateReformatAndSelect()));
				}
				if (statements[0] is ExpressionStatementSyntax exp) {
					return Chain.Create(Replace(ctx.SelectedStatementInfo.Items,
						SF.UsingStatement(null, exp.Expression, newBlock).AnnotateReformatAndSelect()));
				}
				return Enumerable.Empty<RefactoringAction>();
			}
		}

		sealed class DeleteStatementRefactoring : ReplaceStatements
		{
			string _Title;
			public override int IconId => IconIds.Delete;
			public override string Title => _Title;

			public override bool Accept(RefactoringContext ctx) {
				if (ctx.SelectedStatementInfo.Items != null) {
					_Title = R.CMD_DeleteStatement;
					return true;
				}
				if (ctx.Node is StatementSyntax s) {
					SyntaxNode p;
					if ((p = ctx.Node.Parent) is StatementSyntax || p?.Kind().IsDeclaration() == true) {
						_Title = R.CMD_DeleteAStatement.Replace("<A>", s.Kind().ToString().Replace("Statement", string.Empty));
						return true;
					}
				}
				if (ctx.Node.Parent is SwitchSectionSyntax) {
					_Title = R.CMD_DeleteSwitchSection;
					return true;
				}
				return false;
			}

			public override IEnumerable<RefactoringAction> Refactor(RefactoringContext ctx) {
				return Chain.Create(ctx.SelectedStatementInfo.Items != null
					? Remove(ctx.SelectedStatementInfo.Items)
					: ctx.Node is StatementSyntax
					? Remove(ctx.Node)
					: Remove(ctx.Node.Parent));
			}
		}
	}
}
