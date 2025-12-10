using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Codist.Controls;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;

namespace Codist.Commands
{
	internal static class OpenOutputFolderCommand
	{
		const string OutputPathId = "OutputPath", IntermediatePathId = "IntermediatePath",
			DebugConfigId = "Debug", ReleaseConfigId = "Release";

		public static void Initialize() {
            Command.OpenOutputFolder.Register(Execute, HasSelectedProject);
			Command.OpenDebugOutputFolder.Register(ExecuteDebug, (s, args) => {
				ThreadHelper.ThrowIfNotOnUIThread();
				((OleMenuCommand)s).Visible = GetSelectedProjectConfigurationExceptActive("Debug") != null;
			});
			Command.OpenReleaseOutputFolder.Register(ExecuteRelease, HasReleaseConfig);
			Command.OpenIntermediateFolder.Register(ExecuteIntermediate, HasSelectedProject);
			Command.OpenReleaseIntermediateFolder.Register(ExecuteReleaseIntermediate, HasReleaseConfig);
		}

		static void HasSelectedProject(object s, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			((OleMenuCommand)s).Visible = GetSelectedProject()?.ConfigurationManager != null;
		}
		static void HasReleaseConfig(object s, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			((OleMenuCommand)s).Visible = GetSelectedProjectConfigurationExceptActive("Release") != null;
		}

		static void Execute(object sender, EventArgs e) {
            TryOpenPath(null, OutputPathId);
		}
		static void ExecuteIntermediate(object sender, EventArgs e) {
            TryOpenPath(null, IntermediatePathId);
		}
		static void ExecuteReleaseIntermediate(object sender, EventArgs e) {
            TryOpenPath(ReleaseConfigId, IntermediatePathId);
		}
		static void ExecuteDebug(object sender, EventArgs e) {
            TryOpenPath(DebugConfigId, OutputPathId);
        }

        static void ExecuteRelease(object sender, EventArgs e) {
			TryOpenPath(ReleaseConfigId, OutputPathId);
		}

		static void TryOpenPath(string configId, string pathId) {
            var p = GetSelectedProject();
            if (p != null) {
                OpenOutputFolder(p, configId, pathId);
            }
        }
		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.EventHandler)]
		static Configuration GetSelectedProjectConfigurationExceptActive(string rowName) {
			var cm = GetSelectedProject()?.ConfigurationManager;
			if (cm == null || cm.ActiveConfiguration.ConfigurationName == rowName) {
				return null;
			}
			var p = cm.ActiveConfiguration.PlatformName;
			for (int i = cm.Count; i > 0; i--) {
				var item = cm.Item(i);
				if (item.ConfigurationName == rowName && item.PlatformName == p) {
					return item;
				}
			}
			return null;
		}

		[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.EventHandler)]
		static void OpenOutputFolder(Project p, string rowName, string pathId) {
			try {
				if (p.Properties.Item("FullPath")?.Value is string projectPath
					&& (rowName == null ? p.ConfigurationManager.ActiveConfiguration : GetSelectedProjectConfigurationExceptActive(rowName))?.Properties.Item(pathId)?.Value is string confPath) {
					var outputPath = Path.Combine(projectPath, confPath);
					if (Directory.Exists(outputPath)) {
						FileHelper.TryRun(outputPath);
					}
					else {
						MessageWindow.Error($"{R.T_OutputFolderMissing}{Environment.NewLine}{Environment.NewLine}{outputPath}", R.CMD_OpenOutputFolder);
					}
				}
			}
			catch (Exception ex) {
				ShowError(ex);
			}
		}

		static void ShowError(Exception ex) {
			MessageWindow.Error($"{R.T_FailedToOpenOutputFolder}{Environment.NewLine}{Environment.NewLine}{ex}", R.CMD_OpenOutputFolder);
		}

		static Project GetSelectedProject() {
			ThreadHelper.ThrowIfNotOnUIThread();
			var dte = CodistPackage.DTE;
			var p = dte.ActiveWindow.Project;
			if (p != null) {
				return p;
			}
			var o = (dte.ToolWindows.SolutionExplorer?.SelectedItems as object[])
				?.OfType<UIHierarchyItem>()
				.FirstOrDefault()
				?.Object;
			if (o != null) {
				if ((p = o as Project) != null) {
					return p;
				}
				if (o is ProjectItem pi) {
					return pi.ContainingProject;
				}
			}
			return null;
		}
	}
}
