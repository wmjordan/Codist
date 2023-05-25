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
		public static void Initialize() {
			Command.OpenOutputFolder.Register(Execute, (s, args) => ((OleMenuCommand)s).Visible = GetSelectedProject()?.ConfigurationManager != null);
			Command.OpenDebugOutputFolder.Register(ExecuteDebug, (s, args) => ((OleMenuCommand)s).Visible = GetSelectedProjectConfigurationExceptActive("Debug") != null);
			Command.OpenReleaseOutputFolder.Register(ExecuteRelease, (s, args) => ((OleMenuCommand)s).Visible = GetSelectedProjectConfigurationExceptActive("Release") != null);
		}

		static void Execute(object sender, EventArgs e) {
			var p = GetSelectedProject();
			if (p != null) {
				OpenOutputFolder(p, null);
			}
		}
		static void ExecuteDebug(object sender, EventArgs e) {
			var p = GetSelectedProject();
			if (p != null) {
				OpenOutputFolder(p, "Debug");
			}
		}
		static void ExecuteRelease(object sender, EventArgs e) {
			var p = GetSelectedProject();
			if (p != null) {
				OpenOutputFolder(p, "Release");
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
		static void OpenOutputFolder(Project p, string rowName) {
			try {
				if (p.Properties.Item("FullPath")?.Value is string projectPath
					&& (rowName == null ? p.ConfigurationManager.ActiveConfiguration : GetSelectedProjectConfigurationExceptActive(rowName))?.Properties.Item("OutputPath")?.Value is string confPath) {
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
