using System;
using System.IO;
using System.Windows.Documents;
using System.Xml.Linq;
using CLR;
using Codist.Controls;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;

namespace Codist.Commands
{
	/// <summary>A command which opens ActivityLog.xml.</summary>
	internal static class OpenActivityLogCommand
	{
		public static void Initialize() {
			Command.OpenActivityLog.Register(Execute, (s, args) => {
				ThreadHelper.ThrowIfNotOnUIThread();
				((OleMenuCommand)s).Visible = Config.Instance.DeveloperOptions.MatchFlags(DeveloperOptions.ShowActivityLog);
			});
		}

		static void Execute(object sender, EventArgs e) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var root = CodistPackage.DTE.RegistryRoot;
			int i;
			string verDir;
			if (String.IsNullOrEmpty(root)
				|| (i = root.LastIndexOf(Path.DirectorySeparatorChar)) == -1
				|| String.IsNullOrEmpty(verDir = root.Substring(i + 1))) {
				MessageWindow.Error(R.T_CouldNotDetermineVSDataDir, "ActivityLog");
				return;
			}
			var activityLogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\VisualStudio", verDir, "ActivityLog.xml");
			if (File.Exists(activityLogPath) == false) {
				MessageWindow.Error(R.T_ActivityLogNotExists, "ActivityLog");
				return;
			}

			try {
				TextEditorHelper.OpenFile(activityLogPath);
				ShowWarningOrErrorInLog(activityLogPath);
			}
			catch (Exception ex) {
				MessageWindow.Error(ex, "ActivityLog", null, new Source());
			}
		}

		static void ShowWarningOrErrorInLog(string activityLogPath) {
			var doc = XDocument.Load(activityLogPath);
			var box = new ThemedRichTextBox(true);
			var d = box.Document;
			var f = SymbolFormatter.Instance;
			string t;
			foreach (var entry in doc.Root.Elements("entry")) {
				t = entry.Element("type")?.Value;
				if (String.IsNullOrEmpty(t) || t == "Information") {
					continue;
				}
				var err = t == "Error";
				var p = new Paragraph { Margin = WpfHelper.MiddleTopMargin }.Append(t, true, err ? f.Keyword : f.Class);

				t = entry.Element("source")?.Value;
				if (String.IsNullOrEmpty(t) == false) {
					p.Append(" : " + t, false, f.Property);
				}

				t = entry.Element("time")?.Value;
				if (String.IsNullOrEmpty(t) == false) {
					p.Append(" (" + t + ")", false, SymbolFormatter.SemiTransparent.Number);
				}

				d.Blocks.Add(p);

				p = new Paragraph { Margin = WpfHelper.MiddleHorizontalMargin };
				var tf = err ? f : SymbolFormatter.SemiTransparent;

				t = entry.Element("description")?.Value;
				if (String.IsNullOrEmpty(t) == false) {
					p.Append(t, false, tf.PlainText).Append(new LineBreak());
				}

				t = entry.Element("errorinfo")?.Value;
				if (String.IsNullOrEmpty(t) == false) {
					p.Append(t, false, tf.PlainText).Append(new LineBreak());
				}

				if (p.Inlines.Count != 0) {
					d.Blocks.Add(p);
				}
			}
			if (d.Blocks.Count == 0) {
				d.Blocks.Add(new Paragraph().Append(R.T_NothingInterestingInActivityLog));
			}
			MessageWindow.Show(box, activityLogPath);
		}

		struct Source { }
	}
}
