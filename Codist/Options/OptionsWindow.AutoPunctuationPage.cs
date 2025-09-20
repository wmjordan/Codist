using System;
using System.Windows.Controls;
using R = Codist.Properties.Resources;

namespace Codist.Options
{
	sealed partial class OptionsWindow
	{
		sealed class AutoPunctuation : OptionPageFactory
		{
			public override string Name => R.T_AutoSurround;
			public override Features RequiredFeature => Features.AutoSurround;

			protected override OptionPage CreatePage() {
				return new PageControl();
			}

			sealed class PageControl : OptionPage
			{
				readonly OptionBox<PunctuationOptions> _TrimSelection, _MethodParentheses, _ShowParameterInfo;

				public PageControl() {
					var o = Config.Instance.PunctuationOptions;
					SetContents(new Note(R.OT_AutoSurroundSelectionNote),

						new TitleBox(R.OT_AllLanguages),
						o.CreateOptionBox(PunctuationOptions.Trim, UpdateConfig, R.OT_TrimBeforeSurround)
							.SetLazyToolTip(() => R.OT_TrimBeforeSurroundTip)
							.Set(ref _TrimSelection),

						new TitleBox(R.OT_CSharp),
						o.CreateOptionBox(PunctuationOptions.MethodParentheses, UpdateConfig, R.OT_AppendParenthesesOnMethodName).Set(ref _MethodParentheses),
						o.CreateOptionBox(PunctuationOptions.ShowParameterInfo, UpdateConfig, R.OT_ShowParameterInfo)
							.SetLazyToolTip(() => R.OT_ShowParameterInfoTip)
							.Set(ref _ShowParameterInfo)
					);

					_ShowParameterInfo.WrapMargin(SubOptionMargin);
					_MethodParentheses.BindDependentOptionControls(_ShowParameterInfo);
				}

				protected override void LoadConfig(Config config) {
					var o = config.PunctuationOptions;
					_TrimSelection.UpdateWithOption(o);
					_MethodParentheses.UpdateWithOption(o);
					_ShowParameterInfo.UpdateWithOption(o);
				}

				void UpdateConfig(PunctuationOptions options, bool set) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, set);
					Config.Instance.FireConfigChangedEvent(Features.AutoSurround);
				}
			}
		}
	}
}
