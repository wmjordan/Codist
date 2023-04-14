using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using AppHelpers;
using Codist.Controls;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using R = Codist.Properties.Resources;

namespace Codist.AutoBuildVersion
{
	/// <summary>
	/// Events related to building projects and solutions.
	/// </summary>
	/// <remarks>
	/// <para>We could have used <see cref="EnvDTE.BuildEvents"/>. However, VS 2022 won't work without importing Microsoft.VisualStudio.Shell.Interop v17, which breaks Codist on VS 2017 and VS 2019. Thus we rewrite one which implements the interfaces that <see cref="EnvDTE.BuildEvents"/> does.</para>
	/// <para>Further more, auto-build-version feature is also implemented with the <see cref="IVsSolutionEvents"/> and <see cref="IVsRunningDocTableEvents3"/> events.</para>
	/// </remarks>
	public sealed class BuildEvents : IVsUpdateSolutionEvents2, IVsSolutionEvents, IVsRunningDocTableEvents3
	{
		readonly CodistPackage _Package;
		readonly HashSet<Project> _ChangedProjects = new HashSet<Project>();
		readonly IVsSolution _VsSolution;
		uint _VsSolutionCookie, _RunningDocumentTableCookie;
		IVsRunningDocumentTable _RunningDocumentTable;
		bool _LockChangeTracking;

		internal BuildEvents(CodistPackage package) {
			ThreadHelper.ThrowIfNotOnUIThread("BuildEvents.ctor");
			ServicesHelper.Get<IVsSolutionBuildManager, SVsSolutionBuildManager>().AdviseUpdateSolutionEvents(this, out _);
			(_VsSolution = ServicesHelper.Get<IVsSolution, SVsSolution>()).AdviseSolutionEvents(this, out _VsSolutionCookie);
			// we don't always rely on the OnAfterOpenSolution event, a solution could have been loaded before CodistPackage is initialized
			(_RunningDocumentTable = ServicesHelper.Get<IVsRunningDocumentTable, SVsRunningDocumentTable>())?.AdviseRunningDocTableEvents(this, out _RunningDocumentTableCookie);
			_Package = package;
		}

		#region IVsUpdateSolutionEvents2
		public int UpdateSolution_Begin(ref int cancelUpdate) {
			return VSConstants.S_OK;
		}

		public int UpdateSolution_Done(int succeeded, int modified, int cancelCommand) {
			ThreadHelper.ThrowIfNotOnUIThread();
			if (Config.Instance.BuildOptions.MatchFlags(BuildOptions.BuildTimestamp)) {
				WriteBuildText(DateTime.Now.ToLongTimeString() + " " + R.T_BuildFinished + Environment.NewLine);
			}
			// hack: workaround to fix a bug in VS that causes build animation does not stop
			object icon = (short)Microsoft.VisualStudio.Shell.Interop.Constants.SBAI_Build;
			ServicesHelper.Get<IVsStatusbar, SVsStatusbar>().Animation(0, ref icon);
			_LockChangeTracking = false;
			return VSConstants.S_OK;
		}

		public int UpdateSolution_StartUpdate(ref int cancelUpdate) {
			ThreadHelper.ThrowIfNotOnUIThread();
			if (Config.Instance.BuildOptions.MatchFlags(BuildOptions.BuildTimestamp)) {
				WriteBuildText(DateTime.Now.ToLongTimeString() + " " + R.T_BuildStarted + Environment.NewLine);
			}
			if (Config.Instance.BuildOptions.MatchFlags(BuildOptions.PrintSolutionProjectProperties)) {
				PrintProperties(CodistPackage.DTE.Solution.Properties, "Solution " + CodistPackage.DTE.Solution.FileName);
			}
			_LockChangeTracking = true;
			return VSConstants.S_OK;
		}

		public int UpdateSolution_Cancel() {
			_LockChangeTracking = false;
			return VSConstants.S_OK;
		}

		public int OnActiveProjectCfgChange(IVsHierarchy vsHierarchy) {
#if DEBUG
			ThreadHelper.ThrowIfNotOnUIThread();
			if (vsHierarchy != null) {
				Project p = vsHierarchy.GetExtObjectAs<Project>();
				if (p != null) {
					WriteText($"Configuration of {p.UniqueName} changed to {p.ConfigurationManager.ActiveConfiguration.ConfigurationName}|{p.ConfigurationManager.ActiveConfiguration.PlatformName}.");
				}
			}
#endif
			return VSConstants.S_OK;
		}

		public int UpdateProjectCfg_Begin(IVsHierarchy proj, IVsCfg cfgProj, IVsCfg cfgSln, uint action, ref int cancel) {
			ThreadHelper.ThrowIfNotOnUIThread();
			// This method is called when a specific project begins building.
			var project = proj.GetExtObjectAs<Project>();
			if (project != null) {
				if (Config.Instance.BuildOptions.MatchFlags(BuildOptions.PrintSolutionProjectProperties)) {
					PrintProperties(project.Properties, "project " + project.Name);
					PrintProperties(project.ConfigurationManager.ActiveConfiguration.Properties, $"project {project.Name} active config");
				}
				if (Config.Instance.SuppressAutoBuildVersion == false
					&& (_ChangedProjects.Remove(project) || project.IsDirty)) {
					AutoChangeBuildVersion(cfgProj, project);
				}
			}
			return VSConstants.S_OK;
		}

