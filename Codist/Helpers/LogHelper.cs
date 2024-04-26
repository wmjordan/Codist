using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Codist
{
	/// <summary>
	/// <para>A simple logger which logs information to the file defined in <c>%AppData%\Codist\log.ini</c>.</para>
	/// <para>By default, all log methods are simply ignored. Specify "<c>LOG</c>" or "<c>DEBUG</c>" in <i>conditional compilation symbols</i> of the project.</para>
	/// </summary>
	static class LogHelper
	{
		static readonly int __PSId = Process.GetCurrentProcess().Id;
		static readonly string __LogPath = InitLogPath($"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\{Constants.NameOfMe}\\log.ini");
		static int __Sync;

		[Conditional("LOG")]
		[Conditional("DEBUG")]
		public static void Log(this string message) {
			if (__LogPath != null) {
				WriteLog(message);
			}
			Debug.WriteLine(message);
		}

		[Conditional("LOG")]
		[Conditional("DEBUG")]
		public static void LogInitialized(this string message) {
			Log(message + " initialized.");
		}

		[Conditional("LOG")]
		[Conditional("DEBUG")]
		static void WriteLog(string message) {
			while (Interlocked.CompareExchange(ref __Sync, 1, 0) != 0) {
				SpinWait.SpinUntil(() => Volatile.Read(ref __Sync) == 0);
			}
			try {
				File.AppendAllText(__LogPath, $"{__PSId.ToText()}\t{DateTime.Now.ToLongTimeString()}\t{message}{Environment.NewLine}");
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
			if (File.Exists(__LogPath) == false) {
				return;
			}
			while (Interlocked.CompareExchange(ref __Sync, 1, 0) != 0) {
				SpinWait.SpinUntil(() => Volatile.Read(ref __Sync) == 0);
			}
			try {
				File.WriteAllText(__LogPath, String.Empty);
			}
			catch (SystemException) {
				// ignore
			}
			finally {
				Volatile.Write(ref __Sync, 0);
			}
		}

		static string InitLogPath(string logConfigPath) {
			try {
				return File.Exists(logConfigPath) ? File.ReadAllText(logConfigPath) : null;
			}
			catch (Exception) {
				// ignore
				return null;
			}
		}
	}
}
