using System;

namespace TestProject
{
	//todo: Hover on IInterface to see size-limited Quick Info
	/// <summary>
	/// <para>Without limiting the size of Quick Info items, this comment can take up quite substantial of space when displayed.</para>
	/// <para>Go to Tools/Options/Codist/Super Quick Info/C# and find "Quick Info item size" options to limit its size.</para>
	/// <para>Scrollbars will be shown when the content is too long.</para>
	/// <para><see cref="IInterface"/> is inherited from <see cref="IDisposable"/>. Thus we must imprement the <see cref="IDisposable.Dispose"/> method as well.</para>
	/// </summary>
	interface IInterface : IDisposable // interface declaration
	{
		/// <summary>
		/// A property with a custom attribute.
		/// </summary>
		[System.ComponentModel.DefaultValue(MyEnum.OK | MyEnum.Sad)]
		MyEnum Property { get; set; }

		void VirtualMethod();
	}

	interface IMultiInterface : IInterface, System.Collections.Generic.ICollection<int>
	{
	}

#if EXCLUDED
	// Excluded code here
	class Unused
	{
	}
#endif

	partial class AbstractClass
	{
		protected AbstractClass(int property) {
			(this as IInterface).Property = (MyEnum)property;
		}
		// This property does not have an XML doc, however, the interface member has.
		// If we enables "Inherit from base type or interfaces" option in the "Super Quick Info" option page,
		// hover on the property you will read "Documentation from IInterface.Property".
		MyEnum IInterface.Property { get; set; } // explicit interface implementation
	}

	namespace Namespaces
	{
		class A : B, IA { }

		class B : C, IB, IExtra2, ICommon, IBOnly { }

		class C : IC, ICommon, ICOnly { }

		interface IA : IB { }

		interface IB : IExtra1 { }

		interface IBOnly : ICommonForBandC { }

		interface ICOnly : ICommonForBandC { }

		interface IC : IExtra2 { }

		interface IExtra1 { }

		interface IExtra2 { }

		interface ICommon { }

		interface ICommonForBandC { }
	}
}
