using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;

namespace Codist.Controls
{
	public class ThemedRichTextBox : RichTextBox
	{
		public ThemedRichTextBox(bool readOnly) {
			BorderThickness = WpfHelper.NoMargin;
			Background = ThemeHelper.DocumentPageBrush;
			Foreground = ThemeHelper.DocumentTextBrush;
			FontFamily = ThemeHelper.CodeTextFont;
			IsDocumentEnabled = true;
			IsReadOnly = readOnly;
			IsReadOnlyCaretVisible = true;
			AcceptsReturn = !readOnly;
			ApplyTemplate();
			this.GetFirstVisualChild<ScrollViewer>().ReferenceStyle(VsResourceKeys.ScrollViewerStyleKey);
		}
	}
}
