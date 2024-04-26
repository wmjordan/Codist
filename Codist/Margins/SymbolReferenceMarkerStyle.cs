using System;
using System.ComponentModel;
using System.Windows.Media;

namespace Codist.Margins
{
	/// <summary>The style for symbol references markers on the scrollbar margin.</summary>
	public sealed class SymbolReferenceMarkerStyle
	{
		Color _ReferenceColor, _WriteColor;

		public SymbolReferenceMarkerStyle() {
			Reset();
		}

		/// <summary>Gets or sets the foreground color to render the marker for symbol reference. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		[DefaultValue(Constants.EmptyColor)]
		public string ReferenceMarkerColor {
			get => _ReferenceColor.ToHexString();
			set {
				UIHelper.ParseColor(value, out _ReferenceColor, out _);
			}
		}

		/// <summary>Gets or sets the foreground color to render the marker for value assignment. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		[DefaultValue(Constants.EmptyColor)]
		public string WriteMarkerColor {
			get => _WriteColor.ToHexString();
			set {
				UIHelper.ParseColor(value, out _WriteColor, out _);
			}
		}

		internal Color ReferenceMarker => _ReferenceColor;
		internal Color WriteMarker => _WriteColor;
		internal static Color DefaultReferenceMarkerColor => Colors.Aqua;
		internal static Color DefaultWriteMarkerColor => Colors.Khaki;

		public void Reset() {
			_ReferenceColor = DefaultReferenceMarkerColor;
			_WriteColor = DefaultWriteMarkerColor;
		}
	}
}
