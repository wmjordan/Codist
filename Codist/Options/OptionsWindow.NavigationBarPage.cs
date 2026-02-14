using System;
using R = Codist.Properties.Resources;

namespace Codist.Options
{
	sealed partial class OptionsWindow
	{
		sealed class NavigationBarPage : OptionPageFactory
		{
			public override string Name => R.T_NavigationBar;
			public override Features RequiredFeature => Features.NaviBar;

			protected override OptionPage CreatePage() {
				return new PageControl();
			}

			sealed class PageControl : OptionPage
			{
				readonly OptionBox<NaviBarOptions> _SyntaxDetail, _SymbolToolTip, _RegionOnBar, _StripRegionNonLetter, _RangeHighlight, _CtrlGoToSource,
					_ParameterList, _ParameterListShowParamName, _FieldValue, _AutoPropertyAsField, _MemberType, _PartialClassMember, _BaseClassMember, _Region, _RegionInMember, _LineOfCode;
				readonly OptionBox<NaviBarOptions>[] _Options;

				public PageControl() {
					var o = Config.Instance.NaviBarOptions;
					SetContents(
						new Note(R.OT_NaviBarNote),

						new TitleBox(R.OT_CSharpNaviBar),
						_SyntaxDetail = o.CreateOptionBox(NaviBarOptions.SyntaxDetail, UpdateConfig, R.OT_SyntaxDetail)
							.SetLazyToolTip(() => R.OT_SyntaxDetailTip),
						_SymbolToolTip = o.CreateOptionBox(NaviBarOptions.SymbolToolTip, UpdateConfig, R.OT_SymbolToolTip)
							.SetLazyToolTip(() => R.OT_SymbolToolTipTip),
						_RegionOnBar = o.CreateOptionBox(NaviBarOptions.RegionOnBar, UpdateConfig, R.OT_Region)
							.SetLazyToolTip(() => R.OT_RegionTip),
						_StripRegionNonLetter = o.CreateOptionBox(NaviBarOptions.StripRegionNonLetter, UpdateConfig, R.OT_TrimNonLetterRegion)
							.SetLazyToolTip(() => R.OT_TrimNonLetterRegionTip),
						_RangeHighlight = o.CreateOptionBox(NaviBarOptions.RangeHighlight, UpdateConfig, R.OT_HighlightSyntaxRange)
							.SetLazyToolTip(() => R.OT_HighlightSyntaxRangeTip),
						_AutoPropertyAsField = o.CreateOptionBox(NaviBarOptions.AutoPropertiesAsFields, UpdateConfig, R.OT_FilterAutoPropertiesAsFields),
						new DescriptionBox(R.OT_FilterAutoPropertiesAsFieldsNote),
						_CtrlGoToSource = o.CreateOptionBox(NaviBarOptions.CtrlGoToSource, UpdateConfig, R.OT_CtrlGoToSource),

						new TitleBox(R.OT_CSharpNaviBarMenu),
						_ParameterList = o.CreateOptionBox(NaviBarOptions.ParameterList, UpdateConfig, R.OT_MethodParameterList)
							.SetLazyToolTip(() => R.OT_MethodParameterListTip),
						_ParameterListShowParamName = o.CreateOptionBox(NaviBarOptions.ParameterListShowParamName, UpdateConfig, R.OT_ShowMethodParameterName)
							.SetLazyToolTip(() => R.OT_ShowMethodParameterNameTip),
						_FieldValue = o.CreateOptionBox(NaviBarOptions.FieldValue, UpdateConfig, R.OT_ValueOfFields)
							.SetLazyToolTip(() => R.OT_ValueOfFieldsTip),
						_MemberType = o.CreateOptionBox(NaviBarOptions.MemberType, UpdateConfig, R.OT_MemberTypes).SetLazyToolTip(() => R.OT_MemberTypesTip),
						_PartialClassMember = o.CreateOptionBox(NaviBarOptions.PartialClassMember, UpdateConfig, R.OT_IncludePartialTypeMembers)
							.SetLazyToolTip(() => R.OT_IncludePartialTypeMembersTip),
						_BaseClassMember = o.CreateOptionBox(NaviBarOptions.BaseClassMember, UpdateConfig, R.OT_IncludeBaseTypeMembers)
							.SetLazyToolTip(() => R.OT_IncludeBaseTypeMembersTip),
						_Region = o.CreateOptionBox(NaviBarOptions.Region, UpdateConfig, R.OT_IncludeRegions)
							.SetLazyToolTip(() => R.OT_IncludeRegionsTip),
						_RegionInMember = o.CreateOptionBox(NaviBarOptions.RegionInMember, UpdateConfig, R.OT_IncludeMemberRegions)
							.SetLazyToolTip(() => R.OT_IncludeMemberRegionsTip),
						_LineOfCode = o.CreateOptionBox(NaviBarOptions.LineOfCode, UpdateConfig, R.OT_LineOfCode)
							.SetLazyToolTip(() => R.OT_LineOfCodeTip),
						new DescriptionBox(R.OT_NaviBarUpdateNote),

						new TitleBox(R.OT_ShortcutKeys),
						new DescriptionBox(R.OT_NaviBarShortcutKeys),
						new DescriptionBox(R.OT_ShortcutKeysNote)
					);
					_Options = new[] { _SyntaxDetail, _SymbolToolTip, _RegionOnBar, _StripRegionNonLetter, _RangeHighlight, _ParameterList, _ParameterListShowParamName, _FieldValue, _AutoPropertyAsField, _MemberType, _PartialClassMember, _BaseClassMember, _Region, _RegionInMember, _LineOfCode };
					SubOptionMargin.ApplyMargin(_StripRegionNonLetter, _ParameterListShowParamName, _RegionInMember);
					_RegionOnBar.BindDependentOptionControls(_StripRegionNonLetter);
					_ParameterList.BindDependentOptionControls(_ParameterListShowParamName);
					_Region.BindDependentOptionControls(_RegionInMember);
				}

				protected override void LoadConfig(Config config) {
					var o = config.NaviBarOptions;
					Array.ForEach(_Options, i => i.UpdateWithOption(o));
				}

				void UpdateConfig(NaviBarOptions options, bool set) {
					if (IsConfigUpdating) {
						return;
					}
					Config.Instance.Set(options, set);
					Config.Instance.FireConfigChangedEvent(Features.NaviBar);
				}
			}
		}
	}
}
