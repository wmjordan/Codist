using System;
using System.Collections.Generic;

namespace Codist
{
	sealed class GenericEqualityComparer<TItem> : IEqualityComparer<TItem>
	{
		readonly Func<TItem, TItem, bool> _Comparer;
		readonly Func<TItem, int> _HashProvider;

		public GenericEqualityComparer(Func<TItem, TItem, bool> comparer, Func<TItem, int> hashProvider) {
			_Comparer = comparer;
			_HashProvider = hashProvider;
		}
		public bool Equals(TItem x, TItem y) {
			return _Comparer(x, y);
		}

		public int GetHashCode(TItem obj) {
			return _HashProvider(obj);
		}
	}
}
