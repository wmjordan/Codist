using System;
using System.Collections;
using System.Collections.Generic;

namespace Codist
{
	static class Chain
	{
		public static Chain<T> Create<T>(T item) {
			return new Chain<T>(item);
		}
	}

	/// <summary>A light-weight add-only single linked list.</summary>
	/// <typeparam name="T">Type of the item on the list.</typeparam>
	sealed class Chain<T> : IEnumerable<T>
	{
		Node _Head;
		Node _Tail;

		public Chain() {}
		public Chain(T item) {
			_Head = _Tail = new Node(item);
		}

		public bool IsEmpty => _Head == null;
		public T Head => _Head != null ? _Head.Value : default;
		public T Tail => _Tail != null ? _Tail.Value : default;

		public Chain<T> Add(T item) {
			if (_Head != null) {
				_Tail = _Tail.Next = new Node(item);
			}
			else {
				_Head = _Tail = new Node(item);
			}
			return this;
		}

		public Chain<T> Insert(T item) {
			_Head = new Node(item) {
				Next = _Head
			};
			return this;
		}

		public bool Contains(T item) {
			var t = _Head;
			while (t != null) {
				if (Comparer.Func(t.Value, item)) {
					return true;
				}
				t = t.Next;
			}
			return false;
		}

		public void Clear() {
			_Head = _Tail = null;
		}

		public static Chain<T> operator +(Chain<T> chain, T item) {
			return chain.Add(item);
		}

		public IEnumerator<T> GetEnumerator() {
			return new ChainEnumerator(this);
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		static class Comparer
		{
			public static readonly Func<T,T,bool> Func = EqualityComparer<T>.Default.Equals;
		}

		sealed class Node
		{
			public readonly T Value;
			public Node Next;

			public Node(T value) {
				Value = value;
			}
		}

		struct ChainEnumerator : IEnumerator<T>
		{
			Node _Node;

			public ChainEnumerator(Chain<T> chain) : this() {
				_Node = chain._Head;
			}

			public T Current { get; private set; }
			object IEnumerator.Current => Current;

			public void Dispose() {}

			public bool MoveNext() {
				if (_Node == null) {
					return false;
				}
				Current = _Node.Value;
				_Node = _Node.Next;
				return true;
			}

			public void Reset() {
				throw new NotImplementedException();
			}
		}
	}
}
