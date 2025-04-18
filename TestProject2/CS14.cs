using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace TestProject.CS14_0;

internal class Field
{
	public string Message {
		get;
		set => field = value ?? throw new ArgumentNullException(nameof(value));
	}
}

static class NameOfUnboundGeneric
{
	static readonly string list = nameof(List<>);
	const string dictionary = nameof(Dictionary<,>);
}

class LambdaParametersWithModifiers
{
	delegate bool TryParse<T>(string text, out T result);
	TryParse<int> parse1 = (text, out result) => Int32.TryParse(text, out result);
}

partial class PartialConstructorAndEvent
{
	internal partial PartialConstructorAndEvent();
	protected partial event EventHandler DoWork;
}

partial class PartialConstructorAndEvent
{
	internal partial PartialConstructorAndEvent() {
		Console.WriteLine("Inside partial constructor");
	}
	EventHandler _DoWork;
	protected partial event EventHandler DoWork {
		add => _DoWork += value;
		remove => _DoWork -= value;
	}
}

static class NullConditionalAssignment
{
	public sealed class O
	{
		public string Name { get; internal set; }
	}

	public static void SetName(O item, object value) {
		item?.Name = value?.ToString();
	}
}

public static class Extensions
{
	extension(string text) {
		public bool HasContent => !String.IsNullOrWhiteSpace(text);
	}

    extension<T>(IEnumerable<T> source) where T : System.Numerics.INumber<T>
    {
        public IEnumerable<T> WhereGreaterThan(T threshold)
            => source.Where(x => x > threshold);

        public bool IsEmpty
            => !source.Any();
    }

	extension<T>([NotNullWhen(true)]T[] array) {
		public bool HasContent => array?.Length > 0;
	}

	public static bool IsNumber(this string text) {
		return !String.IsNullOrWhiteSpace(text) && Double.TryParse(text, out _);
	}
}

sealed class ExtensionBlock
{
	public int Threshold { get; }

	public ExtensionBlock(int threshold) {
		Threshold = threshold;
	}

	IEnumerable<int> Filter(IEnumerable<int> source) {
		if (source.IsEmpty) {
			yield break;
		}
		foreach (var item in source.WhereGreaterThan(Threshold)) {
			yield return item;
		}
	}
}