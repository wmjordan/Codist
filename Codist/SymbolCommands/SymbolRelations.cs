using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Codist.SymbolCommands
{
	sealed class SymbolRelations<TKeySymbol, TRelSymbol> where TKeySymbol : ISymbol where TRelSymbol : ISymbol
	{
		readonly Dictionary<TKeySymbol, List<TRelSymbol>> _Relations;

		public SymbolRelations() {
			_Relations = new(7);
		}
		public SymbolRelations(IEqualityComparer<TKeySymbol> comparer) {
			_Relations = new(7, comparer);
		}

		public ICollection<TKeySymbol> KeySymbols => _Relations.Keys;

		public bool HasRelation(TKeySymbol baseType) {
			return _Relations.ContainsKey(baseType); 
		}
		public IReadOnlyList<TRelSymbol> GetRelations(TKeySymbol baseType) {
			_Relations.TryGetValue(baseType, out var types);
			return types;
		}
		public void SetRelations(TKeySymbol baseType, IEnumerable<TRelSymbol> relSymbols) {
			_Relations[baseType] = new(relSymbols);
		}
		public void SetEmpty(TKeySymbol baseType) {
			_Relations[baseType] = null;
		}

		public bool Add(TKeySymbol baseType, TRelSymbol relatedType) {
			if (_Relations.TryGetValue(baseType, out var list)) {
				list.Add(relatedType);
				return false;
			}
			_Relations[baseType] = [relatedType];
			return true;
		}

		public bool AddRange(TKeySymbol baseType, IEnumerable<TRelSymbol> relatedTypes) {
			if (_Relations.TryGetValue(baseType, out var list)) {
				list.AddRange(relatedTypes);
				return false;
			}
			_Relations[baseType] = new(relatedTypes);
			return true;
		}

		internal bool AddNew(TKeySymbol baseType, TRelSymbol relatedType) {
			if (_Relations.TryGetValue(baseType, out var list)) {
				if (list.Contains(relatedType)) {
					return false;
				}
				list.Add(relatedType);
				return true;
			}
			_Relations[baseType] = [relatedType];
			return true;
		}
	}
}
