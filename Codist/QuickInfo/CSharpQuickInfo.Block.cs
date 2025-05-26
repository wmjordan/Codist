using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using CLR;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using R = Codist.Properties.Resources;

namespace Codist.QuickInfo
{
	partial class CSharpQuickInfo
	{
		static void ShowBlockInfo(Context ctx) {
			var node = ctx.node;
			var container = ctx.Container;
			var textSnapshot = ctx.CurrentSnapshot;
			if (node.Kind().CeqAny(SyntaxKind.ArrayInitializerExpression,
						SyntaxKind.CollectionInitializerExpression,
						SyntaxKind.ComplexElementInitializerExpression,
						SyntaxKind.ObjectInitializerExpression,
						CodeAnalysisHelper.WithInitializerExpression)) {
				container.Add(new BlockItem(IconIds.InstanceMember, R.T_ExpressionCount)
					.Append(((InitializerExpressionSyntax)node).Expressions.Count.ToText(), true, __SymbolFormatter.Number));
			}
			else if (node.IsKind(CodeAnalysisHelper.PropertyPatternClause)) {
				container.Add(new BlockItem(IconIds.InstanceMember, R.T_SubPatternCount)
					.Append(((CSharpSyntaxNode)node).GetPropertyPatternSubPatternsCount().ToText(), true, __SymbolFormatter.Number)
					);
			}
			var lines = textSnapshot.GetLineSpan(node.Span).Length + 1;
			if (lines > 1) {
				container.Add(
					lines > 100
						? new BlockItem(IconIds.LineOfCode, lines + R.T_Lines, true)
						: new BlockItem(IconIds.LineOfCode, lines + R.T_Lines)
					);
			}
			if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.DataFlow) == false
				|| (node is StatementSyntax || node is ExpressionSyntax || node is ConstructorInitializerSyntax) == false) {
				return;
			}
			var df = ctx.semanticModel.AnalyzeDataFlow(node);
			if (df.Succeeded) {
				ShowDataFlowAnalysis(ctx, df);
			}
		}

		static void ShowDataFlowAnalysis(Context ctx, DataFlowAnalysis df) {
			var node = ctx.node;
			var infoBlock = new GeneralInfoBlock(IconIds.DataFlow, R.T_DataFlow);
			ListVariables(infoBlock, df.VariablesDeclared, R.T_DeclaredVariable, IconIds.DeclaredVariables);
			var readVars = df.ReadInside;
			if (node.Parent.Kind().IsMethodDeclaration()) {
				readVars = readVars.RemoveRange(((IMethodSymbol)ctx.semanticModel.GetSymbol(node.Parent)).Parameters);
			}
			ListVariables(infoBlock, readVars, R.T_ReadVariable, IconIds.ReadVariables);
			ListVariables(infoBlock, df.WrittenInside, R.T_WrittenVariable, IconIds.WrittenVariables);
			ListVariables(infoBlock, df.UnsafeAddressTaken, R.T_TakenAddress, IconIds.RefVariables);
			ListVariables(infoBlock, df.CapturedInside, R.T_CapturedVariable, IconIds.CapturedVariables);
			if (infoBlock.HasItem) {
				ctx.Container.Add(infoBlock);
			}
		}

		static void ListVariables(GeneralInfoBlock container, ImmutableArray<ISymbol> variables, string title, int icon) {
			if (variables.IsEmpty) {
				return;
			}
			var p = new BlockItem(icon, title)
				.Append(variables.Length.ToText(), true, __SymbolFormatter.Number)
				.AppendLine();
			bool s = false;
			foreach (var item in variables) {
				if (s) {
					p.Append(", ");
				}
				if (item.IsImplicitlyDeclared) {
					p.AddSymbol(item.GetReturnType(), item.Name);
				}
				else {
					p.AddSymbol(item, false);
				}
				s = true;
			}
			container.Add(p);
		}

		static void ShowCapturedVariables(SyntaxNode node, ISymbol symbol, SemanticModel semanticModel, ThemedTipDocument tip, CancellationToken cancellationToken) {
			if (node is LambdaExpressionSyntax
				|| (symbol as IMethodSymbol)?.MethodKind == MethodKind.LocalFunction) {
				var ss = node is LambdaExpressionSyntax
					? node.AncestorsAndSelf().FirstOrDefault(i => i is StatementSyntax || i is ExpressionSyntax && i.IsKind(SyntaxKind.IdentifierName) == false)
					: symbol.GetSyntaxNode(cancellationToken);
				if (ss != null) {
					var captured = semanticModel.GetCapturedVariables(ss);
					if (captured.Length > 0) {
						var p = new ThemedTipParagraph(IconIds.CapturedVariables, new ThemedTipText().Append(R.T_CapturedVariables, true));
						int i = 0;
						foreach (var item in captured) {
							p.Content.Append(++i == 1 ? ": " : ", ").AddSymbol(item, false, __SymbolFormatter);
						}
						tip.Append(p);
					}
				}
			}
		}
	}
}
