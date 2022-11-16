using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using R = Codist.Properties.Resources;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Codist.Refactorings
{
	abstract class ReplaceStatements : IRefactoring<List<StatementSyntax>>
	{
		public static readonly ReplaceStatements WrapInIf = new WrapInIfRefactoring();
		public static readonly ReplaceStatements WrapInTryCatch = new WrapInTryCatchRefactoring();
		public static readonly ReplaceStatements WrapInUsing = new WrapInUsingRefactoring();

		public abstract int IconId { get; }
		public abstract string Title { get; }

		public virtual bool Accept(List<StatementSyntax> statements) {
			return true;
		}

		protected abstract SyntaxNode Refactor(List<StatementSyntax> statements);

		public void Refactor(SemanticContext context) {
			var view = context.View;
			var selectedSpan = view.FirstSelectionSpan();
			var statements = context.Compilation.GetStatements(selectedSpan.ToTextSpan());
			var span = new Microsoft.VisualStudio.Text.SnapshotSpan(view.TextSnapshot, statements[0].FullSpan.Start, statements.Sum(s => s.FullSpan.Length));
			SyntaxAnnotation annStatement = new SyntaxAnnotation();
			var first = statements[0];
			if (first.HasLeadingTrivia) {
				statements[0] = first.WithoutLeadingTrivia();
			}
			SyntaxNode statement = Refactor(statements)
				.WithAdditionalAnnotations(annStatement);
			var root = first.SyntaxTree.GetRoot()
				.ReplaceNode(first, statement);
			statement = root.Format(context.Workspace)
				.GetAnnotatedNodes(annStatement)
				.First();
			view.Edit(
				(rep: statement.ToFullString(), sel: span),
				(v, p, edit) => edit.Replace(p.sel, p.rep)
			);
			var selSpan = statement.GetAnnotatedNodes(CodeFormatHelper.Select).FirstOrDefault().Span;
			if (selSpan.Length != 0) {
				view.SelectSpan(selectedSpan.Start.Position + (selSpan.Start - statement.SpanStart), selSpan.Length, 1);
			}
		}

		sealed class WrapInIfRefactoring : ReplaceStatements
		{
			public override int IconId => IconIds.If;
			public override string Title => R.CMD_WrapInIf;

			protected override SyntaxNode Refactor(List<StatementSyntax> statements) {
				return SF.IfStatement(
					SF.LiteralExpression(SyntaxKind.TrueLiteralExpression).AnnotateSelect(),
					SF.Block(statements));
			}
		}

		sealed class WrapInTryCatchRefactoring : ReplaceStatements
		{
			public override int IconId => IconIds.TryCatch;
			public override string Title => R.CMD_WrapInTryCatch;

			protected override SyntaxNode Refactor(List<StatementSyntax> statements) {
				return SF.TryStatement(SF.Block(statements),
					new SyntaxList<CatchClauseSyntax>(
						SF.CatchClause(SF.Token(SyntaxKind.CatchKeyword), SF.CatchDeclaration(SF.IdentifierName("Exception").AnnotateSelect(), SF.Identifier("ex")),
						null,
						SF.Block())),
					null);
			}
		}

		sealed class WrapInUsingRefactoring : ReplaceStatements
		{
			public override int IconId => IconIds.Using;
			public override string Title => "Wrap in <using>";

			protected override SyntaxNode Refactor(List<StatementSyntax> statements) {
				return SF.UsingStatement(null, SF.IdentifierName("disposable").AnnotateSelect(), SF.Block(statements));
			}
		}
	}
}
