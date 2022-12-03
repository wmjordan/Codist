namespace Codist
{
	/// <summary>
	/// Denotes where an assembly is imported.
	/// </summary>
	public enum AssemblySource
	{
		/// <summary>
		/// The assembly is an external one.
		/// </summary>
		Metadata,
		/// <summary>
		/// The assembly comes from source code.
		/// </summary>
		SourceCode,
		/// <summary>
		/// The assembly comes from other projects.
		/// </summary>
		Retarget
	}
}
