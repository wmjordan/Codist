using System;
using System.ComponentModel;
using System.Windows.Media;

namespace Codist.Margins
{
	/// <summary>The style for symbol references markers on the scrollbar margin.</summary>
	public sealed class SymbolReferenceMarkerStyle
	{
		Color _ReferenceColor, _WriteColor;
		SolidColorBrush _Reference, _Write;
		Pen _SetNull;

		public SymbolReferenceMarkerStyle() {
			Reset();
		}

		/// <summary>Gets or sets the foreground color to render the marker for symbol reference. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		[DefaultValue(Constants.EmptyColor)]
		public string ReferenceMarkerColor {
			get => _ReferenceColor.ToHexString();
			set {
				UIHelper.ParseColor(value, out _ReferenceColor, out _);
				_Reference = new SolidColorBrush(_ReferenceColor.A != 0 ? _ReferenceColor : DefaultReferenceMarkerColor);
			}
		}

		/// <summary>Gets or sets the foreground color to render the marker for value assignment. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		[DefaultValue(Constants.EmptyColor)]
		public string WriteMarkerColor {
			get => _WriteColor.ToHexString();
			set {
				UIHelper.ParseColor(value, out _WriteColor, out _);
				_Write = new SolidColorBrush(_WriteColor.A != 0 ? _WriteColor : DefaultWriteMarkerColor);
				_SetNull = _Write != null ? new Pen(_Write, 1) : null;
			}
		}

		internal SolidColorBrush ReferenceMarkerBrush => _Reference;
		internal SolidColorBrush WriteMarkerBrush => _Write;
		internal Pen SetNullPen => _SetNull;
		internal static Color DefaultReferenceMarkerColor => Colors.Aqua;
		internal static Color DefaultWriteMarkerColor => Colors.Khaki;

		public void Reset() {
			_ReferenceColor = DefaultReferenceMarkerColor;
			_WriteColor = DefaultWriteMarkerColor;
			_Reference = new SolidColorBrush(_ReferenceColor);
			_Write = new SolidColorBrush(_WriteColor);
			_SetNull = new Pen(_Write, 1);
		}
	}
}
