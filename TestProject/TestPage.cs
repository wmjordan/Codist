using System;
using System.Linq; // unnecessary code
using System.Runtime.InteropServices;
using A = System.EventArgs;

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
				case MyEnum.None: break; // normal switch break
				case MyEnum.OK: return; // control flow keyword
				default:
					throw new NotImplementedException(fieldId.ToString()); // control flow keyword
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
			Method<ConcreteClass>();
		}

		private void TestPage_MyEvent(object sender, A e) {
			var anonymous = new { // anonymous type
				sender, // property of anonymous type
				@event = e,
				time = DateTime.Now,
				one = '\u4e00'
			};
			Console.WriteLine(anonymous);
		}

		protected override int Property { get; set; } = 1;
        public DateTime InitDate { get; } = DateTime.Now;
		public int PropertyAddOne => unchecked(Property + 1);

		public new void Method() { checked { Property--; } }

		/// <typeparam name="TGeneric">Any type</typeparam>
		public void Method<TGeneric>() { //type parameter
			#region Test Methods
			// unnecessary code
			TestProject.ExtensionClass.Log(typeof(TGeneric).Name); // extension method
			NativeMethods.ExternMethod(value: 1, ptr: IntPtr.Zero); // extern method
			NativeMethods.ExternMethod(IntPtr.Zero, 1); // extern method
			AbstractMethod(); // overridden abstract method
			VirtualMethod(); // overridden virtual method
			base.VirtualMethod(); // base virtual method
			MyEvent(this, EventArgs.Empty); // event
			this.Method(); // qualified invocation
			#endregion
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
				Array.FindAll(text, t => t != null && t.Length > 0),
				s => (value++).ToString() + "." + text[value - 1] // captured variables: value, text
			);
		}

		/// <summary>A generic delegate with a parameter.</summary>
		/// <typeparam name="TObject">The generic type parameter of the delegate.</typeparam>
		/// <param name="obj">The method parameter of type <typeparamref name="TObject"/>.</param>
		/// <remarks>Don't take this too serious.</remarks>
		/// <returns>Returns an instance of the generic type parameter.</returns>
		delegate TObject Clone<TObject>(TObject obj);

		/// <summary>A generic delegate with a parameter.</summary>
		/// <typeparam name="TFrom">The generic type parameter of the delegate.</typeparam>
		/// <typeparam name="TTo">  </typeparam>
		delegate TTo CloneAs<TFrom, TTo>(TFrom from);

		event EventHandler<EventArgs> MyEvent, MoreEvent;

		/// <summary>
		/// Nested class
		/// </summary>
		static class NativeMethods
		{
			[Obsolete]
			[System.ComponentModel.Description("An extern method")]
			[DllImport("dummy.dll", // multiline attribute annotations
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
