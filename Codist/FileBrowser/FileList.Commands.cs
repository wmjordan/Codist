using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Codist.Controls;
using R = Codist.Properties.Resources;

namespace Codist.FileBrowser;

partial class FileList
{
	const string ClipboardFileFormat = "Preferred DropEffect";

	#region Working mode
	void ToggleMultiSelectionMode(object sender, RoutedEventArgs e) {
		if (SelectionMode == SelectionMode.Extended) {
			MouseUp -= HandleMouseUp;
			MouseDoubleClick += HandleMouseDoubleClick;
			SelectionMode = SelectionMode.Multiple;
		}
		else {
			MouseUp += HandleMouseUp;
			MouseDoubleClick -= HandleMouseDoubleClick;
			SelectionMode = SelectionMode.Extended;
		}
	}

	void ToggleSyncMode(object sender, RoutedEventArgs e) {
		if (_TrackActiveFile = ((ThemedToggleButton)sender).IsChecked == true) {
			RefreshCurrentFileAsync(true, default).FireAndForget();
		}
	}
	#endregion

	#region Special navigation
	void GoToCurrentFile(object sender, RoutedEventArgs args) {
		ClearFilter();
		var (directory, _) = FileHelper.DeconstructPath(_ActiveFilePath, true);
		if (String.IsNullOrEmpty(directory)) {
			return;
		}
		LocationType = FileListLocationType.CurrentDocumentFolder;
		UnsafeNavigateToDirectoryAsync(directory).FireAndForget();
	}

	void GoToSolutionFolder(object sender, RoutedEventArgs args) {
		if (!String.IsNullOrEmpty(_SolutionFolderPath)) {
			LocationType = FileListLocationType.SolutionFolder;
			UnsafeNavigateToDirectoryAsync(_SolutionFolderPath).FireAndForget();
		}
	}

	void GoToProjectFolder(object sender, RoutedEventArgs args) {
		if (!String.IsNullOrEmpty(_ProjectFolderPath)) {
			LocationType = FileListLocationType.CurrentProjectFolder;
			UnsafeNavigateToDirectoryAsync(_ProjectFolderPath).FireAndForget();
		}
	}
	#endregion

