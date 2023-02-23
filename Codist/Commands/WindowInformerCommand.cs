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
	/// <summary>A command which displays information about the active interactive window.</summary>
	internal static class WindowInformerCommand
	{
		public static void Initialize() {
			Command.WindowInformer.Register(Execute, (s, args) => {
				ThreadHelper.ThrowIfNotOnUIThread();
				((OleMenuCommand)s).Visible = Config.Instance.DeveloperOptions.MatchFlags(DeveloperOptions.ShowWindowInformer)
					&& TextEditorHelper.GetActiveWpfInteractiveView() != null;
			});
		}

		static void Execute(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var view = TextEditorHelper.GetActiveWpfInteractiveView();
			if (view == null) {
				return;
			}
			DisplayWindowInfo(view);
		}

		static void DisplayWindowInfo(Microsoft.VisualStudio.Text.Editor.IWpfTextView view) {
			using (var b = ReusableStringBuilder.AcquireDefault(100)) {
				var sb = b.Resource;
				var d = view.TextBuffer.GetTextDocument();
				string fileName;
				if (d != null) {
					sb.AppendLine(fileName = System.IO.Path.GetFileName(d.FilePath))
						.Append(R.T_Folder).AppendLine(System.IO.Path.GetDirectoryName(d.FilePath))
						.Append(R.T_TextEncoding).AppendLine(d.Encoding.EncodingName)
						.Append(R.T_LastSaved).AppendLine(d.LastSavedTime == default ? R.T_NotSaved : d.LastSavedTime.ToLocalTime().ToString())
						.Append(R.T_LastModified).AppendLine(d.LastContentModifiedTime.ToLocalTime().ToString());
				}
				else {
					fileName = null;
				}

				ShowDTEDocumentProperties(sb);

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
				ShowContentType(view.TextBuffer.ContentType, sb, new HashSet<IContentType>(), 0);

				sb.AppendLine()
					.AppendLine(R.T_ViewRoles)
					.AppendLine(String.Join(", ", view.Roles));

				sb.AppendLine()
					.AppendLine(R.T_ViewProperties)
					.AppendLine(GetPropertyString(view.Properties));

				sb.AppendLine()
					.AppendLine(R.T_TextBufferProperties)
					.AppendLine(GetPropertyString(view.TextBuffer.Properties));

				MessageWindow.Show(sb.ToString(),
					fileName == null
						? R.T_DocumentProperties
						: $"{R.T_DocumentProperties} - {fileName}");
			}
		}

		static void ShowDTEDocumentProperties(StringBuilder sb) {
			var doc = CodistPackage.DTE.ActiveDocument;
			if (doc == null) {
				return;
			}
			sb.AppendLine()
				.AppendLine(R.T_ActiveDocumentProperties)
				.Append(R.T_Language).AppendLine(doc.Language)
				.Append(R.T_DocumentKind).AppendLine(doc.Kind)
				.Append(R.T_Type).AppendLine(doc.Type)
				.Append(R.T_DocumentExtenderNames).AppendLine(String.Join(", ", doc.ExtenderNames as string[]))
				.Append(R.T_DocumentExtenderCATID).Append(doc.ExtenderCATID).AppendLine();
			try {
				sb.Append(R.T_ContainingProject).AppendLine(doc.ProjectItem?.ContainingProject?.Name)
					.Append(R.T_ProjectExtenderNames).AppendLine(String.Join(", ", doc.ProjectItem?.ContainingProject?.ExtenderNames as string[]));
			}
			catch (System.Runtime.InteropServices.COMException ex) {
				sb.Append(R.T_ContainingProject).AppendLine(ex.Message);
			}
			sb.Append(R.T_WindowCaption).AppendLine(doc.ActiveWindow.Caption)
				.Append(R.T_WindowKind).AppendLine(doc.ActiveWindow.Kind);

			sb.AppendLine(R.T_ProjectItemProperties);
			var properties = doc.ProjectItem?.Properties;
			if (properties != null) {
				foreach (var item in properties.Enumerate()) {
					sb.Append("* ").Append(item.Key).Append(" = ").Append(item.Value).AppendLine();
				}
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
					return k == v ? $"* {k}" : $"* {k} = {v}";
				}).OrderBy(i => i));
		}
	}
}
