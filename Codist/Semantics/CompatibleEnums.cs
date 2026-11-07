using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Codist;

// note:
// This file contains enum values for corresponding enums introduced in newer roslyn versions.
// By doing so, we make Codist compatible with the old VS 2017 thru the newest VS

/// <summary><see cref="SyntaxKind"/></summary>
enum SyntaxKindEx
{
	DotDotToken = 8222,
	QuestionQuestionEqualsToken = 8284,
	GreaterThanGreaterThanGreaterThanToken = 8286,
	WithKeyword = 8442,
	InitKeyword = 8443,
	RecordKeyword = 8444,
	ManagedKeyword = 8445,
	UnmanagedKeyword = 8446,
	RequiredKeyword = 8447,
	ExtensionKeyword = 8451,
	SingleLineRawStringLiteralToken = 8518,
	MultiLineRawStringLiteralToken = 8519,
	Utf8StringLiteralToken = 8520,
	ImplicitObjectCreationExpression = 8659,
	FileScopedNamespaceDeclaration = 8845,
	ConstantPattern = 9002,
	CasePatternSwitchLabel = 9009,
	RecursivePattern = 9020,
	PropertyPatternClause = 9021,
	PositionalPatternClause = 9023,
	DiscardPattern = 9024,
	SwitchExpression = 9025,
	SwitchExpressionArm = 9026,
	VarPattern = 9027,
	RelationPattern = 9029,
	TypePattern = 9030,
	OrPattern = 9031,
	AndPattern = 9032,
	NotPattern = 9033,
	SlicePattern = 9034,
	ListPatternExpression = 9035,
	FunctionPointerCallingConvention = 9059,
	InitAccessorDeclaration = 9060,
	WithInitializerExpression = 9062,
	RecordDeclaration = 9063,
	PrimaryConstructorBaseType = 9065,
	RecordStructDeclaration = 9068,
	InterpolatedSingleLineRawStringStartToken = 9072,
	InterpolatedMultiLineRawStringStartToken = 9073,
	CollectionExpression = 9076,
	ExtensionDeclaration = 9079,
	UnionDeclaration = 9082,
}

/// <summary><see cref="SymbolKind"/></summary>
enum SymbolKindEx
{
	FunctionPointerType = 20
}

/// <summary><see cref="TypeKind"/></summary>
enum TypeKindEx
{
	FunctionPointer = 13,
	Extension = 14
}

/// <summary><see cref="MethodKind"/></summary>
enum MethodKindEx
{
	FunctionPointerMethod = 18
}

/// <summary><see cref="RefKind"/></summary>
enum RefKindEx
{
	RefReadonly = 4
}