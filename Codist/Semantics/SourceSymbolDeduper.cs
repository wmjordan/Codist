using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Codist
{
	sealed class SourceSymbolDeduper
	{
		readonly HashSet<ISymbol> _Symbols = new HashSet<ISymbol>(SourceSymbolComparer.Instance);

		public bool Exists(ISymbol symbol) {
			return _Symbols.Contains(symbol);
		}
		public bool TryAdd(ISymbol symbol) {
			return _Symbols.Add(symbol);
		}

		sealed class SourceSymbolComparer : IEqualityComparer<ISymbol>
		{
			public static readonly SourceSymbolComparer Instance = new SourceSymbolComparer();

			private SourceSymbolComparer() {}

			public bool Equals(ISymbol x, ISymbol y) {
				var rx = x.DeclaringSyntaxReferences;
				var ry = y.DeclaringSyntaxReferences;
				SyntaxReference xr, yr;
				return rx.Length == ry.Length
					&& rx.Length > 0
					&& (xr = rx[0]).Span == (yr = ry[0]).Span
					&& xr.SyntaxTree.FilePath == yr.SyntaxTree.FilePath;
			}

			public int GetHashCode(ISymbol symbol) {
				SyntaxReference r;
				if (symbol.HasSource() == false
					|| (r = symbol.DeclaringSyntaxReferences[0]) == null) {
					return symbol.GetHashCode();
				}
				return unchecked(r.Span.GetHashCode() * 99577 + r.SyntaxTree.FilePath.GetHashCode());
			}
		}
	}
}
