using System;
using System.ComponentModel.Design;
using System.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;

namespace Codist.Commands
{
	/// <summary>A command which displays content types of the document.</summary>
	internal static class GetContentTypeCommand
	{
		/// <summary>
		/// Command ID.
		/// </summary>
		public const int CommandId = 4133;

		/// <summary>
		/// Command menu group (command set GUID).
		/// </summary>
		public static readonly Guid CommandSet = new Guid("d668a130-cb52-4143-b389-55560823f3d6");

		public static void Initialize(AsyncPackage package) {
			var menuItem = new OleMenuCommand(Execute, new CommandID(CommandSet, CommandId));
			menuItem.BeforeQueryStatus += (s, args) => {
				ThreadHelper.ThrowIfNotOnUIThread();
				var c = s as OleMenuCommand;
				c.Visible = TextEditorHelper.GetActiveWpfDocumentView() != null;
			};
			CodistPackage.MenuService.AddCommand(menuItem);
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
			var t = docWindow.TextBuffer.ContentType;
			using (var b = ReusableStringBuilder.AcquireDefault(100)) {
				var sb = b.Resource;
				var d = docWindow.TextBuffer.GetTextDocument();
				sb.Append("Content type of document ");
				if (d != null) {
					sb.Append(System.IO.Path.GetFileName(d.FilePath));
				}
				sb.AppendLine();
				sb.AppendLine();
				var h = new HashSet<IContentType>();
				ShowContentType(t, sb, h, 0);
				System.Windows.Forms.MessageBox.Show(sb.ToString(), nameof(Codist));
			}

			void ShowContentType (IContentType type, StringBuilder sb, HashSet<IContentType> h, int indent) {
				sb.Append(' ', indent)
					.Append(type.DisplayName);
				if (type.DisplayName != type.TypeName) {
					sb.Append('(')
					.Append(type.TypeName)
					.Append(')');
				}
				sb.AppendLine();
				foreach (var bt in type.BaseTypes) {
					if (h.Add(bt)) {
						ShowContentType(bt, sb, h, indent + 2);
					}
				}
			}
		}
	}
}
