namespace Codist.SyntaxHighlight
{
	sealed class CSharpStyle : StyleBase<CSharpStyleTypes>
	{
		string _Category;

		internal override int Id => (int)StyleID;

		/// <summary>Gets or sets the code style.</summary>
		public override CSharpStyleTypes StyleID { get; set; }

		internal override string Category => _Category ?? (_Category = GetCategory());

		internal new CSharpStyle Clone() {
			return (CSharpStyle)MemberwiseClone();
		}

		public override string ToString() {
			return FriendlyNamePattern.Replace(StyleID.ToString(), "$1 $2");
		}
	}
}
