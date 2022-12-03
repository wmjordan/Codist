namespace Codist.Controls
{
	enum SymbolListType
	{
		None,
		/// <summary>
		/// Previews KnownImageIds
		/// </summary>
		VsKnownImage,
		/// <summary>
		/// Previews predefined colors
		/// </summary>
		PredefinedColors,
		/// <summary>
		/// Enables drag and drop
		/// </summary>
		NodeList,
		/// <summary>
		/// Filter by type kinds
		/// </summary>
		TypeList,
		/// <summary>
		/// Lists source code locations
		/// </summary>
		Locations,
		/// <summary>
		/// List of symbol referrers
		/// </summary>
		SymbolReferrers,
		/// <summary>
		/// List of enum flags
		/// </summary>
		EnumFlags,
	}
}
