using System;

namespace TestProject
{
	/// <summary>
	/// This attribute denotes the .NET version which introduced features within annotated type or member. It does nothing except for reference.
	/// </summary>
	[AttributeUsage(AttributeTargets.All)]
	internal sealed class ApiVersionAttribute : Attribute
	{
		public Version Version { get; }
		public ApiVersionAttribute(int major) {
			Version = new Version(major, 0);
		}
		public ApiVersionAttribute(int major, int minor) {
			Version = new Version(major, minor);
		}
	}
}
