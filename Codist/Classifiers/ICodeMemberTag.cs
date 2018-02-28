using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist
{
	public interface ICodeMemberTag : ITag
    {
        SnapshotSpan Span { get; }
		CodeMemberType Type { get; }
        int Level { get; }
		ICodeMemberTag Parent { get; }
		string Name { get; }
	}

	public class CodeBlock : ICodeMemberTag
	{
		readonly SnapshotSpan _Span;
		readonly CodeBlock _Parent;
		readonly List<CodeBlock> _Children = new List<CodeBlock>();
		readonly CodeMemberType _MemberType;
		readonly string _Name;
		readonly int _Level;

		public CodeBlock(CodeBlock parent, CodeMemberType type, string name, SnapshotSpan span, int level) {
			_Parent = parent;
			if (parent != null) {
				parent._Children.Add(this);
			}
			_MemberType = type;
			_Name = name;
			_Span = span;
			_Level = level;
		}

		public CodeBlock Parent => _Parent;
		public IList<CodeBlock> Children => _Children;
		public SnapshotSpan Span => _Span;
		public CodeMemberType Type => _MemberType;
		ICodeMemberTag ICodeMemberTag.Parent => _Parent;
		public int Level => _Level;
		public string Name => _Name;
	}

	public enum CodeMemberType
    {
        Root, Class, Interface, Struct, Type = Struct, Enum, Delegate, Member, Constructor, Property, Method, Field, Event, Other, Unknown
    }
}
