using System;
using System.Windows.Input;
using System.Windows.Controls;

namespace Codist.Controls
{
	sealed class QuickInfoTextBox : TextBox
	{
		public QuickInfoTextBox() {
			IsReadOnly = true;
			this.ReferenceStyle(Microsoft.VisualStudio.Shell.VsResourceKeys.TextBoxStyleKey);
		}

		protected override void OnMouseRightButtonUp(MouseButtonEventArgs e) {
			Copy();
			e.Handled = true;
		}
	}
}
