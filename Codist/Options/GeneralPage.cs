using System;
using System.Drawing;
using System.Windows.Forms;

namespace Codist.Options
{
	public partial class GeneralPage : UserControl
	{
		readonly UiLock _UI = new UiLock();
		bool _Loaded;

		public GeneralPage() {
			InitializeComponent();
		}
		internal GeneralPage(ConfigPage page) : this() {
		}
		private void MiscPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			_TopMarginBox.Value = (decimal)LineTransformers.LineHeightTransformProvider.TopSpace;
			_BottomMarginBox.Value = (decimal)LineTransformers.LineHeightTransformProvider.BottomSpace;
			LoadConfig(Config.Instance);

			_GlobalFeatureBox.Font = new Font(_GlobalFeatureBox.Font.FontFamily, _GlobalFeatureBox.Font.Size * 1.5f);
			_GlobalFeatureBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Features = _GlobalFeatureBox.Checked ? Features.All : Features.None);
			_TopMarginBox.ValueChanged += _UI.HandleEvent(() => {
				LineTransformers.LineHeightTransformProvider.TopSpace = (double)_TopMarginBox.Value;
				Config.Instance.FireConfigChangedEvent();
			});
			_BottomMarginBox.ValueChanged += _UI.HandleEvent(() => {
				LineTransformers.LineHeightTransformProvider.BottomSpace = (double)_BottomMarginBox.Value;
				Config.Instance.FireConfigChangedEvent();
			});
			_NoSpaceBetweenWrappedLinesBox.CheckedChanged += _UI.HandleEvent(() => {
				Config.Instance.NoSpaceBetweenWrappedLines = _NoSpaceBetweenWrappedLinesBox.Checked;
				Config.Instance.FireConfigChangedEvent();
			});
			Config.Updated += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				_GlobalFeatureBox.Checked = config.Features != Features.None;
				_NoSpaceBetweenWrappedLinesBox.Checked = config.NoSpaceBetweenWrappedLines;
			});
		}
	}
}
