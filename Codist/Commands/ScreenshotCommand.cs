using System;
using Codist.Controls;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;

namespace Codist.Commands
{
	/// <summary>A command which takes screenshot of the active code document window.</summary>
	internal static class ScreenshotCommand
	{
		public static void Initialize() {
			Command.CodeWindowScreenshot.Register(Execute, (s, args) => {
				ThreadHelper.ThrowIfNotOnUIThread();
				((OleMenuCommand)s).Visible = TextEditorHelper.GetActiveWpfDocumentView() != null;
			});
		}

		static void Execute(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var doc = CodistPackage.DTE.ActiveDocument;
			if (doc == null) {
				return;
			}
			var docWindow = TextEditorHelper.GetActiveWpfDocumentView();
			if (docWindow == null) {
				return;
			}
			using (var f = new System.Windows.Forms.SaveFileDialog {
				Filter = R.T_PngFileFilter,
				AddExtension = true,
				Title = R.T_SpecifyScreenshotLocation,
				FileName = System.IO.Path.GetFileNameWithoutExtension(doc.Name) + ".png"
			}) {
				if (f.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
					try {
						var g = docWindow.VisualElement.GetParent<System.Windows.Controls.Grid>();
						WpfHelper.ScreenShot(g, f.FileName, (int)g.ActualWidth, (int)g.ActualHeight);
					}
					catch (Exception ex) {
						MessageWindow.Error(ex, R.T_FailedToSaveScreenshot.Replace("<NAME>", doc.Name), null, new Source());
					}
				}
			}
		}
		struct Source { }
	}
}
