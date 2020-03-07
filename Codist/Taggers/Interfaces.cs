using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Text;

namespace Codist.Taggers
{
	interface IReuseableTagger {
		void AddRef();
		void Release();
	}

	/// <summary>Interface for text-based custom tagger.</summary>
	interface ITextTagger
	{
		StringComparison StringComparison { get; set; }
		void GetTags(string text, ref SnapshotSpan span, ICollection<TaggedContentSpan> results);
	}

	/// <summary>
	/// A symbol temporarily bookmarked by <see cref="SymbolMarkManager"/> or <see cref="NaviBar.CSharpBar"/>.
	/// </summary>
	interface IBookmarkedSymbol : IEquatable<IBookmarkedSymbol>
	{
		string Name { get; }
		SymbolKind Kind { get; }
		//SyntaxReference Reference { get; }
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
