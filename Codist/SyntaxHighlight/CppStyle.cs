using System.Windows.Media;

namespace Codist.SyntaxHighlight
{
	sealed class CppStyle : StyleBase<CppStyleTypes>
	{
		string _Category;
		public CppStyle() {
		}
		public CppStyle(CppStyleTypes styleID) {
			StyleID = styleID;
		}
		public CppStyle(CppStyleTypes styleID, Color foregroundColor) {
			StyleID = styleID;
			ForeColor = foregroundColor;
		}

		internal override int Id => (int)StyleID;

		/// <summary>Gets or sets the C++ style.</summary>
		public override CppStyleTypes StyleID { get; set; }

		internal override string Category => _Category ?? (_Category = GetCategory());

		internal new CppStyle Clone() {
			return (CppStyle)MemberwiseClone();
		}

		public override string ToString() {
			return FriendlyNamePattern.Replace(StyleID.ToString(), "$1 $2");
		}
	}
}
