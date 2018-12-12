using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.IO;

namespace Codist.SmartBars
{
	sealed class OutputSmartBar : SmartBar
	{
		public OutputSmartBar(IWpfTextView textView, Microsoft.VisualStudio.Text.Operations.ITextSearchService2 textSearchService) : base(textView, textSearchService) {
		}

		ToolBar MyToolBar => ToolBar;

		protected override void AddCommands(CancellationToken cancellationToken) {
			base.AddCommands(cancellationToken);
			try {
				if (View.TryGetFirstSelectionSpan(out var span) && span.Length < 255) {
					var t = View.TextSnapshot.GetText(span);
					if (Path.IsPathRooted(t) && (File.Exists(t) || Directory.Exists(t))) {
						AddCommand(MyToolBar, KnownImageIds.OpenFolder, "Open file in Windows Explorer", ctx => {
							if (ctx.View.TryGetFirstSelectionSpan(out var s) && s.Length < 255) {
								var p = View.TextSnapshot.GetText(s);
								System.Diagnostics.Process.Start("Explorer.exe", "/select,\"" + t + "\"");
							}
						});
					}
				}
			}
			catch (ArgumentException) {
				// ignore
			}
		}
	}
}
