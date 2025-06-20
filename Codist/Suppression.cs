namespace Codist
{
	/// <summary>
	/// Defines checkId and justifications for <see cref="System.Diagnostics.CodeAnalysis.SuppressMessageAttribute.SuppressMessageAttribute"/>.
	/// </summary>
	public static class Suppression
	{
		public const string VSTHRD010 = "VSTHRD010:Invoke single-threaded types on Main thread";
		public const string CheckedInCaller = "Checked in caller";
		public const string VSTHRD100 = "VSTHRD100:Avoid async void methods";
		public const string EventHandler = "Event handler";
		public const string ExceptionHandled = "Exception handled";
	}
}
