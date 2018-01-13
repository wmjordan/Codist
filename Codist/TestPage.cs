using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Codist.Fake
{
#if DEBUG
	interface IInterface
	{
		int Property { get; set; }
	}
	[Flags]
	enum MyEnum
	{
		None = 0, OK = 1
	}
	abstract class Abstract
	{
		protected abstract int P { get; set; }
		protected abstract int AbstractMethod();
		public virtual void VirtualMethod() { }
	}
	class TestPage : Abstract
	{
		private int _field;
		private readonly int _readonlyField;
		static int _staticField;
		static readonly int _staticReadonlyField = 10;
		const string ConstantField = "literal constant";
		delegate void Clone<T>(T text);
		event EventHandler<EventArgs> MyEvent;

		public TestPage(int fieldId) {
			const int A = 1;
			_field = fieldId;
			_staticField = (int)DateTime.Now.Ticks; // static and instance property

			@"Multiline
text".Log();
			$"Test page {fieldId} is initialized".Log();

			switch ((MyEnum)fieldId) {
				case MyEnum.None:
					break;
				case MyEnum.OK:
					return;
				default:
					throw new NotImplementedException(fieldId.ToString() + " is not supported");
			}
			for (int i = 0; i < fieldId; i++) {
				if (i == 0) {
					continue;
				}
				else if (i > A) {
					break;
				}
				else if (i > 2) {
					goto END;
				}
				else {
					throw new InvalidOperationException();
				}
			}
			END:
			MyEvent += TestPage_MyEvent;
		}

		private void TestPage_MyEvent(object sender, EventArgs e) {
			var anonymous = new {
				sender,
				@event = e,
				time = DateTime.Now
			};
		}

		protected override int P { get; set; }

		public void Method<TGeneric>() {
			// unnecessary code
			Codist.Fake.ExtensionClass.Log(typeof(TGeneric).Name);
			NativeMethods.Print(IntPtr.Zero);
			AbstractMethod();
			VirtualMethod();
			MyEvent(this, EventArgs.Empty);
		}

		protected override int AbstractMethod() {
			throw new NotImplementedException();
		}

		public override void VirtualMethod() {
			base.VirtualMethod();
		}

		static class NativeMethods
		{
			[DllImport("dummy.dll", EntryPoint = "DummyFunction")]
			public static extern void Print(IntPtr ptr);
		}
	}

	static class ExtensionClass
	{
		public static void Log(this string text) { }
	}
#else
	// Excluded code here
#endif
}
