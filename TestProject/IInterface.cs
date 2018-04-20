using System;

namespace TestProject
{
#if DEBUG
	//todo: Hover on IInterface to see size-limited Quick Info
	/// <summary>
	/// <para>Without limiting the size of Quick Info items, this comment can take up quite substantial of space when displayed.</para>
	/// <para>Go to Tools/Options/Codist/C#/Super Quick Info and find Quick Info item size options to limit its size.</para>
	/// <para>Scrollbars will be shown when the content is too long.</para>
	/// <para><see cref="IInterface"/> is inherited from <see cref="IDisposable"/>. Thus we must imprement the <see cref="IDisposable.Dispose"/> method as well.</para>
	/// </summary>
	interface IInterface : IDisposable // interface declaration
	{
		[System.ComponentModel.DefaultValue(MyEnum.OK | MyEnum.Sad)]
		MyEnum Property { get; set; }
	}
#else
	// Excluded code here
	class Unused
	{
	}
#endif
}
