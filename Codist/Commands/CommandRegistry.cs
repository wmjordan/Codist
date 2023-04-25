using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;

namespace Codist.Commands
{
	static class CommandRegistry
	{
		// this value should be the same as the one in guidCodistPackageCmdSet of CodistPackage.vsct
		static readonly Guid __CommandSetGuid = Guid.Parse("D668A130-CB52-4143-B389-55560823F3D6");

		public static CommandID GetID(this Command command) {
			return new CommandID(__CommandSetGuid, (int)command);
		}

		public static void Register(this Command command, EventHandler commandHandler, EventHandler queryStatusHandler = null) {
			CodistPackage.MenuService.AddCommand(new OleMenuCommand(commandHandler, null, queryStatusHandler, command.GetID()));
		}

		public static void Initialize() {
			AutoBuildVersionWindowCommand.Initialize();
			IncrementVsixVersionCommand.Initialize();
			NaviBarSearchDeclarationCommand.Initialize();
			OpenOutputFolderCommand.Initialize();
			ScreenshotCommand.Initialize();
			SemanticContextCommand.Initialize();
			ToggleAutoBuildVersionCommand.Initialize();
			WindowInformerCommand.Initialize();
		}
	}

	internal enum Command
	{
		CodeWindowScreenshot = 0x4001,
		WindowInformer,
		IncrementVsixVersion,
		SymbolFinderWindow,
		SyntaxCustomizerWindow,
		NaviBarSearchDeclaration,
		NaviBarSearchActiveClass,
		NaviBarSearchDeclarationInProject,
		AutoBuildVersionWindow,
		ToggleAutoBuildVersion,
		CodeRefactoring,
		OpenOutputFolder,
		OpenDebugOutputFolder,
		OpenReleaseOutputFolder,
	}
}
