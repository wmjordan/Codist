using System;
using System.Collections.Generic;

namespace TestProject
{
	static class ExtensionClass // static class
	{
		public static void Log(this string text) {
			var cc = new ConcreteClass(0);
			cc.VirtualMethod(); // call overriden method
			var ac = cc as AbstractClass;
			ac.VirtualMethod(); // call virtual method
		} // static method

		public static TType ExtensionWithConstraint<TType>(this TType type)
			where TType : struct, IConvertible {
			return type;
		}

		public static void MethodWithGenericTypeArguments(this List<string> list, List<List<string>> lists) {
			lists.Add(list);
		}
	}
}
