using System;

namespace TestProject2.CS8_0
{
	public interface INoImplementation
	{
		string Name { get; set; }
		void Action();
	}
	public interface IDefaultImplementation
	{
		void Action() {
			Console.WriteLine("Default action");
		}
	}

	public interface IAdvancedImplementation
	{
		protected event EventHandler Default;
		void Action() {
			Console.WriteLine("Advanced action");
		}
	}

	public interface IStaticImplementation
	{
		static readonly DateTime DefaultTime = DateTime.Now;
		static void PrintName() {
			Console.WriteLine(nameof(IStaticImplementation));
		}
		private static void PrintTime() {
			Console.WriteLine(DateTime.Now);
		}
		void Action() {
			PrintTime();
		}
		protected static void PrintDate() {
			Console.WriteLine(DateTime.Today);
		}
	}

	public class EmptyImplementation : IDefaultImplementation
	{
	}

	public class AdvancedImplementation : IDefaultImplementation, IAdvancedImplementation
	{
		event EventHandler _Default;
		event EventHandler IAdvancedImplementation.Default {
			add { _Default += value; }
			remove { _Default -= value; }
		}
	}

	public class OverrideImplementation : AdvancedImplementation
	{
		public void Action() {
			Console.WriteLine("Override action");
		}
	}

	public static class ImplementationConsumer
	{
		static void Main() {
			var i = new AdvancedImplementation();
			IDefaultImplementation a = i;
			a.Action();
			IAdvancedImplementation d = i;
			d.Action();
			var o = new OverrideImplementation();
			o.Action();
			Console.WriteLine(IStaticImplementation.DefaultTime);
			IStaticImplementation.PrintName();
		}
	}
}
