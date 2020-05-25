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
			AddCommand(MyToolBar, IconIds.GoToDefinition, "Go to definition", ctx => {
				TextEditorHelper.ExecuteEditorCommand("Edit.GoToDefinition", GetCurrentWord(ctx.View));
			});
			AddCommand(MyToolBar, IconIds.GoToDeclaration, "Go to declaration", ctx => {
				TextEditorHelper.ExecuteEditorCommand("Edit.GoToDeclaration", GetCurrentWord(ctx.View));
			});
			var mode = CodistPackage.DebuggerStatus;
			if (mode != DebuggerStatus.Running) {
				//AddEditorCommand(MyToolBar, KnownImageIds.IntellisenseLightBulb, "EditorContextMenus.CodeWindow.QuickActionsForPosition", "Quick actions for position");
				AddCommand(MyToolBar, IconIds.Comment, "Comment selection\nRight click: Comment line", ctx => {
					if (ctx.RightClick) {
						ctx.View.ExpandSelectionToLine();
					}
					TextEditorHelper.ExecuteEditorCommand("Edit.CommentSelection");
				});
				AddEditorCommand(MyToolBar, IconIds.Uncomment, "Edit.UncommentSelection", "Uncomment selection");
			}
			else if (mode != DebuggerStatus.Design) {
				AddCommands(MyToolBar, IconIds.ToggleBreakpoint, "Debugger...\nLeft click: Toggle breakpoint\nRight click: Debugger menu...", ctx => TextEditorHelper.ExecuteEditorCommand("Debug.ToggleBreakpoint"), ctx => DebugCommands);
			}
		}

		string GetCurrentWord(ITextView view) {
			return _TextStructureNavigator.GetExtentOfWord(view.Selection.Start.Position).Span.GetText();
		}
	}
}
