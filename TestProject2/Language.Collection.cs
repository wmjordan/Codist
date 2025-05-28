using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject.Language.Collection;

[ApiVersion(12)]
class InlineArray
{
	[System.Runtime.CompilerServices.InlineArray(10)]
	public struct Buffer
	{
		private int _element0;
	}

	InlineArray() {
		var buffer = new Buffer();
		for (int i = 0; i < 10; i++) {
			buffer[i] = i;
		}
	}
}

[ApiVersion(12)]
static class CollectionExpression
{
	static void Array() {
		int[] a = [1, 2, 3, 4, 5, 6, 7, 8];
	}

	static void GenericList() {
		List<string> b = ["one", "two", "three"];
	}

	static void Span() {
		Span<char> c = ['a', 'b', 'c', 'd', 'e', 'f', 'h', 'i'];
	}

	static void Jagged2DArray() {
		int[][] twoD = [[1, 2, 3], [4, 5, 6], [7, 8, 9]];
	}

	static void Jagged2DArrayFromVariables() {
		int[] row0 = [1, 2, 3];
		int[] row1 = [4, 5, 6];
		int[] row2 = [7, 8, 9];
		int[][] twoDFromVariables = [row0, row1, row2];

		int[] single = [.. row0, .. row1, .. row2];
		foreach (var element in single) {
			Console.Write($"{element}, ");
		}
	}

	static void ConcatArray() {
		int[] row0 = [1, 2, 3];
		int[] row1 = [4, 5, 6];
		int[] single = [.. row0, .. row1, 7, 8, 9];
		foreach (var element in single) {
			Console.Write($"{element}, ");
		}
	}
}

