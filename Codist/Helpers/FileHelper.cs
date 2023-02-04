using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Codist
{
	static class FileHelper
	{
		public static (string folder, string file) DeconstructPath(string path) {
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
				CodistPackage.ShowMessageBox(ex.Message, nameof(Codist), true);
			}
			catch (FileNotFoundException) {
				// ignore
			}
		}

		public static void OpenInExplorer(string path) {
			var (folder, file) = DeconstructPath(path);
			OpenPathInExplorer(folder, path);
		}

		public static void OpenInExplorer(string folder, string file) {
			OpenPathInExplorer(folder, Path.Combine(folder, file));
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
