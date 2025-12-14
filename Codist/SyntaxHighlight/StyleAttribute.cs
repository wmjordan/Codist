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
			ColorHelper.ParseColor(foreColor, out var c, out var o);
			ForeColor = o != 0 ? c.Alpha(o) : c;
		}
		public StyleAttribute(string foreColor, string backColor) {
			ColorHelper.ParseColor(foreColor, out var c, out var o);
			ForeColor = o != 0 ? c.Alpha(o) : c;
			ColorHelper.ParseColor(backColor, out c, out o);
			BackColor = o != 0 ? c.Alpha(o) : c;
		}
	}
}
