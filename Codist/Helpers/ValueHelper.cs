using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Codist
{
	static class ValueHelper
	{
		public static IImmutableSet<T> MakeImmutableSet<T>(this IEnumerable<T> values) {
			return values is null ? null : (IImmutableSet<T>)ImmutableHashSet.CreateRange(values);
		}
	}
}
