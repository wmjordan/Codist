using System;
using System.Collections.Generic;
using System.Linq;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using R = Codist.Properties.Resources;

namespace Codist.QuickInfo
{
	partial class CSharpQuickInfo
	{
		static readonly Dictionary<SyntaxKind, Action<Context>> __NodeProcessors = new Dictionary<SyntaxKind, Action<Context>> {
			{ SyntaxKind.Block, ShowBlockInfo },
			{ SyntaxKind.SwitchStatement, ProcessSwitchStatementNode },
			{ SyntaxKind.MethodDeclaration, ProcessMethodBody },
			{ SyntaxKind.OperatorDeclaration, ProcessMethodBody },
			{ SyntaxKind.ConversionOperatorDeclaration, ProcessMethodBody },
			{ SyntaxKind.ConstructorDeclaration, ProcessMethodBody },
			{ SyntaxKind.DestructorDeclaration, ProcessMethodBody },
			{ SyntaxKind.GetAccessorDeclaration, ProcessMethodBody },
			{ SyntaxKind.SetAccessorDeclaration, ProcessMethodBody },
			{ SyntaxKind.AddAccessorDeclaration, ProcessMethodBody },
			{ SyntaxKind.RemoveAccessorDeclaration, ProcessMethodBody },
			{ SyntaxKind.LocalFunctionStatement, ProcessMethodBody },
			{ SyntaxKind.SimpleLambdaExpression, ProcessMethodBody },
			{ SyntaxKind.ParenthesizedLambdaExpression, ProcessMethodBody },
			{ SyntaxKind.Argument, ProcessArgumentNode },
			{ SyntaxKind.ArgumentList, ProcessArgumentNode },
			{ SyntaxKind.LetClause, ProcessLinqNode },
			{ SyntaxKind.JoinClause, ProcessLinqNode },
			{ SyntaxKind.JoinIntoClause, ProcessLinqNode },
			{ SyntaxKind.EqualsExpression, ProcessOperatorExpression },
			{ SyntaxKind.NotEqualsExpression, ProcessOperatorExpression },
			{ SyntaxKind.LeftShiftExpression, ProcessOperatorExpression },
			{ SyntaxKind.RightShiftExpression, ProcessOperatorExpression },
			{ SyntaxKind.ExclusiveOrExpression, ProcessOperatorExpression },
			{ SyntaxKind.AddExpression, ProcessOperatorExpression },
			{ SyntaxKind.SubtractExpression, ProcessOperatorExpression },
			{ SyntaxKind.MultiplyExpression, ProcessOperatorExpression },
			{ SyntaxKind.DivideExpression, ProcessOperatorExpression },
			{ SyntaxKind.ModuloExpression, ProcessOperatorExpression },
			{ SyntaxKind.BitwiseNotExpression, ProcessOperatorExpression },
			{ SyntaxKind.BitwiseAndExpression, ProcessOperatorExpression },
			{ SyntaxKind.BitwiseOrExpression, ProcessOperatorExpression },
			{ SyntaxKind.GreaterThanExpression, ProcessOperatorExpression },
			{ SyntaxKind.GreaterThanOrEqualExpression, ProcessOperatorExpression },
			{ SyntaxKind.LessThanExpression, ProcessOperatorExpression },
			{ SyntaxKind.LessThanOrEqualExpression, ProcessOperatorExpression },
		};

		static void ProcessOperatorExpression(Context ctx) {
			if (ctx.node is BinaryExpressionSyntax) {
				ctx.symbol = ctx.semanticModel.GetSymbolInfo(ctx.node, ctx.cancellationToken).Symbol;
			}
		}

		static void ProcessArgumentNode(Context ctx) {
			LocateNodeInParameterList(ctx);
		}

		static void ProcessLinqNode(Context ctx) {
			if (ctx.node.GetIdentifierToken().FullSpan == ctx.token.FullSpan) {
				ctx.symbol = ctx.semanticModel.GetDeclaredSymbol(ctx.node, ctx.cancellationToken);
			}
		}

		static void ProcessSwitchStatementNode(Context ctx) {
			ShowBlockInfo(ctx);
			var node = ctx.node;
			var sections = ((SwitchStatementSyntax)node).Sections;
			var c = sections.Count;
			string tip;
			switch (c) {
				case 0: return;
				case 1:
					c = sections[0].Labels.Count;
					if (c < 2) {
						return;
					}
					tip = R.T_1SectionCases.Replace("<C>", c.ToText());
					break;
				default:
					var cases = 0;
					foreach (var section in sections) {
						cases += section.Labels.Count;
					}
					tip = R.T_SectionsCases.Replace("<C>", c.ToText()).Replace("<S>", cases.ToText());
					break;
			}
			ctx.Container.Add(new ThemedTipText(tip).SetGlyph(IconIds.Switch));
		}

		static void ProcessMethodBody(Context ctx) {
			BlockSyntax block;
			var node = ctx.node;
			if (node is BaseMethodDeclarationSyntax m) {
				block = m.Body;
			}
			else if (node is AccessorDeclarationSyntax a) {
				block = a.Body;
			}
			else if (node is LocalFunctionStatementSyntax l) {
				block = l.Body;
			}
			else if (node is ParenthesizedLambdaExpressionSyntax pl) {
				block = pl.Body as BlockSyntax;
			}
			else if (node is SimpleLambdaExpressionSyntax sl) {
				block = sl.Body as BlockSyntax;
			}
			else {
				return;
			}
			if (block == null) {
				return;
			}

			int returns = 0, throws = 0, yieldReturns = 0, yieldBreaks = 0;
			foreach (var item in block.DescendantNodes(n => n.IsAnyKind(SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.LocalFunctionStatement, SyntaxKind.AnonymousMethodExpression) == false)) {
				switch (item.Kind()) {
					case SyntaxKind.ReturnStatement:
						returns++;
						break;
					case SyntaxKind.ThrowStatement:
					case SyntaxKind.ThrowExpression:
						throws++;
						break;
					case SyntaxKind.YieldBreakStatement:
						yieldBreaks++;
						break;
					case SyntaxKind.YieldReturnStatement:
						yieldReturns++;
						break;
				}
			}
			if (returns == 0 && throws == 0 && yieldBreaks == 0 && yieldReturns == 0) {
				return;
			}

			var t = new GeneralInfoBlock(IconIds.ReturnValue, "Exit points");
			if (returns != 0) {
				t.AddBlock(new BlockItem(IconIds.Return, "Return: ").Append(returns.ToText(), __SymbolFormatter.Number));
			}
			if (throws != 0) {
				t.AddBlock(new BlockItem(IconIds.Warning, "Throw: ").Append(throws.ToText(), __SymbolFormatter.Number));
			}
			if (yieldReturns != 0) {
				t.AddBlock(new BlockItem(IconIds.Return, "Yield return: ").Append(yieldReturns.ToText(), __SymbolFormatter.Number));
			}
			if (yieldBreaks != 0) {
				t.AddBlock(new BlockItem(IconIds.YieldBreak, "Yield break: ").Append(yieldBreaks.ToText(), __SymbolFormatter.Number));
			}
			ctx.Container.Add(t);
		}
	}
}
