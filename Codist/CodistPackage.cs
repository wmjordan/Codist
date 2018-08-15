using System;
using System.Drawing;
using System.Runtime.InteropServices;
using EnvDTE80;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codist
{
	/// <summary>This is the class that implements the package exposed by this assembly.</summary>
	/// <remarks>
	/// <para>The minimum requirement for a class to be considered a valid package for Visual Studio is to implement the IVsPackage interface and register itself with the shell. This package uses the helper classes defined inside the Managed Package Framework (MPF) to do it: it derives from the Package class that provides the implementation of the IVsPackage interface and uses the registration attributes defined in the framework to register itself and its components with the shell. These attributes tell the pkgdef creation utility what data to put into .pkgdef file.</para>
	/// <para>To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.</para>
	/// </remarks>
	[PackageRegistration(UseManagedResourcesOnly = true)]
	[InstalledProductRegistration("#110", "#112", "3.5", IconResourceID = 400)] // Information on this package for Help/About
	[Guid(PackageGuidString)]
	[ProvideOptionPage(typeof(Options.General), Constants.NameOfMe, "General", 0, 0, true)]
	[ProvideOptionPage(typeof(Options.SuperQuickInfo), CategorySuperQuickInfo, "General", 0, 0, true, Sort = 10)]
	[ProvideOptionPage(typeof(Options.CSharpSuperQuickInfo), CategorySuperQuickInfo, "C#", 0, 0, true, Sort = 20)]

	[ProvideOptionPage(typeof(Options.ScrollbarMarker), CategoryScrollbarMarker, "General", 0, 0, true, Sort = 50)]
	[ProvideOptionPage(typeof(Options.CSharpScrollbarMarker), CategoryScrollbarMarker, "C#", 0, 0, true, Sort = 50)]

	[ProvideOptionPage(typeof(Options.SyntaxHighlight), CategorySyntaxHighlight, "General", 0, 0, true, Sort = 100)]
	[ProvideOptionPage(typeof(Options.CSharpStyle), CategorySyntaxHighlight, "C#", 0, 0, true, Sort = 10)]
	[ProvideOptionPage(typeof(Options.XmlStyle), CategorySyntaxHighlight, "XML", 0, 0, true, Sort = 30)]
	[ProvideOptionPage(typeof(Options.CommentStyle), CategorySyntaxHighlight, "Comment", 0, 0, true, Sort = 60)]
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[ProvideAutoLoad(UIContextGuids.SolutionExists)]
	sealed class CodistPackage : Package {
		const string CategorySuperQuickInfo = Constants.NameOfMe + "\\Super Quick Info";
		const string CategoryScrollbarMarker = Constants.NameOfMe + "\\Scrollbar Marker";
		const string CategorySyntaxHighlight = Constants.NameOfMe + "\\Syntax Highlight";
		/// <summary>
		/// CodistPackage GUID string.
		/// </summary>
		public const string PackageGuidString = "c7b93d20-621f-4b21-9d28-d51157ef0b94";
		static readonly int _IconSize = 16;
		static EnvDTE.DTE _dte;
		static ImageAttributes _ImageAttributes;
		static IVsImageService2 _ImageService;

		//static VsDebugger _Debugger;

		/// <summary>
		/// Initializes a new instance of the <see cref="CodistPackage"/> class.
		/// </summary>
		public CodistPackage() {
			// Inside this method you can place any initialization code that does not require
			// any Visual Studio service because at this point the package object is created but
			// not sited yet inside Visual Studio environment. The place to do all the other
			// initialization is the Initialize method.
		}

		public static EnvDTE.DTE DTE => _dte ?? (_dte = ServiceProvider.GlobalProvider.GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE);
		public static Color ToolWindowBackgroundColor { get; private set; }
		public static Color TitleBackgroundColor { get; private set; }
		public static Color TooltipTextColor { get; private set; }
		public static Color TooltipBackgroundColor { get; private set; }

		public static DebuggerStatus DebuggerStatus {
			get {
				switch (DTE.Debugger.CurrentMode) {
					case EnvDTE.dbgDebugMode.dbgBreakMode: return DebuggerStatus.Break;
					case EnvDTE.dbgDebugMode.dbgDesignMode: return DebuggerStatus.Design;
					case EnvDTE.dbgDebugMode.dbgRunMode: return DebuggerStatus.Running;
				}
				return DebuggerStatus.Design;
			}
		}

		public static System.Windows.Media.Imaging.BitmapSource GetImage(int monikerId) {
			var moniker = new ImageMoniker() { Guid = Microsoft.VisualStudio.Imaging.KnownImageIds.ImageCatalogGuid, Id = monikerId };
			Object data;
			(_ImageService ?? (_ImageService = ServiceProvider.GlobalProvider.GetService(typeof(SVsImageService)) as IVsImageService2)).GetImage(moniker, _ImageAttributes).get_Data(out data);
			return data as System.Windows.Media.Imaging.BitmapSource;
		}


		#region Package Members

		/// <summary>
		/// Initialization of the package; this method is called right after the package is sited, so this is the place
		/// where you can put all the initialization code that rely on services provided by VisualStudio.
		/// </summary>
		protected override void Initialize() {
            base.Initialize();
			Commands.ScreenshotCommand.Initialize(this);
			LoadThemeColors();
			VSColorTheme.ThemeChanged += (args) => {
				LoadThemeColors();
			};
		}

		static void LoadThemeColors() {
			ToolWindowBackgroundColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
			TitleBackgroundColor = VSColorTheme.GetThemedColor(EnvironmentColors.MainWindowActiveCaptionColorKey);
			TooltipTextColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolTipTextColorKey);
			TooltipBackgroundColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolTipColorKey);
			var v = TitleBackgroundColor.ToArgb();
			_ImageAttributes = new ImageAttributes {
				Flags = unchecked((uint)(_ImageAttributesFlags.IAF_RequiredFlags | _ImageAttributesFlags.IAF_Background)),
				ImageType = (uint)_UIImageType.IT_Bitmap,
				Format = (uint)_UIDataFormat.DF_WPF,
				StructSize = Marshal.SizeOf(typeof(ImageAttributes)),
				LogicalHeight = _IconSize,
				LogicalWidth = _IconSize,
				Background = (uint)(v & 0xFFFFFF << 8 | v & 0xFF)
			};
		}

		#endregion
	}
}
