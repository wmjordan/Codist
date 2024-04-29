using System;
using System.ComponentModel;
using System.Windows.Media;

namespace Codist.Margins
{
	/// <summary>The style for symbol references markers on the scrollbar margin.</summary>
	public sealed class SymbolReferenceMarkerStyle
	{
		internal static Color DefaultReferenceMarkerColor => Colors.Aqua;
		internal static Color DefaultWriteMarkerColor => Colors.Khaki;
		internal static Color DefaultSymbolDefinitionColor => Colors.Black;

		Color _ReferenceColor, _WriteColor, _SymbolDefinitionColor;

		public SymbolReferenceMarkerStyle() {
			Reset();
		}

		/// <summary>Gets or sets the foreground color to render the marker for symbol reference. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		[DefaultValue(Constants.EmptyColor)]
		public string ReferenceMarkerColor {
			get => _ReferenceColor.ToHexString();
			set => Parse(value, DefaultReferenceMarkerColor, out _ReferenceColor);
		}

		/// <summary>Gets or sets the foreground color to render the marker for value assignment. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		[DefaultValue(Constants.EmptyColor)]
		public string WriteMarkerColor {
			get => _WriteColor.ToHexString();
			set => Parse(value, DefaultWriteMarkerColor, out _WriteColor);
		}

		[DefaultValue(Constants.EmptyColor)]
		public string SymbolDefinitionColor {
			get => _SymbolDefinitionColor.ToHexString();
			set => Parse(value, DefaultSymbolDefinitionColor, out _SymbolDefinitionColor);
		}

		internal Color ReferenceMarker {
			get => _ReferenceColor;
			set => _ReferenceColor = value;
		}
		internal Color WriteMarker {
			get => _WriteColor;
			set => _WriteColor = value;
		}
		internal Color SymbolDefinition {
			get => _SymbolDefinitionColor;
			set => _SymbolDefinitionColor = value;
		}

		public void Reset() {
			_ReferenceColor = DefaultReferenceMarkerColor;
			_WriteColor = DefaultWriteMarkerColor;
			_SymbolDefinitionColor = DefaultSymbolDefinitionColor;
		}

		static void Parse(string value, Color defaultColor, out Color color) {
			if (value == Constants.EmptyColor) {
				color = DefaultSymbolDefinitionColor;
				return;
			}
			UIHelper.ParseColor(value, out color, out _);
		}
	}
}
