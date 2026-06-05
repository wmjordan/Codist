using System;
using System.Diagnostics;
using System.IO;
using R = Codist.Properties.Resources;

namespace Codist
{
	static class FileHelper
	{
		static readonly char[] __InvalidFileNameChars = Path.GetInvalidFileNameChars();

		public static (string folder, string file) DeconstructPath(string path, bool removeTrailingDirSeparator = false) {
			if (path == null) {
				return default;
			}
			try {
				var folder = Path.GetDirectoryName(path);
				if (!removeTrailingDirSeparator
					&& folder.Length > 0
					&& folder[folder.Length - 1] != Path.DirectorySeparatorChar) {
					folder += Path.DirectorySeparatorChar;
				}
				var file = Path.GetFileName(path);
				return (folder, file);
			}
			catch (ArgumentException) {
				return default;
			}
		}

		public static void TryRun(string path) {
			if (path == null) {
				return;
			}
			try {
				Process.Start(new ProcessStartInfo(path) { WorkingDirectory = Path.GetDirectoryName(path) });
			}
			catch (System.ComponentModel.Win32Exception ex) {
				Controls.MessageWindow.Error(ex, R.T_ErrorOpeningFile);
			}
			catch (FileNotFoundException) {
				// ignore
			}
		}

		public static void OpenInExplorer(string path) {
			var (folder, _) = DeconstructPath(path);
			OpenPathInExplorer(folder, path);
		}

		public static void OpenFolderInExplorer(string folderPath) {
			OpenPathInExplorer(folderPath, null);
		}

		public static void OpenInExplorer(string folder, string file) {
			try {
				file = Path.Combine(folder, file);
			}
			catch (Exception ex) {
				Controls.MessageWindow.Error(ex, R.T_ErrorOpeningFile);
			}
			OpenPathInExplorer(folder, file);
		}

		public static bool AreFileNamesEqual(string file1, string file2) {
			return String.Equals(file1, file2, StringComparison.OrdinalIgnoreCase);
		}

		public static bool ContainsInvalidFileNameCharacter(string path) {
			return path.IndexOfAny(__InvalidFileNameChars) >= 0;
		}

		public static bool HasExtension(string filePath, string extensionWithoutDot) {
			int p;
			return (p = filePath.Length - extensionWithoutDot.Length) > 0
				&& filePath[p - 1] == '.'
				&& filePath.IndexOf(extensionWithoutDot) == p;
		}

		public static bool HasAnyExtension(string filePath, params string[] extensionsWithoutDot) {
			foreach (var item in extensionsWithoutDot) {
				if (HasExtension(filePath, item)) {
					return true;
				}
			}
			return false;
		}

		static void OpenPathInExplorer(string folder, string path) {
			try {
				if (File.Exists(path)) {
					Process.Start(new ProcessStartInfo("Explorer.exe", $"/select,\"{path}\"") { WorkingDirectory = folder });
				}
				else if (Directory.Exists(folder)) {
					Process.Start(new ProcessStartInfo("Explorer.exe", $"\"{folder}\""));
				}
			}
			catch (SystemException) {
				// ignore
			}
		}
	}
}
