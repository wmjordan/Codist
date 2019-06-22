using System;
using System.Drawing;
using System.Windows.Forms;
using AppHelpers;

namespace Codist.Options
{
	[System.ComponentModel.ToolboxItem(false)]
	public partial class SmartBarPage : UserControl
	{
		readonly UiLock _UI = new UiLock();
		bool _Loaded;

		public SmartBarPage() {
			InitializeComponent();
		}
		internal SmartBarPage(ConfigPage page) : this() {
			_UI.CommonEventAction += () => Config.Instance.FireConfigChangedEvent(Features.SmartBar);
		}
		private void SmartBarPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_AutoShowSmartBarBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SmartBarOptions.ManualDisplaySmartBar, _AutoShowSmartBarBox.Checked == false));
			_ToggleSmartBarBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SmartBarOptions.ShiftToggleDisplay, _ToggleSmartBarBox.Checked));
			_BrowserPathBox.TextChanged += _UI.HandleEvent(() => Config.Instance.BrowserPath = _BrowserPathBox.Text);
			_BrowserParameterBox.TextChanged += _UI.HandleEvent(() => Config.Instance.BrowserParameter = _BrowserParameterBox.Text);
			_BrowseBrowserButton.Click += (s, args) => {
				if (_BrowseBrowserDialog.ShowDialog() == DialogResult.OK) {
					_BrowserPathBox.Text = _BrowseBrowserDialog.FileName;
				}
			};
			Config.Updated += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				_AutoShowSmartBarBox.Checked = config.SmartBarOptions.MatchFlags(SmartBarOptions.ManualDisplaySmartBar) == false;
				_ToggleSmartBarBox.Checked = config.SmartBarOptions.MatchFlags(SmartBarOptions.ShiftToggleDisplay);
				_BrowserPathBox.Text = config.BrowserPath;
				_BrowserParameterBox.Text = config.BrowserParameter;
			});
		}
	}
}
