using System;
using System.Windows.Forms;

namespace Codist.Options
{
	public partial class SyntaxHighlightPage : UserControl
	{
		//readonly UiLock _UI = new UiLock();
		readonly ConfigPage _servicePage;
		bool _Loaded;

		public SyntaxHighlightPage() {
			InitializeComponent();
		}
		internal SyntaxHighlightPage(ConfigPage page) : this() {
			_servicePage = page;
		}
		private void SyntaxHighlightPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			//LoadConfig(Config.Instance);
			_SyntaxHighlightTabs.AddPage("Common Syntax", new SyntaxStyleOptionPage(_servicePage, () => Config.Instance.GeneralStyles, Config.GetDefaultCodeStyles), false);

			_DarkThemeButton.Click += (s, args) => {
				Config.LoadConfig(Config.DarkTheme);
			};
			_LightThemeButton.Click += (s, args) => {
				Config.LoadConfig(Config.LightTheme);
			};
			_ResetThemeButton.Click += (s, args) => {
				if (MessageBox.Show("Do you want to reset the syntax highlight settings to default?", nameof(Codist), MessageBoxButtons.YesNo) == DialogResult.Yes) {
					Config.ResetStyles();
				}
			};
			//Config.Updated += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		//void LoadConfig(Config config) {
		//}
	}
}
