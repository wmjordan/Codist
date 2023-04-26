using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;

namespace Codist.Controls
{
	public class ThemedTextBox : TextBox
	{
		public ThemedTextBox() {
			BorderThickness = new Thickness(0, 0, 0, 1);
			this.ReferenceStyle(VsResourceKeys.TextBoxStyleKey);
		}
	}
}
