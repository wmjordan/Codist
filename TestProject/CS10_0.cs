using System;
using System.Linq.Expressions;

namespace TestProject.CS10_0;
record struct RecordStruct
{
	public string Name { get; init; }
	public string Value { get; init; }
	public Address Address { get; init; }

	public RecordStruct NewName(string name)
	{
		return this with { Name = name };
	}

	void PropertyPattern()
	{
		object obj = new RecordStruct
		{
			Name = "Kathleen",
			Value = "Dollard",
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
	}

	void AttributesOnLambdas()
	{
		Func<string, int> parse = [Name("abc")] (s) => int.Parse(s);
	}
}
