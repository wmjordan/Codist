using System;
using System.Collections.Generic;

namespace TestProject.Methods.Parameter;

[ApiVersion(13)]
class EnumerableParams
{
	public void Concat<T>(params T[] items) { }
	public void Concat<T>(params IEnumerable<T> items) { }
	public void Concat<T>(params IReadOnlyCollection<T> items) { }
	public void Concat<T>(params ReadOnlySpan<T> items) {
		for (int i = 0; i < items.Length; i++) {
			Console.Write(items[i]);
			Console.Write(" ");
		}
		Console.WriteLine();
	}
	public void Use() {
		Concat(1, 2, 3);
	}
}

[ApiVersion(14)]
class LambdaParametersWithModifiers
{
	delegate bool TryParse<T>(string text, out T result);
	TryParse<int> parse1 = (text, out result) => Int32.TryParse(text, out result);
}
