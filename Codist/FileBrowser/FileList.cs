using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using CLR;
using Codist.Controls;
using EnvDTE;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using R = Codist.Properties.Resources;
using Task = System.Threading.Tasks.Task;

namespace Codist.FileBrowser;

sealed partial class FileList : VirtualList
{
	readonly TextBlock _PathBlock, _SelectionInfoBlock;
	readonly TextBox _FilterBox;
	readonly ThemedControlGroup _FilterGroup;
	readonly ThemedMenuButton _SelectionMenuButton;
	readonly ThemedToggleButton _FolderFilterButton, _FileFilterButton;
	readonly ThemedButton _BackButton, _GoToCurrentFileButton, _GoToSolutionFolderButton, _GoToProjectFolderButton;
	readonly ContextMenu _FileMenu, _DocumentMenu;
	readonly Grid _PathControl;
	readonly ViewItemList _ViewHistories;
	readonly bool _InWindowPane;

	ObservableCollection<FileItem> _Items;
	ICollectionView _ItemsView;
	bool _LockFilter, _TrackActiveFile;
	ViewMode _ViewMode;
	FileListLocationType _LocationType;
	ViewItem _CurrentView;

	string _ActiveFilePath, _ActiveDirPath, _SolutionFolderPath, _ProjectFolderPath;
	int _ProjectIconId;

	public FileList(bool inWindowPane = false) {
		MaxWidth = 600;
		BorderThickness = WpfHelper.NoMargin;
		Focusable = true;
		this.ReferenceStyle(typeof(VirtualList))
			.ReferenceProperty(BackgroundProperty, CommonControlsColors.ComboBoxListBackgroundBrushKey)
			.ReferenceProperty(BorderBrushProperty, CommonControlsColors.ComboBoxListBorderBrushKey);

		ContextMenu m;
		ItemTemplate = SharedDictionaryManager.VirtualList.Get<DataTemplate>("FileItemTemplate");
		ItemContainerStyle = new Style(typeof(ListBoxItem)) {
			Setters = {
				new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
				new Setter(PaddingProperty, WpfHelper.TinyMargin),
				new Setter(MaxWidthProperty, 550d),
				new Setter(ToolTipService.ToolTipProperty, new Binding {
					Converter = new FileItemToTooltipConverter(this)
				}),
				new Setter(ToolTipService.PlacementProperty, PlacementMode.Right),
				new Setter(ToolTipService.ShowDurationProperty, 30000),
				new Setter(ToolTipService.InitialShowDelayProperty, 500),
			},
		};
		SelectionMode = SelectionMode.Extended;
		MouseUp += HandleMouseUp;
		#region Extra controls
		_PathControl = new Grid {
			ColumnDefinitions = {
				new ColumnDefinition { Width = GridLength.Auto },
				new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
				new ColumnDefinition { Width = GridLength.Auto }
			},
			Children = {
				new Border {
					BorderThickness = WpfHelper.TinyMargin,
					Margin = WpfHelper.SmallMargin,
					CornerRadius = WpfHelper.SmallCorner,
					Child = new ThemedMenuButton(IconIds.OpenFolder, R.CMD_OpenFolder, ShowFolderMenu)
						.ClearSpacing()
						.SetProperty(PaddingProperty, WpfHelper.SmallMargin),
					VerticalAlignment = VerticalAlignment.Top,
				}.ReferenceProperty(Border.BorderBrushProperty, CommonControlsColors.TextBoxBorderBrushKey),
				new TextBlock {
					Padding = WpfHelper.SmallMargin,
					VerticalAlignment = VerticalAlignment.Center,
					TextWrapping = TextWrapping.Wrap,
				}.SetValue(Grid.SetColumn, 1)
				.ReferenceProperty(TextBlock.ForegroundProperty, EnvironmentColors.SystemCaptionTextBrushKey)
				.Set(ref _PathBlock)
			}
		};
		var toolbar = new StackPanel {
			Orientation = Orientation.Horizontal,
			Margin = WpfHelper.SmallMargin,
			Children = {
				new ThemedControlGroup(
					_SelectionMenuButton = new ThemedMenuButton(IconIds.SelectionMenu, R.T_SelectionMenu, ShowSelectionMenu, ConfigSelectionMenu),
					_BackButton = new ThemedButton(IconIds.GoBack, R.CMD_NavigateBackward, NavigateBackward){ IsEnabled = false }
					) {
					Margin = WpfHelper.GlyphMargin,
					VerticalAlignment = VerticalAlignment.Center,
				},
				new ThemedControlGroup(
					_FolderFilterButton = new ThemedToggleButton(IconIds.Folder, R.T_Folder, HandleFolderFileFilterChange),
					_FileFilterButton = new ThemedToggleButton(IconIds.File, R.T_File, HandleFolderFileFilterChange)
				) {
					Margin = WpfHelper.GlyphMargin,
					VerticalAlignment = VerticalAlignment.Center,
				}
				.Set(ref _FilterGroup),
				new ThemedTextBox {
					MinWidth = 120,
					ToolTip = new ThemedToolTip(R.T_ResultFilter, R.T_ResultFilterTip),
				}.Set(ref _FilterBox),
				new ThemedControlGroup(
					new ThemedButton(IconIds.ClearFilter, R.CMD_ClearFilter, ClearFilterBox),
					new ThemedButton(IconIds.GoToCurrentFile, R.CMD_BackToCurrentFile, GoToCurrentFile).Set(ref _GoToCurrentFileButton),
					new ThemedButton(IconIds.GoToSolutionFolder, R.CMD_GoToSolutionFolder, GoToSolutionFolder).Set(ref _GoToSolutionFolderButton),
					new ThemedButton(IconIds.GoToProjectFolder, R.CMD_GoToProjectFolder, GoToProjectFolder).Set(ref _GoToProjectFolderButton)
					) {
					Margin = WpfHelper.GlyphMargin,
					VerticalAlignment = VerticalAlignment.Center,
				},
				new TextBlock {
					VerticalAlignment = VerticalAlignment.Center,
				}.Set(ref _SelectionInfoBlock)
			}
		};
		var commandPanel = new StackPanel {
			Children = { _PathControl, toolbar }
		};
		if (_InWindowPane = inWindowPane) {
			Header = commandPanel;
			((ThemedControlGroup)toolbar.Children[0])
				.AddRange(new ThemedToggleButton(IconIds.SyncActiveFile, R.CMDT_SyncActiveFile, ToggleSyncMode));
		}
		else {
			Footer = commandPanel;
		}
		#endregion
		ContextMenu = m = _FileMenu = new() {
			PlacementTarget = this,
			Resources = SharedDictionaryManager.ContextMenu,
		};

		this.ReferenceCrispImageBackground(CommonControlsColors.ComboBoxListBackgroundColorKey)
			.ReferenceProperty(ForegroundProperty, CommonControlsColors.ComboBoxListItemTextBrushKey);

		m.SetBackgroundForCrispImage(ThemeCache.TitleBackgroundColor);

		UpdateSolutionFolderPath();

		_GoToCurrentFileButton.Visibility = Visibility.Collapsed;
		_FilterBox.TextChanged += FilterBox_TextChanged;
		_FilterBox.Loaded += FilterBox_Loaded;

		_DocumentMenu = new() {
			PlacementTarget = this,
			Resources = SharedDictionaryManager.ContextMenu,
		};
		_ViewHistories = new(this);
	}

