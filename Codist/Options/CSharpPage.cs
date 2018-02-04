using System;
using System.ComponentModel;
using System.Windows.Forms;
using AppHelpers;

namespace Codist.Options
{
	[Browsable(false)]
	public partial class CSharpPage : UserControl
	{
		readonly UiLock _UI = new UiLock();
		bool _Loaded;

		public CSharpPage() {
			InitializeComponent();
		}
		internal CSharpPage(ConfigPage page) : this() {
			_UI.CommonAction += Config.Instance.FireConfigChangedEvent;
		}
		private void CSharpPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_CodeAbstractionsBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.MarkAbstractions = _CodeAbstractionsBox.Checked);
			_DirectivesBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.MarkAbstractions = _DirectivesBox.Checked);
			_SpecialCommentsBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.MarkComments = _SpecialCommentsBox.Checked);
			_TypeDeclarationBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.MarkDeclarations = _TypeDeclarationBox.Checked);
			_LineNumbersBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.MarkLineNumbers = _LineNumbersBox.Checked);
			_CSharpAttributesQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.Attributes, _CSharpAttributesQuickInfoBox.Checked));
			_CSharpBaseTypeQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.BaseType, _CSharpBaseTypeInheritenceQuickInfoBox.Enabled = _CSharpBaseTypeQuickInfoBox.Checked));
			_CSharpBaseTypeInheritenceQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.BaseTypeInheritence, _CSharpBaseTypeInheritenceQuickInfoBox.Checked));
			_CSharpDeclarationQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.Declaration, _CSharpDeclarationQuickInfoBox.Checked));
			_CSharpExtensionMethodQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.ExtensionMethod, _CSharpExtensionMethodQuickInfoBox.Checked));
			_CSharpInterfacesQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.Interfaces, _CSharpInterfaceInheritenceQuickInfoBox.Enabled = _CSharpInterfacesQuickInfoBox.Checked));
			_CSharpInterfaceInheritenceQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.InterfacesInheritence, _CSharpInterfaceInheritenceQuickInfoBox.Checked));
			_CSharpNumberQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.NumericValues,  _CSharpNumberQuickInfoBox.Checked));
			_CSharpStringQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.String, _CSharpStringQuickInfoBox.Checked));
			_CSharpParameterQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.Parameter, _CSharpParameterQuickInfoBox.Checked));

			Config.ConfigUpdated += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				_CodeAbstractionsBox.Checked = config.MarkAbstractions;
				_DirectivesBox.Checked = config.MarkDirectives;
				_SpecialCommentsBox.Checked = config.MarkComments;
				_TypeDeclarationBox.Checked = config.MarkDeclarations;
				_LineNumbersBox.Checked = config.MarkLineNumbers;
				_CSharpAttributesQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.Attributes);
				_CSharpBaseTypeInheritenceQuickInfoBox.Enabled = _CSharpBaseTypeQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseType);
				_CSharpBaseTypeInheritenceQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseTypeInheritence);
				_CSharpDeclarationQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration);
				_CSharpInterfaceInheritenceQuickInfoBox.Enabled = _CSharpInterfacesQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.Interfaces);
				_CSharpInterfaceInheritenceQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfacesInheritence);
				_CSharpExtensionMethodQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.ExtensionMethod);
				_CSharpNumberQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues);
				_CSharpStringQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.String);
				_CSharpParameterQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter);
			});
		}
	}
}
