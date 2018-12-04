using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
