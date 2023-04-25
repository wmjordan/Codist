using System;
using System.IO;
using Codist.Controls;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;

namespace Codist.Commands
{
	internal static class OpenOutputFolderCommand
	{
		public static void Initialize() {
			Command.OpenOutputFolder.Register(Execute, (s, args) => ((OleMenuCommand)s).Visible = GetSelectedProject() != null);
		}

		static void Execute(object sender, EventArgs e) {
			var p = GetSelectedProject();
			if (p == null) {
				return;
			}

			try {
				if (p.Properties.Item("FullPath")?.Value is string projectPath
					&& p.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath")?.Value is string confPath) {
					var outputPath = Path.Combine(projectPath, confPath);
					if (Directory.Exists(outputPath)) {
						FileHelper.TryRun(outputPath);
					}
					else {
						MessageWindow.Error($"{R.T_OutputFolderMissing}{Environment.NewLine}{outputPath}", R.CMD_OpenOutputFolder);
					}
				}
			}
			catch (System.Runtime.InteropServices.COMException ex) {
				ShowError(ex);
			}
			catch (IOException ex) {
				ShowError(ex);
			}
			catch (InvalidOperationException ex) {
				ShowError(ex);
			}
		}

		static void ShowError(Exception ex) {
			MessageWindow.Error($"{R.T_FailedToOpenOutputFolder}{Environment.NewLine}{ex}", R.CMD_OpenOutputFolder);
		}

		static Project GetSelectedProject() {
			ThreadHelper.ThrowIfNotOnUIThread();
			return CodistPackage.DTE.ActiveDocument?.ProjectItem.ContainingProject;
		}
	}
}
