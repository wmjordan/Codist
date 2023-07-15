using System;
using CLR;
using Codist.Controls;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;

namespace Codist.Commands
{
	/// <summary>
	/// Command handler
	/// </summary>
	internal static class SyntaxCustomizerWindowCommand
	{
		static Options.SyntaxHighlightCustomizationWindow _Window;

		public static void Initialize() {
			Command.SyntaxCustomizerWindow.Register(Execute);
		}

		/// <summary>
		/// Shows the tool window when the menu item is clicked.
		/// </summary>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event args.</param>
		internal static void Execute(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			if (Config.Instance.Features.MatchFlags(Features.SyntaxHighlight) == false) {
				MessageWindow.Error(R.T_SyntaxHighlightDisabled, R.CMD_ConfigureSyntaxHighlight);
				return;
			}
			if (_Window == null || _Window.IsVisible == false) {
				var v = TextEditorHelper.GetActiveWpfInteractiveView();
				if (v == null) {
					MessageWindow.Error(R.T_CustomizeSyntaxHighlightNote, R.CMD_ConfigureSyntaxHighlight);
					return;
				}
				CreateWindow(v);
			}
			_Window.Show();

			//// Get the instance number 0 of this tool window. This window is single instance so this instance
			//// is actually the only one.
			//// The last flag is set to true so that if the tool window does not exists it will be created.
			//var window = CodistPackage.Instance.FindToolWindow(typeof(SyntaxCustomizerWindow), 0, true);
			//if ((null == window) || (null == window.Frame)) {
			//	throw new NotSupportedException("Cannot create SyntaxCustomizerWindow");
			//}

			//var windowFrame = (IVsWindowFrame)window.Frame;
			//Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
		}

		static void CreateWindow(Microsoft.VisualStudio.Text.Editor.IWpfTextView v) {
			_Window = new Options.SyntaxHighlightCustomizationWindow(v) {
				Width = 560,
				Height = 600,
				Owner = System.Windows.Application.Current.MainWindow
			};
			// stop VS from stealing key strokes (enter, backspace, arrow keys, tab stops, etc.) from the window
			KeystrokeThief.Bind(_Window);
		}
	}
}
