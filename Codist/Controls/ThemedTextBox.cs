using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.Shell;

namespace Codist.Controls
{
	public class ThemedTextBox : TextBox
	{
		readonly KeystrokeThief _thief;
		public ThemedTextBox() {
			BorderThickness = new Thickness(0, 0, 0, 1);
			this.ReferenceStyle(VsResourceKeys.TextBoxStyleKey);
		}
		public ThemedTextBox(bool captureFocus)
			: this() {
			if (captureFocus == false) {
				return;
			}
			_thief = new KeystrokeThief(CodistPackage.OleComponentManager);
		}
		protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) {
			base.OnGotKeyboardFocus(e);

			if (_thief != null && _thief.IsStealing == false && IsKeyboardFocusWithin) {
				_thief.StartStealing();
			}
		}

		protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) {
			base.OnLostKeyboardFocus(e);

			if (_thief != null && _thief.IsStealing) {
				_thief.StopStealing();
			}
		}
	}
}
