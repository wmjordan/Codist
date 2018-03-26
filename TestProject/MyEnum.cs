using System;

namespace TestProject
{
	[Flags]
	enum MyEnum : ushort // enum declaration
	{
		None = 0, OK = 1, Happy = 1 << 1, Sad = 1 << 2, Composite = OK | Happy, Unknown = 0xFFFF
	}
}
