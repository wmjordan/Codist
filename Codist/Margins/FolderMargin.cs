using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Codist.Controls;
using Codist.FileBrowser;
using Microsoft.VisualStudio.Text.Editor;
using R = Codist.Properties.Resources;

namespace Codist.Margins;

sealed partial class FolderMargin : IWpfTextViewMargin
{
	internal const string Name = nameof(FolderMargin);

	readonly ThemedControlGroup _Container;
	readonly ThemedToggleButton _FileButton, _SolutionButton, _ProjectViewButton;
	ThemedToggleButton _ProjectButton;
	readonly IWpfTextView _View;
	Popup _FilePopup;
	FileList _FileList;
	CancellationTokenSource _CancellationTokenSource;

	public FrameworkElement VisualElement => _Container;
	public double MarginSize => _FileButton.RenderSize.Height;
	public bool Enabled => true;

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	public FolderMargin(IWpfTextView view) {
		_Container = new ThemedControlGroup { Margin = new Thickness(2), Resources = SharedDictionaryManager.ThemedControls };
		if (!String.IsNullOrEmpty(ServicesHelper.Instance.DTE.Solution.FullName)) {
			_SolutionButton = new ThemedToggleButton(IconIds.GoToSolutionFolder, R.CMD_GoToSolutionFolder, OnSolutionClick) {
				Background = Brushes.Transparent,
				BorderThickness = WpfHelper.NoMargin
			}.TinySpacing();
			_ProjectViewButton = new ThemedToggleButton(IconIds.RelatedProjects, R.CMD_ListProjectFolders, OnProjectViewClick) {
				Background = Brushes.Transparent,
				BorderThickness = WpfHelper.NoMargin
			}.TinySpacing();

			_Container.AddRange(_ProjectViewButton, _SolutionButton);

			_Container.Loaded += AddProjectButtonOnLoaded;
		}
		_FileButton = new ThemedToggleButton(IconIds.Folder, R.CMDT_ClickToViewFolder, OnFolderClick) {
			Background = Brushes.Transparent,
			BorderThickness = WpfHelper.NoMargin
		}.TinySpacing();
		_Container.AddRange(_FileButton);

		_View = view;
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.EventHandler)]
	void AddProjectButtonOnLoaded(object sender, RoutedEventArgs e) {
		_Container.Loaded -= AddProjectButtonOnLoaded;
		var project = ServicesHelper.Instance.DTE.ActiveDocument?.ProjectItem?.ContainingProject;
		if (project?.IsMiscOrProjectFolder() != false) {
			return;
		}
		_ProjectButton = new ThemedToggleButton(VsImageHelper.GetImageIdForFile(project.UniqueName), R.CMD_GoToProjectFolder, OnProjectClick) {
			Background = Brushes.Transparent,
			BorderThickness = WpfHelper.NoMargin
		}.TinySpacing();
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
	}

	ITextViewMargin ITextViewMargin.GetTextViewMargin(string marginName) {
		return marginName == Name ? this : null;
	}

}
