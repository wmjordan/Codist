namespace Codist.SyntaxHighlight
{
	sealed class MarkdownStyle : StyleBase<MarkdownStyleTypes>
	{
		string _Category;

		internal override int Id => (int)StyleID;

		/// <summary>Gets or sets the code style.</summary>
		public override MarkdownStyleTypes StyleID { get; set; }

		internal override string Category => _Category ?? (_Category = GetCategory());

		internal new MarkdownStyle Clone() {
			return (MarkdownStyle)MemberwiseClone();
		}

		public override string ToString() {
			return FriendlyNamePattern.Replace(StyleID.ToString(), "$1 $2");
		}
	}
}
