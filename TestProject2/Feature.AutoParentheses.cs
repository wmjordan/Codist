using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject2.Feature;

class AutoParentheses
{
	internal class Generic<T>
	{
		public Generic() { }
	}

	class Cases
	{
		public static event EventHandler Event;
		public static Action Delegate;
		public static void GenericMethod<T>() { }
		public static void TakesAction(Action action) { }
		public static void TakesActions(params Action[] actions) { }
		public static string ReturnsText() => String.Empty;
	}

	[LoaderOptimization(1)]
	void ShouldAppend() {
		new Cases();
		EventHandler(null, EventArgs.Empty);
		var a = nameof(Cases);
		var t = typeof(Cases);
		var s = sizeof(int);
		(string name, int n) = (Cases.ReturnsText(), 0);
	}

	[STAThread, Obsolete]
	void ShouldNotAppend() {
		new Generic<int>();
		Cases.GenericMethod<int>();
		Cases.Event += EventHandler;
		Cases.Event -= EventHandler;
		Cases.Delegate = Console.WriteLine;
		Action[] actions = [
			Console.WriteLine
			];
		(string name, Action action) = ("default", Console.WriteLine);
		Cases.TakesAction(ShouldAppend);
		Cases.TakesActions(ShouldAppend, ShouldNotAppend);
	}

	void EventHandler(object sender, EventArgs e) { }
}
