using System;
using System.ComponentModel.Design;
using System.Xml.Linq;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using EnvDTE;

namespace Codist.Commands
{
	internal static class IncrementVsixVersionCommand
	{
		/// <summary>
		/// Command ID.
		/// </summary>
		public const int CommandId = 4128;

		/// <summary>
		/// Command menu group (command set GUID).
		/// </summary>
		public static readonly Guid CommandSet = new Guid("EB9F9D42-D000-4D82-874A-4688DDD26DBE");

		public static void Initialize(AsyncPackage package) {
			var menuItem = new OleMenuCommand(Execute, new CommandID(CommandSet, CommandId));
			menuItem.BeforeQueryStatus += (s, args) => {
				var cmd = s as OleMenuCommand;
				cmd.Visible = GetSelectedProjectItem() != null;
			};
			CodistPackage.MenuService.AddCommand(menuItem);
		}

		static void Execute(object sender, EventArgs e) {
			var item = GetSelectedProjectItem();
			if (item == null) {
				return;
			}
			const int YesButton = 6;
			if (item.Saved == false && YesButton == VsShellUtilities.ShowMessageBox(CodistPackage.Instance, item.Name + " is not saved.\nDiscard its changes?", "Increment Version", OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_YESNO, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND)) {
				return;
			}
			string message;
			bool error = false;
			const string Namespace = "http://schemas.microsoft.com/developer/vsx-schema/2011";
			try {
				var fileName = item.FileNames[0];
				var doc = XDocument.Load(fileName);
				var v = doc.Root.Element(XName.Get("Metadata", Namespace))?.Element(XName.Get("Identity", Namespace))?.Attribute("Version");
				if (v != null && Version.TryParse(v.Value, out var ver)) {
					v.Value = new Version(Math.Max(ver.Major, 1), Math.Max(ver.Minor, 0), Math.Max(ver.Build, 0), Math.Max(ver.Revision, 0) + 1).ToString();
					doc.Save(fileName);
					message = "Incremented version to " + v.Value;
				}
				else {
					message = "Version not found or verion number invalid in file " + fileName;
					error = true;
				}
			}
			catch (Exception ex) {
				message = ex.Message;
				error = true;
			}

			CodistPackage.ShowErrorMessageBox(message, "Increment Version", error);
		}

		static ProjectItem GetSelectedProjectItem() {
			ThreadHelper.ThrowIfNotOnUIThread();
			var items = (object[])CodistPackage.DTE2.ToolWindows.SolutionExplorer.SelectedItems;
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
