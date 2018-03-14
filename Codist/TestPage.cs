using System;
using System.Linq; // unnccessary code
using System.Runtime.InteropServices;

namespace Codist.Fake
{
#if DEBUG
	//todo: Move types into separated files
	interface IInterface : IDisposable // interface declaration
	{
		[System.ComponentModel.DefaultValue(MyEnum.OK | MyEnum.Sad)]
		MyEnum Property { get; set; }
	}
	[Flags]
	enum MyEnum : ushort // enum declaration
	{
		None = 0, OK = 1, Happy = 1 << 1, Sad = 1 << 2, Composite = OK | Happy, Unknown = 0xFFFF
	}
	[System.ComponentModel.Description("demo")]
	struct MyStruct // struct declaration
	{
		//note hover on Constant to see its value
		private const short Constant = ushort.MaxValue ^ 0xF0F0; // const field
		private const string ConstantString = "literal string"; // const string
		private static int _static = (int)DateTime.Now.Ticks; // static field
		private static readonly int _staticReadonly = Int32.MinValue; // static readonly field
		private int _instanceField; // field
		private readonly int _readonlyField; // readonly field

		public readonly static DateTime StartDate = DateTime.Now; // public readonly static field
	}
	abstract class AbstractClass : IInterface
	{
		protected abstract int Property { get; set; } // protected abstract property
		MyEnum IInterface.Property { get; set; } // explicit interface implementation
		public void Method() { Property++; }
		protected abstract int AbstractMethod(); // abstract method
		public virtual void VirtualMethod() { } // virtual method
		void IDisposable.Dispose() { } // explicit interface implementation
	}
	static class ExtensionClass // static class
	{
		public static void Log(this string text) { } // static method
	}
	sealed class ConcreteClass : AbstractClass
	{
		delegate void Clone<T>(T text);
		event EventHandler<EventArgs> MyEvent;

		/// <summary>
		/// Creates a new instance of <see cref="ConcreteClass"/>.
		/// </summary>
		/// <param name="fieldId">The field.</param>
		/// <example><code><![CDATA[System.Console.WriteLine(Codist.Constants.NameOfMe);]]></code></example>
		// Todo++ hover on the "{" below to see line count of this method block
		public ConcreteClass(int fieldId) {
			const int A = 1; // local constant
			var localField = fieldId; // local field
			@"Multiline
text".Log(); // multiline string (string verbatim)
			$"Test page {fieldId} is initialized" // interpolated string
				.Log(); // calling extension method

			switch ((MyEnum)fieldId) {
				case MyEnum.None:
					break; // normal swtich break
				case MyEnum.OK:
					return; // control flow keyword
				default:
					throw new NotImplementedException(fieldId.ToString() + " is not supported"); // control flow keyword
			}
			for (int i = 0; i < 0XFF; i++) {
				if (i == localField) {
					continue; // control flow keyword
				}
				else if (i > A) {
					break; // control flow keyword
				}
				else if (i > 2) {
					goto END; // label
				}
				else {
					throw new InvalidOperationException();
				}
			}
			END: // label
			MyEvent += TestPage_MyEvent;
		}

		private void TestPage_MyEvent(object sender, EventArgs e) {
			var anonymous = new { // anonymous type
				sender, // property of anonymous type
				@event = e,
				time = DateTime.Now
			};
		}

		protected override int Property { get; set; }

		public new void Method() { Property--; }

		public void Method<TGeneric>() { //type parameter
			// unnecessary code
			Codist.Fake.ExtensionClass.Log(typeof(TGeneric).Name); // extension method
			NativeMethods.ExternMethod(name: "none", ptr: IntPtr.Zero); // extern method
			AbstractMethod(); // overridden abstract method
			VirtualMethod(); // overridden virtual method
			base.VirtualMethod(); // base virtual method
			MyEvent(this, EventArgs.Empty); // event
		}

		protected override int AbstractMethod() { // overridden method
			throw new NotImplementedException();
		}

		public override void VirtualMethod() { // overridden method
			base.VirtualMethod();
		}

		static class NativeMethods
		{
			[DllImport("dummy.dll", EntryPoint = "DummyFunction", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
			public static extern void ExternMethod(IntPtr ptr, string name);
		}
	}
#else
	// Excluded code here
	class Unused
	{
	}
#endif
}
