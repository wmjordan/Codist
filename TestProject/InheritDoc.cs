using System;

namespace TestProject
{

	[Serializable]
	public class MyException : Exception
	{
		// hover on following constructor methods to see XML Doc from type Exception
		public MyException() { }
		public MyException(string message) : base(message) { }
		public MyException(string message, Exception inner) : base(message, inner) { }
		protected MyException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}

	class TestInheritDoc
	{
		/// <summary>
		/// The description of method.
		/// </summary>
		/// <param name="param">The description of parameter.</param>
		public static void Method(string param) {
		}

		// turn on <inheritdoc cref=""/> feature in the options page of Super Quick Info,
		// hover on the following method to see it inherit doc from the above method
		/// <inheritdoc cref="Method(string)"/>
		/// <param name="value">The value of the method.</param>
		public void InheritFromMethod(string param, string value) {

		}
	}
}
