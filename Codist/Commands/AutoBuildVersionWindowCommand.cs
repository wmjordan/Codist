using System;
using System.Linq;
using System.Windows;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace Codist.Commands
{
	internal static class AutoBuildVersionWindowCommand
	{
		public static void Initialize() {
			Command.AutoBuildVersionWindow.Register(Execute, (s, args) => {
				ThreadHelper.ThrowIfNotOnUIThread();
				((OleMenuCommand)s).Visible = GetSelectedProjectItem() != null;
			});
		}

		static void Execute(object sender, EventArgs e) {
			var item = GetSelectedProjectItem();
			if (item == null) {
				return;
			}
			new AutoBuildVersionWindow(item) { Owner = Application.Current.MainWindow }.ShowDialog();
		}

		static Project GetSelectedProjectItem() {
			ThreadHelper.ThrowIfNotOnUIThread();
			var p = VsShellHelper.GetActiveProjectInSolutionExplorer();
			return p.Kind == VsShellHelper.CSharpProjectKind ? p : null;
		}
	}
}
