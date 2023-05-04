using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Codist.Commands
{
	/// <summary>
	/// Command handler
	/// </summary>
	internal sealed class SymbolFinderWindowCommand
	{
		/// <summary>
		/// VS Package that provides this command, not null.
		/// </summary>
		private readonly AsyncPackage _Package;

		/// <summary>
		/// Initializes a new instance of the <see cref="SymbolFinderWindowCommand"/> class.
		/// Adds our command handlers for menu (commands must exist in the command table file)
		/// </summary>
		/// <param name="package">Owner package, not null.</param>
		/// <param name="commandService">Command service to add command to, not null.</param>
		private SymbolFinderWindowCommand(AsyncPackage package, OleMenuCommandService commandService) {
			this._Package = package ?? throw new ArgumentNullException(nameof(package));

			Command.SymbolFinderWindow.Register(Execute);
		}

		/// <summary>
		/// Gets the instance of the command.
		/// </summary>
		public static SymbolFinderWindowCommand Instance {
			get;
			private set;
		}

		/// <summary>
		/// Gets the service provider from the owner package.
		/// </summary>
		private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider {
			get {
				return this._Package;
			}
		}

		/// <summary>
		/// Initializes the singleton instance of the command.
		/// </summary>
		/// <param name="package">Owner package, not null.</param>
		public static async Task InitializeAsync(AsyncPackage package) {
			// Switch to the main thread - the call to AddCommand in SymbolFinderWindowCommand's constructor requires
			// the UI thread.
			await SyncHelper.SwitchToMainThreadAsync(package.DisposalToken);

			var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
			Instance = new SymbolFinderWindowCommand(package, commandService);
		}

		/// <summary>
		/// Shows the tool window when the menu item is clicked.
		/// </summary>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event args.</param>
		private void Execute(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();

			// Get the instance number 0 of this tool window. This window is single instance so this instance
			// is actually the only one.
			// The last flag is set to true so that if the tool window does not exists it will be created.
			var window = _Package.FindToolWindow(typeof(SymbolFinderWindow), 0, true);
			if ((null == window) || (null == window.Frame)) {
				throw new NotSupportedException("Cannot create " + nameof(SymbolFinderWindow));
			}

			var windowFrame = (IVsWindowFrame)window.Frame;
			Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
		}
	}
}
