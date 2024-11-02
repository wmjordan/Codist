using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Text;
using R = Codist.Properties.Resources;

namespace Codist.QuickInfo
{
	partial class CSharpQuickInfo
	{
		static void ShowBlockInfo(InfoContainer qiContent, ITextSnapshot textSnapshot, SyntaxNode node, SemanticModel semanticModel) {
			var lines = textSnapshot.GetLineSpan(node.Span).Length + 1;
			if (lines > 1) {
				qiContent.Add(
					(lines > 100 ? new ThemedTipText(lines + R.T_Lines, true) : new ThemedTipText(lines + R.T_Lines))
						.SetGlyph(IconIds.LineOfCode)
					);
			}
			if ((node is StatementSyntax || node is ExpressionSyntax || node is ConstructorInitializerSyntax) == false) {
				return;
			}
			var df = semanticModel.AnalyzeDataFlow(node);
			if (df.Succeeded) {
				ListVariables(qiContent, df.VariablesDeclared, R.T_DeclaredVariable, IconIds.DeclaredVariables);
				ListVariables(qiContent, df.DataFlowsIn, R.T_ReadVariable, IconIds.ReadVariables);
				ListVariables(qiContent, df.DataFlowsOut, R.T_WrittenVariable, IconIds.WrittenVariables);
				ListVariables(qiContent, df.UnsafeAddressTaken, R.T_TakenAddress, IconIds.RefVariables);
			}

			void ListVariables(InfoContainer container, ImmutableArray<ISymbol> variables, string title, int icon) {
				if (variables.IsEmpty) {
					return;
				}
				var p = new ThemedTipText(title, true).Append(variables.Length).AppendLine();
				bool s = false;
				foreach (var item in variables) {
					if (s) {
						p.Append(", ");
					}
					if (item.IsImplicitlyDeclared) {
						p.AddSymbol(item.GetReturnType(), item.Name, __SymbolFormatter);
					}
					else {
						p.AddSymbol(item, false, __SymbolFormatter);
					}
					s = true;
				}
				container.Add(new ThemedTipDocument().Append(new ThemedTipParagraph(icon, p)));
			}
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
						var p = new ThemedTipParagraph(IconIds.ReadVariables, new ThemedTipText().Append(R.T_CapturedVariables, true));
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