	public string CurrentFile {
		get => _ActiveFilePath;
		set {
			ThreadHelper.ThrowIfNotOnUIThread();
			_ActiveFilePath = value;
			UpdateProjectFolderPath(value);
		}
	}

	public IEnumerable<string> SelectedFileNames {
		get {
			foreach (var item in SelectedItems) {
				if (item is FileItem fs && fs.IsFile) {
					yield return fs.Name;
				}
			}
		}
	}
	public IEnumerable<string> SelectedFilePaths {
		get {
			foreach (var item in SelectedItems) {
				if (item is FileItem fs && fs.IsFile) {
					yield return fs.FullPath;
				}
			}
		}
	}
	public IEnumerable<string> SelectedNames {
		get {
			foreach (var item in SelectedItems) {
				if (item is FileItem fs) {
					yield return fs.Name;
				}
			}
		}
	}
	public IEnumerable<string> SelectedPaths {
		get {
			foreach (var item in SelectedItems) {
				if (item is FileItem fs) {
					yield return fs.FullPath;
				}
			}
		}
	}
	public FileListLocationType LocationType {
		get => _LocationType;
		set {
			if (_LocationType != value) {
				_LocationType = value;
				LocationTypeChanged?.Invoke(this, new(value));
			}
		}
	}

	public event EventHandler<EventArgs<FileItem>> FileActivated;

	public event EventHandler<EventArgs<FileListLocationType>> LocationTypeChanged;

	#region Event handlers
	protected override void OnContextMenuOpening(ContextMenuEventArgs e) {
		if (ContextMenu is null
			|| (e.OriginalSource as UIElement).GetParent<ListBoxItem>() is null) {
			e.Handled = true;
			return;
		}
		base.OnContextMenuOpening(e);
		if (!ContextMenu.HasItems) {
			if (ContextMenu == _FileMenu) {
				_FileMenu.Items.AddRange(
					new ListItemContextMenuItem(IconIds.OpenWithVisualStudio, R.CMD_OpenWithVS, ActivationCondition.HasFile, OpenFilesWithVisualStudio),
					new ListItemContextMenuItem(IconIds.LocateInSolutionExplorer, R.CMD_LocateInSolutionExplorer, ActivationCondition.HasSingleSolutionItem, LocateInSolutionExplorer),
					new ListItemContextMenuItem(IconIds.Open, R.CMD_OpenOrExecuteFile, ActivationCondition.HasFile | ActivationCondition.HasSingleItem, OpenOrExecuteFile),
					new Separator(),
					new ListItemContextMenuItem(IconIds.Cut, R.CMD_Cut, ActivationCondition.HasFileOrFolder, CutFiles),
					new ListItemContextMenuItem(IconIds.Copy, R.CMD_Copy, ActivationCondition.HasFileOrFolder, CopyFiles),
					new ListItemContextMenuItem(IconIds.Paste, R.CMD_Paste, ActivationCondition.HasClipboardFile, PasteFiles),
					new Separator(),
					new ListItemContextMenuItem(IconIds.Delete, R.CMD_Delete, ActivationCondition.HasFileOrFolder, DeleteFiles),
					new Separator(),
					new ListItemContextMenuItem(IconIds.Rename, R.CMD_Rename, ActivationCondition.HasSingleItem, StartRename),
					new ListItemContextMenuItem(IconIds.Properties, R.CMD_Properties, ActivationCondition.HasFileOrFolder, ShowProperties)
				);
			}
			else if (ContextMenu == _DocumentMenu) {
				_DocumentMenu.Items.AddRange(
					new ListItemContextMenuItem(IconIds.OpenFolder, R.CMD_OpenFolder, ActivationCondition.HasFile, OpenInExplorer),
					new ListItemContextMenuItem(IconIds.Folder, R.CMD_ViewFolderInFileBrowser, ActivationCondition.HasFile, LocateInFileBrowser),
					new ListItemContextMenuItem(IconIds.LocateInSolutionExplorer, R.CMD_LocateInSolutionExplorer, ActivationCondition.HasSingleSolutionItem, LocateInSolutionExplorer),
					new ListItemContextMenuItem(IconIds.Open, R.CMD_OpenOrExecuteFile, ActivationCondition.HasFile | ActivationCondition.HasSingleItem, OpenOrExecuteFile),
					// note: disabled due to a problem when saving untitled document
					//new ListItemContextMenuItem(IconIds.Save, R.CMD_Save, ActivationCondition.HasFile, SaveDocument),
					new ListItemContextMenuItem(IconIds.Close, R.CMD_Close, ActivationCondition.HasFile, CloseDocument),
					new Separator(),
					//new ListItemContextMenuItem(IconIds.SaveAll, R.CMD_SaveAll, ActivationCondition.HasFile, SaveAllDocuments),
					new ListItemContextMenuItem(IconIds.CloseAll, R.CMD_CloseOtherSaved, ActivationCondition.HasFile, CloseOtherSavedDocuments)
				);
			}
		}
		ActivationCondition condition = default;
		if (SelectedItem != null) {
			var selected = SelectedItems;
			if (((FileItem)selected[0]).IsFolder) {
				condition |= ActivationCondition.HasFolder;
			}
			if (((FileItem)selected[selected.Count - 1]).IsFile) {
				condition |= ActivationCondition.HasFile;
			}
			if (selected.Count == 1
				&& ((FileItem)SelectedItem).Type != FileItemType.InaccessibleFolder) {
				condition |= ActivationCondition.HasSingleItem;
			}
			if (condition.MatchFlags(ActivationCondition.HasSingleItem | ActivationCondition.HasFile)
				&& _SolutionFolderPath.Length != 0
				&& ((FileItem)SelectedItem).IsSolutionItem) {
				condition |= ActivationCondition.HasSingleSolutionItem;
			}
		}
		if (Clipboard.ContainsFileDropList()) {
			condition |= ActivationCondition.HasClipboardFile;
		}
		foreach (var item in ContextMenu.Items) {
			if (item is ListItemContextMenuItem menuItem) {
				menuItem.IsEnabled = menuItem.Condition.HasAnyFlag(condition);
			}
		}
	}

