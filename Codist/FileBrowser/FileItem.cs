using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using CLR;
using Microsoft.VisualStudio.Shell;

namespace Codist.FileBrowser;

sealed class FileItem : INotifyPropertyChanged
{
	static SolutionItemInfo __DefaultSolutionState = GetDefaultSolutionState();

	static SolutionItemInfo GetDefaultSolutionState() {
		Config.RegisterUpdateHandler(UpdateSolutionStateOnConfigChange);
		return Config.Instance.FileBrowserOptions.MatchFlags(FileBrowserOptions.DimNonSolutionItems)
			? SolutionItemInfo.Unknown
			: SolutionItemInfo.Yes;
	}

	static void UpdateSolutionStateOnConfigChange(ConfigUpdatedEventArgs args) {
		if (args.UpdatedFeature.MatchFlags(Features.FileBrowser)) {
			__DefaultSolutionState = GetDefaultSolutionState();
		}
	}

	readonly FileSystemInfo _Info;
	readonly FileItemType _Type;
	readonly string _Name;
	bool _IsCurrent;
	SolutionItemInfo _IsSolutionItem = __DefaultSolutionState;
	FileState _State;
	FrameworkElement _Icon;
	FrameworkElement _Note;

	long _FileSize = -1;
	DateTime _CreationTime;
	DateTime _LastWriteTime;

	public event PropertyChangedEventHandler PropertyChanged;

	public string Name => _Name;
	public string FullPath => _Info.FullName;
	public FileItemType Type => _Type;
	public bool IsEmptyFolder => _Type == FileItemType.EmptyFolder;
	public bool IsFolder => _Type.CeqAny(FileItemType.Folder, FileItemType.EmptyFolder);
	public bool IsFile => _Type.CeqAny(FileItemType.File, FileItemType.OpenedDocument);
	public bool IsCurrent {
		get => _IsCurrent;
		set {
			if (_IsCurrent != value) {
				_IsCurrent = value;
				PropertyChanged?.Invoke(this, new (nameof(IsCurrent)));
			}
		}
	}

	public FrameworkElement Icon => _Icon ??= VsImageHelper.GetImage(_Type switch {
		FileItemType.Folder => IconIds.Folder,
		FileItemType.EmptyFolder => IconIds.EmptyFolder,
		FileItemType.InaccessibleFolder => IconIds.InaccessibleFolder,
		FileItemType.Solution => IconIds.GoToSolutionFolder,
		FileItemType.UnloadedProject => IconIds.UnloadedProject,
		FileItemType.OpenedDocument => VsImageHelper.GetImageIdForFile(_Info.Name),
		_ => VsImageHelper.GetImageIdForFile(_Name)
	});

	public long FileSize {
		get {
			if (_FileSize < 0) {
				if (IsFile) {
					try {
						_FileSize = _Info is FileInfo info
							? info.Length
							: new FileInfo(_Info.FullName).Length;
					}
					catch {
						_FileSize = 0;
					}
				}
				else {
					_FileSize = 0;
				}
			}
			return _FileSize;
		}
	}

	public DateTime CreationTime {
		get {
			if (_CreationTime == default) {
				try { _CreationTime = _Info.CreationTime; }
				catch { _CreationTime = DateTime.MaxValue; }
			}
			return _CreationTime;
		}
	}

	public DateTime LastWriteTime {
		get {
			if (_LastWriteTime == default) {
				try { _LastWriteTime = _Info.LastWriteTime; }
				catch { _LastWriteTime = DateTime.MaxValue; }
			}
			return _LastWriteTime;
		}
	}

	public string FormattedFileSize => IsFile ? FormatFileSize(FileSize) : null;

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	public bool IsSolutionItem {
		get {
			if (_Type != FileItemType.File) {
				return _Type != FileItemType.UnloadedProject;
			}
			if (_IsSolutionItem == SolutionItemInfo.Unknown) {
				_IsSolutionItem = GetIsSolutionItem();
			}
			return _IsSolutionItem == SolutionItemInfo.Yes;
		}
	}

	public FileState FileState {
		get => _State;
		set => _State = value;
	}

