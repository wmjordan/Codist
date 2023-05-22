using System;
using System.ComponentModel.Composition;
using System.Windows;
using CLR;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Display
{
	/// <summary>
	/// Applies display optimizations to editor windows
	/// </summary>
	[Export(typeof(IWpfTextViewCreationListener))]
	[ContentType(Constants.CodeTypes.Text)]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	sealed class EditorWindowDisplayOptimizer : IWpfTextViewCreationListener
	{
		public void TextViewCreated(IWpfTextView textView) {
			textView.VisualElement.Loaded += TextViewLoaded;
		}

		void TextViewLoaded(object sender, EventArgs args) {
			var e = (FrameworkElement)sender;
			e.Loaded -= TextViewLoaded;
			if (Config.Instance.DisplayOptimizations.MatchFlags(DisplayOptimizations.CodeWindow)) {
				WpfHelper.SetUITextRenderOptions(e, true);
			}
		}
	}
}
