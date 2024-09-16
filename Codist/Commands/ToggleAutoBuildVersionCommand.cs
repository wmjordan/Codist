using System;
using Microsoft.VisualStudio.Shell;

namespace Codist.Commands
{
	internal static class ToggleAutoBuildVersionCommand
	{
		public static void Initialize() {
			Command.ToggleAutoBuildVersion.Register(Execute, (s, args) => {
				ThreadHelper.ThrowIfNotOnUIThread();
				var c = (OleMenuCommand)s;
				c.Visible = CodistPackage.DTE.Solution.Projects.Count > 0;
				c.Checked = Config.Instance.SuppressAutoBuildVersion == false;
			});
		}

		static void Execute(object sender, EventArgs e) {
			var c = (OleMenuCommand)sender;
			c.Checked = !c.Checked;
			Config.Instance.SuppressAutoBuildVersion = c.Checked == false;
		}
	}
}
