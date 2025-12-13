using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Codist
{
	/// <summary>
	/// Denotes filter for symbols should or should not comes from source code.
	/// </summary>
	public readonly struct SymbolSourceFilter : IEquatable<SymbolSourceFilter>
	{
		readonly int _Value;

		public static readonly SymbolSourceFilter RequiresSource = new SymbolSourceFilter(1);
		public static readonly SymbolSourceFilter ExcludesSource = new SymbolSourceFilter(-1);
		public static readonly SymbolSourceFilter Default = default;

		SymbolSourceFilter(int value) {
			_Value = value;
		}
		public bool HasFilter => _Value != 0;

		public bool Match(ISymbol symbol) {
			return _Value == 0 || ((symbol.HasSource() ? 1 : -1) ^ _Value) >= 0;
		}
		public bool Mismatch(ISymbol symbol) {
			return _Value != 0 && ((symbol.HasSource() ? 1 : -1) ^ _Value) < 0;
		}

        public IEnumerable<TSymbol> Filter<TSymbol>(IEnumerable<TSymbol> overrides)
			where TSymbol : ISymbol {
			return _Value == 1 ? overrides.Where(s => s.HasSource())
				: _Value == -1 ? overrides.Where(s => !s.HasSource())
				: overrides;
		}

		public override bool Equals(object obj) {
			return obj is SymbolSourceFilter filter && Equals(filter);
		}

		public bool Equals(SymbolSourceFilter other) {
			return _Value == other._Value;
		}

		public override int GetHashCode() {
			return _Value;
		}

        public static bool operator ==(SymbolSourceFilter left, SymbolSourceFilter right) {
			return left.Equals(right);
		}

		public static bool operator !=(SymbolSourceFilter left, SymbolSourceFilter right) {
			return !(left == right);
		}
	}
}
