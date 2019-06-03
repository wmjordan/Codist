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
		public const int CommandSearchDeclarationId = 4130;
		public const int CommandSearchActiveClassId = 4131;

		public static readonly Guid CommandSet = new Guid("5EF88028-C0FC-4849-9883-10F4BD2217B3");

		public static void Initialize(AsyncPackage package) {
			var menuItem = new OleMenuCommand(ExecuteSearchDeclaration, new CommandID(CommandSet, CommandSearchDeclarationId));
			menuItem.BeforeQueryStatus += HandleMenuState;
			CodistPackage.MenuService.AddCommand(menuItem);
			menuItem = new OleMenuCommand(ExecuteSearchActiveClass, new CommandID(CommandSet, CommandSearchActiveClassId));
			menuItem.BeforeQueryStatus += HandleMenuState;
			CodistPackage.MenuService.AddCommand(menuItem);
		}

		static void HandleMenuState(object s, EventArgs args) {
			ThreadHelper.ThrowIfNotOnUIThread();
			(s as OleMenuCommand).Enabled = GetCSharpBar() != null;
		}

		static NaviBar.CSharpBar GetCSharpBar() {
			NaviBar.CSharpBar bar = null;
			return TextEditorHelper.GetActiveWpfDocumentView()?.Properties.TryGetProperty<NaviBar.CSharpBar>(nameof(NaviBar), out bar) == true ? bar : null;
		}

		static void ExecuteSearchDeclaration(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			GetCSharpBar()?.ShowRootItemMenu();
		}
		static void ExecuteSearchActiveClass(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			GetCSharpBar()?.ShowActiveClassMenu();
		}
	}
}