		public int UpdateProjectCfg_Done(IVsHierarchy proj, IVsCfg cfgProj, IVsCfg cfgSln, uint action, int success, int cancel) {
			if (success == 0) {
				return VSConstants.S_OK;
			}
			ThreadHelper.ThrowIfNotOnUIThread();

			// if clean project or solution,   dwAction == 0x100000
			// if build project or solution,   dwAction == 0x010000
			// if rebuild project or solution, dwAction == 0x410000
			Project project;
			if (((VSSOLNBUILDUPDATEFLAGS)action).HasAnyFlag(VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_BUILD | VSSOLNBUILDUPDATEFLAGS.SBF_OPERATION_FORCE_UPDATE) == false
				|| (project = proj.GetExtObjectAs<Project>()) is null) {
				return VSConstants.S_OK;
			}
			if (Config.Instance.BuildOptions.MatchFlags(BuildOptions.VsixAutoIncrement) && project.IsVsixProject()) {
				AutoIncrementVsixVersion(project);
			}
			return VSConstants.S_OK;
		}
		#endregion

		void PrintProperties(EnvDTE.Properties properties, string title) {
			WriteBuildText(title + Environment.NewLine);
			foreach (var p in properties.Enumerate()) {
				WriteBuildText($"  {p.Key} = {p.Value}{Environment.NewLine}");
			}
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		static void AutoChangeBuildVersion(IVsCfg cfgProj, Project project) {
			if (cfgProj.get_DisplayName(out var s) != VSConstants.S_OK) {
				return;
			}

			try {
				var buildConfig = BuildSetting.Load(project);
				if (buildConfig != null) {
					var i = s.IndexOf('|');
					if (i != -1) {
						s = s.Substring(0, i);
					}
					var setting = buildConfig.Merge("<Any>", s);
					if (setting != null && setting.ShouldRewrite) {
						setting.RewriteVersion(project);
					}
				}
			}
			catch (Exception ex) {
				MessageWindow.Error(ex, "Changing version number failed.");
			}
		}

		void AutoIncrementVsixVersion(Project project) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var projItems = project.ProjectItems;
			for (int i = projItems.Count; i > 0; i--) {
				var item = projItems.Item(i);
				if (item.Name.EndsWith(".vsixmanifest", StringComparison.OrdinalIgnoreCase)) {
					if (item.IsOpen && item.IsDirty) {
						item.Document.NewWindow().Activate();
						MessageWindow.Error($"{item.Name} is open and modified. Auto increment VSIX version number failed.");
					}
					else if (Commands.IncrementVsixVersionCommand.IncrementVersion(item, out var message)) {
						WriteBuildText(nameof(Codist) + ": " + message + Environment.NewLine);
					}
					else {
						MessageWindow.Error("Auto increment VSIX version number failed:" + message);
					}
					break;
				}
			}
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
		void WriteBuildText(string text) {
			_Package.GetOutputPane(VSConstants.BuildOutput, "Build")?.OutputString(text);
		}
		[Conditional("DEBUG")]
		static void WriteText(string text) {
			CodistPackage.OutputString(text);
		}

		#region IVsSolutionEvents
		public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var project = pHierarchy.GetExtObjectAs<Project>();
			if (project != null) {
				WriteText($"Project {project.UniqueName} loaded.");
			}
			return VSConstants.S_OK;
		}

		public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) {
			return VSConstants.S_OK;
		}

		public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) {
			return VSConstants.S_OK;
		}

		public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) {
			return VSConstants.S_OK;
		}

		public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) {
			return VSConstants.S_OK;
		}

		public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) {
			return VSConstants.S_OK;
		}

		public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution) {
			ThreadHelper.ThrowIfNotOnUIThread();
			if (_RunningDocumentTable == null) {
				(_RunningDocumentTable = ServicesHelper.Get<IVsRunningDocumentTable, SVsRunningDocumentTable>())?.AdviseRunningDocTableEvents(this, out _RunningDocumentTableCookie);
			}
			WriteText("Solution loaded.");
			return VSConstants.S_OK;
		}

		public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) {
			return VSConstants.S_OK;
		}

		public int OnBeforeCloseSolution(object pUnkReserved) {
			return VSConstants.S_OK;
		}

		public int OnAfterCloseSolution(object pUnkReserved) {
			ThreadHelper.ThrowIfNotOnUIThread();
			_RunningDocumentTable?.UnadviseRunningDocTableEvents(_RunningDocumentTableCookie);
			_RunningDocumentTable = null;
			_ChangedProjects.Clear();
			_RunningDocumentTableCookie = 0;
			return VSConstants.S_OK;
		}
		#endregion

		#region IVsRunningDocTableEvents3
		public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) {
			return VSConstants.S_OK;
		}

		public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) {
			return VSConstants.S_OK;
		}

		public int OnAfterSave(uint docCookie) {
			if (_LockChangeTracking == false) {
				ThreadHelper.ThrowIfNotOnUIThread();
				if (_RunningDocumentTable is IVsRunningDocumentTable4 t) {
					var pGuid = t.GetDocumentProjectGuid(docCookie);
					if (_VsSolution.GetProjectOfGuid(ref pGuid, out var proj) == VSConstants.S_OK && proj != null) {
						var p = proj.GetExtObjectAs<Project>();
						if (p != null && _ChangedProjects.Add(p)) {
							WriteText($"Project {p.Name}: updated.");
						}
					}
				}
			}
			return VSConstants.S_OK;
		}

		public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) {
			return VSConstants.S_OK;
		}

		public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame) {
			return VSConstants.S_OK;
		}

		public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame) {
			return VSConstants.S_OK;
		}

		public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew) {
			return VSConstants.S_OK;
		}

		public int OnBeforeSave(uint docCookie) {
			return VSConstants.S_OK;
		}
		#endregion
	}
}