	public FrameworkElement Note {
		get => _Note;
		set => _Note = value;
	}

	public FileItem(FileInfo fileInfo, bool isCurrent) {
		(_Info, _Type, _IsCurrent, _Name) = (fileInfo, FileItemType.File, isCurrent, fileInfo.Name);
	}

	public FileItem(DirectoryInfo dirInfo, bool isEmpty, bool isCurrent) {
		(_Info, _Type, _IsCurrent, _Name) = (dirInfo, isEmpty ? FileItemType.Folder : FileItemType.EmptyFolder, isCurrent, dirInfo.Name);
	}
	public FileItem(DirectoryInfo dirInfo, FileItemType type) {
		(_Info, _Type, _Name) = (dirInfo, type, dirInfo.Name);
	}
	public FileItem(FileSystemInfo fsInfo, FileItemType type, bool isCurrent) {
		(_Info, _Type, _IsCurrent, _Name) = (fsInfo, type, isCurrent, fsInfo.Name);
	}
	public FileItem(FileSystemInfo fsInfo, FileItemType type, bool isCurrent, string alias) {
		(_Info, _Type, _IsCurrent, _Name) = (fsInfo, type, isCurrent, alias);
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	internal void RefreshIsSolutionItem() {
		if (_Type != FileItemType.File
			|| _IsSolutionItem == 0) { // do not update IsSolutionItem if it is not initialized
			return;
		}
		var i = GetIsSolutionItem();
		if (i != _IsSolutionItem) {
			_IsSolutionItem = i;
			PropertyChanged?.Invoke(this, new(nameof(IsSolutionItem)));
		}
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	SolutionItemInfo GetIsSolutionItem() {
		var projItem = ServicesHelper.Instance.DTE.Solution.FindProjectItem(_Info.FullName);
		return projItem != null && projItem.Kind != VsShellHelper.MiscFilesKind
			? SolutionItemInfo.Yes
			: SolutionItemInfo.No;
	}

	internal void ClearIsSolutionItem() {
		if (_Type != FileItemType.File) {
			return;
		}
		if (_IsSolutionItem != 0) {
			_IsSolutionItem = 0;
			PropertyChanged?.Invoke(this, new(nameof(IsSolutionItem)));
		}
	}
	internal void ClearIsCurrent() {
		if (_IsCurrent) {
			_IsCurrent = false;
			PropertyChanged?.Invoke(this, new(nameof(IsCurrent)));
		}
	}
	internal void SetStateFromRunningDocumentInfo(RunningDocumentInfo docInfo, Chain<int> icons) {
		if (!docInfo.IsDocumentInitialized) {
			//_State |= FileState.Uninitialized;
			//icons.Add(IconIds.Hibernated);
			return;
		}

		if (docInfo.IsReadOnly) {
			_State |= FileState.ReadOnly;
			icons.Add(IconIds.Readonly);
		}
		uint flags = docInfo.Flags;
		if (flags.MatchFlags((uint)(VsRdtFlags.DontAddToMRU | VsRdtFlags.DontAutoOpen))) {
			_State |= FileState.New;
			icons.Add(IconIds.NewFile);
		}
		if (flags.HasAnyFlag((uint)(VsRdtFlags.DontSave | VsRdtFlags.DontSaveAs))) {
			_State |= FileState.DontSave;
			icons.Add(IconIds.DontSave);
		}
		if (flags.MatchFlags((uint)VsRdtFlags.VirtualDocument)) {
			_State |= FileState.Virtual;
			icons.Add(IconIds.FileVirtual);
		}
		if (docInfo.IsDirty) {
			_State |= FileState.Modified;
			icons.Add(IconIds.Modified);
		}
	}

	static string FormatFileSize(long bytes) {
		string[] sizes = { "B", "KB", "MB", "GB", "TB" };
		int order = 0;
		double size = bytes;
		while (size >= 1024 && order < sizes.Length - 1) {
			order++;
			size /= 1024;
		}
		return $"{size:0.##} {sizes[order]}";
	}

	enum SolutionItemInfo : byte
	{
		Unknown,
		Yes,
		No
	}
}
