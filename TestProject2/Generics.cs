using System.Collections.Generic;

namespace TestProject.Generics;

[ApiVersion(13)]
public class RefStructConstraint<T> where T : allows ref struct
{
	// Use T as a ref struct:
	public void M(scoped T p) {
		// The parameter p must follow ref safety rules
	}
}

[ApiVersion(14)]
static class NameOfUnboundGeneric
{
	static readonly string list = nameof(List<>);
	const string dictionary = nameof(Dictionary<,>);
}
