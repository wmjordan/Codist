using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Codist
{
	readonly struct SelectedStatementInfo
	{
		public readonly StatementSyntax Preceding, Following;
		public readonly List<StatementSyntax> Statements;

		public SelectedStatementInfo(StatementSyntax preceding, List<StatementSyntax> statements, StatementSyntax following) {
			Preceding = preceding;
			Statements = statements;
			Following = following;
		}

		public TextSpan FullSpan => Statements?.Count > 0
			? TextSpan.FromBounds(Statements[0].FullSpan.Start, Statements[Statements.Count - 1].FullSpan.End)
			: default;
	}
}
