using System.Windows.Media;

namespace Codist.SyntaxHighlight
{
	sealed class CommentStyle : StyleBase<CommentStyleTypes>
	{
		string _Category;
		public CommentStyle() {
		}
		public CommentStyle(CommentStyleTypes styleID) {
			StyleID = styleID;
		}
		public CommentStyle(CommentStyleTypes styleID, Color foregroundColor) {
			StyleID = styleID;
			ForeColor = foregroundColor;
		}

		internal override int Id => (int)StyleID;

		/// <summary>Gets or sets the comment style.</summary>
		public override CommentStyleTypes StyleID { get; set; }

		internal override string Category => _Category ?? (_Category = GetCategory());

		internal new CommentStyle Clone() {
			return (CommentStyle)MemberwiseClone();
		}

		public override string ToString() {
			return FriendlyNamePattern.Replace(StyleID.ToString(), "$1 $2");
		}
	}
}
