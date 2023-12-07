using System;
using System.Diagnostics;
using System.IO;
using R = Codist.Properties.Resources;

namespace Codist
{
	static class FileHelper
	{
		public static (string folder, string file) DeconstructPath(string path) {
			if (path == null) {
				return default;
			}
			try {
				var folder = Path.GetDirectoryName(path);
				if (folder.Length > 0 && folder[folder.Length - 1] != Path.DirectorySeparatorChar) {
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
				Process.Start(path);
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

		public static void OpenInExplorer(string folder, string file) {
			try {
				file = Path.Combine(folder, file);
			}
			catch (Exception ex) {
				Controls.MessageWindow.Error(ex, R.T_ErrorOpeningFile);
			}
			OpenPathInExplorer(folder, file);
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
