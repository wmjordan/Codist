using System;

namespace TestProject
{
#if DEBUG
	//todo: Move types into separated files
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
