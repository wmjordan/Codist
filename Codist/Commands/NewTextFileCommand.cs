using System;
using Microsoft.VisualStudio.Shell;

namespace Codist.Commands;

/// <summary>A command which opens a new plain text code document window.</summary>
internal static class NewTextFileCommand
{
	public static void Initialize() {
		Command.NewTextFile.Register(Execute);
	}

	static void Execute(object sender, EventArgs e) {
		ThreadHelper.ThrowIfNotOnUIThread();
		ServicesHelper.Instance.DTE.ItemOperations.NewFile();
	}
}
