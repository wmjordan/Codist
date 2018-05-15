using System;
using System.Drawing;
using System.Windows.Forms;

namespace Codist.Options
{
	public partial class SyntaxHighlightPage : UserControl
	{
		readonly UiLock _UI = new UiLock();
		bool _Loaded;

		public SyntaxHighlightPage() {
			InitializeComponent();
		}
		internal SyntaxHighlightPage(ConfigPage page) : this() {
		}
		private void SyntaxHighlightPage_Load(object sender, EventArgs e) {
			if (_Loaded) {
				return;
			}

			_SaveConfigButton.Click += (s, args) => {
				using (var d = new SaveFileDialog {
					Title = "Save Codist configuration file...",
					FileName = "Codist.json",
					DefaultExt = "json",
					Filter = "Codist configuration file|*.json"
				}) {
					if (d.ShowDialog() != DialogResult.OK) {
						return;
					}
					Config.Instance.SaveConfig(d.FileName);
				}
			};
			_LoadConfigButton.Click += (s, args) => {
				_ThemeMenu.Show(_LoadConfigButton, new Point(0, _LoadConfigButton.Height));
			};
			_ResetConfigButton.Click += (s, args) => {
				if (MessageBox.Show("Do you want to reset the syntax highlight settings to default?", nameof(Codist), MessageBoxButtons.YesNo) == DialogResult.Yes) {
					Config.ResetStyles();
				}
			};
			_ThemeMenu.ItemClicked += (s, args) => {
				switch (args.ClickedItem.Tag) {
					case "Light": Config.LoadConfig(Config.LightTheme); return;
					case "Dark": Config.LoadConfig(Config.DarkTheme); return;
				}
				_ThemeMenu.Close();
				using (var d = new OpenFileDialog {
					Title = "Load Codist configuration file...",
					FileName = "Codist.json",
					DefaultExt = "json",
					Filter = "Codist configuration file|*.json"
				}) {
					if (d.ShowDialog() != DialogResult.OK) {
						return;
					}
					try {
						Config.LoadConfig(d.FileName);
						System.IO.File.Copy(d.FileName, Config.ConfigPath, true);
					}
					catch (Exception ex) {
						MessageBox.Show("Error occured while loading config file: " + ex.Message, nameof(Codist));
					}
				}
			};
			_Loaded = true;
		}

	}
}
