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
		public const int CommandId = 4130;

		public static readonly Guid CommandSet = new Guid("5EF88028-C0FC-4849-9883-10F4BD2217B3");

		public static void Initialize(AsyncPackage package) {
			var menuItem = new OleMenuCommand(Execute, new CommandID(CommandSet, CommandId));
			menuItem.BeforeQueryStatus += (s, args) => {
				ThreadHelper.ThrowIfNotOnUIThread();
				var c = s as OleMenuCommand;
				c.Enabled = CodistPackage.DTE.ActiveDocument != null;
			};
			CodistPackage.MenuService.AddCommand(menuItem);
		}

		static void Execute(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var doc = CodistPackage.DTE.ActiveDocument;
			if (doc == null) {
				return;
			}
			var docWindow = CodistPackage.Instance.GetActiveWpfDocumentView();
			if (docWindow == null
				|| docWindow.Properties.TryGetProperty<NaviBar.CSharpBar>(nameof(NaviBar), out var bar) == false) {
				return;
			}
			bar.ShowRootItemMenu();
		}
	}
}
