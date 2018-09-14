using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Text.Classification;

namespace Codist.Classifiers
{
	static class SymbolMarkManager
	{
		static readonly Dictionary<ISymbol, IClassificationType> _Bookmarks = new Dictionary<ISymbol, IClassificationType>(7, new SymbolComparer());
		static readonly object _SyncRoot = new object();

		internal static IEnumerable<ISymbol> MarkedSymbols => _Bookmarks.Keys;

		internal static bool CanBookmark(ISymbol symbol) {
			symbol = symbol.GetAliasTarget();
			switch (symbol.Kind) {
				case SymbolKind.Event:
				case SymbolKind.Field:
				case SymbolKind.Local:
				case SymbolKind.Method:
				case SymbolKind.NamedType:
				case SymbolKind.Property:
					return true;
			}
			return false;
		}
		internal static IClassificationType GetSymbolMarkerStyle(ISymbol symbol) {
			if (_Bookmarks.Count > 0 && _Bookmarks.TryGetValue(symbol, out var result)) {
				return result;
			}
			return null;
		}
		internal static void Clear() {
			lock (_SyncRoot) {
				_Bookmarks.Clear();
			}
		}
		internal static void Update(ISymbol symbol, IClassificationType classificationType) {
			lock (_SyncRoot) {
				_Bookmarks.Remove(symbol);
				_Bookmarks.Add(symbol, classificationType);
			}
		}
		internal static void Remove(ISymbol symbol) {
			lock (_SyncRoot) {
				_Bookmarks.Remove(symbol);
			}
		}

		/// <summary>
		/// Implements a loose <see cref="IEqualityComparer{T}"/> of <see cref="ISymbol"/> which compares <see cref="ISymbol.Kind"/>, <see cref="ISymbol.DeclaredAccessibility"/>, <see cref="ISymbol.Name"/> and <see cref="ISymbol.ContainingType"/> only.
		/// </summary>
		sealed class SymbolComparer : IEqualityComparer<ISymbol>
		{
			public bool Equals(ISymbol x, ISymbol y) {
				if (x == y) {
					return true;
				}
				if (x.Kind != y.Kind || x.DeclaredAccessibility != y.DeclaredAccessibility
					|| x.Name != y.Name
					|| CompareNamedType(x.ContainingType, y.ContainingType) == false) {
					return false;
				}
				switch (x.Kind) {
					case SymbolKind.Field:
						return CompareNamedType(((IFieldSymbol)x).Type as INamedTypeSymbol, ((IFieldSymbol)y).Type as INamedTypeSymbol);
					case SymbolKind.Property:
						return CompareNamedType(((IPropertySymbol)x).Type as INamedTypeSymbol, ((IPropertySymbol)y).Type as INamedTypeSymbol);
					case SymbolKind.Local:
						return CompareNamedType(((ILocalSymbol)x).Type as INamedTypeSymbol, ((ILocalSymbol)y).Type as INamedTypeSymbol);
					case SymbolKind.Method:
						var mx = (IMethodSymbol)x;
						var my = (IMethodSymbol)y;
						if (CompareNamedType(mx.ReturnType as INamedTypeSymbol, my.ReturnType as INamedTypeSymbol) == false) {
							return false;
						}
						var px = mx.Parameters;
						var py = my.Parameters;
						if (px.Length != py.Length) {
							return false;
						}
						for (int i = px.Length - 1; i >= 0; i--) {
							if (CompareType(px[i].Type, py[i].Type) == false) {
								return false;
							}
						}
						return true;
				}
				return true;
			}
			static bool CompareType(ITypeSymbol x, ITypeSymbol y) {
				if (x == null || y == null) {
					return x == y;
				}
				if (x.TypeKind != y.TypeKind
					|| x.Name != y.Name) {
					return false;
				}
				return true;
			}
			static bool CompareNamedType(INamedTypeSymbol x, INamedTypeSymbol y) {
				if (x == null || y == null) {
					return x == y;
				}
				if (x.TypeKind != y.TypeKind
					|| x.IsGenericType != y.IsGenericType
					|| x.Arity != y.Arity
					|| x.Name != y.Name) {
					return false;
				}
				return true;
			}

			public int GetHashCode(ISymbol obj) {
				const int M = 17, M2 = -1521134295;
				var c = obj.Name.GetHashCode() + ((int)obj.Kind * M2);
				c = c * M + (int)obj.DeclaredAccessibility * M2;
				return c;
			}
		}
	}
}
