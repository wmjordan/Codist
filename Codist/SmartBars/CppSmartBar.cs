using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Controls;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

namespace Codist.SmartBars
{
	sealed class CppSmartBar : SmartBar
	{
		readonly ITextStructureNavigator _TextStructureNavigator;
		public CppSmartBar(IWpfTextView textView, ITextSearchService2 textSearchService) : base(textView, textSearchService) {
			_TextStructureNavigator = ServicesHelper.Instance.TextStructureNavigator.GetTextStructureNavigator(textView.TextBuffer);
		}

		ToolBar MyToolBar => ToolBar2;

		protected override void AddCommands(CancellationToken cancellationToken) {
			base.AddCommands(cancellationToken);
			AddCommand(MyToolBar, KnownImageIds.GoToDefinition, "Go to definition", ctx => {
				TextEditorHelper.ExecuteEditorCommand("Edit.GoToDefinition", GetCurrentWord(ctx.View));
			});
			AddCommand(MyToolBar, KnownImageIds.GoToDeclaration, "Go to declaration", ctx => {
				TextEditorHelper.ExecuteEditorCommand("Edit.GoToDeclaration", GetCurrentWord(ctx.View));
			});
			var mode = CodistPackage.DebuggerStatus;
			if (mode != DebuggerStatus.Running) {
				//AddEditorCommand(MyToolBar, KnownImageIds.IntellisenseLightBulb, "EditorContextMenus.CodeWindow.QuickActionsForPosition", "Quick actions for position");
				AddCommand(MyToolBar, KnownImageIds.CommentCode, "Comment selection\nRight click: Comment line", ctx => {
					if (ctx.RightClick) {
						ctx.View.ExpandSelectionToLine();
					}
					TextEditorHelper.ExecuteEditorCommand("Edit.CommentSelection");
				});
				AddEditorCommand(MyToolBar, KnownImageIds.UncommentCode, "Edit.UncommentSelection", "Uncomment selection");
			}
			else if (mode != DebuggerStatus.Design) {
				AddCommands(MyToolBar, KnownImageIds.BreakpointEnabled, "Debugger...\nLeft click: Toggle breakpoint\nRight click: Debugger menu...", ctx => TextEditorHelper.ExecuteEditorCommand("Debug.ToggleBreakpoint"), GetDebugCommands);
			}
		}

		CommandItem[] GetDebugCommands(CommandContext ctx) {
			return new CommandItem[] {
				new CommandItem(KnownImageIds.Watch, "Add Watch", c => TextEditorHelper.ExecuteEditorCommand("Debug.AddWatch")),
				new CommandItem(KnownImageIds.Watch, "Add Parallel Watch", c => TextEditorHelper.ExecuteEditorCommand("Debug.AddParallelWatch")),
				new CommandItem(KnownImageIds.DeleteBreakpoint, "Delete All Breakpoints", c => TextEditorHelper.ExecuteEditorCommand("Debug.DeleteAllBreakpoints"))
			};
		}

		string GetCurrentWord(ITextView view) {
			return _TextStructureNavigator.GetExtentOfWord(view.Selection.Start.Position).Span.GetText();
		}
	}
}
