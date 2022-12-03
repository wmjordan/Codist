using System;
using System.Diagnostics;
using System.Windows.Media;

namespace Codist.SyntaxHighlight
{
	[DebuggerDisplay("{StyleID} {ForegroundColor} {FontSize}")]
	sealed class XmlCodeStyle : StyleBase<XmlStyleTypes>
	{
		public XmlCodeStyle() {
		}
		public XmlCodeStyle(XmlStyleTypes styleID, Color foregroundColor) {
			StyleID = styleID;
			ForeColor = foregroundColor;
		}

		internal override int Id => (int)StyleID;

		/// <summary>Gets or sets the comment style.</summary>
		public override XmlStyleTypes StyleID { get; set; }

		internal override string Category => StyleID != XmlStyleTypes.None ? Constants.SyntaxCategory.Xml : String.Empty;

		internal new CommentStyle Clone() {
			return (CommentStyle)MemberwiseClone();
		}

		public override string ToString() {
			return FriendlyNamePattern.Replace(StyleID.ToString(), "$1 $2");
		}
	}

}
