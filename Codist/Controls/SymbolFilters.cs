using System;

namespace Codist.Controls
{
	interface ISymbolFilterable
	{
		SymbolFilterKind SymbolFilterKind { get; }
		void Filter(string[] keywords, int filterFlags);
	}
	interface ISymbolFilter
	{
		bool Filter(int filterFlags);
	}
	[Flags]
	enum MemberFilterTypes
	{
		None,
		Field = 1,
		Property = 1 << 1,
		FieldOrProperty = Field | Property,
		Event = 1 << 2,
		Method = 1 << 3,
		TypeAndNamespace = 1 << 4,
		AllMembers = FieldOrProperty | Event | Method | TypeAndNamespace,
		Public = 1 << 5,
		Protected = 1 << 6,
		Internal = 1 << 7,
		Private = 1 << 8,
		AllAccessibilities = Public | Protected | Private | Internal,
		Class = 1 << 10,
		Struct = 1 << 11,
		Enum = 1 << 12,
		StructAndEnum = Struct | Enum,
		Delegate = 1 << 13,
		Interface = 1 << 14,
		Namespace = 1 << 15,
		AllTypes = Class | StructAndEnum | Delegate | Interface | Namespace,
		Static = 1 << 17,
		Instance = 1 << 18,
		AllInstance = Static | Instance,
		Read = 1 << 20,
		Write = 1 << 21,
		TypeCast = 1 << 22,
		TypeReference = 1 << 23,
		Trigger = 1 << 24,
		AllUsages = Read | Write | TypeCast | TypeReference | Trigger,
		All = AllMembers | AllAccessibilities | AllTypes | AllInstance | AllUsages
	}
	enum ScopeType
	{
		Undefined,
		ActiveDocument,
		ActiveProject,
		Solution,
		OpenedDocument
	}
	enum SymbolFilterKind
	{
		Undefined,
		Member,
		Node,
		Type,
		Usage
	}
}
