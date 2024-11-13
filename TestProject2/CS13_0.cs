using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject.CS13_0;

public class C<T> where T : allows ref struct
{
	// Use T as a ref struct:
	public void M(scoped T p) {
		// The parameter p must follow ref safety rules
	}
}

class Params
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

static class PartialProperty
{
	partial class C
	{
		// Declaring declaration
		public partial string Name { get; set; }
	}

	partial class C
	{
		// implementation declaration:
		private string _name;
		public partial string Name {
			get => _name;
			set => _name = value;
		}
	}

	internal static void Use() {
		var c = new C {
			Name = "123"
		};
	}
}