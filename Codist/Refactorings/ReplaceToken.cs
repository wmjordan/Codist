using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.Text;
using R = Codist.Properties.Resources;

namespace Codist.Refactorings
{
	abstract class ReplaceToken : IRefactoring
	{
		public static readonly ReplaceToken InvertOperator = new InvertOperatorRefactoring();

		public abstract int IconId { get; }
		public abstract string Title { get; }

		public abstract bool Accept(RefactoringContext ctx);
		protected abstract string GetReplacement(SemanticContext ctx, SyntaxToken token);

		public void Refactor(SemanticContext ctx) {
			var view = ctx.View;
			var token = ctx.Token;
			var rep = GetReplacement(ctx, token);
			view.Edit(
				(rep, sel: token.Span.ToSpan()),
				(v, p, edit) => edit.Replace(p.sel, p.rep)
			);
			view.MoveCaret(token.SpanStart);
			view.Selection.Select(new SnapshotSpan(view.TextSnapshot, token.SpanStart, rep.Length), false);
		}

		sealed class InvertOperatorRefactoring : ReplaceToken
		{
			public override int IconId => IconIds.InvertOperator;
			public override string Title => R.CMD_InvertOperator;

			public override bool Accept(RefactoringContext ctx) {
				switch (ctx.Token.Kind()) {
					case SyntaxKind.EqualsEqualsToken:
					case SyntaxKind.ExclamationEqualsToken:
					case SyntaxKind.AmpersandAmpersandToken:
					case SyntaxKind.BarBarToken:
					case SyntaxKind.MinusMinusToken:
					case SyntaxKind.PlusPlusToken:
					case SyntaxKind.LessThanToken:
					case SyntaxKind.GreaterThanToken:
					case SyntaxKind.LessThanEqualsToken:
					case SyntaxKind.GreaterThanEqualsToken:
					case SyntaxKind.PlusToken:
					case SyntaxKind.MinusToken:
					case SyntaxKind.AsteriskToken:
					case SyntaxKind.SlashToken:
					case SyntaxKind.AmpersandToken:
					case SyntaxKind.BarToken:
					case SyntaxKind.LessThanLessThanToken:
					case SyntaxKind.GreaterThanGreaterThanToken:
					case SyntaxKind.PlusEqualsToken:
					case SyntaxKind.MinusEqualsToken:
					case SyntaxKind.AsteriskEqualsToken:
					case SyntaxKind.SlashEqualsToken:
					case SyntaxKind.LessThanLessThanEqualsToken:
					case SyntaxKind.GreaterThanGreaterThanEqualsToken:
					case SyntaxKind.AmpersandEqualsToken:
					case SyntaxKind.BarEqualsToken:
						return true;
				}
				return false;
			}

			protected override string GetReplacement(SemanticContext ctx, SyntaxToken token) {
				switch (token.Kind()) {
					case SyntaxKind.EqualsEqualsToken: return "!=";
					case SyntaxKind.ExclamationEqualsToken: return "==";
					case SyntaxKind.AmpersandAmpersandToken: return "||";
					case SyntaxKind.BarBarToken: return "&&";
					case SyntaxKind.MinusMinusToken: return "++";
					case SyntaxKind.PlusPlusToken: return "--";
					case SyntaxKind.LessThanToken: return ">=";
					case SyntaxKind.GreaterThanToken: return "<=";
					case SyntaxKind.LessThanEqualsToken: return ">";
					case SyntaxKind.GreaterThanEqualsToken: return "<";
					case SyntaxKind.PlusToken: return "-";
					case SyntaxKind.MinusToken: return "+";
					case SyntaxKind.AsteriskToken: return "/";
					case SyntaxKind.SlashToken: return "*";
					case SyntaxKind.AmpersandToken: return "|";
					case SyntaxKind.BarToken: return "&";
					case SyntaxKind.LessThanLessThanToken: return ">>";
					case SyntaxKind.GreaterThanGreaterThanToken: return "<<";
					case SyntaxKind.PlusEqualsToken: return "-=";
					case SyntaxKind.MinusEqualsToken: return "+=";
					case SyntaxKind.AsteriskEqualsToken: return "/=";
					case SyntaxKind.SlashEqualsToken: return "*=";
					case SyntaxKind.LessThanLessThanEqualsToken: return ">>=";
					case SyntaxKind.GreaterThanGreaterThanEqualsToken: return "<<=";
					case SyntaxKind.AmpersandEqualsToken: return "|=";
					case SyntaxKind.BarEqualsToken: return "&=";
				}
				return null;
			}
		}
	}
}
