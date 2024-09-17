using System;
using Microsoft.CodeAnalysis;

namespace Codist.Taggers
{
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

#if UNUSED_TAG_TREE

		class Program
		{
			static void Main(string[] args) {
				var r = new TagTree<string>();
				for (int i = 0; i < 7; i++) {
					r.Add(L(i));
				}
				foreach (TagLeaf<string> item in r.Nodes) {
					Console.WriteLine(item.Content);
				}
				r.RemoveAfter(1);
			}

			static TagLeaf<string> L(int i) {
				return new TagLeaf<string>(i.ToString(), i, 1);
			}
		}

		abstract class TagNode<T>
		{
			public TagBranch<T> Parent;
			public TagNode<T> Next;
			public int Offset;
			public abstract bool IsLeaf { get; }
			public abstract int Start { get; }
			public abstract int End { get; }

			public int TotalOffset {
				get {
					var n = Parent;
					var o = Offset;
					while (n != null) {
						o += n.Offset;
						n = n.Parent;
					}
					return o;
				}
			}

			public abstract void Update(int offset, int delta);
		}
		sealed class TagTree<T>
		{
			TagBranch<T> _Root;

			public IEnumerable<TagNode<T>> Nodes => _Root;
			public bool IsEmpty => _Root is null;

			public void Add(TagNode<T> node) {
				if (_Root == null) {
					_Root = new TagBranch<T>(node);
					return;
				}
				_Root.Add(node);
				if (_Root.Parent != null) {
					_Root = _Root.Parent;
				}
			}

			public void RemoveAfter(int offset) {
				_Root?.RemoveAfter(offset);
			}
		}

		sealed class TagBranch<T> : TagNode<T>, IEnumerable<TagNode<T>>
		{
			const int BranchCapacity = 4;

			public int NodeCount;
			public TagNode<T> First;
			public TagNode<T> Last;
			public override bool IsLeaf => false;
			public override int Start => First.TotalOffset;
			public override int End => Last.End;
			public TagLeaf<T> FirstLeaf {
				get {
					if (NodeCount == 0) {
						return null;
					}
					var b = this;
					while (b.First.IsLeaf == false) {
						b = b.First as TagBranch<T>;
					}
					return b.First as TagLeaf<T>;
				}
			}
			public TagBranch<T> Root {
				get {
					var n = this;
					while (n.Parent != null) {
						n = n.Parent;
					}
					return n;
				}
			}
			public int TotalNodeCount {
				get {
					if (First is TagBranch<T> b) {
						var c = 0;
						for (; b != null; b = b.Next as TagBranch<T>) {
							c += b.TotalNodeCount;
						}
						return c;
					}
					return NodeCount;
				}
			}

			public TagBranch(TagNode<T> node) {
				First = node;
				node.Parent = this;
				var n = 1;
				while ((node = node.Next) != null) {
					node.Parent = this;
					Last = node;
					++n;
				}
				if (Last == null) {
					Last = First;
				}
				NodeCount = n;
			}

			public void Add(TagNode<T> newNode) {
				var o = newNode.Offset -= Offset;
				if (NodeCount == 0) {
					First = Last = newNode;
					NodeCount = 1;
					return;
				}
				if (First.IsLeaf) {
					AddLeaf(newNode);
					return;
				}
				// there are branches, go deeper
				var n = First as TagBranch<T>;
				if (o < n.Offset + n.First.Offset) {
					// insert into the first branch
					n.Add(newNode);
					return;
				}
				var nn = n.Next as TagBranch<T>;
				while (nn != null) {
					if (o < nn.Offset + nn.First.Offset) {
						// insert into the middle branch
						n.Add(newNode);
						return;
					}
					n = nn;
					nn = n.Next as TagBranch<T>;
				}
				// insert into the last branch
				n.Add(newNode);
			}

			void AddLeaf(TagNode<T> newNode) {
				newNode.Parent = this;
				var o = newNode.Offset;
				// fast path for appending
				if (o > Last.Offset) {
					Append(newNode);
					return;
				}
				var n = First;
				if (o < n.Offset) {
					Prepend(newNode);
					return;
				}
				Insert(newNode, o, n);
			}

