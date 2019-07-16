using System;
using System.ComponentModel;
using System.Windows.Forms;
using AppHelpers;

namespace Codist.Options
{
	[ToolboxItem(false)]
	public partial class CSharpNaviBarPage : UserControl
	{
		readonly UiLock _UI = new UiLock();
		bool _Loaded;

		public CSharpNaviBarPage() {
			InitializeComponent();
		}
		internal CSharpNaviBarPage(ConfigPage page) : this() {
			_UI.CommonEventAction += () => Config.Instance.FireConfigChangedEvent(Features.NaviBar);
		}

		void CSharpNaviBarPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_FieldValueBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(NaviBarOptions.FieldValue, _AutoPropertyValueBox.Enabled = _FieldValueBox.Checked));
			_AutoPropertyValueBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(NaviBarOptions.AutoPropertyAnnotation, _AutoPropertyValueBox.Checked));
			_ParameterListBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(NaviBarOptions.ParameterList, _ParameterListParamNameBox.Enabled = _ParameterListBox.Checked));
			_ParameterListParamNameBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(NaviBarOptions.ParameterListShowParamName, _ParameterListParamNameBox.Checked));
			_PartialClassBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(NaviBarOptions.PartialClassMember, _PartialClassBox.Checked));
			_RangeHighlightBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(NaviBarOptions.RangeHighlight, _RangeHighlightBox.Checked));
			_RegionBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(NaviBarOptions.RegionOnBar, _StripNonLetterCharactersBox.Enabled = _RegionBox.Checked));
			_RegionItemBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(NaviBarOptions.Region, _InMemberRegionItemBox.Enabled = _RegionItemBox.Checked));
			_InMemberRegionItemBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(NaviBarOptions.RegionInMember, _InMemberRegionItemBox.Checked));
			_StripNonLetterCharactersBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(NaviBarOptions.StripRegionNonLetter, _StripNonLetterCharactersBox.Checked));
			_SyntaxNodesBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(NaviBarOptions.SyntaxDetail, _SyntaxNodesBox.Checked));
			_ToolTipBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(NaviBarOptions.SymbolToolTip, _ToolTipBox.Checked));
			_LineOfCodeBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(NaviBarOptions.LineOfCode, _LineOfCodeBox.Checked));

			Config.Loaded += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				var o = config.NaviBarOptions;
				_FieldValueBox.Checked = _AutoPropertyValueBox.Enabled = o.MatchFlags(NaviBarOptions.FieldValue);
				_AutoPropertyValueBox.Checked = o.MatchFlags(NaviBarOptions.AutoPropertyAnnotation);
				_ParameterListBox.Checked = _ParameterListParamNameBox.Enabled = o.MatchFlags(NaviBarOptions.ParameterList);
				_ParameterListParamNameBox.Checked = o.MatchFlags(NaviBarOptions.ParameterListShowParamName);
				_PartialClassBox.Checked = o.MatchFlags(NaviBarOptions.PartialClassMember);
				_RangeHighlightBox.Checked = o.MatchFlags(NaviBarOptions.RangeHighlight);
				_StripNonLetterCharactersBox.Enabled = _RegionBox.Checked = o.MatchFlags(NaviBarOptions.RegionOnBar);
				_StripNonLetterCharactersBox.Checked = o.MatchFlags(NaviBarOptions.StripRegionNonLetter);
				_RegionItemBox.Checked = _InMemberRegionItemBox.Enabled = o.MatchFlags(NaviBarOptions.Region);
				_InMemberRegionItemBox.Checked = o.MatchFlags(NaviBarOptions.RegionInMember);
				_SyntaxNodesBox.Checked = o.MatchFlags(NaviBarOptions.SyntaxDetail);
				_ToolTipBox.Checked = o.MatchFlags(NaviBarOptions.SymbolToolTip);
				_LineOfCodeBox.Checked = o.MatchFlags(NaviBarOptions.LineOfCode);
			});
		}
	}
}
