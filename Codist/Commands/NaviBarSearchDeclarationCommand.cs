using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;

namespace Codist.Commands
{
	/// <summary>
	/// A command handler which shows the root item menu of Navigation Bar.
	/// </summary>
	internal static class NaviBarSearchDeclarationCommand
	{
		public static void Initialize() {
			Command.NaviBarSearchDeclaration.Register(ExecuteSearchDeclaration, HandleMenuState);
			Command.NaviBarSearchDeclarationInProject.Register(ExecuteSearchDeclarationInProject, HandleMenuState);
			Command.NaviBarSearchActiveClass.Register(ExecuteSearchActiveClass, HandleMenuState);
		}

		static void HandleMenuState(object s, EventArgs args) {
			ThreadHelper.ThrowIfNotOnUIThread();
			((OleMenuCommand)s).Enabled = GetNaviBar() != null;
		}

		static NaviBar.INaviBar GetNaviBar() {
			return TextEditorHelper.GetActiveWpfDocumentView()?.Properties.TryGetProperty(nameof(NaviBar), out NaviBar.INaviBar bar) == true ? bar : null;
		}

		static void ExecuteSearchDeclaration(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			GetNaviBar()?.ShowRootItemMenu((int)Controls.ScopeType.ActiveDocument);
		}
		static void ExecuteSearchDeclarationInProject(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			GetNaviBar()?.ShowRootItemMenu((int)Controls.ScopeType.ActiveProject);
		}
		static void ExecuteSearchActiveClass(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			GetNaviBar()?.ShowActiveItemMenu();
		}
	}
}
