using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

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
			_CSharpAttributesQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.ShowAttributesQuickInfo = _CSharpAttributesQuickInfoBox.Checked);
			_CSharpBaseTypeQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => _CSharpBaseTypeInheritenceQuickInfoBox.Enabled = Config.Instance.ShowBaseTypeQuickInfo = _CSharpBaseTypeQuickInfoBox.Checked);
			_CSharpBaseTypeInheritenceQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.ShowBaseTypeInheritenceQuickInfo = _CSharpBaseTypeInheritenceQuickInfoBox.Checked);
			_CSharpExtensionMethodQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.ShowExtensionMethodQuickInfo = _CSharpExtensionMethodQuickInfoBox.Checked);
			_CSharpInterfacesQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => _CSharpInterfaceInheritenceQuickInfoBox.Enabled = Config.Instance.ShowInterfacesQuickInfo = _CSharpInterfacesQuickInfoBox.Checked);
			_CSharpInterfaceInheritenceQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.ShowInterfacesInheritenceQuickInfo = _CSharpInterfaceInheritenceQuickInfoBox.Checked);
			_CSharpNumberQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.ShowNumericQuickInfo = _CSharpNumberQuickInfoBox.Checked);
			_CSharpStringQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.ShowStringQuickInfo = _CSharpStringQuickInfoBox.Checked);

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
				_CSharpAttributesQuickInfoBox.Checked = config.ShowAttributesQuickInfo;
				_CSharpBaseTypeInheritenceQuickInfoBox.Enabled = _CSharpBaseTypeQuickInfoBox.Checked = config.ShowBaseTypeQuickInfo;
				_CSharpBaseTypeInheritenceQuickInfoBox.Checked = config.ShowBaseTypeInheritenceQuickInfo;
				_CSharpInterfaceInheritenceQuickInfoBox.Enabled = _CSharpInterfacesQuickInfoBox.Checked = config.ShowInterfacesQuickInfo;
				_CSharpInterfaceInheritenceQuickInfoBox.Checked = config.ShowInterfacesInheritenceQuickInfo;
				_CSharpExtensionMethodQuickInfoBox.Checked = config.ShowExtensionMethodQuickInfo;
				_CSharpNumberQuickInfoBox.Checked = config.ShowNumericQuickInfo;
				_CSharpStringQuickInfoBox.Checked = config.ShowStringQuickInfo;
			});
		}
	}
}
