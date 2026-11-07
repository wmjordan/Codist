using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestProject;

namespace TestProject2.Types.Derivation;

public record class Cat(string Name);
public record class Dog(string Name);
public record class Bird(string Name);

[ApiVersion(15)]
public union Pet(Cat, Dog, Bird)
{
	void GetName(Pet pet) {
		string name = pet switch {
			Dog d => d.Name,
			Cat c => c.Name,
			Bird b => b.Name,
			// all situations handled
		};
	}
}

public closed record class GateState
{
    string Describe() => this switch
    {
        Closed => "closed",
        Open(var percent) => $"{percent}% open",
        // no warning, all subclasses handled
    };
}

public record class Closed : GateState;
public record class Open(float Percent) : GateState;

