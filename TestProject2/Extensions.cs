using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace TestProject.Extensions;

[ApiVersion(14)]
public static class ExtensionTypes
{
	extension(string text) {
		public bool HasContent => !String.IsNullOrWhiteSpace(text);
	}

    extension<T>(IEnumerable<T> source) where T : System.Numerics.INumber<T>
    {
        public IEnumerable<T> WhereGreaterThan(T threshold)
            => source.Where(x => x > threshold);

        public bool IsEmpty
            => !source.Any();
    }

	extension<T>([NotNullWhen(true)]T[] array) {
		public bool HasContent => array?.Length > 0;
	}

	public static bool IsNumber(this string text) {
		return !String.IsNullOrWhiteSpace(text) && Double.TryParse(text, out _);
	}
}

sealed class ExtensionTypeConsumer(int threshold)
{
	public int Threshold { get; } = threshold;

	IEnumerable<int> Filter(IEnumerable<int> source) {
		if (source.IsEmpty) {
			yield break;
		}
		foreach (var item in source.WhereGreaterThan(Threshold)) {
			yield return item;
		}
	}
}