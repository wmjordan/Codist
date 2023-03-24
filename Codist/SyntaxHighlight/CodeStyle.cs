namespace Codist.SyntaxHighlight
{
	sealed class CodeStyle : StyleBase<CodeStyleTypes>
	{
		string _Category;

		internal override int Id => (int)StyleID;

		/// <summary>Gets or sets the code style.</summary>
		public override CodeStyleTypes StyleID { get; set; }

		internal override string Category => _Category ?? (_Category = GetCategory());

		internal new CodeStyle Clone() {
			return (CodeStyle)MemberwiseClone();
		}

		public override string ToString() {
			return FriendlyNamePattern.Replace(StyleID.ToString(), "$1 $2");
		}
	}

}
