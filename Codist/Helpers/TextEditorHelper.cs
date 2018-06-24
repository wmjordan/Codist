using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using VsTextView = Microsoft.VisualStudio.TextManager.Interop.IVsTextView;
using VsUserData = Microsoft.VisualStudio.TextManager.Interop.IVsUserData;

namespace Codist
{
	static class TextEditorHelper
	{
		static /*readonly*/ Guid guidIWpfTextViewHost = new Guid("8C40265E-9FDB-4f54-A0FD-EBB72B7D0476");

		public static bool AnyTextChanges(ITextVersion oldVersion, ITextVersion currentVersion) {
			while (oldVersion != currentVersion) {
				if (oldVersion.Changes.Count > 0) {
					return true;
				}

				oldVersion = oldVersion.Next;
			}

			return false;
		}

		public static bool Contains(this TextSpan token, ITextSelection selection, bool inclusive) {
			var start = selection.Start.Position.Position;
			var end = selection.End.Position.Position;
			return token.Contains(start) && (token.Contains(end) || inclusive && token.End == end);
		}

		public static void TryExecuteCommand(this EnvDTE.DTE dte, string command) {
			try {
				if (dte.Commands.Item(command).IsAvailable) {
					dte.ExecuteCommand(command);
				}
			}
			catch (System.Runtime.InteropServices.COMException ex) {
				System.Windows.Forms.MessageBox.Show(ex.ToString());
				if (System.Diagnostics.Debugger.IsAttached) {
					System.Diagnostics.Debugger.Break();
				}
			}
			
		}

		public static void ExecuteEditorCommand(string command) {
			CodistPackage.DTE.TryExecuteCommand(command);
		}

		public static IWpfTextView GetActiveWpfDocumentView(this IServiceProvider service) {
			var doc = CodistPackage.DTE.ActiveDocument;
			if (doc == null) {
				return null;
			}
			var textView = GetIVsTextView(service, doc.FullName);
			return textView == null ? null : GetWpfTextView(textView);
		}

		static VsTextView GetIVsTextView(IServiceProvider service, string filePath) {
			var dte2 = (EnvDTE80.DTE2)Package.GetGlobalService(typeof(SDTE));
			var sp = (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)dte2;
			var serviceProvider = new ServiceProvider(sp);

			IVsWindowFrame windowFrame;
			return VsShellUtilities.IsDocumentOpen(service, filePath, Guid.Empty, out IVsUIHierarchy uiHierarchy, out uint itemID, out windowFrame)
				? VsShellUtilities.GetTextView(windowFrame)
				: null;
		}
		static IWpfTextView GetWpfTextView(VsTextView vTextView) {
			var userData = vTextView as VsUserData;
			if (userData == null) {
				return null;
			}
			var guidViewHost = guidIWpfTextViewHost;
			userData.GetData(ref guidViewHost, out object holder);
			return ((IWpfTextViewHost)holder).TextView;
		}
	}
}