	void OpenInExplorer(object sender, RoutedEventArgs args) {
		if (!String.IsNullOrEmpty(_ActiveFilePath)
			&& Path.GetDirectoryName(_ActiveFilePath) == _ActiveDirPath) {
			FileHelper.OpenInExplorer(_ActiveFilePath);
		}
		else {
			FileHelper.OpenFolderInExplorer(_ActiveDirPath);
		}
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.EventHandler)]
	void OpenFilesWithVisualStudio(object sender, RoutedEventArgs args) {
		var activeItem = (FileSystemItem)SelectedItem;
		FileActivated?.Invoke(this, new(activeItem));
		if (activeItem.IsFile
			&& (FileHelper.HasAnyExtension(activeItem.Name, "sln", "slnx"))) {
			if (!String.IsNullOrEmpty(ServicesHelper.Instance.DTE.Solution.FullName)
				&& MessageWindow.AskYesNo(R.T_ConfirmLoadSolutionNote.Replace("<FILE>", activeItem.Name), Constants.NameOfMe) == false) {
				return;
			}
			ServicesHelper.Instance.DTE.Solution.Open(activeItem.FullPath);
			return;
		}
		var newWindow = false; // preview the first; open others
		var useDesigner = !UIHelper.IsCtrlDown; // press Ctrl to force code view
		foreach (var path in SelectedFilePaths) {
			TextEditorHelper.OpenFile(path, newWindow, useDesigner);
			newWindow = true;
		}
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.EventHandler)]
	void LocateInSolutionExplorer(object sender, RoutedEventArgs args) {
		if (SelectedItem is FileSystemItem fsi && fsi.IsFile) {
			var dte = ServicesHelper.Instance.DTE;
			var projectItem = dte.Solution.FindProjectItem(fsi.FullPath);
			var parents = new Stack<string>();
			var current = projectItem;
			while (current != null) {
				parents.Push(current.Name);
				current = current.Collection?.Parent as EnvDTE.ProjectItem;
				if (current == null && projectItem.ContainingProject != null) {
					parents.Push(projectItem.ContainingProject.Name);
					parents.Push(Path.GetFileNameWithoutExtension(dte.Solution.FileName));
					break;
				}
			}

			try {
				if (parents.Count != 0) {
					var item = dte.ToolWindows.SolutionExplorer.GetItem(parents.Pop());
					while (parents.Count != 0) {
						var items = item.UIHierarchyItems;
						if (!items.Expanded) {
							items.Expanded = true;
						}
						item = items.Item(parents.Pop());
					}
					item.Select(EnvDTE.vsUISelectionType.vsUISelectionTypeSelect);
				}
				FileActivated?.Invoke(this, new(fsi));
			}
			catch (Exception ex) {
				MessageWindow.Error(ex, source: this);
			}
		}
	}

	#region File operations
	void CutFiles(object sender, RoutedEventArgs args) {
		CopyOrCutFiles(true);
	}

	void CopyFiles(object sender, RoutedEventArgs args) {
		CopyOrCutFiles(false);
	}

	void CopyOrCutFiles(bool isCut) {
		var paths = SelectedPaths.ToArray();
		if (paths.Length == 0) {
			return;
		}
		try {
			var dataObject = new DataObject(DataFormats.FileDrop, paths);
			dataObject.SetData(ClipboardFileFormat, new MemoryStream([(byte)(isCut ? 2 : 1), 0, 0, 0]));
			Clipboard.SetDataObject(dataObject, true);
		}
		catch (Exception ex) {
			MessageWindow.Error(ex, source: this);
		}
	}

	void PasteFiles(object sender, RoutedEventArgs args) {
		if (!Clipboard.ContainsFileDropList()) {
			return;
		}

		var files = Clipboard.GetFileDropList();
		if (files.Count == 0) {
			return;
		}

		bool isCut = false;
		if (Clipboard.ContainsData(ClipboardFileFormat)) {
			using var stream = Clipboard.GetData(ClipboardFileFormat) as MemoryStream;
			if (stream?.Length >= 4) {
				var bytes = new byte[4];
				stream.Read(bytes, 0, 4);
				isCut = (bytes[0] == 2); // Move=2
			}
		}

		try {
			NativeMethods.ShellCopyOrMove(files.Cast<string>(), _ActiveDirPath, isCut);
		}
		catch { }
		LoadDirectoryAsync(_ActiveDirPath, default).FireAndForget();
		FileActivated?.Invoke(this, new((FileSystemItem)SelectedItem));
	}

	void DeleteFiles(object sender, RoutedEventArgs args) {
		var selection = SelectedItems;
		int c = selection.Count;
		var pathsToDelete = new string[c];
		for (int i = 0; i < c; i++) {
			var item = (FileSystemItem)selection[i];
			pathsToDelete[i] = item.FullPath;
			if (_ActiveFilePath != null && item.IsCurrent) {
				_ActiveFilePath = null;
			}
		}

		try {
			var toRecycleBin = !UIHelper.IsShiftDown;
			NativeMethods.DeleteFile(pathsToDelete, toRecycleBin);
		}
		catch { }

		LoadDirectoryAsync(_ActiveDirPath, default).FireAndForget();
	}

	void StartRename(object sender, RoutedEventArgs args) {
		if (SelectedItem is not FileSystemItem fsi) {
			return;
		}

		var itemContainer = (ListBoxItem)ItemContainerGenerator.ContainerFromItem(SelectedItem);
		if (itemContainer == null) {
			return;
		}

		// retrieve StackPanel and TextBlock created in CreateItemTemplate
		// path：ListBoxItem -> ContentPresenter -> StackPanel -> [ContentPresenter(icon), TextBlock(name)]
		var sp = itemContainer.GetFirstVisualChild<StackPanel>();
		if (sp is null) {
			return;
		}
		var originalTextBlock = sp.Children[1] as TextBlock;
		if (originalTextBlock is null) {
			return;
		}

		originalTextBlock.Visibility = Visibility.Collapsed;

		// create in-place TextBox over originalTextBlock
		var editBox = new ThemedTextBox {
			Text = fsi.Name,
			Padding = originalTextBlock.Padding,
			FontFamily = originalTextBlock.FontFamily,
			FontSize = originalTextBlock.FontSize,
			FontWeight = originalTextBlock.FontWeight,
			VerticalAlignment = VerticalAlignment.Center
		};

		sp.Children.Insert(1, editBox);
		editBox.Focus();

		// select file name without extension
		int selectLength = fsi.Name.Length;
		if (fsi.IsFile) {
			int dotIndex = fsi.Name.LastIndexOf('.');
			if (dotIndex > 0) {
				selectLength = dotIndex;
			}
		}
		editBox.Select(0, selectLength);

		bool isCanceled = false;

		void EditBox_LostFocus(object s, RoutedEventArgs e) {
			if (!isCanceled) {
				editBox.LostFocus -= EditBox_LostFocus;
				CommitRename(fsi, editBox.Text, sp, originalTextBlock, editBox);
			}
		}

		editBox.KeyUp += (s, e) => {
			if (e.Key == Key.Enter) {
				e.Handled = true;
				editBox.LostFocus -= EditBox_LostFocus;
				CommitRename(fsi, editBox.Text, sp, originalTextBlock, editBox);
			}
			else if (e.Key == Key.Escape) {
				e.Handled = true;
				isCanceled = true;
				editBox.LostFocus -= EditBox_LostFocus;
				CancelRename(sp, originalTextBlock, editBox);
			}
		};

		editBox.LostFocus += EditBox_LostFocus;
	}

	void CommitRename(FileSystemItem fsi, string newText, StackPanel panel, TextBlock originalTb, TextBox editBox) {
		panel.Children.Remove(editBox);
		originalTb.Visibility = Visibility.Visible;

		newText = newText.Trim();

		if (string.IsNullOrWhiteSpace(newText) || FileHelper.AreFileNamesEqual(newText, fsi.Name)) {
			return;
		}
		if (FileHelper.ContainsInvalidFileNameCharacter(newText)) {
			MessageWindow.Error(R.T_InvalidFileName, R.T_FailedToRename);
			return;
		}

		var newPath = Path.Combine(_ActiveDirPath, newText);
		try {
			NativeMethods.ShellRename(fsi.FullPath, newPath);
		}
		catch (Exception ex) {
			MessageWindow.Error(ex, R.T_FailedToRename, source: this);
			return;
		}

		if (!String.IsNullOrEmpty(_ActiveFilePath)) {
			var oldPath = fsi.FullPath;
			if (FileHelper.AreFileNamesEqual(_ActiveFilePath, oldPath)) {
				// current file renamed
				_ActiveFilePath = newPath;
			}
			else if (_ActiveFilePath.StartsWith(oldPath, StringComparison.OrdinalIgnoreCase)
				&& _ActiveFilePath[oldPath.Length] == '\\') {
				// parent folder of current file renamed
				_ActiveFilePath = newPath + _ActiveFilePath.Substring(oldPath.Length);
			}
		}
		LoadDirectoryAsync(_ActiveDirPath, default).FireAndForget();
	}

	static void CancelRename(StackPanel panel, TextBlock originalTb, TextBox editBox) {
		panel.Children.Remove(editBox);
		originalTb.Visibility = Visibility.Visible;
	}

	void ShowProperties(object sender, RoutedEventArgs args) {
		if (SelectedItem is FileSystemItem fsi) {
			NativeMethods.ShowFileProperties(fsi.FullPath);
		}
	}
	#endregion

	#region Selection
	void HandleSelectAll(object sender, EventArgs args) {
		SelectAll();
	}
	void HandleSelectNone(object sender, EventArgs args) {
		SelectedIndex = -1;
	}
	#endregion

	static class NativeMethods
	{
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct SHFILEOPSTRUCT
		{
			public IntPtr hwnd;
			public uint wFunc;
			public string pFrom;
			public string pTo;
			public ushort fFlags;
			public bool fAnyOperationsAborted;
			public IntPtr hNameMappings;
			public string lpszProgressTitle;
		}

		const uint FO_CUT = 1;
		const uint FO_COPY = 2;
		const uint FO_DELETE = 3;
		const uint FO_MOVE = 4;
		const ushort FOF_NONE = 0;
		const ushort FOF_SILENT = 0x04; // no progress
		const ushort FOF_ALLOWUNDO = 0x40; // move to recycle bin (allow redo)
		const ushort FOF_NOCONFIRMATION = 0x10;
		const ushort FOF_NOCONFIRMMKDIR = 0x200; // create DIR if not exist

		const int MaxSilentItemCount = 100;
		const long MaxSilentCopyBytes = 50 * 1024 * 1024;

		[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
		static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

		public static void DeleteFile(IEnumerable<string> paths, bool toRecycleBin) {
			var showProgress = ShouldShowProgressDialog(paths, checkBytes: false);

			var op = new SHFILEOPSTRUCT {
				hwnd = CodistPackage.WindowHandle,
				wFunc = FO_DELETE,
				pFrom = JoinPaths(paths),
				fFlags = (ushort)((toRecycleBin ? FOF_ALLOWUNDO : FOF_NONE) | (showProgress ? FOF_NONE : FOF_SILENT))
			};
			SHFileOperation(ref op);
		}

		static string JoinPaths(IEnumerable<string> paths) {
			return String.Join("\0", paths) + "\0\0";
		}

		public static void ShellCopyOrMove(IEnumerable<string> sourcePaths, string destDir, bool isMove) {
			var showProgress = ShouldShowProgressDialog(sourcePaths, checkBytes: false);

			var op = new SHFILEOPSTRUCT {
				hwnd = CodistPackage.WindowHandle,
				wFunc = isMove ? FO_CUT : FO_COPY,
				pFrom = JoinPaths(sourcePaths),
				pTo = destDir + "\0\0",
				fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMMKDIR | (showProgress ? FOF_NONE : FOF_SILENT))
			};
			SHFileOperation(ref op);
		}

		public static void ShellRename(string oldPath, string newPath) {
			var op = new SHFILEOPSTRUCT {
				wFunc = FO_MOVE,
				pFrom = oldPath + "\0\0",
				pTo = newPath + "\0\0",
				fFlags = FOF_ALLOWUNDO | FOF_SILENT | FOF_NOCONFIRMATION
			};
			SHFileOperation(ref op);
		}

		[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
		static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		struct SHELLEXECUTEINFO
		{
			public int cbSize;
			public uint fMask;
			public IntPtr hwnd;
			public string lpVerb;
			public string lpFile;
			public string lpParameters;
			public string lpDirectory;
			public int nShow;
			public IntPtr hInstApp;
			public IntPtr lpIDList;
			public string lpClass;
			public IntPtr hkeyClass;
			public uint dwHotKey;
			public IntPtr hIcon;
			public IntPtr hProcess;
		}

		public static void ShowFileProperties(string filePath) {
			var info = new SHELLEXECUTEINFO {
				lpVerb = "properties",
				lpFile = filePath,
				nShow = 5, // SW_SHOW
				fMask = 0x0000000C
			};
			info.cbSize = Marshal.SizeOf(info);
			ShellExecuteEx(ref info);
		}

		static bool ShouldShowProgressDialog(IEnumerable<string> paths, bool checkBytes) {
			int count = 0;
			long totalBytes = 0;

			foreach (var path in paths) {
				var attr = File.GetAttributes(path);
				if ((attr & FileAttributes.Directory) == FileAttributes.Directory) {
					if (EnumerateForThresholdCheck(path, ref count, ref totalBytes, checkBytes)) {
						return true;
					}
					continue;
				}
				if (++count > MaxSilentItemCount) {
					return true;
				}
				if (checkBytes) {
					try { totalBytes += new FileInfo(path).Length; }
					catch {
						continue;
					}
					if (totalBytes > MaxSilentCopyBytes) {
						return true;
					}
				}
			}
			return false;
		}

		static bool EnumerateForThresholdCheck(string dirPath, ref int count, ref long totalBytes, bool checkBytes) {
			try {
				foreach (var entry in new DirectoryInfo(dirPath).EnumerateFileSystemInfos()) {
					if (++count > MaxSilentItemCount) {
						return true;
					}

					if (entry is FileInfo file) {
						if (checkBytes) {
							try { totalBytes += file.Length; } catch { continue; }
							if (totalBytes > MaxSilentCopyBytes) return true;
						}
					}
					else if (entry is DirectoryInfo dir) {
						if (EnumerateForThresholdCheck(dir.FullName, ref count, ref totalBytes, checkBytes)) {
							return true;
						}
					}
				}
			}
			catch (UnauthorizedAccessException) { }
			catch (PathTooLongException) { }
			catch (IOException) { }

			return false;
		}
	}
}
