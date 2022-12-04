using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;

namespace Codist.Commands
{
	static class CommandRegistry
	{
		static readonly Guid CommandSetGuid = Guid.Parse("D668A130-CB52-4143-B389-55560823F3D6");

		public static CommandID GetID(this Command command) {
			return new CommandID(CommandSetGuid, (int)command);
		}

		public static void Register(this Command command, EventHandler commandHandler, EventHandler queryStatusHandler = null) {
			var cmd = new OleMenuCommand(commandHandler, command.GetID());
			if (queryStatusHandler != null) {
				cmd.BeforeQueryStatus += queryStatusHandler;
			}
			CodistPackage.MenuService.AddCommand(cmd);
		}
	}

	internal enum Command
	{
		CodeWindowScreenshot = 0x4001,
		GetContentType,
		IncrementVsixVersion,
		SymbolFinderWindow,
		SyntaxCustomizerWindow,
		NaviBarSearchDeclaration,
		NaviBarSearchActiveClass,
		NaviBarSearchDeclarationInProject,
		AutoBuildVersionWindow,
		ToggleAutoBuldVersion,
		CodeRefactoring,
	}
}
