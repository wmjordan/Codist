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
			Command.AutoBuildVersionWindow.Register(Execute, (s, args) => ((OleMenuCommand)s).Visible = GetSelectedProjectItem() != null);
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
			if (CodistPackage.DTE.ToolWindows.SolutionExplorer.SelectedItems is object[] selectedObjects) {
				foreach (UIHierarchyItem hi in selectedObjects.OfType<UIHierarchyItem>()) {
					if (hi.Object is Project item
						&& item.Kind == VsShellHelper.CSharpProjectKind) {
						return item;
					}
				}
			}
			return null;
		}
	}
}
