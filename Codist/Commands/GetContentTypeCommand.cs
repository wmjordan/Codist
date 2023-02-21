using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppHelpers;
using Codist.Controls;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
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
					&& TextEditorHelper.GetActiveWpfInteractiveView() != null;
			});
		}

		static void Execute(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var view = TextEditorHelper.GetActiveWpfInteractiveView();
			if (view == null) {
				return;
			}
			DisplayDocumentWindowInfo(view);
		}

		static void DisplayDocumentWindowInfo(Microsoft.VisualStudio.Text.Editor.IWpfTextView view) {
			var t = view.TextBuffer.ContentType;
			using (var b = ReusableStringBuilder.AcquireDefault(100)) {
				var sb = b.Resource;
				var d = view.TextBuffer.GetTextDocument();
				if (d != null) {
					sb.AppendLine(System.IO.Path.GetFileName(d.FilePath))
						.Append(R.T_Folder).AppendLine(System.IO.Path.GetDirectoryName(d.FilePath))
						.Append(R.T_TextEncoding).AppendLine(d.Encoding.EncodingName)
						.Append(R.T_LastSaved).AppendLine(d.LastSavedTime == default ? R.T_NotSaved : d.LastSavedTime.ToLocalTime().ToString())
						.Append(R.T_LastModified).AppendLine(d.LastContentModifiedTime.ToLocalTime().ToString());
				}
				sb.AppendLine()
					.Append(R.T_LineCount)
					.AppendLine(view.TextSnapshot.LineCount.ToText())
					.Append(R.T_CharacterCount)
					.AppendLine(view.TextSnapshot.Length.ToText());

				sb.AppendLine()
					.Append(R.T_Selection)
					.AppendLine($"{view.Selection.Start.Position.Position}-{view.Selection.End.Position.Position}")
					.Append(R.T_SelectionLength)
					.AppendLine(view.Selection.SelectedSpans.Sum(i => i.Length).ToString())
					.Append(R.T_Caret)
					.AppendLine(view.Caret.Position.BufferPosition.Position.ToString());

				sb.AppendLine().AppendLine(R.T_ContentTypeOfDocument);
				ShowContentType(t, sb, new HashSet<IContentType>(), 0);

				sb.AppendLine()
					.AppendLine(R.T_ViewRoles)
					.AppendLine(String.Join(", ", view.Roles));

				sb.AppendLine()
					.AppendLine(R.T_ViewProperties)
					.AppendLine(GetPropertyString(view.Properties));

				sb.AppendLine()
					.AppendLine(R.T_TextBufferProperties)
					.AppendLine(GetPropertyString(view.TextBuffer.Properties));

				MessageWindow.Show(sb.ToString(), R.T_DocumentProperties);
			}
		}

		static void ShowContentType (IContentType type, StringBuilder sb, HashSet<IContentType> dedup, int indent) {
			sb.Append(' ', indent)
				.Append(type.DisplayName);
			if (type.DisplayName != type.TypeName) {
				sb.Append('(')
					.Append(type.TypeName)
					.Append(')');
			}
			sb.AppendLine();
			foreach (var bt in type.BaseTypes) {
				if (dedup.Add(bt)) {
					ShowContentType(bt, sb, dedup, indent + 2);
				}
			}
		}

		static string GetPropertyString(PropertyCollection properties) {
			return String.Join(Environment.NewLine, properties.PropertyList.Select(i => {
					string k = i.Key.ToString(), v = i.Value?.ToString();
					return k == v ? k : $"{k} = {v}";
				}).OrderBy(i => i));
		}
	}
}
