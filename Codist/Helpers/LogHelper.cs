using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CLR;

namespace Codist
{
	/// <summary>
	/// <para>A simple logger which logs information to the file defined in <c>%AppData%\Codist\log.ini</c>.</para>
	/// <para>By default, all log methods are simply ignored. Specify "<c>LOG</c>" or "<c>DEBUG</c>" in <i>conditional compilation symbols</i> of the project.</para>
	/// </summary>
	static class LogHelper
	{
		static readonly int __PSId = Process.GetCurrentProcess().Id;
		static LogCategory __Categories;
		static readonly string __LogPath = InitLogConfig($"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\{Constants.NameOfMe}\\log.ini");
		static int __Sync;

		public static LogCategory Categories {
			get => __Categories;
			set => __Categories = value;
		}

		[Conditional("LOG")]
		[Conditional("DEBUG")]
		public static void Log(this string message, LogCategory category = LogCategory.None) {
			if (!Categories.MatchFlags(category)) {
				return;
			}
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

		static string InitLogConfig(string logConfigPath) {
			try {
				if (!File.Exists(logConfigPath)) {
					return default;
				}
				string f = null;
				foreach (var line in File.ReadAllLines(logConfigPath)) {
					var p = line.Split('=');
					if (p.Length == 2) {
						switch (p[0]) {
							case "file": f = p[1]; break;
							case "category":
								switch (p[1]) {
									case "config": __Categories |= LogCategory.Config; break;
									case "format": __Categories |= LogCategory.FormatStore; break;
									case "highlight": __Categories |= LogCategory.SyntaxHighlight; break;
								}
								break;
							default:
								break;
						}
					}
				}
				return f;
			}
			catch (Exception) {
				// ignore
				return default;
			}
		}
	}

	[Flags]
	enum LogCategory
	{
		None,
		Config = 1,
		FormatStore = 1 << 1,
		SyntaxHighlight = 1 << 2,
		All = Config | FormatStore | SyntaxHighlight
	}
}
