using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Codist
{
	/// <summary>
	/// This is the class that implements the package exposed by this assembly.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The minimum requirement for a class to be considered a valid package for Visual Studio
	/// is to implement the IVsPackage interface and register itself with the shell.
	/// This package uses the helper classes defined inside the Managed Package Framework (MPF)
	/// to do it: it derives from the Package class that provides the implementation of the
	/// IVsPackage interface and uses the registration attributes defined in the framework to
	/// register itself and its components with the shell. These attributes tell the pkgdef creation
	/// utility what data to put into .pkgdef file.
	/// </para>
	/// <para>
	/// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
	/// </para>
	/// </remarks>
	[PackageRegistration(UseManagedResourcesOnly = true)]
	[InstalledProductRegistration("#110", "#112", "2.5", IconResourceID = 400)] // Info on this package for Help/About
	[Guid(PackageGuidString)]
	[ProvideOptionPage(typeof(Options.Misc), Constants.NameOfMe, "General", 0, 0, true)]
	[ProvideOptionPage(typeof(Options.CSharp), Constants.NameOfMe, "C#", 0, 0, true, Sort = 10)]
	[ProvideOptionPage(typeof(Options.CodeStyle), Constants.NameOfMe , "Syntax highlight (C# & C++)", 0, 0, true, Sort = 110)]
	[ProvideOptionPage(typeof(Options.XmlCodeStyle), Constants.NameOfMe, "Syntax highlight (XML)", 0, 0, true, Sort = 120)]
	[ProvideOptionPage(typeof(Options.CommentStyle), Constants.NameOfMe, "Syntax highlight (Comment)", 0, 0, true, Sort = 130)]
	[ProvideOptionPage(typeof(Options.CommentTagger), Constants.NameOfMe + "\\Syntax highlight (Comment)", "Tagger (Comment)", 0, 0, true, Sort = 131)]
	sealed class CodistPackage : Package
	{
		/// <summary>
		/// CodistPackage GUID string.
		/// </summary>
		public const string PackageGuidString = "c7b93d20-621f-4b21-9d28-d51157ef0b94";

		/// <summary>
		/// Initializes a new instance of the <see cref="CodistPackage"/> class.
		/// </summary>
		public CodistPackage() {
			// Inside this method you can place any initialization code that does not require
			// any Visual Studio service because at this point the package object is created but
			// not sited yet inside Visual Studio environment. The place to do all the other
			// initialization is the Initialize method.
		}

		#region Package Members

		/// <summary>
		/// Initialization of the package; this method is called right after the package is sited, so this is the place
		/// where you can put all the initialization code that rely on services provided by VisualStudio.
		/// </summary>
		protected override void Initialize() {
            base.Initialize();
		}

		#endregion

	}
}
