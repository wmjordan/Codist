using System;
using System.Collections.Generic;
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
			{ SyntaxKind.Argument, ProcessArgumentNode },
			{ SyntaxKind.ArgumentList, ProcessArgumentNode },
			{ SyntaxKind.LetClause, ProcessLinqNode },
			{ SyntaxKind.JoinClause, ProcessLinqNode },
			{ SyntaxKind.JoinIntoClause, ProcessLinqNode },
		};

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
	}
}
