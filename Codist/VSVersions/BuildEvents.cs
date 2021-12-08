using System;
using AppHelpers;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using R = Codist.Properties.Resources;

namespace Codist
{
	/// <summary>
	/// Events related to building projects and solutions.
	/// </summary>
	/// <remarks>
	/// We could have used <see cref="EnvDTE.BuildEvents"/>. However, VS 2022 won't work without importing Microsoft.VisualStudio.Shell.Interop v17, which breaks Codist on VS 2017 and VS 2019. Thus we rewrite one which implements the interfaces that  <see cref="EnvDTE.BuildEvents"/> does.
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
				WriteBuildText(DateTime.Now.ToLongTimeString() + " " + R.T_BuildFinished + Environment.NewLine);
			}
			return VSConstants.S_OK;
		}

		int IVsUpdateSolutionEvents2.UpdateSolution_Done(int succeeded, int modified, int cancelCommand) {
			return VSConstants.S_OK;
		}

		int IVsUpdateSolutionEvents.UpdateSolution_StartUpdate(ref int cancelUpdate) {
			ThreadHelper.ThrowIfNotOnUIThread();
			if (Config.Instance.BuildOptions.MatchFlags(BuildOptions.BuildTimestamp)) {
				WriteBuildText(DateTime.Now.ToLongTimeString() + " " + R.T_BuildStarted + Environment.NewLine);
			}
			if (Config.Instance.BuildOptions.MatchFlags(BuildOptions.PrintSolutionProjectProperties)) {
				PrintProperties(CodistPackage.DTE.Solution.Properties, "Solution " + CodistPackage.DTE.Solution.FileName);
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
			Project project;
			if (Config.Instance.BuildOptions.MatchFlags(BuildOptions.PrintSolutionProjectProperties)
				&& (project = proj.GetExtObjectAs<Project>()) != null) {
				PrintProperties(project.Properties, "project " + project.Name);
				PrintProperties(project.ConfigurationManager.ActiveConfiguration.Properties, $"project {project.Name} active config");
			}
			return VSConstants.S_OK;
		}

		int IVsUpdateSolutionEvents2.UpdateProjectCfg_Done(IVsHierarchy proj, IVsCfg cfgProj, IVsCfg cfgSln, uint action, int success, int cancel) {
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
			AutoChangeBuildVersion(cfgProj, project);
			return VSConstants.S_OK;
		}

		void PrintProperties(EnvDTE.Properties properties, string title) {
			var c = properties.Count;
			WriteBuildText(title + Environment.NewLine);
			for (int i = 1; i <= c; i++) {
				var p = properties.Item(i);
				try {
					WriteBuildText($"  {p.Name} = {p.Value}{Environment.NewLine}");
				}
				catch (System.Runtime.InteropServices.COMException) {
					WriteBuildText($"  {p.Name} = ?{Environment.NewLine}");
				}
			}
		}

		static void AutoChangeBuildVersion(IVsCfg cfgProj, Project project) {
			if (cfgProj.get_DisplayName(out var s) != VSConstants.S_OK || !project.IsCSharpProject()) {
				return;
			}

			try {
				var buildConfig = AutoBuildVersion.BuildSetting.Load(project);
				if (buildConfig != null) {
					var i = s.IndexOf('|');
					if (i != -1) {
						s = s.Substring(0, i);
					}
					if (buildConfig.TryGetValue(s, out var setting) && setting.ShouldRewrite
						|| buildConfig.TryGetValue("<Any>", out setting) && setting.ShouldRewrite) {
						setting.RewriteVersion(project);
					}
				}
			}
			catch (Exception ex) {
				CodistPackage.ShowMessageBox(ex.Message, "Changing version number failed.", true);
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
						CodistPackage.ShowMessageBox(item.Name + " is open and modified. Auto increment VSIX version number failed.", nameof(Codist), true);
					}
					else if (Commands.IncrementVsixVersionCommand.IncrementVersion(item, out var message)) {
						WriteBuildText(nameof(Codist) + ": " + message + Environment.NewLine);
					}
					else {
						CodistPackage.ShowMessageBox(message, "Auto increment VSIX version number failed.", true);
					}
					break;
				}
			}
		}

		void WriteBuildText(string text) {
			_Package.GetOutputPane(VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid, "Build")?.OutputString(text);
		}
	}
}