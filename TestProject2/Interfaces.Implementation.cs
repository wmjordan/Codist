using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject.Interfaces.Implementation
{
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
}
