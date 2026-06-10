using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CLR;
using Codist.Controls;
using Codist.FileBrowser;
using EnvDTE;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using R = Codist.Properties.Resources;

namespace Codist.Margins;

sealed class FileBrowserMargin : IWpfTextViewMargin
{
	internal const string Name = nameof(FileBrowserMargin);
	static readonly Thickness __ContainerMargin = CodistPackage.VsVersion.Major < 17 ? WpfHelper.NoMargin : new Thickness(2);

	readonly ThemedControlGroup _Container;
	readonly ThemedToggleButton _ProjectViewButton, _SolutionButton, _FolderButton, _FileButton;
	readonly IWpfTextView _View;
	readonly ITextDocument _Document;
	ThemedToggleButton _ProjectButton;
	Popup _FilePopup;
	FileList _FileList;
	CancellationTokenSource _CancellationTokenSource;

	public FrameworkElement VisualElement => _Container;
	public double MarginSize => _FolderButton.RenderSize.Height;
	public bool Enabled => true;

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	public FileBrowserMargin(IWpfTextView view) {
		string path;
		_Container = new ThemedControlGroup {
			Margin = __ContainerMargin,
			Resources = SharedDictionaryManager.ThemedControls
		};
		if (!String.IsNullOrEmpty(path = ServicesHelper.Instance.DTE.Solution.FullName)) {
			_ProjectViewButton = new ThemedToggleButton(IconIds.GoToSolutionProjects, R.CMD_ListProjectFolders, OnProjectViewClick) {
				Background = Brushes.Transparent,
				BorderThickness = WpfHelper.NoMargin
			}.TinySpacing();
			_ProjectViewButton.SetText("\\");
			_ProjectViewButton.Text.Margin = WpfHelper.SmallHorizontalMargin;

			_SolutionButton = new ThemedToggleButton(IconIds.GoToSolutionFolder, R.CMD_GoToSolutionFolder, OnSolutionClick) {
				Background = Brushes.Transparent,
				BorderThickness = WpfHelper.NoMargin
			}.TinySpacing();
			_SolutionButton.SetText(Path.GetFileNameWithoutExtension(path));
			_SolutionButton.Text.Margin = WpfHelper.SmallHorizontalMargin;

			_Container.AddRange(_ProjectViewButton, _SolutionButton);

			view.VisualElement.Loaded += AddProjectButtonOnLoaded;
		}

		_Document = view.TextBuffer.GetTextDocument();
		_Document.FileActionOccurred += HandleDocumentFileActivation;

		var (dir, file) = FileHelper.DeconstructPath(_Document.FilePath, true);
		_FolderButton = new ThemedToggleButton(IconIds.Folder, R.CMDT_ViewCurrentFolder, OnFolderClick) {
			Background = Brushes.Transparent,
			BorderThickness = WpfHelper.NoMargin
		}.TinySpacing();
		_FolderButton.SetText(Path.GetFileName(dir));
		_FolderButton.Text.Margin = WpfHelper.SmallHorizontalMargin;

		_FileButton = new ThemedToggleButton(VsImageHelper.GetImageIdForFile(file), R.CMDT_ListOpenDocuments, OnFileClick) {
			Background = Brushes.Transparent,
			BorderThickness = WpfHelper.NoMargin
		}.TinySpacing();
		_FileButton.SetText(file);
		_FileButton.Text.Margin = WpfHelper.SmallHorizontalMargin;

		_Container.AddRange(_FolderButton, _FileButton);

		_View = view;

		Config.RegisterUpdateHandler(HandleConfigUpdate);
		// apply config
		HandleConfigUpdate(new(Config.Instance, Features.FileBrowser));
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
		_Container.Insert(_Container.ControlCount - 2, _ProjectButton);
		_ProjectButton.Text.ToggleVisibility(Config.Instance.FileBrowserOptions.MatchFlags(FileBrowserOptions.ShowLabels));
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.EventHandler)]
	void OnSolutionClick(object sender, RoutedEventArgs e) {
		if (_SolutionButton.IsChecked != true) {
			return;
		}
		UncheckToggleButtons(_Container, _SolutionButton);
		CreateFilePopup();
		_FileList.InitCurrentFile();
		_FileList.LoadSolutionDirectoryAsync(SyncHelper.CancelAndRetainToken(ref _CancellationTokenSource)).FireAndForget();
		_FilePopup.PlacementTarget = _SolutionButton;
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
		_FilePopup.PlacementTarget = _ProjectViewButton;
		_FilePopup.IsOpen = true;
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.EventHandler)]
	void OnProjectClick(object sender, RoutedEventArgs e) {
		if (_ProjectButton.IsChecked != true) {
			return;
		}
		UncheckToggleButtons(_Container, _ProjectButton);
		CreateFilePopup();
		_FileList.InitCurrentFile();
		_FileList.LoadCurrentProjectDirectoryAsync(SyncHelper.CancelAndRetainToken(ref _CancellationTokenSource)).FireAndForget();
		_FilePopup.PlacementTarget = _ProjectButton;
		_FilePopup.IsOpen = true;
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.EventHandler)]
	void OnFolderClick(object sender, RoutedEventArgs args) {
		if (_FolderButton.IsChecked != true) {
			return;
		}
		UncheckToggleButtons(_Container, _FolderButton);
		var path = _View.TextBuffer.GetTextDocument().FilePath;
		var (folder, _) = FileHelper.DeconstructPath(path, true);
		if (String.IsNullOrEmpty(folder)) {
			_FolderButton.IsChecked = false;
			return;
		}
		CreateFilePopup();
		_FileList.CurrentFile = path;
		_FileList.LoadCurrentDirectoryAsync(folder, SyncHelper.CancelAndRetainToken(ref _CancellationTokenSource)).FireAndForget();
		_FilePopup.PlacementTarget = _FolderButton;
		_FilePopup.IsOpen = true;
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.EventHandler)]
	void OnFileClick(object sender, RoutedEventArgs args) {
		if (_FileButton.IsChecked != true) {
			return;
		}
		UncheckToggleButtons(_Container, _FileButton);
		CreateFilePopup();
		_FileList.InitCurrentFile();
		_FileList.ListOpenedDocuments();
		_FilePopup.PlacementTarget = _FileButton;
		_FilePopup.IsOpen = true;
	}

	void MakeTitleBar(string title) {
		_FileList.Header = new Border {
			Child = new ThemedMenuText(title) {
				Margin = WpfHelper.MiddleHorizontalMargin,
				FontWeight = FontWeights.Bold
			},
			BorderThickness = WpfHelper.TinyBottomMargin,
			Padding = WpfHelper.SmallVerticalMargin
		}.ReferenceProperty(Border.BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey);
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
		_FileList.LocationTypeChanged += HandleLocationTypeChanged;
		_FileList.KeyUp += HandleFileListKeyUp;
	}

	void HandleFileActivation(object sender, EventArgs<FileItem> e) {
		_FilePopup.IsOpen = false;
		if (e.Data.IsCurrent) {
			_View.VisualElement.Focus();
		}
	}

	void HandleDocumentFileActivation(object sender, TextDocumentFileActionEventArgs e) {
		var (dir, file) = FileHelper.DeconstructPath(e.FilePath, true);
		_FolderButton.SetText(Path.GetFileName(dir));
		_FileButton.SetText(file);
	}

	void HandleLocationTypeChanged(object sender, EventArgs<FileListLocationType> e) {
		MakeTitleBar(e.Data switch {
			FileListLocationType.CurrentDocumentFolder => R.T_CurrentDocumentFolder,
			FileListLocationType.CurrentProjectFolder => R.T_CurrentProjectFolder,
			FileListLocationType.SolutionFolder => R.T_SolutionFolder,
			FileListLocationType.SolutionProjects => R.T_SolutionProjects,
			FileListLocationType.OpenedDocuments => R.T_OpenedDocuments,
			_ => R.T_FolderContent
		});
	}

	void HandleFileListKeyUp(object sender, KeyEventArgs e) {
		if (e.Key == Key.Escape) {
			_FilePopup.IsOpen = false;
			_View.VisualElement.Focus();
		}
	}

	void Popup_Closed(object sender, EventArgs e) {
		_FolderButton.IsChecked = _FileButton.IsChecked = false;
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

	void HandleConfigUpdate(ConfigUpdatedEventArgs args) {
		if (!args.UpdatedFeature.MatchFlags(Features.FileBrowser)) {
			return;
		}

		var options = args.Config.FileBrowserOptions;
		if (!args.Config.Features.MatchFlags(Features.FileBrowser)
			|| !options.HasAnyFlag(FileBrowserOptions.AllButtons)) {
			_Container.ToggleVisibility(false);
			return;
		}
		var hasControl = false;
		ToggleButton(_ProjectViewButton, options, FileBrowserOptions.ShowSolutionProjects, ref hasControl);
		ToggleButton(_SolutionButton, options, FileBrowserOptions.ShowSolutionFolder, ref hasControl);
		ToggleButton(_ProjectButton, options, FileBrowserOptions.ShowCurrentProjectFolder, ref hasControl);
		ToggleButton(_FolderButton, options, FileBrowserOptions.ShowCurrentDocumentFolder, ref hasControl);
		ToggleButton(_FileButton, options, FileBrowserOptions.ShowOpenedDocuments, ref hasControl);
		_Container.ToggleVisibility(hasControl);

		var showLabels = options.MatchFlags(FileBrowserOptions.ShowLabels);
		_ProjectViewButton?.Text.ToggleVisibility(showLabels);
		_SolutionButton?.Text.ToggleVisibility(showLabels);
		_ProjectButton?.Text.ToggleVisibility(showLabels);
		_FolderButton.Text.ToggleVisibility(showLabels);
		_FileButton.Text.ToggleVisibility(showLabels);

		void ToggleButton(UIElement b, FileBrowserOptions opts, FileBrowserOptions opt, ref bool c) {
			if (b is null) {
				return;
			}
			if (opts.MatchFlags(opt)) {
				b.Visibility = Visibility.Visible;
				c = true;
			}
			else {
				b.Visibility = Visibility.Collapsed;
			}
		}
	}

	public void Dispose() {
		_CancellationTokenSource.CancelAndDispose();
		_Document.FileActionOccurred -= HandleDocumentFileActivation;
		_View.VisualElement.Loaded -= AddProjectButtonOnLoaded;
		_FilePopup?.IsOpen = false;
		Config.UnregisterUpdateHandler(HandleConfigUpdate);
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
