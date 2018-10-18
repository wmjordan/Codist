using System;
using Microsoft.CodeAnalysis;

namespace Codist.Classifiers
{
	/// <summary>
	/// A symbol temporarily bookmarked by <see cref="SymbolMarkManager"/> or <see cref="NaviBar.CSharpBar"/>.
	/// </summary>
	interface IBookmarkedSymbol : IEquatable<IBookmarkedSymbol>
	{
		string Name { get; }
		SymbolKind Kind { get; }
		IBookmarkedSymbolType ContainingType { get; }
		IBookmarkedSymbolType MemberType { get; }
		int ImageId { get; }
		string DisplayString { get; }
	}

	/// <summary>
	/// The type of a <see cref="IBookmarkedSymbol"/>.
	/// </summary>
	interface IBookmarkedSymbolType
	{
		string Name { get; }
		int Arity { get; }
		TypeKind TypeKind { get; }
	}
}
