using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Media;
using AppHelpers;
using Microsoft.VisualStudio.Text.Classification;
using Newtonsoft.Json;

namespace Codist.SyntaxHighlight
{
	/// <summary>The base style for syntax highlight elements.</summary>
	abstract class StyleBase
	{
		static protected readonly Regex FriendlyNamePattern = new Regex(@"([a-z])([A-Z0-9])", RegexOptions.Singleline);
		Color _ForeColor, _BackColor, _LineColor;
		byte _ForeColorOpacity, _BackColorOpacity, _LineOpacity, _LineThickness, _LineOffset;

		internal abstract int Id { get; }
		/// <summary>Gets or sets whether the content rendered in bold.</summary>
		public bool? Bold { get; set; }
		/// <summary>Gets or sets whether the content rendered in italic.</summary>
		public bool? Italic { get; set; }
		/// <summary>Gets or sets whether the content rendered with overline.</summary>
		public bool? OverLine { get; set; }
		/// <summary>Gets or sets whether the content rendered stricken-through.</summary>
		public bool? Strikethrough { get; set; }
		/// <summary>Gets or sets whether the content rendered with underline.</summary>
		public bool? Underline { get; set; }
		/// <summary>Gets or sets whether the text is stretched.</summary>
		public int? Stretch { get; set; }
		/// <summary>Gets or sets the font size. Font size number is relative to the editor text size.</summary>
		public double FontSize { get; set; }
		/// <summary>Gets or sets the foreground color to render the text. The color format could be #AARRGGBB or #RRGGBB.</summary>
		[DefaultValue(Constants.EmptyColor)]
		[JsonProperty("ForegroundColor")]
		public string ForeColorText {
			get => _ForeColor.A == 0 && _ForeColorOpacity == 0 ? Constants.EmptyColor
				: _ForeColor.A > 0 && _ForeColorOpacity == 0 ? _ForeColor.ToHexString()
				: _ForeColor.A == 0 && _ForeColorOpacity > 0 ? "#" + _ForeColorOpacity.ToString("X2")
				: _ForeColor.Alpha(_ForeColorOpacity).ToHexString();
			set => UIHelper.ParseColor(value, out _ForeColor, out _ForeColorOpacity);
		}
		/// <summary>Gets or sets the background color to render the text. The color format could be #AARRGGBB or #RRGGBB.</summary>
		[DefaultValue(Constants.EmptyColor)]
		[JsonProperty("BackgroundColor")]
		public string BackColorText {
			get => _BackColor.A == 0 && _BackColorOpacity == 0 ? Constants.EmptyColor
				: _BackColor.A > 0 && _BackColorOpacity == 0 ? _BackColor.ToHexString()
				: _BackColor.A == 0 && _BackColorOpacity > 0 ? "#" + _BackColorOpacity.ToString("X2")
				: _BackColor.Alpha(_BackColorOpacity).ToHexString();
			set => UIHelper.ParseColor(value, out _BackColor, out _BackColorOpacity);
		}
		/// <summary>Gets or sets the brush effect to draw the background color.</summary>
		[DefaultValue(BrushEffect.Solid)]
		public BrushEffect BackgroundEffect { get; set; }
		/// <summary>Gets or sets the underline color to render the text. The color format could be #AARRGGBB or #RRGGBB.</summary>
		[DefaultValue(Constants.EmptyColor)]
		[JsonProperty("LineColor")]
		public string LineColorText {
			get => _LineColor.A == 0 && _LineOpacity == 0 ? Constants.EmptyColor
				: _LineColor.A > 0 && _LineOpacity == 0 ? _LineColor.ToHexString()
				: _LineColor.A == 0 && _LineOpacity > 0 ? "#" + _LineOpacity.ToString("X2")
				: _LineColor.Alpha(_LineOpacity).ToHexString();
			set => UIHelper.ParseColor(value, out _LineColor, out _LineOpacity);
		}
		[DefaultValue(LineStyle.Solid)]
		public LineStyle LineStyle { get; set; }
		[DefaultValue((byte)0)]
		public byte LineThickness { get => _LineThickness; set => _LineThickness = value; }
		[DefaultValue((byte)0)]
		public byte LineOffset { get => _LineOffset; set => _LineOffset = value; }
		/// <summary>Gets or sets the style of marker on the scrollbar.</summary>
		[DefaultValue(ScrollbarMarkerStyle.None)]
		public ScrollbarMarkerStyle ScrollBarMarkerStyle { get; set; }
		/// <summary>Gets or sets the font.</summary>
		public string Font { get; set; }

		internal Color ForeColor { get => _ForeColor; set => _ForeColor = value; }
		internal byte ForegroundOpacity { get => _ForeColorOpacity; set => _ForeColorOpacity = value; }
		internal Color BackColor { get => _BackColor; set => _BackColor = value; }
		internal byte BackgroundOpacity { get => _BackColorOpacity; set => _BackColorOpacity = value; }
		internal Color LineColor { get => _LineColor; set => _LineColor = value; }
		internal byte LineOpacity { get => _LineOpacity; set => _LineOpacity = value; }

