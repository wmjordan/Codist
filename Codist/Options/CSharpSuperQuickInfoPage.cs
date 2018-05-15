using System;
using System.ComponentModel;
using System.Windows.Forms;
using AppHelpers;

namespace Codist.Options
{
	[Browsable(false)]
	public partial class CSharpSuperQuickInfoPage : UserControl
	{
		readonly UiLock _UI = new UiLock();
		bool _Loaded;

		public CSharpSuperQuickInfoPage() {
			InitializeComponent();
		}
		internal CSharpSuperQuickInfoPage(ConfigPage page) : this() {
			//_UI.CommonEventAction += Config.Instance.FireConfigChangedEvent;
		}

		void CSharpSuperQuickInfoPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_ClickAndGoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.ClickAndGo, _ClickAndGoBox.Checked));
			_CSharpAttributesQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.Attributes, _CSharpAttributesQuickInfoBox.Checked));
			_CSharpBaseTypeQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.BaseType, _CSharpBaseTypeInheritenceQuickInfoBox.Enabled = _CSharpBaseTypeQuickInfoBox.Checked));
			_CSharpBaseTypeInheritenceQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.BaseTypeInheritence, _CSharpBaseTypeInheritenceQuickInfoBox.Checked));
			_CSharpOverrideDefaultXmlDocBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.OverrideDefaultDocumentation, _CSharpDocumentationBaseTypeBox.Enabled = _CSharpTextOnlyDocBox.Enabled = _CSharpReturnsDocBox.Enabled = _CSharpOverrideDefaultXmlDocBox.Checked));
			_CSharpDeclarationQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.Declaration, _CSharpDeclarationQuickInfoBox.Checked));
			_CSharpSymbolLocationQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.SymbolLocation, _CSharpSymbolLocationQuickInfoBox.Checked));
			_CSharpInterfacesQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.Interfaces, _CSharpInterfaceInheritenceQuickInfoBox.Enabled = _CSharpInterfacesQuickInfoBox.Checked));
			_CSharpInterfaceImplementationsQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.InterfaceImplementations, _CSharpInterfaceImplementationsQuickInfoBox.Checked));
			_CSharpInterfaceInheritenceQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.InterfacesInheritence, _CSharpInterfaceInheritenceQuickInfoBox.Checked));
			_CSharpNumberQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.NumericValues,  _CSharpNumberQuickInfoBox.Checked));
			_CSharpStringQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.String, _CSharpStringQuickInfoBox.Checked));
			_CSharpParameterQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.Parameter, _CSharpParameterQuickInfoBox.Checked));
			_CSharpTypeParameterQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.TypeParameters, _CSharpTypeParameterQuickInfoBox.Checked));
			_CSharpDocumentationBaseTypeBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.DocumentationFromBaseType, _CSharpDocumentationBaseTypeBox.Checked));
			_CSharpReturnsDocBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.ReturnsDoc, _CSharpReturnsDocBox.Checked));
			_CSharpTextOnlyDocBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.TextOnlyDoc, _CSharpTextOnlyDocBox.Checked));
			_QuickInfoMaxWidthBox.ValueChanged += _UI.HandleEvent(() => Config.Instance.QuickInfoMaxWidth = (double)_QuickInfoMaxWidthBox.Value);
			_QuickInfoMaxHeightBox.ValueChanged += _UI.HandleEvent(() => Config.Instance.QuickInfoMaxHeight = (double)_QuickInfoMaxHeightBox.Value);

			Config.Loaded += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				_ClickAndGoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.ClickAndGo);
				_CSharpAttributesQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.Attributes);
				_CSharpBaseTypeQuickInfoBox.Checked = _CSharpBaseTypeInheritenceQuickInfoBox.Enabled = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseType);
				_CSharpBaseTypeInheritenceQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.BaseTypeInheritence);
				_CSharpDeclarationQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.Declaration);
				_CSharpInterfacesQuickInfoBox.Checked = _CSharpInterfaceInheritenceQuickInfoBox.Enabled = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.Interfaces);
				_CSharpInterfaceInheritenceQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfacesInheritence);
				_CSharpInterfaceImplementationsQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.InterfaceImplementations);
				_CSharpSymbolLocationQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.SymbolLocation);
				_CSharpOverrideDefaultXmlDocBox.Checked = _CSharpDocumentationBaseTypeBox.Enabled = _CSharpTextOnlyDocBox.Enabled = _CSharpReturnsDocBox.Enabled = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation);
				_CSharpNumberQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.NumericValues);
				_CSharpStringQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.String);
				_CSharpParameterQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.Parameter);
				_CSharpTypeParameterQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.TypeParameters);
				_CSharpDocumentationBaseTypeBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.DocumentationFromBaseType);
				_CSharpTextOnlyDocBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.TextOnlyDoc);
				_QuickInfoMaxWidthBox.Value = (decimal)(config.QuickInfoMaxWidth >= 0 && config.QuickInfoMaxWidth < (double)_QuickInfoMaxWidthBox.Maximum ? config.QuickInfoMaxWidth : 0);
				_QuickInfoMaxHeightBox.Value = (decimal)(config.QuickInfoMaxHeight >= 0 && config.QuickInfoMaxHeight < (double)_QuickInfoMaxHeightBox.Maximum ? config.QuickInfoMaxHeight : 0);
			});
		}
	}
}
