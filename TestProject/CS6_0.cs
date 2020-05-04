using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Math;
using static System.String;

namespace TestProject.CS6_0
{
	class Student
	{
		public string FirstName { get; }
		public string LastName { get; }
		public string FullName => $"{FirstName} {LastName}";
		public ICollection<double> Grades { get; } = new List<double>();
		public string GetGradePointPercentage() =>
			$"Name: {LastName}, {FirstName}. G.P.A: {Grades.Average():F2}";
		public override string ToString() => Join(", ", LastName, FirstName);

		void M() {
			var person = new Student();
			var first = person?.FirstName;
			first = person?.FirstName ?? "Unspecified";
			FormattableString str = $"Average grade is {person.Grades.Average()}";
			string gradeStr;

			try {
				gradeStr = str.ToString(new System.Globalization.CultureInfo("de-DE"));
			}
			catch (Exception ex) when (ex.Message.Contains("System")) {
				gradeStr = "System error";
			}
			Console.WriteLine(gradeStr);
		}
	}

	class AsyncInCatchAndFinally
	{
		public static async Task<string> MakeRequestAndLogFailures() {
			await Task.Delay(100);
			var streamTask = Task.FromResult("https://localHost:10000");
			try {
				return await streamTask;
			}
			catch (Exception e) when (e.Message.Contains("301")) {
				return await Task.FromException<string>(e);
			}
			finally {
				await Task.Delay(100);
			}
		}
	}

	class InitializeAssociativeCollection
	{
		private Dictionary<int, string> messages = new Dictionary<int, string>
		{
			{ 404, "Page not Found"},
			{ 302, "Page moved, but left a forwarding address."},
			{ 500, "The web server can't come out to play today."}
		};
		private Dictionary<int, string> webErrors = new Dictionary<int, string> {
			[404] = "Page not Found",
			[302] = "Page moved, but left a forwarding address.",
			[500] = "The web server can't come out to play today."
		};
		private Dictionary<int, string> addWithExtension = new Dictionary<int, string> {
			(1, "one"), ((byte)2, "two"), { 3, "three" }, 4, 5, byte.MaxValue
		};
	}

	static class AddDictionaryHelper
	{
		public static void Add<TKey, TValue>(this Dictionary<TKey, TValue> collection, (TKey k, TValue v) keyAndValue) {
			collection.Add(keyAndValue.k, keyAndValue.v);
		}
		public static void Add(this Dictionary<int, string> collection, int value) {
			collection.Add(value, value.ToString());
		}
	}
}
