using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;

namespace Codist.Controls
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
					System.Diagnostics.Process.Start(Config.Instance.BrowserPath, String.IsNullOrEmpty(Config.Instance.BrowserParameter) ? url : Config.Instance.BrowserParameter.Replace("%u", url));
				}
				else {
					System.Diagnostics.Process.Start(url);
				}
			}
			catch (Exception ex) {
				MessageBox.Show(R.T_FailedToLaunchBrowser + Environment.NewLine + ex.Message, nameof(Codist), MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
		}

		public static void OpenTaskManager() {
			System.Diagnostics.Process.Start(Config.Instance.TaskManagerPath ?? "TaskMgr.exe", Config.Instance.TaskManagerParameter);
		}
	}
}
