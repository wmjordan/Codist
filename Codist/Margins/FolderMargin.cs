using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Codist.Controls;
using Codist.FileBrowser;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using R = Codist.Properties.Resources;

namespace Codist.Margins;

sealed class FolderMargin : IWpfTextViewMargin
{
	internal const string Name = nameof(FolderMargin);
	static readonly Thickness __ContainerMargin = CodistPackage.VsVersion.Major < 17 ? WpfHelper.NoMargin : new Thickness(2);

	readonly ThemedControlGroup _Container;
	readonly ThemedToggleButton _FileButton, _SolutionButton, _ProjectViewButton;
	readonly IWpfTextView _View;
	readonly ITextDocument _Document;
	ThemedToggleButton _ProjectButton;
	Popup _FilePopup;
	FileList _FileList;
	CancellationTokenSource _CancellationTokenSource;

	public FrameworkElement VisualElement => _Container;
	public double MarginSize => _FileButton.RenderSize.Height;
	public bool Enabled => true;

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	public FolderMargin(IWpfTextView view) {
		string path;
		_Container = new ThemedControlGroup {
			Margin = __ContainerMargin,
			Resources = SharedDictionaryManager.ThemedControls
		};
		if (!String.IsNullOrEmpty(path = ServicesHelper.Instance.DTE.Solution.FullName)) {
			_SolutionButton = new ThemedToggleButton(IconIds.GoToSolutionFolder, R.CMD_GoToSolutionFolder, OnSolutionClick) {
				Background = Brushes.Transparent,
				BorderThickness = WpfHelper.NoMargin
			}.TinySpacing();
			_SolutionButton.SetText(Path.GetFileNameWithoutExtension(path));
			_SolutionButton.Text.Margin = WpfHelper.SmallHorizontalMargin;

			_ProjectViewButton = new ThemedToggleButton(IconIds.GoToSolutionProjects, R.CMD_ListProjectFolders, OnProjectViewClick) {
				Background = Brushes.Transparent,
				BorderThickness = WpfHelper.NoMargin
			}.TinySpacing();
			_ProjectViewButton.SetText("\\");
			_ProjectViewButton.Text.Margin = WpfHelper.SmallHorizontalMargin;

			_Container.AddRange(_ProjectViewButton, _SolutionButton);

			view.VisualElement.Loaded += AddProjectButtonOnLoaded;
		}

		_Document = view.TextBuffer.GetTextDocument();
		_FileButton = new ThemedToggleButton(IconIds.Folder, R.CMDT_ClickToViewFolder, OnFolderClick) {
			Background = Brushes.Transparent,
			BorderThickness = WpfHelper.NoMargin
		}.TinySpacing();
		_Document.FileActionOccurred += HandleDocumentFileActivation;
		_FileButton.SetText(Path.GetFileName(Path.GetDirectoryName(_Document.FilePath)));
		_FileButton.Text.Margin = WpfHelper.SmallHorizontalMargin;

		_Container.AddRange(_FileButton);

		_View = view;
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.EventHandler)]
	void AddProjectButtonOnLoaded(object sender, EventArgs e) {
		_View.VisualElement.Loaded -= AddProjectButtonOnLoaded;
		var dte = ServicesHelper.Instance.DTE;
		// hack: sometimes when the View is ready, but dte.ActiveDocument is somehow null,
		//   we try alternative ways
		var project = dte.ActiveDocument?.ProjectItem?.ContainingProject
			?? GetProjectFromTextView()
			?? dte.Solution.FindProjectItem(_View.TextBuffer.GetTextDocument().FilePath)?.ContainingProject;
		if (project?.IsMiscOrProjectFolder() != false) {
			return;
		}
		_ProjectButton = new ThemedToggleButton(VsImageHelper.GetImageIdForFile(project.UniqueName), R.CMD_GoToProjectFolder, OnProjectClick) {
			Background = Brushes.Transparent,
			BorderThickness = WpfHelper.NoMargin
		}.TinySpacing();
		_ProjectButton.SetText(Path.GetFileNameWithoutExtension(project.UniqueName));
		_ProjectButton.Text.Margin = WpfHelper.SmallHorizontalMargin;
		_Container.Insert(_Container.ControlCount - 1, _ProjectButton);
	}

	void OnSolutionClick(object sender, RoutedEventArgs e) {
		if (_SolutionButton.IsChecked != true) {
			return;
		}
		UncheckToggleButtons(_Container, _SolutionButton);
		CreateFilePopup();
		_FileList.InitCurrentFile();
		_FileList.LoadSolutionDirectoryAsync(SyncHelper.CancelAndRetainToken(ref _CancellationTokenSource)).FireAndForget();
		_FilePopup.IsOpen = true;
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.EventHandler)]
	void OnProjectViewClick(object sender, RoutedEventArgs e) {
		if (_ProjectViewButton.IsChecked != true) {
			return;
		}
		UncheckToggleButtons(_Container, _ProjectViewButton);
		CreateFilePopup();
		_FileList.InitCurrentFile();
		_FileList.ListSolutionAndProjects();
		_FilePopup.IsOpen = true;
	}

	void OnProjectClick(object sender, RoutedEventArgs e) {
		if (_ProjectButton.IsChecked != true) {
			return;
		}
		UncheckToggleButtons(_Container, _ProjectButton);
		CreateFilePopup();
		_FileList.InitCurrentFile();
		_FileList.LoadCurrentProjectDirectoryAsync(SyncHelper.CancelAndRetainToken(ref _CancellationTokenSource)).FireAndForget();
		_FilePopup.IsOpen = true;
	}

	void OnFolderClick(object sender, RoutedEventArgs args) {
		if (_FileButton.IsChecked != true) {
			return;
		}
		UncheckToggleButtons(_Container, _FileButton);
		var path = _View.TextBuffer.GetTextDocument().FilePath;
		var (folder, _) = FileHelper.DeconstructPath(path, true);
		if (String.IsNullOrEmpty(folder)) {
			_FileButton.IsChecked = false;
			return;
		}
		CreateFilePopup();
		_FileList.CurrentFile = path;
		_FileList.LoadCurrentDirectoryAsync(folder, SyncHelper.CancelAndRetainToken(ref _CancellationTokenSource)).FireAndForget();
		_FilePopup.IsOpen = true;
	}

	void CreateFilePopup() {
		if (_FilePopup != null) {
			return;
		}
		_FilePopup = new Popup {
			PlacementTarget = _Container,
			Placement = PlacementMode.Top,
			AllowsTransparency = true,
			StaysOpen = false,
			Focusable = true,
			MaxHeight = 600,
			Child = _FileList = new FileList()
		};
		_FilePopup.Closed += Popup_Closed;
		KeystrokeThief.Bind(_FilePopup);
		_FileList.FileActivated += HandleFileActivation;
	}

	void HandleFileActivation(object sender, EventArgs<FileSystemItem> e) {
		_FilePopup.IsOpen = false;
		if (e.Data.IsCurrent) {
			_View.VisualElement.Focus();
		}
	}

	void HandleDocumentFileActivation(object sender, TextDocumentFileActionEventArgs e) {
		_FileButton.SetText(Path.GetFileNameWithoutExtension(e.FilePath));
	}

	void Popup_Closed(object sender, EventArgs e) {
		_FileButton.IsChecked = false;
		_SolutionButton?.IsChecked = false;
		_ProjectViewButton?.IsChecked = false;
		_ProjectButton?.IsChecked = false;
	}

	static void UncheckToggleButtons(ThemedControlGroup panel, ThemedToggleButton keepButton) {
		foreach (var button in panel.Controls.OfType<ThemedToggleButton>()) {
			if (button != keepButton) {
				button.IsChecked = false;
			}
		}
	}

	public void Dispose() {
		_CancellationTokenSource.CancelAndDispose();
		_Document.FileActionOccurred -= HandleDocumentFileActivation;
		_View.VisualElement.Loaded -= AddProjectButtonOnLoaded;
	}

	ITextViewMargin ITextViewMargin.GetTextViewMargin(string marginName) {
		return marginName == Name ? this : null;
	}

	Project GetProjectFromTextView() {
		return _Document != null
			&& ServicesHelper.Get<IVsRunningDocumentTable, SVsRunningDocumentTable>()
				.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, _Document.FilePath, out var hierarchy, out _, out _, out _) == 0
			? hierarchy.GetExtObjectAs<Project>()
			: null;
	}
}
