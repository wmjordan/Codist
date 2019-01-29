using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Media;
using AppHelpers;
using Microsoft.VisualStudio.Text.Classification;

namespace Codist.SyntaxHighlight
{
	/// <summary>The base style for syntax highlight elements.</summary>
	abstract class StyleBase
	{
		static protected readonly Regex FriendlyNamePattern = new Regex(@"([a-z])([A-Z0-9])", RegexOptions.Singleline);
		Color _ForeColor, _BackColor;
		byte _ForeColorOpacity, _BackColorOpacity;

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
		[DefaultValue("#00000000")]
		public string ForegroundColor {
			get => _ForeColor.A == 0 && _ForeColorOpacity == 0 ? "#00000000"
				: _ForeColor.A > 0 && _ForeColorOpacity == 0 ? _ForeColor.ToHexString()
				: _ForeColor.A == 0 && _ForeColorOpacity > 0 ? "#" + _ForeColorOpacity.ToString("X2")
				: _ForeColor.Alpha(_ForeColorOpacity).ToHexString();
			set => UIHelper.ParseColor(value, out _ForeColor, out _ForeColorOpacity);
		}
		/// <summary>Gets or sets the foreground color to render the text. The color format could be #AARRGGBB or #RRGGBB.</summary>
		[DefaultValue("#00000000")]
		public string BackgroundColor {
			get => _BackColor.A == 0 && _BackColorOpacity == 0 ? "#00000000"
				: _BackColor.A > 0 && _BackColorOpacity == 0 ? _BackColor.ToHexString()
				: _BackColor.A == 0 && _BackColorOpacity > 0 ? "#" + _BackColorOpacity.ToString("X2")
				: _BackColor.Alpha(_BackColorOpacity).ToHexString();
			set => UIHelper.ParseColor(value, out _BackColor, out _BackColorOpacity);
		}
		/// <summary>Gets or sets the brush effect to draw the background color.</summary>
		[DefaultValue(BrushEffect.Solid)]
		public BrushEffect BackgroundEffect { get; set; }
		/// <summary>Gets or sets the style of marker on the scrollbar.</summary>
		[DefaultValue(ScrollbarMarkerStyle.None)]
		public ScrollbarMarkerStyle ScrollBarMarkerStyle { get; set; }
		/// <summary>Gets or sets the font.</summary>
		public string Font { get; set; }

		internal Color ForeColor { get => _ForeColor; set => _ForeColor = value; }
		internal byte ForegroundOpacity { get => _ForeColorOpacity; set => _ForeColorOpacity = value; }
		internal Color AlphaForeColor => _ForeColorOpacity > 0 ? _ForeColor.Alpha(_ForeColorOpacity) : _ForeColor;
		internal Color BackColor { get => _BackColor; set => _BackColor = value; }
		internal byte BackgroundOpacity { get => _BackColorOpacity; set => _BackColorOpacity = value; }
		internal Color AlphaBackColor => _BackColorOpacity > 0 ? _BackColor.Alpha(_BackColorOpacity) : _BackColor;

		/// <summary>The category used in option pages to group style items</summary>
		[Newtonsoft.Json.JsonIgnore]
		public abstract string Category { get; }

		/// <summary>Returns whether any option in this style is set.</summary>
		[Newtonsoft.Json.JsonIgnore]
		public bool IsSet => ForeColor.A > 0 || BackColor.A > 0 || ForegroundOpacity != 0 || BackgroundOpacity != 0 || Bold.HasValue || Italic.HasValue || Underline.HasValue || OverLine.HasValue || Strikethrough.HasValue || FontSize > 0 || String.IsNullOrEmpty(Font) == false;

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
			target.BackgroundEffect = BackgroundEffect;
			target.Font = Font;
			target.ForeColor = ForeColor;
			target.BackColor = BackColor;
			target.ForegroundOpacity = ForegroundOpacity;
			target.BackgroundOpacity = BackgroundOpacity;
		}
		internal void CopyTo(StyleBase target, StyleFilters filters) {
			if (filters.MatchFlags(StyleFilters.Color)) {
				target.ForeColor = ForeColor;
				target.BackColor = BackColor;
				target.ForegroundOpacity = ForegroundOpacity;
				target.BackgroundOpacity = BackgroundOpacity;
				target.BackgroundEffect = BackgroundEffect;
			}
			if (filters.MatchFlags(StyleFilters.FontFamily)) {
				target.Font = Font;
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
		}
		internal void Reset() {
			Bold = Italic = OverLine = Underline = Strikethrough = null;
			FontSize = 0;
			BackgroundEffect = BrushEffect.Solid;
			Font = null;
			ForeColor = BackColor = default;
			ForegroundOpacity = BackgroundOpacity = 0;
		}
	}
	abstract class StyleBase<TStyle> : StyleBase where TStyle : Enum
	{
		string _ClassficationType, _Description;

		public abstract TStyle StyleID { get; set; }

		internal override string ClassificationType => _ClassficationType ?? (_ClassficationType = GetClassificationType());
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

