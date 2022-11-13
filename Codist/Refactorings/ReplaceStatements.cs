using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Codist.Refactorings
{
	sealed class ReplaceStatements
	{
		public static readonly ReplaceStatements WrapInIf = new ReplaceStatements((s,a) => {
			return SF.IfStatement(
				SF.LiteralExpression(SyntaxKind.TrueLiteralExpression).WithAdditionalAnnotations(a),
				SF.Block(s));
		});
		public static readonly ReplaceStatements WrapInTryCatch = new ReplaceStatements((s,a) => {
			return SF.TryStatement(SF.Block(s),
				new SyntaxList<CatchClauseSyntax>(
					SF.CatchClause(SF.Token(SyntaxKind.CatchKeyword), SF.CatchDeclaration(SF.IdentifierName("Exception").WithAdditionalAnnotations(a), SF.Identifier("ex")),
					null,
					SF.Block())),
				null);
		});
		public static readonly ReplaceStatements WrapInUsing = new ReplaceStatements((s,a) => {
			return SF.UsingStatement(null, SF.IdentifierName("disposable").WithAdditionalAnnotations(a), SF.Block(s));
		});

		readonly Func<List<StatementSyntax>, SyntaxAnnotation, SyntaxNode> _Refactor;

		ReplaceStatements(Func<List<StatementSyntax>, SyntaxAnnotation, SyntaxNode> refactor) {
			_Refactor = refactor;
		}

		public void Refactor(SemanticContext context) {
			var view = context.View;
			var statements = context.Compilation.GetStatements(view.FirstSelectionSpan().ToTextSpan());
			var start = view.Selection.StreamSelectionSpan.Start.Position;
			SyntaxAnnotation annStatement = new SyntaxAnnotation(),
				annSelect = new SyntaxAnnotation();
			var first = statements[0];
			if (first.HasLeadingTrivia) {
				statements[0] = first.WithoutLeadingTrivia();
			}
			SyntaxNode statement = _Refactor(statements, annSelect)
				.WithAdditionalAnnotations(annStatement);
			var root = first.SyntaxTree.GetRoot()
				.ReplaceNode(first, statement);
			statement = root.Format(context.Workspace)
				.GetAnnotatedNodes(annStatement)
				.First();
			view.Edit(
				(rep: statement.ToString(), sel: view.FirstSelectionSpan()),
				(v, p, edit) => edit.Replace(p.sel, p.rep)
			);
			var selSpan = statement.GetAnnotatedNodes(annSelect).First().Span;
			view.SelectSpan(start.Position + (selSpan.Start - statement.SpanStart), selSpan.Length, 1);
		}
	}
}
