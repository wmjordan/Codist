using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using CLR;
using Codist.Controls;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Commands
{
	static class ShowSupportedFileTypesCommand
	{
		public static void Initialize() {
			Command.ShowSupportedFileTypes.Register(Execute, (s, args) => {
				ThreadHelper.ThrowIfNotOnUIThread();
				((OleMenuCommand)s).Visible = Config.Instance.DeveloperOptions.MatchFlags(DeveloperOptions.ShowSupportedFileTypes);
			});
		}

		static void Execute(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var tb = new ThemedRichTextBox(true);
			var r = ServicesHelper.Instance.FileToContentType;
			var sortedExts = new SortedDictionary<string, IContentType>();
			foreach (var type in ServicesHelper.Instance.ContentTypeRegistry.ContentTypes) {
				foreach (var item in r.GetExtensionsForContentType(type)) {
					sortedExts[item] = type;
				}
			}
			var tab = new Table();
			tab.Columns.Add(new TableColumn { Width = new GridLength(150) });
			tab.Columns.Add(new TableColumn { Width = new GridLength(150) });
			tab.Columns.Add(new TableColumn());
			var rows = new TableRowGroup();
			tab.RowGroups.Add(rows);
			rows.Rows.Add(new TableRow {
				Cells = {
					new TableCell(new Paragraph().Append("File Extension", true)),
					new TableCell(new Paragraph().Append("ContentType", true)),
					new TableCell(new Paragraph().Append("ContentType.BaseTypes", true)),
				}
			});
			foreach (var item in sortedExts) {
				AddRow(rows.Rows, item.Key, item.Value);
			}
			tb.Document.Blocks.Add(tab);
			MessageWindow.Show(tb, "Registered File Extensions");
		}

		static void AddRow(TableRowCollection rows, string extension, IContentType contentType) {
			rows.Add(new TableRow() {
				Cells = {
					new TableCell(new Paragraph().Append(extension, true)),
					new TableCell(new Paragraph().Append(contentType.TypeName == contentType.DisplayName ? contentType.TypeName : $"{contentType.DisplayName}({contentType.TypeName})")),
					new TableCell(new Paragraph().Append(String.Join(", ", contentType.BaseTypes.Select(i => i.DisplayName))))
				}
			});
		}
	}
}
