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

	/// <summary>A light-weight append-only single linked list.</summary>
	/// <typeparam name="T">Type of the item on the list.</typeparam>
	internal class Chain<T> : IEnumerable<T>
	{
		Node _Head;
		Node _Tail;

		public Chain() {}
		public Chain(T item) {
			Init(item);
		}

		public void Init(T item) {
			_Head = _Tail = new Node(item);
		}

		public void AddOrInit(T item) {
			if (_Head == null) {
				Init(item);
			}
			else {
				Add(item);
			}
		}

		public Chain<T> Add(T item) {
			_Tail = _Tail.Next = new Node(item);
			return this;
		}

		public IEnumerator<T> GetEnumerator() {
			return new ChainEnumerator(this);
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
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
