using System;
using System.Diagnostics.CodeAnalysis;
using Codist.SnippetTexts;
using Microsoft.VisualStudio.Shell;

namespace Codist.Commands;

static class RepeatWrapTextCommand
{
	public static void Initialize() {
		Command.WrapRecentText.Register(Execute, (s, args) => {
			ThreadHelper.ThrowIfNotOnUIThread();
			var m = ((OleMenuCommand)s);
			var view = TextEditorHelper.GetActiveWpfDocumentView();
			if (m.Visible = view != null) {
				m.Enabled = !view.Selection.IsEmpty;
			}
		});
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	static void Execute(object sender, EventArgs e) {
		var view = TextEditorHelper.GetActiveWpfDocumentView();
		if (view is null) {
			return;
		}
		if (!view.TryGetProperty(out WrapText wrapText)) {
			wrapText = WrapText.GetDefault();
		}
		wrapText.WrapSelections(view);
	}
}
