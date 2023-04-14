using System.Windows.Media;

namespace Codist.SyntaxHighlight
{
	sealed class SymbolMarkerStyle : StyleBase<SymbolMarkerStyleTypes>
	{
		string _Category;

		public SymbolMarkerStyle() {
		}
		public SymbolMarkerStyle(SymbolMarkerStyleTypes styleID, Color foregroundColor) {
			StyleID = styleID;
			ForeColor = foregroundColor;
		}

		internal override int Id => (int)StyleID;

		/// <summary>Gets or sets the comment style.</summary>
		public override SymbolMarkerStyleTypes StyleID { get; set; }

		internal override string Category => _Category ?? (_Category = GetCategory());

		internal new CommentStyle Clone() {
			return (CommentStyle)MemberwiseClone();
		}

		public override string ToString() {
			return FriendlyNamePattern.Replace(StyleID.ToString(), "$1 $2");
		}
	}
}
