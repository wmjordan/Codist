using System;
using System.Windows.Forms;
using AppHelpers;

namespace Codist.Options
{
	[System.ComponentModel.ToolboxItem(false)]
	public partial class SyntaxHighlightPage : UserControl
	{
		readonly UiLock _UI = new UiLock();
		readonly ConfigPage _servicePage;
		bool _Loaded;

		public SyntaxHighlightPage() {
			InitializeComponent();
		}
		internal SyntaxHighlightPage(ConfigPage page) : this() {
			_servicePage = page;
			_UI.CommonEventAction += () => Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
		}
		private void SyntaxHighlightPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_DarkThemeButton.Click += (s, args) => {
				Config.LoadConfig(Config.DarkTheme, true);
			};
			_LightThemeButton.Click += (s, args) => {
				Config.LoadConfig(Config.LightTheme, true);
			};
			_SimpleThemeButton.Click += (s, args) => {
				Config.LoadConfig(Config.SimpleTheme, true);
			};
			_ResetThemeButton.Click += (s, args) => {
				if (MessageBox.Show("Do you want to reset the syntax highlight settings to default?", nameof(Codist), MessageBoxButtons.YesNo) == DialogResult.Yes) {
					Config.ResetStyles();
				}
			};
			_LoadButton.Click += (s, args) => {
				using (var d = new OpenFileDialog {
					Title = "Load Codist syntax highlight setting file...",
					FileName = "Codist.styles",
					DefaultExt = "styles",
					Filter = "Codist syntax highlight setting file|*.styles"
				}) {
					if (d.ShowDialog() == DialogResult.OK) {
						try {
							Config.LoadConfig(d.FileName, true);
						}
						catch (Exception ex) {
							MessageBox.Show("Error occured while loading style file: " + ex.Message, nameof(Codist));
						}
					}
				}
			};
			_SaveButton.Click += (s, args) => {
				using (var d = new SaveFileDialog {
					Title = "Save Codist syntax highlight setting file...",
					FileName = "Codist.styles",
					DefaultExt = "styles",
					Filter = "Codist syntax highlight setting file|*.styles"
				}) {
					if (d.ShowDialog() == DialogResult.OK) {
						Config.Instance.SaveConfig(d.FileName, true);
					}
				}
			};
			_HighlightSpecialCommentBox.CheckedChanged += _UI.HandleEvent(() => Config.Instance.Set(SpecialHighlightOptions.SpecialComment, _HighlightSpecialCommentBox.Checked));
			Config.Updated += (s, args) => LoadConfig(s as Config);
			_Loaded = true;
		}

		void LoadConfig(Config config) {
			_UI.DoWithLock(() => {
				_HighlightSpecialCommentBox.Checked = config.SpecialHighlightOptions.MatchFlags(SpecialHighlightOptions.SpecialComment);
			});
		}
	}
}
