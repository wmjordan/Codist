using System;
using System.ComponentModel.Design;
using Codist.FileBrowser;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Codist.Commands;

/// <summary>
/// Command handler to display a window pane that list file system contents
/// </summary>
internal sealed class FileBrowserWindowCommand
{
	readonly AsyncPackage _Package;
	ToolWindowPane _Window;

	/// <summary>
	/// Initializes a new instance of the <see cref="FileBrowserWindowCommand"/> class.
	/// Adds our command handlers for menu (commands must exist in the command table file)
	/// </summary>
	/// <param name="package">Owner package, not null.</param>
	/// <param name="commandService">Command service to add command to, not null.</param>
	FileBrowserWindowCommand(AsyncPackage package, OleMenuCommandService commandService) {
		_Package = package ?? throw new ArgumentNullException(nameof(package));

		Command.FileBrowser.Register(Execute);
	}

	public static FileBrowserWindowCommand Instance { get; private set; }

	/// <summary>
	/// Initializes the singleton instance of the command.
	/// </summary>
	/// <param name="package">Owner package, not null.</param>
	public static async Task InitializeAsync(AsyncPackage package) {
		// Switch to the main thread - the call to AddCommand in FileExplorerWindowCommand's constructor requires
		// the UI thread.
		await SyncHelper.SwitchToMainThreadAsync(package.DisposalToken);

		var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
		Instance = new FileBrowserWindowCommand(package, commandService);
	}

	internal void ShowAt(string activeFolderPath) {
		_Package.JoinableTaskFactory.RunAsync(async () => {
			await ShowWindowAsync();
			if (_Window.Content is FileList list) {
				await list.NavigateToDirectoryAsync(activeFolderPath, _Package.DisposalToken);
			}
		}).FileAndForget(nameof(FileBrowserWindowCommand));
	}

	void Execute(object sender, EventArgs e) {
		ThreadHelper.ThrowIfNotOnUIThread();

		_Package.JoinableTaskFactory
			.RunAsync(ShowWindowAsync)
			.FileAndForget(nameof(FileBrowserWindowCommand));
	}

	async Task ShowWindowAsync() {
		var window = await _Package.ShowToolWindowAsync(typeof(FileBrowserWindow), 0, true, _Package.DisposalToken);
		if ((null == window) || (null == window.Frame)) {
			throw new NotSupportedException("Cannot create tool window");
		}

		await _Package.JoinableTaskFactory.SwitchToMainThreadAsync(_Package.DisposalToken);
		Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(((IVsWindowFrame)(_Window = window).Frame).Show());
	}
}
