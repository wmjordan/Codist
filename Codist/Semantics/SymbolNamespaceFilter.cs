using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Codist;

readonly struct SymbolNamespaceFilter(ISymbol symbol)
{
	public INamespaceSymbol Namespace { get; } = symbol is INamespaceSymbol ns ? ns : symbol.ContainingNamespace;
	public bool HasFilter => Namespace != null;

	public IEnumerable<TSymbol> Filter<TSymbol>(IEnumerable<TSymbol> symbols) where TSymbol : ISymbol {
		return HasFilter
			? symbols.Where(Filter)
			: symbols;
	}
	public bool Filter<TSymbol>(TSymbol target) where TSymbol : ISymbol {
		if (target is INamespaceSymbol ns) {
			if (Namespace.Equals(ns)) {
				return true;
			}
		}
		ns = target.ContainingNamespace;
		do {
			if (Namespace.Equals(ns)) {
				return true;
			}
		}
		while ((ns = ns.ContainingNamespace) != null);
		return false;
	}
}
