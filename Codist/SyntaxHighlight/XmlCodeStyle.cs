using System.Windows.Media;

namespace Codist.SyntaxHighlight
{
	sealed class XmlCodeStyle : StyleBase<XmlStyleTypes>
	{
		string _Category;
		public XmlCodeStyle() {
		}
		public XmlCodeStyle(XmlStyleTypes styleID, Color foregroundColor) {
			StyleID = styleID;
			ForeColor = foregroundColor;
		}

		internal override int Id => (int)StyleID;

		/// <summary>Gets or sets the comment style.</summary>
		public override XmlStyleTypes StyleID { get; set; }

		internal override string Category => _Category ?? (_Category = GetCategory());

		internal new CommentStyle Clone() {
			return (CommentStyle)MemberwiseClone();
		}

		public override string ToString() {
			return FriendlyNamePattern.Replace(StyleID.ToString(), "$1 $2");
		}
	}
}
