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
			//_UI.CommonEventAction += Config.Instance.FireConfigChangedEvent;
		}
		private void CSharpPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_DirectivesBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(MarkerOptions.CompilerDirective, _DirectivesBox.Checked));
			_SpecialCommentsBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(MarkerOptions.SpecialComment, _SpecialCommentsBox.Checked));
			_MemberDeclarationBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(MarkerOptions.MemberDeclaration, _LongMethodBox.Enabled = _TypeDeclarationBox.Enabled = _MemberDeclarationBox.Checked));
			_LongMethodBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(MarkerOptions.LongMemberDeclaration, _LongMethodBox.Checked));
			_TypeDeclarationBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(MarkerOptions.TypeDeclaration, _TypeDeclarationBox.Checked));
			_CSharpAttributesQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.Attributes, _CSharpAttributesQuickInfoBox.Checked));
			_CSharpBaseTypeQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.BaseType, _CSharpBaseTypeInheritenceQuickInfoBox.Enabled = _CSharpBaseTypeQuickInfoBox.Checked));
			_CSharpBaseTypeInheritenceQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.BaseTypeInheritence, _CSharpBaseTypeInheritenceQuickInfoBox.Checked));
			_CSharpDeclarationQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.Declaration, _CSharpDeclarationQuickInfoBox.Checked));
			_CSharpExtensionMethodQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.ExtensionMethod, _CSharpExtensionMethodQuickInfoBox.Checked));
			_CSharpInterfacesQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.Interfaces, _CSharpInterfaceInheritenceQuickInfoBox.Enabled = _CSharpInterfacesQuickInfoBox.Checked));
			_CSharpInterfaceImplementationsQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.InterfaceImplementations, _CSharpInterfaceImplementationsQuickInfoBox.Checked));
			_CSharpInterfaceInheritenceQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.InterfacesInheritence, _CSharpInterfaceInheritenceQuickInfoBox.Checked));
			_CSharpNumberQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.NumericValues,  _CSharpNumberQuickInfoBox.Checked));
			_CSharpStringQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.String, _CSharpStringQuickInfoBox.Checked));
			_CSharpParameterQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.Parameter, _CSharpParameterQuickInfoBox.Checked));
			_CSharpTypeParameterQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.TypeParameters, _CSharpTypeParameterQuickInfoBox.Checked));
			_HighlightDeclarationBracesBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SpecialHighlightOptions.DeclarationBrace, _HighlightDeclarationBracesBox.Checked));

			Config.Loaded += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				_DirectivesBox.Checked = config.MarkerOptions.MatchFlags(MarkerOptions.CompilerDirective);
				_SpecialCommentsBox.Checked = config.MarkerOptions.MatchFlags(MarkerOptions.SpecialComment);
				_MemberDeclarationBox.Checked = _LongMethodBox.Enabled = config.MarkerOptions.MatchFlags(MarkerOptions.MemberDeclaration);
				_LongMethodBox.Checked = config.MarkerOptions.MatchFlags(MarkerOptions.LongMemberDeclaration);
				_TypeDeclarationBox.Checked = _LongMethodBox.Enabled = config.MarkerOptions.MatchFlags(MarkerOptions.TypeDeclaration);
				_CSharpAttributesQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.Attributes);
				_CSharpBaseTypeInheritenceQuickInfoBox.Enabled = _CSharpBaseTypeQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseType);
				_CSharpBaseTypeInheritenceQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseTypeInheritence);
				_CSharpDeclarationQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration);
				_CSharpInterfaceInheritenceQuickInfoBox.Enabled = _CSharpInterfacesQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.Interfaces);
				_CSharpInterfaceInheritenceQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfacesInheritence);
				_CSharpInterfaceImplementationsQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfaceImplementations);
				_CSharpExtensionMethodQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.ExtensionMethod);
				_CSharpNumberQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues);
				_CSharpStringQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.String);
				_CSharpParameterQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter);
				_CSharpTypeParameterQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.TypeParameters);
				_HighlightDeclarationBracesBox.Checked = config.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.DeclarationBrace);
			});
		}
	}
}
