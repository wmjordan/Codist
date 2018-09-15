using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace Codist.SyntaxHighlight
{
	/// <summary>The base style for syntax highlight elements.</summary>
	abstract class StyleBase
	{
		static protected readonly Regex FriendlyNamePattern = new Regex(@"([a-z])([A-Z0-9])", RegexOptions.Singleline);

		public abstract int Id { get; }
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
		/// <summary>Gets or sets the foreground color to render the text. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		[DefaultValue("#00000000")]
		public string ForegroundColor {
			get => ForeColor.ToHexString();
			set => ForeColor = UIHelper.ParseColor(value);
		}
		/// <summary>Gets or sets the foreground color to render the text. The color format could be #RRGGBBAA or #RRGGBB.</summary>
		[DefaultValue("#00000000")]
		public string BackgroundColor {
			get => BackColor.ToHexString();
			set => BackColor = UIHelper.ParseColor(value);
		}
		/// <summary>Gets or sets the brush effect to draw the background color.</summary>
		[DefaultValue(BrushEffect.Solid)]
		public BrushEffect BackgroundEffect { get; set; }
		/// <summary>Gets or sets the style of marker on the scrollbar.</summary>
		[DefaultValue(ScrollbarMarkerStyle.None)]
		public ScrollbarMarkerStyle ScrollBarMarkerStyle { get; set; }
		/// <summary>Gets or sets the font.</summary>
		public string Font { get; set; }

		internal Color ForeColor { get; set; }
		internal Color BackColor { get; set; }

		/// <summary>The category used in option pages to group style items</summary>
		[Newtonsoft.Json.JsonIgnore]
		public abstract string Category { get; }

		/// <summary>Returns whether any option in this style is set.</summary>
		[Newtonsoft.Json.JsonIgnore]
		public bool IsSet => Bold.HasValue || Italic.HasValue || Underline.HasValue || OverLine.HasValue || Strikethrough.HasValue || String.IsNullOrEmpty(Font) == false || ForeColor.A > 0 || BackColor.A > 0;

		internal StyleBase Clone() {
			return (StyleBase)MemberwiseClone();
		}
		internal void CopyTo(StyleBase style) {
			style.Bold = Bold;
			style.Italic = Italic;
			style.OverLine = OverLine;
			style.Underline = Underline;
			style.Strikethrough = Strikethrough;
			style.FontSize = FontSize;
			style.BackgroundEffect = BackgroundEffect;
			style.Font = Font;
			style.ForeColor = ForeColor;
			style.BackColor = BackColor;
		}
		internal void Reset() {
			Bold = Italic = OverLine = Underline = Strikethrough = null;
			FontSize = 0;
			BackgroundEffect = BrushEffect.Solid;
			Font = null;
			ForeColor = BackColor = default;
		}
	}
	abstract class StyleBase<TStyle> : StyleBase where TStyle : struct
	{
		public abstract TStyle StyleID { get; set; }
	}

	[DebuggerDisplay("{StyleID} {ForegroundColor} {FontSize}")]
	sealed class CodeStyle : StyleBase<CodeStyleTypes>
	{
		string _Category;

		public override int Id => (int)StyleID;

		/// <summary>Gets or sets the code style.</summary>
		public override CodeStyleTypes StyleID { get; set; }

		public override string Category {
			get {
				if (_Category != null) {
					return _Category;
				}
				var f = typeof(CodeStyleTypes).GetField(StyleID.ToString());
				if (f == null) {
					return _Category = String.Empty;
				}
				var c = f.GetCustomAttribute<CategoryAttribute>(false);
				if (c == null) {
					return _Category = String.Empty;
				}
				return _Category = c.Category;
			}
		}
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

		public override int Id => (int)StyleID;

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
	sealed class CSharpStyle : StyleBase<CSharpStyleTypes>
	{
		string _Category;

		public override int Id => (int)StyleID;

		/// <summary>Gets or sets the code style.</summary>
		public override CSharpStyleTypes StyleID { get; set; }

		public override string Category {
			get {
				if (_Category != null) {
					return _Category;
				}
				var f = typeof(CSharpStyleTypes).GetField(StyleID.ToString());
				if (f == null) {
					return _Category = String.Empty;
				}
				var c = f.GetCustomAttribute<CategoryAttribute>(false);
				if (c == null) {
					return _Category = String.Empty;
				}
				return _Category = c.Category;
			}
		}
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

		public override int Id => (int)StyleID;

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
		public SymbolMarkerStyle() {
		}
		public SymbolMarkerStyle(SymbolMarkerStyleTypes styleID, Color foregroundColor) {
			StyleID = styleID;
			ForegroundColor = foregroundColor.ToHexString();
		}

		public override int Id => (int)StyleID;

		/// <summary>Gets or sets the comment style.</summary>
		public override SymbolMarkerStyleTypes StyleID { get; set; }

		public override string Category => StyleID != SymbolMarkerStyleTypes.None ? Constants.SyntaxCategory.Highlight : String.Empty;

		internal new CommentStyle Clone() {
			return (CommentStyle)MemberwiseClone();
		}

		public override string ToString() {
			return FriendlyNamePattern.Replace(StyleID.ToString(), "$1 $2");
		}
	}

}
