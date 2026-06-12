using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Codist.FileBrowser;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Events;
using Microsoft.VisualStudio.Text.Editor;
using R = Codist.Properties.Resources;

namespace Codist.Commands;

[Guid(WindowGuidString)]
public class FileBrowserWindow : ToolWindowPane
{
	const string WindowGuidString = "f4fff674-6d0e-4cae-8619-8a66bb65c7b5";
	internal static readonly Guid WindowGuid = new(WindowGuidString);

	FileList _FileList;
	bool _SolutionJustLoaded, _SolutionChanged;

	CancellationTokenSource _CancellationTokenSource = new();

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	public FileBrowserWindow() : base(null) {
		Caption = R.T_FileBrowser;
		Content = _FileList = new FileList(true);
		_FileList.IsVisibleChanged += HandleVisibilityChange;
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.EventHandler)]
	void HandleVisibilityChange(object sender, DependencyPropertyChangedEventArgs e) {
		if (!(bool)e.NewValue) {
			SolutionEvents.OnAfterOpenSolution -= HandleAfterOpenSolution;
			SolutionEvents.OnAfterCloseSolution -= HandleAfterCloseSolution;
			SolutionEvents.OnAfterOpenSolution += HandleSolutionChange;
			SolutionEvents.OnAfterCloseSolution += HandleSolutionChange;
			SolutionEvents.OnAfterLoadProjectBatch -= HandleAfterLoadProjects;
			TextEditorHelper.ActiveTextViewChanged -= HandleActiveTextViewChanged;
			TextEditorHelper.AllTextViewClosed -= HandleAllTextViewClosed;
			return;
		}

		SolutionEvents.OnAfterOpenSolution -= HandleSolutionChange;
		SolutionEvents.OnAfterCloseSolution -= HandleSolutionChange;
		SolutionEvents.OnAfterOpenSolution += HandleAfterOpenSolution;
		SolutionEvents.OnAfterCloseSolution += HandleAfterCloseSolution;
		SolutionEvents.OnAfterLoadProjectBatch += HandleAfterLoadProjects;
		TextEditorHelper.ActiveTextViewChanged += HandleActiveTextViewChanged;
		TextEditorHelper.AllTextViewClosed += HandleAllTextViewClosed;

		var currentFile = ServicesHelper.Instance.DTE.ActiveDocument?.FullName
			?? ServicesHelper.Instance.DTE.Solution.FullName;
		if (FileHelper.AreFileNamesEqual(currentFile, _FileList.CurrentFile)) {
			RefreshSolutionIfChanged();
			_FileList.LoadCurrentDirectoryAsync(_CancellationTokenSource.Token).FireAndForget();
		}
		else if (String.IsNullOrEmpty(currentFile)) {
			_FileList.CurrentFile = null;
			RefreshSolutionIfChanged();
			_FileList.NavigateToDirectoryAsync(VsShellHelper.GetDefaultProjectLocation(), _CancellationTokenSource.Token).FireAndForget();
		}
		else {
			_FileList.CurrentFile = currentFile;
			RefreshSolutionIfChanged();
			_FileList.LoadCurrentDirectoryAsync(_CancellationTokenSource.Token).FireAndForget();
		}
	}

	void HandleAllTextViewClosed(object sender, EventArgs e) {
		_FileList.ClearCurrentFileAsync(SyncHelper.CancelAndRetainToken(ref _CancellationTokenSource)).FireAndForget();
	}

	void HandleSolutionChange(object sender, EventArgs e) {
		_SolutionChanged = true;
	}

	void HandleAfterOpenSolution(object sender, EventArgs e) {
		_SolutionJustLoaded = true;
		_FileList.RefreshSolutionAsync(SyncHelper.CancelAndRetainToken(ref _CancellationTokenSource)).FireAndForget();
	}

	void HandleAfterCloseSolution(object sender, EventArgs e) {
		_FileList.RefreshSolutionAsync(SyncHelper.CancelAndRetainToken(ref _CancellationTokenSource)).FireAndForget();
	}
	void HandleAfterLoadProjects(object sender, LoadProjectBatchEventArgs e) {
		_FileList.RefreshProjectAsync(SyncHelper.CancelAndRetainToken(ref _CancellationTokenSource)).FireAndForget();
	}

	void HandleActiveTextViewChanged(object sender, TextViewCreatedEventArgs e) {
		if (!e.TextView.Roles.Contains(PredefinedTextViewRoles.PrimaryDocument)) {
			return;
		}
		_FileList.RefreshCurrentFileAsync(_SolutionJustLoaded, SyncHelper.CancelAndRetainToken(ref _CancellationTokenSource)).FireAndForget();
		_SolutionJustLoaded = false;
	}

	void RefreshSolutionIfChanged() {
		if (_SolutionChanged) {
			_SolutionChanged = false;
			_FileList.RefreshSolutionAsync(_CancellationTokenSource.Token).FireAndForget();
		}
	}

	protected override void Dispose(bool disposing) {
		base.Dispose(disposing);
		_CancellationTokenSource.CancelAndDispose();
	}
}
