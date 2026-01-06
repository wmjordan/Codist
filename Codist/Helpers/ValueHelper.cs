using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace Codist
{
	static class ValueHelper
	{
		public static IImmutableSet<T> MakeImmutableSet<T>(this IEnumerable<T> values) {
			return values is null ? null : (IImmutableSet<T>)ImmutableHashSet.CreateRange(values);
		}
		public static string ToText(this int value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}
		public static bool Contains(this string text, char character) {
			return text.IndexOf(character) >= 0;
		}
	}
}
