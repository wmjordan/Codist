﻿using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Events;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio;
using AppHelpers;

namespace Codist
{
	/// <summary>
	/// <para>This is the class that implements the package exposed by <see cref="Codist"/>.</para>
	/// <para>The project consists of the following namespace: <see cref="SyntaxHighlight"/> backed by <see cref="Taggers"/>, <see cref="SmartBars"/>, <see cref="QuickInfo"/>, <see cref="Margins"/>, <see cref="NaviBar"/> etc.</para>
	/// </summary>
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[InstalledProductRegistration("#110", "#112", "5.3", IconResourceID = 400)] // Information on this package for Help/About
	[Guid(PackageGuidString)]
	[ProvideOptionPage(typeof(Options.General), Constants.NameOfMe, "General", 0, 0, true, Sort = 0)]

	[ProvideOptionPage(typeof(Options.SyntaxHighlight), Constants.NameOfMe, "Syntax Highlight", 0, 0, true, Sort = 10)]
	[ProvideOptionPage(typeof(Options.CommonStyle), CategorySyntaxHighlight, "All languages", 0, 0, true, Sort = 11)]
	[ProvideOptionPage(typeof(Options.CSharpStyle), CategorySyntaxHighlight, "C#", 0, 0, true, Sort = 12)]
	[ProvideOptionPage(typeof(Options.CppStyle), CategorySyntaxHighlight, "C/C++", 0, 0, true, Sort = 13)]
	[ProvideOptionPage(typeof(Options.MarkdownStyle), CategorySyntaxHighlight, "Markdown", 0, 0, true, Sort = 14)]
	[ProvideOptionPage(typeof(Options.XmlStyle), CategorySyntaxHighlight, "XML", 0, 0, true, Sort = 15)]
	[ProvideOptionPage(typeof(Options.CommentStyle), CategorySyntaxHighlight, "Comment", 0, 0, true, Sort = 19)]

	[ProvideOptionPage(typeof(Options.SuperQuickInfo), Constants.NameOfMe, "Super Quick Info", 0, 0, true, Sort = 20)]
	[ProvideOptionPage(typeof(Options.CSharpSuperQuickInfo), CategorySuperQuickInfo, "C#", 0, 0, true, Sort = 21)]

	[ProvideOptionPage(typeof(Options.SmartBar), Constants.NameOfMe, "Smart Bar", 0, 0, true, Sort = 30)]

	[ProvideOptionPage(typeof(Options.NaviBar), Constants.NameOfMe, "Navigation Bar", 0, 0, true, Sort = 40)]

	[ProvideOptionPage(typeof(Options.ScrollbarMarker), Constants.NameOfMe, "Scrollbar Marker", 0, 0, true, Sort = 50)]
	[ProvideOptionPage(typeof(Options.CSharpScrollbarMarker), CategoryScrollbarMarker, "C#", 0, 0, true, Sort = 51)]
	[ProvideMenuResource("Menus.ctmenu", 1)]
	//[ProvideToolWindow(typeof(Commands.SymbolFinderWindow))]
	[ProvideAutoLoad(VSConstants.UICONTEXT.ShellInitialized_string, PackageAutoLoadFlags.BackgroundLoad)]
	sealed class CodistPackage : AsyncPackage
	{
		/// <summary>CodistPackage GUID string.</summary>
		const string PackageGuidString = "c7b93d20-621f-4b21-9d28-d51157ef0b94";

		const string CategorySuperQuickInfo = Constants.NameOfMe + "\\Super Quick Info";
		const string CategoryScrollbarMarker = Constants.NameOfMe + "\\Scrollbar Marker";
		const string CategorySyntaxHighlight = Constants.NameOfMe + "\\Syntax Highlight";
		const string CategoryNaviBar = Constants.NameOfMe + "\\Navigation Bar";
		const string CategorySmartBar = Constants.NameOfMe + "\\Smart Bar";

		static EnvDTE.DTE _dte;
		static EnvDTE80.DTE2 _dte2;
		static EnvDTE.Events _dteEvents;
		static EnvDTE.BuildEvents _buildEvents;
		static OleMenuCommandService _menu;
		static IOleComponentManager _componentManager;

