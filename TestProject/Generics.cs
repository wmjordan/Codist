using System;
using System.Collections.Generic;

namespace TestProject.Generics
{
	interface IDefault<T>
	{
		T DefaultValue { get; }
	}
	class Default<T> : IDefault<T>
	{
		public virtual T DefaultValue => default;
		T IDefault<T>.DefaultValue => DefaultValue;
	}

	class DefaultValue<T> : Default<T> where T : struct { }

	class DefaultRef<T> : Default<T> where T : class { }

	class Int32Default : DefaultValue<int> { }

	class StringRef : DefaultRef<string>
	{
		public override string DefaultValue => String.Empty;
	}

	class Generic<T>
	{
		public static class Preset
		{
			public static readonly DateTime CreateDate = DateTime.Now;
			public static readonly Default<T> Default = new Default<T>();
		}

		public TOut TryOutput<TOut>(T input) => default;

		public T TryInput<TIn>(TIn input) => default;
	}

	static class TestCases
	{
		static StringRef _StringRef = new StringRef();
		static Default<DateTime> _DateTime = new Default<DateTime>();

		static DateTime _DefaultDateTime = ((IDefault<DateTime>)_DateTime).DefaultValue;
		static DateTime _DefaultDateTime2 = _DateTime.DefaultValue;
		static int _DefaultInt32 = new Int32Default().DefaultValue;
		static string _DefaultString = _StringRef.DefaultValue;
		static string _DefaultV = Generic<string>.Preset.Default.DefaultValue;
		static DateTime _DefaultPDate = Generic<string>.Preset.CreateDate;

		static void Test() {
			new Generic<int>().TryOutput<string>(1);
			new Generic<int>().TryInput<string>("");
		}
	}
}
