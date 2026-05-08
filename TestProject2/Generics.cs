using System.Collections.Generic;
using IntDict = System.Collections.Generic.Dictionary<int, object>;
using StringDict = System.Collections.Generic.Dictionary<string, object>;

namespace TestProject.Generics;

class GenericReferenceTest
{
	static void UseStringDictionary() {
		var d = new StringDict();
		ReferenceMember(d);
	}

	static void UseIntDictionary() {
		var d = new IntDict();
		ReferenceMember(d);
	}

	static void ReferenceMember<TKey, TValue>(Dictionary<TKey, TValue> d) {
		var c = d.Count;
		foreach (var item in d) {
			var s = item.Key;
			var o = item.Value;
		}
	}
}

class IntDictionary : IntDict { }
class LongDictionary : Dictionary<long, object> { }
class StringDictionary : StringDict { }

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
