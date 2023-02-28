using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Codist
{
	/// <summary>
	/// <para>A simple logger which logs information to the file in <see cref="LogPath"/>. The file must pre-exist before <see cref="LogPath"/> is specified.</para>
	/// <para>By default, all log methods are simply ignored. Specify "<c>LOG</c>" or "<c>DEBUG</c>" in <i>conditional compilation symbols</i> of the project.</para>
	/// </summary>
	static class LogHelper
	{
		static int _Sync;

		static string _Path;
		public static string LogPath {
			get => _Path;
			set => _Path = File.Exists(value) ? value : null;
		}

		[Conditional("LOG")]
		[Conditional("DEBUG")]
		public static void Log(this string message) {
			if (_Path != null) {
				WriteLog(message);
			}
			Debug.WriteLine(message);
		}

		[Conditional("LOG")]
		[Conditional("DEBUG")]
		static void WriteLog(string message) {
			while (Interlocked.CompareExchange(ref _Sync, 1, 0) != 0) {
				SpinWait.SpinUntil(() => Volatile.Read(ref _Sync) == 0);
			}
			try {
				File.AppendAllText(_Path, $"{DateTime.Now.ToLongTimeString()}\t{message}{Environment.NewLine}");
			}
			catch (SystemException) {
				// ignore
			}
			finally {
				Volatile.Write(ref _Sync, 0);
			}
		}

		[Conditional("LOG")]
		[Conditional("DEBUG")]
		public static void Log(this Exception exception) {
			Log(exception.ToString());
		}

		[Conditional("LOG")]
		[Conditional("DEBUG")]
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
