using System;
using System.Linq; // unnccessary code
using System.Runtime.InteropServices;

namespace TestProject
{
	abstract partial class AbstractClass : IInterface
	{
		protected abstract int Property { [DispId(1)] get; [DispId(2)] set; } // protected abstract property
		public void Method() { Property++; }
		protected abstract int AbstractMethod(); // abstract method
		/// <summary>
		/// The virtual method does nothing by default.
		/// <para>Nor does <see cref="Property"/> do anything.</para>
		/// </summary>
		public virtual void VirtualMethod() { } // virtual method
		void IDisposable.Dispose() { } // explicit interface implementation
	}
	sealed class ConcreteClass : AbstractClass, IInterface
	{
		// note hover on the "{" below to see line count of this method block
		/// <summary>
		/// Creates a new instance of <see cref="ConcreteClass"/>.
		/// </summary>
		/// <param name="fieldId">The field.</param>
		/// <example><code><![CDATA[System.Console.WriteLine(Codist.Constants.NameOfMe);]]></code></example>
		public ConcreteClass(int fieldId) : base(0) {
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

		protected override int Property { get; set; } = 1;
		public int PropertyAddOne => Property + 1;

		public new void Method() { Property--; }

		public void Method<TGeneric>() { //type parameter
			// unnecessary code
			TestProject.ExtensionClass.Log(typeof(TGeneric).Name); // extension method
			NativeMethods.ExternMethod(value: 1, ptr: IntPtr.Zero); // extern method
			AbstractMethod(); // overridden abstract method
			VirtualMethod(); // overridden virtual method
			base.VirtualMethod(); // base virtual method
			MyEvent(this, EventArgs.Empty); // event
			this.Method(); // qualified invocation
		}

		protected override int AbstractMethod() { // overridden method
			throw new NotImplementedException();
		}

		public override void VirtualMethod() { // overridden method
			base.VirtualMethod();
			List(1, "abc", null, String.Empty);
		}

		ConcreteClass InvokeDelegateParameter(Clone<ConcreteClass> clone) {
			try {
				return clone(this); // invoking delegate
			}
			catch (Exception ex) {
				throw new InvalidOperationException("Error when calling delegate", ex);
			}
		}

		static string[] List(int value, params string[] text) {
			return Array.ConvertAll(
				Array.FindAll(text, t => t != null),
				s => { return (value++).ToString() + "." + text[value - 1]; }
			);
		}

		/// <summary>A generic delegate with a parameter.</summary>
		/// <typeparam name="TObject">The generic type parameter of the delegate.</typeparam>
		/// <param name="obj">The method parameter of type <typeparamref name="TObject"/>.</param>
		/// <remarks>Don't take this too serious.</remarks>
		/// <returns>Returns an instance of the generic type parameter.</returns>
		delegate TObject Clone<TObject>(TObject obj);
		event EventHandler<EventArgs> MyEvent;

		/// <summary>
		/// Nested class
		/// </summary>
		static class NativeMethods
		{
			[Obsolete]
			[System.ComponentModel.Description("An extern method")]
			[DllImport("dummy.dll",
				EntryPoint = "DummyFunction",
				CallingConvention = CallingConvention.Cdecl,
				SetLastError = false)]
			public static extern void ExternMethod(
				[In, Out] IntPtr ptr,
				[In] int value
			);
		}
	}
}
