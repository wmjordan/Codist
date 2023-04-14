using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist
{
	interface ICodeMemberTag : ITag
    {
        SnapshotSpan Span { get; }
		CodeMemberType Type { get; }
        int Level { get; }
		ICodeMemberTag Parent { get; }
		string Name { get; }
	}

	sealed class CodeBlock : ICodeMemberTag
	{
		public CodeBlock(CodeBlock parent, CodeMemberType type, string name, SnapshotSpan span, int level) {
			Parent = parent;
			parent?.Children.Add(this);
			Type = type;
			Name = name;
			Span = span;
			Level = level;
		}

		public CodeBlock Parent { get; }
		ICodeMemberTag ICodeMemberTag.Parent => Parent;
		public IList<CodeBlock> Children { get; } = new List<CodeBlock>();
		public SnapshotSpan Span { get; }
		public CodeMemberType Type { get; }
		public int Level { get; }

		public string Name { get; }
	}

	enum CodeMemberType
    {
        Root, Class, Interface, Struct, Type = Struct, Enum, Delegate, Member, Constructor, Property, Method, Field, Event, Other, Unknown
    }

	static class CodeMemberTypeExtensions
	{
		public static bool IsType(this CodeMemberType type) {
			return type > CodeMemberType.Root && type < CodeMemberType.Member;
		}

		public static bool IsMember(this CodeMemberType type) {
			return type > CodeMemberType.Member && type < CodeMemberType.Other;
		}
	}
}
