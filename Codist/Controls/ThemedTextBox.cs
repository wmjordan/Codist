using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.Shell;

namespace Codist.Controls
{
	public class ThemedTextBox : TextBox
	{
		public ThemedTextBox() {
			BorderThickness = new Thickness(0, 0, 0, 1);
			this.ReferenceStyle(VsResourceKeys.TextBoxStyleKey);
			ContextMenu = new();
			ContextMenuOpening += ThemedTextBox_ContextMenuOpening;
		}

		void ThemedTextBox_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
			var m = ContextMenu;
			if (m.HasItems) {
				m.IsOpen = true;
				return;
			}
			m.PlacementTarget = this;
			m.Resources = SharedDictionaryManager.ContextMenu;
			m.Items.AddRange(
				new MenuItem { Command = ApplicationCommands.Cut, Icon = VsImageHelper.GetImage(IconIds.Cut) },
				new MenuItem { Command = ApplicationCommands.Copy, Icon = VsImageHelper.GetImage(IconIds.Copy) },
				new MenuItem { Command = ApplicationCommands.Paste, Icon = VsImageHelper.GetImage(IconIds.Paste) },
				new Separator(),
				new MenuItem { Command = ApplicationCommands.Delete, Icon = VsImageHelper.GetImage(IconIds.Delete) }
			);
			m.MinWidth = 180;
			m.IsOpen = true;
			e.Handled = true;
		}
	}
}
