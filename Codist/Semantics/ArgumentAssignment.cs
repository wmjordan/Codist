using System;

namespace Codist
{
	enum ArgumentAssignment
	{
		Normal,
		Default,
		NameValue,
		ImplicitlyConverted,
		ImplicitlyConvertedNameValue,
	}

	enum ArgumentAssignmentFilter
	{
		Undefined,
		ExplicitValue,
		DefaultValue
	}
}
