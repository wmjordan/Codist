using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject.CS7_0
{
	class Misc
	{
		void M() {
			// out variables
			string input = Console.ReadLine();
			if (int.TryParse(input, out int result))
				Console.WriteLine(result);
			else
				Console.WriteLine("Could not parse input");

			// tuples
			(string Alpha, string Beta) namedLetters = ("a", "b");
			Console.WriteLine($"{namedLetters.Alpha}, {namedLetters.Beta}");

			var alphabetStart = (Alpha: "a", Beta: "b");
			Console.WriteLine($"{alphabetStart.Alpha}, {alphabetStart.Beta}");

			var p = new Point(3.14, 2.71);
			(double X, double Y) = p;

			var s1 = new Student("Cary", "Totten", 4.5);
			var (fName, lName, gpa) = s1; // call deconstruct extension method

			Dictionary<int, (int, string)> dict = new Dictionary<int, (int, string)>();
			dict.Add(1, (234, "First!"));
			dict.Add(2, (345, "Second"));
			dict.Add(3, (456, "Last"));

			// TryGetValue already demonstrates using out parameters
			dict.TryGetValue(2, out (int num, string place) pair);

			Console.WriteLine($"{pair.num}: {pair.place}");
		}

		internal IEnumerable<(int ID, string Title)> GetCurrentItemsMobileList() {
			var r = from item in new ToDoItem[0]
					let firstCharInTitle = item.Title[0]
					group item by firstCharInTitle into gid
					orderby gid.Key
					select gid;
			return from item in new ToDoItem[0]
				   join ritem in r on item.ID equals ritem.Key
				   where !item.IsDone
				   orderby item.ID, item.IsDone, item.DueDate descending
				   select (item.ID, item.Title);
		}
		public static double StandardDeviation(IEnumerable<double> sequence) {
			(double sum, double sumOfSquares, int count) = ComputeSumAndSumOfSquares(sequence);

			var variance = sumOfSquares - sum * sum / count;
			return Math.Sqrt(variance / count);
		}
		public static double StandardDeviation2(IEnumerable<double> sequence) {
			var (sum, sumOfSquares, count) = ComputeSumAndSumOfSquares(sequence);

			var variance = sumOfSquares - sum * sum / count;
			return Math.Sqrt(variance / count);
		}
		private static (double, double, int) ComputeSumAndSumOfSquares(IEnumerable<double> sequence) {
			double sum = 0;
			double sumOfSquares = 0;
			int count = 0;

			foreach (var item in sequence) {
				count++;
				sum += item;
				sumOfSquares += item * item;
			}

			return (sum, sumOfSquares, count);
		}
	}

	public class Discards
	{
		public static void M() {
			var (_, _, _, pop1, _, pop2) = QueryCityDataForYears("New York City", 1960, 2010);

			Console.WriteLine($"Population change, 1960 to 2010: {pop2 - pop1:N0}");
		}

		private static (string, double, int, int, int, int) QueryCityDataForYears(string name, int year1, int year2) {
			int population1 = 0, population2 = 0;
			double area = 0;

			if (name == "New York City") {
				area = 468.48;
				if (year1 == 1960) {
					population1 = 7781984;
				}
				if (year2 == 2010) {
					population2 = 8175133;
				}
				return (name, area, year1, population1, year2, population2);
			}

			return ("", 0, 0, 0, 0, 0);
		}
	}

	public class PatternMatching
	{
		public static int SumPositiveNumbers(IEnumerable<object> sequence) {
			int sum = 0;
			foreach (var i in sequence) {
				switch (i) {
					case 0:
						break;
					case IEnumerable<int> childSequence: {
						foreach (var item in childSequence)
							sum += (item > 0) ? item : 0;
						break;
					}
					case int n when n > 0:
						sum += n;
						break;
					case null:
						throw new NullReferenceException("Null found in sequence");
					default:
						throw new InvalidOperationException("Unrecognized type");
				}
			}
			return sum;
		}
	}

	public class RefLocalsAndReturns
	{
		public static ref int Find(int[,] matrix, Func<int, bool> predicate) {
			for (int i = 0; i < matrix.GetLength(0); i++)
				for (int j = 0; j < matrix.GetLength(1); j++)
					if (predicate(matrix[i, j]))
						return ref matrix[i, j];
			throw new InvalidOperationException("Not found");
		}

		void M() {
			var matrix = new int[5, 3];
			ref var item = ref Find(matrix, (val) => val == 42);
			Console.WriteLine(item);
			item = 24;
			Console.WriteLine(matrix[4, 2]);
		}

		public class Book
		{
			public string Author;
			public string Title;
		}

		public class BookCollection
		{
			private Book[] books = { new Book { Title = "Call of the Wild, The", Author = "Jack London" },
						new Book { Title = "Tale of Two Cities, A", Author = "Charles Dickens" }
					   };
			private Book nobook = null; // can be readonly in C# 7.2

			public ref Book GetBookByTitle(string title) {
				for (int ctr = 0; ctr < books.Length; ctr++) {
					if (title == books[ctr].Title)
						return ref books[ctr];
				}
				return ref nobook;
			}

			public void ListBooks() {
				foreach (var book in books) {
					Console.WriteLine($"{book.Title}, by {book.Author}");
				}
				Console.WriteLine();
			}
		}
	}

	public class ToDoItem
	{
		public int ID { get; set; }
		public bool IsDone { get; set; }
		public DateTime DueDate { get; set; }
		public string Title { get; set; }
		public string Notes { get; set; }
	}
	public class Point
	{
		public Point(double x, double y)
			=> (X, Y) = (x, y);

		public double X { get; }
		public double Y { get; }

		public void Deconstruct(out double x, out double y) =>
			(x, y) = (X, Y);
	}

	public class Person
	{
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public Person(string first, string last) {
			FirstName = first;
			LastName = last;
		}
	}
	public class Student : Person
	{
		public double GPA { get; }
		public Student(string first, string last, double gpa) :
			base(first, last) {
			GPA = gpa;
		}
	}

	public static class Extensions
	{
		public static void Deconstruct(this Student s, out string first, out string last, out double gpa) {
			first = s.FirstName;
			last = s.LastName;
			gpa = s.GPA;
		}
	}
}
