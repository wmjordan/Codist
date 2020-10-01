using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using R = Codist.Properties.Resources;

namespace Codist.Commands
{
	sealed class VersionInfoBar : IVsInfoBarUIEvents
	{
		readonly IServiceProvider _ServiceProvider;
		IVsInfoBarUIElement _InfoBarUI;
		uint _Cookie;

		public VersionInfoBar(IServiceProvider serviceProvider) {
			_ServiceProvider = serviceProvider;
		}

		public bool Show(InitStatus status) {
			if (TryGetInfoBarHost(out var host)) {
				switch (status) {
					case InitStatus.Upgraded:
						return ShowInfoBar(host, new InfoBarModel(
							new[] {
								new InfoBarTextSpan(R.T_CodistUpdated.Replace("<VERSION>", Config.CurrentVersion)),
								new InfoBarHyperlink(R.CMD_SeeWhatsNew, "New"),
								new InfoBarTextSpan(R.T_Or),
								new InfoBarHyperlink(R.CMD_DismissNotification, "Close")
							},
							KnownMonikers.StatusInformation));
					case InitStatus.FirstLoad:
						return ShowInfoBar(host, new InfoBarModel(
							new[] {
								new InfoBarTextSpan(R.T_CodistFirstRun),
								new InfoBarHyperlink(R.CMD_LearnMore, "More"),
								new InfoBarTextSpan(R.T_Or),
								new InfoBarHyperlink(R.CMD_DismissNotification, "Close")
							},
							KnownMonikers.StatusInformation));
				}
			}
			return false;
		}

		public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem) {
			ThreadHelper.ThrowIfNotOnUIThread();
			string context = actionItem.ActionContext as string;
			switch (context) {
				case "New": {
					Process.Start("https://github.com/wmjordan/Codist/releases");
					break;
				}
				case "More":
					Process.Start("https://github.com/wmjordan/Codist");
					break;
				case "Close": break;
			}
			_InfoBarUI.Close();
		}

		public void OnClosed(IVsInfoBarUIElement infoBarUIElement) {
			ThreadHelper.ThrowIfNotOnUIThread();
			if (_InfoBarUI != null) {
				_InfoBarUI.Unadvise(_Cookie);
			}
		}

#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
		bool ShowInfoBar(IVsInfoBarHost host, InfoBarModel infoBar) {
			var factory = _ServiceProvider.GetService(typeof(SVsInfoBarUIFactory)) as IVsInfoBarUIFactory;
			if (factory != null) {
				_InfoBarUI = factory.CreateInfoBar(infoBar);
				_InfoBarUI.Advise(this, out _Cookie);
				host.AddInfoBar(_InfoBarUI);
				return true;
			}
			return false;
		}
		bool TryCreateInfoBarUI(IVsInfoBar infoBar, out IVsInfoBarUIElement uiElement) {
			var infoBarUIFactory = _ServiceProvider.GetService(typeof(SVsInfoBarUIFactory)) as IVsInfoBarUIFactory;
			if (infoBarUIFactory == null) {
				uiElement = null;
				return false;
			}
			return (uiElement = infoBarUIFactory.CreateInfoBar(infoBar)) != null;
		}

		bool TryGetInfoBarHost(out IVsInfoBarHost infoBarHost) {
			var shell = _ServiceProvider.GetService(typeof(SVsShell)) as IVsShell;
			if (shell == null) {
				goto Exit;
			}
			object infoBarHostObj;
			if (ErrorHandler.Failed(shell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out infoBarHostObj))) {
				goto Exit;
			}
			return (infoBarHost = infoBarHostObj as IVsInfoBarHost) != null;
			Exit:
			infoBarHost = null;
			return false;
		}
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
	}
}
