using System;
using System.Drawing;
using System.Windows.Forms;
using AppHelpers;

namespace Codist.Options
{
	public partial class SuperQuickInfoPage : UserControl
	{
		readonly UiLock _UI = new UiLock();
		bool _Loaded;

		public SuperQuickInfoPage() {
			InitializeComponent();
		}
		internal SuperQuickInfoPage(ConfigPage page) : this() {
		}
		private void SuperQuickInfoPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_ControlQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.CtrlQuickInfo, _ControlQuickInfoBox.Checked));
			_SelectionQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.Selection, _SelectionQuickInfoBox.Checked));
			_ColorQuickInfoBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(QuickInfoOptions.Color, _ColorQuickInfoBox.Checked));

			Config.Updated += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				_ControlQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.CtrlQuickInfo);
				_SelectionQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.Selection);
				_ColorQuickInfoBox.Checked = config.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color);
			});
		}
	}
}
