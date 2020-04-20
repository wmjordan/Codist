using System;
using System.ComponentModel.Design;
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
			Command.NaviBarSearchActiveClass.Register(ExecuteSearchActiveClass, HandleMenuState);
		}

		static void HandleMenuState(object s, EventArgs args) {
			ThreadHelper.ThrowIfNotOnUIThread();
			((OleMenuCommand)s).Enabled = GetCSharpBar() != null;
		}

		static NaviBar.INaviBar GetCSharpBar() {
			NaviBar.INaviBar bar = null;
			return TextEditorHelper.GetActiveWpfDocumentView()?.Properties.TryGetProperty(nameof(NaviBar), out bar) == true ? bar : null;
		}

		static void ExecuteSearchDeclaration(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			GetCSharpBar()?.ShowRootItemMenu();
		}
		static void ExecuteSearchActiveClass(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			GetCSharpBar()?.ShowActiveItemMenu();
		}
	}
}
