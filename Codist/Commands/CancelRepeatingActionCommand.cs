using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Commands
{
	[Export(typeof(ICommandHandler))]
	[Name(nameof(CancelRepeatingActionCommand))]
	[ContentType(Constants.CodeTypes.Markdown)]
	[ContentType("code++.Markdown")]
	[ContentType(Constants.CodeTypes.VsMarkdown)]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	[TextViewRole(PredefinedTextViewRoles.Editable)]
	sealed class CancelRepeatingActionCommand : ICommandHandler<EscapeKeyCommandArgs>, ICommandHandler<UndoCommandArgs>, ICommandHandler<RedoCommandArgs>
	{
		public string DisplayName => nameof(CancelRepeatingActionCommand);

		public CommandState GetCommandState(EscapeKeyCommandArgs args) {
			return CommandState.Unspecified;
		}

		public bool ExecuteCommand(EscapeKeyCommandArgs args, CommandExecutionContext executionContext) {
			return args.TextView.UnregisterRepeatingAction();
		}

		public CommandState GetCommandState(UndoCommandArgs args) {
			return CommandState.Unspecified;
		}

		public bool ExecuteCommand(UndoCommandArgs args, CommandExecutionContext executionContext) {
			return RunCommand(args);
		}

		static bool RunCommand(Microsoft.VisualStudio.Text.Editor.Commanding.EditorCommandArgs args) {
			args.TextView.UnregisterRepeatingAction();
			return false;
		}

		public CommandState GetCommandState(RedoCommandArgs args) {
			return CommandState.Unspecified;	
		}

		public bool ExecuteCommand(RedoCommandArgs args, CommandExecutionContext executionContext) {
			return RunCommand(args);
		}
	}
}
