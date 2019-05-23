using System;
using System.Collections.Generic;

namespace TestProject
{
	[System.ComponentModel.Description("demo")]
	struct MyStruct // struct declaration
	{
		#region Private fields
		//todo hover on Constant to see its value
		private const short Constant = ushort.MaxValue ^ 0xF0F0; // const field
		private const string ConstantString = "literal string"; // const string
		private static int _static = (int)DateTime.Now.Ticks; // static field
		private static readonly int _staticReadonly = Int32.MinValue; // static readonly field
		private int _instanceField; // field
		private readonly int _readonlyField; // readonly field 
		#endregion

		public readonly static DateTime StartDate = DateTime.Now; // public readonly static field

		public int PropReadOnly => Int32.MinValue + 1;

		public static IEnumerable<(string, int)> TupleDeconstruct() {
			var a = new[] { (1, "one"), (2, "two"), (3, "three") };
			foreach (var (num, str) in a) {
				yield return (str, num);
			}
		}

	}
}
