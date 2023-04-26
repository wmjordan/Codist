using System;
using System.Diagnostics;
using System.Windows.Input;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;

namespace Codist
{
	public static class ExternalCommand
	{
		public static void OpenWithWebBrowser(string url, string text) {
			ThreadHelper.ThrowIfNotOnUIThread();
			try {
				url = url.Replace("%s", System.Net.WebUtility.UrlEncode(text));
				if (Keyboard.Modifiers == ModifierKeys.Control) {
					CodistPackage.DTE.ItemOperations.Navigate(url);
				}
				else if (String.IsNullOrEmpty(Config.Instance.BrowserPath) == false) {
					Process.Start(Config.Instance.BrowserPath, String.IsNullOrEmpty(Config.Instance.BrowserParameter) ? url : Config.Instance.BrowserParameter.Replace("%u", url));
				}
				else {
					Process.Start(url);
				}
			}
			catch (Exception ex) {
				Controls.MessageWindow.Error(R.T_FailedToLaunchBrowser + Environment.NewLine + ex.Message, nameof(Codist));
			}
		}

		public static void OpenTaskManager() {
			Process.Start(Config.Instance.TaskManagerPath ?? "TaskMgr.exe", Config.Instance.TaskManagerParameter);
		}
	}
}
