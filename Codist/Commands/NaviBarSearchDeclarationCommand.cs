using System;
using CLR;
using Microsoft.VisualStudio.Shell;

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
			var m = (OleMenuCommand)s;
			if (m.Visible = Config.Instance.Features.MatchFlags(Features.NaviBar)) {
				m.Enabled = GetNaviBar() != null;
			}
		}

		static NaviBar.INaviBar GetNaviBar() {
			var v = TextEditorHelper.GetActiveWpfDocumentView();
			return v == null || !v.Properties.TryGetProperty(nameof(NaviBar), out NaviBar.INaviBar bar) ? null : bar;
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
