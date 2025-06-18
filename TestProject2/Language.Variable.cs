using System;
using System.Threading.Tasks;

namespace TestProject.Language.Variable;

class MakeRef
{
	static void Test() {
		int number = 42;

		// use __makeref to create TypedReference
		TypedReference typedRef = __makeref(number);

		// use __reftype to get type
		Type type = __reftype(typedRef);
		Console.WriteLine($"Type: {type}"); // output: Type: System.Int32

		// use __refvalue to get value
		int value = __refvalue(typedRef, int);
		Console.WriteLine($"Value: {value}"); // output: Value: 42
	}
}

static class VariableCapture
{
	public static async void Test(Exception exception) {
		DateTime now = DateTime.Now;
		string file = $"log{now.ToShortDateString()}.txt";
		// captures exception, now, file
		await Task.Run(() => WriteLog());
		WriteLog();
		// captures exception, now, file
		await Task.Run(WriteLog);
		// captures exception, now
		await Task.Run((Action)WriteConsole);
		WriteConsole();
		// captures <nothing>
		WriteTextToConsole(null);
		// captures exception
		await Task.Run(() => {
			var ex = exception.ToString();
			System.Diagnostics.Debug.WriteLine(ex);
		});
		// captures exception
		await Task.Run(() => System.Diagnostics.Debug.WriteLine(exception));
		// captures now
		Array.FindAll(new[] { DateTime.MaxValue, DateTime.Now }, t => t > now);
		// captures exception, now, file
		void WriteLog() {
			var ex = exception.ToString();
			System.IO.File.AppendAllLines(file, new[] { ex });
			WriteConsole();
		}
		// captures exception, now
		void WriteConsole() {
			Console.Write(now);
			Console.WriteLine(exception.Message);
		}
		// captures <nothing>
		Action WriteTextToConsole(string text) {
			// captures text
			return () => {
				Console.WriteLine(text);
			};
		}
	}
}
