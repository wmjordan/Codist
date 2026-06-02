using System;
using System.Windows;
using Codist.Controls;
using Microsoft.VisualStudio.Shell;

namespace Codist.Commands;

/// <summary>
/// Command handler
/// </summary>
internal static class OptionsWindowCommand
{
	static Options.OptionsWindow __Window;

	public static void Initialize() {
		Command.OptionsWindow.Register(Execute);
	}

	/// <summary>
	/// Shows the tool window when the menu item is clicked.
	/// </summary>
	/// <param name="sender">The event sender.</param>
	/// <param name="e">The event args.</param>
	internal static void Execute(object sender, EventArgs e) {
		ThreadHelper.ThrowIfNotOnUIThread();
		if (__Window is null || !__Window.IsVisible) {
			CreateWindow();
		}
		if (__Window.WindowState == WindowState.Minimized) {
			__Window.WindowState = WindowState.Normal;
		}
		__Window.Show();
	}

	internal static void ShowOptionPage(string name) {
		Execute(null, EventArgs.Empty);
		__Window.ShowOptionPage(name);
	}

	static void CreateWindow() {
		__Window = new Options.OptionsWindow() {
			Width = 760,
			Height = 600,
			Owner = Application.Current.MainWindow
		};
		// stop VS from stealing key strokes (enter, backspace, arrow keys, tab stops, etc.) from the window
		KeystrokeThief.Bind(__Window);
	}
}
