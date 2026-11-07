using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Codist;

record struct ArgumentAssignmentLocation(ArgumentAssignment Assignment, Location Location, ExpressionSyntax Expression);

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