	void HandleFolderFileFilterChange(object sender, RoutedEventArgs e) {
		if (_LockFilter) {
			return;
		}
		var s = (ThemedToggleButton)sender;
		if (s.IsChecked == true) {
			_LockFilter = true;
			if (s == _FolderFilterButton) {
				_FileFilterButton.IsChecked = false;
			}
			else {
				_FolderFilterButton.IsChecked = false;
			}
			_LockFilter = false;
		}
		ApplyFilter();
	}

	void FilterBox_Loaded(object sender, RoutedEventArgs e) {
		_FilterBox.Focus();
	}

	void FilterBox_TextChanged(object sender, TextChangedEventArgs e) {
		if (!_LockFilter) {
			ApplyFilter();
		}
	}

	protected override void OnKeyUp(KeyEventArgs e) {
		if (e.Key == Key.Enter) {
			ActivateSelectedItem();
			e.Handled = true;
		}
		else {
			base.OnKeyUp(e);
		}
	}

	void HandleMouseUp(object sender, MouseButtonEventArgs e) {
		switch (e.ChangedButton) {
			case MouseButton.Left:
				if (!UIHelper.IsCtrlDown
					&& !UIHelper.IsShiftDown
					&& e.OriginalSource is UIElement u
					&& u.FindAncestor<ListBoxItem>() != null) {
					ActivateSelectedItem();
					e.Handled = true;
				}
				break;
			case MouseButton.XButton1:
				NavigateBackward(this, EventArgs.Empty);
				e.Handled = true;
				break;
		}
	}

	void HandleMouseDoubleClick(object sender, MouseButtonEventArgs e) {
		if (e.ChangedButton == MouseButton.Left
			&& e.OriginalSource is UIElement u
			&& u.FindAncestor<ListBoxItem>() != null) {
			ActivateSelectedItem();
			e.Handled = true;
		}
	}

	void NavigateBackward(object sender, EventArgs e) {
		if (_ViewHistories.TryPop(out var history)) {
			// prevent current view from added back to history
			_CurrentView = default;
			NavigateToView(history);
		}
	}

	void NavigateToView(ViewItem history) {
		LocationType = history.LocationType;
		switch (history.Mode) {
			case ViewMode.File:
				NavigateToDirectoryAsync(history.Path, default).FireAndForget();
				break;
			case ViewMode.SolutionProjects:
				ListSolutionAndProjects();
				break;
			case ViewMode.Documents:
				ListOpenedDocuments();
				break;
		}
	}

	void ShowFolderMenu(ContextMenu menu) {
		menu.Placement = PlacementMode.Bottom;
		menu.Items.AddRange(
			new ThemedMenuItem(IconIds.OpenFolder, R.CMD_OpenFolder, OpenInExplorer),
			new ThemedMenuItem(IconIds.OpenWithCmd, R.CMD_OpenFolderWithCmd, OpenFolderInCmd)
		);
		if (!_InWindowPane) {
			menu.Items.AddRange(
				new Separator(),
				new ThemedMenuItem(IconIds.Folder, R.CMD_ViewFolderInFileBrowser, LocateInFileBrowser)
			);
		}
	}

	void ShowSelectionMenu(ContextMenu menu) {
		menu.Placement = PlacementMode.Bottom;
		menu.Items.AddRange(
			new ThemedMenuItem(IconIds.MultiSelection, R.CMD_ToggleMultiSelectionMode, ToggleMultiSelectionMode, R.CMDT_ToggleMultiSelectionMode),
			new ThemedMenuItem(IconIds.SelectAll, R.CMD_SelectAll, HandleSelectAll),
			new ThemedMenuItem(IconIds.None, R.CMD_SelectNone, HandleSelectNone)
		);
	}

