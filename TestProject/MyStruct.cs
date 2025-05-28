using System;
using System.Collections.Generic;

namespace TestProject
{
	[System.ComponentModel.Description("demo")]
	struct MyStruct // struct declaration
	{
		public int PropReadOnly => Int32.MinValue + 1;

		public static IEnumerable<(string, int)> TupleDeconstruct() {
			var a = new[] { (1, "one"), (2, "two"), (3, "three") };
			foreach (var (num, str) in a) {
				yield return (str, num);
			}
		}

	}
}