		//int _extenderCookie;

		//static VsDebugger _Debugger;

		/// <summary>
		/// Initializes a new instance of the <see cref="CodistPackage"/> class.
		/// </summary>
		public CodistPackage() {
			// Inside this method you can place any initialization code that does not require
			// any Visual Studio service because at this point the package object is created but
			// not sited yet inside Visual Studio environment. The place to do all the other
			// initialization is the Initialize method.
			Instance = this;
		}

		public static CodistPackage Instance { get; private set; }
		public static EnvDTE.DTE DTE {
			get {
				ThreadHelper.ThrowIfNotOnUIThread();
				return _dte ?? (_dte = ServiceProvider.GlobalProvider.GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE);
			}
		}
		public static EnvDTE80.DTE2 DTE2 {
			get {
				ThreadHelper.ThrowIfNotOnUIThread();
				return _dte2 ?? (_dte2 = ServiceProvider.GlobalProvider.GetService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2);
			}
		}
		public static OleMenuCommandService MenuService {
			get {
				ThreadHelper.ThrowIfNotOnUIThread();
				return _menu ?? (_menu = Instance.GetService(typeof(System.ComponentModel.Design.IMenuCommandService)) as OleMenuCommandService);
			}
		}
		public static IOleComponentManager OleComponentManager {
			get {
				ThreadHelper.ThrowIfNotOnUIThread();
				return _componentManager ?? (_componentManager = ServiceProvider.GetGlobalServiceAsync<SOleComponentManager, IOleComponentManager>().ConfigureAwait(false).GetAwaiter().GetResult());
			}
		}

		public static DebuggerStatus DebuggerStatus {
			get {
				ThreadHelper.ThrowIfNotOnUIThread(nameof(DebuggerStatus));
				switch (DTE.Debugger.CurrentMode) {
					case EnvDTE.dbgDebugMode.dbgBreakMode: return DebuggerStatus.Break;
					case EnvDTE.dbgDebugMode.dbgDesignMode: return DebuggerStatus.Design;
					case EnvDTE.dbgDebugMode.dbgRunMode: return DebuggerStatus.Running;
				}
				return DebuggerStatus.Design;
			}
		}

		public static void ShowErrorMessageBox(string message, string title, bool error) {
			VsShellUtilities.ShowMessageBox(
				Instance,
				message,
				title ?? nameof(Codist),
				error ? OLEMSGICON.OLEMSGICON_WARNING : OLEMSGICON.OLEMSGICON_INFO,
				OLEMSGBUTTON.OLEMSGBUTTON_OK,
				OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
		}

		#region Package Members
		/// <summary>
		/// Initialization of the package; this method is called right after the package is sited, so this is the place
		/// where you can put all the initialization code that rely on services provided by VisualStudio.
		/// </summary>
		/// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
		/// <param name="progress">A provider for progress updates.</param>
		/// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
		protected override async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress) {
			await base.InitializeAsync(cancellationToken, progress);

			VSColorTheme.ThemeChanged += _ => {
				System.Diagnostics.Debug.WriteLine("Theme changed.");
				ThemeHelper.RefreshThemeCache();
			};
			SolutionEvents.OnAfterOpenSolution += (s, args) => {
				Taggers.SymbolMarkManager.Clear();
			};
			// When initialized asynchronously, the current thread may be a background thread at this point.
			// Do any initialization that requires the UI thread after switching to the UI thread.
			await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
			if ((Config.Instance.DisplayOptimizations & DisplayOptimizations.MainWindow) != 0) {
				WpfHelper.SetUITextRenderOptions(Application.Current.MainWindow, true);
			}
			_dteEvents = DTE2.Events;
			_buildEvents = _dteEvents.BuildEvents;
			_buildEvents.OnBuildBegin += BuildEvents_OnBuildBegin;
			_buildEvents.OnBuildDone += BuildEvents_OnBuildEnd;
			_buildEvents.OnBuildProjConfigDone += BuildEvents_OnBuildProjConfigDone;
			//_extenderCookie = DTE.ObjectExtenders.RegisterExtenderProvider(VSConstants.CATID.CSharpFileProperties_string, BuildBots.AutoReplaceExtenderProvider.Name, new BuildBots.AutoReplaceExtenderProvider());
			//await Commands.SymbolFinderWindowCommand.InitializeAsync(this);
			Commands.ScreenshotCommand.Initialize(this);
			Commands.IncrementVsixVersionCommand.Initialize(this);
			Commands.NaviBarSearchDeclarationCommand.Initialize(this);
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			//DTE.ObjectExtenders.UnregisterExtenderProvider(_extenderCookie);
		}