	void ConfigSelectionMenu(ContextMenu menu) {
		((ThemedMenuItem)menu.Items[0]).Icon = VsImageHelper.GetImage(SelectionMode == SelectionMode.Multiple ? IconIds.Enabled : IconIds.Default);
		foreach (FrameworkElement item in menu.Items) {
			if (item.Tag is ViewMode m) {
				item.ToggleVisibility(m == _ViewMode);
			}
		}
	}

	void HandleSelectionMenuClosed(object sender, EventArgs e) {
		(((ContextMenu)sender).PlacementTarget as ThemedToggleButton)?.IsChecked = false;
	}

	protected override void OnSelectionChanged(SelectionChangedEventArgs e) {
		base.OnSelectionChanged(e);
		_SelectionInfoBlock.Inlines.Clear();
		var c = SelectedItems.Count;
		if (c > 1) {
			_SelectionInfoBlock.AddImage(IconIds.FileLocations).Append(c);
		}
	}
	#endregion

	public void InitCurrentFile() {
		ThreadHelper.ThrowIfNotOnUIThread();
		if (_ActiveFilePath != null) {
			return;
		}
		var doc = ServicesHelper.Instance.DTE.ActiveDocument;
		_ActiveFilePath = doc.FullName;
		UpdateProjectStatus(doc.ProjectItem.ContainingProject);
	}

	public void ListSolutionAndProjects() {
		ThreadHelper.ThrowIfNotOnUIThread();
		SetViewMode(ViewMode.SolutionProjects);
		_ViewHistories.Push(new(ViewMode.SolutionProjects, FileListLocationType.SolutionProjects, null));
		var solution = ServicesHelper.Instance.DTE.Solution;
		var projects = solution.Projects;
		_Items?.Clear();
		var solutionPath = solution.FullName;
		var items = new List<FileItem>(projects.Count + 1);
		if (Directory.Exists(solutionPath)) {
			_SolutionFolderPath = solutionPath;
			items.Add(new(new DirectoryInfo(solutionPath), FileItemType.Solution, true));
		}
		else {
			(_SolutionFolderPath, _) = FileHelper.DeconstructPath(solutionPath, true);
			items.Add(new(new FileInfo(solutionPath), FileItemType.Solution, false));
		}
		var current = ServicesHelper.Instance.DTE.ActiveDocument?.ProjectItem?.ContainingProject?.UniqueName;
		foreach (EnvDTE.Project project in projects) {
			AddProject(project, items, current);
		}
		SetItems(items);
		LocationType = FileListLocationType.SolutionProjects;
	}

