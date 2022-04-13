using System;
using Microsoft.VisualStudio.Shell;

namespace Codist.Commands
{
	internal static class ToggleAutoBuildVersionCommand
	{
		public static void Initialize() {
			Command.ToggleAutoBuldVersion.Register(Execute, (s, args) => {
				var c = (OleMenuCommand)s;
				c.Visible = CodistPackage.DTE.Solution.Projects.Count > 0;
				c.Checked = Config.Instance.SupressAutoBuildVersion == false;
			});
		}

		static void Execute(object sender, EventArgs e) {
			var c = (OleMenuCommand)sender;
			c.Checked = !c.Checked;
			Config.Instance.SupressAutoBuildVersion = c.Checked == false;
		}
	}
}
