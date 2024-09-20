using System;
using System.Windows.Media;

namespace Codist.SyntaxHighlight
{
	[AttributeUsage(AttributeTargets.Field)]
	sealed class StyleAttribute : Attribute
	{
		public bool Bold { get; set; }
		public bool Italic { get; set; }
		public bool Underline { get; set; }
		public double Size { get; set; }
		public Color ForeColor { get; set; }
		public Color BackColor { get; set; }

		public StyleAttribute() {}

		public StyleAttribute(bool bold) {
			Bold = bold;
		}
		public StyleAttribute(string foreColor) {
			UIHelper.ParseColor(foreColor, out var c, out var o);
			ForeColor = c.Alpha(o);
		}
		public StyleAttribute(string foreColor, string backColor) {
			UIHelper.ParseColor(foreColor, out var c, out var o);
			ForeColor = c.Alpha(o);
			UIHelper.ParseColor(backColor, out c, out o);
			BackColor = c.Alpha(o);
		}
	}
}
