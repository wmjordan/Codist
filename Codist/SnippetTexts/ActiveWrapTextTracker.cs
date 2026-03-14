using Microsoft.VisualStudio.Text.Editor;

namespace Codist.SnippetTexts;

sealed class ActiveWrapTextTracker
{
	public WrapText Active { get; set; }

	public static ActiveWrapTextTracker Get(IWpfTextView textView) {
		return textView.GetOrCreateSingletonProperty<ActiveWrapTextTracker>();
	}
}
