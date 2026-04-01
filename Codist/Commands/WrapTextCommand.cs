using System;
using System.Diagnostics.CodeAnalysis;
using CLR;
using Codist.Controls;
using Codist.SnippetTexts;
using Microsoft.VisualStudio.Shell;

namespace Codist.Commands;

static class WrapTextCommand
{
	public static void Initialize() {
		Command.WrapRecentText.Register(Execute, QueryStatus);
		Command.ListWrapText.Register(ExecuteListWrapText, QueryStatus);
	}

	static void QueryStatus(object sender, EventArgs args) {
		ThreadHelper.ThrowIfNotOnUIThread();
		var m = ((OleMenuCommand)sender);
		var view = Config.Instance.Features.MatchFlags(Features.WrapText)
			? TextEditorHelper.GetActiveWpfDocumentView()
			: null;
		if (m.Visible = view != null) {
			m.Enabled = !view.Selection.IsEmpty;
		}
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	static void Execute(object sender, EventArgs e) {
		var view = TextEditorHelper.GetActiveWpfDocumentView();
		if (view is null) {
			return;
		}
		var t = ActiveWrapTextTracker.Get(view);
		(t.Active ??= WrapText.GetDefault()).WrapSelections(view);
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	static void ExecuteListWrapText(object sender, EventArgs e) {
		var view = TextEditorHelper.GetActiveWpfDocumentView();
		if (view is null
			|| TextViewOverlay.Get(view)?.GetFirstVisualChild<WrapTextPicker>() != null) {
			return;
		}

		new WrapTextPicker(view, Config.Instance.WrapTexts).Show();
	}
}
