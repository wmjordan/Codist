using System;
using System.Windows.Forms;
using AppHelpers;

namespace Codist.Options
{
	[System.ComponentModel.ToolboxItem(false)]
	public partial class SuperQuickInfoPage : UserControl
	{
		readonly UiLock _UI = new UiLock();
		bool _Loaded;

		public SuperQuickInfoPage() {
			InitializeComponent();
		}
		internal SuperQuickInfoPage(ConfigPage page) : this() {
			_UI.PostEventAction += () => Config.Instance.FireConfigChangedEvent(Features.SuperQuickInfo);
		}
		private void SuperQuickInfoPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_ControlQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.CtrlQuickInfo, _ControlQuickInfoBox.Checked));
			_SelectionQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.Selection, _SelectionQuickInfoBox.Checked));
			_ColorQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.Color, _ColorQuickInfoBox.Checked));
			_QuickInfoMaxWidthBox.ValueChanged += _UI.HandleEvent(() => Config.Instance.QuickInfoMaxWidth = (double)_QuickInfoMaxWidthBox.Value);
			_QuickInfoMaxHeightBox.ValueChanged += _UI.HandleEvent(() => Config.Instance.QuickInfoMaxHeight = (double)_QuickInfoMaxHeightBox.Value);
			_QuickInfoXmlDocExtraHeightBox.ValueChanged += _UI.HandleEvent(() => Config.Instance.QuickInfoXmlDocExtraHeight = (double)_QuickInfoXmlDocExtraHeightBox.Value);

			Config.Updated += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				_ControlQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.CtrlQuickInfo);
				_SelectionQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.Selection);
				_ColorQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color);
				_QuickInfoMaxWidthBox.Value = (decimal)(config.QuickInfoMaxWidth >= 0 && config.QuickInfoMaxWidth < (double)_QuickInfoMaxWidthBox.Maximum ? config.QuickInfoMaxWidth : 0);
				_QuickInfoMaxHeightBox.Value = (decimal)(config.QuickInfoMaxHeight >= 0 && config.QuickInfoMaxHeight < (double)_QuickInfoMaxHeightBox.Maximum ? config.QuickInfoMaxHeight : 0);
				_QuickInfoXmlDocExtraHeightBox.Value = (decimal)(config.QuickInfoXmlDocExtraHeight >= 0 && config.QuickInfoXmlDocExtraHeight < (double)_QuickInfoXmlDocExtraHeightBox.Maximum ? config.QuickInfoXmlDocExtraHeight : 0);
			});
		}
	}
}