			void Prepend(TagNode<T> newNode) {
				if (NodeCount < BranchCapacity) {
					newNode.Next = First;
					First = newNode;
					NodeCount++;
					return;
				}
				var b = new TagBranch<T>(newNode) { Offset = Offset };
				if (Parent == null) {
					b.Next = this;
					Parent = new TagBranch<T>(b);
				}
				else {
					Parent.AddLeaf(b);
				}
			}

			void Insert(TagNode<T> newNode, int o, TagNode<T> n) {
				var c = 1;
				do {
					n = n.Next;
					++c;
				} while (o > n.Offset);
				newNode.Next = n.Next;
				if (NodeCount < BranchCapacity) {
					n.Next = newNode;
					NodeCount++;
					return;
				}
				n.Next = null;
				NodeCount = c;
				var b = new TagBranch<T>(newNode) { Offset = Offset };
				if (Parent == null) {
					b.Next = this;
					Parent = new TagBranch<T>(b);
				}
				else {
					Parent.AddLeaf(b);
				}
			}

			void Append(TagNode<T> newNode) {
				if (NodeCount < BranchCapacity) {
					Last = Last.Next = newNode;
					NodeCount++;
					return;
				}
				var b = new TagBranch<T>(newNode) { Offset = Offset };
				if (Parent == null) {
					Next = b;
					Parent = new TagBranch<T>(this);
				}
				else {
					Parent.AddLeaf(b);
				}
			}

			public void RemoveAfter(int offset) {
				if (NodeCount == 0) {
					return;
				}
				var o = offset - Offset;
				if (o <= First.Offset) {
					First = Last = Next = null;
					NodeCount = Offset = 0;
					return;
				}
				if (o > Last.Offset) {
					return;
				}
				var n = First.Next;
				var c = 1;
				while (o > n.Offset) {
					n = n.Next;
					++c;
				}
				n.Next = null;
				Last = n;
				if (n.IsLeaf == false) {
					((TagBranch<T>)n).RemoveAfter(o);
				}
				NodeCount = c;
			}

			public T GetFirstItemBeforeOffset(int offset) {
				return default;
			}

			public override void Update(int offset, int delta) {
				if (NodeCount == 0) {
					return;
				}
				var o = offset -= Offset;
				if (o > Last.Offset) {
					return;
				}
				if (o <= First.Offset) {
					Offset += delta;
					return;
				}
				var n = First;
				while ((n = n.Next) != null) {
					if (n.Offset > offset) {
						n.Update(offset, delta);
					}
				}
			}

			public override string ToString() {
				return First is null ? "B(0)" : $"B[{NodeCount}]: {TotalOffset} ({Offset})";
			}

			public IEnumerator<TagNode<T>> GetEnumerator() {
				return First == null ? System.Linq.Enumerable.Empty<TagNode<T>>().GetEnumerator() : new TagBranchEnumerator(this);
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
				return GetEnumerator();
			}

			sealed class TagBranchEnumerator : IEnumerator<TagNode<T>>
			{
				readonly TagBranch<T> _TagBranch;
				TagNode<T> _Current;

				public TagBranchEnumerator(TagBranch<T> tagTree) {
					_TagBranch = tagTree;
				}

				public TagNode<T> Current => _Current;
				object System.Collections.IEnumerator.Current => _Current;

				public bool MoveNext() {
					if (_Current is null) {
						_Current = _TagBranch.FirstLeaf;
						return true;
					}
					var n = _Current.Next;
					if (n != null) {
						_Current = n;
						return true;
					}
					if ((n = _Current.Parent) is null) {
						goto NO_MORE;
					}
					while (n.Next is null) {
						if ((n = n.Parent) is null) {
							goto NO_MORE;
						}
					}
					_Current = (n.Next as TagBranch<T>).FirstLeaf;
					return true;
					NO_MORE:
					_Current = null;
					return false;
				}

				public void Reset() {
					_Current = null;
				}

				public void Dispose() {
				}
			}
		}

		sealed class TagLeaf<T> : TagNode<T>
		{
			public T Content;
			public int Length;
			public override bool IsLeaf => true;
			public override int Start => TotalOffset + Offset;
			public override int End => TotalOffset + Length;

			public TagLeaf(T content, int offset, int length) {
				Offset = offset;
				Length = length;
				Content = content;
			}

			public override void Update(int offset, int delta) {
				Offset += delta;
			}

			public override string ToString() {
				return $"L: [{TotalOffset}, {TotalOffset + Length}) {Offset}, {Length}";
			}
		}

#endif
}
