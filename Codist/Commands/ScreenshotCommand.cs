using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Codist.Commands
{
	/// <summary>A command which takes screenshot of the active code document window.</summary>
	internal sealed class ScreenshotCommand
	{
		/// <summary>
		/// Command ID.
		/// </summary>
		public const int CommandId = 0x0100;

		/// <summary>
		/// Command menu group (command set GUID).
		/// </summary>
		public static readonly Guid CommandSet = new Guid("d668a130-cb52-4143-b389-55560823f3d6");

		/// <summary>
		/// VS Package that provides this command, not null.
		/// </summary>
		readonly Package package;

		/// <summary>
		/// Initializes a new instance of the <see cref="ScreenshotCommand"/> class.
		/// Adds our command handlers for menu (commands must exist in the command table file)
		/// </summary>
		/// <param name="package">Owner package, not null.</param>
		/// <param name="commandService">Command service to add command to, not null.</param>
		private ScreenshotCommand(Package package, OleMenuCommandService commandService) {
			this.package = package ?? throw new ArgumentNullException(nameof(package));
			commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

			var menuCommandID = new CommandID(CommandSet, CommandId);
			var menuItem = new OleMenuCommand(Execute, menuCommandID);
			menuItem.BeforeQueryStatus += (s, args) => {
				var c = s as OleMenuCommand;
				c.Enabled = CodistPackage.DTE.ActiveDocument != null;
			};
			commandService.AddCommand(menuItem);
		}

		/// <summary>
		/// Gets the instance of the command.
		/// </summary>
		public static ScreenshotCommand Instance { get; private set; }

		/// <summary>
		/// Initializes the singleton instance of the command.
		/// </summary>
		/// <param name="package">Owner package, not null.</param>
		public static async Task InitializeAsync(AsyncPackage package) {
			// Switch to the main thread - the call to AddCommand in SymbolFinderWindowCommand's constructor requires
			// the UI thread.
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

			var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
			Instance = new ScreenshotCommand(package, commandService);
		}

		/// <summary>
		/// This function is the callback used to execute the command when the menu item is clicked.
		/// See the constructor to see how the menu item is associated with this function using
		/// OleMenuCommandService service and MenuCommand class.
		/// </summary>
		/// <param name="sender">Event sender.</param>
		/// <param name="e">Event args.</param>
		private void Execute(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var doc = CodistPackage.DTE.ActiveDocument;
			if (doc == null) {
				return;
			}
			var docWindow = package.GetActiveWpfDocumentView();
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
						var g = docWindow.VisualElement.GetVisualParent<System.Windows.Controls.Grid>();
						WpfHelper.ScreenShot(g, f.FileName, (int)g.ActualWidth, (int)g.ActualHeight);
					}
					catch (Exception ex) {
						VsShellUtilities.ShowMessageBox(
							package,
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
