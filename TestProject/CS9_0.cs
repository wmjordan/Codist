using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#region Top level statement
System.Console.WriteLine("Hello World!");
#endregion

namespace TestProject.CS9_0
{
	#region Record
	public record Person
	{
		public string LastName { get; }
		public string FirstName { get; }

		public Person(string first, string last) => (FirstName, LastName) = (first, last);
	}
	public record Teacher : Person
	{
		public string Subject { get; }

		public Teacher(string first, string last, string sub)
			: base(first, last) => Subject = sub;
	}
	public sealed record Student : Person
	{
		public int Level { get; }

		public Student(string first, string last, int level) : base(first, last) => Level = level;
	}
	public class TestRecord
	{
		void Test() {
			var person = new Person("Bill", "Wagner");
			var student = new Student("Bill", "Wagner", 11);

			Console.WriteLine(student == person); // false
			Console.WriteLine(student.ToString());
		}
	}
	public class PositionalRecords
	{
		public record Person(string FirstName, string LastName);

		public record Teacher(string FirstName, string LastName,
			string Subject)
			: Person(FirstName, LastName);

		public sealed record Student(string FirstName,
			string LastName, int Level)
			: Person(FirstName, LastName);

		public record Pet(string Name)
		{
			public void ShredTheFurniture() =>
				Console.WriteLine("Shredding furniture");
		}

		public record Dog(string Name) : Pet(Name)
		{
			public void WagTail() =>
				Console.WriteLine("It's tail wagging time");

			public override string ToString() {
				StringBuilder s = new();
				base.PrintMembers(s);
				return $"{s.ToString()} is a dog";
			}
		}

		void DeconstructRecord() {
			var person = new Person("Bill", "Wagner");

			var (first, last) = person;
			Console.WriteLine(first);
			Console.WriteLine(last);
		}

		void RecordAndWithExpression() {
			var person = new Person("Bill", "Wagner");
			Person brother = person with { FirstName = "Paul" };

			Person clone = person with { };
		}
	}
	#endregion

	#region Init only setters
	struct WeatherObservation
	{
		public DateTime RecordedAt { get; init; }
		public readonly decimal TemperatureInCelsius { get; init; }
		public readonly decimal PressureInMillibars { get; init; }
		public object Tag { get; set; }

		public override readonly string ToString() =>
			$"At {RecordedAt:h:mm tt} on {RecordedAt:M/d/yyyy}: " +
			$"Temp = {TemperatureInCelsius}, with {PressureInMillibars} pressure";

		readonly void Initialize() {
			var now = new WeatherObservation {
				RecordedAt = DateTime.Now,
				TemperatureInCelsius = 20,
				PressureInMillibars = 998.0m
			};
		}
	}
	#endregion

	#region Pattern matching enhancements
	public static class PatternMatching
	{
		public static bool IsLetter(this char c) =>
			c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

		public static bool IsLetterOrSeparator(this char c) =>
			c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '.' or ',';

		public static void NullCheck(object e) {
			if (e is not null) {
				// ...
			}
		}
		public static void Tuple(object o1, object o2) {
			var t = (o1, o2);
			if (t is (int k, string v)) {
				Console.WriteLine("o1 is int && o2 is string");
			}
			switch (o1) {
				case int: Console.WriteLine("o1 is int"); break;
				case System.String: Console.WriteLine("o1 is string"); break;
			}
		}
		public static void OperatorSwitch() {
			var t = DateTime.Now.Year;
			var n = t switch {
				> 5000 => 10.00m + 5.00m,
				< 3000 => 10.00m - 2.00m,
				_ => 10.00m,
			};
			var log10 = t switch {
				< 0 => -1,
				>= 0 and < 10 => 0,
				>= 10 and < 100 => 1,
				>= 100 and < 1000 => 2,
				>= 1000 and < 10000 => 3,
				>= 10000 and < 100000 => 4,
				>= 100000 and < 1000000 => 5,
				>= 1000000 and < 10000000 => 6,
				_ => 7
			};
		}
	}
	#endregion

	#region Fit and finish features
	public class FitAndFinish
	{
		private List<WeatherObservation> _observations = new();
	}
	#endregion

	#region Static lambda expression
	public class StaticLambda
	{
		Func<double, double> square = static x => x * x;
	}
	#endregion

	#region Local function with attributes

	#nullable enable
	public class LocalFunctionWithAttribute
	{
		private static void Process(string?[] lines, string mark) {
			foreach (var line in lines) {
				if (IsValid(line)) {
					// Processing logic...
				}
			}

			bool IsValid([NotNullWhen(true)] string? line) {
				return !string.IsNullOrEmpty(line) && line.Length >= mark.Length;
			}
		}
	}

	[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
	sealed class NotNullWhenAttribute : Attribute
	{
		/// <summary>Initializes the attribute with the specified return value condition.</summary>
		/// <param name="returnValue">
		/// The return value condition. If the method returns this value, the associated parameter will not be null.
		/// </param>
		public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

		/// <summary>Gets the return value condition.</summary>
		public bool ReturnValue { get; }
	}
	#endregion

	#region Function pointers
	unsafe class FunctionPointers
	{
		static readonly delegate*<int, int, int> p1 = null;
		delegate* managed<int, int, int> p2 = null;
		//delegate* unmanaged<int, int, int> p3 = null; // this is supported only after .NET 5

		void Conversions() {
			var c = p1(Int32.MaxValue, 0);
			delegate*<int, int, int> p3 = p1;
			Console.WriteLine(p2 == p1); // True
		}
		void F(Action<int> a, delegate*<int, void> f) {
			a(42);
			f(42);
		}

		class Util
		{
			public static void Log() { }

			void Use() {
				delegate*<void> ptr1 = &Util.Log;
			}
		}
		unsafe struct Action
		{
			readonly delegate*<void> _ptr;

			Action(delegate*<void> ptr) => _ptr = ptr;
			public void Invoke() => _ptr();
		}
	}
	#endregion

	#region New expression
	static class NewExpression
	{
		readonly static object _syncObj = new();
		static void New() {
			Dictionary<string, List<int>> field = new() { { "item1", new() { 1, 2, 3 } } };

			TakeList(new() { 1, 2, 3 });
		}
		static void TakeList(List<int> values) {
		}
	}
	#endregion

	#region Partials
	[Serializable]
	sealed partial class PartAttribute : Attribute
	{
		public partial object Clone();
	}
	[AttributeUsage(AttributeTargets.Class)]
	public partial class PartAttribute : ICloneable
	{
		public partial object Clone() { return this; }
	}
	#endregion

	#region GetEnumeratorExtension
	public static class BitEnumerable
	{
		public static IEnumerator<int> GetEnumerator(this int value) {
			for (int i = 0, v = value; i < 32; i++, v >>= 1) {
				if ((v & 1) != 0) {
					yield return i;
				}
			}
		}

		static void Run() {
			// hover on "in" to see Quick Info for BitEnumerable.GetEnumerator
			foreach (var bit in 17) {
				Console.WriteLine(bit);
			}
		}
	}
	#endregion
}

namespace System.Runtime.CompilerServices
{
	public static class IsExternalInit
	{
	}
}