		void BuildEvents_OnBuildBegin(EnvDTE.vsBuildScope Scope, EnvDTE.vsBuildAction Action) {
			if (Config.Instance.BuildOptions.MatchFlags(BuildOptions.BuildTimestamp)) {
				var output = GetOutputPane(VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid, "Build");
				output?.OutputString(DateTime.Now.ToLongTimeString() + " Build started." + Environment.NewLine);
			}
		}

		void BuildEvents_OnBuildEnd(EnvDTE.vsBuildScope Scope, EnvDTE.vsBuildAction Action) {
			if (Config.Instance.BuildOptions.MatchFlags(BuildOptions.BuildTimestamp)) {
				var output = GetOutputPane(VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid, "Build");
				if (output != null) {
					output.OutputString(DateTime.Now.ToLongTimeString() + " Build finished." + Environment.NewLine);
				}
			}
		}

		void BuildEvents_OnBuildProjConfigDone(string projectName, string projectConfig, string platform, string solutionConfig, bool success) {
			if (success == false
				|| Config.Instance.BuildOptions.MatchFlags(BuildOptions.VsixAutoIncrement) == false) {
				return;
			}
			var project = TextEditorHelper.GetProject(projectName);
			if (project.IsVsixProject() == false) {
				return;
			}
			var projItems = project.ProjectItems;
			for (int i = projItems.Count; i > 0; i--) {
				var item = projItems.Item(i);
				if (item.Name.EndsWith(".vsixmanifest", StringComparison.OrdinalIgnoreCase)) {
					if (item.IsOpen && item.IsDirty) {
						item.Document.NewWindow().Activate();
						ShowErrorMessageBox(item.Name + " is open and modified. Auto increment VSIX version number failed.", nameof(Codist), true);
					}
					else if (Commands.IncrementVsixVersionCommand.IncrementVersion(item, out var message)) {
						var output = GetOutputPane(VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid, "Build");
						output?.OutputString(nameof(Codist) + ": " + message + Environment.NewLine);
					}
					else {
						ShowErrorMessageBox(message, "Auto increment VSIX version number failed.", true);
					}
					break;
				}
			}
		}

		#endregion
	}

	internal static class SharedDictionaryManager
	{
		static ResourceDictionary _Controls, _Menu, _ContextMenu, _ItemList, _SymbolList;

		internal static ResourceDictionary ThemedControls => _Controls ?? (_Controls = WpfHelper.LoadComponent("controls/ThemedControls.xaml"));

		// to get started with our own context menu styles, see this answer on StackOverflow
		// https://stackoverflow.com/questions/3391742/wpf-submenu-styling?rq=1
		internal static ResourceDictionary ContextMenu => _ContextMenu ?? (_ContextMenu = WpfHelper.LoadComponent("controls/ContextMenu.xaml").MergeWith(ThemedControls));

		// for menu styles, see https://docs.microsoft.com/en-us/dotnet/framework/wpf/controls/menu-styles-and-templates
		internal static ResourceDictionary Menu => _Menu ?? (_Menu = WpfHelper.LoadComponent("controls/NavigationBar.xaml").MergeWith(ThemedControls));

		internal static ResourceDictionary ItemList => _ItemList ?? (_ItemList = WpfHelper.LoadComponent("controls/ItemList.xaml").MergeWith(ThemedControls));
		internal static ResourceDictionary SymbolList => _SymbolList ?? (_SymbolList = WpfHelper.LoadComponent("controls/SymbolList.xaml").MergeWith(ThemedControls));
	}

}