	public void ListOpenedDocuments() {
		ThreadHelper.ThrowIfNotOnUIThread();
		SetViewMode(ViewMode.Documents);
		_ViewHistories.Push(new(ViewMode.Documents, FileListLocationType.OpenedDocuments, null));

		var currentFrame = VsShellHelper.GetCurrentWindowFrame();
		RunningDocumentTable t = new();
		List<FileItem> items = [];
		HashSet<string> openedFiles = RecentlyClosedFileCollection.HasItem ? new() : null;
		foreach (var frame in VsShellHelper.GetDocumentWindows()) {
			if (!frame.TryGetProperty(__VSFPROPID.VSFPROPID_pszMkDocument, out string fullPath)) {
				continue;
			}
			FileItem item = new(new FileInfo(fullPath),
				FileItemType.OpenedDocument,
				currentFrame == frame, // is active
				frame.GetProperty<string>(__VSFPROPID.VSFPROPID_Caption));
			Chain<int> extIcons = [];
			if (frame.GetProperty<bool>((int)__VSFPROPID5.VSFPROPID_IsPinned)) {
				item.FileState = FileState.Pinned;
				extIcons.Add(IconIds.Pin);
			}
			if (frame.TryGetDocCookie(out var cookie)) {
				item.SetStateFromRunningDocumentInfo(t.GetDocumentInfo((uint)cookie), extIcons);
			}
			if (!extIcons.IsEmpty) {
				var p = new StackPanel { Orientation = Orientation.Horizontal };
				foreach (var id in extIcons) {
					p.Children.Add(VsImageHelper.GetImage(id, 14).UseGrayscaleIcon(true));
				}
				item.Note = p;
			}
			items.Add(item);
			openedFiles?.Add(fullPath);
		}
		items.Sort((x, y) => String.Compare(x.Name, y.Name, true));
		#region add recently closed files
		if (openedFiles != null) {
			items.InsertRange(0,
				RecentlyClosedFileCollection.Items
					.Where(i => !openedFiles.Contains(i))
					.Take(Config.Instance.FileBrowser.ListRecentClosedFiles)
					.Select(i => new FileItem(new FileInfo(i), FileItemType.File, false) {
						FileState = FileState.RecentlyClosed,
						Note = VsImageHelper.GetImage(IconIds.FileClosed, 14)
					})
				);
		}
		#endregion

		SetItems(items);
		LocationType = FileListLocationType.OpenedDocuments;
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	void AddProjectItems(EnvDTE.ProjectItems projectItems, List<FileItem> items, string current) {
		foreach (var item in projectItems) {
			var project = (item as EnvDTE.ProjectItem)?.SubProject;
			if (project != null) {
				AddProject(project, items, current);
			}
		}
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	void AddProject(Project project, List<FileItem> items, string current) {
		FileItemType type;
		bool isCurrent;
		string path;
		switch (project.Kind) {
			case VsShellHelper.MiscKind:
				return;
			case VsShellHelper.ProjectFolderKind:
				AddProjectItems(project.ProjectItems, items, current);
				return;
			case VsShellHelper.UnloadedProjectKind:
				type = FileItemType.UnloadedProject;
				path = project.UniqueName;
				isCurrent = false;
				break;
			default:
				type = FileItemType.Project;
				path = project.UniqueName;
				isCurrent = FileHelper.AreFileNamesEqual(path, current);
				break;
		}
		if (String.IsNullOrEmpty(path)) {
			return;
		}
		items.Add(new(new FileInfo(Path.Combine(_SolutionFolderPath, path)), type, isCurrent));
	}

	void SetViewMode(ViewMode viewMode) {
		if (_ViewMode == viewMode) {
			return;
		}
		switch (_ViewMode = viewMode) {
			case ViewMode.File:
				_PathControl.Visibility
					= _SelectionMenuButton.Visibility
					= _FilterGroup.Visibility
					= Visibility.Visible;
				ContextMenu = _FileMenu;
				break;
			case ViewMode.SolutionProjects:
				_PathControl.Visibility = Visibility.Collapsed;
				_GoToCurrentFileButton.ToggleVisibility(_ActiveFilePath != null);
				_SelectionMenuButton.Visibility
					= _GoToSolutionFolderButton.Visibility
					= _GoToProjectFolderButton.Visibility
					= _FilterGroup.Visibility
					= Visibility.Collapsed;
				ContextMenu = null;
				break;
			case ViewMode.Documents:
				_GoToCurrentFileButton.Visibility = _SelectionMenuButton.Visibility = Visibility.Visible;
				_PathControl.Visibility = _FilterGroup.Visibility = Visibility.Collapsed;
				ContextMenu = _DocumentMenu;
				break;
		}
	}

	void SetItems(List<FileItem> items) {
		_Items = new ObservableCollection<FileItem>(items);
		ItemsSource = _ItemsView = CollectionViewSource.GetDefaultView(_Items);
		var highlightItem = items.FirstOrDefault(i => i.IsCurrent);
		ApplyFilter();
		if (highlightItem != null) {
			SelectedItem = highlightItem;
			this.ScrollToSelectedItem();
		}
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	void UpdateSolutionFolderPath() {
		_SolutionFolderPath = ServicesHelper.Instance.DTE.Solution.FullName;
		SetFolderShortcut(_GoToSolutionFolderButton, ref _SolutionFolderPath);
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	void UpdateProjectFolderPath(string value) {
		if (String.IsNullOrEmpty(value)) {
			_ProjectFolderPath = String.Empty;
			_GoToProjectFolderButton.Visibility = Visibility.Collapsed;
			return;
		}
		UpdateProjectStatus(ServicesHelper.Instance.DTE.ActiveDocument?.ProjectItem?.ContainingProject);
	}

	void UpdateProjectStatus(EnvDTE.Project project) {
		ThreadHelper.ThrowIfNotOnUIThread();
		string projectPath;
		if (project == null
			|| String.IsNullOrEmpty(projectPath = project.FullName)) {
			_GoToProjectFolderButton.Visibility = Visibility.Collapsed;
			_ProjectFolderPath = String.Empty;
			return;
		}

		_ProjectFolderPath = projectPath;
		var icon = VsImageHelper.GetImageIdForFile(projectPath);
		if (icon == IconIds.OtherFile) {
			icon = IconIds.GoToProjectFolder;
		}
		_ProjectIconId = icon;
		_GoToProjectFolderButton.Content = VsImageHelper.GetImage(icon);
		SetFolderShortcut(_GoToProjectFolderButton, ref _ProjectFolderPath);
	}

	static void SetFolderShortcut(ThemedButton shortcutButton, ref string filePath) {
		if (String.IsNullOrEmpty(filePath)) {
			shortcutButton.Visibility = Visibility.Collapsed;
			filePath ??= String.Empty;
		}
		else {
			shortcutButton.Visibility = Visibility.Visible;
			filePath = Directory.Exists(filePath)
				? filePath // filePath maybe a directory after "Open Folder" command is executed
				: Path.GetDirectoryName(filePath); // filePath is a file
		}
	}

	void ClearFilterBox() {
		ClearFilter();
		ApplyFilter();
	}

	void ClearFilter() {
		_LockFilter = true;
		_FolderFilterButton.IsChecked = _FileFilterButton.IsChecked = false;
		_FilterBox.Clear();
		_LockFilter = false;
	}

	IVsWindowFrame GetWindowFrameForFile(FileItem file) {
		if (file?.IsFile != true) {
			return null;
		}
		var filePath = file.FullPath;
		foreach (var frame in VsShellHelper.GetDocumentWindows()) {
			if (!FileHelper.AreFileNamesEqual(frame.GetDocumentFullPath(), filePath)
				|| frame.GetCaption() != file.Name) {
				continue;
			}
			return frame;
		}
		return null;
	}

	IEnumerable<OpenDocumentId> GetOpenedDocuments() {
		if (_ViewMode != ViewMode.Documents) {
			yield break;
		}
		foreach (var item in Items) {
			if (item is FileItem fi && fi.Type == FileItemType.OpenedDocument) {
				yield return new OpenDocumentId(fi.Name, fi.FullPath);
			}
		}
	}
	IEnumerable<OpenDocumentId> GetSelectedOpenedDocuments() {
		if (_ViewMode != ViewMode.Documents) {
			yield break;
		}
		foreach (var item in SelectedItems) {
			if (item is FileItem fi && fi.Type == FileItemType.OpenedDocument) {
				yield return new OpenDocumentId(fi.Name, fi.FullPath);
			}
		}
	}

	void ActivateSelectedItem() {
		if (SelectedItem is not FileItem item) {
			return;
		}
		switch (item.Type) {
			case FileItemType.File:
				if (item.FileState == FileState.RecentlyClosed) {
					RecentlyClosedFileCollection.Reopen(item.FullPath);
				}
				else {
					TextEditorHelper.OpenFile(item.FullPath, !Config.Instance.FileBrowserOptions.MatchFlags(FileBrowserOptions.UseProvisional), !Config.Instance.FileBrowserOptions.MatchFlags(FileBrowserOptions.UseCodeWindow));
				}
				FileActivated?.Invoke(this, new(item));
				break;
			case FileItemType.Folder:
			case FileItemType.EmptyFolder:
				LocationType = FileListLocationType.Normal;
				UnsafeNavigateToDirectoryAsync(item.FullPath).FireAndForget();
				break;
			case FileItemType.Solution:
				LocationType = FileListLocationType.SolutionFolder;
				UnsafeNavigateToDirectoryAsync(item.IsCurrent ? item.FullPath : Path.GetDirectoryName(item.FullPath)).FireAndForget();
				break;
			case FileItemType.Project:
			case FileItemType.UnloadedProject:
				LocationType = FileListLocationType.Normal;
				UnsafeNavigateToDirectoryAsync(Path.GetDirectoryName(item.FullPath)).FireAndForget();
				break;
			case FileItemType.OpenedDocument:
				ActivateWindow(item);
				break;
		}
	}

	public void ClearNavigationHistory() {
		_ViewHistories.Clear();
		_BackButton.IsEnabled = false;
	}

	public Task NavigateToDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default) {
		return Directory.Exists(directoryPath)
			? UnsafeNavigateToDirectoryAsync(directoryPath, cancellationToken)
			: Task.CompletedTask;
	}

	async Task UnsafeNavigateToDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default) {
		SetCurrentDir(directoryPath);
		SetViewMode(ViewMode.File);
		_ViewHistories.Push(new(ViewMode.File, LocationType, directoryPath));

		await LoadDirectoryAsync(_ActiveDirPath, cancellationToken);
		FileItem fs;
		_GoToCurrentFileButton.ToggleVisibility(_ActiveFilePath != null
			&& ((fs = SelectedItem as FileItem) is null || !fs.IsFile || !fs.IsCurrent));
		ToggleFolderButton(_GoToSolutionFolderButton, _SolutionFolderPath, directoryPath);
		ToggleFolderButton(_GoToProjectFolderButton, _ProjectFolderPath, directoryPath);
		if (_Items.Count != 0 && SelectedIndex < 0) {
			SelectedIndex = 0;
		}
		_FilterBox.Focus();
	}

	void SetCurrentDir(string directoryPath) {
		_ActiveDirPath = directoryPath;
		BuildPathNavigator(directoryPath);
	}

	public Task LoadCurrentDirectoryAsync(CancellationToken cancellationToken = default) {
		if (!File.Exists(_ActiveFilePath)) {
			IsEnabled = false;
			return Task.CompletedTask;
		}
		IsEnabled = true;
		var (folder, _) = FileHelper.DeconstructPath(_ActiveFilePath, true);
		return LoadCurrentDirectoryAsync(folder, cancellationToken);
	}

	public async Task LoadCurrentDirectoryAsync(string directory, CancellationToken cancellationToken = default) {
		SetCurrentDir(directory);
		SetViewMode(ViewMode.File);
		_ViewHistories.Push(new(ViewMode.File, FileListLocationType.CurrentDocumentFolder, directory));
		await LoadDirectoryAsync(_ActiveDirPath, cancellationToken);

		var highlightItem = _Items.FirstOrDefault(i => i.IsCurrent);
		if (highlightItem != null) {
			SelectedItem = highlightItem;
			this.ScrollToSelectedItem();
		}
		_GoToCurrentFileButton.ToggleVisibility(false);
		ToggleFolderButton(_GoToSolutionFolderButton, _SolutionFolderPath, directory);
		LocationType = FileListLocationType.CurrentDocumentFolder;
	}

	public Task LoadSolutionDirectoryAsync(CancellationToken cancellationToken = default) {
		LocationType = FileListLocationType.SolutionFolder;
		return String.IsNullOrEmpty(_SolutionFolderPath)
			? Task.CompletedTask
			: UnsafeNavigateToDirectoryAsync(_SolutionFolderPath, cancellationToken);
	}

	public Task LoadCurrentProjectDirectoryAsync(CancellationToken cancellationToken = default) {
		LocationType = FileListLocationType.CurrentProjectFolder;
		return String.IsNullOrEmpty(_ProjectFolderPath)
			? Task.CompletedTask
			: UnsafeNavigateToDirectoryAsync(_ProjectFolderPath, cancellationToken);
	}

	static void ToggleFolderButton(ThemedButton folderButton, string folderPath, string directory) {
		folderButton.ToggleVisibility(folderPath.Length != 0 && !FileHelper.AreFileNamesEqual(directory, folderPath));
	}

	Task LoadDirectoryAsync(string directoryPath, CancellationToken cancellationToken) {
		_Items?.Clear();
		SelectedIndex = -1;
		if (!Directory.Exists(directoryPath)) {
			MessageWindow.Error(R.T_ErrorInexistentDirectory + Environment.NewLine + directoryPath, R.T_FailedToOpenFolder);
			return Task.CompletedTask;
		}
		return UncheckedLoadDirectoryAsync(directoryPath, cancellationToken);
	}

	async Task UncheckedLoadDirectoryAsync(string directoryPath, CancellationToken cancellationToken) {
		try {
			var (items, folders, files) = GetFileSystemItems(directoryPath, _ActiveFilePath, cancellationToken);
			_FolderFilterButton.SetText(folders.ToText());
			_FileFilterButton.SetText(files.ToText());
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			SetItems(items);
		}
		catch (OperationCanceledException) { }
		catch (Exception ex) {
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			MessageWindow.Error(ex, R.T_FailedToOpenFolder, source: this);
		}
	}

	static (List<FileItem> items, int folders, int files) GetFileSystemItems(string directory, string highlightFilePath, CancellationToken token) {
		if (directory[directory.Length - 1] == ':') {
			directory += "\\";
		}
		var dirInfo = new DirectoryInfo(directory);
		var dirs = dirInfo.GetDirectories();
		var files = dirInfo.GetFiles();
		var items = new List<FileItem>(dirs.Length + files.Length);
		var highlight = GetHighlightName(directory, highlightFilePath);

		foreach (var dir in dirs) {
			if (token.IsCancellationRequested) break;
			try { items.Add(new FileItem(dir, dir.EnumerateFileSystemInfos().Any(), highlight.IsCurrent(dir))); }
			catch (UnauthorizedAccessException) { items.Add(new FileItem(dir, FileItemType.InaccessibleFolder)); }
			catch (SecurityException) { items.Add(new FileItem(dir, FileItemType.InaccessibleFolder)); }
		}

		foreach (var file in files) {
			items.Add(new FileItem(file, highlight.IsCurrent(file)));
		}

		return (items, dirs.Length, files.Length);
	}

	static HighlightCondition GetHighlightName(string directory, string highlightFilePath) {
		if (String.IsNullOrEmpty(highlightFilePath) ||
			!highlightFilePath.StartsWith(directory, StringComparison.OrdinalIgnoreCase)) {
			return default;
		}
		var relativePath = highlightFilePath.Substring(highlightFilePath[directory.Length] == '\\' ? directory.Length + 1 : directory.Length);
		if (String.IsNullOrEmpty(relativePath)) {
			return default;
		}
		int slashIndex = relativePath.IndexOf('\\');
		return slashIndex == -1
			? new HighlightCondition(true, relativePath)
			: new HighlightCondition(false, relativePath.Substring(0, slashIndex));
	}

	void BuildPathNavigator(string path) {
		var pathInlines = _PathBlock.Inlines;
		foreach (var item in pathInlines) {
			if (item is Hyperlink link) {
				link.Click -= OpenFolderLink;
			}
		}
		pathInlines.Clear();

		int length = path.Length;

		if (length == 0) {
			return;
		}

		if (path[length - 1] == '\\') {
			length--;
		}

		int startIndex = 0;
		do {
			int separatorIndex = path.IndexOf('\\', startIndex, length - startIndex);
			// If no separator is found (-1), we are at the last segment (e.g., "System32" in "C:\Windows\System32")
			int segmentEndIndex = separatorIndex == -1 ? length : separatorIndex;
			if (segmentEndIndex == startIndex) {
				break;
			}
			var segment = path.Substring(startIndex, segmentEndIndex - startIndex);

			if (separatorIndex < 0) {
				pathInlines.Add(new Run(segment) { FontWeight = FontWeights.Bold });
				break;
			}

			#region add icons for solution and project folder
			if (_SolutionFolderPath?.Length == separatorIndex && path.StartsWith(_SolutionFolderPath, StringComparison.OrdinalIgnoreCase)) {
				pathInlines.Add(new InlineUIContainer(VsImageHelper.GetImage(IconIds.GoToSolutionFolder)) { BaselineAlignment = BaselineAlignment.Center });
			}
			if (_ProjectFolderPath?.Length == separatorIndex && path.StartsWith(_ProjectFolderPath, StringComparison.OrdinalIgnoreCase)) {
				pathInlines.Add(new InlineUIContainer(VsImageHelper.GetImage(_ProjectIconId)) { BaselineAlignment = BaselineAlignment.Center });
			}
			#endregion

			var link = new Hyperlink(new Run(segment)) {
				CommandParameter = new PathSegment(path, separatorIndex)
			}.SetContentLazyToolTip(l => new CommandToolTip(IconIds.Folder, R.CMD_GoToFolder + "\n" + l.CommandParameter));
			link.SetResourceReference(TextElement.ForegroundProperty, CommonDocumentColors.HyperlinkBrushKey);
			link.Click += OpenFolderLink;
			link.Unloaded += UnloadLink;
			pathInlines.Add(link);

			// Add the separator character "\"
			if (separatorIndex == -1) {
				// No more separators, we've reached the end of the path
				break;
			}
			pathInlines.Add(new Run("\\"));
			startIndex = separatorIndex + 1;
		}
		while (startIndex <= length);
	}

	void OpenFolderLink(object s, EventArgs e) {
		var link = (Hyperlink)s;
		((TextBlock)link.Parent)
			.FindAncestor<FileList>()
			?.UnsafeNavigateToDirectoryAsync(((PathSegment)link.CommandParameter).Text)
			.FireAndForget();
	}

	void UnloadLink(object sender, RoutedEventArgs e) {
		var link = (Hyperlink)sender;
		link.Click -= OpenFolderLink;
		link.Unloaded -= UnloadLink;
	}

	void ApplyFilter() {
		const int ALL = 0, FILES = 1, FOLDERS = 2;
		if (_ItemsView is null) {
			return;
		}
		var keywords = _FilterBox.Text.Split([' '], StringSplitOptions.RemoveEmptyEntries);
		var fs = _FolderFilterButton.IsChecked == true ? FOLDERS
			: _FileFilterButton.IsChecked == true ? FILES
			: ALL;

		if (fs == ALL && keywords.Length == 0) {
			_ItemsView.Filter = null;
			return;
		}

		_ItemsView.Filter = item => {
			if (item is not FileItem fsi) return false;

			var isFile = fsi.IsFile;
			if (fs == FILES && !isFile) return false;
			if (fs == FOLDERS && isFile) return false;

			return fsi.Name.ContainsWords(keywords);
		};

		if (SelectedIndex == -1 && Items.Count != 0) {
			SelectedIndex = 0;
		}
	}

	#region Refresh methods for window mode
	internal Task RefreshSolutionAsync(CancellationToken cancellationToken) {
		if (String.IsNullOrEmpty(_ActiveDirPath)) {
			UpdateSolutionFolderPath();
			return Task.CompletedTask;
		}
		return InternalRefreshSolutionAsync(cancellationToken);
	}

	async Task InternalRefreshSolutionAsync(CancellationToken cancellationToken) {
		await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
		UpdateSolutionFolderPath();
		if (_SolutionFolderPath.Length == 0) { // solution closed
			_ProjectFolderPath = String.Empty;
			foreach (var item in _Items) {
				item.ClearIsSolutionItem();
			}
			ToggleFolderButton(_GoToProjectFolderButton, _ProjectFolderPath, _ActiveDirPath);
		}
		else {
			CurrentFile = ServicesHelper.Instance.DTE.Solution.FullName;
			await LoadCurrentDirectoryAsync(cancellationToken);
		}
		ToggleFolderButton(_GoToSolutionFolderButton, _SolutionFolderPath, _ActiveDirPath);
		BuildPathNavigator(_ActiveDirPath);
	}

	internal async Task ClearCurrentFileAsync(CancellationToken cancellationToken) {
		CurrentFile = null;
		await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
		foreach (var item in _Items) {
			item.ClearIsCurrent();
		}
		_GoToCurrentFileButton.Visibility = Visibility.Collapsed;
	}

	internal Task RefreshProjectAsync(CancellationToken cancellationToken) {
		return String.IsNullOrEmpty(_ActiveDirPath)
			? Task.CompletedTask
			: InternalRefreshProjectAsync(cancellationToken);
	}

	async Task InternalRefreshProjectAsync(CancellationToken cancellationToken) {
		await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
		UpdateProjectFolderPath(_ActiveDirPath);
		ToggleFolderButton(_GoToProjectFolderButton, _ProjectFolderPath, _ActiveDirPath);
		BuildPathNavigator(_ActiveDirPath);
	}

	internal async Task RefreshCurrentFileAsync(bool forceReload, CancellationToken cancellationToken) {
		await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
		var newPath = TextEditorHelper.GetActiveWpfInteractiveView().TextBuffer.GetTextDocument().FilePath;
		FileItem currentItem = null;
		if (!forceReload && _TrackActiveFile) {
			forceReload = !FileHelper.AreFileNamesEqual(CurrentFile, newPath)
				&& _ActiveDirPath != Path.GetDirectoryName(newPath);
		}
		CurrentFile = newPath;
		if (forceReload || _ActiveDirPath is null) {
			_ActiveDirPath ??= Path.GetDirectoryName(CurrentFile);
			BuildPathNavigator(_ActiveDirPath);
			await LoadCurrentDirectoryAsync(cancellationToken);
			currentItem = SelectedItem as FileItem;
		}
		else {
			BuildPathNavigator(_ActiveDirPath);
			var highlight = GetHighlightName(_ActiveDirPath, _ActiveFilePath);
			foreach (var item in _Items) {
				item.RefreshIsSolutionItem();
				if (item.IsCurrent = highlight.IsCurrent(item)) {
					currentItem = item;
				}
			}
		}
		_GoToCurrentFileButton.ToggleVisibility(_ActiveFilePath != null
				&& (currentItem is null || !currentItem.IsFile || !currentItem.IsCurrent));
		if (_TrackActiveFile && currentItem != null) {
			ScrollIntoView(currentItem);
		}
	}
	#endregion

	readonly record struct OpenDocumentId(string Name, string FullPath)
	{
		public OpenDocumentId(IVsWindowFrame windowFrame) : this(windowFrame.GetCaption(), windowFrame.GetDocumentFullPath()) { }
	}

	readonly record struct HighlightCondition(bool IsFile, string Name)
	{
		public bool IsCurrent(DirectoryInfo dir) {
			return !IsFile && FileHelper.AreFileNamesEqual(Name, dir.Name);
		}
		public bool IsCurrent(FileInfo file) {
			return IsFile && FileHelper.AreFileNamesEqual(Name, file.Name);
		}
		public bool IsCurrent(FileItem item) {
			return IsFile == item.IsFile && FileHelper.AreFileNamesEqual(Name, item.Name);
		}
	}

	sealed class ListItemContextMenuItem(int iconId, string name, ActivationCondition condition, RoutedEventHandler clickHandler) : ThemedMenuItem(iconId, name, clickHandler)
	{
		public ActivationCondition Condition { get; } = condition;
	}

	[Flags]
	enum ActivationCondition
	{
		None,
		HasFile,
		HasFolder = 1 << 1,
		HasFileOrFolder = HasFile | HasFolder,
		HasClipboardFile = 1 << 2,
		HasSingleItem = 1 << 3,
		HasSingleSolutionItem = 1 << 4,
	}

	enum ViewMode
	{
		File,
		SolutionProjects,
		Documents,
	}

	readonly record struct ViewItem(ViewMode Mode, FileListLocationType LocationType, string Path);

	// A view item list with limited capacity to avoid memory leak.
	// When new entry is added to the list, if the capacity is exceeded, the oldest entry will be removed
	sealed class ViewItemList(FileList list, int capacity = 256) : LinkedList<ViewItem>
	{
		readonly int _capacity = capacity;

		public void Push(ViewItem item) {
			if (item == list._CurrentView) {
				return;
			}
			Add(list._CurrentView);
			list._CurrentView = item;
		}

		public void Add(ViewItem history) {
			if (history == default
				|| history.Path is null
				|| Count != 0 && FileHelper.AreFileNamesEqual(history.Path, Last.Value.Path)) {
				return;
			}
			list._BackButton.IsEnabled = true;
			AddFirst(history);
			if (Count > _capacity) {
				RemoveLast();
			}
		}

		public bool TryPop(out ViewItem recentHistory) {
			if (Count != 0) {
				recentHistory = First.Value;
				RemoveFirst();
				if (Count == 0) {
					list._BackButton.IsEnabled = false;
				}
				return true;
			}
			recentHistory = default;
			return false;
		}
	}

	sealed class PathSegment(string path, int index)
	{
		string _text;

		public string Text => _text ??= path.Substring(0, index);

		public override string ToString() {
			return Text;
		}
	}

}
