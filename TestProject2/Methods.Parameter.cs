using System;
using System.Collections.Generic;

namespace TestProject.Methods.Parameter;

class ArgList
{
	public static void PrintFormatted(__arglist) {
		var args = new ArgIterator(__arglist);
		while (args.GetRemainingCount() > 0) {
			var arg = TypedReference.ToObject(args.GetNextArg());
			Console.WriteLine(arg);
		}
	}
}

class DefaultParams
{
	public void Method(int value, int optional = 0, params int[] others) {
	}

	public void Method(int option1 = 0, int option2 = 0) { }

	public void Consumer() {
		Method(0, optional: 3);
		Method(0);
		Method(0, 1);
		Method(0, 1, 2);
		Method(0, option2: 1);
		Method(value: 0, 1);
	}
}

class TakeDelegate
{
	public void Take(Action action, Action action2, params Action[] moreActions) {
		action();
		action2();
		foreach (var a in moreActions) {
			a();
		}
	}

	public void Use() {
		// Test auto parentheses feature,
		// no parentheses should be inserted on completing "Dummy"
		Take(Dummy, Dummy, Dummy);
	}

	public void Dummy() { }
}

[ApiVersion(12)]
class RefReadonly
{
	void M1(I1 o, ref readonly int x) => System.Console.Write("1");
	void M2(I2 o, ref int x) => System.Console.Write("2");
	void Run() {
		D1 m1 = M1;
		D2 m2 = M2;

		var i = 5;
		m1(null, in i);
		m2(null, ref i);
	}
	static void Main() => new RefReadonly().Run();

	interface I1 { }
	interface I2 { }
	class X : I1, I2 { }
	delegate void D1(X s, ref readonly int x);
	delegate void D2(X s, ref int x);
}

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
		Action a, b, c;
		a = b = c = () => { };
		TakeEnumerableParams<Action> e = Concat<Action>;
		e(a, b, Console.WriteLine);
		int x, y, z;
		x = y = z = 0;
		TakeReadOnlySpanParams<int> s = Concat<int>;
		s(x, y, z);
	}

	delegate void TakeEnumerableParams<T>(params IEnumerable<T> values);

	delegate void TakeReadOnlySpanParams<T>(params ReadOnlySpan<T> values);
}

[ApiVersion(14)]
class LambdaParametersWithModifiers
{
	delegate bool TryParse<T>(string text, out T result);
	TryParse<int> parse1 = (text, out result) => Int32.TryParse(text, out result);
}
