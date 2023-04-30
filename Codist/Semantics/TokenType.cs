using System;

namespace Codist
{
	[Flags]
	public enum TokenType
	{
		None,
		Letter = 1,
		Digit = 2,
		Dot = 4,
		Underscore = 8,
		Guid = 16,
		GuidPlaceHolder = 32,
		Hex = 64,
		ZeroXHex = 128
	}
}