	[DebuggerDisplay("{StyleID} {ForegroundColor} {FontSize}")]
	sealed class CodeStyle : StyleBase<CodeStyleTypes>
	{
		string _Category;

		internal override int Id => (int)StyleID;

		/// <summary>Gets or sets the code style.</summary>
		public override CodeStyleTypes StyleID { get; set; }

		public override string Category => _Category ?? (_Category = GetCategory());

		internal new CodeStyle Clone() {
			return (CodeStyle)MemberwiseClone();
		}

		public override string ToString() {
			return FriendlyNamePattern.Replace(StyleID.ToString(), "$1 $2");
		}
	}

	[DebuggerDisplay("{StyleID} {ForegroundColor} {FontSize}")]
	sealed class CommentStyle : StyleBase<CommentStyleTypes>
	{
		public CommentStyle() {
		}
		public CommentStyle(CommentStyleTypes styleID) {
			StyleID = styleID;
		}
		public CommentStyle(CommentStyleTypes styleID, Color foregroundColor) {
			StyleID = styleID;
			ForegroundColor = foregroundColor.ToHexString();
		}

		internal override int Id => (int)StyleID;

		/// <summary>Gets or sets the comment style.</summary>
		public override CommentStyleTypes StyleID { get; set; }

		public override string Category => Constants.SyntaxCategory.Comment;

		internal new CommentStyle Clone() {
			return (CommentStyle)MemberwiseClone();
		}

		public override string ToString() {
			return FriendlyNamePattern.Replace(StyleID.ToString(), "$1 $2");
		}
	}

	[DebuggerDisplay("{StyleID} {ForegroundColor} {FontSize}")]
	sealed class CppStyle : StyleBase<CppStyleTypes>
	{
		public CppStyle() {
		}
		public CppStyle(CppStyleTypes styleID) {
			StyleID = styleID;
		}
		public CppStyle(CppStyleTypes styleID, Color foregroundColor) {
			StyleID = styleID;
			ForegroundColor = foregroundColor.ToHexString();
		}

		internal override int Id => (int)StyleID;

		/// <summary>Gets or sets the C++ style.</summary>
		public override CppStyleTypes StyleID { get; set; }

		public override string Category => StyleID != CppStyleTypes.None ? Constants.SyntaxCategory.General : String.Empty;

		internal new CppStyle Clone() {
			return (CppStyle)MemberwiseClone();
		}

		public override string ToString() {
			return FriendlyNamePattern.Replace(StyleID.ToString(), "$1 $2");
		}
	}

	[DebuggerDisplay("{StyleID} {ForegroundColor} {FontSize}")]
	sealed class CSharpStyle : StyleBase<CSharpStyleTypes>
	{
		string _Category;

		internal override int Id => (int)StyleID;

		/// <summary>Gets or sets the code style.</summary>
		public override CSharpStyleTypes StyleID { get; set; }

		public override string Category => _Category ?? (_Category = GetCategory());

		internal new CSharpStyle Clone() {
			return (CSharpStyle)MemberwiseClone();
		}

		public override string ToString() {
			return FriendlyNamePattern.Replace(StyleID.ToString(), "$1 $2");
		}
	}

	[DebuggerDisplay("{StyleID} {ForegroundColor} {FontSize}")]
	sealed class XmlCodeStyle : StyleBase<XmlStyleTypes>
	{
		public XmlCodeStyle() {
		}
		public XmlCodeStyle(XmlStyleTypes styleID, Color foregroundColor) {
			StyleID = styleID;
			ForegroundColor = foregroundColor.ToHexString();
		}

		internal override int Id => (int)StyleID;

		/// <summary>Gets or sets the comment style.</summary>
		public override XmlStyleTypes StyleID { get; set; }

		public override string Category => StyleID != XmlStyleTypes.None ? Constants.SyntaxCategory.Xml : String.Empty;

		internal new CommentStyle Clone() {
			return (CommentStyle)MemberwiseClone();
		}

		public override string ToString() {
			return FriendlyNamePattern.Replace(StyleID.ToString(), "$1 $2");
		}
	}
	[DebuggerDisplay("{StyleID} {ForegroundColor} {FontSize}")]
	sealed class SymbolMarkerStyle : StyleBase<SymbolMarkerStyleTypes>
	{
		string _Category;

		public SymbolMarkerStyle() {
		}
		public SymbolMarkerStyle(SymbolMarkerStyleTypes styleID, Color foregroundColor) {
			StyleID = styleID;
			ForegroundColor = foregroundColor.ToHexString();
		}

		internal override int Id => (int)StyleID;

		/// <summary>Gets or sets the comment style.</summary>
		public override SymbolMarkerStyleTypes StyleID { get; set; }

		public override string Category => _Category ?? (_Category = GetCategory());

		internal new CommentStyle Clone() {
			return (CommentStyle)MemberwiseClone();
		}

		public override string ToString() {
			return FriendlyNamePattern.Replace(StyleID.ToString(), "$1 $2");
		}
	}

}
