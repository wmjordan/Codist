using System;
using System.Linq.Expressions;

namespace TestProject.CS10_0;

record struct RecordStruct
{
	public string Name { get; init; }
	public Address Address { get; init; } = new();
	public System.Collections.Generic.Dictionary<string, object> Values { get; init; } = new(StringComparer.Ordinal);

	public RecordStruct() {
		Name = nameof(RecordStruct);
	}

	public readonly RecordStruct NewName(string name)
	{
		return this with { Name = name };
	}

	void PropertyPattern()
	{
		object obj = new RecordStruct
		{
			Name = "Kathleen",
			Values = { { "Dollard", 1 } },
			Address = new Address { City = "Seattle" }
		};

		if (obj is RecordStruct { Address: { City: "Seattle" } })
			Console.WriteLine("Seattle");

		if (obj is RecordStruct { Address.City: "Seattle" }) // Extended property pattern
			Console.WriteLine("Seattle");
	}
}
record class Address
{
	public string City { get; set; }
}
public readonly record struct Person(string FirstName, string LastName);
class Fields
{
	public string S = new('*', 3);
	public readonly System.Collections.Generic.Dictionary<string, object> Values = new();

	void DeclareInDeconstruct() {
		var p = (1, 2);
		(int x, int y) = p;
		(x, int z) = p;
		Console.WriteLine($"x={x},y={y},z={z}");
	}
}

class Lambdas
{
	[Obsolete($"Call {nameof(NaturalTypesOfMethodGroups)} instead")]
	void NaturalTypesOfLambdas()
	{
		var parse = (string s) => int.Parse(s);
		LambdaExpression parseExpr = (string s) => int.Parse(s);
	}

	void NaturalTypesOfMethodGroups()
	{
		var read = Console.Read;
	}

	void ReturnTypesForLambdas()
	{
		var choose = object (bool b) => b ? 1 : "two";
		var parse = (string t, out int v) => Int32.TryParse(t, out v);
	}

	void AttributesOnLambdas()
	{
		Func<string, int> parse = [Name("abc")] (s) => int.Parse(s);
	}
}
