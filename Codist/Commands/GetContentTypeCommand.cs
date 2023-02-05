using System;
using System.ComponentModel.Design;
using System.Text;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;
using AppHelpers;
using R = Codist.Properties.Resources;

namespace Codist.Commands
{
	/// <summary>A command which displays content types of the document.</summary>
	internal static class GetContentTypeCommand
	{
		public static void Initialize() {
			Command.GetContentType.Register(Execute, (s, args) => {
				ThreadHelper.ThrowIfNotOnUIThread();
				((OleMenuCommand)s).Visible = Config.Instance.DeveloperOptions.MatchFlags(DeveloperOptions.ShowDocumentContentType)
					&& TextEditorHelper.GetActiveWpfDocumentView() != null;
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
			var t = docWindow.TextBuffer.ContentType;
			using (var b = ReusableStringBuilder.AcquireDefault(100)) {
				var sb = b.Resource;
				var d = docWindow.TextBuffer.GetTextDocument();
				sb.Append(R.T_ContentTypeOfDocument);
				if (d != null) {
					sb.Append(System.IO.Path.GetFileName(d.FilePath));
				}
				sb.AppendLine()
					.AppendLine();
				var h = new HashSet<IContentType>();
				ShowContentType(t, sb, h, 0);

				sb.AppendLine()
					.Append(R.T_ViewRoles)
					.AppendLine(String.Join(", ", docWindow.Roles));

				CodistPackage.ShowMessageBox(sb.ToString(), nameof(Codist), false);
			}
		}

		static void ShowContentType (IContentType type, StringBuilder sb, HashSet<IContentType> h, int indent) {
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
