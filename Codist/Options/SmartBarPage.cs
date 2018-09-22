using System;
using System.Drawing;
using System.Windows.Forms;
using AppHelpers;

namespace Codist.Options
{
	public partial class SmartBarPage : UserControl
	{
		readonly UiLock _UI = new UiLock();
		bool _Loaded;

		public SmartBarPage() {
			InitializeComponent();
		}
		internal SmartBarPage(ConfigPage page) : this() {
		}
		private void SmartBarPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_ControlSmartBarBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SmartBarOptions.DisplayOnShiftPressed, _ControlSmartBarBox.Checked));

			Config.Updated += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				_ControlSmartBarBox.Checked = config.SmartBarOptions.MatchFlags(SmartBarOptions.DisplayOnShiftPressed);
			});
		}
	}
}
