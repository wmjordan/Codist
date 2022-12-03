using System;

namespace Codist
{
	[Flags]
	enum SymbolUsageKind
	{
		Normal,
		External = 1,
		Container = 1 << 1,
		Write = 1 << 2,
		Delegate = 1 << 3,
		Attach = 1 << 5,
		Detach = 1 << 6,
		TypeCast = 1 << 7,
		TypeParameter = 1 << 8,
		Catch = 1 << 9,
		Trigger = 1 << 10,
		SetNull = 1 << 17,
		Usage = Delegate | Write | Attach | Detach | TypeCast | TypeParameter | Catch | Trigger
	}
}
