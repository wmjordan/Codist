using System;
using R = Codist.Properties.Resources;

namespace Codist.Options
{
	sealed partial class OptionsWindow
	{
		sealed class SmartBarPage : OptionPageFactory
		{
			public override string Name => R.T_SmartBar;
			public override Features RequiredFeature => Features.SmartBar;

			protected override OptionPage CreatePage() {
				return new PageControl();
			}

			sealed class PageControl : OptionPage
			{
				readonly OptionBox<SmartBarOptions> _ShiftToggleDisplay, _ManualDisplaySmartBar, _CtrlSuppression, _UnderscoreBold, _UnderscoreItalic, _DoubleIndentRefactoring;
				readonly OptionBox<SmartBarOptions>[] _Options;

				public PageControl() {
					var o = Config.Instance.SmartBarOptions;
					SetContents(
						new Note(R.OT_BehaviorTip),
						_CtrlSuppression = o.CreateOptionBox(SmartBarOptions.CtrlSuppressDisplay, UpdateConfig, R.OT_CtrlSuppressSmartBar)
							.SetLazyToolTip(() => R.OT_CtrlSuppressSmartBarTip),
						_ManualDisplaySmartBar = o.CreateOptionBox(SmartBarOptions.ManualDisplaySmartBar, UpdateConfig, R.OT_ManualSmartBar)
							.SetLazyToolTip(() => R.OT_ManualSmartBarTip),
						_ShiftToggleDisplay = o.CreateOptionBox(SmartBarOptions.ShiftToggleDisplay, UpdateConfig, R.OT_ToggleSmartBar)
							.SetLazyToolTip(() => R.OT_ToggleSmartBarTip),
						new DescriptionBox(R.OT_ToggleSmartBarNote),

						new TitleBox(R.OT_CSharpSmartBar),
						_DoubleIndentRefactoring = o.CreateOptionBox(SmartBarOptions.DoubleIndentRefactoring, UpdateConfig, R.OT_DoubleIndentation)
							.SetLazyToolTip(() => R.OT_DoubleIndentationTip),

						new TitleBox(R.OT_MarkdownSmartBar),
						_UnderscoreBold = o.CreateOptionBox(SmartBarOptions.UnderscoreBold, UpdateConfig, R.OT_BoldPreferUnderscore)
							.SetLazyToolTip(() => R.OT_PreferUnderscoreTip),
						_UnderscoreItalic = o.CreateOptionBox(SmartBarOptions.UnderscoreItalic, UpdateConfig, R.OT_ItalicPreferUnderscore)
							.SetLazyToolTip(() => R.OT_PreferUnderscoreTip)
					);
					_Options = [_ShiftToggleDisplay, _ManualDisplaySmartBar, _CtrlSuppression, _DoubleIndentRefactoring, _UnderscoreBold, _UnderscoreItalic];
				}

				void UpdateConfig(SmartBarOptions options, bool set) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, set);
					Config.Instance.FireConfigChangedEvent(Features.SmartBar);
				}

				protected override void LoadConfig(Config config) {
					var o = config.SmartBarOptions;
					Array.ForEach(_Options, i => i.UpdateWithOption(o));
				}
			}
		}
	}
}
