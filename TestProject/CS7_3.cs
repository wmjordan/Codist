using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject.CS7_3
{
	// Indexing fixed fields does not require pinning
	unsafe struct S
	{
		public fixed int myFixedField[10];
	}

	class C
	{
		static S s = new S();

		unsafe public void M() {
			int p = s.myFixedField[5];
			// stackalloc arrays support initializers
			int* pArr = stackalloc int[3] { 1, 2, 3 };
			int* pArr2 = stackalloc int[] { 1, 2, 3 };
			Span<int> arr = stackalloc[] { 1, 2, 3 };
		}
	}

	public class FieldAttributeInProperty
	{
		// Attach attributes to the backing fields for auto-implemented properties
		[field: NonSerialized]
		[property: Obsolete("test only")]
		public int SomeProperty { get; set; }
	}

	// in method overload resolution tiebreaker
	public class InMethodOverload
	{
		static void M(S arg) { }
		static void M(in S arg) { }
	}

	// Extend expression variables in initializers
	public class B
	{
		public B(int i, out int j) {
			j = i;
		}
	}

	public class D : B
	{
		public D(int i) : base(i, out var j) {
			Console.WriteLine($"The value of 'j' is {j}");
		}
	}
}
