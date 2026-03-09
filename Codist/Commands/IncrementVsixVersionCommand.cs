using System;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using Codist.Controls;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;

namespace Codist.Commands;

internal static class IncrementVsixVersionCommand
{
	public static void Initialize() {
		Command.IncrementVsixVersion.Register(Execute, (s, args) => {
			ThreadHelper.ThrowIfNotOnUIThread();
			((OleMenuCommand)s).Visible = GetSelectedProjectItem() != null;
		});
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	static void Execute(object sender, EventArgs e) {
		var item = GetSelectedProjectItem();
		if (item == null) {
			return;
		}
		if (!item.Saved
			&& MessageWindow.AskYesNo(R.T_DiscardUnsavedChanges.Replace("<NAME>", item.Name), R.T_IncrementVersion) != true) {
			return;
		}

		if (IncrementVersion(item, out string message)) {
			MessageWindow.Info(message, R.T_IncrementVersion);
		}
		else {
			MessageWindow.Error(message, R.T_IncrementVersion);
		}
	}

	internal static bool IncrementVersion(ProjectItem item, out string message) {
		ThreadHelper.ThrowIfNotOnUIThread();
		const string Namespace = "http://schemas.microsoft.com/developer/vsx-schema/2011";
		try {
			var fileName = item.FileNames[0];
			var file = new System.IO.FileInfo(fileName);
			var doc = XDocument.Load(fileName);
			var v = doc.Root.Element(XName.Get("Metadata", Namespace))?.Element(XName.Get("Identity", Namespace))?.Attribute("Version");
			if (v != null && Version.TryParse(v.Value, out var ver)) {
				v.Value = new Version(Math.Max(ver.Major, 1), Math.Max(ver.Minor, 0), Math.Max(ver.Build, 0), Math.Max(ver.Revision, 0) + 1).ToString();
				doc.Save(fileName);
				message = "Incremented VSIX manifest version to " + v.Value;
				return true;
			}

			message = "Version not found or version number invalid in file " + fileName;
		}
		catch (Exception ex) {
			message = ex.Message;
		}
		return false;
	}

	static ProjectItem GetSelectedProjectItem() {
		return VsShellHelper.GetFirstSelectedItemInSolutionExplorer<ProjectItem>(IsVsixManifest);
	}

	[SuppressMessage("Usage", Suppression.VSTHRD010, Justification = Suppression.CheckedInCaller)]
	static bool IsVsixManifest(ProjectItem item) {
		return item.Name.EndsWith(".extension.vsixmanifest", StringComparison.OrdinalIgnoreCase) && item.FileCount > 0;
	}
}
