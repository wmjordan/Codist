using System;
using System.Collections.Generic;
using TestProject.Interfaces.Static;

namespace TestProject.Language.Comparison;

public class ListPattern
{
	public T AddAll<T>(params T[] elements) where T : IMonoid<T> =>
		elements switch {
			[] => T.Zero,
			[var first, .. var rest] => first + AddAll<T>(rest),
		};

	void Compare() {
		List<int> numbers = new() { 1, 2, 3 };

		if (numbers is [var first, _, _]) {
			Console.WriteLine($"The first element of a three-item list is {first}.");
		}

		var t = new[] { 1, 2, 3, 4 } is [.., > 0, > 0];
		t = new[] { 1, 1 } is [_, _, ..];
		t = new[] { 1, 2, 3, 4, 5 } is [> 0, > 0, ..];
		t = new[] { 1, 2, 3, 4 } is [>= 0, .., 2 or 4];
		t = new[] { 1, 0, 0, 1 } is [1, 0, .., 0, 1];

		t = new[] { -1, 0, 0, 1 } is [< 0, .. { Length: 2 or 4 }, > 0];
		var f = new[] { -1, 0, 1 } is [< 0, .. { Length: 2 or 4 }, > 0];
	}

	public void MatchMessage(string message) {
		var result = message is ['a' or 'A', .. var s, 'a' or 'A']
			? $"Message {message} matches; inner part is {s}."
			: $"Message {message} doesn't match.";
		Console.WriteLine(result);
	}
}
