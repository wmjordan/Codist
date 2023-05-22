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
			Controls.KeystrokeThief.Bind(_Window);
			_Window.Closed += UnbindConfig;
			Config.RegisterLoadHandler(RefreshSyntaxCustomizerWindow);
		}

		static void UnbindConfig(object sender, EventArgs e) {
			_Window.Closed -= UnbindConfig;
			Config.UnregisterLoadHandler(RefreshSyntaxCustomizerWindow);
		}

		static void RefreshSyntaxCustomizerWindow(Config config) {
			if (_Window != null && _Window.IsClosing == false && _Window.IsVisible) {
				var b = _Window.RestoreBounds;
				var s = _Window.WindowState;
				_Window.Close();
				if (_Window.IsClosing == false) {
					CreateWindow(TextEditorHelper.GetActiveWpfInteractiveView());
					_Window.Top = b.Top;
					_Window.Left = b.Left;
					_Window.Width = b.Width;
					_Window.Height = b.Height;
					_Window.WindowState = s;
				}
				_Window.Show();
			}
		}
	}
}
