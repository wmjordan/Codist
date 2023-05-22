using System;
using System.Windows;
using System.Windows.Shell;
using CLR;
using R = Codist.Properties.Resources;

namespace Codist.Display
{
	/// <summary>
	/// Enhances jump list with VS startup switches
	/// </summary>
	/// <remarks>Reference: <c>https://github.com/madskristensen/Tweakster</c>.</remarks>
	static class JumpListEnhancer
	{
		public static void Initialize() {
			Config.RegisterUpdateHandler(AddJumpListItems);

			AddJumpListItems(new ConfigUpdatedEventArgs(Config.Instance, Features.JumpList));
		}

		public static void AddJumpListItems(ConfigUpdatedEventArgs args) {
			if (args.UpdatedFeature.MatchFlags(Features.JumpList) == false) {
				return;
			}

			var list = JumpList.GetJumpList(Application.Current) ?? new JumpList();
            list.ShowRecentCategory = true;

			if (args.Config.Features.MatchFlags(Features.JumpList)) {
				var devenv = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
				if (args.Config.JumpListOptions.MatchFlags(JumpListOptions.NoScaling)) {
					AddJumpListItem(list, devenv, R.T_NoScaleMode, R.T_NoScaleModeTip, "/noScale");
				}
				if (args.Config.JumpListOptions.MatchFlags(JumpListOptions.SafeMode)) {
					AddJumpListItem(list, devenv, R.T_SafeMode, R.T_SafeModeTip, "/SafeMode");
				}
				if (args.Config.JumpListOptions.MatchFlags(JumpListOptions.DemonstrationMode)) {
					AddJumpListItem(list, devenv, R.T_PresentationMode, R.T_PresentationModeTip, "/RootSuffix Demo");
				}
			}

			list.Apply();
        }

		static void AddJumpListItem(JumpList list, string appPath, string title, string description, string arguments) {
			list.JumpItems.Add(new JumpTask {
				ApplicationPath = appPath,
				IconResourcePath = appPath,
				Title = title,
				Description = description,
				Arguments = arguments,
			});
		}
	}
}
