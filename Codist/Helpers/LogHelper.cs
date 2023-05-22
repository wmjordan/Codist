using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Codist
{
	/// <summary>
	/// <para>A simple logger which logs information to the file in <see cref="LogPath"/>. The file must pre-exist before <see cref="LogPath"/> is specified.</para>
	/// <para>By default, all log methods are simply ignored. Specify "<c>LOG</c>" or "<c>DEBUG</c>" in <i>conditional compilation symbols</i> of the project.</para>
	/// </summary>
	static class LogHelper
	{
		static int __Sync;
		static readonly int __PSId = Process.GetCurrentProcess().Id;

		static string __Path;
		public static string LogPath {
			get => __Path;
			set {
				if (String.IsNullOrWhiteSpace(value) == false
					&& File.Exists(value) == false) {
					try {
						File.CreateText(value);
					}
					catch (SystemException ex) {
						// ignore
						Debug.WriteLine(ex);
						__Path = null;
						return;
					}
				}
				__Path = value;
			}
		}

		[Conditional("LOG")]
		[Conditional("DEBUG")]
		public static void Log(this string message) {
			if (__Path != null) {
				WriteLog(message);
			}
			Debug.WriteLine(message);
		}

		[Conditional("LOG")]
		[Conditional("DEBUG")]
		static void WriteLog(string message) {
			while (Interlocked.CompareExchange(ref __Sync, 1, 0) != 0) {
				SpinWait.SpinUntil(() => Volatile.Read(ref __Sync) == 0);
			}
			try {
				File.AppendAllText(__Path, $"{__PSId.ToText()}\t{DateTime.Now.ToLongTimeString()}\t{message}{Environment.NewLine}");
			}
			catch (SystemException) {
				// ignore
			}
			finally {
				Volatile.Write(ref __Sync, 0);
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
			if (File.Exists(LogPath) == false) {
				return;
			}
			while (Interlocked.CompareExchange(ref __Sync, 1, 0) != 0) {
				SpinWait.SpinUntil(() => Volatile.Read(ref __Sync) == 0);
			}
			try {
				File.WriteAllText(LogPath, String.Empty);
			}
			catch (SystemException) {
				// ignore
			}
			finally {
				Volatile.Write(ref __Sync, 0);
			}
		}
	}
}
