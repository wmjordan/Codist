using System;
using System.ComponentModel.Design;
using System.Xml.Linq;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using EnvDTE;
using Codist.Controls;
using R = Codist.Properties.Resources;

namespace Codist.Commands
{
	internal static class IncrementVsixVersionCommand
	{
		public static void Initialize() {
			Command.IncrementVsixVersion.Register(Execute, (s, args) => {
				((OleMenuCommand)s).Visible = GetSelectedProjectItem() != null;
			});
		}

#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
		static void Execute(object sender, EventArgs e) {
			var item = GetSelectedProjectItem();
			if (item == null) {
				return;
			}
			const int YesButton = 6;
			if (item.Saved == false && YesButton == VsShellUtilities.ShowMessageBox(CodistPackage.Instance, item.Name + " is not saved.\nDiscard its changes?", R.T_IncrementVersion, OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_YESNO, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND)) {
				return;
			}
			string message;
			bool error = IncrementVersion(item, out message) == false;
			if (error) {
				MessageWindow.Error(message, R.T_IncrementVersion);
			}
			else {
				MessageWindow.Show(message, R.T_IncrementVersion);
			}
		}
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread

		internal static bool IncrementVersion(ProjectItem item, out string message) {
			ThreadHelper.ThrowIfNotOnUIThread();
			const string Namespace = "http://schemas.microsoft.com/developer/vsx-schema/2011";
			try {
				var fileName = item.FileNames[0];
				var file = new System.IO.FileInfo(fileName);
				var fileTime = file.LastWriteTime;
				var doc = XDocument.Load(fileName);
				var v = doc.Root.Element(XName.Get("Metadata", Namespace))?.Element(XName.Get("Identity", Namespace))?.Attribute("Version");
				if (v != null && Version.TryParse(v.Value, out var ver)) {
					v.Value = new Version(Math.Max(ver.Major, 1), Math.Max(ver.Minor, 0), Math.Max(ver.Build, 0), Math.Max(ver.Revision, 0) + 1).ToString();
					doc.Save(fileName);
					file.LastWriteTime = fileTime;
					message = "Incremented VSIX manifest version to " + v.Value;
					return true;
				}

				message = "Version not found or version number invalid in file " + fileName;
			}
			catch (Exception ex) {
				message = ex.Message;
			}
			return false;
		}

		static ProjectItem GetSelectedProjectItem() {
			ThreadHelper.ThrowIfNotOnUIThread();
			var items = (object[])CodistPackage.DTE.ToolWindows.SolutionExplorer.SelectedItems;
			foreach (UIHierarchyItem hi in items) {
				var item = hi.Object as ProjectItem;
				if (item != null
					&& item.Name.EndsWith(".extension.vsixmanifest", StringComparison.OrdinalIgnoreCase)
					&& item.FileCount > 0) {
					return item;
				}
			}
			return null;
		}
	}
}
