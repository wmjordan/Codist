using System;

namespace Codist.SymbolCommands
{
	[Flags]
	enum CommandOptions
	{
		Default,
		CurrentFile = 1,
		CurrentProject = 1 << 1,
		RelatedProjects = 1 << 2,
		SourceCode = 1 << 3,
		External = 1 << 4,
		ExtractMatch = 1 << 5,
		DirectDerive = 1 << 6,
		MatchCase = 1 << 7,
		Explicit = 1 << 8,
		Implicit = 1 << 9,
		MatchTypeArgument = 1 << 10,
		NoTypeArgument = 1 << 11,
	}

}
