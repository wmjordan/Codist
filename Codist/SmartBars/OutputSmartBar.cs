using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Controls;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.SmartBars
{
	sealed class OutputSmartBar : SmartBar
	{
		public OutputSmartBar(IWpfTextView textView, Microsoft.VisualStudio.Text.Operations.ITextSearchService2 textSearchService) : base(textView, textSearchService) {
		}

		ToolBar MyToolBar => ToolBar;

		protected override void AddCommands(CancellationToken cancellationToken) {
			base.AddCommands(cancellationToken);
			string t = TryGetPath(View);
			if (t == null) {
				return;
			}
			if (File.Exists(t)) {
				AddCommand(MyToolBar, KnownImageIds.Open, "Open or execute file", ctx => {
					TryRun(TryGetPath(View));
				});
				AddCommand(MyToolBar, KnownImageIds.OpenFolder, "Open folder", ctx => {
					var s = TryGetPath(View);
					if (s != null) {
						Process.Start("Explorer.exe", "/select,\"" + s + "\"");
					}
				});
				if (IsFileTypeRegisteredInVS(t)) {
					AddCommand(MyToolBar, KnownImageIds.VisualStudio, "Open file with Visual Studio", ctx => {
						var s = TryGetPath(View);
						if (s != null && IsFileTypeRegisteredInVS(s)) {
							CodistPackage.DTE.OpenFile(s, 1, 1);
						}
					});
				}
			}
			else if (Directory.Exists(t)) {
				AddCommand(MyToolBar, KnownImageIds.OpenFolder, "Open folder", ctx => {
					TryRun(TryGetPath(View));
				});
			}

			string TryGetPath(ITextView view) {
				if (view.TryGetFirstSelectionSpan(out var span) && span.Length < 255) {
					var text = view.TextSnapshot.GetText(span).TrimStart();
					try {
						if (Path.IsPathRooted(text)) {
							return text.Replace(@"\\", @"\");
						}
					}
					catch (ArgumentException) {
						// ignore
					}
				}
				return null;
			}
			void TryRun(string path) {
				if (path == null) {
					return;
				}
				try {
					Process.Start(path);
				}
				catch (System.ComponentModel.Win32Exception ex) {
					CodistPackage.ShowErrorMessageBox(ex.Message, null, true);
				}
				catch (FileNotFoundException) {
					// ignore
				}
			}
			bool IsFileTypeRegisteredInVS(string fileName) {
				try {
					return ServicesHelper.Instance.FileExtensionRegistry.GetContentTypeForExtension(Path.GetExtension(fileName)).TypeName != "UNKNOWN";
				}
				catch (ArgumentException) {
					return false;
				}
			}
		}
	}
}
