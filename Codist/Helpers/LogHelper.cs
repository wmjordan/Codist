using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Codist
{
	static class LogHelper
	{
		static int _Sync;

		static string _Path;
		public static string LogPath {
			get => _Path;
			set => _Path = File.Exists(value) ? value : null;
		}

		public static void Log(this string message) {
			if (_Path != null) {
				WriteLog(message);
			}
			Debug.WriteLine(message);
		}

		static void WriteLog(string message) {
			while (Interlocked.CompareExchange(ref _Sync, 1, 0) != 0) {
				SpinWait.SpinUntil(() => Volatile.Read(ref _Sync) == 0);
			}
			try {
				File.AppendAllText(_Path, $"{DateTime.Now.ToShortTimeString()} {message}{Environment.NewLine}");
			}
			catch (SystemException) {
				// ignore
			}
			finally {
				Volatile.Write(ref _Sync, 0);
			}
		}

		public static void Log(this Exception exception) {
			Log(exception.ToString());
		}

		public static void ClearLog() {
			if (File.Exists(LogPath)) {
				try {
					File.WriteAllText(LogPath, String.Empty);
				}
				catch (SystemException) {
					// ignore
				}
			}
		}
	}
}
