using System;
using System.Windows;
using Microsoft.VisualStudio.Shell;

namespace Codist.Commands
{
	/// <summary>A command which performs XSLT on the active code document window.</summary>
	internal static class TransformDocumentCommand
	{
		public static void Initialize() {
			if (CodistPackage.VsVersion.Major < 17) {
				return;
			}
			Command.TransformDocument.Register(Execute, (s, args) => {
				ThreadHelper.ThrowIfNotOnUIThread();
				((OleMenuCommand)s).Visible = TextEditorHelper.GetActiveWpfDocumentView()?.TextBuffer.LikeContentType("markdown") == true;
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
			new TransformDocumentWindow(VsShellHelper.GetActiveProjectInSolutionExplorer(), docWindow.TextSnapshot, doc.FullName) {
				Owner = Application.Current.MainWindow,
				WindowStartupLocation = WindowStartupLocation.CenterOwner
			}.ShowDialog();
		}
	}
}
