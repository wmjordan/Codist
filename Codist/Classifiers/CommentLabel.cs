using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Media;
using Newtonsoft.Json;

namespace Codist.Classifiers
{

	[DebuggerDisplay("{Label} IgnoreCase: {IgnoreCase} AllowPunctuationDelimiter: {AllowPunctuationDelimiter}")]
	sealed class CommentLabel
	{
		string _label;
		int _labelLength;
		StringComparison _stringComparison;

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
			get { return _label; }
			set {
				value = value != null ? value.Trim() : String.Empty;
				_label = value;
				_labelLength = value.Length;
			}
		}
		internal int LabelLength => _labelLength;
		/// <summary>Gets or sets whether the label is case-sensitive.</summary>
		public bool IgnoreCase {
			get => _stringComparison == StringComparison.OrdinalIgnoreCase;
			set => _stringComparison = value ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		}
		public CommentStyleApplication StyleApplication { get; set; }
		internal StringComparison Comparison => _stringComparison;
		/// <summary>Gets or sets the comment style.</summary>
		public CommentStyleTypes StyleID { get; set; }

		public CommentLabel Clone() {
			return (CommentLabel)MemberwiseClone();
		}
		public void CopyTo(CommentLabel label) {
			label.AllowPunctuationDelimiter = AllowPunctuationDelimiter;
			label.StyleApplication = StyleApplication;
			label.StyleID = StyleID;
			label._label = _label;
			label._labelLength = _labelLength;
			label._stringComparison = _stringComparison;
		}
	}
}
