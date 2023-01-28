using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TestProject2.CS11_0;

public interface IMonoid<TSelf> where TSelf : IMonoid<TSelf>
{
	static abstract TSelf operator +(TSelf a, TSelf b);
	public static abstract TSelf Zero { get; }
}

public struct MyInt : IMonoid<MyInt>
{
	int value;
	public MyInt(int i) => value = i;
	public static MyInt operator +(MyInt a, MyInt b) => new MyInt(a.value + b.value);
	public static MyInt Zero => new MyInt(0);
}

public class Consumer
{
	T AddAll<T>(params T[] elements) where T : IMonoid<T> {
		T result = T.Zero;
		foreach (var element in elements) {
			result += element;
		}
		return result;
	}
}

public class ListPattern
{

	T AddAll<T>(params T[] elements) where T : IMonoid<T> =>
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

	void MatchMessage(string message) {
		var result = message is ['a' or 'A', .. var s, 'a' or 'A']
			? $"Message {message} matches; inner part is {s}."
			: $"Message {message} doesn't match.";
		Console.WriteLine(result);
	}
}

public interface IGetNext<T> where T : IGetNext<T>
{
	static abstract T operator ++(T other);
}
public struct RepeatSequence : IGetNext<RepeatSequence>
{
	private const char Ch = 'A';
	public string Text = new string(Ch, 1);

	public RepeatSequence() { }

	public static RepeatSequence operator ++(RepeatSequence other)
		=> other with { Text = other.Text + Ch };

	public override string ToString() => Text;
}

#nullable enable
public class Person
{
	public required string FirstName { get; init; }
	public string? MiddleName { get; init; }
	public required string LastName { get; init; }
}

#nullable disable
