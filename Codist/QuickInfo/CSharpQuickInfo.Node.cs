using System;
using System.Collections.Generic;
using CLR;
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
			{ SyntaxKind.MethodDeclaration, ShowControlFlowInfo },
			{ SyntaxKind.OperatorDeclaration, ShowControlFlowInfo },
			{ SyntaxKind.ConversionOperatorDeclaration, ShowControlFlowInfo },
			{ SyntaxKind.ConstructorDeclaration, ShowControlFlowInfo },
			{ SyntaxKind.DestructorDeclaration, ShowControlFlowInfo },
			{ SyntaxKind.GetAccessorDeclaration, ShowControlFlowInfo },
			{ SyntaxKind.SetAccessorDeclaration, ShowControlFlowInfo },
			{ SyntaxKind.AddAccessorDeclaration, ShowControlFlowInfo },
			{ SyntaxKind.RemoveAccessorDeclaration, ShowControlFlowInfo },
			{ SyntaxKind.LocalFunctionStatement, ShowControlFlowInfo },
			{ SyntaxKind.SimpleLambdaExpression, ShowControlFlowInfo },
			{ SyntaxKind.ParenthesizedLambdaExpression, ShowControlFlowInfo },
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
			ctx.Container.Add(new GeneralInfoBlock(new BlockItem(IconIds.Switch, tip)));
			ShowBlockInfo(ctx);
			ShowControlFlowInfo(ctx);
			ctx.State = State.Return;
		}

		static void ShowControlFlowInfo(Context ctx) {
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.ControlFlow) == false) {
				return;
			}
			var node = ctx.node ?? (ctx.node = ctx.token.Parent);
			int returns = 0, throws = 0, yieldReturns = 0, yieldBreaks = 0, jump = 0;
			if (node.RawKind.CeqAny(SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.LocalFunctionStatement, SyntaxKind.AnonymousMethodExpression)) {
				GetAnalysisRange(ref node);
			}
			var k = node.RawKind;
			foreach (var item in node.DescendantNodes(n => n.RawKind.CeqAny(SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.LocalFunctionStatement, SyntaxKind.AnonymousMethodExpression, SyntaxKind.AttributeList) == false)) {
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
					case SyntaxKind.GotoStatement:
					case SyntaxKind.GotoCaseStatement:
					case SyntaxKind.GotoDefaultStatement:
					case SyntaxKind.ContinueStatement:
						jump++;
						break;
					case SyntaxKind.BreakStatement:
						// do not count if breaking from switch section
						if (item.Parent.FirstAncestorOrSelf<SyntaxNode>(n => n.RawKind.CeqAny(SyntaxKind.SwitchSection, SyntaxKind.ForEachStatement, SyntaxKind.ForEachVariableStatement, SyntaxKind.ForStatement, SyntaxKind.WhileStatement, SyntaxKind.DoStatement)).IsKind(SyntaxKind.SwitchSection) == false) {
							jump++;
						}
						break;
				}
			}
			if (returns == 0 && throws == 0 && yieldBreaks == 0 && yieldReturns == 0 && jump == 0) {
				return;
			}

			var t = new GeneralInfoBlock(IconIds.ControlFlow, R.T_ControlFlow);
			if (returns != 0) {
				t.Add(new BlockItem(IconIds.Return, "Return: ").Append(returns.ToText(), __SymbolFormatter.Number));
			}
			if (throws != 0) {
				t.Add(new BlockItem(IconIds.Warning, "Throw: ").Append(throws.ToText(), __SymbolFormatter.Number));
			}
			if (jump != 0) {
				t.Add(new BlockItem(IconIds.GoTo, "Jump: ").Append(jump.ToText(), __SymbolFormatter.Number));
			}
			if (yieldReturns != 0) {
				t.Add(new BlockItem(IconIds.Return, "Yield return: ").Append(yieldReturns.ToText(), __SymbolFormatter.Number));
			}
			if (yieldBreaks != 0) {
				t.Add(new BlockItem(IconIds.YieldBreak, "Yield break: ").Append(yieldBreaks.ToText(), __SymbolFormatter.Number));
			}
			ctx.Container.Add(t);
		}

		static void GetAnalysisRange(ref SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.LocalFunctionStatement:
					var f = (LocalFunctionStatementSyntax)node;
					if (f.Body != null) {
						node = f.Body;
					}
					else if (f.ExpressionBody != null) {
						node = f.ExpressionBody;
					}
					return;
				case SyntaxKind.SimpleLambdaExpression:
				case SyntaxKind.ParenthesizedLambdaExpression:
					var l = (LambdaExpressionSyntax)node;
					if (l.Body != null) {
						node = l.Body;
					}
					return;
				case SyntaxKind.AnonymousMethodExpression:
					var a = (AnonymousMethodExpressionSyntax)node;
					if (a.Body != null) {
						node = a.Body;
					}
					return;
			}
		}
	}
}
