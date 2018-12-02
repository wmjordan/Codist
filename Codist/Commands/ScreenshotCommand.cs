using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codist.Commands
{
	/// <summary>A command which takes screenshot of the active code document window.</summary>
	internal static class ScreenshotCommand
	{
		/// <summary>
		/// Command ID.
		/// </summary>
		public const int CommandId = 0x0100;

		/// <summary>
		/// Command menu group (command set GUID).
		/// </summary>
		public static readonly Guid CommandSet = new Guid("d668a130-cb52-4143-b389-55560823f3d6");

		public static void Initialize(AsyncPackage package) {
			var menuItem = new OleMenuCommand(Execute, new CommandID(CommandSet, CommandId));
			menuItem.BeforeQueryStatus += (s, args) => {
				ThreadHelper.ThrowIfNotOnUIThread();
				var c = s as OleMenuCommand;
				c.Enabled = CodistPackage.DTE.ActiveDocument != null;
			};
			CodistPackage.MenuService.AddCommand(menuItem);
		}

		static void Execute(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var doc = CodistPackage.DTE.ActiveDocument;
			if (doc == null) {
				return;
			}
			var docWindow = CodistPackage.Instance.GetActiveWpfDocumentView();
			if (docWindow == null) {
				return;
			}
			using (var f = new System.Windows.Forms.SaveFileDialog {
				Filter = "PNG images (*.png)|*.png",
				AddExtension = true,
				Title = "Please specify the location of the screenshot file",
				FileName = System.IO.Path.GetFileNameWithoutExtension(doc.Name) + ".png"
			}) {
				if (f.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
					try {
						var g = docWindow.VisualElement.GetParent<System.Windows.Controls.Grid>();
						WpfHelper.ScreenShot(g, f.FileName, (int)g.ActualWidth, (int)g.ActualHeight);
					}
					catch (Exception ex) {
						VsShellUtilities.ShowMessageBox(
							CodistPackage.Instance,
							"Failed to save screenshot for " + doc.Name + "\n" + ex.Message,
							nameof(Codist),
							OLEMSGICON.OLEMSGICON_INFO,
							OLEMSGBUTTON.OLEMSGBUTTON_OK,
							OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
					}
				}
			}
		}
	}
}
