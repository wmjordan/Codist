using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using AppHelpers;
using Codist.Controls;
using Microsoft.VisualStudio.Text.Classification;
using Newtonsoft.Json;

namespace Codist.SyntaxHighlight
{
	/// <summary>The base style for syntax highlight elements.</summary>
	[DebuggerDisplay("{ClassificationType}: {ForeColor} {FontSize}")]
	abstract class StyleBase
	{
		static protected readonly Regex FriendlyNamePattern = new Regex("([a-z])([A-Z0-9])", RegexOptions.Singleline);
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
		/// <summary>Gets or sets the font variant.</summary>
		public string FontVariant { get; set; }

		internal Color ForeColor { get => _ForeColor; set => _ForeColor = value; }
		internal byte ForegroundOpacity { get => _ForeColorOpacity; set => _ForeColorOpacity = value; }
		internal Color BackColor { get => _BackColor; set => _BackColor = value; }
		internal byte BackgroundOpacity { get => _BackColorOpacity; set => _BackColorOpacity = value; }
		internal Color LineColor { get => _LineColor; set => _LineColor = value; }
		internal byte LineOpacity { get => _LineOpacity; set => _LineOpacity = value; }
		internal bool HasLine => Underline == true || Strikethrough == true || OverLine == true;
		internal bool HasLineColor => HasLine && _LineColor.A != 0;

		/// <summary>The category used in option pages to group style items</summary>
		internal abstract string Category { get; }

		/// <summary>Returns whether any option in this style is set.</summary>
		internal bool IsSet => ForeColor.A != 0
			|| BackColor.A != 0
			|| Bold.HasValue
			|| Italic.HasValue
			|| Underline.HasValue
			|| FontSize != 0
			|| ForegroundOpacity != 0
			|| BackgroundOpacity != 0
			|| OverLine.HasValue
			|| Strikethrough.HasValue
			|| String.IsNullOrEmpty(Font) == false;

		internal abstract string ClassificationType { get; }
		internal abstract string Description { get; }

		internal SolidColorBrush MakeBrush() {
			return ForeColor.A != 0 ? new SolidColorBrush(ForeColor) : null;
		}

		internal Brush MakeBackgroundBrush(Color backColor) {
			backColor = backColor.Alpha(0);
			switch (BackgroundEffect) {
				case BrushEffect.ToBottom:
					return new LinearGradientBrush(backColor, BackColor, 90);
				case BrushEffect.ToTop:
					return new LinearGradientBrush(BackColor, backColor, 90);
				case BrushEffect.ToRight:
					return new LinearGradientBrush(backColor, BackColor, 0);
				case BrushEffect.ToLeft:
					return new LinearGradientBrush(BackColor, backColor, 0);
				default:
					return BackColor.A != 0 ? new SolidColorBrush(BackColor) : null;
			}
		}

		internal Typeface MakeTypeface() {
			var f = new FontFamily(Font);
			bool fontValid = false;
			foreach (var item in f.FamilyNames) {
				if (item.Value?.Contains(Font) == true) {
					fontValid = true;
					break;
				}
			}
			if (fontValid) {
				if (String.IsNullOrWhiteSpace(FontVariant)) {
					return new Typeface(f, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
				}
				var ft = f.GetTypefaces().FirstOrDefault(t => t.FaceNames.Values.Contains(FontVariant));
				if (ft != null) {
					return new Typeface(new FontFamily($"{Font} {FontVariant}"), ft.Style, ft.Weight, ft.Stretch);
				}
			}
			return null;
		}

		internal TextDecorationCollection MakeTextDecorations() {
			var tdc = new TextDecorationCollection();
			var hasSet = false;
			if (Underline == true) {
				if (LineColor.A > 0) {
					tdc.Add(MakeLineDecoration(TextDecorationLocation.Underline));
				}
				else {
					tdc.Add(TextDecorations.Underline);
				}
			}
			else if (Underline == false) {
				hasSet = true;
			}
			if (Strikethrough == true) {
				if (LineColor.A > 0) {
					tdc.Add(MakeLineDecoration(TextDecorationLocation.Strikethrough));
				}
				else {
					tdc.Add(TextDecorations.Strikethrough);
				}
			}
			else if (Strikethrough == false) {
				hasSet = true;
			}
			if (OverLine == true) {
				if (LineColor.A > 0) {
					tdc.Add(MakeLineDecoration(TextDecorationLocation.OverLine));
				}
				else {
					tdc.Add(TextDecorations.OverLine);
				}
			}
			else if (OverLine == false) {
				hasSet = true;
			}
			return tdc.Count > 0 || hasSet ? tdc : null;
		}

		TextDecoration MakeLineDecoration(TextDecorationLocation location) {
			var d = new TextDecoration {
				Location = location,
				Pen = new Pen {
					Brush = new SolidColorBrush(LineOpacity == 0 ? LineColor : LineColor.Alpha(LineOpacity))
				},
			};
			if (LineStyle != LineStyle.Solid) {
				switch (LineStyle) {
					case LineStyle.Dot:
						d.Pen.DashStyle = new DashStyle(new double[] { 2, 2 }, 0);
						break;
					case LineStyle.Dash:
						d.Pen.DashStyle = new DashStyle(new double[] { 4, 4 }, 0);
						break;
					case LineStyle.DashDot:
						d.Pen.DashStyle = new DashStyle(new double[] { 4, 4, 2, 4 }, 0);
						break;
					case LineStyle.Squiggle:
						if (location == TextDecorationLocation.Underline) {
							d.Pen.Brush = SquiggleBrushCache.GetOrCreate(d.Pen.Brush);
							d.PenOffset = 3;
							d.Pen.Thickness = 3.0;
							d.PenThicknessUnit = TextDecorationUnit.Pixel;
							return d;
						}
						break;
				}
			}
			if (LineOffset != 0 && location != TextDecorationLocation.Strikethrough) {
				d.PenOffset = LineOffset;
				d.PenOffsetUnit = TextDecorationUnit.Pixel;
			}
			if (LineThickness > 0) {
				d.Pen.Thickness = LineThickness + 1;
				d.PenThicknessUnit = TextDecorationUnit.Pixel;
			}
			return d;
		}

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
			target.FontVariant = FontVariant;
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
				target.FontVariant = FontVariant;
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
			Font = null;
			FontSize = 0;
			FontVariant = null;
			ForeColor = BackColor = LineColor = default;
			ForegroundOpacity = BackgroundOpacity = LineOpacity = LineThickness = LineOffset = 0;
			LineStyle = LineStyle.Solid;
			BackgroundEffect = BrushEffect.Solid;
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
