using System;
using System.ComponentModel;
using System.Windows.Media;

namespace Codist.Margins
{
	/// <summary>The style for markers on the scrollbar margin.</summary>
	public sealed class MarkerStyle
	{
		public int Id { get => (int)StyleID; set => StyleID = (MarkerStyleTypes)value; }
		internal MarkerStyleTypes StyleID { get; set; }
		/// <summary>Gets or sets the foreground color to render the marker. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		[DefaultValue("#00000000")]
		public string ForegroundColor {
			get => ForeColor.ToHexString();
			set => ForeColor = UIHelper.ParseColor(value);
		}
		/// <summary>Gets or sets the foreground color to render the marker. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		[DefaultValue("#00000000")]
		public string BackgroundColor {
			get => BackColor.ToHexString();
			set => BackColor = UIHelper.ParseColor(value);
		}
		internal Color ForeColor { get; set; }
		internal Color BackColor { get; set; }

		internal MarkerStyle(MarkerStyleTypes style, Color foreColor) {
			StyleID = style;
			ForeColor = foreColor;
		}
	}
}
