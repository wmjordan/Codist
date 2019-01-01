using System;
using System.ComponentModel;
using System.Windows.Forms;
using AppHelpers;

namespace Codist.Options
{
	[ToolboxItem(false)]
	public partial class CSharpSuperQuickInfoPage : UserControl
	{
		readonly UiLock _UI = new UiLock();
		bool _Loaded;

		public CSharpSuperQuickInfoPage() {
			InitializeComponent();
		}
		internal CSharpSuperQuickInfoPage(ConfigPage page) : this() {
			_UI.CommonEventAction += () => Config.Instance.FireConfigChangedEvent(Features.SuperQuickInfo);
		}

		void CSharpSuperQuickInfoPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_AlternativeStyleBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.AlternativeStyle, _AlternativeStyleBox.Checked));
			_ClickAndGoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.ClickAndGo, _ClickAndGoBox.Checked));
			_CSharpAttributesQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.Attributes, _CSharpAttributesQuickInfoBox.Checked));
			_CSharpBaseTypeQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.BaseType, _CSharpBaseTypeInheritenceQuickInfoBox.Enabled = _CSharpBaseTypeQuickInfoBox.Checked));
			_CSharpBaseTypeInheritenceQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.BaseTypeInheritence, _CSharpBaseTypeInheritenceQuickInfoBox.Checked));
			_CSharpOverrideDefaultXmlDocBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(
				QuickInfoOptions.OverrideDefaultDocumentation,
				_CSharpDocumentationBaseTypeBox.Enabled
					= _CSharpInheritDocCrefBox.Enabled
					= _CSharpTextOnlyDocBox.Enabled
					= _CSharpReturnsDocBox.Enabled
					= _CSharpRemarksDocBox.Enabled
					= _CSharpExceptionDocBox.Enabled
					= _CSharpOverrideDefaultXmlDocBox.Checked));
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
			_CSharpInheritDocCrefBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.DocumentationFromInheritDoc, _CSharpInheritDocCrefBox.Checked));
			_CSharpReturnsDocBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.ReturnsDoc, _CSharpReturnsDocBox.Checked));
			_CSharpRemarksDocBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.RemarksDoc, _CSharpRemarksDocBox.Checked));
			_CSharpExceptionDocBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.ExceptionDoc, _CSharpExceptionDocBox.Checked));
			_CSharpTextOnlyDocBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.TextOnlyDoc, _CSharpTextOnlyDocBox.Checked));
			_QuickInfoMaxWidthBox.ValueChanged += _UI.HandleEvent(() => Config.Instance.QuickInfoMaxWidth = (double)_QuickInfoMaxWidthBox.Value);
			_QuickInfoMaxHeightBox.ValueChanged += _UI.HandleEvent(() => Config.Instance.QuickInfoMaxHeight = (double)_QuickInfoMaxHeightBox.Value);

			Config.Loaded += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				var o = config.QuickInfoOptions;
				_AlternativeStyleBox.Checked = o.MatchFlags(QuickInfoOptions.AlternativeStyle);
				_ClickAndGoBox.Checked = o.MatchFlags(QuickInfoOptions.ClickAndGo);
				_CSharpAttributesQuickInfoBox.Checked = o.MatchFlags(QuickInfoOptions.Attributes);
				_CSharpBaseTypeQuickInfoBox.Checked
					= _CSharpBaseTypeInheritenceQuickInfoBox.Enabled
					= o.MatchFlags(QuickInfoOptions.BaseType);
				_CSharpBaseTypeInheritenceQuickInfoBox.Checked = o.MatchFlags(QuickInfoOptions.BaseTypeInheritence);
				_CSharpDeclarationQuickInfoBox.Checked = o.MatchFlags(QuickInfoOptions.Declaration);
				_CSharpInterfacesQuickInfoBox.Checked
					= _CSharpInterfaceInheritenceQuickInfoBox.Enabled
					= o.MatchFlags(QuickInfoOptions.Interfaces);
				_CSharpInterfaceInheritenceQuickInfoBox.Checked = o.MatchFlags(QuickInfoOptions.InterfacesInheritence);
				_CSharpInterfaceImplementationsQuickInfoBox.Checked = o.MatchFlags(QuickInfoOptions.InterfaceImplementations);
				_CSharpSymbolLocationQuickInfoBox.Checked = o.MatchFlags(QuickInfoOptions.SymbolLocation);
				_CSharpReturnsDocBox.Checked = o.MatchFlags(QuickInfoOptions.ReturnsDoc);
				_CSharpRemarksDocBox.Checked = o.MatchFlags(QuickInfoOptions.RemarksDoc);
				_CSharpExceptionDocBox.Checked = o.MatchFlags(QuickInfoOptions.ExceptionDoc);
				_CSharpOverrideDefaultXmlDocBox.Checked
					= _CSharpDocumentationBaseTypeBox.Enabled
					= _CSharpInheritDocCrefBox.Enabled
					= _CSharpTextOnlyDocBox.Enabled
					= _CSharpReturnsDocBox.Enabled
					= _CSharpRemarksDocBox.Enabled
					= _CSharpExceptionDocBox.Enabled
					= o.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation);
				_CSharpNumberQuickInfoBox.Checked = o.MatchFlags(QuickInfoOptions.NumericValues);
				_CSharpStringQuickInfoBox.Checked = o.MatchFlags(QuickInfoOptions.String);
				_CSharpParameterQuickInfoBox.Checked = o.MatchFlags(QuickInfoOptions.Parameter);
				_CSharpTypeParameterQuickInfoBox.Checked = o.MatchFlags(QuickInfoOptions.TypeParameters);
				_CSharpDocumentationBaseTypeBox.Checked = o.MatchFlags(QuickInfoOptions.DocumentationFromBaseType);
				_CSharpInheritDocCrefBox.Checked = o.MatchFlags(QuickInfoOptions.DocumentationFromInheritDoc);
				_CSharpTextOnlyDocBox.Checked = o.MatchFlags(QuickInfoOptions.TextOnlyDoc);
				_QuickInfoMaxWidthBox.Value = (decimal)(config.QuickInfoMaxWidth >= 0 && config.QuickInfoMaxWidth < (double)_QuickInfoMaxWidthBox.Maximum ? config.QuickInfoMaxWidth : 0);
				_QuickInfoMaxHeightBox.Value = (decimal)(config.QuickInfoMaxHeight >= 0 && config.QuickInfoMaxHeight < (double)_QuickInfoMaxHeightBox.Maximum ? config.QuickInfoMaxHeight : 0);
			});
		}
	}
}
