using System;
using AppHelpers;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using R = Codist.Properties.Resources;

namespace Codist {
	/// <summary>
	/// Events related to building projects and solutions.
	/// </summary>
	/// <remarks>
	/// We could have used <see cref="EnvDTE.BuildEvents"/>. However, VS 2022 won't work without importing Microsoft.VisualStudio.Shell.Interop v17, which breaks Codist on VS 2017 and VS 2019.
	/// </remarks>
	public class BuildEvents : IVsUpdateSolutionEvents2, IVsUpdateSolutionEvents
	{
		readonly CodistPackage _Package;

		internal BuildEvents(CodistPackage package) {
			ThreadHelper.ThrowIfNotOnUIThread(".ctor");
			ServicesHelper.Get<IVsSolutionBuildManager, SVsSolutionBuildManager>().AdviseUpdateSolutionEvents(this, out _);
			_Package = package;
		}

		int IVsUpdateSolutionEvents.UpdateSolution_Begin(ref int cancelUpdate) {
			return VSConstants.S_OK;
		}

		int IVsUpdateSolutionEvents2.UpdateSolution_Begin(ref int cancelUpdate) {
			return VSConstants.S_OK;
		}

		int IVsUpdateSolutionEvents.UpdateSolution_Done(int succeeded, int modified, int cancelCommand) {
			ThreadHelper.ThrowIfNotOnUIThread();
			if (Config.Instance.BuildOptions.MatchFlags(BuildOptions.BuildTimestamp)) {
				var output = _Package.GetOutputPane(VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid, "Build");
				if (output != null) {
					output.OutputString(DateTime.Now.ToLongTimeString() + " " + R.T_BuildFinished + Environment.NewLine);
				}
			}
			return VSConstants.S_OK;
		}

		int IVsUpdateSolutionEvents2.UpdateSolution_Done(int succeeded, int modified, int cancelCommand) {
			return VSConstants.S_OK;
		}

		int IVsUpdateSolutionEvents.UpdateSolution_StartUpdate(ref int cancelUpdate) {
			ThreadHelper.ThrowIfNotOnUIThread();
			if (Config.Instance.BuildOptions.MatchFlags(BuildOptions.BuildTimestamp)) {
				var output = _Package.GetOutputPane(VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid, "Build");
				output?.OutputString(DateTime.Now.ToLongTimeString() + " " + R.T_BuildStarted + Environment.NewLine);
			}

			return VSConstants.S_OK;
		}

		int IVsUpdateSolutionEvents2.UpdateSolution_StartUpdate(ref int cancelUpdate) {
			return VSConstants.S_OK;
		}

		int IVsUpdateSolutionEvents2.UpdateSolution_Cancel() {
			return VSConstants.S_OK;
		}

		int IVsUpdateSolutionEvents.UpdateSolution_Cancel() {
			return VSConstants.S_OK;
		}

		int IVsUpdateSolutionEvents.OnActiveProjectCfgChange(IVsHierarchy vsHierarchy) {
			return VSConstants.S_OK;
		}

		int IVsUpdateSolutionEvents2.OnActiveProjectCfgChange(IVsHierarchy vsHierarchy) {
			return VSConstants.S_OK;
		}

		int IVsUpdateSolutionEvents2.UpdateProjectCfg_Begin(IVsHierarchy proj, IVsCfg cfgProj, IVsCfg cfgSln, uint action, ref int cancel) {
			// This method is called when a specific project begins building.

			// if clean project or solution,   dwAction == 0x100000
			// if build project or solution,   dwAction == 0x010000
			// if rebuild project or solution, dwAction == 0x410000
			return VSConstants.S_OK;
		}

		int IVsUpdateSolutionEvents2.UpdateProjectCfg_Done(IVsHierarchy proj, IVsCfg cfgProj, IVsCfg cfgSln, uint action, int success, int cancel) {
			ThreadHelper.ThrowIfNotOnUIThread();
			if (success == 0
				|| Config.Instance.BuildOptions.MatchFlags(BuildOptions.VsixAutoIncrement) == false
				|| proj.GetProperty(VSConstants.VSITEMID_ROOT, (int)VsHierarchyPropID.ExtObject, out var name) != 0) {
				return VSConstants.S_OK;
			}
			var project = name as EnvDTE.Project;
			if (project.IsVsixProject()) {
				AutoIncrementVsixVersion(project);
			}
			return VSConstants.S_OK;
		}

		void AutoIncrementVsixVersion(EnvDTE.Project project) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var projItems = project.ProjectItems;
			for (int i = projItems.Count; i > 0; i--) {
				var item = projItems.Item(i);
				if (item.Name.EndsWith(".vsixmanifest", StringComparison.OrdinalIgnoreCase)) {
					if (item.IsOpen && item.IsDirty) {
						item.Document.NewWindow().Activate();
						CodistPackage.ShowMessageBox(item.Name + " is open and modified. Auto increment VSIX version number failed.", nameof(Codist), true);
					}
					else if (Commands.IncrementVsixVersionCommand.IncrementVersion(item, out var message)) {
						var output = _Package.GetOutputPane(VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid, "Build");
						output?.OutputString(nameof(Codist) + ": " + message + Environment.NewLine);
					}
					else {
						CodistPackage.ShowMessageBox(message, "Auto increment VSIX version number failed.", true);
					}
					break;
				}
			}
		}
	}
}