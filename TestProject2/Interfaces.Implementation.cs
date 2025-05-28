using System;

namespace TestProject.Interfaces.Implementation;

interface IBase
{
	void DoWork();
}

interface IDerived : IBase
{
	int Id { get; }
	void DoMoreWork();
}

interface IDerived2 : IBase
{
	int Id { get; set; }
	void DoAnotherWork();
}

interface IGeneric<T>
{
	T Value { get; }
}

interface IDerivedGeneric<T> : IGeneric<T>, IDerived
{
}

interface IMisc : IDerived, IDerived2, IGeneric<int> { }

class Base : IBase
{
	public void DoWork() { }
}

class Derived : Base, IDerived
{
	public int Id { get; }
	public void DoMoreWork() { }
}

class Lazy : Derived
{
	public void Sleep() { }
}

class GenericInt : IGeneric<int>
{
	public int Value { get; set; }
}

class GenericFloat : IGeneric<float>
{
	public float Value { get; set; }
}

class Generic<T>
{
	class Internal : IGeneric<T>
	{
		public T Value { get; set; }
	}

	class InheritedInternal : Internal, IDerivedGeneric<T>
	{
		public int Id { get; }

		public void DoMoreWork() { }

		public void DoWork() { }
	}
}

class Misc : IMisc
{
	public int Id { get; set; }
	public int Value { get; set; }
	public virtual void DoWork() { }
	public void DoMoreWork() { }
	public void DoAnotherWork() { }
}

class NotSoLazy : Misc
{
	public override void DoWork() { }
}

[ApiVersion(8)]
public class DefaultInterfaceImplementation
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

	[ApiVersion(8)]
	public interface IAdvancedImplementation
	{
		protected event EventHandler Default;
		void Action() {
			Console.WriteLine("Advanced action");
		}
	}

	[ApiVersion(8)]
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
