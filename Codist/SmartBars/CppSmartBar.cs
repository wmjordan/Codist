using System;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using R = Codist.Properties.Resources;

namespace Codist.SmartBars
{
	sealed class CppSmartBar : SmartBar
	{
		ITextStructureNavigator _TextStructureNavigator;
		public CppSmartBar(IWpfTextView textView, ITextSearchService2 textSearchService) : base(textView, textSearchService) {
			_TextStructureNavigator = ServicesHelper.Instance.TextStructureNavigator.GetTextStructureNavigator(textView.TextBuffer);
			textView.Closed += TextView_Closed;
		}

		protected override BarType Type => BarType.Cpp;

		ToolBar MyToolBar => ToolBar2;

		protected override void AddCommands() {
			base.AddCommands();
			AddCommand(MyToolBar, IconIds.GoToDefinition, R.CMD_GoToDefinition, ctx => {
				TextEditorHelper.ExecuteEditorCommand("Edit.GoToDefinition", GetCurrentWord(ctx.View));
			});
			AddCommand(MyToolBar, IconIds.GoToDeclaration, R.CMD_GoToDeclaration, ctx => {
				TextEditorHelper.ExecuteEditorCommand("Edit.GoToDeclaration", GetCurrentWord(ctx.View));
			});
			AddCommand(MyToolBar, IconIds.FindReference, R.CMD_FindAllReferences, ctx => {
				TextEditorHelper.ExecuteEditorCommand("Edit.FindAllReferences");
			});
			if (IsReadOnly == false) {
				//AddEditorCommand(MyToolBar, KnownImageIds.IntellisenseLightBulb, "EditorContextMenus.CodeWindow.QuickActionsForPosition", "Quick actions for position");
				AddCommentCommand(MyToolBar);
				AddEditorCommand(MyToolBar, IconIds.Uncomment, "Edit.UncommentSelection", R.CMD_UncommentSelection);
			}
		}

		string GetCurrentWord(ITextView view) {
			return _TextStructureNavigator.GetExtentOfWord(view.Selection.Start.Position).Span.GetText();
		}

		void TextView_Closed(object sender, EventArgs e) {
			(sender as ITextView).Closed -= TextView_Closed;
			_TextStructureNavigator = null;
		}
	}
}
