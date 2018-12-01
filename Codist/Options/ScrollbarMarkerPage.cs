using System;
using System.Drawing;
using System.Windows.Forms;
using AppHelpers;

namespace Codist.Options
{
	[System.ComponentModel.ToolboxItem(false)]
	public partial class ScrollbarMarkerPage : UserControl
	{
		readonly UiLock _UI = new UiLock();
		bool _Loaded;

		public ScrollbarMarkerPage() {
			InitializeComponent();
		}
		internal ScrollbarMarkerPage(ConfigPage page) : this() {
			_UI.CommonEventAction += () => Config.Instance.FireConfigChangedEvent(Features.ScrollbarMarkers);
		}

		void ScrollbarMarkerPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_LineNumbersBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(MarkerOptions.LineNumber, _LineNumbersBox.Checked));
			Config.Updated += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				_LineNumbersBox.Checked = config.MarkerOptions.MatchFlags(MarkerOptions.LineNumber);
			});
		}
	}
}