		/// <summary>The category used in option pages to group style items</summary>
		internal abstract string Category { get; }

		/// <summary>Returns whether any option in this style is set.</summary>
		internal bool IsSet => ForeColor.A > 0 || BackColor.A > 0 || ForegroundOpacity != 0 || BackgroundOpacity != 0 || Bold.HasValue || Italic.HasValue || Underline.HasValue || OverLine.HasValue || Strikethrough.HasValue || Stretch.HasValue || FontSize > 0 || String.IsNullOrEmpty(Font) == false || LineColor.A > 0 || LineOpacity != 0 || LineThickness != 0 || LineStyle != LineStyle.Solid;

		internal abstract string ClassificationType { get; }
		internal abstract string Description { get; }

		internal StyleBase Clone() {
			return (StyleBase)MemberwiseClone();
		}
		internal void CopyTo(StyleBase target) {
			target.Bold = Bold;
			target.Italic = Italic;
			target.OverLine = OverLine;
			target.Underline = Underline;
			target.Strikethrough = Strikethrough;
			target.FontSize = FontSize;
			target.Stretch = Stretch;
			target.BackgroundEffect = BackgroundEffect;
			target.Font = Font;
			target.ForeColor = ForeColor;
			target.BackColor = BackColor;
			target.ForegroundOpacity = ForegroundOpacity;
			target.BackgroundOpacity = BackgroundOpacity;
			target.LineColor = LineColor;
			target.LineOpacity = LineOpacity;
			target.LineThickness = LineThickness;
			target.LineOffset = LineOffset;
			target.LineStyle = LineStyle;
		}
		internal void CopyTo(StyleBase target, StyleFilters filters) {
			if (filters.MatchFlags(StyleFilters.Color)) {
				target.ForeColor = ForeColor;
				target.BackColor = BackColor;
				target.ForegroundOpacity = ForegroundOpacity;
				target.BackgroundOpacity = BackgroundOpacity;
				target.LineColor = LineColor;
				target.LineOpacity = LineOpacity;
				target.BackgroundEffect = BackgroundEffect;
			}
			if (filters.MatchFlags(StyleFilters.FontFamily)) {
				target.Font = Font;
				target.Stretch = Stretch;
			}
			if (filters.MatchFlags(StyleFilters.FontSize)) {
				target.FontSize = FontSize;
			}
			if (filters.MatchFlags(StyleFilters.FontStyle)) {
				target.Bold = Bold;
				target.Italic = Italic;
				target.OverLine = OverLine;
				target.Underline = Underline;
				target.Strikethrough = Strikethrough;
			}
			if (filters.MatchFlags(StyleFilters.LineStyle)) {
				target.LineStyle = LineStyle;
				target.LineOffset = LineOffset;
				target.LineThickness = LineThickness;
			}
		}
		internal void Reset() {
			Bold = Italic = OverLine = Underline = Strikethrough = null;
			FontSize = 0;
			Stretch = null;
			BackgroundEffect = BrushEffect.Solid;
			Font = null;
			ForeColor = BackColor = LineColor = default;
			ForegroundOpacity = BackgroundOpacity = LineOpacity = LineThickness = LineOffset = 0;
			LineStyle = LineStyle.Solid;
		}
	}
	sealed class SyntaxStyle : StyleBase
	{
		internal override string Category { get; }
		public string Key { get; set; }
		internal override int Id { get; }
		internal override string ClassificationType => Key;
		internal override string Description { get; }

		public SyntaxStyle(string classificationType) {
			Key = classificationType;
			Category = "General";
		}
	}

	abstract class StyleBase<TStyle> : StyleBase where TStyle : Enum
	{
		string _ClassificationType, _Description;

		public abstract TStyle StyleID { get; set; }

		internal override string ClassificationType => _ClassificationType ?? (_ClassificationType = GetClassificationType());
		internal override string Description => _Description ?? (_Description = GetDescription());

		protected string GetCategory() {
			return typeof(TStyle).GetField(StyleID.ToString())
				?.GetCustomAttribute<CategoryAttribute>(false)
				?.Category ?? String.Empty;
		}

		string GetDescription() {
			return typeof(TStyle).GetField(StyleID.ToString())
				?.GetCustomAttributes<DescriptionAttribute>(false)
				?.FirstOrDefault()
				?.Description;
		}
		string GetClassificationType() {
			return typeof(TStyle).GetField(StyleID.ToString())
				?.GetCustomAttributes<ClassificationTypeAttribute>(false)
				?.FirstOrDefault()
				?.ClassificationTypeNames;
		}
	}
}
