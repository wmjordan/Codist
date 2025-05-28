using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyPoint = (int x, int y);

namespace TestProject.CS12_0;

static class Lambda
{
	static void LambdaWithDefault() {
		var expression = (int source, int increment = 1) => source + increment;

		Console.WriteLine(expression(5)); // 6
		Console.WriteLine(expression(5, 2)); // 7
	}

	static void LambdaWithParams() {
		var sum = (params int[] values) => {
			int sum = 0;
			foreach (var value in values)
				sum += value;

			return sum;
		};

		var empty = sum();
		Console.WriteLine(empty); // 0
		var total = sum(1, 2, 3, 4, 5);
		Console.WriteLine(total); // 15
	}
}

delegate int DelegateWithDefaultValueParam(int source, int increment = 1);

delegate int DelegateWithParams(params int[] values);

static class Alias
{
	static void AnyAlias() {
		ValueTuple<int, int> p = new MyPoint(0, 0);
		MyPoint point = new MyPoint(0, 0);
		point.x = 1;
		point.y = 2;
	}
}
