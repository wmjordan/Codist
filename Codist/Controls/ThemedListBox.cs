using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;

namespace Codist.Controls
{
	sealed class ThemedListBox : ListBox
	{
		public ThemedListBox() {
			SetResourceReference(StyleProperty, VsResourceKeys.ThemedDialogListBoxStyleKey);
		}

		public override void OnApplyTemplate() {
			this.GetFirstVisualChild<ScrollViewer>()?.ReferenceStyle(VsResourceKeys.ScrollViewerStyleKey);
			base.OnApplyTemplate();
		}
	}
}
