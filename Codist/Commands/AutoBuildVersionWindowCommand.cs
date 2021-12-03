using System;
using System.Windows;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace Codist.Commands
{
	internal static class AutoBuildVersionWindowCommand
	{
		public static void Initialize() {
			Command.AutoBuildVersionWindow.Register(Execute, (s, args) => {
				((OleMenuCommand)s).Visible = GetSelectedProjectItem() != null;
			});
		}

#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
		static void Execute(object sender, EventArgs e) {
			var item = GetSelectedProjectItem();
			if (item == null) {
				return;
			}
			new AutoBuildVersionWindow(item) { Owner = Application.Current.MainWindow }.ShowDialog();
		}
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread

		static Project GetSelectedProjectItem() {
			ThreadHelper.ThrowIfNotOnUIThread();
			var items = (object[])CodistPackage.DTE.ToolWindows.SolutionExplorer.SelectedItems;
			foreach (UIHierarchyItem hi in items) {
				var item = hi.Object as Project;
				if (item != null
					&& item.Kind == TextEditorHelper.CSharpProjectKind) {
					return item;
				}
			}
			return null;
		}
	}
}
