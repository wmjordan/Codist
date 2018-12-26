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
		StyleFilters _StyleFilters = StyleFilters.All;
		bool _Loaded;

		public SyntaxHighlightPage() {
			InitializeComponent();
		}
		internal SyntaxHighlightPage(ConfigPage page) : this() {
			_servicePage = page;
			_ColorBox.Checked = _FontFamilyBox.Checked = _FontSizeBox.Checked = _FontStyleBox.Checked = true;
			_ColorBox.CheckedChanged += (s, args) => _StyleFilters = _StyleFilters.SetFlags(StyleFilters.Color, _ColorBox.Checked);
			_FontFamilyBox.CheckedChanged += (s, args) => _StyleFilters = _StyleFilters.SetFlags(StyleFilters.FontFamily, _FontFamilyBox.Checked);
			_FontSizeBox.CheckedChanged += (s, args) => _StyleFilters = _StyleFilters.SetFlags(StyleFilters.FontSize, _FontSizeBox.Checked);
			_FontStyleBox.CheckedChanged += (s, args) => _StyleFilters = _StyleFilters.SetFlags(StyleFilters.FontStyle, _FontStyleBox.Checked);
			_UI.CommonEventAction += () => Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
		}
		private void SyntaxHighlightPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}
			LoadConfig(Config.Instance);

			_DarkThemeButton.Click += (s, args) => {
				LoadTheme(Config.DarkTheme);
			};
			_LightThemeButton.Click += (s, args) => {
				LoadTheme(Config.LightTheme);
			};
			_SimpleThemeButton.Click += (s, args) => {
				LoadTheme(Config.SimpleTheme);
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
							LoadTheme(d.FileName);
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

		void LoadTheme(string path) {
			if (_StyleFilters == StyleFilters.None) {
				MessageBox.Show("Select at least one style filter to apply the syntax theme.", nameof(Codist));
				return;
			}
			Config.LoadConfig(path, _StyleFilters);
		}
	}
}
