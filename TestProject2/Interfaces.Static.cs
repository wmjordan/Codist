using System.Runtime.InteropServices;

namespace TestProject.Interfaces.Static;

[ApiVersion(11)]
public interface IMonoid<TSelf> where TSelf : IMonoid<TSelf>
{
	static abstract TSelf operator +(TSelf a, TSelf b);
	public static abstract TSelf Zero { get; }
}

[ApiVersion(11)]
public struct MyInt : IMonoid<MyInt>
{
	int value;
	public MyInt(int i) => value = i;
	public static MyInt operator +(MyInt a, MyInt b) => new MyInt(a.value + b.value);
	public static MyInt Zero => new MyInt(0);
}

[ApiVersion(11)]
public static class StaticInterfaceConsumer
{
	static T AddAll<T>(params T[] elements) where T : IMonoid<T> {
		T result = T.Zero;
		foreach (var element in elements) {
			result += element;
		}
		return result;
	}
}

[ApiVersion(11)]
public static class InterfaceOperator
{
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

		public override readonly string ToString() => Text;
	}
}

#nullable enable
public class Person
{
	public required string FirstName { get; init; }
	public string? MiddleName { get; init; }
	public required string LastName { get; init; }
}

#nullable disable
