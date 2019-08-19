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

		public bool ShowAfterUpdate() {
			if (TryGetInfoBarHost(out var host)) {
				return ShowInfoBar(host, new InfoBarModel(
					new[] {
						new InfoBarTextSpan(nameof(Codist) + " has been updated to " + Config.CurrentVersion + ". "),
						new InfoBarHyperlink("Click to see what's new", "New"),
						new InfoBarTextSpan(" or "),
						new InfoBarHyperlink("dismiss this bar", "Close")
					},
					KnownMonikers.StatusInformation));
			}
			return false;
		}

		public bool ShowAfterFirstRun() {
			if (TryGetInfoBarHost(out var host)) {
				return ShowInfoBar(host, new InfoBarModel(
					new[] {
						new InfoBarTextSpan(nameof(Codist) + " is run on your Visual Studio for the first time. "),
						new InfoBarHyperlink("Click to learn more", "More"),
						new InfoBarTextSpan(" or "),
						new InfoBarHyperlink("dismiss this bar", "Close"),
					},
					KnownMonikers.StatusInformation));
			}
			return false;
		}

		public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem) {
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
			if (_InfoBarUI != null) {
				_InfoBarUI.Unadvise(_Cookie);
			}
		}

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
	}
}
