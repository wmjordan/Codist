using System;
using System.Diagnostics;

namespace Codist.Taggers
{
	[DebuggerDisplay("{Label} IgnoreCase: {IgnoreCase} AllowPunctuationDelimiter: {AllowPunctuationDelimiter}")]
	sealed class CommentLabel
	{
		string _Label;

		public CommentLabel() {
		}

		public CommentLabel(string label, CommentStyleTypes styleID) {
			Label = label;
			StyleID = styleID;
		}
		public CommentLabel(string label, CommentStyleTypes styleID, bool ignoreCase) {
			Label = label;
			StyleID = styleID;
			IgnoreCase = ignoreCase;
		}

		public bool AllowPunctuationDelimiter { get; set; }

		/// <summary>Gets or sets the label to identifier the comment type.</summary>
		public string Label {
			get { return _Label; }
			set {
				value = value != null ? value.Trim() : String.Empty;
				_Label = value;
				LabelLength = value.Length;
			}
		}
		internal int LabelLength { get; private set; }
		/// <summary>Gets or sets whether the label is case-sensitive.</summary>
		public bool IgnoreCase {
			get => Comparison == StringComparison.OrdinalIgnoreCase;
			set => Comparison = value ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		}
		public CommentStyleApplication StyleApplication { get; set; }
		internal StringComparison Comparison { get; private set; }
		/// <summary>Gets or sets the comment style.</summary>
		public CommentStyleTypes StyleID { get; set; }

		public CommentLabel Clone() {
			return (CommentLabel)MemberwiseClone();
		}
		public void CopyTo(CommentLabel label) {
			label.AllowPunctuationDelimiter = AllowPunctuationDelimiter;
			label.StyleApplication = StyleApplication;
			label.StyleID = StyleID;
			label._Label = _Label;
			label.LabelLength = LabelLength;
			label.Comparison = Comparison;
		}
	}
}
