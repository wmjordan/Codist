using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject2.Feature;

class AutoParentheses
{
	class Generic<T>
	{
		public Generic() { }

		public Generic<int> ShouldNotAppend() {
			return new Generic<int>();
		}
	}

	class TakeActions(params Action[] actions) {
		public TakeActions(Func<int> func, params Action[] actions) : this(actions) {
		}
	}
	class TakeStrings(params string[] contents) { }

	class Cases
	{
		public static event EventHandler Event;
		public static Action Delegate;
		public static Action ShouldNotAppend => Console.WriteLine;
		public static T GenericMethod<T>() { return default; }
		public static int Max() => Int32.MaxValue;
		public static void TakesAction(Action action) { }
		public static void TakesActions(params Action[] actions) { }
		public static string ReturnsText() => String.Empty;
		public static string ReturnsText(int value) => value.ToString();
	}

	[LoaderOptimization(1)]
	void ShouldAppend() {
		EventHandler(new Cases(), EventArgs.Empty);
		Console.WriteLine(Cases.ReturnsText().ToLower());
		var a = nameof(Cases);
		var t = typeof(Cases);
		var s = sizeof(int);
		(string name, int n) = (Cases.ReturnsText(), 0);
		(name, n) = (Cases.ReturnsText(1), 0);
		new TakeStrings(Cases.ReturnsText(), DateTime.Now.ToString());
	}

	[STAThread, Obsolete]
	void ShouldNotAppend() {
		new Generic<int>();
		var l = new List<int>();
		Cases.GenericMethod<int>();
		Cases.Event += EventHandler;
		Cases.Event -= EventHandler;
		Cases.Delegate = Console.WriteLine;
		Action a = Console.WriteLine;
		Action[] actions = [
			Console.WriteLine,
			ShouldNotAppend
			];
		Func<string>[] funcs = new Func<string>[] { Cases.ReturnsText, DateTime.Now.ToString };
		(string name, Action action) = ("default", Console.WriteLine);
		Cases.TakesAction(ShouldAppend);
		Cases.TakesActions(ShouldAppend, ShouldNotAppend);
		new TakeActions(ShouldAppend, ShouldNotAppend);
		new TakeActions(Cases.GenericMethod<int>, ShouldAppend);
		new TakeActions(Cases.Max, ShouldAppend);
	}

	void EventHandler(object sender, EventArgs e) { }
}
