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
}
