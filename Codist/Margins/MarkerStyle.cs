using System;
using System.ComponentModel;
using System.Windows.Media;

namespace Codist.Margins
{
	/// <summary>The style for markers on the scrollbar margin.</summary>
	public sealed class MarkerStyle
	{
		Color _ForeColor, _BackColor;

		public int Id { get => (int)StyleID; set => StyleID = (MarkerStyleTypes)value; }

		internal MarkerStyleTypes StyleID { get; set; }

		/// <summary>Gets or sets the foreground color to render the marker. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		[DefaultValue(Constants.EmptyColor)]
		public string ForegroundColor {
			get => ForeColor.ToHexString();
			set => UIHelper.ParseColor(value, out _ForeColor, out _);
		}

		/// <summary>Gets or sets the foreground color to render the marker. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		[DefaultValue(Constants.EmptyColor)]
		public string BackgroundColor {
			get => BackColor.ToHexString();
			set => UIHelper.ParseColor(value, out _BackColor, out _);
		}

		internal Color ForeColor { get => _ForeColor; set => _ForeColor = value; }
		internal Color BackColor { get => _BackColor; set => _BackColor = value; }

		internal MarkerStyle(MarkerStyleTypes style, Color foreColor) {
			StyleID = style;
			ForeColor = foreColor;
		}
	}
}
