using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
{
	static class SymbolMarkManager
	{
		static readonly ConcurrentDictionary<IBookmarkedSymbol, ClassificationTag> _Bookmarks = new ConcurrentDictionary<IBookmarkedSymbol, ClassificationTag>(new SymbolComparer());

		internal static IEnumerable<IBookmarkedSymbol> MarkedSymbols => _Bookmarks.Keys;
		internal static bool HasBookmark => _Bookmarks.IsEmpty == false;

		internal static bool CanBookmark(ISymbol symbol) {
			symbol = symbol.GetAliasTarget();
			switch (symbol.Kind) {
				case SymbolKind.Event:
				case SymbolKind.Field:
				case SymbolKind.Local:
				case SymbolKind.Method:
				case SymbolKind.NamedType:
				case SymbolKind.Property:
				case SymbolKind.Parameter:
					return true;
			}
			return false;
		}
		internal static ClassificationTag GetSymbolMarkerStyle(ISymbol symbol) {
			return _Bookmarks.TryGetValue(new WrappedSymbol(symbol), out var result) ? result : null;
		}
		internal static void Clear() {
			_Bookmarks.Clear();
		}
		internal static bool Contains(ISymbol symbol) {
			return _Bookmarks.ContainsKey(new WrappedSymbol(symbol));
		}
		internal static void Update(ISymbol symbol, ClassificationTag classificationType) {
			_Bookmarks[new BookmarkedSymbol(symbol)] = classificationType;
		}
		internal static bool Remove(IBookmarkedSymbol symbol) {
			return _Bookmarks.TryRemove(symbol, out _);
		}
		internal static bool Remove(ISymbol symbol) {
			return _Bookmarks.TryRemove(new WrappedSymbol(symbol), out _);
		}

		static int GetHashCode(IBookmarkedSymbol symbol) {
			const int M = -1521134295;
			return symbol.Name.GetHashCode() + ((int)symbol.Kind * M);
		}
		static int GetHashCode(IBookmarkedSymbolType symbol) {
			return symbol.Name.GetHashCode() + ((int)symbol.TypeKind * 17);
		}

		sealed class BookmarkedSymbol : IBookmarkedSymbol, IEquatable<IBookmarkedSymbol>
		{
			public string Name { get; }
			public SymbolKind Kind { get; }
			public IBookmarkedSymbolType ContainingType { get; }
			public IBookmarkedSymbolType MemberType { get; }
			public int ImageId { get; }
			public string DisplayString { get; }
			//public SyntaxReference Reference { get; }

			public BookmarkedSymbol(ISymbol symbol) {
				Name = symbol.Name;
				Kind = symbol.Kind;
				ContainingType = symbol.ContainingType != null
					? new BookmarkedSymbolType(symbol.ContainingType)
					: EmptyBookmarkedSymbolType.Instance;
				MemberType = symbol.GetReturnType() is INamedTypeSymbol rt
					? new BookmarkedSymbolType(rt)
					: EmptyBookmarkedSymbolType.Instance;
				ImageId = symbol.GetImageId();
				DisplayString = symbol.ToDisplayString();
				//Reference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
			}

			public bool Equals(IBookmarkedSymbol other) {
				return other != null && other.Kind == Kind && other.Name == Name
					&& (ContainingType == other.ContainingType || ContainingType != null && other.ContainingType != null && ContainingType.Equals(other.ContainingType))
					&& (MemberType == other.MemberType || MemberType != null && other.MemberType != null && MemberType.Equals(other.MemberType));
			}
			public override bool Equals(object obj) {
				return Equals(obj as IBookmarkedSymbol);
			}
			public override int GetHashCode() {
				return SymbolMarkManager.GetHashCode(this);
			}
			public override string ToString() {
				return DisplayString;
			}
		}
		sealed class EmptyBookmarkedSymbolType : IBookmarkedSymbolType
		{
			public string Name => String.Empty;
			public int Arity => 0;
			public TypeKind TypeKind => TypeKind.Unknown;

			public static readonly IBookmarkedSymbolType Instance = new EmptyBookmarkedSymbolType();
			private EmptyBookmarkedSymbolType() {}
		}
		sealed class BookmarkedSymbolType : IEquatable<IBookmarkedSymbolType>, IBookmarkedSymbolType
		{
			public string Name { get; }
			public int Arity { get; }
			public TypeKind TypeKind { get; }

			public BookmarkedSymbolType (INamedTypeSymbol symbol) {
				Arity = symbol.Arity;
				Name = symbol.Name;
				TypeKind = symbol.TypeKind;
			}

			public bool Equals(IBookmarkedSymbolType other) {
				return other != null && other.TypeKind == TypeKind && other.Arity == Arity && other.Name == Name;
			}
			public override bool Equals(object obj) {
				return Equals(obj as IBookmarkedSymbolType);
			}
			public override int GetHashCode() {
				return SymbolMarkManager.GetHashCode(this);
			}
			public override string ToString() {
				return Name;
			}
		}

		sealed class WrappedSymbol : IBookmarkedSymbol
		{
			readonly ISymbol _Symbol;
			IBookmarkedSymbolType _ContainingType, _MemberType;

			public WrappedSymbol(ISymbol symbol) {
				_Symbol = symbol;
			}

			public string Name => _Symbol.Name;
			public SymbolKind Kind => _Symbol.Kind;
			public Accessibility Accessibility => _Symbol.DeclaredAccessibility;
			public IBookmarkedSymbolType ContainingType => _ContainingType ?? (_ContainingType = _Symbol.ContainingType != null ? new WrappedSymbolType(_Symbol.ContainingType) : EmptyBookmarkedSymbolType.Instance);
			public IBookmarkedSymbolType MemberType {
				get {
					return _MemberType ?? (_MemberType = _Symbol.GetReturnType() is INamedTypeSymbol t
						? new WrappedSymbolType(t)
						: EmptyBookmarkedSymbolType.Instance);
				}
			}
			public int ImageId => _Symbol.GetImageId();
			public string DisplayString => _Symbol.ToDisplayString(CodeAnalysisHelper.MemberNameFormat);
			//public SyntaxReference Reference => _Symbol.DeclaringSyntaxReferences.FirstOrDefault();

			public bool Equals(IBookmarkedSymbol other) {
				return other != null && other.Kind == Kind && other.Name == Name
					&& (ContainingType == other.ContainingType || ContainingType != null && other.ContainingType != null && ContainingType.Equals(other.ContainingType))
					&& (MemberType == other.MemberType || MemberType != null && other.MemberType != null && MemberType.Equals(other.MemberType));
			}

			public override int GetHashCode() {
				return SymbolMarkManager.GetHashCode(this);
			}
			public override string ToString() {
				return "(" + DisplayString + ")";
			}
		}
		sealed class WrappedSymbolType : IBookmarkedSymbolType
		{
			readonly INamedTypeSymbol _Symbol;

			public WrappedSymbolType(INamedTypeSymbol symbol) {
				_Symbol = symbol;
			}

			public string Name => _Symbol.Name;
			public int Arity => _Symbol.Arity;
			public TypeKind TypeKind => _Symbol.TypeKind;
			public override int GetHashCode() {
				return SymbolMarkManager.GetHashCode(this);
			}
			public override string ToString() {
				return "(" + Name + ")";
			}
		}
		/// <summary>
		/// Implements a loose <see cref="IEqualityComparer{T}"/> of <see cref="ISymbol"/> which compares <see cref="ISymbol.Kind"/>, <see cref="ISymbol.DeclaredAccessibility"/>, <see cref="ISymbol.Name"/> and <see cref="ISymbol.ContainingType"/> only.
		/// </summary>
		sealed class SymbolComparer : IEqualityComparer<IBookmarkedSymbol>
		{
			public bool Equals(IBookmarkedSymbol x, IBookmarkedSymbol y) {
				return x.Equals(y);
			}
			public int GetHashCode(IBookmarkedSymbol obj) {
				return SymbolMarkManager.GetHashCode(obj);
			}
		}
	}
}
