using System;
using System.Windows.Controls;
using R = Codist.Properties.Resources;

namespace Codist.Options
{
	sealed partial class OptionsWindow
	{
		sealed class ExtensionDeveloperPage : OptionPageFactory
		{
			public override string Name => R.OT_ExtensionDevelopment;
			public override Features RequiredFeature => Features.None;

			protected override OptionPage CreatePage() {
				return new PageControl();
			}

			sealed class PageControl : OptionPage
			{
				readonly OptionBox<BuildOptions> _BuildVsixAutoIncrement;
				readonly OptionBox<DeveloperOptions> _ShowActiveWindowProperties, _ShowSyntaxClassificationInfo, _OpenActivityLog;

				public PageControl() {
					var o = Config.Instance.SpecialHighlightOptions;
					SetContents(new Note(R.OT_ExtensionNote),

						new TitleBox(R.OT_SyntaxDiagnostics),
						new DescriptionBox(R.OT_SyntaxDiagnosticsNote),
						_ShowSyntaxClassificationInfo = Config.Instance.DeveloperOptions.CreateOptionBox(DeveloperOptions.ShowSyntaxClassificationInfo, UpdateConfig, R.OT_AddShowSyntaxClassifcationInfo)
							.SetLazyToolTip(() => R.OT_AddShowSyntaxClassifcationInfoTip),

						new TitleBox(R.OT_Build),
						_BuildVsixAutoIncrement = Config.Instance.BuildOptions.CreateOptionBox(BuildOptions.VsixAutoIncrement, UpdateConfig, R.OT_AutoIncrementVsixVersion)
							.SetLazyToolTip(() => R.OT_AutoIncrementVsixVersionTip),

						new TitleBox(R.OT_VisualStudio),
						_ShowActiveWindowProperties = Config.Instance.DeveloperOptions.CreateOptionBox(DeveloperOptions.ShowWindowInformer, UpdateConfig, R.OT_AddShowActiveWindowProperties)
							.SetLazyToolTip(() => R.OT_AddShowActiveWindowPropertiesTip),
						_OpenActivityLog = Config.Instance.DeveloperOptions.CreateOptionBox(DeveloperOptions.ShowActivityLog, UpdateConfig, R.OT_AddOpenActivityLog)
					);
				}

				protected override void LoadConfig(Config config) {
					var o = config.BuildOptions;
					_BuildVsixAutoIncrement.UpdateWithOption(o);
				}

				void UpdateConfig(BuildOptions options, bool set) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, set);
					Config.Instance.FireConfigChangedEvent(Features.None);
				}

				void UpdateConfig(DeveloperOptions options, bool set) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, set);
					Config.Instance.FireConfigChangedEvent(Features.None);
				}
			}
		}
	}
}
